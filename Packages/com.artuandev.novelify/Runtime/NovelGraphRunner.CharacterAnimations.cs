using System.Collections;
using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        private bool BeginCharacterAnimation(RuntimeAnimateCharacterNode node, int version)
        {
            NovelCharacterReference target = ResolveCharacterReference(
                node.CharacterReferenceValue, node.CharacterValue, node.Character, node.InstanceID);
            CharacterInfo character = ShowCharacter(target.Character, target.InstanceID);
            if (character == null) return false;
            float duration = AsFloat(Evaluate(node.DurationValue), node.Duration);
            character.StartSimpleAnimation(node.Animation,
                AsFloat(Evaluate(node.AmplitudeValue), node.Amplitude),
                AsFloat(Evaluate(node.FrequencyValue), node.Frequency), duration);
            if (!node.WaitForCompletion || duration <= 0f || !character.IsAnimating) return false;
            _isWaiting = true;
            _waitCoroutine = StartCoroutine(WaitForCharacterAnimation(node, version, character));
            return true;
        }

        private void StopCharacterAnimation(RuntimeStopCharacterAnimationNode node)
        {
            NovelCharacterReference target = ResolveCharacterReference(
                node.CharacterReferenceValue, node.CharacterValue, node.Character, node.InstanceID);
            if (Stage.TryGet(target.Character, target.InstanceID, out CharacterInfo character))
                character.StopSimpleAnimation();
        }

        private IEnumerator WaitForCharacterAnimation(RuntimeNode node, int version, CharacterInfo character)
        {
            do { yield return null; } while (character != null && character.IsAnimating);
            _waitCoroutine = null;
            _isWaiting = false;
            if (version == _flowVersion && _currentNode == node) AdvanceCurrentNode();
        }
    }
}
