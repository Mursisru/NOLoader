using System;
using System.Reflection;
using UnityEngine;

namespace NOLoader.Registry
{
    /// <summary>Gate L3 Unity prefab checks — editor-level collider/mesh/motor validation.</summary>
    internal static class ScriptableObjectGateL3Unity
    {
        internal static bool ValidateMissilePrefab(ref MissileEntry entry, out string? reason)
        {
            reason = null;

            PropertyInfo? prefabProp = entry.Definition!.GetType().GetProperty("unitPrefab", BindingFlags.Public | BindingFlags.Instance);
            object? prefabObj = prefabProp?.GetValue(entry.Definition);
            if (prefabObj == null)
            {
                reason = "unitPrefab is null";
                return false;
            }

            GameObject? prefabGo = prefabObj as GameObject;
            if (prefabGo == null)
            {
                reason = "unitPrefab is not GameObject";
                return false;
            }

            if (entry.Mesh == null)
            {
                MeshFilter? mf = prefabGo.GetComponentInChildren<MeshFilter>(true);
                entry.Mesh = mf?.sharedMesh;
            }

            if (entry.Mesh == null)
            {
                reason = "Mesh missing on unitPrefab";
                return false;
            }

            if (entry.EngineSpawnPoint == null)
            {
                Type? missileType = ResolveGameType("Missile");
                if (missileType != null)
                {
                    Component? missile = prefabGo.GetComponent(missileType);
                    FieldInfo? motorsField = missileType.GetField("motors", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (motorsField?.GetValue(missile) is Array motors && motors.Length > 0)
                    {
                        object? motor = motors.GetValue(0);
                        FieldInfo? transformField = motor?.GetType().GetField("transform", BindingFlags.Public | BindingFlags.Instance);
                        if (transformField?.GetValue(motor) is Transform t)
                            entry.EngineSpawnPoint = t;
                    }
                }

                if (entry.EngineSpawnPoint == null)
                    entry.EngineSpawnPoint = prefabGo.transform;
            }

            if (entry.EngineSpawnPoint == null)
            {
                reason = "EngineSpawnPoint missing";
                return false;
            }

            Type? missileDefType = ResolveGameType("MissileDefinition");
            if (missileDefType != null && missileDefType.IsInstanceOfType(entry.Definition))
            {
                MethodInfo? getMass = missileDefType.GetMethod("GetMass", BindingFlags.Public | BindingFlags.Instance);
                if (getMass?.Invoke(entry.Definition, null) is float mass && (float.IsNaN(mass) || mass <= 0f))
                {
                    reason = "GetMass() invalid: " + mass;
                    return false;
                }
            }

            return true;
        }

        internal static bool ValidateAircraftPrefab(ref AircraftEntry entry, out string? reason)
        {
            reason = null;

            Type? aircraftDefType = ResolveGameType("AircraftDefinition");
            if (aircraftDefType != null && !aircraftDefType.IsInstanceOfType(entry.Definition))
            {
                reason = "Definition is not AircraftDefinition";
                return false;
            }

            PropertyInfo? prefabProp = entry.Definition!.GetType().GetProperty("unitPrefab", BindingFlags.Public | BindingFlags.Instance);
            object? prefabObj = prefabProp?.GetValue(entry.Definition);
            if (prefabObj == null)
            {
                reason = "unitPrefab is null";
                return false;
            }

            GameObject? prefabGo = prefabObj as GameObject;
            if (prefabGo == null)
            {
                reason = "unitPrefab is not GameObject";
                return false;
            }

            Type colliderType = typeof(Collider);
            Type meshFilterType = typeof(MeshFilter);
            bool hasCollider = prefabGo.GetComponentInChildren(colliderType, true) != null;
            bool hasMesh = prefabGo.GetComponentInChildren(meshFilterType, true) != null;
            if (!hasCollider && !hasMesh)
            {
                reason = "unitPrefab missing Collider and MeshFilter";
                return false;
            }

            Rigidbody? rb = prefabGo.GetComponentInChildren(typeof(Rigidbody), true) as Rigidbody;
            if (rb != null && (float.IsNaN(rb.mass) || rb.mass <= 0f))
            {
                reason = "Rigidbody mass invalid";
                return false;
            }

            return true;
        }

        private static Type? ResolveGameType(string name)
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
