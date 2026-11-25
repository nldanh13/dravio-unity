// Stub tối thiểu của UnityEngine để biên dịch các file logic thuần ngoài Unity. KHÔNG phải Unity thật:
// chỉ đủ kiểu và hành vi mà các file trong csproj dùng. Unity bỏ qua thư mục DotNet~.
using System;

namespace UnityEngine
{
    public class Object { public string name; }
    public class ScriptableObject : Object { }
    public sealed class GameObject : Object { public GameObject(string n = null) { name = n; } }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity { get { return new Quaternion { w = 1 }; } }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white { get { return new Color(1, 1, 1, 1); } }
    }

    public sealed class Animator
    {
        public static int StringToHash(string s) { return s == null ? 0 : s.GetHashCode(); }
    }

    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class HeaderAttribute : Attribute { public HeaderAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class SerializeField : Attribute { }
}
