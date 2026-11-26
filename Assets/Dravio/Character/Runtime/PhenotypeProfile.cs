using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character
{
    /// <summary>
    /// Data-driven genotype/variant -> material property overrides. The GDD does not yet define what any
    /// allele looks like, so no rules ship with the project: an empty profile changes nothing. Art/design add
    /// rules here once the mapping is decided; code never invents one.
    /// Overrides are applied with a MaterialPropertyBlock only when at least one rule matches (property blocks
    /// opt the renderer out of the SRP Batcher, so unmodified dragons keep batching).
    /// </summary>
    [CreateAssetMenu(menuName = "Dravio/Character/Phenotype Profile", fileName = "PhenotypeProfile")]
    public sealed class PhenotypeProfile : ScriptableObject
    {
        public enum ValueKind { Color, Float }

        [Serializable]
        public sealed class Rule
        {
            [Tooltip("Empty = any species")] public string speciesId;
            [Tooltip("Locus id, e.g. pigment. Empty = rule keyed on variant only")] public string locus;
            [Tooltip("Allele pair, order-insensitive, e.g. Rr")] public string alleles;
            [Tooltip("Empty = any variant")] public string variantId;
            public string materialProperty;
            public ValueKind kind;
            public Color color = Color.white;
            public float value;
        }

        public struct Override
        {
            public string property;
            public ValueKind kind;
            public Color color;
            public float value;
        }

        public List<Rule> rules = new List<Rule>();

        public List<Override> Resolve(string speciesId, string variantId, IList<GenotypeEntry> genotype)
        {
            return Resolve(rules, speciesId, variantId, genotype);
        }

        /// <summary>Pure: every matching rule in order; later rules for the same property win.</summary>
        public static List<Override> Resolve(IList<Rule> rules, string speciesId, string variantId, IList<GenotypeEntry> genotype)
        {
            var byProperty = new Dictionary<string, int>(StringComparer.Ordinal);
            var result = new List<Override>();
            if (rules == null) return result;
            foreach (var r in rules)
            {
                if (r == null || string.IsNullOrEmpty(r.materialProperty)) continue;
                if (!string.IsNullOrEmpty(r.speciesId) && r.speciesId != speciesId) continue;
                if (!string.IsNullOrEmpty(r.variantId) && r.variantId != variantId) continue;
                if (!string.IsNullOrEmpty(r.locus))
                {
                    string have = null;
                    if (genotype != null)
                        foreach (var g in genotype) if (g.locus == r.locus) { have = g.alleles; break; }
                    if (have == null || !SamePair(have, r.alleles)) continue;
                }
                else if (string.IsNullOrEmpty(r.variantId)) continue; // a rule must key on something
                var o = new Override { property = r.materialProperty, kind = r.kind, color = r.color, value = r.value };
                int idx;
                if (byProperty.TryGetValue(r.materialProperty, out idx)) result[idx] = o;
                else { byProperty[r.materialProperty] = result.Count; result.Add(o); }
            }
            return result;
        }

        /// <summary>"Rr" == "rR"; case matters (upper case is the dominant allele).</summary>
        public static bool SamePair(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            if (a.Length != 2) return a == b;
            return (a[0] == b[0] && a[1] == b[1]) || (a[0] == b[1] && a[1] == b[0]);
        }
    }
}
