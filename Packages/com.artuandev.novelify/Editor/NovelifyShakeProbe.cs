using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
internal static class NovelifyShakeProbe
{
    private static double next;
    static NovelifyShakeProbe()
    {
        EditorApplication.update += Dump;
        EditorApplication.delayCall += Dump;
    }
    private static void WireScreenShake()
    {
        const string assetPath = "Assets/Novelify/Samples/NovelGraphs/Example.novelgraph";
        var graph = Unity.GraphToolkit.Editor.GraphDatabase.LoadGraph<Novelify.Editor.NovelGraph>(assetPath);
        Novelify.Editor.ShakeCharacterNode animation = null;
        foreach (var node in graph.GetNodes())
        {
            if (node is Novelify.Editor.ScreenShakeNode) throw new InvalidOperationException("Screen Shake already exists.");
            if (node is Novelify.Editor.ShakeCharacterNode candidate) animation = candidate;
        }
        if (animation == null) throw new InvalidOperationException("The expected character shake branch is missing.");
        var output = animation.GetOutputPortByName("out");
        var destination = output.FirstConnectedPort;
        if (destination == null) throw new InvalidOperationException("The branch has no following dialogue.");
        graph.UndoBeginRecordGraph("Add Screen Shake to the shake branch");
        try
        {
            var shake = new Novelify.Editor.ScreenShakeNode();
            graph.AddNode(shake);
            shake.Position = animation.Position + new Vector2(300f, 0f);
            shake.GetInputPortByName("Duration").TrySetValue(0.6f);
            shake.GetInputPortByName("Amplitude").TrySetValue(24f);
            shake.GetNodeOptionByName("Wait For Completion").TrySetValue(true);
            if (!graph.Disconnect(output, destination) ||
                !graph.Connect(output, shake.GetInputPortByName("in")) ||
                !graph.Connect(shake.GetOutputPortByName("out"), destination))
                throw new InvalidOperationException("Could not wire Screen Shake between the animation and its dialogue.");
        }
        finally { graph.UndoEndRecordGraph(); }
        Unity.GraphToolkit.Editor.GraphDatabase.SaveGraph(graph);
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        File.WriteAllText(Path.Combine(Application.dataPath, "../Library/NovelifyWireScreenShake.result"), "Saved and imported Screen Shake on the existing shake branch.");
    }
    private static object Field(object target, string name) => target?.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(target);
    private static void Dump()
    {
        if (EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + 0.5;
        var request = Path.Combine(Application.dataPath, "../Library/NovelifyWireScreenShake.request");
        if (File.Exists(request))
        {
            File.Delete(request);
            WireScreenShake();
        }
        var output = new StringBuilder();
        output.AppendLine("playing=" + EditorApplication.isPlaying + " paused=" + EditorApplication.isPaused +
            " scale=" + Time.timeScale + " screen=" + Screen.width + "x" + Screen.height);
        for (int i = 0; i < SceneManager.sceneCount; i++) output.AppendLine("scene=" + SceneManager.GetSceneAt(i).path);
        foreach (var method in typeof(Unity.GraphToolkit.Editor.Graph).GetMethods())
            if (method.Name.Contains("connect") || method.Name.Contains("Remove"))
                output.AppendLine("graphAPI=" + method);
        var authoring = Unity.GraphToolkit.Editor.GraphDatabase.LoadGraph<Novelify.Editor.NovelGraph>(
            "Assets/Novelify/Samples/NovelGraphs/Example.novelgraph");
        foreach (var node in authoring.GetNodes())
        {
            output.AppendLine("authoring=" + node.GetType().Name + ":" + node.ID);
            if (node is Novelify.Editor.ScreenShakeNode)
            {
                node.GetInputPortByName("Duration").TryGetValue<float>(out var duration);
                node.GetInputPortByName("Amplitude").TryGetValue<float>(out var amplitude);
                output.AppendLine("authoringShake duration=" + duration + " amplitude=" + amplitude);
            }
        }
        foreach (var runner in UnityEngine.Object.FindObjectsByType<Novelify.NovelGraphRunner>(
            FindObjectsInactive.Include))
        {
            output.AppendLine("runner=" + runner.name + " enabled=" + runner.isActiveAndEnabled +
                " graph=" + AssetDatabase.GetAssetPath(runner.RuntimeGraph) + " running=" + runner.Session.IsRunning +
                " current=" + runner.CurrentNode?.GetType().Name + ":" + runner.CurrentNode?.NodeID +
                " waiting=" + runner.IsWaiting + " clock=" + runner.TimeMode);
            if (runner.RuntimeGraph != null)
                foreach (var node in runner.RuntimeGraph.AllNodes)
                {
                    output.Append("node=" + node.GetType().Name + ":" + node.NodeID + " next=" + node.NextNodeID);
                    if (node is Novelify.RuntimeScreenShakeNode shake)
                        output.Append(" duration=" + shake.Duration + " amplitude=" + shake.Amplitude +
                            " wait=" + shake.WaitForCompletion + " durationExpr=" + JsonUtility.ToJson(shake.DurationValue) +
                            " amplitudeExpr=" + JsonUtility.ToJson(shake.AmplitudeValue));
                    output.AppendLine();
                }
            var presentation = runner.GetComponent<Novelify.NovelGeneratedPresentation>();
            if (presentation == null) continue;
            output.AppendLine("presentationActive=" + presentation.isActiveAndEnabled +
                " shakeCoroutine=" + (Field(presentation, "_shakeCoroutine") != null) +
                " shakeCamera=" + Field(presentation, "_shakeCamera") +
                " cameraOrigin=" + Field(presentation, "_cameraShakeOrigin") +
                " cameraPosition=" + Field(presentation, "_cameraShakePosition"));
            if (Field(presentation, "_screenShakeTargets") is IEnumerable targets)
                foreach (var target in targets)
                {
                    var rect = Field(target, "Rect") as RectTransform;
                    output.AppendLine("target=" + rect?.name + " active=" + rect?.gameObject.activeInHierarchy +
                        " original=" + Field(target, "Origin") + " applied=" + Field(target, "Position") +
                        " actual=" + rect?.anchoredPosition);
                }
        }
        foreach (var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
            output.AppendLine("canvas=" + canvas.name + " active=" + canvas.isActiveAndEnabled +
                " mode=" + canvas.renderMode + " root=" + canvas.isRootCanvas + " camera=" + canvas.worldCamera +
                " display=" + canvas.targetDisplay + " scale=" + canvas.scaleFactor + " children=" + canvas.transform.childCount);
        var path = Path.Combine(Application.dataPath, "../Library/NovelifyShakeProbe.txt");
        File.WriteAllText(path, output.ToString());
    }
}