using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NOLoader.API;
using NOLoader.API.World;
using NOLoader.Core.Interop;
using NOLoader.Core.Logging;
using UnityEngine;

namespace NOLoader.Core.Runtime.Perf
{
    internal sealed class WorldSnapshotService : INOModWorldReader
    {
        public static readonly WorldSnapshotService Instance = new WorldSnapshotService();

        private NOWorldUnit[] _units = Array.Empty<NOWorldUnit>();
        private int _count;
        private int _frameId;
        private bool _active;
        private int _stride;
        private int _frameCounter;
        private Type? _unitType;
        private Type? _aircraftType;
        private FieldInfo? _allUnitsField;
        private MethodInfo? _globalPositionMethod;
        private PropertyInfo? _rbProp;
        private PropertyInfo? _rbVelocityProp;
        private MethodInfo? _getLocalAircraft;
        private object? _localAircraft;
        private int _localAircraftId;

        public bool IsActive => _active;

        public int FrameId => _frameId;

        public int UnitCount => _count;

        public NOWorldUnit GetUnit(int index)
        {
            if (index < 0 || index >= _count)
                return default;
            return _units[index];
        }

        public INOModWorldReader Activate()
        {
            _active = true;
            NOModRuntime.World = this;
            ResolveGameApi();
            ModRuntimeHost.EnsureInstalled();
            return this;
        }

        public void Tick()
        {
            if (!_active)
                return;

            _stride = RuntimeConfig.WorldSnapshotStride;
            if (_stride < 1)
                _stride = 1;

            _frameCounter++;
            if ((_frameCounter % _stride) != 0)
                return;

            Refresh();
        }

        private void Refresh()
        {
            if (_allUnitsField == null)
            {
                ResolveGameApi();
                if (_allUnitsField == null)
                    return;
            }

            object? listObj = _allUnitsField.GetValue(null);
            if (listObj is not IList list)
                return;

            RefreshLocalAircraft();
            int needed = list.Count;
            if (_units.Length < needed)
            {
                int newCap = needed < 64 ? 64 : needed * 2;
                _units = new NOWorldUnit[newCap];
            }

            int written = 0;
            for (int i = 0; i < list.Count; i++)
            {
                object? unit = list[i];
                if (unit == null)
                    continue;

                if (!TryBuildUnit(unit, out NOWorldUnit snapshot))
                    continue;

                _units[written++] = snapshot;
            }

            _count = written;
            _frameId++;
        }

        private void RefreshLocalAircraft()
        {
            _localAircraft = null;
            _localAircraftId = 0;
            if (_getLocalAircraft == null)
                return;

            object?[] args = { null };
            try
            {
                if (_getLocalAircraft.Invoke(null, args) is bool ok && ok && args[0] != null)
                {
                    _localAircraft = args[0];
                    _localAircraftId = ReadInstanceId(_localAircraft);
                }
            }
            catch
            {
                _localAircraft = null;
            }
        }

        private bool TryBuildUnit(object unit, out NOWorldUnit snapshot)
        {
            snapshot = default;
            int unitId = ReadInstanceId(unit);
            if (unitId == 0)
                return false;

            NOVec3 pos = default;
            if (_globalPositionMethod != null)
            {
                try
                {
                    object? gp = _globalPositionMethod.Invoke(unit, null);
                    pos = ToVec3(gp);
                }
                catch
                {
                    return false;
                }
            }

            NOVec3 vel = default;
            if (_rbProp != null && _rbVelocityProp != null)
            {
                try
                {
                    object? rb = _rbProp.GetValue(unit);
                    if (rb != null)
                    {
                        object? v = _rbVelocityProp.GetValue(rb);
                        vel = ToVec3(v);
                    }
                }
                catch
                {
                    vel = default;
                }
            }

            int teamId = 0;
            try
            {
                PropertyInfo? hqProp = _unitType?.GetProperty("NetworkHQ", BindingFlags.Public | BindingFlags.Instance);
                object? hq = hqProp?.GetValue(unit);
                if (hq is UnityEngine.Object uo)
                    teamId = uo.GetInstanceID();
            }
            catch
            {
                teamId = 0;
            }

            bool isLocal = _localAircraft != null && ReferenceEquals(unit, _localAircraft);
            snapshot = new NOWorldUnit(unitId, pos, vel, isLocal, teamId);
            return true;
        }

        private static int ReadInstanceId(object unit)
        {
            if (unit is UnityEngine.Object uo)
                return uo.GetInstanceID();
            return 0;
        }

        private static NOVec3 ToVec3(object? value)
        {
            if (value == null)
                return default;

            Type t = value.GetType();
            float x = ReadComponent(t, value, "x");
            float y = ReadComponent(t, value, "y");
            float z = ReadComponent(t, value, "z");
            return new NOVec3(x, y, z);
        }

        private static float ReadComponent(Type t, object value, string name)
        {
            FieldInfo? field = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(float))
                return (float)field.GetValue(value)!;

            PropertyInfo? prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(float))
                return (float)prop.GetValue(value)!;

            return 0f;
        }

        private void ResolveGameApi()
        {
            _unitType = GameTypeCache.Resolve("Unit");
            _aircraftType = GameTypeCache.Resolve("Aircraft");
            Type? registry = GameTypeCache.Resolve("UnitRegistry");
            if (registry != null)
                _allUnitsField = registry.GetField("allUnits", BindingFlags.Public | BindingFlags.Static);

            if (_unitType != null)
            {
                _globalPositionMethod = _unitType.GetMethod("GlobalPosition", BindingFlags.Public | BindingFlags.Instance);
                _rbProp = _unitType.GetProperty("rb", BindingFlags.Public | BindingFlags.Instance);
            }

            if (_rbProp != null)
            {
                Type? rbType = _rbProp.PropertyType;
                _rbVelocityProp = rbType?.GetProperty("velocity", BindingFlags.Public | BindingFlags.Instance);
            }

            Type? gameManager = GameTypeCache.Resolve("GameManager");
            if (gameManager != null)
            {
                MethodInfo? method = gameManager.GetMethod("GetLocalAircraft", BindingFlags.Public | BindingFlags.Static);
                if (method != null && method.IsGenericMethod == false)
                    _getLocalAircraft = method;
            }
        }
    }
}
