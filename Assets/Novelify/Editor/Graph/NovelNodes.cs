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
                .WithDisplayName("Begin")
                .WithTooltip("Connect to the first story node.")
                .Build();
        }
    }

    [Serializable]
    [Node("Novelify/Flow", "d_winbtn_win_close", "End",
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
    [Node("Novelify/Story", "d_console.infoicon", "Dialogue",
        "Assets/Novelify/Editor/Graph/Styles/DialogueNode.uss")]
    [UseWithGraph(typeof(NovelGraph))]
    public class DialogueNode: Node
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
            context.AddInputPort("in")
                .WithDisplayName("Enter")
                .Build();
            context.AddOutputPort("out")
                .WithDisplayName("Continue")
                .Build();

            context.AddInputPort<NovelCharacter>("Speaker")
                .WithTooltip("Character whose name, portrait, voice, and timing are used.")
                .Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption("Speaker Preview", typeof(SpeakerPortraitOption))
                .WithDefaultValue(new SpeakerPortraitOption())
                .Build();

            context.AddOption("Dialogue", typeof(string))
                .AsTextArea(3, 10)
                .WithDefaultValue("")
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

            var option = GetNodeOptionByName(optionID);
            option.TryGetValue(out int portCount);
            for (int i = 0; i < portCount; i++)
            {
                context.AddInputPort<string>($"Choice Text {i}").Build();
                context.AddOutputPort($"Choice {i}").Build();
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption("Speaker Preview", typeof(SpeakerPortraitOption))
                .WithDefaultValue(new SpeakerPortraitOption())
                .Build();

            context.AddOption("Dialogue", typeof(string))
                .AsTextArea(3, 10)
                .WithDefaultValue("")
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
