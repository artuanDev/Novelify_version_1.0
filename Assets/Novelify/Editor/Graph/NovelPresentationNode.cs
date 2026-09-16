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
    }

    [Serializable]
    [Node("Novelify/Presentation", null, "Create Dialogue Box")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CreateDialogueBoxNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            NovelPresentationNodeOptions.AddStyle(context, NovelBoxStyle.DialogueDefault);

            context.AddOption<float>("Height").WithDefaultValue(180f).Build();
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
            context.AddOption<float>("Width").WithDefaultValue(260f).Build();
            context.AddOption<float>("Height").WithDefaultValue(54f).Build();
            context.AddOption<float>("Horizontal Offset").WithDefaultValue(24f).Build();
            context.AddOption<float>("Vertical Overlap").WithDefaultValue(27f).Build();
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
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            NovelPresentationNodeOptions.AddStyle(
                context, NovelBoxStyle.BubbleDefault);
            context.AddOption<float>("Minimum Width")
                .WithDefaultValue(180f).Build();
            context.AddOption<float>("Maximum Width")
                .WithDefaultValue(520f).Build();
            context.AddOption<float>("Horizontal Padding")
                .WithDefaultValue(24f).Build();
            context.AddOption<float>("Vertical Padding")
                .WithDefaultValue(18f).Build();
            context.AddOption<float>("Tail Width")
                .WithDefaultValue(34f).Build();
            context.AddOption<float>("Tail Length")
                .WithDefaultValue(30f).Build();
            context.AddOption<float>("Target Margin")
                .WithDefaultValue(18f).Build();
        }
    }
}