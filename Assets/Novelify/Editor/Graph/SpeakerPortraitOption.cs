using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    [Serializable]
    public struct SpeakerPortraitOption
    {
        public NovelCharacter Character;
    }

    [CustomPropertyDrawer(typeof(SpeakerPortraitOption))]
    public class SpeakerPortraitOptionDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty characterProperty = property.FindPropertyRelative(
                nameof(SpeakerPortraitOption.Character));

            var root = new VisualElement();
            root.style.alignItems = Align.Center;
            root.style.marginTop = 2f;
            root.style.marginBottom = 3f;

            var viewport = new VisualElement
            {
                name = "speaker-portrait-viewport",
                pickingMode = PickingMode.Ignore
            };
            viewport.style.flexGrow = 0f;
            viewport.style.flexShrink = 0f;
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.55f);

            var preview = new Image
            {
                name = "speaker-portrait-preview",
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            preview.style.position = Position.Absolute;
            preview.style.left = 0f;
            preview.style.right = 0f;
            preview.style.top = 0f;
            preview.style.bottom = 0f;

            var emptyLabel = new Label("Connect a Character variable to Speaker");
            emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            emptyLabel.style.whiteSpace = WhiteSpace.Normal;
            emptyLabel.style.paddingLeft = 8f;
            emptyLabel.style.paddingRight = 8f;
            emptyLabel.style.position = Position.Absolute;
            emptyLabel.style.left = 0f;
            emptyLabel.style.right = 0f;
            emptyLabel.style.top = 0f;
            emptyLabel.style.bottom = 0f;

            viewport.Add(preview);
            viewport.Add(emptyLabel);
            root.Add(viewport);

            void RefreshPreview()
            {
                var character = characterProperty.objectReferenceValue as NovelCharacter;
                Sprite portrait = character != null ? character.Portrait : null;
                float width = character != null
                    ? Mathf.Clamp(character.PreviewWidth, 80f, 220f)
                    : 160f;
                float height = character != null
                    ? Mathf.Clamp(character.PreviewHeight, 60f, 220f)
                    : 64f;
                float zoom = character != null
                    ? Mathf.Clamp(character.PreviewZoom, 1f, 4f)
                    : 1f;

                viewport.style.width = width;
                viewport.style.height = height;
                preview.sprite = portrait;
                preview.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
                preview.style.translate = character != null
                    ? new Translate(
                        character.PreviewOffsetX * width * 0.5f,
                        -character.PreviewOffsetY * height * 0.5f)
                    : Translate.None();

                bool hasPortrait = portrait != null;
                emptyLabel.text = character == null
                    ? "Connect a Character variable to Speaker"
                    : $"{character.SpeakerName}\nNo portrait assigned";
                preview.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
                emptyLabel.style.display = hasPortrait ? DisplayStyle.None : DisplayStyle.Flex;
            }

            RefreshPreview();
            root.TrackPropertyValue(characterProperty, _ => RefreshPreview());
            root.schedule.Execute(RefreshPreview).Every(250);

            return root;
        }
    }
}
