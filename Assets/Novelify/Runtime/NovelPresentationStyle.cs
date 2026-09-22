using UnityEngine;

namespace Novelify
{
    /// <summary>
    /// Reusable presentation styling with no dialogue, character, placement, or timing data.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Novelify/Presentation Style",
        fileName = "New Novel Presentation Style")]
    public sealed class NovelPresentationStyle : ScriptableObject
    {
        public NovelBoxStyle DialogueBox = NovelBoxStyle.DialogueDefault;
        public NovelBoxStyle SpeakerBox = NovelBoxStyle.SpeakerDefault;
        public NovelBoxStyle SpeechBubble = NovelBoxStyle.BubbleDefault;

        public NovelBoxStyle DialogueBoxStyle => DialogueBox.Validated();
        public NovelBoxStyle SpeakerBoxStyle => SpeakerBox.Validated();
        public NovelBoxStyle SpeechBubbleStyle => SpeechBubble.Validated();

        private void OnValidate()
        {
            DialogueBox = DialogueBox.Validated();
            SpeakerBox = SpeakerBox.Validated();
            SpeechBubble = SpeechBubble.Validated();
        }
    }
}
