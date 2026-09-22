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

    [Serializable]
    public sealed class TransformTargetAuthoringList
    {
        public List<TransformTargetAuthoringEntry> Targets = new();

        public static TransformTargetAuthoringList CreateDefault(int count = 2)
        {
            var result = new TransformTargetAuthoringList();
            for (int index = 0; index < Math.Max(1, count); index++)
                result.Targets.Add(new TransformTargetAuthoringEntry());
            return result;
        }
    }

    [Serializable]
    public sealed class TransformTargetAuthoringEntry
    {
        // Graph Toolkit drops lists whose elements contain no serialized data.
        // A real field keeps every target entry persistent across graph saves.
        public int SerializationMarker = 1;
    }

    [CustomPropertyDrawer(typeof(TransformTargetAuthoringList))]
    internal sealed class TransformTargetAuthoringListDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(
            SerializedProperty property)
        {
            SerializedProperty targets = property.FindPropertyRelative(
                nameof(TransformTargetAuthoringList.Targets));
            var root = new VisualElement();
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.Center;

            var count = new Label();
            count.style.flexGrow = 1f;
            root.Add(count);

            Button remove = null;
            remove = new Button(() =>
            {
                if (targets == null || targets.arraySize <= 1)
                    return;
                Undo.RecordObject(
                    property.serializedObject.targetObject,
                    "Remove Transform Character");
                targets.DeleteArrayElementAtIndex(targets.arraySize - 1);
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            }) { text = "- Remove last" };
            remove.tooltip =
                "Remove the last character transform group and its ports.";
            root.Add(remove);

            var add = new Button(() =>
            {
                if (targets == null)
                    return;
                Undo.RecordObject(
                    property.serializedObject.targetObject,
                    "Add Transform Character");
                targets.InsertArrayElementAtIndex(targets.arraySize);
                property.serializedObject.ApplyModifiedProperties();
                Refresh();
            }) { text = "+ Add character" };
            add.tooltip =
                "Add another independently positioned character to this synchronized transform.";
            root.Add(add);

            void Refresh()
            {
                int value = Math.Max(1, targets?.arraySize ?? 1);
                count.text = value == 1
                    ? "1 animated character"
                    : $"{value} animated characters";
                remove.SetEnabled(value > 1);
            }

            Refresh();
            if (targets != null)
                root.TrackPropertyValue(targets, _ => Refresh());
            return root;
        }
    }
}
