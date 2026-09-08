using System;
using System.Linq;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;

namespace Novelify.Editor
{
    /// <summary>Creates the editable sample that documents the state/conditional-flow feature.</summary>
    [InitializeOnLoad]
    internal static class NovelStateDemoBuilder
    {
        internal const string SampleFolder = "Assets/Novelify/Samples/StateAndConditionalFlow";
        internal const string GraphPath = SampleFolder + "/StateAndConditionalFlowDemo.novelgraph";

        static NovelStateDemoBuilder() => EditorApplication.delayCall += EnsureSample;

        [MenuItem("Tools/Novelify/Samples/Create State & Conditional Flow Demo")]
        private static void CreateFromMenu()
        {
            BuildSample();
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(GraphPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
        }

        private static void EnsureSample()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += EnsureSample;
                return;
            }
            BuildSample();
        }

        // Public so CI or a batch-mode Unity editor can materialize the sample.
        public static void BuildSample()
        {
            if (AssetDatabase.LoadMainAssetAtPath(GraphPath) != null) return;

            EnsureFolder(SampleFolder + "/Variables");
            NovelVariableDefinition playerName = CreateVariable("Player Name", NovelVariableType.String, NovelVariableScope.Profile, "PlayerName");
            NovelVariableDefinition trust = CreateVariable("Trust", NovelVariableType.Integer, NovelVariableScope.Story, "Trust");
            NovelVariableDefinition rapport = CreateVariable("Rapport", NovelVariableType.Float, NovelVariableScope.Story, "Rapport");
            NovelVariableDefinition helped = CreateVariable("Helped Before", NovelVariableType.Boolean, NovelVariableScope.Story, "HelpedBefore");
            CreateVariable("Temporary Count", NovelVariableType.Integer, NovelVariableScope.CallLocal, "TemporaryCount");

            NovelGraph graph = GraphDatabase.CreateGraph<NovelGraph>(GraphPath);
            if (graph == null) return;
            graph.UndoBeginRecordGraph("Create state and conditional flow demo");
            try
            {
                var start = Add<StartNode>(graph, 0, 100);
                SetNovelVariableNode setName = AddSet(graph, playerName, NovelVariableType.String, "Reader", 250, 0);
                SetNovelVariableNode setTrust = AddSet(graph, trust, NovelVariableType.Integer, 2, 500, 0);
                SetNovelVariableNode setRapport = AddSet(graph, rapport, NovelVariableType.Float, 0.6f, 750, 0);
                SetNovelVariableNode setHelped = AddSet(graph, helped, NovelVariableType.Boolean, false, 1000, 0);
                var branch = Add<BranchNovelNode>(graph, 1500, 100);

                ConnectFlow(graph, start, setName);
                ConnectFlow(graph, setName, setTrust);
                ConnectFlow(graph, setTrust, setRapport);
                ConnectFlow(graph, setRapport, setHelped);
                ConnectFlow(graph, setHelped, branch);

                GetNovelVariableNode getTrust = AddGet(graph, trust, NovelVariableType.Integer, 250, 500);
                CompareNovelValuesNode trustCheck = AddCompare(graph, NovelVariableType.Integer,
                    RuntimeComparisonOperation.GreaterOrEqual, 500, 500);
                trustCheck.GetInputPortByName("B").TrySetValue(2);
                ConnectValue(graph, getTrust, "Value", trustCheck, "A");

                GetNovelVariableNode getRapport = AddGet(graph, rapport, NovelVariableType.Float, 250, 700);
                CompareNovelValuesNode rapportCheck = AddCompare(graph, NovelVariableType.Float,
                    RuntimeComparisonOperation.GreaterOrEqual, 500, 700);
                rapportCheck.GetInputPortByName("B").TrySetValue(0.5f);
                ConnectValue(graph, getRapport, "Value", rapportCheck, "A");

                var numericAnd = Add<AndNovelValuesNode>(graph, 800, 600);
                ConnectValue(graph, trustCheck, "Result", numericAnd, "A");
                ConnectValue(graph, rapportCheck, "Result", numericAnd, "B");

                GetNovelVariableNode getHelped = AddGet(graph, helped, NovelVariableType.Boolean, 250, 950);
                var notHelped = Add<NotNovelValueNode>(graph, 500, 950);
                ConnectValue(graph, getHelped, "Value", notHelped, "Value");

                GetNovelVariableNode getName = AddGet(graph, playerName, NovelVariableType.String, 250, 1150);
                CompareNovelValuesNode nameCheck = AddCompare(graph, NovelVariableType.String,
                    RuntimeComparisonOperation.Equal, 500, 1150);
                nameCheck.GetInputPortByName("B").TrySetValue("Reader");
                ConnectValue(graph, getName, "Value", nameCheck, "A");

                var identityOr = Add<OrNovelValuesNode>(graph, 800, 1050);
                ConnectValue(graph, notHelped, "Result", identityOr, "A");
                ConnectValue(graph, nameCheck, "Result", identityOr, "B");

                var finalAnd = Add<AndNovelValuesNode>(graph, 1150, 800);
                ConnectValue(graph, numericAnd, "Result", finalAnd, "A");
                ConnectValue(graph, identityOr, "Result", finalAnd, "B");
                ConnectValue(graph, finalAnd, "Result", branch, "Condition");

                ModifyNovelVariableNode addTrust = AddModify(graph, trust, NovelNumericType.Integer,
                    RuntimeVariableModifyOperation.Add, 1, 1800, 0);
                ModifyNovelVariableNode addRapport = AddModify(graph, rapport, NovelNumericType.Float,
                    RuntimeVariableModifyOperation.Add, 0.2f, 2050, 0);
                DialogueNode success = AddDialogue(graph,
                    "All checks passed. Trust and rapport were increased.", 2300, 0);
                var successEnd = Add<EndNode>(graph, 2600, 0);
                ConnectNamedFlow(graph, branch, "True", addTrust);
                ConnectFlow(graph, addTrust, addRapport);
                ConnectFlow(graph, addRapport, success);
                ConnectFlow(graph, success, successEnd);

                ModifyNovelVariableNode subtractTrust = AddModify(graph, trust, NovelNumericType.Integer,
                    RuntimeVariableModifyOperation.Subtract, 1, 1800, 300);
                DialogueNode fallback = AddDialogue(graph,
                    "A check failed. Trust was reduced and the false branch ran.", 2050, 300);
                var fallbackEnd = Add<EndNode>(graph, 2350, 300);
                ConnectNamedFlow(graph, branch, "False", subtractTrust);
                ConnectFlow(graph, subtractTrust, fallback);
                ConnectFlow(graph, fallback, fallbackEnd);
            }
            finally
            {
                graph.UndoEndRecordGraph();
            }

            GraphDatabase.SaveGraph(graph);
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log($"Novelify created the state and conditional-flow demo at {GraphPath}");
        }

