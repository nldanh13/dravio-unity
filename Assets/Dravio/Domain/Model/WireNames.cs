using System;
using System.Collections.Generic;
using System.Reflection;

namespace Dravio.Domain
{
    /// <summary>Ghi đè chuỗi wire của một giá trị enum (mặc định: tên camelCase).</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class WireNameAttribute : Attribute
    {
        public WireNameAttribute(string name) { Name = name; }
        public string Name { get; }
    }

    /// <summary>
    /// Ánh xạ enum ↔ chuỗi wire giống hệt TypeScript gốc (ví dụ <c>GrowthStage.Juvenile</c> ↔ "juvenile",
    /// <c>HazardId.StrongCurrent</c> ↔ "strong_current"). Dùng cho save, i18n và kiểm thử đối chiếu.
    /// </summary>
    public static class WireNames
    {
        private static class Cache<T> where T : struct, Enum
        {
            internal static readonly Dictionary<T, string> ToWire = new Dictionary<T, string>();
            internal static readonly Dictionary<string, T> FromWire = new Dictionary<string, T>(StringComparer.Ordinal);

            static Cache()
            {
                foreach (FieldInfo f in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
                {
                    var value = (T)f.GetValue(null);
                    var attr = f.GetCustomAttribute<WireNameAttribute>();
                    string wire = attr != null ? attr.Name : char.ToLowerInvariant(f.Name[0]) + f.Name.Substring(1);
                    ToWire[value] = wire;
                    FromWire[wire] = value;
                }
            }
        }

        public static string ToWire<T>(this T value) where T : struct, Enum
        {
            if (Cache<T>.ToWire.TryGetValue(value, out string wire)) return wire;
            throw new ArgumentOutOfRangeException(nameof(value), value, "Undefined " + typeof(T).Name);
        }

        public static bool TryParse<T>(string wire, out T value) where T : struct, Enum
        {
            if (wire != null && Cache<T>.FromWire.TryGetValue(wire, out value)) return true;
            value = default;
            return false;
        }

        /// <summary>Giá trị enum có thuộc tập đã khai báo không (chặn giá trị ép kiểu tùy ý).</summary>
        public static bool IsDefined<T>(T value) where T : struct, Enum => Cache<T>.ToWire.ContainsKey(value);
    }
}
