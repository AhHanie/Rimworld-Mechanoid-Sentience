using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Verse;

namespace Mechanoid_Sentience
{
    internal static class DefCloner
    {
        private static readonly MethodInfo MemberwiseCloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly Dictionary<Type, FieldInfo[]> FieldCache = new Dictionary<Type, FieldInfo[]>();

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
        }

        public static T Clone<T>(T source) where T : Def
        {
            var copies = new Dictionary<object, object>(new ReferenceComparer());
            return (T)CopyObject(source, copies);
        }

        private static object Copy(object source, Dictionary<object, object> copies)
        {
            if (source == null)
            {
                return null;
            }
            if (copies.TryGetValue(source, out object existing))
            {
                return existing;
            }
            Type type = source.GetType();
            if (type.IsPrimitive || type.IsEnum || type.IsValueType || type == typeof(string) || type == typeof(decimal))
            {
                return source;
            }
            if (ShouldShare(source, type))
            {
                return source;
            }
            if (type.IsArray)
            {
                Array array = (Array)source;
                Array arrayCopy = Array.CreateInstance(type.GetElementType(), array.Length);
                copies[source] = arrayCopy;
                for (int i = 0; i < array.Length; i++)
                {
                    arrayCopy.SetValue(Copy(array.GetValue(i), copies), i);
                }
                return arrayCopy;
            }
            return CopyObject(source, copies);
        }

        private static bool ShouldShare(object source, Type type)
        {
            if (source is Def || source is Type || source is Delegate || source is MemberInfo || source is ModContentPack)
            {
                return true;
            }
            if (source is UnityEngine.Object)
            {
                return true;
            }
            string ns = type.Namespace ?? string.Empty;
            bool isGenericCollection = ns == "System.Collections.Generic";
            if (!isGenericCollection && (ns.StartsWith("System") || ns.StartsWith("UnityEngine")))
            {
                return true;
            }
            return false;
        }

        private static object CopyObject(object source, Dictionary<object, object> copies)
        {
            Type type = source.GetType();
            if (type.Namespace == "System.Collections.Generic")
            {
                object fresh = Activator.CreateInstance(type);
                copies[source] = fresh;
                if (source is IDictionary sourceDictionary)
                {
                    IDictionary freshDictionary = (IDictionary)fresh;
                    foreach (DictionaryEntry entry in sourceDictionary)
                    {
                        freshDictionary.Add(Copy(entry.Key, copies), Copy(entry.Value, copies));
                    }
                }
                else if (source is IList sourceList)
                {
                    IList freshList = (IList)fresh;
                    for (int i = 0; i < sourceList.Count; i++)
                    {
                        freshList.Add(Copy(sourceList[i], copies));
                    }
                }
                else
                {
                    MethodInfo add = type.GetMethod("Add");
                    if (add == null)
                    {
                        throw new NotSupportedException("Cannot clone collection type " + type);
                    }
                    foreach (object item in (IEnumerable)source)
                    {
                        add.Invoke(fresh, new[] { Copy(item, copies) });
                    }
                }
                return fresh;
            }

            object clone = MemberwiseCloneMethod.Invoke(source, null);
            copies[source] = clone;
            foreach (FieldInfo field in FieldsOf(type))
            {
                object value = field.GetValue(clone);
                if (value == null)
                {
                    continue;
                }
                object copied = Copy(value, copies);
                if (!ReferenceEquals(copied, value))
                {
                    field.SetValue(clone, copied);
                }
            }
            return clone;
        }

        private static FieldInfo[] FieldsOf(Type type)
        {
            if (FieldCache.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }
            var fields = new List<FieldInfo>();
            for (Type t = type; t != null && t != typeof(object); t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    Type fieldType = field.FieldType;
                    if (fieldType.IsPrimitive || fieldType.IsEnum || fieldType == typeof(string))
                    {
                        continue;
                    }
                    fields.Add(field);
                }
            }
            FieldInfo[] result = fields.ToArray();
            FieldCache[type] = result;
            return result;
        }
    }
}
