using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Novelify.Editor
{
    public sealed class NovelGraphCatalogBuilder : IPreprocessBuildWithReport
    {
        public const string CatalogPath = "Assets/Novelify/Resources/NovelGraphCatalog.asset";
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => RebuildCatalog();

        [InitializeOnLoadMethod]
        private static void EnsureDefaultCatalogExists()
        {
            EditorApplication.delayCall += CreateDefaultCatalogWhenReady;
        }

        private static void CreateDefaultCatalogWhenReady()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CreateDefaultCatalogWhenReady;
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(CatalogPath) == null)
                RebuildCatalog();
        }

        [MenuItem("Tools/Novelify/Rebuild Runtime Graph Catalog")]
        public static NovelGraphCatalog RebuildCatalog()
        {
            EnsureDefaultFolder();
            NovelGraphCatalog catalog = AssetDatabase.LoadAssetAtPath<NovelGraphCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<NovelGraphCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            return RebuildCatalog(catalog);
        }

        public static void EnsureDefaultFolder() => EnsureFolder("Assets/Novelify/Resources");

        public static NovelGraphCatalog RebuildCatalog(NovelGraphCatalog catalog)
        {
            if (catalog == null) return RebuildCatalog();

            var entries = new List<NovelGraphCatalog.Entry>();
            foreach (string path in AssetDatabase.GetAllAssetPaths()
                         .Where(path => path.EndsWith("." + NovelGraph.AssetExtension, StringComparison.OrdinalIgnoreCase) ||
                                        path.EndsWith("." + NovelFunctionGraph.AssetExtension, StringComparison.OrdinalIgnoreCase))
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                RuntimeNovelGraph graph = AssetDatabase.LoadAssetAtPath<RuntimeNovelGraph>(path);
                if (graph == null || string.IsNullOrEmpty(graph.GraphID)) continue;
                entries.Add(new NovelGraphCatalog.Entry { GraphID = graph.GraphID, Graph = graph });
            }

            var characters = AssetDatabase.FindAssets("t:NovelCharacter")
                .Select(guid => new NovelGraphCatalog.CharacterEntry
                {
                    CharacterID = guid,
                    Character = AssetDatabase.LoadAssetAtPath<NovelCharacter>(AssetDatabase.GUIDToAssetPath(guid))
                })
                .Where(entry => entry.Character != null)
                .OrderBy(entry => entry.CharacterID, StringComparer.Ordinal)
                .ToList();
            var variables = AssetDatabase.FindAssets("t:NovelVariableDefinition")
                .Select(guid => AssetDatabase.LoadAssetAtPath<NovelVariableDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(variable => variable != null && !string.IsNullOrEmpty(variable.ID))
                .OrderBy(variable => variable.ID, StringComparer.Ordinal)
                .Select(variable => new NovelGraphCatalog.VariableEntry { VariableID = variable.ID, Variable = variable })
                .ToList();

            catalog.ReplaceEntries(entries);
            catalog.ReplaceAssetEntries(characters, variables);
            catalog.GeneratedAtUtc = DateTime.UtcNow.ToString("O");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
