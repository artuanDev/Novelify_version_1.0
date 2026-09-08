using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    [Serializable]
    public sealed class NovelStateStore
    {
        [NonSerialized] private Dictionary<string, RuntimeValue> _profileValues;
        [NonSerialized] private Dictionary<string, RuntimeValue> _storyValues;

        public event Action<NovelVariableDefinition, RuntimeValue> ValueChanged;

        public RuntimeValue Get(NovelVariableDefinition definition)
        {
            if (definition == null) return RuntimeValue.None();
            if (definition.Scope == NovelVariableScope.CallLocal)
            {
                Debug.LogWarning($"Call-local variable '{definition.Name}' must be read through a NovelManager call scope.");
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
                error = $"Call-local variable '{definition.Name}' must be written through a NovelManager call scope.";
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

        public void ResetStory() => _storyValues?.Clear();
        public void ResetProfile() => _profileValues?.Clear();
        public void ResetAll() { ResetStory(); ResetProfile(); }

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
    }
}
