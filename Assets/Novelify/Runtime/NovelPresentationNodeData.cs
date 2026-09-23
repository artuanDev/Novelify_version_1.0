using System;
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

    /// <summary>Controls what happens to an already visible bubble when a new one is shown.</summary>
    public enum NovelSpeechBubbleOverlapMode
    {
        ReplacePrevious,
        KeepPrevious
    }

    public enum NovelBackgroundScaleMode
    {
        Cover,
        Contain,
        Stretch
    }

    /// <summary>
    /// Configures the generated choice surface. The style asset owns appearance;
    /// this node owns placement, grouping, and arrangement.
    /// </summary>
    [Serializable]
    public sealed class RuntimeCreateChoiceLayoutNode : RuntimeNode
    {
        public NovelChoiceStyle Style;
        public NovelDialogueAnchor Anchor = NovelDialogueAnchor.CenterCenter;
        public Vector2 Offset;
        public Vector2 PanelSize = new Vector2(1440f, 720f);
        public NovelChoiceArrangement Arrangement =
            NovelChoiceArrangement.Vertical;
        [Tooltip("0 keeps every choice in one group. Vertical groups become columns, horizontal groups become rows, and circular groups become rings.")]
        public int ChoicesPerGroup;
        public float ChoiceSpacing = 14f;
        public float GroupSpacing = 24f;
        public float CircleRadius = 210f;
        public float CircleStartAngle = 90f;
        public float CircleArc = 360f;
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
        public Texture2D FillTexture;
        public Vector2 FillTiling;
        public Vector2 FillOffset;
        public float CornerRadius;
        public bool OutlineEnabled;
        public Color OutlineColor;
        public float OutlineTransparency;
        public float OutlineThickness;
        public Texture2D OutlineTexture;
        public Vector2 OutlineTiling;
        public Vector2 OutlineOffset;

        public NovelBoxStyle Validated()
        {
            NovelBoxStyle value = this;
            value.Opacity = Mathf.Clamp01(value.Opacity);
            value.FillTiling = ValidateTiling(value.FillTiling);
            value.CornerRadius = Mathf.Max(0f, value.CornerRadius);
            value.OutlineTransparency = Mathf.Clamp01(
                value.OutlineTransparency);
            value.OutlineThickness = Mathf.Max(0f, value.OutlineThickness);
            value.OutlineTiling = ValidateTiling(value.OutlineTiling);
            return value;
        }

        private static Vector2 ValidateTiling(Vector2 value)
        {
            if (Mathf.Abs(value.x) < 0.001f)
                value.x = 1f;
            if (Mathf.Abs(value.y) < 0.001f)
                value.y = 1f;
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

        public Color EffectiveOutlineColor
        {
            get
            {
                NovelBoxStyle value = Validated();
                Color color = value.OutlineColor;
                color.a *= 1f - value.OutlineTransparency;
                return color;
            }
        }

        //Default styles
        public static NovelBoxStyle DialogueDefault = new NovelBoxStyle
        {
            FillColor = new Color(0.055f, 0.075f, 0.13f, 1f),
            Opacity = 0.94f,
            FillTiling = Vector2.one,
            CornerRadius = 24f,
            OutlineEnabled = false,
            OutlineColor = new Color(1f, 1f, 1f, 0.35f),
            OutlineTransparency = 0f,
            OutlineThickness = 2f,
            OutlineTiling = Vector2.one,
        };

        public static NovelBoxStyle SpeakerDefault => new NovelBoxStyle
        {
            FillColor = new Color(0.12f, 0.42f, 0.88f, 1f),
            Opacity = 1f,
            FillTiling = Vector2.one,
            CornerRadius = 16f,
            OutlineEnabled = false,
            OutlineColor = Color.white,
            OutlineTransparency = 0f,
            OutlineThickness = 2f,
            OutlineTiling = Vector2.one,
        };

        public static NovelBoxStyle BubbleDefault => new NovelBoxStyle
        {
            FillColor = new Color(0.04f, 0.05f, 0.1f, 0.3f),
            Opacity = 0.2f,
            FillTiling = Vector2.one,
            CornerRadius = 24f,
            OutlineEnabled = false,
            OutlineColor = Color.white,
            OutlineTransparency = 0f,
            OutlineThickness = 2f,
            OutlineTiling = Vector2.one,
        };
    }

    //Create a dialogue box in runtime
    [Serializable]
    public sealed class RuntimeCreateDialogueBoxNode : RuntimeNode
    {
        public NovelPresentationStyle StyleAsset;
        public NovelBoxStyle Style = NovelBoxStyle.DialogueDefault;
        public NovelBoxStyle ResolvedStyle => StyleAsset != null
            ? StyleAsset.DialogueBoxStyle
            : Style.Validated();
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
        public NovelPresentationStyle StyleAsset;
        public NovelBoxStyle Style = NovelBoxStyle.SpeakerDefault;
        public NovelBoxStyle ResolvedStyle => StyleAsset != null
            ? StyleAsset.SpeakerBoxStyle
            : Style.Validated();
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
        public NovelPresentationStyle StyleAsset;
        public NovelBoxTarget Target = NovelBoxTarget.Both;
        public NovelBoxStyle Style = NovelBoxStyle.DialogueDefault;

        public NovelBoxStyle Resolve(NovelBoxTarget target)
        {
            if (StyleAsset == null)
                return Style.Validated();
            return target == NovelBoxTarget.Speaker
                ? StyleAsset.SpeakerBoxStyle
                : StyleAsset.DialogueBoxStyle;
        }
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
    public sealed class RuntimeSetBackgroundNode : RuntimeNode
    {
        public Sprite Background;
        [SerializeReference] public RuntimeValueExpression BackgroundValue;
        public Color Tint = Color.white;
        public NovelBackgroundScaleMode ScaleMode =
            NovelBackgroundScaleMode.Cover;
        public float TransitionDuration = 0.35f;
        [SerializeReference] public RuntimeValueExpression TransitionDurationValue;
        public bool WaitForCompletion;
    }

    [Serializable]
    public abstract class RuntimeSpeechBubblePresentationNode : RuntimeNode
    {
        public NovelPresentationStyle StyleAsset;
        public NovelBoxStyle BubbleStyle = NovelBoxStyle.BubbleDefault;
        public NovelBoxStyle ResolvedStyle => StyleAsset != null
            ? StyleAsset.SpeechBubbleStyle
            : BubbleStyle.Validated();
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
        public NovelSpeechBubbleOverlapMode OverlapMode =
            NovelSpeechBubbleOverlapMode.ReplacePrevious;
        [Tooltip("Negative values wait for player input. Zero or greater advances automatically after that many seconds.")]
        public float AutoAdvanceDelay = -1f;
    }
}
