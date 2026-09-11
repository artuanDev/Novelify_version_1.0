using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    [Serializable]
    public sealed class ChoiceAuthoringList
    {
        public List<ChoiceAuthoringEntry> Entries = new();

        public static ChoiceAuthoringList CreateDefault(int count = 2)
        {
            var result = new ChoiceAuthoringList();
            for (int index = 0; index < count; index++)
                result.Entries.Add(new ChoiceAuthoringEntry());
            return result;
        }

        public ChoiceAuthoringList Clone(int minimumCount = 0)
        {
            var result = new ChoiceAuthoringList();
            foreach (ChoiceAuthoringEntry source in Entries ?? new List<ChoiceAuthoringEntry>())
            {
                result.Entries.Add(new ChoiceAuthoringEntry
                {
                    ID = source?.ID ?? string.Empty,
                    Text = source?.Text ?? string.Empty,
                    Condition = source?.Condition ?? true,
                    UnavailablePolicy = source?.UnavailablePolicy ?? NovelChoiceUnavailablePolicy.Hide,
                    DisabledReason = source?.DisabledReason ?? string.Empty,
                    OnceOnly = source?.OnceOnly ?? false,
                    Transaction = source?.Transaction
                });
            }
            while (result.Entries.Count < minimumCount)
                result.Entries.Add(new ChoiceAuthoringEntry());
            return result;
        }
    }

    [Serializable]
    public sealed class ChoiceAuthoringEntry
    {
        public string ID = string.Empty;
        public string Text = string.Empty;
        public bool Condition = true;
        public NovelChoiceUnavailablePolicy UnavailablePolicy = NovelChoiceUnavailablePolicy.Hide;
        public string DisabledReason = string.Empty;
        public bool OnceOnly;
        public NovelChoiceTransactionDefinition Transaction;
    }

    [CustomPropertyDrawer(typeof(ChoiceAuthoringList))]
    internal sealed class ChoiceAuthoringListDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            SerializedProperty entries = property.FindPropertyRelative(nameof(ChoiceAuthoringList.Entries));
            if (entries == null)
            {
                root.Add(new HelpBox("Novelify could not load the choice list.", HelpBoxMessageType.Error));
                return root;
            }

            var choices = new VisualElement();
            root.Add(choices);

            void Rebuild()
            {
                choices.Clear();
                for (int index = 0; index < entries.arraySize; index++)
                {
                    int capturedIndex = index;
                    SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                    SerializedProperty id = entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.ID));
                    SerializedProperty text = entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.Text));
                    string heading = string.IsNullOrWhiteSpace(id?.stringValue)
                        ? $"Choice {index + 1}"
                        : id.stringValue.Trim();
                    var foldout = new Foldout { text = heading, value = index == 0 };
                    foldout.tooltip = "Open to edit this choice. Its ID also names the output port.";
                    foldout.Add(new PropertyField(id, "ID / output name"));
                    var textField = new PropertyField(text, "Choice text");
                    foldout.Add(textField);
                    foldout.Add(new PropertyField(entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.UnavailablePolicy)), "When unavailable"));
                    foldout.Add(new PropertyField(entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.DisabledReason)), "Disabled reason"));
                    foldout.Add(new PropertyField(entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.OnceOnly)), "Once only"));
                    foldout.Add(new PropertyField(entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.Transaction)), "Transaction"));

                    var remove = new Button(() =>
                    {
                        Undo.RecordObject(property.serializedObject.targetObject, "Remove Choice");
                        entries.DeleteArrayElementAtIndex(capturedIndex);
                        property.serializedObject.ApplyModifiedProperties();
                        Rebuild();
                    }) { text = "Remove choice" };
                    remove.style.marginTop = 4f;
                    foldout.Add(remove);
                    choices.Add(foldout);
                }
            }

            var add = new Button(() =>
            {
                Undo.RecordObject(property.serializedObject.targetObject, "Add Choice");
                int index = entries.arraySize;
                entries.InsertArrayElementAtIndex(index);
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.ID)).stringValue = string.Empty;
                entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.Text)).stringValue = string.Empty;
                entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.Condition)).boolValue = true;
                entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.UnavailablePolicy)).enumValueIndex =
                    (int)NovelChoiceUnavailablePolicy.Hide;
                entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.DisabledReason)).stringValue = string.Empty;
                entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.OnceOnly)).boolValue = false;
                entry.FindPropertyRelative(nameof(ChoiceAuthoringEntry.Transaction)).objectReferenceValue = null;
                property.serializedObject.ApplyModifiedProperties();
                Rebuild();
            }) { text = "+ Add choice" };
            add.style.marginTop = 5f;
            root.Add(add);

            Rebuild();
            return root;
        }
    }
}
