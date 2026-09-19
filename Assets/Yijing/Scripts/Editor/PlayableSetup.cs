using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Yijing.Infrastructure;
using Yijing.Presentation;

namespace Yijing.Editor
{
    public static class PlayableSetup
    {
        [MenuItem("Yijing/Playable/Prepare Scene")]
        public static void PrepareScene()
        {
            if (!UnityEngine.Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ProjectSetup.ValidateConfiguration();
            var scene = EditorSceneManager.OpenScene(ProjectSetup.GamePath);
            var presenter = UnityEngine.Object.FindFirstObjectByType<GamePresenter>();
            if (presenter == null) presenter = new GameObject("Tea Room Game").AddComponent<GamePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("configuration").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ProjectSetup.ConfigAssetPath);
            serialized.FindProperty("art").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ArtCatalog>("Assets/Yijing/Data/ArtCatalog.asset");
            serialized.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Yijing/Fonts/NotoSansSC-Regular.otf");
            serialized.FindProperty("storyContent").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Yijing/Data/FirstSessionStory.json");
            foreach (var name in new[] { "configuration", "art", "font", "storyContent" })
                if (serialized.FindProperty(name).objectReferenceValue == null) throw new InvalidOperationException("Missing playable dependency: " + name);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Yijing playable tea loop prepared: open Bootstrap or Game and press Play.");
        }

        public static void PrepareStoryUpdate() { ProjectSetup.ImportConfiguration(); PrepareScene(); }

        [MenuItem("Yijing/Playable/Build macOS Preview")]
        public static void BuildMacPreview()
        {
            var output = Path.GetFullPath("Artifacts/Build/Yijing.app");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ProjectSetup.BootstrapPath, ProjectSetup.GamePath },
                locationPathName = output, target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("macOS preview build failed: " + report.summary.result);
            Debug.Log("Yijing macOS preview built: " + output);
        }
    }
}
