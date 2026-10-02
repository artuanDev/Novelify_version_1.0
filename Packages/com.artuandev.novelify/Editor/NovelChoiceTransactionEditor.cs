using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    [CustomPropertyDrawer(typeof(NovelChoiceStateChangeDefinition))]
    public sealed class NovelChoiceStateChangeDefinitionDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            Rect row = new Rect(position.x, position.y, position.width, line);
            SerializedProperty variable = property.FindPropertyRelative("Variable");
            SerializedProperty operation = property.FindPropertyRelative("Operation");
            EditorGUI.PropertyField(row, variable, label);
            row.y += line + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(row, operation);
            row.y += line + EditorGUIUtility.standardVerticalSpacing;

            NovelVariableDefinition definition = variable.objectReferenceValue as NovelVariableDefinition;
            string valueProperty = definition?.Type switch
            {
                NovelVariableType.Boolean => "BooleanValue",
                NovelVariableType.Integer => "IntegerValue",
                NovelVariableType.Float => "FloatValue",
                NovelVariableType.String => "StringValue",
                _ => "IntegerValue"
            };
            GUIContent valueLabel = (NovelChoiceStateOperation)operation.enumValueIndex == NovelChoiceStateOperation.Spend
                ? new GUIContent("Cost", "The choice is unavailable unless this non-negative numeric amount can be spent.")
                : new GUIContent("Value");
            using (new EditorGUI.DisabledScope(definition == null))
                EditorGUI.PropertyField(row, property.FindPropertyRelative(valueProperty), valueLabel);
            EditorGUI.EndProperty();
        }
    }
}
