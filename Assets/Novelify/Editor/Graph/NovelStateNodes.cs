using System;
using Unity.GraphToolkit.Editor;
using UnityEngine;

namespace Novelify.Editor
{
    public enum NovelNumericType { Integer, Float }

    internal static class NovelStatePortDefinition
    {
        public static NovelVariableType GetValueType(INode node)
        {
            INodeOption option = node.GetNodeOptionByName("Value Type");
            return option != null && option.TryGetValue(out NovelVariableType type)
                ? type
                : NovelVariableType.Boolean;
        }

        public static NovelNumericType GetNumericType(INode node)
        {
            INodeOption option = node.GetNodeOptionByName("Value Type");
            return option != null && option.TryGetValue(out NovelNumericType type)
                ? type
                : NovelNumericType.Integer;
        }

    }

    [Serializable, Node("Novelify/State", null, "Get Variable"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class GetNovelVariableNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<NovelVariableDefinition>("Variable").Build();
            switch (NovelStatePortDefinition.GetValueType(this))
            {
                case NovelVariableType.Boolean: context.AddOutputPort<bool>("Value").Build(); break;
                case NovelVariableType.Integer: context.AddOutputPort<int>("Value").Build(); break;
                case NovelVariableType.Float: context.AddOutputPort<float>("Value").Build(); break;
                case NovelVariableType.String: context.AddOutputPort<string>("Value").Build(); break;
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) =>
            context.AddOption<NovelVariableType>("Value Type").WithDefaultValue(NovelVariableType.Boolean)
                .WithTooltip("Must match the selected variable definition.").Build();
    }

    [Serializable, Node("Novelify/State", null, "Set Variable"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class SetNovelVariableNode : ActionNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<NovelVariableDefinition>("Variable").Build();
            switch (NovelStatePortDefinition.GetValueType(this))
            {
                case NovelVariableType.Boolean: context.AddInputPort<bool>("Value").WithDefaultValue(false).Build(); break;
                case NovelVariableType.Integer: context.AddInputPort<int>("Value").WithDefaultValue(0).Build(); break;
                case NovelVariableType.Float: context.AddInputPort<float>("Value").WithDefaultValue(0f).Build(); break;
                case NovelVariableType.String: context.AddInputPort<string>("Value").WithDefaultValue(string.Empty).Build(); break;
            }
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context) =>
            context.AddOption<NovelVariableType>("Value Type").WithDefaultValue(NovelVariableType.Boolean)
                .WithTooltip("Must match the selected variable definition.").Build();
    }

    [Serializable, Node("Novelify/State", null, "Modify Variable"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class ModifyNovelVariableNode : ActionNode
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            base.OnDefinePorts(context);
            context.AddInputPort<NovelVariableDefinition>("Variable").Build();
            if (NovelStatePortDefinition.GetNumericType(this) == NovelNumericType.Integer)
                context.AddInputPort<int>("Amount").WithDefaultValue(1).Build();
            else
                context.AddInputPort<float>("Amount").WithDefaultValue(1f).Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<NovelNumericType>("Value Type").WithDefaultValue(NovelNumericType.Integer)
                .WithTooltip("Must match an Integer or Float variable definition.").Build();
            context.AddOption<RuntimeVariableModifyOperation>("Operation")
                .WithDefaultValue(RuntimeVariableModifyOperation.Add).Build();
        }
    }

    [Serializable, Node("Novelify/Logic", null, "Compare"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class CompareNovelValuesNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            NovelVariableType type = NovelStatePortDefinition.GetValueType(this);
            switch (type)
            {
                case NovelVariableType.Boolean:
                    context.AddInputPort<bool>("A").WithDefaultValue(false).Build();
                    context.AddInputPort<bool>("B").WithDefaultValue(false).Build();
                    break;
                case NovelVariableType.Integer:
                    context.AddInputPort<int>("A").WithDefaultValue(0).Build();
                    context.AddInputPort<int>("B").WithDefaultValue(0).Build();
                    break;
                case NovelVariableType.Float:
                    context.AddInputPort<float>("A").WithDefaultValue(0f).Build();
                    context.AddInputPort<float>("B").WithDefaultValue(0f).Build();
                    break;
                case NovelVariableType.String:
                    context.AddInputPort<string>("A").WithDefaultValue(string.Empty).Build();
                    context.AddInputPort<string>("B").WithDefaultValue(string.Empty).Build();
                    break;
            }
            context.AddOutputPort<bool>("Result").Build();
        }

        protected override void OnDefineOptions(IOptionDefinitionContext context)
        {
            context.AddOption<NovelVariableType>("Value Type").WithDefaultValue(NovelVariableType.Boolean).Build();
            context.AddOption<RuntimeComparisonOperation>("Operator")
                .WithDefaultValue(RuntimeComparisonOperation.Equal).Build();
        }
    }

    [Serializable, Node("Novelify/Logic", null, "And"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class AndNovelValuesNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<bool>("A").WithDefaultValue(false).Build();
            context.AddInputPort<bool>("B").WithDefaultValue(false).Build();
            context.AddOutputPort<bool>("Result").Build();
        }
    }

    [Serializable, Node("Novelify/Logic", null, "Or"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class OrNovelValuesNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<bool>("A").WithDefaultValue(false).Build();
            context.AddInputPort<bool>("B").WithDefaultValue(false).Build();
            context.AddOutputPort<bool>("Result").Build();
        }
    }

    [Serializable, Node("Novelify/Logic", null, "Not"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class NotNovelValueNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort<bool>("Value").WithDefaultValue(false).Build();
            context.AddOutputPort<bool>("Result").Build();
        }
    }

    [Serializable, Node("Novelify/Flow", null, "Branch"), UseWithGraph(typeof(NovelGraph), typeof(NovelFunctionGraph))]
    public sealed class BranchNovelNode : Node
    {
        protected override void OnDefinePorts(IPortDefinitionContext context)
        {
            context.AddInputPort("in").WithDisplayName("Enter").Build();
            context.AddInputPort<bool>("Condition").WithDefaultValue(false).Build();
            context.AddOutputPort("True").WithCapacity(PortCapacity.Single).Build();
            context.AddOutputPort("False").WithCapacity(PortCapacity.Single).Build();
        }
    }
}
