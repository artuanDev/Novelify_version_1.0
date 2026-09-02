using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;
using UnityEngine.UI;
using System.ComponentModel;

namespace Novelify.Editor
{
    /*Nodes CAN´T be accesed by normal monobehaviours, we instead need
    to make a runtime version of the nodes in the graph*/

    [Serializable]
    public class StartNode: Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddOutputPort("out").Build();
        }
    }

    [Serializable]
    public class EndNode: Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in").Build();
        }
    }

    [Serializable]
    public class DialogueNode: Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in").Build();
            context.AddOutputPort("out").Build();

            context.AddInputPort<string>("Speaker").Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption("Dialogue", typeof(string))
                .AsTextArea(3, 10)
                .WithDefaultValue("")
                .Build();
        }
    }

    [Serializable]
    public class ChoiceNode: Node
    {
        const string optionID = "portCount";
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in").Build();

            context.AddInputPort<string>("Speaker").Build();

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
            context.AddOption("Dialogue", typeof(string))
                .AsTextArea(3, 10)
                .WithDefaultValue("")
                .Build();

            context.AddOption(optionID, typeof(int)).Delayed().WithDefaultValue(2).Build();
        }
    }
}
