using System;
using System.Collections.Generic;
using System.Reflection;
#if NOLoader_DEV
using NOLoader.Core.Development;
#endif

namespace NOLoader.Core.Interop
{
    /// <summary>Resolve game types once at startup — no repeated AppDomain type scans.</summary>
    public static class GameTypeCache
    {
        private static readonly Dictionary<string, Type?> Types = new Dictionary<string, Type?>(StringComparer.Ordinal);
        private static readonly Dictionary<string, MemberInfo?> Members = new Dictionary<string, MemberInfo?>(StringComparer.Ordinal);
        private static Assembly? _gameAssembly;

        public static int CachedTypeCount => Types.Count;

        public static Assembly? GameAssembly
        {
            get
            {
                if (_gameAssembly != null)
                    return _gameAssembly;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (string.Equals(asm.GetName().Name, "Assembly-CSharp", StringComparison.Ordinal))
                    {
                        _gameAssembly = asm;
                        break;
                    }
                }
                return _gameAssembly;
            }
        }

        public static Type? Resolve(string fullOrShortName)
        {
            if (Types.TryGetValue(fullOrShortName, out Type? cached))
                return cached;

            Type? resolved = ResolveInternal(fullOrShortName);
            Types[fullOrShortName] = resolved;
#if NOLoader_DEV
            ReflectionTracker.RecordTypeResolve(fullOrShortName, resolved != null);
#endif
            return resolved;
        }

        public static PropertyInfo? ResolveProperty(Type? type, string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
        {
            if (type == null)
                return null;

            string key = type.FullName + "::P::" + name + "::" + (int)flags;
            if (Members.TryGetValue(key, out MemberInfo? cached))
                return cached as PropertyInfo;

            PropertyInfo? prop = type.GetProperty(name, flags);
            Members[key] = prop;
#if NOLoader_DEV
            ReflectionTracker.RecordMemberResolve(key, prop != null);
#endif
            return prop;
        }

        public static PropertyInfo? ResolveStaticProperty(string typeName, string name)
        {
            return ResolveProperty(Resolve(typeName), name, BindingFlags.Public | BindingFlags.Static);
        }

        public static FieldInfo? ResolveField(Type? type, string name, BindingFlags flags = BindingFlags.Public | BindingFlags.Instance)
        {
            if (type == null)
                return null;

            string key = type.FullName + "::F::" + name + "::" + (int)flags;
            if (Members.TryGetValue(key, out MemberInfo? cached))
                return cached as FieldInfo;

            FieldInfo? field = type.GetField(name, flags);
            Members[key] = field;
#if NOLoader_DEV
            ReflectionTracker.RecordMemberResolve(key, field != null);
#endif
            return field;
        }

        public static MethodInfo? ResolveMethod(Type? type, string name, BindingFlags flags, params Type[] paramTypes)
        {
            if (type == null)
                return null;

            string key = type.FullName + "::M::" + name + "::" + (int)flags + "::" + paramTypes.Length;
            if (Members.TryGetValue(key, out MemberInfo? cached))
                return cached as MethodInfo;

            MethodInfo? method = type.GetMethod(name, flags, null, paramTypes, null);
            Members[key] = method;
#if NOLoader_DEV
            ReflectionTracker.RecordMemberResolve(key, method != null);
#endif
            return method;
        }

        private static Type? ResolveInternal(string fullOrShortName)
        {
            Assembly? game = GameAssembly;
            if (game != null)
            {
                Type? t = game.GetType(fullOrShortName, throwOnError: false);
                if (t != null)
                    return t;
            }

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = asm.GetType(fullOrShortName, throwOnError: false);
                if (t != null)
                    return t;

                try
                {
                    foreach (Type type in asm.GetTypes())
                    {
                        if (string.Equals(type.Name, fullOrShortName, StringComparison.Ordinal)
                            || string.Equals(type.FullName, fullOrShortName, StringComparison.Ordinal))
                            return type;
                    }
                }
                catch
                {
                    /* dynamic assemblies may throw */
                }
            }

            return null;
        }
    }
}
