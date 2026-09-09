using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Novelify.Editor
{
    /*Nodes CAN´T be accesed by normal monobehaviours, we instead need
    to make a runtime version of the nodes in the graph*/

    [Serializable]
    [Node("Novelify/Flow", "d_PlayButton", "Start",
        "Assets/Novelify/Editor/Graph/Styles/StartNode.uss")]
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
    [Node("Novelify/Utilities")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class PlaySoundNode : Node
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
    [Node("Novelify/Utilities")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class TransformSpeakerPortraitNode : CharacterActionNode
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Transform character",
                "Moves, rotates, and scales one character instance using normalized screen coordinates.",
                new Color32(251, 191, 36, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<Vector2>("Position")
                .WithDefaultValue(Vector2.zero)
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
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            // Keep the original serialized option IDs so graphs created before the
            // value-port upgrade retain their transform values on reimport.
            context.AddOption<float>("OffsetX").WithDisplayName("Legacy Offset X")
                .WithDefaultValue(0f).ShowInInspectorOnly().Build();
            context.AddOption<float>("OffsetY").WithDisplayName("Legacy Offset Y")
                .WithDefaultValue(0f).ShowInInspectorOnly().Build();
            context.AddOption<float>("Rotation").WithDisplayName("Legacy Rotation")
                .WithDefaultValue(0f).ShowInInspectorOnly().Build();
            context.AddOption<Vector2>("Scale").WithDisplayName("Legacy Scale")
                .WithDefaultValue(Vector2.one).ShowInInspectorOnly().Build();
            context.AddOption<float>("Margin").WithDisplayName("Legacy Margin")
                .WithDefaultValue(0f).ShowInInspectorOnly().Build();
            context.AddOption<CharacterPositionSpace>("Coordinate Space")
                .WithDefaultValue(CharacterPositionSpace.Normalized)
                .WithTooltip("Normalized maps the screen to -1..1. Canvas uses anchored canvas units for legacy layouts.")
                .Build();
            context.AddOption<bool>("Relative").WithTooltip("Add the X/Y displacement, in the selected coordinate space, to the current position. Rotation and scale remain absolute.").Build();
            context.AddOption<bool>("Animate Transform").WithTooltip("Animate position, rotation, and scale over Duration; disable to apply instantly.").WithDefaultValue(false).Build();
            context.AddOption<float>("Duration").WithTooltip("Transform time in real-time seconds. Zero applies instantly.").WithDefaultValue(0.5f).Build();
            context.AddOption<bool>("Ease In Out").WithTooltip("Accelerate and decelerate smoothly; disable for constant speed.").WithDefaultValue(true).Build();
            context.AddOption<bool>("Wait For Completion").WithTooltip("Wait for the transform before continuing. Disable to animate during following dialogue.").WithDefaultValue(true).Build();
        }
    }

    // Retained so existing graph assets containing the old node type continue to load.
    // New nodes should use TransformSpeakerPortraitNode above.
    [Serializable]
    public class TranslateSpeakerPortraitNode : CharacterActionNode
    {
        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<float>("OffsetX").WithTooltip("Legacy target X in canvas units.").WithDefaultValue(0f).Build();
            context.AddOption<float>("OffsetY").WithTooltip("Legacy target Y in canvas units.").WithDefaultValue(0f).Build();
            context.AddOption<bool>("Relative").WithTooltip("Move by this offset from the current position.").Build();
            context.AddOption<bool>("Smooth Movement").WithTooltip("Animate the move over Duration.").WithDefaultValue(false).Build();
            context.AddOption<float>("Duration").WithDefaultValue(0.5f).Build();
            context.AddOption<bool>("Ease In Out").WithDefaultValue(true).Build();
            context.AddOption<bool>("Wait For Completion").WithDefaultValue(true).Build();
        }
    }

    [Serializable]
    [Node("Novelify/Utilities")]
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
    [Node("Novelify/Characters", null, "Set Facing")]
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
    [Node("Novelify/Flow", "d_console.erroricon", "End",
        "Assets/Novelify/Editor/Graph/Styles/EndNode.uss")]
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
    [Node("Novelify/Story", "d_console.infoicon", "SimpleDialogue",
        "Assets/Novelify/Editor/Graph/Styles/DialogueNode.uss")]
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
                .ShowInInspectorOnly()
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
    [Node("Novelify/Story", "d_console.infoicon", "Dialogue",
        "Assets/Novelify/Editor/Graph/Styles/DialogueNode.uss")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class DialogueNode: SimpleDialogueNode
    {
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

            context.AddInputPort<NovelCharacter>("Speaker")
                .WithTooltip("Character whose name, portrait, voice, and timing are used.")
                .Build();
            context.AddInputPort<NovelCharacterReference>("Speaker Reference")
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
        }
    }

    [Serializable]
    [Node("Novelify/Story", "d_TreeEditor.Duplicate", "Choice",
        "Assets/Novelify/Editor/Graph/Styles/ChoiceNode.uss")]
    [UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public class ChoiceNode: Node
    {
        const string optionID = "portCount";
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

            IReadOnlyList<ChoiceAuthoringEntry> authoredChoices = GetAuthoredChoices();
            int portCount = GetDesiredChoiceCount();
            for (int i = 0; i < portCount; i++)
            {
                string outputName = GetChoiceOutputDisplayName(i);
                // These ports remain serialized for graphs created before the
                // foldout editor. Their views are hidden by the Novelify graph
                // UI adapter, so authors see each field only once.
                context.AddInputPort<string>($"Choice ID {i}")
                    .WithDefaultValue(string.Empty)
                    .Build();
                context.AddInputPort<string>($"Choice Text {i}")
                    .WithDefaultValue(string.Empty)
                    .Build();
                context.AddInputPort<bool>($"Condition {i}")
                    .WithDefaultValue(true)
                    .WithDisplayName($"Available when: {outputName}")
                    .WithTooltip("Optional dynamic condition for this choice. Leave true for an always-available choice.")
                    .Build();
                context.AddInputPort<NovelChoiceUnavailablePolicy>($"Unavailable Policy {i}")
                    .WithDefaultValue(NovelChoiceUnavailablePolicy.Hide)
                    .Build();
                context.AddInputPort<string>($"Disabled Reason {i}")
                    .WithDefaultValue(string.Empty)
                    .Build();
                context.AddInputPort<bool>($"Once Only {i}")
                    .WithDefaultValue(false)
                    .Build();
                context.AddInputPort<NovelChoiceTransactionDefinition>($"Transaction {i}")
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
                .ShowInInspectorOnly()
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

            context.AddOption(optionID, typeof(int))
                .WithDisplayName("Legacy Choice Count")
                .WithTooltip("Retained for old graph assets. Add and remove choices with the Choices dropdown instead.")
                .Delayed().WithDefaultValue(2).ShowInInspectorOnly().Build();
        }

        internal IReadOnlyList<ChoiceAuthoringEntry> GetAuthoredChoices()
        {
            INodeOption option = GetNodeOptionByName(ChoicesOptionID);
            return option != null && option.TryGetValue(out ChoiceAuthoringList choices) && choices?.Entries != null
                ? choices.Entries
                : null;
        }

        internal int GetDesiredChoiceCount()
        {
            int legacyPortCount = 0;
            GetNodeOptionByName(optionID)?.TryGetValue(out legacyPortCount);
            // Taking the larger count preserves every branch in graphs authored
            // before the foldout-based choice editor was introduced.
            return Mathf.Max(GetAuthoredChoices()?.Count ?? 0, legacyPortCount);
        }

        internal string GetChoiceOutputDisplayName(int index)
        {
            IReadOnlyList<ChoiceAuthoringEntry> authored = GetAuthoredChoices();
            if (authored != null && index >= 0 && index < authored.Count &&
                !string.IsNullOrWhiteSpace(authored[index]?.ID))
                return authored[index].ID.Trim();

            IPort legacyID = GetInputPortByName($"Choice ID {index}");
            if (legacyID != null && legacyID.TryGetValue(out string id) && !string.IsNullOrWhiteSpace(id))
                return id.Trim();
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

        internal bool TryGetMigratedChoices(out ChoiceAuthoringList migrated)
        {
            IReadOnlyList<ChoiceAuthoringEntry> current = GetAuthoredChoices();
            var source = new ChoiceAuthoringList { Entries = current?.ToList() ?? new List<ChoiceAuthoringEntry>() };
            migrated = source.Clone(GetDesiredChoiceCount());
            bool changed = false;

            for (int index = 0; index < migrated.Entries.Count; index++)
            {
                ChoiceAuthoringEntry entry = migrated.Entries[index];
                IPort idPort = GetInputPortByName($"Choice ID {index}");
                IPort textPort = GetInputPortByName($"Choice Text {index}");
                IPort conditionPort = GetInputPortByName($"Condition {index}");
                IPort policyPort = GetInputPortByName($"Unavailable Policy {index}");
                IPort reasonPort = GetInputPortByName($"Disabled Reason {index}");
                IPort oncePort = GetInputPortByName($"Once Only {index}");
                IPort transactionPort = GetInputPortByName($"Transaction {index}");

                if (string.IsNullOrWhiteSpace(entry.ID) && idPort?.TryGetValue(out string id) == true &&
                    !string.IsNullOrWhiteSpace(id))
                { entry.ID = id.Trim(); changed = true; }
                if (string.IsNullOrEmpty(entry.Text) && textPort?.TryGetValue(out string text) == true &&
                    !string.IsNullOrEmpty(text))
                { entry.Text = text; changed = true; }
                if (entry.Condition && conditionPort?.IsConnected != true &&
                    conditionPort?.TryGetValue(out bool available) == true && !available)
                { entry.Condition = false; changed = true; }
                if (entry.UnavailablePolicy == NovelChoiceUnavailablePolicy.Hide &&
                    policyPort?.TryGetValue(out NovelChoiceUnavailablePolicy policy) == true &&
                    policy != NovelChoiceUnavailablePolicy.Hide)
                { entry.UnavailablePolicy = policy; changed = true; }
                if (string.IsNullOrEmpty(entry.DisabledReason) &&
                    reasonPort?.TryGetValue(out string reason) == true && !string.IsNullOrEmpty(reason))
                { entry.DisabledReason = reason; changed = true; }
                if (!entry.OnceOnly && oncePort?.TryGetValue(out bool once) == true && once)
                { entry.OnceOnly = true; changed = true; }
                if (entry.Transaction == null &&
                    transactionPort?.TryGetValue(out NovelChoiceTransactionDefinition transaction) == true &&
                    transaction != null)
                { entry.Transaction = transaction; changed = true; }
            }
            return changed;
        }

        internal void ClearMigratedLegacyChoiceValues()
        {
            for (int index = 0; index < GetDesiredChoiceCount(); index++)
            {
                ClearIfNotConnected(GetInputPortByName($"Choice ID {index}"), string.Empty);
                ClearIfNotConnected(GetInputPortByName($"Choice Text {index}"), string.Empty);
                ClearIfNotConnected(GetInputPortByName($"Condition {index}"), true);
                ClearIfNotConnected(GetInputPortByName($"Unavailable Policy {index}"), NovelChoiceUnavailablePolicy.Hide);
                ClearIfNotConnected(GetInputPortByName($"Disabled Reason {index}"), string.Empty);
                ClearIfNotConnected(GetInputPortByName($"Once Only {index}"), false);
                ClearIfNotConnected<NovelChoiceTransactionDefinition>(
                    GetInputPortByName($"Transaction {index}"), null);
            }
        }

        private static void ClearIfNotConnected<T>(IPort port, T value)
        {
            if (port?.IsConnected != true) port?.TrySetValue(value);
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
