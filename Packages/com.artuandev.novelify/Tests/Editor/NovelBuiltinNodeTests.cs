using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Novelify.Editor;
using NUnit.Framework;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Novelify.Tests
{
    public class NovelBuiltinNodeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private string _folder;
        private NovelGraph _graph;
        private NovelCharacter _character;
        private NovelVariableDefinition _variable;

        public static IEnumerable<Type> NodeTypes => typeof(NovelGraph).Assembly.GetTypes()
            .Where(type => type.IsPublic && !type.IsAbstract && typeof(Node).IsAssignableFrom(type))
            .OrderBy(type => type.Name);

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/NovelifyNodeAudit_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring(7));
            _character = ScriptableObject.CreateInstance<NovelCharacter>();
            AssetDatabase.CreateAsset(_character, _folder + "/Character.asset");
            _variable = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            AssetDatabase.CreateAsset(_variable, _folder + "/Variable.asset");
            _graph = GraphDatabase.CreateGraph<NovelGraph>(_folder + "/Story.novelgraph");
            _graph.UndoBeginRecordGraph("Audit built-in node");
        }

        [TearDown]
        public void TearDown()
        {
            _graph?.OnDisable();
            AssetDatabase.DeleteAsset(_folder);
        }

        private Node Add(Type type)
        {
            var node = (Node)Activator.CreateInstance(type);
            _graph.AddNode(node);
            return node;
        }

        [TestCaseSource(nameof(NodeTypes))]
        public void BuiltinNodeCompilesAndItsValueOutputsEvaluate(Type type)
        {
            Node node = Add(type);
            foreach (IPort input in node.GetInputPorts())
            {
                if (input.DataType == typeof(NovelCharacter)) input.TrySetValue(_character);
                if (input.DataType == typeof(NovelVariableDefinition))
                {
                    _variable.Type = node is ModifyNovelVariableNode ? NovelVariableType.Integer : NovelVariableType.Boolean;
                    input.TrySetValue(_variable);
                }
            }
            node.GetInputPortByName("Checkpoint ID")?.TrySetValue("node-audit");
            node.GetInputPortByName("Label")?.TrySetValue("node-audit");
            node.GetInputPortByName("Character Reference")?.TrySetValue(new NovelCharacterReference(_character, "audit"));
            node.GetInputPortByName("Instance ID")?.TrySetValue("audit");
            if (node is FloatBinaryNode)
            {
                node.GetInputPortByName("A").TrySetValue(8f);
                node.GetInputPortByName("B").TrySetValue(2f);
            }
            if (node is Vector2BinaryNode)
            {
                node.GetInputPortByName("A").TrySetValue(new Vector2(8f, 6f));
                node.GetInputPortByName("B").TrySetValue(new Vector2(2f, 3f));
            }

            bool valueNode = node is not StartNode && node.GetInputPortByName("in") == null;
            if (valueNode)
            {
                var importer = new NovelGraphImporter();
                var host = new GameObject("Node evaluator");
                try
                {
                    typeof(NovelGraphImporter).GetField("_editorGraph", Private).SetValue(importer, _graph);
                    MethodInfo compile = typeof(NovelGraphImporter).GetMethod("BuildExpression", Private,
                        null, new[] { typeof(IPort) }, null);
                    MethodInfo evaluate = typeof(NovelGraphRunner).GetMethod("Evaluate", Private);
                    NovelGraphRunner runner = host.AddComponent<NovelGraphRunner>();
                    foreach (IPort output in node.GetOutputPorts())
                    {
                        var expression = (RuntimeValueExpression)compile.Invoke(importer, new object[] { output });
                        Assert.That(expression, Is.Not.Null, output.Name);
                        Assert.That(expression, Is.Not.TypeOf<RuntimeConstantExpression>(), output.Name);
                        var result = (RuntimeValue)evaluate.Invoke(runner, new object[] { expression });
                        Assert.That(result.Kind, Is.Not.EqualTo(RuntimeValueKind.None), output.Name);
                        if (node is FloatBinaryNode)
                        {
                            float expected = node is SubtractFloatNode ? 6f : node is MultiplyFloatNode ? 16f :
                                node is DivideFloatNode ? 4f : 10f;
                            Assert.That(result.FloatValue, Is.EqualTo(expected));
                        }
                        if (node is Vector2BinaryNode)
                        {
                            Vector2 expected = node is SubtractVector2Node ? new Vector2(6f, 3f) :
                                node is MultiplyVector2Node ? new Vector2(16f, 18f) :
                                node is DivideVector2Node ? new Vector2(4f, 2f) : new Vector2(10f, 9f);
                            Assert.That(result.Vector2Value, Is.EqualTo(expected));
                        }
                    }
                }
                finally
                {
                    Object.DestroyImmediate(host);
                }
                _graph.UndoEndRecordGraph();
                return;
            }

            Node start = node is StartNode ? node : Add(typeof(StartNode));
            Node end = node is EndNode ? node : Add(typeof(EndNode));
            if (start != node)
                Assert.That(_graph.Connect(start.GetOutputPortByName("out"), node.GetInputPortByName("in")), Is.True);
            if (node is JumpNode)
            {
                Node label = Add(typeof(LabelNode));
                label.GetInputPortByName("Label").TrySetValue("node-audit");
                Assert.That(_graph.Connect(label.GetOutputPortByName("out"), end.GetInputPortByName("in")), Is.True);
            }
            else if (node is BranchNovelNode)
                Assert.That(_graph.Connect(node.GetOutputPortByName("True"), end.GetInputPortByName("in")), Is.True);
            else if (node is ChoiceNode)
                Assert.That(_graph.Connect(node.GetOutputPortByName("Choice 0"), end.GetInputPortByName("in")), Is.True);
            else if (node != end)
                Assert.That(_graph.Connect(node.GetOutputPortByName("out"), end.GetInputPortByName("in")), Is.True);

            _graph.UndoEndRecordGraph();
            GraphDatabase.SaveGraph(_graph);
            AssetDatabase.ImportAsset(_folder + "/Story.novelgraph", ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph runtime = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(_folder + "/Story.novelgraph");
            Assert.That(runtime, Is.Not.Null);
            if (node is StartNode)
                Assert.That(runtime.EntryNodeID, Is.EqualTo(end.ID.ToString()));
            else
            {
                RuntimeNode compiled = runtime.AllNodes.Single(candidate => candidate.NodeID == node.ID.ToString());
                if (node is not EndNode && node is not LabelNode && node is not JumpNode)
                    Assert.That(compiled, Is.Not.TypeOf<RuntimeNode>(), type.Name + " fell through to a no-op");
                if (node is JumpNode)
                    Assert.That(compiled.NextNodeID, Is.Not.Empty);
                else if (node is BranchNovelNode)
                    Assert.That(((RuntimeBranchNode)compiled).TrueNodeID, Is.EqualTo(end.ID.ToString()));
                else if (node is ChoiceNode)
                    Assert.That(((RuntimeChoiceNode)compiled).Choices[0].DestinationNodeID, Is.EqualTo(end.ID.ToString()));
                else if (node != end)
                    Assert.That(compiled.NextNodeID, Is.EqualTo(end.ID.ToString()));
            }
        }
    }
}
