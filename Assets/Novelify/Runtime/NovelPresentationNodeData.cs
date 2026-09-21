using System;
using System.Security.Cryptography.X509Certificates;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine;

namespace Novelify
{
    //Which channel of audio the play audio node will affect
    public enum NovelAudioChannel
    {
        Music,
        Ambience,
        SoundEffects,
        Voice,
        UI
    }

    //Screen position for the standard dialogue panel
    public enum NovelDialogueAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        CenterLeft,
        CenterCenter,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    // Controls whether a speech bubble follows its speaker or remains at a
    // deliberate position in the game viewport.
    public enum NovelSpeechBubblePlacement
    {
        FollowSpeaker,
        ScreenAnchor
    }

    //Alignment of the text inside the dialogue box
    public enum NovelTextAlignment
    {
        TopLeft = 0,
        TopCenter = 3,
        TopRight = 2,
        CenterLeft = 4,
        CenterCenter = 1,
        CenterRight = 5,
        BottomLeft = 6,
        BottomCenter = 7,
        BottomRight = 8,


        [Obsolete("Use TopLeft or CenterLeft.")]
        Left = TopLeft,
        [Obsolete("Use TopCenter or CenterCenter.")]
        Center = CenterCenter,
        [Obsolete("Use TopRight or CenterRight.")]
        Right = TopRight
    }

    // Position of the speaker box around the dialogue panel's edges.
    //This is for the box itself, not the text inside
    public enum NovelSpeakerAnchor
    {
        TopLeft,
        TopCenter,
        TopRight,
        LeftTop,
        LeftCenter,
        LeftBottom,
        BottomLeft,
        BottomCenter,
        BottomRight,
        RightBottom,
        RightCenter,
        RightTop
    }

    //Changing the style of a dialogue box using a target so the logic can be reused
    public enum NovelBoxTarget
    {
        Dialogue,
        Speaker,
        Both
    }

    //Types of fade transition in the novel screen
    public enum NovelFadeEasing
    {
        Linear,
        EaseIn,
        EaseOut,
        EaseInOut
    }

    //Struct that will hold the novel boxes information available to edit
    [Serializable]
    public struct NovelBoxStyle
    {
        public Color FillColor;
        public float Opacity;
        public float CornerRadius;
        public bool OutlineEnabled;
        public Color OutlineColor;
        public float OutlineThickness;

        public NovelBoxStyle Validated()
        {
            NovelBoxStyle value = this;
            value.Opacity = Mathf.Clamp01(value.Opacity);
            value.CornerRadius = Mathf.Max(0f, value.CornerRadius);
            value.OutlineThickness = Mathf.Max(0f, value.OutlineThickness);
            return value;
        }

        public Color EffectiveFillColor
        {
            get
            {
                NovelBoxStyle value = Validated();
                Color color = value.FillColor;
                color.a *= value.Opacity;
                return color;
            }
        }

        //Default styles
        public static NovelBoxStyle DialogueDefault = new NovelBoxStyle
        {
            FillColor = new Color(0.055f, 0.075f, 0.13f, 1f),
            Opacity = 0.94f,
            CornerRadius = 24f,
            OutlineEnabled = false,
            OutlineColor = new Color(1f, 1f, 1f, 0.35f),
            OutlineThickness = 2f,
        };

        public static NovelBoxStyle SpeakerDefault => new NovelBoxStyle
        {
            FillColor = new Color(0.12f, 0.42f, 0.88f, 1f),
            Opacity = 1f,
            CornerRadius = 16f,
            OutlineEnabled = false,
            OutlineColor = Color.white,
            OutlineThickness = 2f,
        };

        public static NovelBoxStyle BubbleDefault => new NovelBoxStyle
        {
            FillColor = new Color(0.04f, 0.05f, 0.1f, 0.3f),
            Opacity = 0.2f,
            CornerRadius = 24f,
            OutlineEnabled = false,
            OutlineColor = Color.white,
            OutlineThickness = 2f,
        };
    }

    //Create a dialogue box in runtime
    [Serializable]
    public sealed class RuntimeCreateDialogueBoxNode : RuntimeNode
    {
        public NovelBoxStyle Style = NovelBoxStyle.DialogueDefault;
        public NovelTextAlignment TextAlignment = NovelTextAlignment.TopLeft;
        public NovelDialogueAnchor Anchor = NovelDialogueAnchor.BottomCenter;
        public float BaseFontSize = 30f;
        public bool AutoSize;
        public float MinimumFontSize = 18f;
        public float MaximumFontSize = 30f;
        public float Height = 180f;
        [Tooltip("0 = stretch")]
        public float Width; //0 means stretch
        public float BottomMargin = 32f;
        public float HorizontalMargin = 48f;
        public float HorizontalPadding = 32f;
        public float VerticalPadding = 22f;
    }

