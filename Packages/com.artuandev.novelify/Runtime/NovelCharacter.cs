using System;
using System.Collections.Generic;
using UnityEngine;

namespace Novelify
{
    public enum CharacterEmotion
    {
        Neutral,
        Happy,
        Sad,
        Angry,
        Surprised,
        Afraid,
        Disgusted,
        Confused,
        Embarrassed,
        Excited
    }

    [Serializable]
    public class CharacterExpression
    {
        public CharacterEmotion Emotion;
        [Tooltip("Empty layers inherit the character's default sprites.")]
        public Sprite Body;
        public Sprite Eyes;
        public Sprite EyesClosed;
        public Sprite Details;
        public Sprite Mouth;
        public Sprite MouthOpen;
    }

    public struct CharacterPortrait
    {
        public Sprite Body, Eyes, EyesClosed, Details, Mouth, MouthOpen;
    }

    [CreateAssetMenu(
        fileName = "New Novel Character",
        menuName = "Novelify/Character",
        order = 0)]
    public class NovelCharacter : ScriptableObject
    {
        [Header("Dialogue")]
        [Tooltip("Name displayed by the Novel Manager while this character is speaking.")]
        public string SpeakerName;

        [Tooltip("Portrait displayed in the graph preview and by the Novel Manager at runtime.")]
        public Sprite PortraitBody;
        public Sprite PortraitMouth;
        public Sprite PortraitFaceDetails;
        public Sprite PortraitEyes;

        [Header("Portrait Animation")]
        [Tooltip("Alternate mouth sprite used while dialogue text is being revealed.")]
        public Sprite PortraitMouthOpen;

        [Min(0.02f)]
        [Tooltip("Seconds between open and closed mouth frames.")]
        public float MouthFrameInterval = 0.12f;

        [Range(0f, 0.75f)]
        [Tooltip("Random variation applied to each mouth frame's duration.")]
        public float MouthTimingVariation = 0.35f;

        [Range(0f, 0.5f)]
        [Tooltip("Chance that the mouth briefly rests closed between spoken characters.")]
        public float MouthPauseChance = 0.12f;

        [Min(1f)]
        [Tooltip("Length multiplier for natural mouth pauses.")]
        public float MouthPauseMultiplier = 1.8f;

        [Tooltip("Closed-eye sprite used for blinking.")]
        public Sprite PortraitEyesClosed;

        [Min(0.1f)]
        [Tooltip("Minimum delay in seconds between blinks.")]
        public float BlinkIntervalMin = 2.5f;

        [Min(0.1f)]
        [Tooltip("Maximum delay in seconds between blinks.")]
        public float BlinkIntervalMax = 5f;

        [Min(0.02f)]
        [Tooltip("How long the closed-eye sprite remains visible during a blink.")]
        public float BlinkDuration = 0.12f;

        [Header("Voice")]
        [Tooltip("Talking sound that will play each character is displayed in the dialogue node")]
        public AudioClip TalkSound;
        public float PitchMinVariation = -0.05f;
        public float PitchMaxVariation = 0.05f;

        [Header("Graph Preview")]
        [Range(80f, 220f)]
        [Tooltip("Width of this character's portrait preview inside Dialogue and Choice nodes.")]
        public float PreviewWidth = 100f;

        [Range(60f, 220f)]
        [Tooltip("Height of this character's portrait preview inside Dialogue and Choice nodes.")]
        public float PreviewHeight = 150f;

        [Range(1f, 4f)]
        [Tooltip("Zoom used to crop the portrait inside its graph preview frame.")]
        public float PreviewZoom = 1f;

        [Range(-1f, 1f)]
        [Tooltip("Positive values move the portrait right; negative values move it left.")]
        public float PreviewOffsetX;

        [Range(-1f, 1f)]
        [Tooltip("Positive values move the portrait up; negative values move it down.")]
        public float PreviewOffsetY;

        [Header("Emotions")]
        public List<CharacterExpression> Expressions = new List<CharacterExpression>();

        public CharacterPortrait GetPortrait(CharacterEmotion emotion)
        {
            CharacterExpression expression = Expressions?.Find(item => item != null && item.Emotion == emotion);
            return new CharacterPortrait
            {
                Body = expression?.Body != null ? expression.Body : PortraitBody,
                Eyes = expression?.Eyes != null ? expression.Eyes : PortraitEyes,
                EyesClosed = expression?.EyesClosed != null ? expression.EyesClosed : PortraitEyesClosed,
                Details = expression?.Details != null ? expression.Details : PortraitFaceDetails,
                Mouth = expression?.Mouth != null ? expression.Mouth : PortraitMouth,
                MouthOpen = expression?.MouthOpen != null ? expression.MouthOpen : PortraitMouthOpen
            };
        }
    }
}
