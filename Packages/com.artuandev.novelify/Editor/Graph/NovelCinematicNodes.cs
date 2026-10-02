using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Novelify.Editor
{
    [Serializable]
    [Node(NovelNodeCategories.Story, null, "Narration")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class NarrationNode : SimpleDialogueNode
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(this, "Narrator",
                "Speakerless line, with optional timed continuation.",
                new Color32(167, 139, 250, 255));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<bool>("Continue Automatically").WithDefaultValue(false).Build();
            context.AddOption<float>("Auto Continue Delay").WithDefaultValue(1f).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.PresentationTransitions, null, "Screen Flash")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class ScreenFlashNode : ActionNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<float>("Duration").WithDefaultValue(0.4f).Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<Color>("Color").WithDefaultValue(Color.white).Build();
            context.AddOption<float>("Attack Fraction").WithDefaultValue(0.15f).Build();
            context.AddOption<float>("Hold Fraction").WithDefaultValue(0.1f).Build();
            context.AddOption<bool>("Wait For Completion").WithDefaultValue(true).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.PresentationTransitions, null, "Screen Shake")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class ScreenShakeNode : ActionNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<float>("Duration").WithDefaultValue(0.35f).Build();
            context.AddInputPort<float>("Amplitude").WithDefaultValue(18f)
                .WithTooltip("Maximum screen displacement in pixels. Shakes the camera, backgrounds, portraits, and dialogue together, including Screen Space UI.").Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<float>("Frequency").WithDefaultValue(25f).Build();
            context.AddOption<bool>("Wait For Completion").WithDefaultValue(true).Build();
        }
    }

    /// <summary>Built-in effects intentionally use the same public compiler path as third-party nodes.</summary>
    public sealed class NovelCinematicNodeCompiler : INovelFlowNodeCompiler
    {
        public bool CanCompile(INode node) => node is ScreenFlashNode || node is ScreenShakeNode;

        public RuntimeNode Compile(INode node, NovelNodeCompileContext context)
        {
            if (node is ScreenFlashNode)
                return new RuntimeScreenFlashNode
                {
                    Duration = Mathf.Max(0f, context.PortValue<float>(node, "Duration")),
                    DurationValue = context.Expression(node, "Duration"),
                    Color = context.OptionValue(node, "Color", Color.white),
                    AttackFraction = Mathf.Clamp01(context.OptionValue(node, "Attack Fraction", 0.15f)),
                    HoldFraction = Mathf.Clamp01(context.OptionValue(node, "Hold Fraction", 0.1f)),
                    WaitForCompletion = context.OptionValue(node, "Wait For Completion", true)
                };

            return new RuntimeScreenShakeNode
            {
                Duration = Mathf.Max(0f, context.PortValue<float>(node, "Duration")),
                DurationValue = context.Expression(node, "Duration"),
                Amplitude = Mathf.Max(0f, context.PortValue<float>(node, "Amplitude")),
                AmplitudeValue = context.Expression(node, "Amplitude"),
                Frequency = Mathf.Max(0f, context.OptionValue(node, "Frequency", 25f)),
                WaitForCompletion = context.OptionValue(node, "Wait For Completion", true)
            };
        }
    }
}
