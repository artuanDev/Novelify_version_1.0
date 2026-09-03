using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TextCore.Text;
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

            var previewBody = new Image
            {
                name = "speaker-portrait-preview",
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            var previewEyes = new Image
            {
                name = "speaker-portrait-eyes-preview",
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            var previewDetails = new Image
            {
                name = "speaker-portrait-eyes-preview",
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            var previewMouth = new Image
            {
                name = "speaker-portrait-eyes-preview",
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            PreparePreviewImage(previewBody);
            PreparePreviewImage(previewEyes);
            PreparePreviewImage(previewDetails);
            PreparePreviewImage(previewMouth);

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

            viewport.Add(previewBody);
            viewport.Add(previewEyes);
            viewport.Add(previewDetails);
            viewport.Add(previewMouth);
            viewport.Add(emptyLabel);
            root.Add(viewport);

            void RefreshPreview()
            {
                var character = characterProperty.objectReferenceValue as NovelCharacter;
                Sprite portrait_body = character != null ? character.PortraitBody : null;
                Sprite portrait_eyes = character != null ? character.PortraitEyes : null;
                Sprite portrait_details = character != null ? character.PortraitFaceDetails : null;
                Sprite portrait_mouth = character != null ? character.PortraitMouth : null;
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

                PlacePreviewImage(previewBody, portrait_body,zoom, zoom, character, width, height);
                PlacePreviewImage(previewEyes, portrait_eyes,zoom, zoom, character, width, height);
                PlacePreviewImage(previewDetails, portrait_details,zoom, zoom, character, width, height);
                PlacePreviewImage(previewMouth, portrait_mouth,zoom, zoom, character, width, height);

                bool hasPortrait = portrait_body != null;
                emptyLabel.text = character == null
                    ? "Connect a Character variable to Speaker"
                    : $"{character.SpeakerName}\nNo portrait assigned";
                previewBody.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
                emptyLabel.style.display = hasPortrait ? DisplayStyle.None : DisplayStyle.Flex;
            }

            RefreshPreview();
            root.TrackPropertyValue(characterProperty, _ => RefreshPreview());
            root.schedule.Execute(RefreshPreview).Every(250);

            return root;
        }
        private void PreparePreviewImage(Image _preview)
        {
            _preview.style.position = Position.Absolute;
            _preview.style.left = 0f;
            _preview.style.right = 0f;
            _preview.style.top = 0f;
            _preview.style.bottom = 0f;
        }

        private void PlacePreviewImage(
            Image _previewImage, Sprite _previewSprite,
            float zoomx, float zoomy,
            NovelCharacter _character,
            float _width, float _height)
        {
            _previewImage.sprite = _previewSprite;
            _previewImage.style.scale = new Scale(new Vector3(zoomx, zoomy, 1f));
            _previewImage.style.translate = _character != null
                ? new Translate(
                    _character.PreviewOffsetX * _width * 0.5f,
                    -_character.PreviewOffsetY * _height * 0.5f)
                : Translate.None();
        }
    }
}
