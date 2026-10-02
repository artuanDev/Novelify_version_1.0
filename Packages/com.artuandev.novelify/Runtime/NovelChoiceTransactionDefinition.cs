using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public enum NovelChoiceUnavailablePolicy { Hide, Disable }

    public enum NovelChoiceStateOperation
    {
        Set,
        Add,
        Subtract,
        Multiply,
        Divide,
        Spend
    }

    [Serializable]
    public sealed class NovelChoiceStateChangeDefinition
    {
        public NovelVariableDefinition Variable;
        public NovelChoiceStateOperation Operation;
        public bool BooleanValue;
        public int IntegerValue;
        public float FloatValue;
        public string StringValue = string.Empty;

        public RuntimeValue CreateValue() => Variable == null
            ? RuntimeValue.None()
            : Variable.Type switch
            {
                NovelVariableType.Boolean => RuntimeValue.From(BooleanValue),
                NovelVariableType.Integer => RuntimeValue.From(IntegerValue),
                NovelVariableType.Float => RuntimeValue.From(FloatValue),
                NovelVariableType.String => RuntimeValue.From(StringValue),
                _ => RuntimeValue.None()
            };
    }

    /// <summary>
    /// A reusable group of state changes validated in full before a choice commits any of them.
    /// Use Spend for a non-negative numeric cost that must be affordable.
    /// </summary>
    [CreateAssetMenu(menuName = "Novelify/Choice Transaction", fileName = "New Choice Transaction")]
    public sealed class NovelChoiceTransactionDefinition : ScriptableObject
    {
        public List<NovelChoiceStateChangeDefinition> Changes = new List<NovelChoiceStateChangeDefinition>();
    }
}
