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
            root.Add(new Button(() => NovelCharacterCreator.Open((NovelCharacter)target)) { text = "Open Character Creator" });
            root.Add(CharacterPreview.Create((NovelCharacter)target));
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            return root;
        }
    }

    internal static class CharacterPreview
    {
        public static VisualElement Create(NovelCharacter character)
        {
            var root = new VisualElement();
            root.style.marginTop = 8;
            root.style.marginBottom = 12;
            var emotion = new EnumField("Preview Emotion", CharacterEmotion.Neutral);
            var talking = new Toggle("Preview Talking") { value = true };
            var blinking = new Toggle("Preview Blinking") { value = true };
            root.Add(emotion);
            root.Add(talking);
            root.Add(blinking);
            var stage = new VisualElement();
            stage.style.height = 300;
            stage.style.backgroundColor = new Color(0.045f, 0.065f, 0.1f);
            stage.style.overflow = Overflow.Hidden;
            stage.style.marginTop = 8;
            root.Add(stage);
            var body = AddLayer(stage);
            var eyes = AddLayer(stage);
            var details = AddLayer(stage);
            var mouth = AddLayer(stage);
            var caption = new Label();
            caption.style.unityFontStyleAndWeight = FontStyle.Bold;
            caption.style.marginTop = 6;
            root.Add(caption);
            var sample = new TextField("Example Line") { value = "We can make this story our own." };
            root.Add(sample);
            var example = new Label();
            example.style.whiteSpace = WhiteSpace.Normal;
            example.style.minHeight = 36;
            root.Add(example);
            root.Add(new HelpBox("Add an entry under Emotions to override its portrait layers. Empty layers inherit the default sprites. Preview controls do not modify the character.", HelpBoxMessageType.Info));

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
    }
}