        public static void ValidateSampleForAutomation()
        {
            BuildSample();
            AssetDatabase.ImportAsset(GraphPath, ImportAssetOptions.ForceUpdate);
            RuntimeNovelGraph runtime = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(GraphPath);
            if (runtime == null) throw new InvalidOperationException("The state demo did not import as a RuntimeNovelGraph.");
            if (runtime.AllNodes.Count(node => node is RuntimeSetVariableNode) != 4)
                throw new InvalidOperationException("The state demo must contain four Set Variable nodes.");
            if (runtime.AllNodes.Count(node => node is RuntimeModifyVariableNode) != 3)
                throw new InvalidOperationException("The state demo must contain three Modify Variable nodes.");
            RuntimeBranchNode branch = runtime.AllNodes.OfType<RuntimeBranchNode>().SingleOrDefault();
            if (branch?.Condition is not RuntimeBooleanExpression ||
                string.IsNullOrEmpty(branch.TrueNodeID) || string.IsNullOrEmpty(branch.FalseNodeID))
                throw new InvalidOperationException("The state demo branch or its compiled Boolean expression is incomplete.");
            Debug.Log($"Novelify validated the state and conditional-flow demo ({runtime.AllNodes.Count} runtime nodes).");
        }

        private static T Add<T>(NovelGraph graph, float x, float y) where T : Node, new()
        {
            var node = new T { Position = new Vector2(x, y) };
            graph.AddNode(node);
            return node;
        }

        private static SetNovelVariableNode AddSet(NovelGraph graph, NovelVariableDefinition variable,
            NovelVariableType type, object value, float x, float y)
        {
            SetNovelVariableNode node = Add<SetNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            node.GetInputPortByName("Value").TrySetValue(value);
            return node;
        }

        private static GetNovelVariableNode AddGet(NovelGraph graph, NovelVariableDefinition variable,
            NovelVariableType type, float x, float y)
        {
            GetNovelVariableNode node = Add<GetNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            return node;
        }

        private static CompareNovelValuesNode AddCompare(NovelGraph graph, NovelVariableType type,
            RuntimeComparisonOperation operation, float x, float y)
        {
            CompareNovelValuesNode node = Add<CompareNovelValuesNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetNodeOptionByName("Operator").TrySetValue(operation);
            return node;
        }

        private static ModifyNovelVariableNode AddModify(NovelGraph graph, NovelVariableDefinition variable,
            NovelNumericType type, RuntimeVariableModifyOperation operation, object amount, float x, float y)
        {
            ModifyNovelVariableNode node = Add<ModifyNovelVariableNode>(graph, x, y);
            node.GetNodeOptionByName("Value Type").TrySetValue(type);
            node.GetNodeOptionByName("Operation").TrySetValue(operation);
            node.GetInputPortByName("Variable").TrySetValue(variable);
            node.GetInputPortByName("Amount").TrySetValue(amount);
            return node;
        }

        private static DialogueNode AddDialogue(NovelGraph graph, string text, float x, float y)
        {
            DialogueNode node = Add<DialogueNode>(graph, x, y);
            node.GetNodeOptionByName("Dialogue").TrySetValue(new RichDialogueText(text));
            return node;
        }

        private static void ConnectFlow(NovelGraph graph, Node from, Node to) =>
            ConnectNamedFlow(graph, from, "out", to);

        private static void ConnectNamedFlow(NovelGraph graph, Node from, string output, Node to) =>
            graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName("in"));

        private static void ConnectValue(NovelGraph graph, Node from, string output, Node to, string input) =>
            graph.Connect(from.GetOutputPortByName(output), to.GetInputPortByName(input));

        private static NovelVariableDefinition CreateVariable(string displayName, NovelVariableType type,
            NovelVariableScope scope, string fileName)
        {
            string path = $"{SampleFolder}/Variables/{fileName}.asset";
            NovelVariableDefinition existing = AssetDatabase.LoadAssetAtPath<NovelVariableDefinition>(path);
            if (existing != null) return existing;
            NovelVariableDefinition variable = ScriptableObject.CreateInstance<NovelVariableDefinition>();
            variable.name = displayName;
            variable.DisplayName = displayName;
            variable.Type = type;
            variable.Scope = scope;
            variable.EnsureID();
            AssetDatabase.CreateAsset(variable, path);
            return variable;
        }

        private static void EnsureFolder(string path)
        {
            string current = "Assets";
            string[] parts = path.Split('/');
            for (int i = 1; i < parts.Length; ++i)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
