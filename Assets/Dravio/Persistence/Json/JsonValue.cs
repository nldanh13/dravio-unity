using System;
using System.Collections;
using System.Collections.Generic;

namespace Dravio.Persistence.Json
{
    public enum JsonKind { Null, Bool, Number, String, Array, Object }

    /// <summary>
    /// Cây JSON bất biến về kiểu, mô phỏng giá trị JavaScript sau <c>JSON.parse</c>: số là double,
    /// object giữ thứ tự chèn và khóa trùng thì giá trị sau thắng (giống V8).
    /// </summary>
    public abstract class JsonValue
    {
        public abstract JsonKind Kind { get; }

        public static readonly JsonValue Null = new JsonNull();
        public static readonly JsonValue True = new JsonBool(true);
        public static readonly JsonValue False = new JsonBool(false);

        public static JsonValue From(bool b) => b ? True : False;
        public static JsonValue From(double d) => new JsonNumber(d);
        public static JsonValue From(long l) => new JsonNumber(l);
        public static JsonValue From(string s) => s == null ? Null : new JsonString(s);

        public bool IsNull => Kind == JsonKind.Null;
        public JsonObject AsObject => this as JsonObject;
        public JsonArray AsArray => this as JsonArray;

        public override string ToString() => JsonWriter.Stringify(this);
    }

    public sealed class JsonNull : JsonValue
    {
        internal JsonNull() { }
        public override JsonKind Kind => JsonKind.Null;
    }

    public sealed class JsonBool : JsonValue
    {
        internal JsonBool(bool v) { Value = v; }
        public bool Value { get; }
        public override JsonKind Kind => JsonKind.Bool;
    }

    public sealed class JsonNumber : JsonValue
    {
        public JsonNumber(double v) { Value = v; }
        public double Value { get; }
        public override JsonKind Kind => JsonKind.Number;
    }

    public sealed class JsonString : JsonValue
    {
        public JsonString(string v) { Value = v ?? throw new ArgumentNullException(nameof(v)); }
        public string Value { get; }
        public override JsonKind Kind => JsonKind.String;
    }

    public sealed class JsonArray : JsonValue, IEnumerable<JsonValue>
    {
        private readonly List<JsonValue> _items = new List<JsonValue>();
        public override JsonKind Kind => JsonKind.Array;
        public int Count => _items.Count;
        public JsonValue this[int i] => _items[i];
        public JsonArray Add(JsonValue v) { _items.Add(v ?? Null); return this; }
        public IEnumerator<JsonValue> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public sealed class JsonObject : JsonValue, IEnumerable<KeyValuePair<string, JsonValue>>
    {
        private readonly List<string> _order = new List<string>();
        private readonly Dictionary<string, JsonValue> _map = new Dictionary<string, JsonValue>(StringComparer.Ordinal);

        public override JsonKind Kind => JsonKind.Object;
        public int Count => _order.Count;
        public IEnumerable<string> Keys => _order;

        /// <summary>Gán khóa. Khóa đã có giữ vị trí cũ, giá trị mới thắng (như JSON.parse / gán thuộc tính JS).</summary>
        public JsonObject Set(string key, JsonValue value)
        {
            if (!_map.ContainsKey(key)) _order.Add(key);
            _map[key] = value ?? Null;
            return this;
        }

        public bool Remove(string key)
        {
            if (!_map.Remove(key)) return false;
            _order.Remove(key);
            return true;
        }

        public bool ContainsKey(string key) => _map.ContainsKey(key);

        /// <summary>Giá trị của khóa; null (C#) khi khóa không tồn tại (khác JSON null).</summary>
        public JsonValue Get(string key) => _map.TryGetValue(key, out JsonValue v) ? v : null;

        /// <summary>Bản sao nông: giá trị con dùng chung (cây coi như bất biến sau khi parse).</summary>
        public JsonObject ShallowCopy()
        {
            var o = new JsonObject();
            foreach (string k in _order) o.Set(k, _map[k]);
            return o;
        }

        public IEnumerator<KeyValuePair<string, JsonValue>> GetEnumerator()
        {
            foreach (string k in _order) yield return new KeyValuePair<string, JsonValue>(k, _map[k]);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
