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

        [Serializable]
        public class CharacterEntry
        {
            public string CharacterID;
            public NovelCharacter Character;
        }

        [Serializable]
        public class VariableEntry
        {
            public string VariableID;
            public NovelVariableDefinition Variable;
        }

        public List<Entry> Graphs = new List<Entry>();
        public List<CharacterEntry> Characters = new List<CharacterEntry>();
        public List<VariableEntry> Variables = new List<VariableEntry>();
        public string GeneratedAtUtc;
        private Dictionary<string, RuntimeNovelGraph> _lookup;
        private Dictionary<string, NovelCharacter> _characterLookup;
        private Dictionary<NovelCharacter, string> _characterIDs;
        private Dictionary<string, NovelVariableDefinition> _variableLookup;

        public bool TryGetGraph(string graphID, out RuntimeNovelGraph graph)
        {
            EnsureLookup();
            return _lookup.TryGetValue(graphID ?? string.Empty, out graph);
        }

        public RuntimeNovelGraph GetGraph(string graphID) =>
            TryGetGraph(graphID, out RuntimeNovelGraph graph) ? graph : null;

        public bool TryGetCharacter(string characterID, out NovelCharacter character)
        {
            EnsureLookup();
            return _characterLookup.TryGetValue(characterID ?? string.Empty, out character);
        }

        public string GetCharacterID(NovelCharacter character)
        {
            EnsureLookup();
            return character != null && _characterIDs.TryGetValue(character, out string id) ? id : null;
        }

        public bool TryGetVariable(string variableID, out NovelVariableDefinition variable)
        {
            EnsureLookup();
            return _variableLookup.TryGetValue(variableID ?? string.Empty, out variable);
        }

        public static NovelGraphCatalog LoadDefault() =>
            Resources.Load<NovelGraphCatalog>("NovelGraphCatalog");

        public void ReplaceEntries(IEnumerable<Entry> entries)
        {
            Graphs = entries != null ? new List<Entry>(entries) : new List<Entry>();
            _lookup = null;
        }

        public void ReplaceAssetEntries(IEnumerable<CharacterEntry> characters, IEnumerable<VariableEntry> variables)
        {
            Characters = characters != null ? new List<CharacterEntry>(characters) : new List<CharacterEntry>();
            Variables = variables != null ? new List<VariableEntry>(variables) : new List<VariableEntry>();
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
            _characterLookup = new Dictionary<string, NovelCharacter>(StringComparer.Ordinal);
            _characterIDs = new Dictionary<NovelCharacter, string>();
            _variableLookup = new Dictionary<string, NovelVariableDefinition>(StringComparer.Ordinal);
            if (Graphs != null)
            {
                foreach (Entry entry in Graphs)
                {
                    if (entry?.Graph == null || string.IsNullOrEmpty(entry.GraphID)) continue;
                    if (!_lookup.TryAdd(entry.GraphID, entry.Graph))
                        Debug.LogWarning($"Novel Graph Catalog contains duplicate graph ID '{entry.GraphID}'.", this);
                }
            }
            if (Characters != null)
            {
                foreach (CharacterEntry entry in Characters)
                {
                    if (entry?.Character == null || string.IsNullOrEmpty(entry.CharacterID)) continue;
                    if (_characterLookup.TryAdd(entry.CharacterID, entry.Character))
                        _characterIDs[entry.Character] = entry.CharacterID;
                }
            }
            if (Variables != null)
            {
                foreach (VariableEntry entry in Variables)
                {
                    if (entry?.Variable == null || string.IsNullOrEmpty(entry.VariableID)) continue;
                    _variableLookup.TryAdd(entry.VariableID, entry.Variable);
                }
            }
        }
    }
}
