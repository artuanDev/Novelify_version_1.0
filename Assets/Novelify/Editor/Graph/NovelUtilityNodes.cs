using System;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    [Serializable, Node("Novelify/Flow", "d_UnityEditor.Graphs.AnimatorControllerTool", "Call Novel Page"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
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
                Graph, GetInputPortByName(GraphPortName));
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
                .WithTooltip("Legacy target asset. Use Character Reference when a specific instance must travel through a graph.").Build();
            context.AddInputPort<NovelCharacterReference>("Character Reference")
                .WithTooltip("Optional target containing both the character asset and its instance ID.").Build();
            context.AddOutputPort<NovelCharacter>("Character")
                .WithTooltip("Pass this character to another character node or a dialogue Speaker input.").Build();
            context.AddOutputPort<NovelCharacterReference>("Character Reference")
                .WithTooltip("Pass the character and instance ID together to another node.").Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) => DefineInstanceOption(context);

        internal static void DefineInstanceOption(IOptionDefinitionContext context)
        {
            context.AddOption<string>("Instance ID").WithDefaultValue(string.Empty)
                .WithTooltip("Empty uses the default instance. Use the same unique ID across nodes to target an additional copy of this character.").Build();
        }
    }

    [Serializable, Node("Novelify/Flow"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
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

    [Serializable, Node("Novelify/Flow"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
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

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class ShowCharacterNode : CharacterActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<Vector2>("Position").WithTooltip("Initial position interpreted in the selected coordinate space.").Build();
            context.AddOption<CharacterPositionSpace>("Coordinate Space")
                .WithDefaultValue(CharacterPositionSpace.Canvas)
                .WithTooltip("Canvas preserves legacy anchored-position behavior. Normalized maps (-1,-1) to bottom-left and (1,1) to top-right.")
                .Build();
            context.AddOption<CharacterEmotion>("Emotion").WithDefaultValue(CharacterEmotion.Neutral).Build();
        }
    }

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class HideCharacterNode : CharacterActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<CharacterTransitionMode>("Hide Transition")
                .WithDefaultValue(CharacterTransitionMode.Instant)
                .WithTooltip("Hide instantly, fade out, slide out, or fade and slide out.")
                .Build();
            context.AddOption<CharacterTransitionDirection>("Exit Toward")
                .WithDefaultValue(CharacterTransitionDirection.Left)
                .WithTooltip("Direction used by Slide and Fade And Slide exits.")
                .Build();
            context.AddOption<float>("Duration")
                .WithDefaultValue(0.35f)
                .WithTooltip("Exit time in real-time seconds.")
                .Build();
            context.AddOption<float>("Slide Offset")
                .WithDefaultValue(0.45f)
                .WithTooltip("In case of sliding, how much? the lower the number the less it slides")
                .Build();
            context.AddOption<PortraitTweenEasing>("Easing")
                .WithDefaultValue(PortraitTweenEasing.EaseIn)
                .WithTooltip("Timing preset for the exit transition.")
                .Build();
            context.AddOption<bool>("Wait For Completion")
                .WithDefaultValue(true)
                .WithTooltip("Wait for the exit before continuing. Disable to run it during following nodes.")
                .Build();
        }
    }

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class HideAllCharactersNode : ActionNode { }

    [Serializable, Node("Novelify/Characters"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class SetCharacterEmotionNode : CharacterActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<CharacterEmotion>("Emotion").WithDefaultValue(CharacterEmotion.Neutral).Build();
        }
    }

    [Serializable, Node("Novelify/Flow"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class WaitNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<float>("Duration").WithDefaultValue(1f)
                .WithTooltip("Wait this many real-time seconds before continuing. Clicks do not skip the wait.").Build();
        }
    }

    [Serializable, Node("Novelify/Flow", null, "Checkpoint"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class CheckpointNode : ActionNode
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(this, "Safe resume point",
                "Requests a snapshot at the next dialogue or choice boundary, after automatic work finishes.",
                new Color32(96, 165, 250, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<string>("Checkpoint ID")
                .WithDefaultValue(string.Empty)
                .WithTooltip("Stable authored name used as a migration fallback if later content removes the saved line.")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<NovelCheckpointSaveMode>("Save Mode")
                .WithDefaultValue(NovelCheckpointSaveMode.SnapshotOnly)
                .WithTooltip("Snapshot Only updates the in-memory recovery point. Autosave also writes it to the configured autosave slot at the safe boundary.")
                .Build();
            context.AddOption<string>("Autosave Slot")
                .WithDefaultValue("autosave")
                .WithTooltip("Used only in Autosave mode. Do not target player-owned manual slots from story content.")
                .Build();
        }
    }

    [Serializable, Node("Novelify/Utilities"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class DialogueEventNode : ActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<string>("Event Name").WithDefaultValue(string.Empty)
                .WithTooltip("Sent to the runner's dialogue-event listeners, then flow continues.").Build();
        }
    }

    [Serializable, Node("Novelify/Utilities"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class StopSoundNode : ActionNode { }

    [Serializable]
    public abstract class FloatBinaryNode : Node
    {
        protected virtual float DefaultB => 0f;

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<float>("A").WithDefaultValue(0f).Build();
            context.AddInputPort<float>("B").WithDefaultValue(DefaultB).Build();
            context.AddOutputPort<float>("Result").Build();
        }
    }

    [Serializable]
    public abstract class Vector2BinaryNode : Node
    {
        protected virtual Vector2 DefaultB => Vector2.zero;

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<Vector2>("A").WithDefaultValue(Vector2.zero).Build();
            context.AddInputPort<Vector2>("B").WithDefaultValue(DefaultB).Build();
            context.AddOutputPort<Vector2>("Result").Build();
        }
    }

    [Serializable, Node("Novelify/Math/Float", null, "Add"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class AddFloatNode : FloatBinaryNode { }

    [Serializable, Node("Novelify/Math/Float", null, "Subtract"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class SubtractFloatNode : FloatBinaryNode { }

    [Serializable, Node("Novelify/Math/Float", null, "Multiply"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class MultiplyFloatNode : FloatBinaryNode { protected override float DefaultB => 1f; }

    [Serializable, Node("Novelify/Math/Float", null, "Divide"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class DivideFloatNode : FloatBinaryNode { protected override float DefaultB => 1f; }

    [Serializable, Node("Novelify/Math/Vector 2", null, "Add"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class AddVector2Node : Vector2BinaryNode { }

    [Serializable, Node("Novelify/Math/Vector 2", null, "Subtract"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class SubtractVector2Node : Vector2BinaryNode { }

    [Serializable, Node("Novelify/Math/Vector 2", null, "Multiply"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class MultiplyVector2Node : Vector2BinaryNode { protected override Vector2 DefaultB => Vector2.one; }

    [Serializable, Node("Novelify/Math/Vector 2", null, "Divide"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class DivideVector2Node : Vector2BinaryNode { protected override Vector2 DefaultB => Vector2.one; }

    [Serializable, Node("Novelify/Characters", null, "Split Novel Character"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class SplitNovelCharacterNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<NovelCharacter>("Character").Build();
            context.AddInputPort<string>("Instance ID").WithDefaultValue(string.Empty)
                .WithTooltip("Selects a specific live copy of the character. Empty uses the default instance.").Build();

            context.AddOutputPort<NovelCharacter>("Character").Build();
            context.AddOutputPort<string>("Speaker Name").Build();
            context.AddOutputPort<Sprite>("Body").Build();
            context.AddOutputPort<Sprite>("Eyes").Build();
            context.AddOutputPort<Sprite>("Eyes Closed").Build();
            context.AddOutputPort<Sprite>("Details").Build();
            context.AddOutputPort<Sprite>("Mouth").Build();
            context.AddOutputPort<Sprite>("Mouth Open").Build();
            context.AddOutputPort<Vector2>("Position (Normalized)")
                .WithTooltip("Current live portrait position in normalized screen coordinates.").Build();
            context.AddOutputPort<Vector2>("Position (Canvas)")
                .WithTooltip("Current live RectTransform anchored position in canvas units.").Build();
            context.AddOutputPort<float>("Rotation").Build();
            context.AddOutputPort<Vector2>("Scale").Build();
        }
    }

    [Serializable, Node("Novelify/Characters", null, "Make Novel Character Reference"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class MakeNovelCharacterReferenceNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<NovelCharacter>("Character").Build();
            context.AddInputPort<string>("Instance ID").WithDefaultValue(string.Empty).Build();
            context.AddOutputPort<NovelCharacterReference>("Character Reference")
                .WithTooltip("Carries the character asset and instance ID as one value.").Build();
        }
    }

    [Serializable, Node("Novelify/Characters", null, "Split Novel Character Reference"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class SplitNovelCharacterReferenceNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<NovelCharacterReference>("Character Reference").Build();
            context.AddOutputPort<NovelCharacter>("Character").Build();
            context.AddOutputPort<string>("Instance ID").Build();
        }
    }
}
