using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace NOLoader.Telemetry
{
    /// <summary>UDP Sim-Connect style telemetry — Aircraft, PilotDismounted, extended NO2 fields.</summary>
    public static class TelemetryService
    {
        private enum UnitKind
        {
            None = 0,
            Aircraft = 1,
            PilotDismounted = 2
        }

        private struct Snapshot
        {
            public bool Valid;
            public UnitKind Kind;
            public float Px, Py, Pz;
            public float Vx, Vy, Vz;
            public float Pitch, Roll, Yaw;
            public float GLoad;
            public float FuelLevel;
            public int ActiveStation;
            public int WeaponNameHash;
            public int DefinitionKeyHash;
            public int Ammo;
        }

        private static UdpClient? _client;
        private static Thread? _thread;
        private static volatile bool _running;
        private static Snapshot _snapshot;

        private static Type? _aircraftType;
        private static Type? _pilotDismountedType;
        private static Type? _gameManagerType;
        private static MethodInfo? _getLocalAircraft;
        private static MethodInfo? _getLocalPilotDismounted;
        private static PropertyInfo? _aircraftRbProperty;
        private static FieldInfo? _aircraftRbField;
        private static FieldInfo? _aircraftGForce;
        private static FieldInfo? _aircraftFuelLevel;
        private static FieldInfo? _weaponManagerField;
        private static FieldInfo? _currentWeaponStationField;
        private static FieldInfo? _weaponStationAmmoField;
        private static FieldInfo? _weaponStationInfoField;
        private static FieldInfo? _weaponInfoNameField;
        private static FieldInfo? _unitDefinitionField;
        private static FieldInfo? _jsonKeyField;
        private static FieldInfo? _pilotRbField;
        private static FieldInfo? _weaponStationNumberField;
        private static bool _bindingsReady;
        private static int _cachedWeaponHash;
        private static object? _cachedWeaponInfo;

        public static int Port { get; set; } = 49000;

        public static bool BindingsReady => _bindingsReady;

        public static bool HasValidSnapshot => _snapshot.Valid;

        public static void Initialize(string gameRoot)
        {
            try
            {
                _client = new UdpClient();
                _client.Connect(IPAddress.Loopback, Port);
                _running = true;
                _thread = new Thread(TelemetryLoop)
                {
                    IsBackground = true,
                    Name = "NOLoader.Telemetry",
                    Priority = System.Threading.ThreadPriority.Lowest
                };
                _thread.Start();
            }
            catch
            {
                _running = false;
            }
        }

        public static void Shutdown()
        {
            _running = false;
            _client?.Close();
        }

        public static void CaptureOnMainThread()
        {
            if (!Application.isPlaying)
            {
                _snapshot = default;
                return;
            }

            try
            {
                EnsureBindings();
                if (!_bindingsReady || _gameManagerType == null)
                {
                    _snapshot = default;
                    return;
                }

                if (TryCaptureAircraft(out Snapshot aircraftSnap))
                {
                    _snapshot = aircraftSnap;
                    return;
                }

                if (TryCapturePilotDismounted(out Snapshot pilotSnap))
                {
                    _snapshot = pilotSnap;
                    return;
                }

                _snapshot = default;
            }
            catch
            {
                _snapshot = default;
            }
        }

        private static bool TryCaptureAircraft(out Snapshot snap)
        {
            snap = default;
            if (_aircraftType == null || _getLocalAircraft == null)
                return false;

            object?[] args = { null };
            if (_getLocalAircraft.Invoke(null, args) is not bool ok || !ok || args[0] is not object aircraft)
                return false;

            Rigidbody? rb = ResolveRigidbody(aircraft);
            if (rb == null)
                return false;

            Vector3 pos = rb.position;
            Vector3 vel = rb.velocity;
            Vector3 euler = rb.rotation.eulerAngles;

            float gLoad = _aircraftGForce?.GetValue(aircraft) is float g ? g : 0f;
            float fuel = _aircraftFuelLevel?.GetValue(aircraft) is float f ? f : 0f;
            ReadWeaponState(aircraft, out int station, out int weaponHash, out int ammo);
            int defHash = ReadDefinitionKeyHash(aircraft);

            snap = new Snapshot
            {
                Valid = true,
                Kind = UnitKind.Aircraft,
                Px = pos.x,
                Py = pos.y,
                Pz = pos.z,
                Vx = vel.x,
                Vy = vel.y,
                Vz = vel.z,
                Pitch = euler.x,
                Roll = euler.z,
                Yaw = euler.y,
                GLoad = gLoad,
                FuelLevel = fuel,
                ActiveStation = station,
                WeaponNameHash = weaponHash,
                DefinitionKeyHash = defHash,
                Ammo = ammo
            };
            return true;
        }

        private static bool TryCapturePilotDismounted(out Snapshot snap)
        {
            snap = default;
            if (_pilotDismountedType == null || _getLocalPilotDismounted == null)
                return false;

            object?[] args = { null };
            if (_getLocalPilotDismounted.Invoke(null, args) is not bool ok || !ok || args[0] is not object pilot)
                return false;

            Rigidbody? rb = _pilotRbField?.GetValue(pilot) as Rigidbody;
            if (rb == null)
                return false;

            Vector3 pos = rb.position;
            Vector3 vel = rb.velocity;
            Vector3 euler = rb.rotation.eulerAngles;

            snap = new Snapshot
            {
                Valid = true,
                Kind = UnitKind.PilotDismounted,
                Px = pos.x,
                Py = pos.y,
                Pz = pos.z,
                Vx = vel.x,
                Vy = vel.y,
                Vz = vel.z,
                Pitch = euler.x,
                Roll = euler.z,
                Yaw = euler.y,
                DefinitionKeyHash = ReadDefinitionKeyHash(pilot)
            };
            return true;
        }

        private static int ReadDefinitionKeyHash(object unit)
        {
            object? definition = _unitDefinitionField?.GetValue(unit);
            if (definition == null)
                return 0;

            if (_jsonKeyField?.GetValue(definition) is string jsonKey && !string.IsNullOrEmpty(jsonKey))
                return NOLoader.API.StringHash.Murmur32(jsonKey);

            PropertyInfo? jsonKeyProp = definition.GetType().GetProperty("JsonKey");
            if (jsonKeyProp?.GetValue(definition) is string key && !string.IsNullOrEmpty(key))
                return NOLoader.API.StringHash.Murmur32(key);

            return 0;
        }

        private static void EnsureBindings()
        {
            if (_bindingsReady)
                return;

            _aircraftType = ResolveType("Aircraft");
            _pilotDismountedType = ResolveType("PilotDismounted");
            _gameManagerType = ResolveType("GameManager");
            if (_gameManagerType == null)
                return;

            if (_aircraftType != null)
            {
                _getLocalAircraft = _gameManagerType.GetMethod(
                    "GetLocalAircraft",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { _aircraftType.MakeByRefType() },
                    null);

                _aircraftRbProperty = FindInstanceProperty(_aircraftType, "rb") ?? FindInstanceProperty(_aircraftType, "RB");
                _aircraftRbField = FindInstanceField(_aircraftType, "rb") ?? FindInstanceField(_aircraftType, "RB");
                _aircraftGForce = FindInstanceField(_aircraftType, "gForce");
                _aircraftFuelLevel = FindInstanceField(_aircraftType, "fuelLevel");
                _weaponManagerField = FindInstanceField(_aircraftType, "weaponManager");
            }

            if (_pilotDismountedType != null)
            {
                _getLocalPilotDismounted = _gameManagerType.GetMethod(
                    "GetLocalPilotDismounted",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { _pilotDismountedType.MakeByRefType() },
                    null);
                _pilotRbField = FindInstanceField(_pilotDismountedType, "rb") ?? FindInstanceField(_pilotDismountedType, "RB");
            }

            Type? unitType = ResolveType("Unit");
            if (unitType != null)
                _unitDefinitionField = FindInstanceField(unitType, "definition");

            Type? hasJsonKey = ResolveType("IHasJsonKey");
            _jsonKeyField = hasJsonKey?.GetField("jsonKey", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type? weaponManagerType = ResolveType("WeaponManager");
            Type? weaponStationType = ResolveType("WeaponStation");
            Type? weaponInfoType = ResolveType("WeaponInfo");

            if (weaponManagerType != null)
            {
                _currentWeaponStationField = weaponManagerType.GetField(
                    "currentWeaponStation",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (weaponStationType != null)
            {
                _weaponStationAmmoField = weaponStationType.GetField("Ammo", BindingFlags.Public | BindingFlags.Instance);
                _weaponStationInfoField = weaponStationType.GetField("WeaponInfo", BindingFlags.Public | BindingFlags.Instance);
            }

            if (weaponInfoType != null)
            {
                _weaponInfoNameField = weaponInfoType.GetField("weaponName", BindingFlags.Public | BindingFlags.Instance);
            }

            if (weaponStationType != null)
            {
                _weaponStationNumberField = weaponStationType.GetField("Number", BindingFlags.Public | BindingFlags.Instance);
            }

            _bindingsReady = _getLocalAircraft != null || _getLocalPilotDismounted != null;
        }

        private static Rigidbody? ResolveRigidbody(object aircraft)
        {
            object? rbObj = _aircraftRbProperty?.GetValue(aircraft) ?? _aircraftRbField?.GetValue(aircraft);
            return rbObj as Rigidbody;
        }

        private static void ReadWeaponState(object aircraft, out int station, out int weaponHash, out int ammo)
        {
            station = -1;
            weaponHash = 0;
            ammo = 0;

            object? weaponManager = _weaponManagerField?.GetValue(aircraft);
            if (weaponManager == null || _currentWeaponStationField == null)
                return;

            object? currentStation = _currentWeaponStationField.GetValue(weaponManager);
            if (currentStation == null)
                return;

            if (_weaponStationNumberField?.GetValue(currentStation) is byte num)
                station = num;
            else if (_weaponStationNumberField?.GetValue(currentStation) is int numInt)
                station = numInt;

            if (_weaponStationAmmoField?.GetValue(currentStation) is int ammoVal)
                ammo = ammoVal;

            object? weaponInfo = _weaponStationInfoField?.GetValue(currentStation);
            if (weaponInfo == null)
                return;

            if (ReferenceEquals(weaponInfo, _cachedWeaponInfo))
            {
                weaponHash = _cachedWeaponHash;
                return;
            }

            _cachedWeaponInfo = weaponInfo;
            if (_weaponInfoNameField?.GetValue(weaponInfo) is string weaponName && !string.IsNullOrEmpty(weaponName))
                _cachedWeaponHash = weaponHash = NOLoader.API.StringHash.Murmur32(weaponName);
            else
                _cachedWeaponHash = weaponHash = 0;
        }

        private static void TelemetryLoop()
        {
            while (_running)
            {
                try
                {
                    string packet = BuildPacket();
                    if (!string.IsNullOrEmpty(packet) && _client != null)
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(packet);
                        _client.Send(bytes, bytes.Length);
                    }
                }
                catch { /* ignore */ }

                Thread.Sleep(33);
            }
        }

        private static string BuildPacket()
        {
            Snapshot snap = _snapshot;
            if (!snap.Valid)
                return string.Empty;

            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "NO2,{0:F2},{1:F2},{2:F2},{3:F2},{4:F2},{5:F2},{6:F2},{7:F2},{8:F2},{9:F2},{10},{11},{12},{13},{14:F2},{15}",
                snap.Px, snap.Py, snap.Pz,
                snap.Vx, snap.Vy, snap.Vz,
                snap.Pitch, snap.Roll, snap.Yaw,
                snap.GLoad,
                snap.ActiveStation,
                snap.WeaponNameHash,
                snap.Ammo,
                (int)snap.Kind,
                snap.DefinitionKeyHash,
                snap.FuelLevel);
        }

        private static FieldInfo? FindInstanceField(Type type, string name)
            => type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        private static PropertyInfo? FindInstanceProperty(Type type, string name)
            => type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        private static Type? ResolveType(string name)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = asm.GetType(name, throwOnError: false);
                if (t != null)
                    return t;
            }
            return null;
        }
    }
}
