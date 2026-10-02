using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    /// <summary>
    /// Explicit layout helper for NovelBoxStyle. This intentionally is not a
    /// CustomPropertyDrawer: Unity 6000.6 can resolve property drawers while an
    /// assembly reload is still settling, which is unsafe for this editor.
    /// </summary>
    internal static class NovelBoxStyleEditorGUI
    {
        public static void Draw(
            SerializedProperty property,
            GUIContent label)
        {
            if (property == null)
                return;

            property.isExpanded = EditorGUILayout.Foldout(
                property.isExpanded, label, true);
            if (!property.isExpanded)
                return;

            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel++;

            DrawSection("Background");
            Draw(property, nameof(NovelBoxStyle.FillColor), "Tint");
            DrawSlider(property, nameof(NovelBoxStyle.Opacity), "Opacity");
            Draw(property, nameof(NovelBoxStyle.FillTexture), "Texture");
            Draw(property, nameof(NovelBoxStyle.FillTiling), "Tiling");
            Draw(property, nameof(NovelBoxStyle.FillOffset), "Offset");

            DrawSection("Shape");
            Draw(property, nameof(NovelBoxStyle.CornerRadius),
                "Corner Radius");

            DrawSection("Outline");
            SerializedProperty enabled = Draw(
                property, nameof(NovelBoxStyle.OutlineEnabled), "Enabled");
            if (enabled != null && enabled.boolValue)
            {
                Draw(property, nameof(NovelBoxStyle.OutlineColor), "Tint");
                DrawSlider(
                    property,
                    nameof(NovelBoxStyle.OutlineTransparency),
                    "Transparency");
                Draw(property, nameof(NovelBoxStyle.OutlineThickness),
                    "Thickness");
                Draw(property, nameof(NovelBoxStyle.OutlineTexture),
                    "Texture");
                Draw(property, nameof(NovelBoxStyle.OutlineTiling),
                    "Tiling");
                Draw(property, nameof(NovelBoxStyle.OutlineOffset),
                    "Offset");
            }

            EditorGUI.indentLevel = previousIndent;
            EditorGUILayout.Space(4f);
        }

        private static void DrawSection(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        }

        private static SerializedProperty Draw(
            SerializedProperty parent,
            string relativeName,
            string label)
        {
            SerializedProperty child = parent.FindPropertyRelative(relativeName);
            if (child != null)
                EditorGUILayout.PropertyField(child, new GUIContent(label), true);
            return child;
        }

        private static void DrawSlider(
            SerializedProperty parent,
            string relativeName,
            string label)
        {
            SerializedProperty child = parent.FindPropertyRelative(relativeName);
            if (child != null)
            {
                child.floatValue = EditorGUILayout.Slider(
                    label, child.floatValue, 0f, 1f);
            }
        }
    }
}
