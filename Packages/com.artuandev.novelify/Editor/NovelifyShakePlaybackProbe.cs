using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Novelify;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class NovelifyShakePlaybackProbe
{
    private static double started, nextAdvance;
    private static int samples;
    private static bool sawShake;
    private static Vector3 cameraOrigin;
    private static Camera camera;
    private static readonly List<(RectTransform rect, Vector2 origin)> origins = new List<(RectTransform, Vector2)>();
    private static readonly StringBuilder history = new StringBuilder();
    private static string previous;
    static NovelifyShakePlaybackProbe() { EditorApplication.update += Update; }
    private static object Field(object target, string name) => target?.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
    private static string PathFor(string file) => Path.Combine(Application.dataPath, "../Library/" + file);
    private static void Finish(string result)
    {
        SessionState.SetBool("NovelifyShakePlayback", false);
        File.WriteAllText(PathFor("NovelifyShakePlayback.result"), result + Environment.NewLine + history);
        EditorApplication.isPlaying = false;
    }
    private static void Update()
    {
        if (File.Exists(PathFor("NovelifyShakePlayback.request")))
        {
            File.Delete(PathFor("NovelifyShakePlayback.request"));
            started = EditorApplication.timeSinceStartup;
            SessionState.SetBool("NovelifyShakePlayback", true);
            EditorApplication.isPlaying = true;
            return;
        }
        if (!SessionState.GetBool("NovelifyShakePlayback", false)) return;
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup - started > 45)
        {
            Finish("FAILED: normal playback did not finish the shake within 45 seconds.");
            return;
        }
        NovelGraphRunner runner = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<NovelGraphRunner>())
            if (AssetDatabase.GetAssetPath(candidate.RuntimeGraph) == "Assets/Novelify/Samples/NovelGraphs/Example.novelgraph")
                runner = candidate;
        if (runner == null || runner.CurrentNode == null) return;
        if (previous != runner.CurrentNode.NodeID)
        {
            previous = runner.CurrentNode.NodeID;
            history.AppendLine(runner.CurrentNode.GetType().Name + ":" + previous);
        }
        var presentation = runner.GetComponent<NovelGeneratedPresentation>();
        if (runner.CurrentNode is RuntimeScreenShakeNode && presentation != null &&
            Field(presentation, "_shakeCoroutine") != null)
        {
            camera = Field(presentation, "_shakeCamera") as Camera;
            if (!sawShake)
            {
                sawShake = true;
                cameraOrigin = (Vector3)Field(presentation, "_cameraShakeOrigin");
                if (Field(presentation, "_screenShakeTargets") is IEnumerable targets)
                    foreach (var target in targets)
                        origins.Add(((RectTransform)Field(target, "Rect"), (Vector2)Field(target, "Origin")));
                ScreenCapture.CaptureScreenshot(PathFor("NovelifyExampleScreenShake.png"));
            }
            bool cameraMoved = camera != null && Vector3.Distance(camera.transform.position, cameraOrigin) > 0.0001f;
            bool uiMoved = false;
            foreach (var origin in origins)
                if (origin.rect != null && Vector2.Distance(origin.rect.anchoredPosition, origin.origin) > 0.5f)
                    uiMoved = true;
            if (cameraMoved && uiMoved) samples++;
            return;
        }
        if (sawShake)
        {
            if (samples < 2) { Finish("FAILED: fewer than two moving camera/UI samples."); return; }
            if (camera == null || Vector3.Distance(camera.transform.position, cameraOrigin) > 0.0001f)
            { Finish("FAILED: camera was not restored."); return; }
            foreach (var origin in origins)
                if (origin.rect != null && Vector2.Distance(origin.rect.anchoredPosition, origin.origin) > 0.01f)
                { Finish("FAILED: UI position was not restored: " + origin.rect.name); return; }
            Finish("PASSED: normal Example graph playback reached Screen Shake, moved camera and UI in " +
                samples + " samples, and restored both.");
            return;
        }
        if (EditorApplication.timeSinceStartup < nextAdvance) return;
        nextAdvance = EditorApplication.timeSinceStartup + 0.15;
        if (runner.CurrentNode is RuntimeChoiceNode choice)
        {
            foreach (var option in choice.Choices)
                foreach (var node in runner.RuntimeGraph.AllNodes)
                    if (node.NodeID == option.DestinationNodeID && node is RuntimeAnimateCharacterNode animation &&
                        animation.Animation == NovelCharacterAnimation.Shake)
                    {
                        if (!runner.Session.TryChoose(option.ChoiceID, out string error))
                            Finish("FAILED: could not choose the shake branch: " + error);
                        else history.AppendLine("Selected branch: " + option.ChoiceText);
                        return;
                    }
            Finish("FAILED: no shake branch was found on the choice.");
        }
        else if (runner.CurrentNode is RuntimeDialogueNode) runner.Session.Advance();
    }
}