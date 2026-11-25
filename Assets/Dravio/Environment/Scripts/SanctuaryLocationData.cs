using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>
    /// Read-only position data for the Origin Sanctuary, generated from
    /// Data/OriginSanctuaryLocations.json (scripts/art/environment/layout.py).
    /// Meant for Living World / habitat systems: microhabitat areas, walkable test,
    /// water (swim) areas, obstacles, dragon spawn points, habitat transitions and
    /// perches. Uses the canonical habitat string IDs but has no dependency on the
    /// domain assemblies and never changes game state.
    /// All areas are XZ polygons in world space on the flat ground (y = 0).
    /// </summary>
    [CreateAssetMenu(menuName = "Dravio/Environment/Sanctuary Location Data", fileName = "OriginSanctuaryLocations")]
    public sealed class SanctuaryLocationData : ScriptableObject
    {
        [Serializable]
        public sealed class Habitat
        {
            public string id;
            public string zoneId;
            public string displayNameVi;
            public string displayNameEn;
            public bool defaultUnlocked;
            public string requiresDiscovery;
            public float[] polygonXZ;
            public float[] centerXZ;
            public float areaM2;
            public float[] swimPolygonXZ;
        }

        [Serializable]
        public sealed class SpawnPoint
        {
            public string id;
            public string habitatId;
            public string kind;
            public float x, y, z, yaw;
            public Vector3 Position => new Vector3(x, y, z);
            public Quaternion Rotation => Quaternion.Euler(0f, yaw, 0f);
        }

        [Serializable]
        public sealed class Transition
        {
            public string id;
            public string habitatA;
            public string habitatB;
            public float x, y, z, radius;
            public Vector3 Position => new Vector3(x, y, z);
            public bool Connects(string habitatId) => habitatA == habitatId || habitatB == habitatId;
            public string Other(string habitatId) => habitatA == habitatId ? habitatB : habitatA;
        }

        [Serializable]
        public sealed class Perch
        {
            public string id;
            public string habitatId;
            public string source;
            public float x, y, z;
            public Vector3 Position => new Vector3(x, y, z);
        }

        [Serializable]
        public sealed class Area
        {
            public string id;
            public string habitatId;
            public string source;
            public float[] polygonXZ;
        }

        public int version;
        public string zoneId;
        public float[] flatRectXZ;
        public float walkClearance = 0.45f;
        public Habitat[] habitats = Array.Empty<Habitat>();
        public SpawnPoint[] spawnPoints = Array.Empty<SpawnPoint>();
        public Transition[] transitions = Array.Empty<Transition>();
        public Perch[] perchPoints = Array.Empty<Perch>();
        public Area[] waterAreas = Array.Empty<Area>();
        public Area[] obstacles = Array.Empty<Area>();

        /// <summary>Replaces the contents from the generated JSON (editor tools call this).</summary>
        public void LoadFromJson(string json) => JsonUtility.FromJsonOverwrite(json, this);

        public Habitat FindHabitat(string habitatId)
        {
            foreach (var h in habitats)
                if (h.id == habitatId) return h;
            return null;
        }

        /// <summary>Microhabitat ID under a world position, or null outside every habitat.</summary>
        public string HabitatAt(Vector3 world)
        {
            foreach (var h in habitats)
                if (Contains(h.polygonXZ, world.x, world.z)) return h.id;
            return null;
        }

        public bool InWater(Vector3 world)
        {
            foreach (var w in waterAreas)
                if (Contains(w.polygonXZ, world.x, world.z)) return true;
            return false;
        }

        public bool InSwimArea(Vector3 world, string habitatId)
        {
            var h = FindHabitat(habitatId);
            return h != null && h.swimPolygonXZ != null && h.swimPolygonXZ.Length >= 6 &&
                   Contains(h.swimPolygonXZ, world.x, world.z);
        }

        public bool Blocked(Vector3 world, float clearance)
        {
            foreach (var o in obstacles)
                if (Contains(o.polygonXZ, world.x, world.z) || EdgeDistance(o.polygonXZ, world.x, world.z) < clearance)
                    return true;
            return false;
        }

        /// <summary>
        /// Safe ground for a walking dragon: inside the habitat (any habitat when null),
        /// clear of obstacles by <see cref="walkClearance"/>, and out of the pond.
        /// Swimmers may also use the habitat's swim area.
        /// </summary>
        public bool IsWalkable(Vector3 world, string habitatId = null, bool canSwim = false)
        {
            string at = HabitatAt(world);
            if (at == null || (habitatId != null && at != habitatId)) return false;
            if (Blocked(world, walkClearance)) return false;
            if (InWater(world)) return canSwim && InSwimArea(world, at);
            return true;
        }

        public IEnumerable<SpawnPoint> SpawnPointsIn(string habitatId)
        {
            foreach (var s in spawnPoints)
                if (s.habitatId == habitatId) yield return s;
        }

        public IEnumerable<Transition> TransitionsFrom(string habitatId)
        {
            foreach (var t in transitions)
                if (t.Connects(habitatId)) yield return t;
        }

        public IEnumerable<Perch> PerchesIn(string habitatId)
        {
            foreach (var p in perchPoints)
                if (p.habitatId == habitatId) yield return p;
        }

        // ------------------------------------------------------------ geometry
        public static bool Contains(float[] xz, float x, float z)
        {
            if (xz == null || xz.Length < 6) return false;
            bool inside = false;
            int n = xz.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xz[i * 2], zi = xz[i * 2 + 1], xj = xz[j * 2], zj = xz[j * 2 + 1];
                if ((zi > z) != (zj > z) && x < (xj - xi) * (z - zi) / (zj - zi) + xi) inside = !inside;
            }
            return inside;
        }

        public static float EdgeDistance(float[] xz, float x, float z)
        {
            if (xz == null || xz.Length < 4) return float.MaxValue;
            float best = float.MaxValue;
            int n = xz.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                var a = new Vector2(xz[i * 2], xz[i * 2 + 1]);
                var b = new Vector2(xz[j * 2], xz[j * 2 + 1]);
                var p = new Vector2(x, z);
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }
    }
}
