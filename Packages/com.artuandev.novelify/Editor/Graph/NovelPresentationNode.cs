using Novelify;
using Novelify.Editor;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using static Unity.GraphToolkit.Editor.Node;
using static UnityEngine.Audio.IAudioGenerator;

namespace Novelify.Editor
{
    internal static class NovelPresentationNodeOptions
    {
        public static void AddStyle(
            IOptionDefinitionContext context,
            NovelBoxStyle defaults)
        {
            context.AddOption<NovelPresentationStyle>("Style Asset")
                .WithTooltip("Optional reusable style asset. Its matching box style overrides the inline values below.")
                .Build();
            context.AddOption<Color>("Fill Color").WithDefaultValue(defaults.FillColor).Build();
            context.AddOption<float>("Opacity").WithDefaultValue(defaults.Opacity).Build();
            context.AddOption<Texture2D>("Fill Texture")
                .WithDefaultValue(defaults.FillTexture).Build();
            context.AddOption<Vector2>("Fill Tiling")
                .WithDefaultValue(defaults.FillTiling).Build();
            context.AddOption<Vector2>("Fill Offset")
                .WithDefaultValue(defaults.FillOffset).Build();
            context.AddOption<float>("Corner Radius").WithDefaultValue(defaults.CornerRadius).Build();
            context.AddOption<bool>("Outline").WithDefaultValue(defaults.OutlineEnabled).Build();
            context.AddOption<Color>("Outline Color").WithDefaultValue(defaults.OutlineColor).Build();
            context.AddOption<float>("Outline Transparency")
                .WithDefaultValue(defaults.OutlineTransparency).Build();
            context.AddOption<float>("Outline Thickness").WithDefaultValue(defaults.OutlineThickness).Build();
            context.AddOption<Texture2D>("Outline Texture")
                .WithDefaultValue(defaults.OutlineTexture).Build();
            context.AddOption<Vector2>("Outline Tiling")
                .WithDefaultValue(defaults.OutlineTiling).Build();
            context.AddOption<Vector2>("Outline Offset")
                .WithDefaultValue(defaults.OutlineOffset).Build();
        }
        public static void AddTextSizing(
            IOptionDefinitionContext context,
            float baseSize,
            float minimumSize,
            float maximumSize)
        {
            context.AddOption<float>("Base Font Size")
                .WithDefaultValue(baseSize).Build();
            context.AddOption<bool>("Auto Size")
                .WithDefaultValue(false).Build();
            context.AddOption<float>("Minimum Font Size")
                .WithDefaultValue(minimumSize).Build();
            context.AddOption<float>("Maximum Font Size")
                .WithDefaultValue(maximumSize).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Create Dialogue Box")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CreateDialogueBoxNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            NovelPresentationNodeOptions.AddStyle(
                context, NovelBoxStyle.DialogueDefault);

            context.AddOption<NovelTextAlignment>("Text Alignment")
                .WithDefaultValue(NovelTextAlignment.TopLeft).Build();

            context.AddOption<NovelDialogueAnchor>("Anchor")
                .WithDefaultValue(NovelDialogueAnchor.BottomCenter).Build();

            NovelPresentationNodeOptions.AddTextSizing(
                context, 30f, 18f, 30f);

