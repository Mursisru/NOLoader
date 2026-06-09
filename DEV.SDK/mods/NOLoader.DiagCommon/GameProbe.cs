using System;
using System.Reflection;
using UnityEngine;

namespace NOLoader.DiagCommon
{
    public static class GameProbe
    {
        public static Type? FindType(string fullName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }
            return null;
        }

        public static object? GetSceneSingleton(string typeName)
        {
            Type? t = FindType(typeName);
            if (t == null)
                return null;

            for (Type? cur = t; cur != null; cur = cur.BaseType)
            {
                FieldInfo? field = cur.GetField(
                    "i",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(null);
            }

            return FindFirstLiveObject(t);
        }

        private static object? FindFirstLiveObject(Type type)
        {
            MethodInfo? find = typeof(Resources).GetMethod(
                "FindObjectsOfTypeAll",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type) },
                null);

            if (find?.Invoke(null, new object[] { type }) is not Array arr)
                return null;

            foreach (object item in arr)
            {
                if (item is MonoBehaviour mb && mb.gameObject.scene.isLoaded)
                    return item;
            }

            return null;
        }

        public static object? GetLocalAircraft()
        {
            Type? gm = FindType("GameManager");
            Type? aircraft = FindType("Aircraft");
            if (gm == null || aircraft == null)
                return null;

            MethodInfo? method = gm.GetMethod(
                "GetLocalAircraft",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { aircraft.MakeByRefType() },
                null);

            if (method == null)
                return null;

            object?[] args = { null };
            return method.Invoke(null, args) is bool ok && ok ? args[0] : null;
        }

        public static int CountObjects(Type type)
        {
            MethodInfo? find = typeof(Resources).GetMethod(
                "FindObjectsOfTypeAll",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Type) },
                null);
            return find?.Invoke(null, new object[] { type }) is Array arr ? arr.Length : 0;
        }

        public static int CountDictionary(object? dict)
        {
            if (dict == null)
                return 0;
            PropertyInfo? count = dict.GetType().GetProperty("Count");
            return count?.GetValue(dict) is int n ? n : 0;
        }

        public static bool IsDllMarker(string gameRoot, string module, string marker)
        {
            string path = System.IO.Path.Combine(gameRoot, "NuclearOption_Data", "Managed", module);
            if (!System.IO.File.Exists(path))
                return false;
            string text = System.Text.Encoding.ASCII.GetString(System.IO.File.ReadAllBytes(path));
            return text.IndexOf(marker, StringComparison.Ordinal) >= 0;
        }

        public static FieldInfo? FindInstanceField(Type type, string name)
            => type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        public static object? GetInstanceField(object target, string name)
            => GetInstanceMember(target, name);

        public static object? GetInstanceMember(object target, string name)
        {
            Type type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
            PropertyInfo? prop = type.GetProperty(name, flags);
            if (prop != null)
                return prop.GetValue(target);
            return type.GetField(name, flags)?.GetValue(target);
        }
    }
}
