using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Novelify.Editor
{
    [Serializable]
    public abstract class CharacterAnimationNode : CharacterActionNode
    {
        public abstract NovelCharacterAnimation Animation { get; }
        protected virtual float DefaultAmplitude => 25f;
        protected virtual float DefaultFrequency => 2f;

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<float>("Amplitude").WithDefaultValue(DefaultAmplitude)
                .WithTooltip(Animation == NovelCharacterAnimation.Sway ? "Maximum lean in degrees." : "Maximum visual offset in canvas units.").Build();
            context.AddInputPort<float>("Frequency").WithDefaultValue(DefaultFrequency)
                .WithTooltip("Animation cycles per second.").Build();
            context.AddInputPort<float>("Duration").WithDefaultValue(0f)
                .WithTooltip("Zero loops until Stop Character Animation. A positive duration stops automatically.").Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<bool>("Wait For Completion").WithDefaultValue(false)
                .WithTooltip("Wait only when Duration is positive. Continuous effects always continue immediately.").Build();
        }
    }

    [Serializable, Node(NovelNodeCategories.Characters, null, "Bounce Character"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class BounceCharacterNode : CharacterAnimationNode
    {
        public override NovelCharacterAnimation Animation => NovelCharacterAnimation.Bounce;
    }

    [Serializable, Node(NovelNodeCategories.Characters, null, "Shake Character"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class ShakeCharacterNode : CharacterAnimationNode
    {
        public override NovelCharacterAnimation Animation => NovelCharacterAnimation.Shake;
        protected override float DefaultAmplitude => 12f;
        protected override float DefaultFrequency => 20f;
    }

    [Serializable, Node(NovelNodeCategories.Characters, null, "Sway Character"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class SwayCharacterNode : CharacterAnimationNode
    {
        public override NovelCharacterAnimation Animation => NovelCharacterAnimation.Sway;
        protected override float DefaultAmplitude => 8f;
        protected override float DefaultFrequency => 1f;
    }

    [Serializable, Node(NovelNodeCategories.Characters, null, "Stop Character Animation"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class StopCharacterAnimationNode : CharacterActionNode { }

    public sealed class NovelCharacterAnimationNodeCompiler : INovelFlowNodeCompiler
    {
        public bool CanCompile(INode node) => node is CharacterAnimationNode || node is StopCharacterAnimationNode;

        public RuntimeNode Compile(INode node, NovelNodeCompileContext context)
        {
            if (node is CharacterAnimationNode animation)
                return new RuntimeAnimateCharacterNode
                {
                    Character = context.PortValue<NovelCharacter>(node, "Character"),
                    InstanceID = context.OptionValue(node, "Instance ID", string.Empty),
                    CharacterValue = context.Expression(node, "Character"),
                    CharacterReferenceValue = context.Expression(node, "Character Reference"),
                    Animation = animation.Animation,
                    Amplitude = Mathf.Max(0f, context.PortValue<float>(node, "Amplitude")),
                    Frequency = Mathf.Max(0f, context.PortValue<float>(node, "Frequency")),
                    Duration = Mathf.Max(0f, context.PortValue<float>(node, "Duration")),
                    AmplitudeValue = context.Expression(node, "Amplitude"),
                    FrequencyValue = context.Expression(node, "Frequency"),
                    DurationValue = context.Expression(node, "Duration"),
                    WaitForCompletion = context.OptionValue(node, "Wait For Completion", false)
                };
            return new RuntimeStopCharacterAnimationNode
            {
                Character = context.PortValue<NovelCharacter>(node, "Character"),
                InstanceID = context.OptionValue(node, "Instance ID", string.Empty),
                CharacterValue = context.Expression(node, "Character"),
                CharacterReferenceValue = context.Expression(node, "Character Reference")
            };
        }
    }
}
