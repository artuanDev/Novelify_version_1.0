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
            root.style.marginTop = 6f;
            root.style.marginBottom = 8f;

            var viewport = new VisualElement
            {
                name = "speaker-portrait-viewport",
                pickingMode = PickingMode.Ignore
            };
            viewport.style.flexGrow = 0f;
            viewport.style.flexShrink = 0f;
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.backgroundColor = (Color)new Color32(8, 13, 26, 255);
            viewport.style.borderTopWidth = 1f;
            viewport.style.borderRightWidth = 1f;
            viewport.style.borderBottomWidth = 1f;
            viewport.style.borderLeftWidth = 1f;
            viewport.style.borderTopColor = (Color)new Color32(71, 85, 105, 255);
            viewport.style.borderRightColor = (Color)new Color32(71, 85, 105, 255);
            viewport.style.borderBottomColor = (Color)new Color32(71, 85, 105, 255);
            viewport.style.borderLeftColor = (Color)new Color32(71, 85, 105, 255);
            viewport.style.borderTopLeftRadius = 10f;
            viewport.style.borderTopRightRadius = 10f;
            viewport.style.borderBottomLeftRadius = 10f;
            viewport.style.borderBottomRightRadius = 10f;

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
                name = "speaker-portrait-details-preview",
                scaleMode = ScaleMode.ScaleAndCrop,
                pickingMode = PickingMode.Ignore
            };
            var previewMouth = new Image
            {
                name = "speaker-portrait-mouth-preview",
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
            emptyLabel.style.color = (Color)new Color32(148, 163, 184, 255);
            emptyLabel.style.fontSize = 11f;
            emptyLabel.style.paddingLeft = 8f;
            emptyLabel.style.paddingRight = 8f;
            emptyLabel.style.position = Position.Absolute;
            emptyLabel.style.left = 0f;
            emptyLabel.style.right = 0f;
            emptyLabel.style.top = 0f;
            emptyLabel.style.bottom = 0f;

            var portraitBadge = new Label("PORTRAIT");
            portraitBadge.style.position = Position.Absolute;
            portraitBadge.style.left = 8f;
            portraitBadge.style.top = 8f;
            portraitBadge.style.height = 18f;
            portraitBadge.style.paddingLeft = 7f;
            portraitBadge.style.paddingRight = 7f;
            portraitBadge.style.unityTextAlign = TextAnchor.MiddleCenter;
            portraitBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            portraitBadge.style.fontSize = 9f;
            portraitBadge.style.color = (Color)new Color32(224, 242, 254, 255);
            portraitBadge.style.backgroundColor = new Color(0.04f, 0.08f, 0.16f, 0.86f);
            portraitBadge.style.borderTopLeftRadius = 5f;
            portraitBadge.style.borderTopRightRadius = 5f;
            portraitBadge.style.borderBottomLeftRadius = 5f;
            portraitBadge.style.borderBottomRightRadius = 5f;
            portraitBadge.pickingMode = PickingMode.Ignore;

            var speakerNameLabel = new Label();
            speakerNameLabel.style.position = Position.Absolute;
            speakerNameLabel.style.left = 0f;
            speakerNameLabel.style.right = 0f;
            speakerNameLabel.style.bottom = 0f;
            speakerNameLabel.style.height = 26f;
            speakerNameLabel.style.paddingLeft = 10f;
            speakerNameLabel.style.paddingRight = 10f;
            speakerNameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            speakerNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            speakerNameLabel.style.fontSize = 11f;
            speakerNameLabel.style.color = (Color)new Color32(241, 245, 249, 255);
            speakerNameLabel.style.backgroundColor = new Color(0.03f, 0.05f, 0.11f, 0.88f);
            speakerNameLabel.pickingMode = PickingMode.Ignore;

            viewport.Add(previewBody);
            viewport.Add(previewEyes);
            viewport.Add(previewDetails);
            viewport.Add(previewMouth);
            viewport.Add(portraitBadge);
            viewport.Add(speakerNameLabel);
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
                speakerNameLabel.text = character != null
                    ? (string.IsNullOrWhiteSpace(character.SpeakerName)
                        ? "UNNAMED CHARACTER"
                        : character.SpeakerName.ToUpperInvariant())
                    : string.Empty;
                previewBody.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
                portraitBadge.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
                speakerNameLabel.style.display = hasPortrait ? DisplayStyle.Flex : DisplayStyle.None;
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
