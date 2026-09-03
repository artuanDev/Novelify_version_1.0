using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public class RuntimeNovelGraph : ScriptableObject
    {
        public string EntryNodeID;
        //  This will hold all nodes in the graph we want to load.
        public List<RuntimeDialogueNode> AllNodes = new List<RuntimeDialogueNode>();
    }
    [Serializable]
    public class RuntimeDialogueNode
    {
        public string NodeID;
        public string SpeakerName;
        public Sprite PortraitBody;
        public Sprite PortraitDetails;
        public Sprite PortraitEyes;
        public Sprite PortraitEyesClosed;
        public Sprite PortraitMouth;
        public Sprite PortraitMouthOpen;
        public string DialogueText;

        public bool ShowTextImmediately;
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
        public float PitchMinVariation = -0.05f;
        public float PitchMaxVariation = 0.05f;

        public List<ChoiceData> Choices = new List<ChoiceData>();
        public string NextNodeID;
    }

    [Serializable]
    public class ChoiceData
    {
        public string ChoiceText;
        public string DestinationNodeID;
    }

}
