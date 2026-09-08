using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        private NovelCharacterStage Stage
        {
            get
            {
                if (_stage != null) return _stage;
                if (CharacterContainer == null && CanvasDialogue != null)
                    CreateCharacterContainer();
                _stage = new NovelCharacterStage(CharacterContainer, PortraitPrefab);
                return _stage;
            }
        }

        public CharacterInfo ShowCharacter(NovelCharacter character, string instanceID = "")
        {
            CharacterInfo info = Stage.Show(character, instanceID);
            if (info != null) info.TimeMode = TimeMode;
            return info;
        }

        public bool SearchAlreadyCreatedCharacter(NovelCharacter character, string instanceID = "") =>
            Stage.TryGet(character, instanceID, out _);

        private void CreateCharacterContainer()
        {
            Canvas canvas = CanvasDialogue.GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : CanvasDialogue.transform;
            var container = new GameObject("Novelify Character Stage", typeof(RectTransform));
            var rect = (RectTransform)container.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            rect.SetAsFirstSibling();
            CharacterContainer = rect;
            _ownsContainer = true;
        }

        private void OnDestroy()
        {
            if (_ownsContainer && CharacterContainer != null)
                Destroy(CharacterContainer.gameObject);
        }
    }
}

