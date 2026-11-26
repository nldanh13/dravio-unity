using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character
{
    [CreateAssetMenu(menuName = "Dravio/Character/Environment Prop Catalog", fileName = "EnvironmentPropCatalog")]
    public sealed class EnvironmentPropCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string propId;
            public string category;
            public GameObject prefab;
            public float height;
            public float footprintRadius;
            public bool sizeConfirmed;
            public string validation;
        }

        public List<Entry> entries = new List<Entry>();

        public Entry Find(string propId)
        {
            foreach (var e in entries) if (e != null && e.propId == propId) return e;
            return null;
        }

        public IEnumerable<Entry> InCategory(string category)
        {
            foreach (var e in entries) if (e != null && e.category == category) yield return e;
        }
    }
}
