using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Novelify.Editor
{
    /// <summary>Installs editable sample content alongside the generated runtime catalog.</summary>
    public static class NovelSampleInstaller
    {
        public const string SamplesPath = "Assets/NovelifyGenerated/Samples";

        public static bool EnsureSamplesExist()
        {
            string destination = Path.Combine(Application.dataPath, "NovelifyGenerated/Samples");
            // A project owns its sample copies after installation. Keep edits and deletions on reload.
            if (Directory.Exists(destination)) return false;

            PackageInfo package = PackageInfo.FindForAssembly(typeof(NovelSampleInstaller).Assembly);
            if (package == null) return false;
            string source = Path.Combine(package.resolvedPath, "Samples~/Generated Samples");
            if (!Directory.Exists(source))
            {
                Debug.LogWarning("Novelify's packaged sample content could not be found at " + source);
                return false;
            }

            try
            {
                CopyDirectory(source, destination);
                // Copy everything before importing so graph and character references resolve together.
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureSceneInput();
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("Novelify could not install sample content: " + exception.Message);
                return false;
            }
        }

        private static void ConfigureSceneInput()
        {
            // Use the installed input backend without making Input System a package dependency.
            Type inputModuleType = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType == null) return;

            var scene = EditorSceneManager.OpenScene(SamplesPath + "/Scenes/TestScene.unity",
                OpenSceneMode.Additive);
            try
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                foreach (EventSystem eventSystem in root.GetComponentsInChildren<EventSystem>(true))
                {
                    foreach (StandaloneInputModule module in eventSystem.GetComponents<StandaloneInputModule>())
                        UnityEngine.Object.DestroyImmediate(module);
                    Component inputModule = eventSystem.gameObject.AddComponent(inputModuleType);
                    inputModuleType.GetMethod("AssignDefaultActions")?.Invoke(inputModule, null);
                }
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (string directory in Directory.GetDirectories(source))
                CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
