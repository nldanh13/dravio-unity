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
        public void SetParent(Transform p, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static float Distance(Vector3 a, Vector3 b) { return 0f; }
    }
    public struct Quaternion { public float x, y, z, w; public static Quaternion identity { get { return new Quaternion { w = 1 }; } } }
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
        public static int StringToHash(string s) { return 0; }
    }
    public class Renderer : Component
    {
        public Bounds bounds { get { return default(Bounds); } }
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
    public static class Mathf { public static float Min(float a, float b) { return a < b ? a : b; } }
    [AttributeUsage(AttributeTargets.Class)] public sealed class CreateAssetMenuAttribute : Attribute { public string menuName, fileName; }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
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
