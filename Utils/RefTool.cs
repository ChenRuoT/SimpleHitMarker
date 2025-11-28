using System;
using System.Collections.Concurrent;
using System.Reflection;
using EFT;

namespace SPT.Reflection.Utils
{
    /// <summary>
    /// Minimal helper that locates EFT types by probing Assembly-CSharp via method names.
    /// This mimics the utility available in other SPT projects.
    /// </summary>
    internal static class RefTool
    {
        private static readonly ConcurrentDictionary<string, Type> Cache = new ConcurrentDictionary<string, Type>(StringComparer.Ordinal);
        private const BindingFlags AllBindings = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        public static Type GetEftType(string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
            {
                return null;
            }

            return Cache.GetOrAdd(memberName, key =>
            {
                Assembly eftAssembly = typeof(Player).Assembly;
                foreach (Type type in eftAssembly.GetTypes())
                {
                    foreach (MethodInfo method in type.GetMethods(AllBindings))
                    {
                        if (method.Name == key)
                        {
                            return type;
                        }
                    }

                    foreach (PropertyInfo property in type.GetProperties(AllBindings))
                    {
                        if (property.Name == key)
                        {
                            return type;
                        }
                    }

                    foreach (FieldInfo field in type.GetFields(AllBindings))
                    {
                        if (field.Name == key)
                        {
                            return type;
                        }
                    }

                    foreach (EventInfo evt in type.GetEvents(AllBindings))
                    {
                        if (evt.Name == key)
                        {
                            return type;
                        }
                    }

                    foreach (Type nested in type.GetNestedTypes(AllBindings))
                    {
                        if (nested.Name == key)
                        {
                            return nested;
                        }
                    }

                    if (type.Name == key)
                    {
                        return type;
                    }
                }

                return null;
            });
        }
    }
}


