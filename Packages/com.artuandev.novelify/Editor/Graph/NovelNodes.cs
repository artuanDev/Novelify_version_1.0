using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Novelify.Editor
{
    internal static class NovelNodeCategories
    {
        public const string Flow = "Novelify/Flow";
        public const string Story = "Novelify/Story";
        public const string Characters = "Novelify/Characters";
        public const string Presentation = "Novelify/Presentation";
        public const string PresentationTransitions = Presentation + "/Transitions";
        public const string Audio = "Novelify/Audio";
        public const string State = "Novelify/State";
        public const string Logic = "Novelify/Logic";
        public const string Values = "Novelify/Values";
        public const string CharacterValues = Values + "/Characters";
        public const string FloatMath = Values + "/Math/Float";
        public const string Vector2Math = Values + "/Math/Vector2";
    }

    // Graph Toolkit cannot assign AnimationCurve directly through TrySetValue.
    // Wrap it in authoring data so custom curves can be edited and serialized.
    [Serializable]
    public struct NovelEasingCurve
    {
        public AnimationCurve Curve;
    }

    // Authoring nodes are editor-only; the importer creates their runtime equivalents.

    [Serializable]
    [Node(NovelNodeCategories.Flow, "d_PlayButton", "Start",
        "Packages/com.artuandev.novelify/Editor/Graph/Styles/StartNode.uss")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class StartNode: Node
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Story entry",
                "The first beat in this narrative path.",
                new Color32(52, 211, 153, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort("out")
                .WithCapacity(PortCapacity.Single)
                .WithDisplayName("Begin")
                .WithTooltip("Connect to the first story node.")
                .Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Audio, null, "Play Sound")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class PlaySoundNode : Node
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Play sound",
                "Plays an audio clip with per-node loop, volume, priority, and pitch settings.",
                new Color32(96, 165, 250, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in").Build();
            context.AddOutputPort("out").WithCapacity(PortCapacity.Single).Build();

            context.AddInputPort<AudioClip>("AudioToPlay").Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<bool>("Loop").Build();
            context.AddOption<float>("Volume").WithTooltip("From 0 to 1").WithDefaultValue(1.0f).Build();
            context.AddOption<int>("Priority").WithTooltip("From 0 to 256").WithDefaultValue(128).Build();
            context.AddOption<float>("Pitch").WithTooltip("From -3 to 3").WithDefaultValue(1.0f).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Characters, null, "Transform Characters")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class TransformSpeakerPortraitNode : CharacterActionNode
    {
        public const string TargetsOptionID = "Animated Characters";

        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Transform characters",
                "Moves, rotates, scales, and fades any number of character instances in one synchronized tween.",
                new Color32(251, 191, 36, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<Vector2>("Position")
                .WithDefaultValue(new Vector2(0f, -1f))
                .WithTooltip("Normalized target: (-1,-1) is bottom-left and (1,1) is top-right.")
                .Build();
            context.AddInputPort<float>("Rotation")
                .WithDefaultValue(0f)
                .WithTooltip("Target Z rotation in degrees.")
                .Build();
            context.AddInputPort<Vector2>("Scale")
                .WithDefaultValue(Vector2.one)
                .WithTooltip("Target local X/Y scale.")
                .Build();
            context.AddInputPort<float>("Margin")
                .WithDefaultValue(0f)
                .WithTooltip("Canvas-unit distance allowed beyond each screen edge.")
                .Build();
            context.AddInputPort<float>("Opacity")
                .WithDefaultValue(1f)
                .WithTooltip("Target portrait opacity from 0 (transparent) to 1 (opaque). Used when Animate Transparency is enabled.")
                .Build();
            int characterCount = GetDesiredCharacterCount();
            for (int target = 2; target <= characterCount; target++)
            {
                context.AddInputPort<NovelCharacter>($"Character {target}")
                    .WithTooltip($"Character target {target}. Every connected target starts its tween on the same frame.")
                    .Build();
                context.AddInputPort<NovelCharacterReference>($"Character Reference {target}")
                    .WithTooltip($"Optional exact instance for character target {target}.")
                    .Build();
                context.AddInputPort<Vector2>($"Position {target}")
                    .WithDefaultValue(new Vector2(0f, -1f))
                    .WithTooltip($"Target {target} position in the shared coordinate space.")
                    .Build();
                context.AddInputPort<float>($"Rotation {target}")
                    .WithDefaultValue(0f).Build();
                context.AddInputPort<Vector2>($"Scale {target}")
                    .WithDefaultValue(Vector2.one).Build();
                context.AddInputPort<float>($"Margin {target}")
                    .WithDefaultValue(0f).Build();
                context.AddInputPort<float>($"Opacity {target}")
                    .WithDefaultValue(1f).Build();
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<CharacterPositionSpace>("Coordinate Space")
                .WithDefaultValue(CharacterPositionSpace.Normalized)
                .WithTooltip("Normalized maps the screen to -1..1. Canvas uses anchored canvas units.")
                .Build();
            context.AddOption<bool>("Relative").WithTooltip("Add the X/Y displacement, in the selected coordinate space, to the current position. Rotation and scale remain absolute.").Build();
            context.AddOption<bool>("Animate Transform").WithTooltip("Animate position, rotation, and scale over Duration; disable to apply instantly.").WithDefaultValue(false).Build();
            context.AddOption<bool>("Animate Transparency").WithTooltip("Animate the portrait opacity to the Opacity value over the same duration and easing.").WithDefaultValue(false).Build();
            context.AddOption<float>("Duration").WithTooltip("Transform time in real-time seconds. Zero applies instantly.").WithDefaultValue(0.5f).Build();
            context.AddOption<PortraitTweenEasing>("Easing").WithTooltip("Timing preset for the portrait tween. None uses constant linear timing; Custom uses the editable curve.").WithDefaultValue(PortraitTweenEasing.EaseInOut).Build();
            context.AddOption<NovelEasingCurve>("Custom Easing Data")
                .WithDefaultValue(default).ShowInInspectorOnly().Build();
            context.AddOption<bool>("Wait For Completion").WithTooltip("Wait for the transform before continuing. Disable to animate during following dialogue.").WithDefaultValue(true).Build();
            context.AddOption(
                    TargetsOptionID,
                    typeof(TransformTargetAuthoringList))
                .WithDefaultValue(
                    TransformTargetAuthoringList.CreateDefault())
                .WithTooltip("Add or remove synchronized character transform groups without a fixed limit.")
                .Build();
            for (int target = 2;
                 target <= GetDesiredCharacterCount();
                 target++)
                context.AddOption<string>($"Instance ID {target}")
                    .WithDefaultValue(string.Empty)
                    .WithTooltip($"Optional instance ID for Character {target} when its reference port is not connected.")
                    .Build();
        }

        internal static AnimationCurve GetCustomEasingCurve(INode node)
        {
            INodeOption data = node.GetNodeOptionByName("Custom Easing Data");
            if (data != null && data.TryGetValue(out NovelEasingCurve authored) && authored.Curve != null)
                return authored.Curve;
            return AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        internal int GetDesiredCharacterCount()
        {
            INodeOption option = GetNodeOptionByName(TargetsOptionID);
            return option != null &&
                option.TryGetValue(out TransformTargetAuthoringList targets)
                ? Mathf.Max(1, targets?.Targets?.Count ?? 1)
                : 1;
        }

        internal bool TransformPortCountMatches()
        {
            int actual = GetInputPorts().Count(port =>
            {
                if (port.Name == "Character")
                    return true;
                const string prefix = "Character ";
                return port.Name.StartsWith(prefix, StringComparison.Ordinal) &&
                       int.TryParse(
                           port.Name.Substring(prefix.Length),
                           out _);
            });
            return actual == GetDesiredCharacterCount();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Characters, null, "Flip Character")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class FlipCharacterNode : CharacterActionNode
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Toggle flip",
                "Toggles the selected axes each time this node runs. Use Set Facing when repeated execution must be idempotent.",
                new Color32(251, 191, 36, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<bool>("FlipX").WithTooltip("Flip in the X Axis.").WithDefaultValue(true).Build();
            context.AddOption<bool>("FlipY").WithTooltip("Flip in the Y Axis.").WithDefaultValue(false).Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Characters, null, "Set Facing")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class SetCharacterFacingNode : CharacterActionNode
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Set facing",
                "Sets horizontal facing deterministically; running it repeatedly keeps the same orientation.",
                new Color32(251, 191, 36, 255));
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<CharacterFacing>("Facing")
                .WithDefaultValue(CharacterFacing.Right)
                .WithTooltip("Right uses a positive absolute X scale; Left uses a negative absolute X scale.")
                .Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Flow, "d_console.erroricon", "End",
        "Packages/com.artuandev.novelify/Editor/Graph/Styles/EndNode.uss")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class EndNode: Node
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Story exit",
                "Closes the current narrative path.",
                new Color32(251, 113, 133, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in")
                .WithDisplayName("Finish")
                .WithTooltip("Connect the final story beat here.")
                .Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Story, "d_console.infoicon", "Simple Dialogue",
        "Packages/com.artuandev.novelify/Editor/Graph/Styles/DialogueNode.uss")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class SimpleDialogueNode : Node
    {
        internal const string SoundPortName = "Sound";

        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Character beat",
                "Displays a spoken line with portrait and delivery controls.",
                new Color32(56, 189, 248, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in")
                .WithDisplayName("Enter")
                .Build();

            context.AddInputPort<AudioClip>(SoundPortName)
                .WithDisplayName("Play Sound")
                .WithTooltip("Sound to play when this node is shown.")
                .Build();

            context.AddOutputPort("out")
                .WithCapacity(PortCapacity.Single)
                .WithDisplayName("Continue")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption("Dialogue", typeof(RichDialogueText))
                .WithDefaultValue(new RichDialogueText(string.Empty))
                .Build();

            context.AddOption("Show Text Immediately", typeof(bool))
                .WithDefaultValue(false)
                .Build();

            context.AddOption("Text Speed (Characters/Second)", typeof(float))
                .WithDefaultValue(30f)
                .Build();
        }
    }


    [Serializable]
    [Node(NovelNodeCategories.Story, "d_console.infoicon", "Dialogue",
        "Packages/com.artuandev.novelify/Editor/Graph/Styles/DialogueNode.uss")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class DialogueNode: SimpleDialogueNode
    {
        protected virtual string CharacterPortName => "Speaker";
        protected virtual string CharacterReferencePortName => "Speaker Reference";
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Character beat",
                "Displays a spoken line with portrait and delivery controls.",
                new Color32(56, 189, 248, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base .OnDefinePorts(context);

            context.AddInputPort<NovelCharacter>(CharacterPortName)
                .WithTooltip("Character whose name, portrait, voice, and timing are used.")
                .Build();
            context.AddInputPort<NovelCharacterReference>(CharacterReferencePortName)
                .WithTooltip("Optional speaker value containing both the character asset and instance ID.")
                .Build();

            context.AddOutputPort<NovelCharacter>("Current Speaker")
                .WithTooltip("Use this output to keep using the same speaker in the next node easily.")
                .Build();
            context.AddOutputPort<NovelCharacterReference>("Current Speaker Reference")
                .WithTooltip("Pass this exact speaker instance to another node.")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {

            context.AddOption("Speaker Preview", typeof(SpeakerPortraitOption))
                .WithDefaultValue(new SpeakerPortraitOption())
                .Build();
            base.OnDefineOptions(context);

            context.AddOption("Emotion", typeof(CharacterEmotion))
                .WithDefaultValue(CharacterEmotion.Neutral)
                .Build();

            CharacterActionNode.DefineInstanceOption(context);

            context.AddOption("Animate Mouth", typeof(bool))
                .WithDefaultValue(true)
                .Build();

            context.AddOption("Animate Blinking", typeof(bool))
                .WithDefaultValue(true)
                .Build();

            context.AddOption<CharacterTransitionMode>("Character Appearance")
                .WithDefaultValue(CharacterTransitionMode.Instant)
                .WithTooltip("Optionally fade or slide the speaker into the scene when this dialogue begins.")
                .Build();
            context.AddOption<CharacterTransitionDirection>("Appear From")
                .WithDefaultValue(CharacterTransitionDirection.Left)
                .WithTooltip("Direction used by Slide and Fade And Slide appearances.")
                .Build();
            context.AddOption<float>("Slide Offset")
                .WithDefaultValue(0.45f)
                .WithTooltip("In case of sliding, how much? the lower the number the less it slides")
                .Build();
            context.AddOption<float>("Appearance Duration")
                .WithDefaultValue(0.35f)
                .WithTooltip("Appearance time in real-time seconds.")
                .Build();
            context.AddOption<PortraitTweenEasing>("Appearance Easing")
                .WithDefaultValue(PortraitTweenEasing.EaseOut)
                .WithTooltip("Timing preset for the speaker appearance.")
                .Build();
        }
    }

    [Serializable]
    [Node(NovelNodeCategories.Story, "d_TreeEditor.Duplicate", "Choice",
        "Packages/com.artuandev.novelify/Editor/Graph/Styles/ChoiceNode.uss")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class ChoiceNode: Node
    {
        public const string ChoicesOptionID = "Choices";

        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Player branch",
                "Presents choices and routes the story through the selected branch.",
                new Color32(192, 132, 252, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in")
                .WithDisplayName("Enter")
                .Build();

            context.AddInputPort<NovelCharacter>("Speaker")
                .WithTooltip("Character presenting this decision.")
                .Build();
            context.AddInputPort<NovelCharacterReference>("Speaker Reference")
                .WithTooltip("Optional speaker value containing both the character asset and instance ID.")
                .Build();

            context.AddInputPort<AudioClip>(SimpleDialogueNode.SoundPortName)
                .WithDisplayName("Play Sound")
                .WithTooltip("Sound to play when this node is shown.")
                .Build();

            int portCount = GetDesiredChoiceCount();
            for (int i = 0; i < portCount; i++)
            {
                string outputName = GetChoiceOutputDisplayName(i);
                context.AddInputPort<string>($"Choice Text {i}")
                    .WithDefaultValue(string.Empty)
                    .WithTooltip("Optional dynamic text. Connect a value to override the text in Choices.")
                    .Build();
                context.AddInputPort<bool>($"Condition {i}")
                    .WithDefaultValue(true)
                    .WithDisplayName($"Available when: {outputName}")
                    .WithTooltip("Optional dynamic condition for this choice. Leave true for an always-available choice.")
                    .Build();
                context.AddInputPort<string>($"Disabled Reason {i}")
                    .WithDefaultValue(string.Empty)
                    .WithTooltip("Optional dynamic reason. Connect a value to override the reason in Choices.")
                    .Build();
                context.AddOutputPort($"Choice {i}")
                    .WithDisplayName(outputName)
                    .WithCapacity(PortCapacity.Single)
                    .Build();
            }
            context.AddOutputPort("Fallback")
                .WithCapacity(PortCapacity.Single)
                .WithTooltip("Used when no choice is actionable.")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            CharacterActionNode.DefineInstanceOption(context);
            context.AddOption("Speaker Preview", typeof(SpeakerPortraitOption))
                .WithDefaultValue(new SpeakerPortraitOption())
                .Build();

            context.AddOption("Dialogue", typeof(RichDialogueText))
                .WithDefaultValue(new RichDialogueText(string.Empty))
                .Build();

            context.AddOption(ChoicesOptionID, typeof(ChoiceAuthoringList))
                .WithDefaultValue(ChoiceAuthoringList.CreateDefault())
                .WithTooltip("Open each dropdown to edit its text, stable ID, availability, and transaction. The ID names its output port.")
                .Build();

            context.AddOption("Emotion", typeof(CharacterEmotion))
                .WithDefaultValue(CharacterEmotion.Neutral)
                .Build();

            context.AddOption("Show Text Immediately", typeof(bool))
                .WithDefaultValue(false)
                .Build();

            context.AddOption("Text Speed (Characters/Second)", typeof(float))
                .WithDefaultValue(30f)
                .Build();

            context.AddOption("Animate Mouth", typeof(bool))
                .WithDefaultValue(true)
                .Build();

            context.AddOption("Animate Blinking", typeof(bool))
                .WithDefaultValue(true)
                .Build();

        }

        internal IReadOnlyList<ChoiceAuthoringEntry> GetAuthoredChoices()
        {
            INodeOption option = GetNodeOptionByName(ChoicesOptionID);
            return option != null && option.TryGetValue(out ChoiceAuthoringList choices) && choices?.Entries != null
                ? choices.Entries
                : null;
        }

        internal int GetDesiredChoiceCount() => GetAuthoredChoices()?.Count ?? 0;

        internal string GetChoiceOutputDisplayName(int index)
        {
            IReadOnlyList<ChoiceAuthoringEntry> authored = GetAuthoredChoices();
            if (authored != null && index >= 0 && index < authored.Count &&
                !string.IsNullOrWhiteSpace(authored[index]?.ID))
                return authored[index].ID.Trim();

            return $"Choice {index + 1}";
        }

        internal bool ChoiceOutputNamesMatch()
        {
            int count = GetDesiredChoiceCount();
            for (int index = 0; index < count; index++)
            {
                IPort output = GetOutputPortByName($"Choice {index}");
                if (output == null || output.DisplayName != GetChoiceOutputDisplayName(index))
                    return false;
            }
            return GetOutputPorts().Count(port => port.Name.StartsWith("Choice ", StringComparison.Ordinal)) == count;
        }

    }

    internal static class NovelNodePresentation
    {
        public static void Apply(Node node, string subtitle, string tooltip, Color accent)
        {
            if (string.IsNullOrWhiteSpace(node.Subtitle))
            {
                node.Subtitle = subtitle;
            }

            if (string.IsNullOrWhiteSpace(node.Tooltip))
            {
                node.Tooltip = tooltip;
            }

            node.DefaultColor = accent;
        }
    }
}
