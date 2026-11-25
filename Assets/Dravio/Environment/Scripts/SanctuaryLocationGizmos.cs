using UnityEngine;

namespace Dravio.EnvironmentArt
{
    /// <summary>Scene-view drawing of <see cref="SanctuaryLocationData"/>. No runtime behaviour.</summary>
    public sealed class SanctuaryLocationGizmos : MonoBehaviour
    {
        public SanctuaryLocationData data;
        public bool drawObstacles = true;
        const float Lift = 0.06f;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (data == null || data.habitats == null) return;
            foreach (var h in data.habitats)
            {
                Gizmos.color = ColorFor(h.id);
                DrawPoly(h.polygonXZ);
                if (h.swimPolygonXZ != null && h.swimPolygonXZ.Length >= 6)
                {
                    Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.9f);
                    DrawPoly(h.swimPolygonXZ);
                }
            }
            if (drawObstacles && data.obstacles != null)
            {
                Gizmos.color = new Color(1f, 0.3f, 0.25f, 0.8f);
                foreach (var o in data.obstacles) DrawPoly(o.polygonXZ);
            }
            if (data.spawnPoints != null)
                foreach (var s in data.spawnPoints)
                {
                    Gizmos.color = ColorFor(s.habitatId);
                    Gizmos.DrawSphere(s.Position + Vector3.up * 0.25f, 0.22f);
                    Gizmos.DrawLine(s.Position + Vector3.up * 0.25f, s.Position + Vector3.up * 0.25f + s.Rotation * Vector3.forward * 0.7f);
                }
            if (data.transitions != null)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.9f);
                foreach (var t in data.transitions) Gizmos.DrawWireSphere(t.Position + Vector3.up * Lift, t.radius);
            }
            if (data.perchPoints != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
                foreach (var p in data.perchPoints) Gizmos.DrawWireCube(p.Position + Vector3.up * 0.15f, Vector3.one * 0.3f);
            }
            DrawZoneData();
        }

        // U1-ENV.4: swim / fly volumes, walk areas at their height, rest points, gates.
        void DrawZoneData()
        {
            if (data.walkAreas != null)
                foreach (var a in data.walkAreas)
                {
                    Gizmos.color = ColorFor(a.habitatId);
                    DrawPolyAt(a.polygonXZ, a.y + Lift);
                }
            if (data.volumes != null)
                foreach (var v in data.volumes)
                {
                    Gizmos.color = v.medium == "fly" ? new Color(0.85f, 0.9f, 1f, 0.6f) : new Color(0.3f, 0.75f, 1f, 0.6f);
                    DrawPolyAt(v.polygonXZ, v.yMin);
                    DrawPolyAt(v.polygonXZ, v.yMax);
                }
            if (data.restPoints != null)
            {
                Gizmos.color = new Color(0.6f, 0.9f, 0.8f, 0.9f);
                foreach (var p in data.restPoints) Gizmos.DrawWireCube(p.Position + Vector3.up * 0.1f, new Vector3(0.5f, 0.2f, 0.5f));
            }
            if (data.areaLinks != null)
            {
                Gizmos.color = new Color(1f, 1f, 0.6f, 0.8f);
                foreach (var l in data.areaLinks) Gizmos.DrawWireSphere(l.Position, l.radius);
            }
            if (data.zoneGates != null)
            {
                Gizmos.color = new Color(1f, 0.4f, 1f, 0.95f);
                foreach (var g in data.zoneGates)
                {
                    Gizmos.DrawWireSphere(g.Position, g.radius);
                    Gizmos.DrawLine(g.Position, g.Position + Vector3.up * 2f);
                }
            }
        }

        static void DrawPolyAt(float[] xz, float y)
        {
            if (xz == null || xz.Length < 4) return;
            int n = xz.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Gizmos.DrawLine(new Vector3(xz[i * 2], y, xz[i * 2 + 1]), new Vector3(xz[j * 2], y, xz[j * 2 + 1]));
            }
        }

        static void DrawPoly(float[] xz)
        {
            if (xz == null || xz.Length < 4) return;
            int n = xz.Length / 2;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Gizmos.DrawLine(new Vector3(xz[i * 2], Lift, xz[i * 2 + 1]), new Vector3(xz[j * 2], Lift, xz[j * 2 + 1]));
            }
        }

        static Color ColorFor(string habitatId) => habitatId switch
        {
            "origin_meadow" => new Color(0.45f, 0.95f, 0.35f, 1f),
            "origin_pondbank" => new Color(0.35f, 0.8f, 1f, 1f),
            "origin_warm_rocks" => new Color(1f, 0.65f, 0.3f, 1f),
            "ocean_estuary" => new Color(0.55f, 0.9f, 0.6f, 1f),
            "ocean_reef" => new Color(1f, 0.55f, 0.6f, 1f),
            "ocean_cool_pool" => new Color(0.45f, 0.7f, 1f, 1f),
            "sky_low_island" => new Color(0.95f, 0.85f, 1f, 1f),
            _ => Color.white,
        };
#endif
    }
}
