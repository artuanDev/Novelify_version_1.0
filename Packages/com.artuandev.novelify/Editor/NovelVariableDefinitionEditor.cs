using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    [CustomEditor(typeof(NovelVariableDefinition))]
    public sealed class NovelVariableDefinitionEditor : UnityEditor.Editor
    {
        private SerializedProperty _displayName;
        private SerializedProperty _type;
        private SerializedProperty _scope;
        private SerializedProperty _defaultBoolean;
        private SerializedProperty _defaultInteger;
        private SerializedProperty _defaultFloat;
        private SerializedProperty _defaultString;

        private void OnEnable()
        {
            _displayName = serializedObject.FindProperty("DisplayName");
            _type = serializedObject.FindProperty("Type");
            _scope = serializedObject.FindProperty("Scope");
            _defaultBoolean = serializedObject.FindProperty("DefaultBoolean");
            _defaultInteger = serializedObject.FindProperty("DefaultInteger");
            _defaultFloat = serializedObject.FindProperty("DefaultFloat");
            _defaultString = serializedObject.FindProperty("DefaultString");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            NovelVariableDefinition definition = (NovelVariableDefinition)target;
            EditorGUILayout.PropertyField(_displayName);
            EditorGUILayout.PropertyField(_type);
            EditorGUILayout.PropertyField(_scope);
            switch ((NovelVariableType)_type.enumValueIndex)
            {
                case NovelVariableType.Boolean: EditorGUILayout.PropertyField(_defaultBoolean, new GUIContent("Default Value")); break;
                case NovelVariableType.Integer: EditorGUILayout.PropertyField(_defaultInteger, new GUIContent("Default Value")); break;
                case NovelVariableType.Float: EditorGUILayout.PropertyField(_defaultFloat, new GUIContent("Default Value")); break;
                case NovelVariableType.String: EditorGUILayout.PropertyField(_defaultString, new GUIContent("Default Value")); break;
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Stable identity", EditorStyles.boldLabel);
            EditorGUILayout.SelectableLabel(definition.ID ?? string.Empty, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Copy ID")) EditorGUIUtility.systemCopyBuffer = definition.ID;
                if (GUILayout.Button("Regenerate ID") && EditorUtility.DisplayDialog(
                        "Regenerate variable ID?",
                        "Existing graph references and future save data use this ID. Regenerate it only to repair an accidental duplicate.",
                        "Regenerate", "Cancel"))
                {
                    Undo.RecordObject(definition, "Regenerate Novel Variable ID");
                    definition.RegenerateID();
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                }
            }
            EditorGUILayout.HelpBox(
                definition.Scope == NovelVariableScope.CallLocal
                    ? "Call-local values are isolated per function call. The main graph has its own root-local scope."
                    : definition.Scope == NovelVariableScope.Profile
                        ? "Profile values live in the assigned state store and may be shared by managers when the same store is injected."
                        : "Story values live in one manager's state store for the current playthrough.",
                MessageType.Info);
        }
    }

    internal sealed class NovelVariableIdentityValidator : AssetPostprocessor
    {
        private static bool _queued;

        [InitializeOnLoadMethod]
        private static void ValidateOnLoad() => QueueValidation();

        private static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Any(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(NovelVariableDefinition)) ||
                movedAssets.Any(path => AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(NovelVariableDefinition)))
                QueueValidation();
        }

        private static void QueueValidation()
        {
            if (_queued) return;
            _queued = true;
            EditorApplication.delayCall += ValidateIdentities;
        }

        private static void ValidateIdentities()
        {
            _queued = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueValidation();
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string path in AssetDatabase.FindAssets("t:NovelVariableDefinition")
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal))
            {
                NovelVariableDefinition definition = AssetDatabase.LoadAssetAtPath<NovelVariableDefinition>(path);
                if (definition == null) continue;
                bool changed = definition.EnsureID();
                if (!ids.Add(definition.ID))
                {
                    definition.RegenerateID();
                    ids.Add(definition.ID);
                    changed = true;
                    Debug.LogWarning($"Novelify assigned a new stable ID to duplicated variable definition '{path}'.", definition);
                }
                if (changed)
                {
                    EditorUtility.SetDirty(definition);
                    AssetDatabase.SaveAssetIfDirty(definition);
                }
            }
        }
    }
}
