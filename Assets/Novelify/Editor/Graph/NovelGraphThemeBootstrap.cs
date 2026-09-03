using Unity.GraphToolkit.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Novelify.Editor
{
    /// <summary>
    /// Attaches the Novelify window-wide theme to Graph Toolkit windows that
    /// currently display a NovelGraph. Node-specific styles are attached by
    /// each node's NodeAttribute in NovelNodes.cs.
    /// </summary>
    [InitializeOnLoad]
    internal static class NovelGraphThemeBootstrap
    {
        private const string ThemePath =
            "Assets/Novelify/Editor/Graph/Styles/NovelGraphTheme.uss";
        private const double ScanIntervalSeconds = 0.75d;

        private static StyleSheet _theme;
        private static double _nextScanTime;

        static NovelGraphThemeBootstrap()
        {
            EditorApplication.update += ThemeOpenNovelGraphWindows;
            EditorApplication.delayCall += ThemeOpenNovelGraphWindows;
        }

        [MenuItem("Window/Novelify/Reapply Graph Theme", false, 2100)]
        private static void ReapplyTheme()
        {
            _theme = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath);

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (IsNovelGraphWindow(window))
                {
                    AttachTheme(window.rootVisualElement, true);
                }
            }
        }

        private static void ThemeOpenNovelGraphWindows()
        {
            if (EditorApplication.timeSinceStartup < _nextScanTime)
            {
                return;
            }

            _nextScanTime = EditorApplication.timeSinceStartup + ScanIntervalSeconds;
            _theme ??= AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemePath);
            if (_theme == null)
            {
                return;
            }

            foreach (EditorWindow window in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (IsNovelGraphWindow(window))
                {
                    AttachTheme(window.rootVisualElement, false);
                }
            }
        }

        private static bool IsNovelGraphWindow(EditorWindow window)
        {
            return window is IGraphWindow graphWindow && graphWindow.Graph is NovelGraph;
        }

        private static void AttachTheme(VisualElement root, bool forceRefresh)
        {
            root.AddToClassList("novelify-graph-window");

            if (forceRefresh && root.styleSheets.Contains(_theme))
            {
                root.styleSheets.Remove(_theme);
            }

            if (!root.styleSheets.Contains(_theme))
            {
                root.styleSheets.Add(_theme);
            }
        }
    }

    /// <summary>
    /// Gives story data ports distinct colors so connections are readable at a glance.
    /// </summary>
    [DataTypeStyleMapper(typeof(NovelGraph))]
    internal sealed class NovelGraphDataTypeStyles : DataTypeStyleMapper
    {
        public NovelGraphDataTypeStyles()
        {
            Register(typeof(NovelCharacter), null, new Color32(45, 212, 191, 255));
        }
    }
}