            context.AddOption<float>("Height").WithDefaultValue(180f).Build();
            context.AddOption<float>("Width")
                .WithDefaultValue(0f)
                .WithTooltip("Set to 0 to stretch between the horizontal margins. " +
                             "A positive value creates a fixed-width panel.")
                .Build();
            context.AddOption<float>("Bottom Margin").WithDefaultValue(32f).Build();
            context.AddOption<float>("Horizontal Margin").WithDefaultValue(48f).Build();
            context.AddOption<float>("Horizontal Padding").WithDefaultValue(32f).Build();
            context.AddOption<float>("Vertical Padding").WithDefaultValue(22f).Build();
        }
    }
    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Create Dialogue Speaker Box")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CreateDialogueSpeakerBoxNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            NovelPresentationNodeOptions.AddStyle(
                context, NovelBoxStyle.SpeakerDefault);

            context.AddOption<NovelSpeakerAnchor>("Anchor")
                .WithDefaultValue(NovelSpeakerAnchor.TopLeft)
                .WithTooltip("Edge of the dialogue box used to attach the speaker-name box.")
                .Build();

            context.AddOption<float>("Font Size")
                .WithDefaultValue(25f).Build();

            context.AddOption<float>("Horizontal Padding")
                .WithDefaultValue(16f).Build();

            context.AddOption<float>("Vertical Padding")
                .WithDefaultValue(6f).Build();

            context.AddOption<float>("Horizontal Offset")
                .WithDefaultValue(24f).Build();

            context.AddOption<float>("Vertical Overlap")
                .WithDefaultValue(27f).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Change Dialogue Background Style")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class ChangeDialogueBackgroundStyleNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<NovelBoxTarget>("Target")
                .WithDefaultValue(NovelBoxTarget.Both).Build();
            NovelPresentationNodeOptions.AddStyle(
                context, NovelBoxStyle.DialogueDefault);
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Reset Dialogue Style")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class ResetDialogueStyleNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<NovelBoxTarget>("Target")
                .WithDefaultValue(NovelBoxTarget.Both).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Audio, null, "Play Music")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class PlayMusicNode : ActionNode
    {
        public const string ClipPort = "Clip";
        public const string VolumePort = "Volume";
        public const string PitchPort = "Pitch";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<AudioClip>(ClipPort).Build();
            context.AddInputPort<float>(VolumePort).WithDefaultValue(1f).Build();
            context.AddInputPort<float>(PitchPort).WithDefaultValue(1f).Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<NovelAudioChannel>("Channel").WithDefaultValue(NovelAudioChannel.Music).Build();
            context.AddOption<bool>("Loop").WithDefaultValue(true).Build();
            context.AddOption<bool>("Replace Current").WithDefaultValue(true).Build();
            context.AddOption<int>("Priority").WithDefaultValue(128).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Audio, null, "Stop Audio Channel")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class StopAudioChannelNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<NovelAudioChannel>("Channel")
                .WithDefaultValue(NovelAudioChannel.Music).Build();
        }
    }

    [Serializable]
    public abstract class SpeechBubblePresentationNode : ActionNode
    {
        protected override void OnDefinePorts(
            IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<NovelCharacter>("Preview Character")
                .WithTooltip("Character used to preview this bubble box at its current authored screen position.")
                .Build();
            context.AddInputPort<NovelCharacterReference>("Preview Character Reference")
                .WithTooltip("Exact character instance used by the bubble-box preview.")
                .Build();
        }

        protected override void OnDefineOptions(
            IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            NovelPresentationNodeOptions.AddStyle(
                context, NovelBoxStyle.BubbleDefault);
            context.AddOption<NovelSpeechBubblePlacement>("Placement")
                .WithDefaultValue(NovelSpeechBubblePlacement.FollowSpeaker)
                .WithTooltip("Follow the speaking character automatically, or keep the bubble at a chosen screen anchor.")
                .Build();
            context.AddOption<NovelDialogueAnchor>("Screen Anchor")
                .WithDefaultValue(NovelDialogueAnchor.TopCenter).Build();
            context.AddOption<float>("Horizontal Offset")
                .WithDefaultValue(0f).Build();
            context.AddOption<float>("Vertical Offset")
                .WithDefaultValue(0f).Build();
            context.AddOption<bool>("Keep Inside Viewport")
                .WithDefaultValue(true).Build();
            context.AddOption<bool>("Auto Size")
                .WithDefaultValue(true)
                .WithTooltip("Fit the bubble to its text within the minimum and maximum size limits.")
                .Build();
            context.AddOption<float>("Minimum Width")
                .WithDefaultValue(180f).Build();
            context.AddOption<float>("Maximum Width")
                .WithDefaultValue(520f).Build();
            context.AddOption<float>("Minimum Height")
                .WithDefaultValue(88f).Build();
            context.AddOption<float>("Maximum Height")
                .WithDefaultValue(320f).Build();
            context.AddOption<float>("Fixed Width")
                .WithDefaultValue(360f).Build();
            context.AddOption<float>("Fixed Height")
                .WithDefaultValue(160f).Build();
            context.AddOption<NovelTextAlignment>("Text Alignment")
                .WithDefaultValue(NovelTextAlignment.TopLeft)
                .WithTooltip("Alignment inside a fixed-size bubble. Auto-sized bubbles always center their dialogue text.")
                .Build();
            context.AddOption<float>("Horizontal Padding")
                .WithDefaultValue(24f).Build();
            context.AddOption<float>("Vertical Padding")
                .WithDefaultValue(18f).Build();
            context.AddOption<float>("Dialogue Font Size")
                .WithDefaultValue(24f).Build();
            context.AddOption<float>("Speaker Font Size")
                .WithDefaultValue(21f).Build();
            context.AddOption<bool>("Show Speaker Name")
                .WithDefaultValue(false)
                .WithTooltip("Show the speaking character's name inside the bubble.")
                .Build();
            context.AddOption<bool>("Show Tail")
                .WithDefaultValue(true).Build();
            context.AddOption<Vector2>("Tail Target")
                .WithDefaultValue(new Vector2(0.5f, 0.5f))
                .WithTooltip("Point on the speaking character that the tail aims at. (0,0) is bottom-left and (1,1) is top-right; values outside that range are allowed.")
                .Build();
            context.AddOption<float>("Tail Width")
                .WithDefaultValue(34f).Build();
            context.AddOption<float>("Tail Length")
                .WithDefaultValue(30f).Build();
            context.AddOption<float>("Target Margin")
                .WithDefaultValue(18f).Build();
            context.AddOption<string>("Preview Instance ID")
                .WithDefaultValue(string.Empty)
                .WithTooltip("Instance used for preview when Preview Character Reference is not connected.")
                .Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Create Speech Bubble Box")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CreateSpeechBubbleNode :
        SpeechBubblePresentationNode { }

    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Change Speech Bubble Box")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class ChangeSpeechBubbleNode :
        SpeechBubblePresentationNode { }

    //Non instantiated node because we need these ports to be shared for fade in and out.
    [Serializable]
    public abstract class FadeAuthoringNode : ActionNode
    {
        public const string DurationPort = "Duration";
        public const string SpeedPort = "Speed";

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<float>(DurationPort)
                .WithDefaultValue(1f).Build();
            context.AddInputPort<float>(SpeedPort)
                .WithDefaultValue(1f).Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<Color>("Color").WithDefaultValue(Color.black).Build();
            context.AddOption<NovelFadeEasing>("Easing")
                .WithDefaultValue(NovelFadeEasing.EaseInOut).Build();
            context.AddOption<bool>("Wait For Completion")
                .WithDefaultValue(true).Build();
            context.AddOption<bool>("Block Input")
                .WithDefaultValue(true).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.PresentationTransitions, null, "Fade In")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class FadeInNode : FadeAuthoringNode { }

    [Serializable]
    [Node(NovelNodeCategories.PresentationTransitions, null, "Fade Out")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class FadeOutNode : FadeAuthoringNode { }

    //Node for speech bubbles
    [Serializable]
    [Node(NovelNodeCategories.Story, null, "Speech Bubble")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class SpeechBubbleNode : DialogueNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<NovelCharacter>("Character")
                .WithTooltip("Character this bubble belongs to. Its current transformed position is used for placement and preview.")
                .Build();
            context.AddInputPort<NovelCharacterReference>("Character Reference")
                .WithTooltip("Exact character instance this bubble follows. Prefer this when using multiple copies of one character.")
                .Build();
        }

        protected override void OnDefineOptions(
            IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<bool>("Thinking")
                .WithDefaultValue(false)
                .WithTooltip("Display this line as a thought bubble with a dotted pointer and no mouth animation.")
                .Build();
            context.AddOption<NovelSpeechBubbleOverlapMode>("When Another Bubble Is Visible")
                .WithDefaultValue(NovelSpeechBubbleOverlapMode.ReplacePrevious)
                .WithTooltip("Keep Previous places this line beside the previous bubble for interruptions and overlapping reactions.")
                .Build();
            context.AddOption<bool>("Continue Automatically")
                .WithDefaultValue(false)
                .WithTooltip("Advance without a click so the following bubble can appear almost immediately.")
                .Build();
            context.AddOption<float>("Auto Continue Delay")
                .WithDefaultValue(0.15f)
                .WithTooltip("Seconds after this bubble appears before continuing. Small values work well for interruptions.")
                .Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Create Choice Layout")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CreateChoiceLayoutNode : ActionNode
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Generate and arrange choices",
                "Double-click to preview placement, grouping, and style.",
                new Color32(139, 148, 255, 255));
        }

        protected override void OnDefineOptions(
            IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<NovelChoiceStyle>("Style Asset")
                .WithTooltip("Reusable visual and text style for every generated choice button. No prefab is required.")
                .Build();
            context.AddOption<NovelDialogueAnchor>("Screen Anchor")
                .WithDefaultValue(NovelDialogueAnchor.CenterCenter)
                .WithTooltip("Point on the screen where the choice panel is attached.")
                .Build();
            context.AddOption<Vector2>("Offset")
                .WithDefaultValue(Vector2.zero)
                .WithTooltip("Canvas-unit offset from the selected screen anchor.")
                .Build();
            context.AddOption<Vector2>("Panel Size")
                .WithDefaultValue(new Vector2(1440f, 720f))
                .Build();
            context.AddOption<NovelChoiceArrangement>("Arrangement")
                .WithDefaultValue(NovelChoiceArrangement.Vertical)
                .Build();
            context.AddOption<int>("Choices Per Group")
                .WithDefaultValue(0)
                .WithTooltip("0 keeps all choices together. Vertical groups form columns, horizontal groups form rows, and circular groups form rings.")
                .Build();
            context.AddOption<float>("Choice Spacing")
                .WithDefaultValue(14f)
                .Build();
            context.AddOption<float>("Group Spacing")
                .WithDefaultValue(24f)
                .Build();
            context.AddOption<float>("Circle Radius")
                .WithDefaultValue(210f)
                .Build();
            context.AddOption<float>("Circle Start Angle")
                .WithDefaultValue(90f)
                .WithTooltip("Degrees. 0 starts on the right and 90 starts at the top.")
                .Build();
            context.AddOption<float>("Circle Arc")
                .WithDefaultValue(360f)
                .WithTooltip("Degrees occupied by each ring. Negative values reverse direction.")
                .Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Presentation, null, "Set Background")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class SetBackgroundNode : ActionNode
    {
        public const string BackgroundPort = "Background Image";
        public const string DurationPort = "Transition Duration";

        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Set scene background",
                "Creates or changes the responsive background. Leave the image empty to clear it.",
                new Color32(96, 165, 250, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<Sprite>(BackgroundPort)
                .WithTooltip("Background sprite. Leave empty to fade the current background out.")
                .Build();
            context.AddInputPort<float>(DurationPort)
                .WithDefaultValue(0.35f)
                .WithTooltip("Cross-fade time in dialogue-clock seconds. Zero changes instantly.")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<Color>("Tint")
                .WithDefaultValue(Color.white)
                .Build();
            context.AddOption<NovelBackgroundScaleMode>("Scale Mode")
                .WithDefaultValue(NovelBackgroundScaleMode.Cover)
                .WithTooltip("Cover fills every aspect ratio, Contain shows the whole image, and Stretch fills without preserving aspect.")
                .Build();
            context.AddOption<bool>("Wait For Completion")
                .WithDefaultValue(false)
                .WithTooltip("Pause graph flow until the cross-fade completes.")
                .Build();
        }
    }
}
