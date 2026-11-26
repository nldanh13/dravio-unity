// Hand-written UnityEngine surface for the type-check only (not Unity, no behaviour). DragonFootGrounding is
// stubbed too because its IK maths uses far more engine API; it is covered by the Unity EditMode tests.
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object o) { }
        public static T Instantiate<T>(T original, Transform parent) where T : Object { return original; }
        public static implicit operator bool(Object o) { return !ReferenceEquals(o, null); }
    }
    public class ScriptableObject : Object { }
    public class Component : Object
    {
        public Transform transform { get { return null; } }
        public GameObject gameObject { get { return null; } }
        public T GetComponent<T>() { return default(T); }
        public T GetComponentInChildren<T>(bool includeInactive) { return default(T); }
        public T[] GetComponentsInChildren<T>(bool includeInactive) { return new T[0]; }
    }
    public class Behaviour : Component { public bool enabled { get; set; } }
    public class MonoBehaviour : Behaviour { }
    public sealed class GameObject : Object
    {
        public GameObject(string n) { name = n; }
        public Transform transform { get { return null; } }
        public void SetActive(bool v) { }
        public T GetComponent<T>() { return default(T); }
        public T AddComponent<T>() where T : Component { return default(T); }
    }
    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 up { get { return default(Vector3); } }
        public Vector3 right { get { return default(Vector3); } }
        public Vector3 forward { get { return default(Vector3); } }
        public void RotateAround(Vector3 point, Vector3 axis, float angle) { }
        public Vector3 InverseTransformDirection(Vector3 d) { return d; }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static float Distance(Vector3 a, Vector3 b) { return 0f; }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator *(float k, Vector3 a) { return new Vector3(k * a.x, k * a.y, k * a.z); }
    }
    public struct Quaternion
    {
        public float x, y, z, w;
        public static Quaternion identity { get { return new Quaternion { w = 1 }; } }
        public static Quaternion AngleAxis(float deg, Vector3 axis) { return identity; }
        public static Quaternion operator *(Quaternion a, Quaternion b) { return a; }
    }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color white { get { return new Color(1, 1, 1); } } }
    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size) { this.center = center; }
        public Vector3 center { get; set; }
        public void Encapsulate(Bounds b) { }
    }
    public struct Plane { }
    public enum AnimatorCullingMode { AlwaysAnimate, CullUpdateTransforms, CullCompletely }
    public class RuntimeAnimatorController : Object { }
    public sealed class Animator : Behaviour
    {
        public bool keepAnimatorStateOnDisable { get; set; }
        public AnimatorCullingMode cullingMode { get; set; }
        public RuntimeAnimatorController runtimeAnimatorController { get; set; }
        public void SetInteger(int id, int v) { }
        public void SetFloat(int id, float v) { }
        public void Update(float dt) { }
        public void Rebind() { }
        public AnimatorControllerParameter[] parameters { get { return new AnimatorControllerParameter[0]; } }
        public static int StringToHash(string s) { return 0; }
    }
    public class Renderer : Component
    {
        public Bounds bounds { get { return default(Bounds); } }
        public bool enabled { get; set; }
        public void SetPropertyBlock(MaterialPropertyBlock b) { }
    }
    public sealed class MaterialPropertyBlock
    {
        public void Clear() { }
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float v) { }
    }
    public sealed class Camera : Behaviour { public static Camera main { get { return null; } } }
    public static class GeometryUtility
    {
        public static void CalculateFrustumPlanes(Camera c, Plane[] planes) { }
        public static bool TestPlanesAABB(Plane[] planes, Bounds b) { return true; }
    }
    public static class Time { public static int frameCount { get { return 0; } } public static float deltaTime { get { return 0f; } } }
    public static class Debug { public static void LogWarning(object m, Object ctx) { } }
    public static class Mathf
    {
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static float Clamp01(float v) { return v < 0f ? 0f : (v > 1f ? 1f : v); }
        public static float Abs(float v) { return v < 0f ? -v : v; }
    }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public sealed class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    public class Mesh : Object
    {
        public int blendShapeCount { get { return 0; } }
        public string GetBlendShapeName(int i) { return ""; }
    }
    public class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh { get { return null; } }
        public void SetBlendShapeWeight(int i, float w) { }
    }
    public enum AnimatorControllerParameterType { Float = 1, Int = 3, Bool = 4, Trigger = 9 }
    public sealed class AnimatorControllerParameter { public string name; public int nameHash; public AnimatorControllerParameterType type; }
}

namespace Dravio.Character
{
    // stand-in for the real MonoBehaviour (Runtime/DragonFootGrounding.cs), same members as used by DragonVisualInstance
    public sealed class DragonFootGrounding : UnityEngine.MonoBehaviour
    {
        public void Configure(DragonVisualCatalog.IkChain[] chains) { }
        public static bool AppliesTo(DragonLocomotion l, DragonInteraction i) { return true; }
    }
}
