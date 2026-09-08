using System;
using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        internal RuntimeValue ReadSessionVariable(NovelVariableDefinition definition) => ReadVariable(definition);

        internal bool TryWriteSessionVariable(NovelVariableDefinition definition, RuntimeValue value, out string error) =>
            TryWriteVariable(definition, value, out error);

        private RuntimeValue Evaluate(RuntimeValueExpression expression)
        {
            switch (expression)
            {
                case null:
                    return RuntimeValue.None();
                case RuntimeConstantExpression constant:
                    return constant.Value ?? RuntimeValue.None();
                case RuntimeFunctionInputExpression input:
                    return _valueScope.Inputs.TryGetValue(input.Name ?? string.Empty, out RuntimeValue inputValue)
                        ? inputValue
                        : RuntimeValue.None();
                case RuntimeFunctionOutputExpression output:
                    return _valueScope.GetOutput(output.CallNodeID, output.Name);
                case RuntimeArithmeticExpression arithmetic:
                    return EvaluateArithmetic(arithmetic);
                case RuntimeCharacterComponentExpression character:
                    return EvaluateCharacterComponent(character);
                case RuntimeMakeCharacterReferenceExpression makeReference:
                    return RuntimeValue.From(new NovelCharacterReference(
                        AsObject<NovelCharacter>(Evaluate(makeReference.Character), null),
                        AsString(Evaluate(makeReference.InstanceID), string.Empty)));
                case RuntimeCharacterReferenceComponentExpression referenceComponent:
                    NovelCharacterReference reference = AsCharacterReference(Evaluate(referenceComponent.Reference));
                    return referenceComponent.Component == RuntimeCharacterReferenceComponent.InstanceID
                        ? RuntimeValue.From(reference.InstanceID)
                        : RuntimeValue.From(reference.Character);
                case RuntimeVariableExpression variable:
                    return ReadVariable(variable.Variable);
                case RuntimeComparisonExpression comparison:
                    return RuntimeValue.From(EvaluateComparison(comparison));
                case RuntimeBooleanExpression boolean:
                    return RuntimeValue.From(EvaluateBoolean(boolean));
                default:
                    return RuntimeValue.None();
            }
        }

        private RuntimeValue EvaluateArithmetic(RuntimeArithmeticExpression expression)
        {
            RuntimeValue a = Evaluate(expression.A);
            RuntimeValue b = Evaluate(expression.B);
            if (expression.ValueKind == RuntimeValueKind.Vector2)
            {
                Vector2 left = AsVector2(a, Vector2.zero);
                Vector2 right = AsVector2(b, Vector2.zero);
                switch (expression.Operation)
                {
                    case RuntimeArithmeticOperation.Subtract: return RuntimeValue.From(left - right);
                    case RuntimeArithmeticOperation.Multiply: return RuntimeValue.From(Vector2.Scale(left, right));
                    case RuntimeArithmeticOperation.Divide:
                        return RuntimeValue.From(new Vector2(SafeDivide(left.x, right.x), SafeDivide(left.y, right.y)));
                    default: return RuntimeValue.From(left + right);
                }
            }

            float first = AsFloat(a, 0f);
            float second = AsFloat(b, 0f);
            switch (expression.Operation)
            {
                case RuntimeArithmeticOperation.Subtract: return RuntimeValue.From(first - second);
                case RuntimeArithmeticOperation.Multiply: return RuntimeValue.From(first * second);
                case RuntimeArithmeticOperation.Divide: return RuntimeValue.From(SafeDivide(first, second));
                default: return RuntimeValue.From(first + second);
            }
        }

        private RuntimeValue EvaluateCharacterComponent(RuntimeCharacterComponentExpression expression)
        {
            NovelCharacter character = AsObject<NovelCharacter>(Evaluate(expression.Character), null);
            string instanceID = AsString(Evaluate(expression.InstanceID), string.Empty);
            CharacterInfo live = null;
            if (character != null) Stage.TryGet(character, instanceID, out live);

            switch (expression.Component)
            {
                case RuntimeCharacterComponent.Character: return RuntimeValue.From(character);
                case RuntimeCharacterComponent.SpeakerName: return RuntimeValue.From(character != null ? character.SpeakerName : string.Empty);
                case RuntimeCharacterComponent.Body: return RuntimeValue.From(live?.Body != null ? live.Body.sprite : character?.PortraitBody);
                case RuntimeCharacterComponent.Eyes: return RuntimeValue.From(live?.Eyes != null ? live.Eyes.sprite : character?.PortraitEyes);
                case RuntimeCharacterComponent.EyesClosed: return RuntimeValue.From(character?.PortraitEyesClosed);
                case RuntimeCharacterComponent.Details: return RuntimeValue.From(live?.Details != null ? live.Details.sprite : character?.PortraitFaceDetails);
                case RuntimeCharacterComponent.Mouth: return RuntimeValue.From(live?.Mouth != null ? live.Mouth.sprite : character?.PortraitMouth);
                case RuntimeCharacterComponent.MouthOpen: return RuntimeValue.From(character?.PortraitMouthOpen);
                case RuntimeCharacterComponent.NormalizedPosition:
                    return RuntimeValue.From(live != null ? live.AnchoredToNormalizedPosition(live.Position) : Vector2.zero);
                case RuntimeCharacterComponent.CanvasPosition:
                    return RuntimeValue.From(live != null ? live.Position : Vector2.zero);
                case RuntimeCharacterComponent.Rotation:
                    return RuntimeValue.From(live != null ? live.Rotation : 0f);
                case RuntimeCharacterComponent.Scale:
                    return RuntimeValue.From(live != null ? live.Scale : Vector2.one);
                default:
                    return RuntimeValue.None();
            }
        }

        private RuntimeValue ReadVariable(NovelVariableDefinition definition)
        {
            if (definition == null) return RuntimeValue.None();
            if (definition.Scope != NovelVariableScope.CallLocal) return StateStore.Get(definition);
            if (!_valueScope.Locals.TryGetValue(definition.ID, out RuntimeValue value))
            {
                value = NovelStateStore.Clone(definition.CreateDefaultValue());
                _valueScope.Locals[definition.ID] = value;
            }
            return NovelStateStore.Clone(value);
        }

        private bool TryWriteVariable(NovelVariableDefinition definition, RuntimeValue value, out string error)
        {
            if (definition == null) { error = "State node has no variable definition."; return false; }
            if (!NovelStateStore.Matches(definition, value))
            {
                error = $"Variable '{definition.Name}' expects {definition.Type}, but received {value?.Kind.ToString() ?? "None"}.";
                return false;
            }
            if (definition.Scope != NovelVariableScope.CallLocal)
                return StateStore.TrySet(definition, value, out error);

            RuntimeValue stored = NovelStateStore.Clone(value);
            _valueScope.Locals[definition.ID] = stored;
            LocalVariableChanged?.Invoke(definition, NovelStateStore.Clone(stored));
            RefreshVisibleChoices();
            error = null;
            return true;
        }

        private bool TryModifyVariable(RuntimeModifyVariableNode node, out string error)
        {
            error = null;
            if (node?.Variable == null) { error = "Modify Variable has no variable definition."; return false; }
            RuntimeValue current = ReadVariable(node.Variable);
            RuntimeValue amount = Evaluate(node.Amount);
            RuntimeValue result;
            if (node.Variable.Type == NovelVariableType.Integer &&
                current.Kind == RuntimeValueKind.Integer && amount.Kind == RuntimeValueKind.Integer)
            {
                int left = current.IntegerValue;
                int right = amount.IntegerValue;
                result = node.Operation switch
                {
                    RuntimeVariableModifyOperation.Subtract => RuntimeValue.From(left - right),
                    RuntimeVariableModifyOperation.Multiply => RuntimeValue.From(left * right),
                    RuntimeVariableModifyOperation.Divide => RuntimeValue.From(right == 0 ? 0 : left / right),
                    _ => RuntimeValue.From(left + right)
                };
            }
            else if (node.Variable.Type == NovelVariableType.Float &&
                     current.Kind == RuntimeValueKind.Float && amount.Kind == RuntimeValueKind.Float)
            {
                float left = current.FloatValue;
                float right = amount.FloatValue;
                result = node.Operation switch
                {
                    RuntimeVariableModifyOperation.Subtract => RuntimeValue.From(left - right),
                    RuntimeVariableModifyOperation.Multiply => RuntimeValue.From(left * right),
                    RuntimeVariableModifyOperation.Divide => RuntimeValue.From(Mathf.Approximately(right, 0f) ? 0f : left / right),
                    _ => RuntimeValue.From(left + right)
                };
            }
            else
            {
                error = $"Modify Variable requires matching numeric values for '{node.Variable.Name}'.";
                return false;
            }
            return TryWriteVariable(node.Variable, result, out error);
        }

        private bool EvaluateBoolean(RuntimeBooleanExpression expression)
        {
            bool first = AsBool(Evaluate(expression.A), false);
            if (expression.Operation == RuntimeBooleanOperation.Not) return !first;
            bool second = AsBool(Evaluate(expression.B), false);
            return expression.Operation == RuntimeBooleanOperation.And ? first && second : first || second;
        }

        private bool EvaluateComparison(RuntimeComparisonExpression expression)
        {
            RuntimeValue a = Evaluate(expression.A);
            RuntimeValue b = Evaluate(expression.B);
            if (a == null || b == null || a.Kind != expression.ValueKind || b.Kind != expression.ValueKind)
                return false;
            switch (expression.ValueKind)
            {
                case RuntimeValueKind.Boolean:
                    return CompareOrder(a.BooleanValue.CompareTo(b.BooleanValue), expression.Operation);
                case RuntimeValueKind.Integer:
                    return CompareOrder(a.IntegerValue.CompareTo(b.IntegerValue), expression.Operation);
                case RuntimeValueKind.Float:
                    if (expression.Operation == RuntimeComparisonOperation.Equal)
                        return Mathf.Approximately(a.FloatValue, b.FloatValue);
                    if (expression.Operation == RuntimeComparisonOperation.NotEqual)
                        return !Mathf.Approximately(a.FloatValue, b.FloatValue);
                    return CompareOrder(a.FloatValue.CompareTo(b.FloatValue), expression.Operation);
                case RuntimeValueKind.String:
                    return CompareOrder(string.Compare(a.StringValue ?? string.Empty, b.StringValue ?? string.Empty, StringComparison.Ordinal), expression.Operation);
                default:
                    return false;
            }
        }

        private static bool CompareOrder(int order, RuntimeComparisonOperation operation) => operation switch
        {
            RuntimeComparisonOperation.Equal => order == 0,
            RuntimeComparisonOperation.NotEqual => order != 0,
            RuntimeComparisonOperation.Less => order < 0,
            RuntimeComparisonOperation.LessOrEqual => order <= 0,
            RuntimeComparisonOperation.Greater => order > 0,
            RuntimeComparisonOperation.GreaterOrEqual => order >= 0,
            _ => false
        };

        private static float SafeDivide(float numerator, float denominator) =>
            Mathf.Approximately(denominator, 0f) ? 0f : numerator / denominator;

        private static float AsFloat(RuntimeValue value, float fallback) =>
            value?.Kind == RuntimeValueKind.Float ? value.FloatValue :
            value?.Kind == RuntimeValueKind.Integer ? value.IntegerValue : fallback;

        private static Vector2 AsVector2(RuntimeValue value, Vector2 fallback) =>
            value?.Kind == RuntimeValueKind.Vector2 ? value.Vector2Value : fallback;

        private static string AsString(RuntimeValue value, string fallback) =>
            value?.Kind == RuntimeValueKind.String ? value.StringValue ?? string.Empty : fallback;

        private static bool AsBool(RuntimeValue value, bool fallback) =>
            value?.Kind == RuntimeValueKind.Boolean ? value.BooleanValue : fallback;

        private static T AsObject<T>(RuntimeValue value, T fallback) where T : UnityEngine.Object =>
            value?.Kind == RuntimeValueKind.Object && value.ObjectValue is T typed ? typed : fallback;

        private static NovelCharacterReference AsCharacterReference(RuntimeValue value) =>
            value?.Kind == RuntimeValueKind.CharacterReference
                ? value.CharacterReferenceValue
                : new NovelCharacterReference(null);

        private NovelCharacterReference ResolveCharacterReference(
            RuntimeValueExpression referenceExpression,
            RuntimeValueExpression characterExpression,
            NovelCharacter fallbackCharacter,
            string fallbackInstanceID)
        {
            NovelCharacterReference reference = AsCharacterReference(Evaluate(referenceExpression));
            if (reference.Character != null) return reference;
            return new NovelCharacterReference(
                AsObject(Evaluate(characterExpression), fallbackCharacter),
                fallbackInstanceID);
        }
    }
}

