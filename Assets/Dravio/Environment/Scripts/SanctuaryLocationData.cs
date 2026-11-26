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
    /// U1-ENV.4 adds optional zone data (Ocean / Sky scenes): swim and fly volumes,
    /// walk areas at their own height (floating islands, seabed), seabed rest points,
    /// links between walk areas and WorldGraph gateways. They are empty for the Origin
    /// Sanctuary, so its data and every query above behave exactly as before.
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
            [Tooltip("U1-ENV.4: ground, seabed, swim or air. Empty = ground (Origin data).")]
            public string medium;
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
            [Tooltip("U1-ENV.4: world height of the collider top (0 = unknown, blocks at any height).")]
            public float top;
        }

        /// <summary>U1-ENV.4: swim ("swim") or flight ("fly") space: XZ polygon between yMin and yMax.</summary>
        [Serializable]
        public sealed class Volume
        {
            public string id;
            public string habitatId;
            public string medium;
            public float[] polygonXZ;
            public float yMin, yMax;
            public bool Contains(Vector3 w) => w.y >= yMin && w.y <= yMax && SanctuaryLocationData.Contains(polygonXZ, w.x, w.z);
        }

        /// <summary>U1-ENV.4: a standable surface at height y (island top, seabed) inside a habitat.</summary>
        [Serializable]
        public sealed class WalkArea
        {
            public string id;
            public string habitatId;
            public string displayNameVi;
            public float[] polygonXZ;
            public float y;
        }

        /// <summary>U1-ENV.4: how a dragon gets between two walk areas (bridge, flight hop).</summary>
        [Serializable]
        public sealed class AreaLink
        {
            public string id;
            public string areaA;
            public string areaB;
            public string[] requires;
            public string source;
            public float x, y, z, radius;
            public Vector3 Position => new Vector3(x, y, z);
            public bool Allows(string locomotion) => requires != null && Array.IndexOf(requires, locomotion) >= 0;
        }

        /// <summary>
        /// U1-ENV.4: where a WorldGraph edge leaves this zone. Data only: the domain owns
        /// travel and unlocks (Ocean / Sky are locked in G1), the environment never acts on it.
        /// </summary>
        [Serializable]
        public sealed class ZoneGate
        {
            public string id;
            public string worldEdgeId;
            public string habitatId;
            public string toZoneId;
            public string toHabitatId;
            public string[] requires;
            public string medium;
            public string source;
            public float x, y, z, radius;
            public bool lockedInG1;
            public Vector3 Position => new Vector3(x, y, z);
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
        // U1-ENV.4 zone data (empty for the Origin Sanctuary).
        public Volume[] volumes = Array.Empty<Volume>();
        public WalkArea[] walkAreas = Array.Empty<WalkArea>();
        public Perch[] restPoints = Array.Empty<Perch>();
        public AreaLink[] areaLinks = Array.Empty<AreaLink>();
        public ZoneGate[] zoneGates = Array.Empty<ZoneGate>();

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
        /// Swimmers may also use the habitat's swim area. In zones with walk areas
        /// (U1-ENV.4) the point must also lie on one of them (island top, seabed).
        /// </summary>
        public bool IsWalkable(Vector3 world, string habitatId = null, bool canSwim = false)
        {
            string at = HabitatAt(world);
            if (at == null || (habitatId != null && at != habitatId)) return false;
            if (Blocked(world, walkClearance)) return false;
            if (walkAreas != null && walkAreas.Length > 0 && WalkAreaAt(world, at) == null) return false;
            if (InWater(world)) return canSwim && InSwimArea(world, at);
            return true;
        }

        // ------------------------------------------------------- U1-ENV.4 zones
        /// <summary>Walk area under a world XZ (the one nearest world.y when several overlap).</summary>
        public WalkArea WalkAreaAt(Vector3 world, string habitatId = null)
        {
            WalkArea best = null;
            if (walkAreas == null) return null;
            foreach (var a in walkAreas)
            {
                if (habitatId != null && a.habitatId != habitatId) continue;
                if (!Contains(a.polygonXZ, world.x, world.z)) continue;
                if (best == null || Mathf.Abs(a.y - world.y) < Mathf.Abs(best.y - world.y)) best = a;
            }
            return best;
        }

        /// <summary>Ground height under a point: walk area height, or 0 on the flat Origin ground.</summary>
        public bool TryGroundHeight(Vector3 world, out float y)
        {
            y = 0f;
            if (walkAreas == null || walkAreas.Length == 0) return HabitatAt(world) != null;
            var a = WalkAreaAt(world);
            if (a == null) return false;
            y = a.y;
            return true;
        }

        public bool InVolume(Vector3 world, string medium, string habitatId = null)
        {
            if (volumes == null) return false;
            foreach (var v in volumes)
                if (v.medium == medium && (habitatId == null || v.habitatId == habitatId) && v.Contains(world)) return true;
            return false;
        }

        /// <summary>Open water for a swimmer: a swim volume (zones) or the habitat swim area (Origin pond).</summary>
        public bool IsSwimmable(Vector3 world, string habitatId = null) =>
            InVolume(world, "swim", habitatId) || (habitatId != null && InSwimArea(world, habitatId));

        /// <summary>Free flight space, clear of any collider that reaches this height.</summary>
        public bool IsFlyable(Vector3 world, string habitatId = null, float clearance = 0.35f) =>
            InVolume(world, "fly", habitatId) && !BlockedAt(world, clearance);

        /// <summary>Like <see cref="Blocked"/> but ignores obstacles whose top is below world.y.</summary>
        public bool BlockedAt(Vector3 world, float clearance)
        {
            foreach (var o in obstacles)
            {
                if (o.top > 0f && o.top < world.y - 0.05f) continue;
                if (Contains(o.polygonXZ, world.x, world.z) || EdgeDistance(o.polygonXZ, world.x, world.z) < clearance)
                    return true;
            }
            return false;
        }

        public IEnumerable<ZoneGate> GatesFrom(string habitatId)
        {
            if (zoneGates == null) yield break;
            foreach (var g in zoneGates)
                if (habitatId == null || g.habitatId == habitatId) yield return g;
        }

        public IEnumerable<Perch> RestPointsIn(string habitatId)
        {
            if (restPoints == null) yield break;
            foreach (var p in restPoints)
                if (p.habitatId == habitatId) yield return p;
        }

        public IEnumerable<AreaLink> LinksFrom(string areaId)
        {
            if (areaLinks == null) yield break;
            foreach (var l in areaLinks)
                if (l.areaA == areaId || l.areaB == areaId) yield return l;
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
