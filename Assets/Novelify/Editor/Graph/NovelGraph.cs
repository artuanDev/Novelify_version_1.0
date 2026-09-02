using UnityEngine;
using Unity.GraphToolkit.Editor;
using UnityEditor;
using System;

namespace Novelify.Editor
{
    [Serializable]
    [Graph(AssetExtension)]
    public class NovelGraph : Graph
    {
        public const string AssetExtension = "novelgraph";
        [MenuItem("Assets/Create/Novelify/Novel Graph", false)]
        private static void CreateAssetFile()
        {
            GraphDatabase.PromptInProjectBrowserToCreateNewAsset<NovelGraph>("NovelGraph");
        }
    }
}
