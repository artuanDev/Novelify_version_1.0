using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    public class NovelCharacterCreator : EditorWindow
    {
        private static readonly Color Ink = new Color32(7, 14, 25, 255);
        private static readonly Color Chrome = new Color32(14, 27, 45, 255);
        private static readonly Color Card = new Color32(18, 35, 57, 255);
        private static readonly Color Border = new Color32(45, 75, 103, 255);
        private static readonly Color Accent = new Color32(66, 185, 236, 255);
        private static readonly Color Text = new Color32(242, 246, 251, 255);
        private static readonly Color Muted = new Color32(151, 172, 194, 255);

        [SerializeField] private NovelCharacter _character;
        private UnityEditor.Editor _inspector;
        private ScrollView _content;
        private ObjectField _picker;

        [MenuItem("Window/Novelify/Character Creator")]
        public static void OpenWindow() => Open(Selection.activeObject as NovelCharacter);

        public static void Open(NovelCharacter character)
        {
            var window = GetWindow<NovelCharacterCreator>("Character Creator");
            window.minSize = new Vector2(420, 600);
            if (character != null) window.SelectCharacter(character);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.backgroundColor = Ink;
            rootVisualElement.style.color = Text;

            VisualElement header = new VisualElement();
            header.style.paddingLeft = header.style.paddingRight = 16f;
            header.style.paddingTop = 14f;
            header.style.paddingBottom = 12f;
            header.style.backgroundColor = Chrome;
            header.style.borderBottomWidth = 1f;
            header.style.borderBottomColor = Border;

            VisualElement brand = new VisualElement();
            brand.style.flexDirection = FlexDirection.Row;
            brand.style.alignItems = Align.Center;
            VisualElement mark = new VisualElement();
            mark.style.width = mark.style.height = 36f;
            mark.style.marginRight = 11f;
            mark.style.backgroundColor = Accent;
            SetRadius(mark, 10f);
            Label markLabel = new Label("N");
            markLabel.style.flexGrow = 1f;
            markLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            markLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            markLabel.style.fontSize = 18f;
            markLabel.style.color = Color.white;
            mark.Add(markLabel);
            brand.Add(mark);
            VisualElement titles = new VisualElement();
            Label title = new Label("Character Studio");
            title.style.fontSize = 17f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Text;
            Label subtitle = new Label("Build layered portraits and preview their performance.");
            subtitle.style.fontSize = 10f;
            subtitle.style.color = Muted;
            titles.Add(title);
            titles.Add(subtitle);
            brand.Add(titles);
            header.Add(brand);

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 12f;
            Button create = new Button(() => CreateCharacter(false)) { text = "+  New Character" };
            StyleButton(create, true);
            actions.Add(create);
            Button duplicateButton = new Button(() => CreateCharacter(true)) { text = "Duplicate" };
            StyleButton(duplicateButton, false);
            actions.Add(duplicateButton);
            Button saveButton = new Button(() =>
            {
                if (_character != null) AssetDatabase.SaveAssetIfDirty(_character);
            }) { text = "Save Asset" };
            StyleButton(saveButton, false);
            actions.Add(saveButton);
            header.Add(actions);
            rootVisualElement.Add(header);

            VisualElement pickerCard = new VisualElement();
            pickerCard.style.marginLeft = pickerCard.style.marginRight = 14f;
            pickerCard.style.marginTop = 12f;
            pickerCard.style.paddingLeft = pickerCard.style.paddingRight = 12f;
            pickerCard.style.paddingTop = pickerCard.style.paddingBottom = 10f;
            pickerCard.style.backgroundColor = Card;
            SetRadius(pickerCard, 9f);
            SetBorder(pickerCard, Border);
            Label pickerEyebrow = new Label("ACTIVE CHARACTER");
            pickerEyebrow.style.fontSize = 9f;
            pickerEyebrow.style.letterSpacing = 1.5f;
            pickerEyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;
            pickerEyebrow.style.color = Accent;
            pickerCard.Add(pickerEyebrow);
            _picker = new ObjectField("Character Asset") { objectType = typeof(NovelCharacter), allowSceneObjects = false };
            _picker.style.marginTop = 5f;
            _picker.RegisterValueChangedCallback(evt => SelectCharacter(evt.newValue as NovelCharacter));
            pickerCard.Add(_picker);
            rootVisualElement.Add(pickerCard);

            _content = new ScrollView(ScrollViewMode.Vertical);
            _content.style.flexGrow = 1;
            _content.style.paddingLeft = _content.style.paddingRight = 14f;
            _content.style.paddingTop = 4f;
            _content.style.paddingBottom = 14f;
            rootVisualElement.Add(_content);
            SelectCharacter(_character);
        }

        private void SelectCharacter(NovelCharacter character)
        {
            _character = character;
            if (_content == null) return;
            _picker.SetValueWithoutNotify(character);
            _content.Clear();
            if (_inspector != null) DestroyImmediate(_inspector);
            if (character == null)
            {
                VisualElement empty = new VisualElement();
                empty.style.marginTop = 12f;
                empty.style.paddingLeft = empty.style.paddingRight = 18f;
                empty.style.paddingTop = empty.style.paddingBottom = 22f;
                empty.style.backgroundColor = Card;
                SetRadius(empty, 10f);
                SetBorder(empty, Border);
                Label emptyTitle = new Label("Your cast starts here");
                emptyTitle.style.fontSize = 15f;
                emptyTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
                emptyTitle.style.color = Text;
                Label emptyCopy = new Label("Create a character or select an existing asset. You can assemble portrait layers, preview emotions, and test talking and blinking before using it in a story graph.");
                emptyCopy.style.marginTop = 5f;
                emptyCopy.style.whiteSpace = WhiteSpace.Normal;
                emptyCopy.style.color = Muted;
                empty.Add(emptyTitle);
                empty.Add(emptyCopy);
                _content.Add(empty);
                return;
            }
            _inspector = UnityEditor.Editor.CreateEditor(character);
            _content.Add(CharacterPreview.Create(character));
            var fields = new VisualElement();
            fields.style.marginTop = 12f;
            fields.style.paddingLeft = fields.style.paddingRight = 12f;
            fields.style.paddingTop = fields.style.paddingBottom = 10f;
            fields.style.backgroundColor = Card;
            SetRadius(fields, 10f);
            SetBorder(fields, Border);
            InspectorElement.FillDefaultInspector(fields, _inspector.serializedObject, _inspector);
            fields.Bind(_inspector.serializedObject);
            _content.Add(fields);
        }

        private void CreateCharacter(bool duplicate)
        {
            if (duplicate && _character == null) return;
            string suggested = duplicate ? _character.name + " Copy" : "New Character";
            string path = EditorUtility.SaveFilePanelInProject("Create Novel Character", suggested, "asset", "Choose where to save the character.");
            if (string.IsNullOrEmpty(path)) return;
            NovelCharacter character = duplicate ? Instantiate(_character) : CreateInstance<NovelCharacter>();
            if (!duplicate) character.SpeakerName = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(character, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(character);
            SelectCharacter(character);
            EditorGUIUtility.PingObject(character);
        }

        private void OnDisable()
        {
            if (_inspector != null) DestroyImmediate(_inspector);
        }

        private static void StyleButton(Button button, bool primary)
        {
            button.style.height = 30f;
            button.style.marginRight = 7f;
            button.style.paddingLeft = button.style.paddingRight = 13f;
            button.style.backgroundColor = primary ? Accent : Card;
            button.style.color = primary ? Color.white : Text;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetRadius(button, 6f);
            SetBorder(button, primary ? Accent : Border);
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = element.style.borderBottomRightRadius = radius;
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
