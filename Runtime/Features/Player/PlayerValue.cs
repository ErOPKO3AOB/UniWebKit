using System;
using System.Collections;
using System.Globalization;
using UnityEngine;

namespace UniWebKit
{
    [Serializable]
    internal sealed class PlayerValue
    {
        public PlayerValueType type;
        public string data;

        [Serializable]
        private sealed class Box<T> { public T value; }

        internal static bool Encode<T>(T value, out PlayerValue encoded, IPlayerDataSerializer serializer = null)
        {
            encoded = null;
            var type = typeof(T);
            try
            {
                var item = new PlayerValue();
                if (type == typeof(string)) { item.type = PlayerValueType.String; item.data = (string)(object)value ?? ""; }
                else if (type == typeof(int)) { item.type = PlayerValueType.Int32; item.data = ((int)(object)value).ToString(CultureInfo.InvariantCulture); }
                else if (type == typeof(long)) { item.type = PlayerValueType.Int64; item.data = ((long)(object)value).ToString(CultureInfo.InvariantCulture); }
                else if (type == typeof(float))
                {
                    float number = (float)(object)value;
                    if (float.IsNaN(number) || float.IsInfinity(number)) return false;
                    item.type = PlayerValueType.Single; item.data = number.ToString("R", CultureInfo.InvariantCulture);
                }
                else if (type == typeof(double))
                {
                    double number = (double)(object)value;
                    if (double.IsNaN(number) || double.IsInfinity(number)) return false;
                    item.type = PlayerValueType.Double; item.data = number.ToString("R", CultureInfo.InvariantCulture);
                }
                else if (type == typeof(decimal)) { item.type = PlayerValueType.Decimal; item.data = ((decimal)(object)value).ToString(CultureInfo.InvariantCulture); }
                else if (type == typeof(bool)) { item.type = PlayerValueType.Boolean; item.data = (bool)(object)value ? "true" : "false"; }
                else if (type.IsEnum) { item.type = PlayerValueType.Enum; item.data = ((Enum)(object)value).ToString("D"); }
                else
                {
                    item.type = PlayerValueType.Json;
                    if (serializer != null)
                    {
                        if (!serializer.TrySerialize(value, out item.data) || string.IsNullOrEmpty(item.data)) return false;
                    }
                    else
                    {
                        if (!CanSerialize(type)) return false;
                        item.data = JsonUtility.ToJson(new Box<T> { value = value });
                        if (string.IsNullOrEmpty(item.data) || !item.data.Contains("\"value\"")) return false;
                    }
                }
                encoded = item; return true;
            }
            catch (Exception) { return false; }
        }

        private static bool CanSerialize(Type type) => CanSerialize(type, new System.Collections.Generic.HashSet<Type>(), false);

        private static bool CanSerialize(Type type, System.Collections.Generic.HashSet<Type> path, bool field)
        {
            if (type == typeof(string)) return field;
            if (type.IsEnum) return field && Enum.GetUnderlyingType(type) != typeof(long) && Enum.GetUnderlyingType(type) != typeof(ulong);
            if (type.IsPrimitive) return field;
            if (type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
                || type == typeof(Guid) || type == typeof(object) || type.IsInterface
                || typeof(IDictionary).IsAssignableFrom(type) || typeof(Delegate).IsAssignableFrom(type)
                || typeof(UnityEngine.Object).IsAssignableFrom(type) || path.Contains(type)) return false;
            if (type.IsArray) return type.GetArrayRank() == 1 && !type.GetElementType().IsArray
                && (type.GetElementType() == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type.GetElementType()))
                && CanSerialize(type.GetElementType(), path, true);
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                var element = type.GetGenericArguments()[0];
                return !element.IsArray && !typeof(IEnumerable).IsAssignableFrom(element) && CanSerialize(element, path, true)
                    || element == typeof(string);
            }
            if (typeof(IEnumerable).IsAssignableFrom(type) || !type.IsSerializable) return false;
            path.Add(type);
            try
            {
                bool hasFields = false;
                foreach (var member in type.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                {
                    if (member.IsStatic || member.IsInitOnly || member.IsNotSerialized
                        || !member.IsPublic && !Attribute.IsDefined(member, typeof(SerializeField))) continue;
                    hasFields = true;
                    if (!CanSerialize(member.FieldType, path, true)) return false;
                }
                return hasFields;
            }
            finally { path.Remove(type); }
        }

        internal bool Decode<T>(out T value, IPlayerDataSerializer serializer = null)
        {
            value = default;
            try
            {
                object result = null;
                var target = typeof(T);
                if (target == typeof(string) && type == PlayerValueType.String) result = data;
                else if (target == typeof(int) && type == PlayerValueType.Int32 && int.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer)) result = integer;
                else if (target == typeof(long) && type == PlayerValueType.Int64 && long.TryParse(data, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wide)) result = wide;
                else if (target == typeof(float) && type == PlayerValueType.Single && float.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var single) && !float.IsInfinity(single) && !float.IsNaN(single)) result = single;
                else if (target == typeof(double) && type == PlayerValueType.Double && double.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && !double.IsInfinity(number) && !double.IsNaN(number)) result = number;
                else if (target == typeof(decimal) && type == PlayerValueType.Decimal && decimal.TryParse(data, NumberStyles.Float, CultureInfo.InvariantCulture, out var precise)) result = precise;
                else if (target == typeof(bool) && type == PlayerValueType.Boolean && bool.TryParse(data, out var flag)) result = flag;
                else if (target.IsEnum && type == PlayerValueType.Enum && Enum.TryParse(target, data, false, out var enumeration)) result = enumeration;
                else if (type == PlayerValueType.Json && serializer != null) return serializer.TryDeserialize(data, out value);
                else if (type == PlayerValueType.Json && CanSerialize(target))
                {
                    var box = JsonUtility.FromJson<Box<T>>(data);
                    if (box == null) return false;
                    value = box.value; return true;
                }
                else return false;
                value = (T)result; return true;
            }
            catch (Exception) { return false; }
        }
    }
}
