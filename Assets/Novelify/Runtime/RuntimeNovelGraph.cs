using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public class RuntimeNovelGraph : ScriptableObject
    {
        public string EntryNodeID;

        // Required so Unity preserves RuntimeDialogueNode,
        // RuntimeChoiceNode, RuntimePlaySoundNode, etc.
        [SerializeReference]
        public List<RuntimeNode> AllNodes = new List<RuntimeNode>();
    }

    [Serializable]
    public class RuntimeNode
    {
        public string NodeID;
        public string NextNodeID;
    }

    [Serializable]
    public class RuntimeDialogueNode : RuntimeNode
    {
        public string SpeakerName;

        public Sprite PortraitBody;
        public Sprite PortraitDetails;
        public Sprite PortraitEyes;
        public Sprite PortraitEyesClosed;
        public Sprite PortraitMouth;
        public Sprite PortraitMouthOpen;

        public string DialogueText;

        public bool ShowTextImmediately;
        public float CharactersPerSecond = 30f;
        public bool AnimateMouth = true;
        public bool AnimateBlinking = true;
        public CharacterEmotion Emotion = CharacterEmotion.Neutral;

        public float MouthFrameInterval = 0.12f;
        public float MouthTimingVariation = 0.35f;
        public float MouthPauseChance = 0.12f;
        public float MouthPauseMultiplier = 1.8f;

        public float BlinkIntervalMin = 2.5f;
        public float BlinkIntervalMax = 5f;
        public float BlinkDuration = 0.12f;

        public AudioClip TalkSound;
        public AudioClip PlaySound;

        public float PitchMinVariation = -0.05f;
        public float PitchMaxVariation = 0.05f;
    }

    // A choice node contains dialogue presentation data,
    // plus its available choices.
    [Serializable]
    public class RuntimeChoiceNode : RuntimeDialogueNode
    {
        public List<ChoiceData> Choices = new List<ChoiceData>();
    }

    [Serializable]
    public class RuntimePlaySoundNode : RuntimeNode
    {
        public bool Loop;
        public AudioClip ClipSound;

        public float Volume;
        public int Priority;
        public float Pitch;
    }

    [Serializable]
    public class RuntimeTranslateSpeakerPortraitNode : RuntimeNode
    {
        public float OffsetX;
        public float OffsetY;
    }

    [Serializable]
    public class ChoiceData
    {
        public string ChoiceText;
        public string DestinationNodeID;
    }
}