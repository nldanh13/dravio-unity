using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Dravio.EnvironmentArt.EditorTools
{
    /// <summary>
    /// Checks the landscape framing with a real Unity camera at 16:9, 19.5:9 and 20:9:
    /// every habitat area, spawn point, transition and perch must stay inside the
    /// part of the screen that is not covered by the right Fantasy UI panel or the
    /// safe margins. Also reports (never changes) the Player orientation settings.
    /// </summary>
    public static class SanctuaryFramingValidator
    {
        static readonly (string label, float aspect)[] Aspects =
        {
            ("16:9", 16f / 9f), ("19.5:9", 19.5f / 9f), ("20:9", 20f / 9f),
        };

        public static bool Run(bool dialog)
        {
            var layout = OriginSanctuaryEnvironmentBuilder.LoadLayout();
            var data = AssetDatabase.LoadAssetAtPath<SanctuaryLocationData>(OriginSanctuaryEnvironmentBuilder.LocationsAssetPath);
            if (data == null) data = OriginSanctuaryEnvironmentBuilder.UpdateLocationData();
            if (layout == null || data == null) return false;
            var c = layout.camera;

            var points = new List<(string name, Vector3 p)>();
            foreach (var h in data.habitats)
                for (int i = 0; i + 1 < h.polygonXZ.Length; i += 2)
                    points.Add(($"{h.id} corner {i / 2}", new Vector3(h.polygonXZ[i], 0f, h.polygonXZ[i + 1])));
            foreach (var s in data.spawnPoints) points.Add((s.id, s.Position + Vector3.up * 1.2f));
            foreach (var t in data.transitions) points.Add((t.id, t.Position));
            foreach (var p in data.perchPoints) points.Add((p.id, p.Position));

            var report = new StringBuilder();
            int failures = 0;
            var go = new GameObject("Dravio framing probe") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.enabled = false;
                cam.orthographic = true;
                foreach (var (label, aspect) in Aspects)
                {
                    SanctuaryCameraFit.ComputePose(OriginSanctuaryEnvironmentBuilder.V(c.anchorPosition),
                        OriginSanctuaryEnvironmentBuilder.V(c.eulerAngles), c.orthographicSize,
                        c.rightPanelScreenHeights, c.leftMarginScreenHeights, c.minAspect, aspect,
                        out var pos, out var rot, out float size);
                    go.transform.SetPositionAndRotation(pos, rot);
                    cam.orthographicSize = size;
                    cam.aspect = aspect;
                    float x0 = c.leftMarginScreenHeights / aspect, x1 = 1f - c.rightPanelScreenHeights / aspect;
                    float y0 = c.bottomMarginScreenHeights, y1 = 1f - c.topMarginScreenHeights;
                    int bad = 0;
                    float minX = 1f, maxX = 0f;
                    foreach (var (name, p) in points)
                    {
                        var v = cam.WorldToViewportPoint(p);
                        minX = Mathf.Min(minX, v.x);
                        maxX = Mathf.Max(maxX, v.x);
                        if (v.x < x0 || v.x > x1 || v.y < y0 || v.y > y1)
                        {
                            bad++;
                            if (bad <= 5) report.AppendLine($"  {label}: {name} at viewport ({v.x:F3}, {v.y:F3}) outside free area");
                        }
                    }
                    failures += bad;
                    report.AppendLine($"{label}: {points.Count - bad}/{points.Count} points inside free area " +
                                      $"x {x0:F3}..{x1:F3} (content x {minX:F3}..{maxX:F3}), UI panel from x {x1:F3}");
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            int unwalkable = 0;
            foreach (var s in data.spawnPoints)
                if (!data.IsWalkable(s.Position, s.habitatId)) { unwalkable++; report.AppendLine("  spawn not walkable: " + s.id); }
            report.AppendLine($"Spawn points walkable: {data.spawnPoints.Length - unwalkable}/{data.spawnPoints.Length}");
            failures += unwalkable;

            // Orientation is a project setting: report only, never change it here.
            bool landscapeOnly = PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeLeft ||
                                 PlayerSettings.defaultInterfaceOrientation == UIOrientation.LandscapeRight ||
                                 (PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation &&
                                  !PlayerSettings.allowedAutorotateToPortrait &&
                                  !PlayerSettings.allowedAutorotateToPortraitUpsideDown);
            report.AppendLine(landscapeOnly
                ? "Player orientation: landscape only (OK)."
                : "Player orientation: portrait is still allowed. Set Player > Resolution and Presentation > " +
                  "Default Orientation to Landscape Left (or Auto Rotation with portrait disabled).");

            string title = failures == 0 ? "Framing OK" : $"Framing: {failures} problem(s)";
            Debug.Log("Dravio Environment " + title + "\n" + report);
            if (dialog) EditorUtility.DisplayDialog("Dravio Environment - " + title, report.ToString(), "OK");
            return failures == 0;
        }
    }
}
