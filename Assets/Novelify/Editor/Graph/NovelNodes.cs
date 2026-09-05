using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;

namespace Novelify.Editor
{
    /*Nodes CAN´T be accesed by normal monobehaviours, we instead need
    to make a runtime version of the nodes in the graph*/

    [Serializable]
    [Node("Novelify/Flow", "d_PlayButton", "Start",
        "Assets/Novelify/Editor/Graph/Styles/StartNode.uss")]
    [UseWithGraph(typeof(NovelGraph))]
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
    [UseWithGraph(typeof(NovelGraph))]
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
    [UseWithGraph(typeof(NovelGraph))]
    public class TranslateSpeakerPortraitNode : CharacterActionNode
    {
        public override void OnEnable()
        {
            base.OnEnable();
            NovelNodePresentation.Apply(
                this,
                "Move character",
                "Creates the selected character if needed, then moves that instance on the stage.",
                new Color32(251, 191, 36, 255));
        }

        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            base.OnDefineOptions(context);
            context.AddOption<float>("OffsetX").WithTooltip("Target X in canvas units; an offset when Relative is enabled.").WithDefaultValue(0f).Build();
            context.AddOption<float>("OffsetY").WithTooltip("Target Y in canvas units; an offset when Relative is enabled.").WithDefaultValue(0f).Build();
            context.AddOption<bool>("Relative").WithTooltip("Move by this offset from the current position.").Build();
            context.AddOption<bool>("Smooth Movement").WithTooltip("Animate the move over Duration; disable to move instantly.").WithDefaultValue(false).Build();
            context.AddOption<float>("Duration").WithTooltip("Movement time in seconds. Zero moves instantly.").WithDefaultValue(0.5f).Build();
            context.AddOption<bool>("Ease In Out").WithTooltip("Accelerate and decelerate smoothly; disable for constant speed.").WithDefaultValue(true).Build();
            context.AddOption<bool>("Wait For Completion").WithTooltip("Wait for this move before continuing the story. Disable to move during dialogue.").WithDefaultValue(true).Build();
        }
    }

    [Serializable]
    [Node("Novelify/Flow", "d_console.erroricon", "End",
        "Assets/Novelify/Editor/Graph/Styles/EndNode.uss")]
    [UseWithGraph(typeof(NovelGraph))]
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
    [UseWithGraph(typeof(NovelGraph))]
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
    [UseWithGraph(typeof(NovelGraph))]
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

            context.AddOutputPort<NovelCharacter>("Current Speaker")
                .WithTooltip("Use this output to keep using the same speaker in the next node easily.")
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
    [UseWithGraph(typeof(NovelGraph))]
    public class ChoiceNode: Node
    {
        const string optionID = "portCount";

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

            context.AddInputPort<AudioClip>(SimpleDialogueNode.SoundPortName)
                .WithDisplayName("Play Sound")
                .WithTooltip("Sound to play when this node is shown.")
                .Build();

            var option = GetNodeOptionByName(optionID);
            option.TryGetValue(out int portCount);
            for (int i = 0; i < portCount; i++)
            {
                context.AddInputPort<string>($"Choice Text {i}").Build();
                context.AddOutputPort($"Choice {i}").WithCapacity(PortCapacity.Single).Build();
            }
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

            context.AddOption(optionID, typeof(int)).Delayed().WithDefaultValue(2).Build();
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
