using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public class NovelGraphCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string GraphID;
            public RuntimeNovelGraph Graph;
        }

        public List<Entry> Graphs = new List<Entry>();
        public string GeneratedAtUtc;
        private Dictionary<string, RuntimeNovelGraph> _lookup;

        public bool TryGetGraph(string graphID, out RuntimeNovelGraph graph)
        {
            EnsureLookup();
            return _lookup.TryGetValue(graphID ?? string.Empty, out graph);
        }

        public RuntimeNovelGraph GetGraph(string graphID) =>
            TryGetGraph(graphID, out RuntimeNovelGraph graph) ? graph : null;

        public static NovelGraphCatalog LoadDefault() =>
            Resources.Load<NovelGraphCatalog>("NovelGraphCatalog");

        public void ReplaceEntries(IEnumerable<Entry> entries)
        {
            Graphs = entries != null ? new List<Entry>(entries) : new List<Entry>();
            _lookup = null;
        }

        private void OnEnable() => _lookup = null;

#if UNITY_EDITOR
        private void OnValidate() => _lookup = null;
#endif

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, RuntimeNovelGraph>(StringComparer.Ordinal);
            if (Graphs == null) return;
            foreach (Entry entry in Graphs)
            {
                if (entry?.Graph == null || string.IsNullOrEmpty(entry.GraphID)) continue;
                if (!_lookup.TryAdd(entry.GraphID, entry.Graph))
                    Debug.LogWarning($"Novel Graph Catalog contains duplicate graph ID '{entry.GraphID}'.", this);
            }
        }
    }
}
