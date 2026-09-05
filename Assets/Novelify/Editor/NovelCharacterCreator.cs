using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    public class NovelCharacterCreator : EditorWindow
    {
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
            var toolbar = new Toolbar();
            toolbar.Add(new ToolbarButton(() => CreateCharacter(false)) { text = "New Character" });
            toolbar.Add(new ToolbarButton(() => CreateCharacter(true)) { text = "Duplicate" });
            toolbar.Add(new ToolbarButton(() =>
            {
                if (_character != null) AssetDatabase.SaveAssetIfDirty(_character);
            }) { text = "Save" });
            rootVisualElement.Add(toolbar);
            _picker = new ObjectField("Character") { objectType = typeof(NovelCharacter), allowSceneObjects = false };
            _picker.RegisterValueChangedCallback(evt => SelectCharacter(evt.newValue as NovelCharacter));
            rootVisualElement.Add(_picker);
            _content = new ScrollView();
            _content.style.flexGrow = 1;
            _content.style.paddingLeft = _content.style.paddingRight = 10;
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
                _content.Add(new HelpBox("Create a new character or select an existing asset. Assign layered sprites, preview emotions and animation, then use the asset in your story nodes.", HelpBoxMessageType.Info));
                return;
            }
            _inspector = UnityEditor.Editor.CreateEditor(character);
            _content.Add(CharacterPreview.Create(character));
            var fields = new VisualElement();
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
    }
}
