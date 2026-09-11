using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Novelify
{
    /// <summary>Owns character lookup and creation within one manager's stage.</summary>
    public sealed class NovelCharacterStage
    {
        private readonly Transform _root;
        private readonly GameObject _prefab;
        private readonly Dictionary<string, CharacterInfo> _characters = new Dictionary<string, CharacterInfo>();
        public IReadOnlyDictionary<string, CharacterInfo> Characters => _characters;

        public NovelCharacterStage(Transform root, GameObject prefab)
        {
            _root = root;
            _prefab = prefab;
            if (root == null) return;
            foreach (CharacterInfo info in root.GetComponentsInChildren<CharacterInfo>(true))
            {
                if (info.character == null) continue;
                string key = Key(info.character, info.InstanceID);
                if (!_characters.ContainsKey(key)) _characters.Add(key, info);
                else Debug.LogWarning($"Duplicate character instance '{info.InstanceID}' for '{info.character.name}'. Assign a unique Instance ID.", info);
            }
        }

        private static string Key(NovelCharacter character, string instanceID) =>
            character.GetEntityId() + ":" + (instanceID ?? string.Empty);

        public bool TryGet(NovelCharacter character, string instanceID, out CharacterInfo info)
        {
            info = null;
            return character != null && _characters.TryGetValue(Key(character, instanceID), out info) && info != null;
        }

        public CharacterInfo Show(NovelCharacter character, string instanceID = "")
        {
            if (character == null)
            {
                Debug.LogWarning("Assign a Character input to this character node.");
                return null;
            }
            if (TryGet(character, instanceID, out CharacterInfo existing))
            {
                existing.PrepareToShow();
                return existing;

            }

            if (_root == null || _prefab == null)
            {
                Debug.LogWarning("NovelGraphRunner needs a Portrait Prefab and a Character Container (or Canvas Dialogue) to create characters.");
                return null;
            }

            GameObject portrait = Object.Instantiate(_prefab, _root, false);
            portrait.name = string.IsNullOrEmpty(instanceID) ? character.name : $"{character.name} ({instanceID})";
            portrait.transform.SetAsFirstSibling();
            CharacterInfo info = portrait.GetComponent<CharacterInfo>();
            if (info == null) info = portrait.AddComponent<CharacterInfo>();
            info.Initialize(character, instanceID);
            _characters[Key(character, instanceID)] = info;
            portrait.SetActive(true);
            return info;
        }

        public void Hide(NovelCharacter character, string instanceID = "")
        {
            if (TryGet(character, instanceID, out CharacterInfo info)) info.HideImmediately();
        }

        public CharacterInfo Hide(
            NovelCharacter character,
            string instanceID,
            CharacterTransitionMode transition,
            CharacterTransitionDirection direction,
            float duration,
            float offset,
            PortraitTweenEasing easing)
        {
            if (!TryGet(character, instanceID, out CharacterInfo info)) return null;

            info.TransitionOut(transition, direction, duration, offset, easing);

            return info;
        }

        public void BringToFront(CharacterInfo info)
        {
            if (info != null && info.transform.parent == _root) info.transform.SetAsLastSibling();
        }

        public void HideAll()
        {
            foreach (CharacterInfo info in _characters.Values)
                if (info != null) info.HideImmediately();
        }

        public void StopMovement()
        {
            foreach (CharacterInfo info in _characters.Values)
                if (info != null) info.StopMovement();
        }
    }
}
