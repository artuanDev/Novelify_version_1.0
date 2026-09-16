using UnityEngine;

namespace Novelify
{
    public partial class NovelGraphRunner
    {
        private NovelGeneratedPresentation _generatedPresentation;

        private NovelGeneratedPresentation GeneratedPresentation
        {
            get
            {
                if (_generatedPresentation == null)
                {
                    _generatedPresentation =
                        GetComponent<NovelGeneratedPresentation>();
                    if (_generatedPresentation == null)
                    {
                        _generatedPresentation = gameObject.AddComponent<
                            NovelGeneratedPresentation>();
                    }
                    _generatedPresentation.Initialize(this);
                }
                return _generatedPresentation;
            }
        }

        private void EnsureGeneratedPresentation(bool buildUI)
        {
            NovelGeneratedPresentation presentation = GeneratedPresentation;
            if (buildUI)
                presentation.EnsureReady();
        }

        private void PrepareGeneratedDialogue(RuntimeDialogueNode node)
        {
            if (_customPresentation == null)
                GeneratedPresentation.PrepareForDialogue(node);
        }

        private void TrackGeneratedSpeechBubble(RuntimeDialogueNode node)
        {
            if (_customPresentation != null)
                return;
            if (node is RuntimeSpeechBubbleNode bubble)
                GeneratedPresentation.TrackSpeechBubble(bubble, _speaker);
            else
                GeneratedPresentation.StopTrackingSpeechBubble();
        }

        private void ExecuteGeneratedPlayMusic(RuntimePlayMusicNode node)
        {
            AudioClip clip = AsObject(
                Evaluate(node.ClipValue), node.Clip);
            float volume = AsFloat(
                Evaluate(node.VolumeValue), node.Volume);
            float pitch = AsFloat(
                Evaluate(node.PitchValue), node.Pitch);
            GeneratedPresentation.PlayAudio(
                node.Channel,
                clip,
              volume,
              pitch,
              node.Loop,
              node.ReplaceCurrent,
              node.Priority);
        }

        private bool BeginGeneratedFade(RuntimeFadeNode node, int version)
        {
            float duration = Mathf.Max(0f, AsFloat(
                Evaluate(node.DurationValue), node.Duration));
            float speed = Mathf.Max(0.001f, AsFloat(
                Evaluate(node.SpeedValue), node.Speed));
            float effectiveDuration = duration / speed;
            bool wait = node.WaitForCompletion && effectiveDuration > 0f;

            if (wait)
                _isWaiting = true;

            GeneratedPresentation.BeginFade(
                node is RuntimeFadeOutNode,
                effectiveDuration,
                node.Color,
                node.Easing,
                node.BlockInput,
                wait ? () => CompleteGeneratedFade(node, version) : null);
            return wait;
        }

        private void CompleteGeneratedFade(RuntimeFadeNode node, int version)
        {
            if (version != _flowVersion || _currentNode != node)
                return;
            _isWaiting = false;
            AdvanceCurrentNode();
        }
   }
}