    [Serializable]
    public sealed class RuntimeCreateDialogueSpeakerBoxNode : RuntimeNode
    {
        public NovelBoxStyle Style = NovelBoxStyle.SpeakerDefault;
        public NovelSpeakerAnchor Anchor = NovelSpeakerAnchor.TopLeft;
        public float FontSize = 25f;
        public float HorizontalPadding = 16f;
        public float VerticalPadding = 6f;
        public float HorizontalOffset = 24f;
        public float VerticalOverlap = 27f;
    }

    [Serializable]
    public sealed class RuntimeChangeDialogueStyleNode : RuntimeNode
    {
        public NovelBoxTarget Target = NovelBoxTarget.Both;
        public NovelBoxStyle Style = NovelBoxStyle.DialogueDefault;
    }

    [Serializable]
    public sealed class RuntimeResetDialogueStyleNode: RuntimeNode
    {
        public NovelBoxTarget Target = NovelBoxTarget.Both;
    }

    [Serializable]
    public sealed class RuntimePlayMusicNode : RuntimeNode
    {
        public NovelAudioChannel Channel = NovelAudioChannel.Music;
        public AudioClip Clip;
        [SerializeReference] public RuntimeValueExpression ClipValue;
        public float Volume = 1f;
        [SerializeReference] public RuntimeValueExpression VolumeValue;
        public float Pitch = 1f;
        [SerializeReference] public RuntimeValueExpression PitchValue;
        public bool Loop;
        public bool ReplaceCurrent = true;
        public int Priority = 128;
    }

    [Serializable]
    public sealed class RuntimeStopAudioChannelNode: RuntimeNode
    {
        public NovelAudioChannel Channel = NovelAudioChannel.Music;
    }

    [Serializable]
    public abstract class RuntimeSpeechBubblePresentationNode : RuntimeNode
    {
        public NovelBoxStyle BubbleStyle = NovelBoxStyle.BubbleDefault;
        public NovelSpeechBubblePlacement Placement =
            NovelSpeechBubblePlacement.FollowSpeaker;
        public NovelDialogueAnchor ScreenAnchor =
            NovelDialogueAnchor.TopCenter;
        public float HorizontalOffset;
        public float VerticalOffset;
        public bool KeepInsideViewport = true;
        public bool AutoSize = true;
        public float MinimumWidth = 180f;
        public float MaximumWidth = 520f;
        public float MinimumHeight = 88f;
        public float MaximumHeight = 320f;
        public float FixedWidth = 360f;
        public float FixedHeight = 160f;
        public NovelTextAlignment TextAlignment =
            NovelTextAlignment.TopLeft;
        public float HorizontalPadding = 24f;
        public float VerticalPadding = 18f;
        public float DialogueFontSize = 24f;
        public float SpeakerFontSize = 21f;
        public bool ShowSpeakerName;
        public bool ShowTail = true;
        [Tooltip("Character-local target: (0,0) is bottom-left and (1,1) is top-right.")]
        public Vector2 TailTarget = new Vector2(0.5f, 0.5f);
        public float TailWidth = 34f;
        public float TailLength = 30f;
        public float TargetMargin = 18f;
    }

    [Serializable]
    public sealed class RuntimeCreateSpeechBubbleNode :
        RuntimeSpeechBubblePresentationNode { }

    [Serializable]
    public sealed class RuntimeChangeSpeechBubbleNode :
        RuntimeSpeechBubblePresentationNode { }

    /*backbone class for any fade. since we want to reuse values from both fade in and out we make this class
     * abstract and the fade in and out both inherit from this
    //*/
    [Serializable]
    public abstract class RuntimeFadeNode : RuntimeNode
    {
        public float Duration = 1f;
        [SerializeReference] public RuntimeValueExpression DurationValue;
        public float Speed = 1f;
        [SerializeReference] public RuntimeValueExpression SpeedValue;
        public Color Color = Color.black;
        public NovelFadeEasing Easing = NovelFadeEasing.EaseInOut;
        public bool WaitForCompletion = true;
        public bool BlockInput = true;
    }

    [Serializable]
    public sealed class RuntimeFadeInNode : RuntimeFadeNode { }

    [Serializable]
    public sealed class RuntimeFadeOutNode : RuntimeFadeNode { }

    [Serializable]
    public sealed class RuntimeSpeechBubbleNode : RuntimeDialogueNode
    {
        public bool Thinking;
    }
}
