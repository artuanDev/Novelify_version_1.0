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
            context.AddOption<Color>("Fill Color").WithDefaultValue(defaults.FillColor).Build();
            context.AddOption<float>("Opacity").WithDefaultValue(defaults.Opacity).Build();
            context.AddOption<float>("Corner Radius").WithDefaultValue(defaults.CornerRadius).Build();
            context.AddOption<bool>("Outline").WithDefaultValue(defaults.OutlineEnabled).Build();
            context.AddOption<Color>("Outline Color").WithDefaultValue(defaults.OutlineColor).Build();
            context.AddOption<float>("Outline Thickness").WithDefaultValue(defaults.OutlineThickness).Build();
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
    [Node("Novelify/Presentation", null, "Create Dialogue Box")]
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
    [Node("Novelify/Presentation", null, "Create Dialogue Speaker Box")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CreateDialogueSpeakerBoxNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);

            NovelPresentationNodeOptions.AddStyle(
                context, NovelBoxStyle.SpeakerDefault);

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
    [Node("Novelify/Presentation", null, "Change Dialogue Background Style")]
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
    [Node("Novelify/Presentation", null, "Reset Dialogue Style")]
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
    [Node("Novelify/Audio", null, "Play Music")]
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
    [Node("Novelify/Audio", null, "Stop Audio Channel")]
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
        }
    }

    [Serializable]
    [Node("Novelify/Presentation", null, "Create Speech Bubble")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CreateSpeechBubbleNode :
        SpeechBubblePresentationNode { }

    [Serializable]
    [Node("Novelify/Presentation", null, "Change Speech Bubble")]
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
    [Node("Novelify/Transitions", null, "Fade In")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class FadeInNode : FadeAuthoringNode { }

    [Serializable]
    [Node("Novelify/Transitions", null, "Fade Out")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class FadeOutNode : FadeAuthoringNode { }

    //Node for speech bubbles
    [Serializable]
    [Node("Novelify/Story", null, "Speech Bubble")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class SpeechBubbleNode : DialogueNode
    {
        protected override void OnDefineOptions(
            IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<bool>("Thinking")
                .WithDefaultValue(false)
                .WithTooltip("Display this line as a thought bubble with a dotted pointer and no mouth animation.")
                .Build();
        }
    }
}
