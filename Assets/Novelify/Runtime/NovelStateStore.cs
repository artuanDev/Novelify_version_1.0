using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Novelify
{
    [Serializable]
    public sealed class NovelStateStore
    {
        [NonSerialized] private Dictionary<string, RuntimeValue> _profileValues;
        [NonSerialized] private Dictionary<string, RuntimeValue> _storyValues;
        [NonSerialized] private HashSet<string> _selectedChoices;
        [NonSerialized] private Dictionary<string, int> _visits;
        [NonSerialized] private HashSet<string> _readLineIDs;

        public event Action<NovelVariableDefinition, RuntimeValue> ValueChanged;
        public event Action<string> ChoiceSelected;
        public event Action StateReset;

        public RuntimeValue Get(NovelVariableDefinition definition)
        {
            if (definition == null) return RuntimeValue.None();
            if (definition.Scope == NovelVariableScope.CallLocal)
            {
                Debug.LogWarning($"Call-local variable '{definition.Name}' must be read through a NovelGraphRunner call scope.");
                return definition.CreateDefaultValue();
            }

            Dictionary<string, RuntimeValue> values = ValuesFor(definition.Scope);
            if (!values.TryGetValue(definition.ID, out RuntimeValue value))
            {
                value = Clone(definition.CreateDefaultValue());
                values[definition.ID] = value;
            }
            return Clone(value);
        }

        public bool TrySet(NovelVariableDefinition definition, RuntimeValue value, out string error)
        {
            error = null;
            if (definition == null) { error = "Variable definition is missing."; return false; }
            if (definition.Scope == NovelVariableScope.CallLocal)
            {
                error = $"Call-local variable '{definition.Name}' must be written through a NovelGraphRunner call scope.";
                return false;
            }
            if (!Matches(definition, value))
            {
                error = $"Variable '{definition.Name}' expects {definition.Type}, but received {value?.Kind.ToString() ?? "None"}.";
                return false;
            }

            RuntimeValue stored = Clone(value);
            ValuesFor(definition.Scope)[definition.ID] = stored;
            ValueChanged?.Invoke(definition, Clone(stored));
            return true;
        }

        public bool TryApplyBatch(
            IReadOnlyDictionary<NovelVariableDefinition, RuntimeValue> changes,
            out string error)
        {
            error = null;
            if (changes == null || changes.Count == 0) return true;
            foreach (KeyValuePair<NovelVariableDefinition, RuntimeValue> change in changes)
            {
                if (change.Key == null) { error = "A state change has no variable definition."; return false; }
                if (change.Key.Scope == NovelVariableScope.CallLocal)
                {
                    error = $"Call-local variable '{change.Key.Name}' cannot be committed by the shared state store.";
                    return false;
                }
                if (!Matches(change.Key, change.Value))
                {
                    error = $"Variable '{change.Key.Name}' expects {change.Key.Type}, but received {change.Value?.Kind.ToString() ?? "None"}.";
                    return false;
                }
            }

            foreach (KeyValuePair<NovelVariableDefinition, RuntimeValue> change in changes)
                ValuesFor(change.Key.Scope)[change.Key.ID] = Clone(change.Value);
            foreach (KeyValuePair<NovelVariableDefinition, RuntimeValue> change in changes)
                ValueChanged?.Invoke(change.Key, Clone(change.Value));
            return true;
        }

        public bool HasSelectedChoice(string choiceID) =>
            !string.IsNullOrEmpty(choiceID) && (_selectedChoices?.Contains(choiceID) ?? false);

        public bool MarkChoiceSelected(string choiceID)
        {
            if (string.IsNullOrEmpty(choiceID)) return false;
            _selectedChoices ??= new HashSet<string>(StringComparer.Ordinal);
            if (!_selectedChoices.Add(choiceID)) return false;
            ChoiceSelected?.Invoke(choiceID);
            return true;
        }

        public void RecordVisit(string graphID, string nodeID)
        {
            if (string.IsNullOrEmpty(nodeID)) return;
            _visits ??= new Dictionary<string, int>(StringComparer.Ordinal);
            string key = VisitKey(graphID, nodeID);
            _visits[key] = GetVisitCount(graphID, nodeID) + 1;
        }

        public int GetVisitCount(string graphID, string nodeID) =>
            _visits != null && _visits.TryGetValue(VisitKey(graphID, nodeID), out int count) ? count : 0;

        public bool MarkLineRead(string lineID)
        {
            if (string.IsNullOrEmpty(lineID)) return false;
            _readLineIDs ??= new HashSet<string>(StringComparer.Ordinal);
            return _readLineIDs.Add(lineID);
        }

        public bool HasReadLine(string lineID) =>
            !string.IsNullOrEmpty(lineID) && (_readLineIDs?.Contains(lineID) ?? false);

        public List<NovelSavedVariable> CaptureStoryVariables() => CaptureValues(_storyValues);
        public List<NovelSavedVariable> CaptureProfileVariables() => CaptureValues(_profileValues);
        public List<string> CaptureSelectedChoices() =>
            _selectedChoices?.OrderBy(value => value, StringComparer.Ordinal).ToList() ?? new List<string>();
        public List<string> CaptureReadLineIDs() =>
            _readLineIDs?.OrderBy(value => value, StringComparer.Ordinal).ToList() ?? new List<string>();

        public List<NovelVisitData> CaptureVisits()
        {
            var result = new List<NovelVisitData>();
            if (_visits == null) return result;
            foreach (KeyValuePair<string, int> item in _visits.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                SplitVisitKey(item.Key, out string graphID, out string nodeID);
                result.Add(new NovelVisitData { GraphID = graphID, NodeID = nodeID, Count = item.Value });
            }
            return result;
        }

        public bool RestoreStory(
            IReadOnlyList<NovelSavedVariable> variables,
            IReadOnlyList<string> selectedChoices,
            IReadOnlyList<NovelVisitData> visits,
            IReadOnlyList<string> readLineIDs,
            out string error)
        {
            if (!TryRestoreValues(variables, out Dictionary<string, RuntimeValue> restored, out error)) return false;
            var restoredChoices = new HashSet<string>(selectedChoices?.Where(value => !string.IsNullOrEmpty(value)) ??
                                                      Enumerable.Empty<string>(), StringComparer.Ordinal);
            var restoredReadLines = new HashSet<string>(readLineIDs?.Where(value => !string.IsNullOrEmpty(value)) ??
                                                        Enumerable.Empty<string>(), StringComparer.Ordinal);
            var restoredVisits = new Dictionary<string, int>(StringComparer.Ordinal);
            if (visits != null)
            {
                foreach (NovelVisitData visit in visits)
                {
                    if (visit == null || string.IsNullOrEmpty(visit.NodeID) || visit.Count < 0)
                    { error = "A saved visit entry is invalid."; return false; }
                    restoredVisits[VisitKey(visit.GraphID, visit.NodeID)] = visit.Count;
                }
            }
            _storyValues = restored;
            _selectedChoices = restoredChoices;
            _readLineIDs = restoredReadLines;
            _visits = restoredVisits;
            StateReset?.Invoke();
            return true;
        }

        public bool RestoreProfile(IReadOnlyList<NovelSavedVariable> variables, out string error)
        {
            if (!TryRestoreValues(variables, out Dictionary<string, RuntimeValue> restored, out error)) return false;
            _profileValues = restored;
            StateReset?.Invoke();
            return true;
        }

        public void ResetStory()
        {
            _storyValues?.Clear();
            _selectedChoices?.Clear();
            _visits?.Clear();
            _readLineIDs?.Clear();
            StateReset?.Invoke();
        }
        public void ResetProfile()
        {
            _profileValues?.Clear();
            StateReset?.Invoke();
        }
        public void ResetAll()
        {
            _storyValues?.Clear();
            _profileValues?.Clear();
            _selectedChoices?.Clear();
            _visits?.Clear();
            _readLineIDs?.Clear();
            StateReset?.Invoke();
        }

        public static bool Matches(NovelVariableDefinition definition, RuntimeValue value) =>
            definition != null && value != null && value.Kind == definition.ValueKind;

        public static RuntimeValue Clone(RuntimeValue value)
        {
            if (value == null) return RuntimeValue.None();
            return new RuntimeValue
            {
                Kind = value.Kind,
                FloatValue = value.FloatValue,
                IntegerValue = value.IntegerValue,
                BooleanValue = value.BooleanValue,
                StringValue = value.StringValue,
                Vector2Value = value.Vector2Value,
                ObjectValue = value.ObjectValue,
                CharacterReferenceValue = value.CharacterReferenceValue
            };
        }

        private Dictionary<string, RuntimeValue> ValuesFor(NovelVariableScope scope)
        {
            if (scope == NovelVariableScope.Profile)
                return _profileValues ??= new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);
            return _storyValues ??= new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);
        }

        private static List<NovelSavedVariable> CaptureValues(Dictionary<string, RuntimeValue> values)
        {
            var result = new List<NovelSavedVariable>();
            if (values == null) return result;
            foreach (KeyValuePair<string, RuntimeValue> item in values.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!IsPersistentKind(item.Value?.Kind ?? RuntimeValueKind.None)) continue;
                result.Add(new NovelSavedVariable { VariableID = item.Key, Value = Serialize(item.Value) });
            }
            return result;
        }

        private static bool TryRestoreValues(IReadOnlyList<NovelSavedVariable> values,
            out Dictionary<string, RuntimeValue> restored, out string error)
        {
            restored = new Dictionary<string, RuntimeValue>(StringComparer.Ordinal);
            error = null;
            if (values == null) return true;
            foreach (NovelSavedVariable item in values)
            {
                if (item == null || string.IsNullOrEmpty(item.VariableID) || item.Value == null ||
                    !IsPersistentKind(item.Value.Kind))
                { error = "A saved variable is missing an ID or has an unsupported value type."; return false; }
                if (!restored.TryAdd(item.VariableID, Deserialize(item.Value)))
                { error = $"Saved variable ID '{item.VariableID}' is duplicated."; return false; }
            }
            return true;
        }

        internal static bool IsPersistentKind(RuntimeValueKind kind) =>
            kind == RuntimeValueKind.Boolean || kind == RuntimeValueKind.Integer ||
            kind == RuntimeValueKind.Float || kind == RuntimeValueKind.String;

        internal static NovelSerializedValue Serialize(RuntimeValue value) => new NovelSerializedValue
        {
            Kind = value?.Kind ?? RuntimeValueKind.None,
            FloatValue = value?.FloatValue ?? 0f,
            IntegerValue = value?.IntegerValue ?? 0,
            BooleanValue = value?.BooleanValue ?? false,
            StringValue = value?.StringValue ?? string.Empty,
            Vector2Value = value?.Vector2Value ?? Vector2.zero
        };

        internal static RuntimeValue Deserialize(NovelSerializedValue value) => new RuntimeValue
        {
            Kind = value?.Kind ?? RuntimeValueKind.None,
            FloatValue = value?.FloatValue ?? 0f,
            IntegerValue = value?.IntegerValue ?? 0,
            BooleanValue = value?.BooleanValue ?? false,
            StringValue = value?.StringValue ?? string.Empty,
            Vector2Value = value?.Vector2Value ?? Vector2.zero
        };

        private static string VisitKey(string graphID, string nodeID) =>
            (graphID ?? string.Empty) + "\n" + (nodeID ?? string.Empty);

        private static void SplitVisitKey(string key, out string graphID, out string nodeID)
        {
            int separator = key?.IndexOf('\n') ?? -1;
            graphID = separator >= 0 ? key.Substring(0, separator) : string.Empty;
            nodeID = separator >= 0 ? key.Substring(separator + 1) : key ?? string.Empty;
        }
    }
}
