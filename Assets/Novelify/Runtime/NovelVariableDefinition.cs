using System;
using UnityEngine;

namespace Novelify
{
    public enum NovelVariableType { Boolean, Integer, Float, String }
    public enum NovelVariableScope { Profile, Story, CallLocal }

    [CreateAssetMenu(menuName = "Novelify/Variable Definition", fileName = "New Novel Variable")]
    public sealed class NovelVariableDefinition : ScriptableObject
    {
        [SerializeField, HideInInspector] private string _id;
        public string DisplayName;
        public NovelVariableType Type;
        public NovelVariableScope Scope = NovelVariableScope.Story;

        public bool DefaultBoolean;
        public int DefaultInteger;
        public float DefaultFloat;
        public string DefaultString = string.Empty;

        public string ID => _id;
        public string Name => string.IsNullOrWhiteSpace(DisplayName) ? name : DisplayName.Trim();
        public RuntimeValueKind ValueKind => Type switch
        {
            NovelVariableType.Boolean => RuntimeValueKind.Boolean,
            NovelVariableType.Integer => RuntimeValueKind.Integer,
            NovelVariableType.Float => RuntimeValueKind.Float,
            NovelVariableType.String => RuntimeValueKind.String,
            _ => RuntimeValueKind.None
        };

        public RuntimeValue CreateDefaultValue() => Type switch
        {
            NovelVariableType.Boolean => RuntimeValue.From(DefaultBoolean),
            NovelVariableType.Integer => RuntimeValue.From(DefaultInteger),
            NovelVariableType.Float => RuntimeValue.From(DefaultFloat),
            NovelVariableType.String => RuntimeValue.From(DefaultString),
            _ => RuntimeValue.None()
        };

        public void EnsureID()
        {
            if (string.IsNullOrEmpty(_id)) _id = Guid.NewGuid().ToString("N");
        }

        public void RegenerateID() => _id = Guid.NewGuid().ToString("N");

        private void OnEnable() => EnsureID();
        private void OnValidate() => EnsureID();
    }
}
