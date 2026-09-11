using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Novelify.Editor.Tests
{
    public sealed class RichDialogueEditorInteractionTests
    {
        private sealed class DialogueHolder : ScriptableObject
        {
            public RichDialogueText Dialogue = new RichDialogueText();
        }

        [UnityTest]
        public IEnumerator FormattedGlyphClickFocusesInputAndAcceptsTyping()
        {
            DialogueHolder holder = ScriptableObject.CreateInstance<DialogueHolder>();
            holder.Dialogue = new RichDialogueText("<size=160%>a</size>b");
            var serializedObject = new SerializedObject(holder);
            SerializedProperty property = serializedObject.FindProperty(
                nameof(DialogueHolder.Dialogue));

            Type drawerType = Type.GetType(
                "Novelify.Editor.NovelRichDialogueInspector, Novelify.Editor",
                true);
            var drawer = (PropertyDrawer)Activator.CreateInstance(
                drawerType,
                true);
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            window.position = new Rect(80f, 80f, 600f, 600f);
            VisualElement editor = drawer.CreatePropertyGUI(property);
            window.rootVisualElement.Add(editor);
            window.Show();

            try
            {
                yield return null;
                yield return null;

                TextField input = editor.Q<TextField>(
                    "rich-dialogue-keyboard-input");
                VisualElement flow = editor.Q<VisualElement>(
                    "rich-dialogue-text-flow");
                Label firstGlyph = flow?.Q<Label>();
                Assert.That(input, Is.Not.Null);
                Assert.That(firstGlyph, Is.Not.Null);

                var systemEvent = new Event
                {
                    type = EventType.MouseDown,
                    button = 0,
                    clickCount = 1,
                    mousePosition = firstGlyph.worldBound.center +
                        Vector2.right * firstGlyph.worldBound.width * 0.25f
                };
                using (PointerDownEvent pointerDown =
                       PointerDownEvent.GetPooled(systemEvent))
                {
                    firstGlyph.SendEvent(pointerDown);
                }

                yield return null;
                Assert.That(input.cursorIndex, Is.EqualTo(1));
                Assert.That(input.selectIndex, Is.EqualTo(1));
                Assert.That(
                    input.focusController.focusedElement,
                    Is.Not.Null,
                    "The keyboard editor did not retain focus after the glyph click.");

                var keyEvent = new Event
                {
                    type = EventType.KeyDown,
                    keyCode = KeyCode.X,
                    character = 'x'
                };
                using (KeyDownEvent keyDown = KeyDownEvent.GetPooled(keyEvent))
                {
                    input.focusController.focusedElement.SendEvent(keyDown);
                }

                yield return null;
                Assert.That(input.value, Is.EqualTo("axb"));
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(holder);
            }
        }
    }
}
