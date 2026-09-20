using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    [CustomEditor(typeof(NovelCharacter))]
    public class NovelCharacterEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            Button open = new Button(() => NovelCharacterCreator.Open((NovelCharacter)target))
            {
                text = "Open in Character Studio  →"
            };
            open.style.height = 32f;
            open.style.marginTop = 7f;
            open.style.marginBottom = 4f;
            open.style.backgroundColor = (Color)new Color32(42, 126, 177, 255);
            open.style.color = Color.white;
            open.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetRadius(open, 6f);
            root.Add(open);
            root.Add(CharacterPreview.Create((NovelCharacter)target));
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }

        private static void SetRadius(VisualElement element, float value)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = value;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = value;
        }
    }

    internal static class CharacterPreview
    {
        public static VisualElement Create(NovelCharacter character)
        {
            var root = new VisualElement();
            root.style.marginTop = 8;
            root.style.marginBottom = 12;
            root.style.paddingLeft = root.style.paddingRight = 10f;
            root.style.paddingTop = root.style.paddingBottom = 10f;
            root.style.backgroundColor = (Color)new Color32(14, 28, 46, 255);
            SetRadius(root, 10f);
            SetBorder(root, new Color32(43, 74, 102, 255));

            VisualElement heading = new VisualElement();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.Center;
            Label previewTitle = new Label("PORTRAIT PERFORMANCE");
            previewTitle.style.flexGrow = 1f;
            previewTitle.style.fontSize = 10f;
            previewTitle.style.letterSpacing = 1.2f;
            previewTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            previewTitle.style.color = (Color)new Color32(83, 199, 238, 255);
            Label live = new Label("● LIVE");
            live.style.fontSize = 9f;
            live.style.color = (Color)new Color32(91, 218, 174, 255);
            heading.Add(previewTitle);
            heading.Add(live);
            root.Add(heading);

            VisualElement controls = new VisualElement();
            controls.style.marginTop = 7f;
            controls.style.paddingLeft = controls.style.paddingRight = 8f;
            controls.style.paddingTop = controls.style.paddingBottom = 6f;
            controls.style.backgroundColor = (Color)new Color32(20, 39, 62, 255);
            SetRadius(controls, 7f);
            var emotion = new EnumField("Emotion", CharacterEmotion.Neutral);
            var talking = new Toggle("Talking animation") { value = true };
            var blinking = new Toggle("Blink animation") { value = true };
            controls.Add(emotion);
            controls.Add(talking);
            controls.Add(blinking);
            root.Add(controls);
            var stage = new VisualElement();
            stage.style.height = 300;
            stage.style.backgroundColor = (Color)new Color32(7, 17, 30, 255);
            stage.style.overflow = Overflow.Hidden;
            stage.style.marginTop = 8;
            SetRadius(stage, 8f);
            SetBorder(stage, new Color32(42, 76, 103, 255));
            root.Add(stage);
            AddGrid(stage);
            var body = AddLayer(stage);
            var eyes = AddLayer(stage);
            var details = AddLayer(stage);
            var mouth = AddLayer(stage);
            var caption = new Label();
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.marginTop = 8;
            caption.style.fontSize = 13f;
            caption.style.color = (Color)new Color32(240, 245, 250, 255);
            root.Add(caption);
            var sample = new TextField("Preview line") { value = "We can make this story our own." };
            sample.style.marginTop = 5f;
            root.Add(sample);
            var example = new Label();
            example.style.whiteSpace = WhiteSpace.Normal;
            example.style.minHeight = 44;
            example.style.marginTop = 5f;
            example.style.paddingLeft = example.style.paddingRight = 11f;
            example.style.paddingTop = example.style.paddingBottom = 9f;
            example.style.backgroundColor = (Color)new Color32(22, 40, 62, 255);
            example.style.color = (Color)new Color32(225, 233, 242, 255);
            SetRadius(example, 7f);
            SetBorder(example, new Color32(49, 80, 108, 255));
            root.Add(example);
            Label hint = new Label("Emotion entries override individual portrait layers; empty layers inherit the defaults. Preview controls never modify the asset.");
            hint.style.marginTop = 8f;
            hint.style.fontSize = 9f;
            hint.style.color = (Color)new Color32(145, 168, 190, 255);
            hint.style.whiteSpace = WhiteSpace.Normal;
            root.Add(hint);

            double started = EditorApplication.timeSinceStartup;
            root.schedule.Execute(() =>
            {
                if (character == null) return;
                CharacterEmotion selected = (CharacterEmotion)emotion.value;
                CharacterPortrait portrait = character.GetPortrait(selected);
                double elapsed = EditorApplication.timeSinceStartup - started;
                float blinkInterval = Mathf.Max(0.1f, (character.BlinkIntervalMin + character.BlinkIntervalMax) * 0.5f);
                float blinkDuration = Mathf.Max(0.02f, character.BlinkDuration);
                bool closed = blinking.value && elapsed % (blinkInterval + blinkDuration) >= blinkInterval;
                bool open = talking.value && (int)(elapsed / Mathf.Max(0.02f, character.MouthFrameInterval)) % 2 == 1;
                SetSprite(body, portrait.Body);
                SetSprite(eyes, closed && portrait.EyesClosed != null ? portrait.EyesClosed : portrait.Eyes);
                SetSprite(details, portrait.Details);
                SetSprite(mouth, open && portrait.MouthOpen != null ? portrait.MouthOpen : portrait.Mouth);
                caption.text = $"{character.SpeakerName} · {selected}";
                string line = sample.value ?? string.Empty;
                int count = talking.value ? (int)(elapsed * 30) % (line.Length + 60) : line.Length;
                example.text = line.Substring(0, Mathf.Min(count, line.Length));
            }).Every(50);
            return root;
        }

        private static void AddGrid(VisualElement stage)
        {
            for (int i = 1; i < 6; i++)
            {
                VisualElement vertical = new VisualElement { pickingMode = PickingMode.Ignore };
                vertical.style.position = Position.Absolute;
                vertical.style.left = Length.Percent(i * 100f / 6f);
                vertical.style.top = 0f;
                vertical.style.bottom = 0f;
                vertical.style.width = 1f;
                vertical.style.backgroundColor = new Color(0.19f, 0.38f, 0.51f, 0.22f);
                stage.Add(vertical);
            }
            for (int i = 1; i < 4; i++)
            {
                VisualElement horizontal = new VisualElement { pickingMode = PickingMode.Ignore };
                horizontal.style.position = Position.Absolute;
                horizontal.style.left = 0f;
                horizontal.style.right = 0f;
                horizontal.style.top = Length.Percent(i * 25f);
                horizontal.style.height = 1f;
                horizontal.style.backgroundColor = new Color(0.19f, 0.38f, 0.51f, 0.22f);
                stage.Add(horizontal);
            }
        }

        private static Image AddLayer(VisualElement stage)
        {
            var image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.style.position = Position.Absolute;
            image.style.left = image.style.right = image.style.top = image.style.bottom = 0;
            stage.Add(image);
            return image;
        }

        private static void SetSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static void SetRadius(VisualElement element, float value)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = value;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = value;
        }

        private static void SetBorder(VisualElement element, Color color)
        {
            element.style.borderLeftWidth = element.style.borderRightWidth = 1f;
            element.style.borderTopWidth = element.style.borderBottomWidth = 1f;
            element.style.borderLeftColor = element.style.borderRightColor = color;
            element.style.borderTopColor = element.style.borderBottomColor = color;
        }
    }
}
