using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    [Serializable, Node("Novelify/Flow", "d_UnityEditor.Graphs.AnimatorControllerTool", "Call Novel Page"), UseWithGraph(typeof(NovelGraph))]
    public class CallNovelPageNode : ActionNode, ISubgraphNode
    {
        public const string GraphPortName = "Novel Graph";

        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(this, "Reusable story beat",
                "Runs another Novel Graph, then continues here when it finishes. Double-click to open it.",
                new Color32(129, 140, 248, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<RuntimeNovelGraph>(GraphPortName)
                .WithTooltip("Novel Graph to run. Its End returns through Continue.")
                .Build();
        }

        public Graph GetSubgraph()
        {
            RuntimeNovelGraph runtimeGraph = NovelGraphValues.Resolve<RuntimeNovelGraph>(
                (NovelGraph)Graph, GetInputPortByName(GraphPortName));
            string path = AssetDatabase.GetAssetPath(runtimeGraph);
            return string.IsNullOrEmpty(path) ? null : GraphDatabase.LoadGraph<NovelGraph>(path);
        }
    }

    [Serializable]
    public abstract class ActionNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in").WithDisplayName("Enter").Build();
            context.AddOutputPort("out").WithCapacity(PortCapacity.Single).WithDisplayName("Continue").Build();
        }
    }

    [Serializable]
    public abstract class CharacterActionNode : ActionNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<NovelCharacter>("Character")
                .WithTooltip("Character asset to act on. Accepts a character variable or Current Speaker output.").Build();
            context.AddOutputPort<NovelCharacter>("Character")
                .WithTooltip("Pass this character to another character node or a dialogue Speaker input.").Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) => DefineInstanceOption(context);

        internal static void DefineInstanceOption(IOptionDefinitionContext context)
        {
            context.AddOption<string>("Instance ID").WithDefaultValue(string.Empty)
                .WithTooltip("Empty uses the default instance. Use the same unique ID across nodes to target an additional copy of this character.").Build();
        }
    }

    [Serializable, Node("Novelify/Flow"), UseWithGraph(typeof(NovelGraph))]
    public class LabelNode : ActionNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<string>("Label")
                .WithDefaultValue(string.Empty)
                .WithTooltip("Unique destination name used by Jump nodes.")
                .Build();
        }
    }

    [Serializable, Node("Novelify/Flow"), UseWithGraph(typeof(NovelGraph))]
    public class JumpNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in").WithDisplayName("Enter").Build();
            context.AddInputPort<string>("Label")
                .WithDefaultValue(string.Empty)
                .WithTooltip("Name of the Label node where story flow should continue.")
                .Build();
        }
    }

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph))]
    public class ShowCharacterNode : CharacterActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<Vector2>("Position").WithTooltip("Position in canvas units relative to the portrait's anchors.").Build();
            context.AddOption<CharacterEmotion>("Emotion").WithDefaultValue(CharacterEmotion.Neutral).Build();
        }
    }

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph))]
    public class HideCharacterNode : CharacterActionNode { }

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph))]
    public class HideAllCharactersNode : ActionNode { }

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph))]
    public class SetCharacterEmotionNode : CharacterActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<CharacterEmotion>("Emotion").WithDefaultValue(CharacterEmotion.Neutral).Build();
        }
    }

    [Serializable, Node("Novelify/Flow"), UseWithGraph(typeof(NovelGraph))]
    public class WaitNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<float>("Duration").WithDefaultValue(1f)
                .WithTooltip("Wait this many real-time seconds before continuing. Clicks do not skip the wait.").Build();
        }
    }

    [Serializable, Node("Novelify/Utilities"), UseWithGraph(typeof(NovelGraph))]
    public class DialogueEventNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("Event Name").WithDefaultValue(string.Empty)
                .WithTooltip("Sent to NovelManager's On Dialogue Event listeners, then flow continues.").Build();
        }
    }

    [Serializable, Node("Novelify/Utilities"), UseWithGraph(typeof(NovelGraph))]
    public class StopSoundNode : ActionNode { }
}
