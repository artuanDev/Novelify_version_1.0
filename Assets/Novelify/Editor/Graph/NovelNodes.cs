using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;
using UnityEngine.UI;

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
}
