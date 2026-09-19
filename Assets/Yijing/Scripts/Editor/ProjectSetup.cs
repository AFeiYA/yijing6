using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Yijing.Domain.Configuration;
using Yijing.Infrastructure;

namespace Yijing.Editor
{
    public static class ProjectSetup
    {
        public const string ConfigAssetPath = "Assets/Yijing/Data/PrototypeConfig.asset";
        public const string BootstrapPath = "Assets/Yijing/Scenes/Bootstrap.unity";
        public const string GamePath = "Assets/Yijing/Scenes/Game.unity";
        public static string SourcePath => Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../docs/prototype_config.json"));

        [MenuItem("Yijing/Configuration/Import and Validate")]
        public static void ImportConfiguration()
        {
            var source = File.ReadAllText(SourcePath);
            var config = JsonUtility.FromJson<PrototypeConfig>(source);
            ConfigValidator.Validate(config);
            var asset = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ConfigAssetPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<PrototypeConfigAsset>();
                AssetDatabase.CreateAsset(asset, ConfigAssetPath);
            }
            asset.SetImportedData(config, SourceHash());
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log($"Imported {config.items.Length} items, {config.orders.Length} orders, {config.oracleCards.Length} oracle cards.");
        }

        [MenuItem("Yijing/Configuration/Validate Imported Data")]
        public static void ValidateConfiguration()
        {
            ConfigValidator.Validate(JsonUtility.FromJson<PrototypeConfig>(File.ReadAllText(SourcePath)));
            var asset = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ConfigAssetPath);
            if (asset == null || asset.SourceSha256 != SourceHash())
                throw new InvalidDataException("Configuration asset is stale. Run Yijing > Configuration > Import and Validate.");
            ConfigValidator.Validate(asset.CreateSnapshot());
            Debug.Log("Yijing configuration and imported snapshot are valid and synchronized.");
        }

        // Batch-only first-time preparation. Existing scenes are never overwritten.
        public static void InitializeProject()
        {
            ImportConfiguration();
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            PlayerSettings.companyName = "AFeiYA";
            PlayerSettings.productName = "Yijing";
            PlayerSettings.bundleVersion = "0.4.0";
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.defaultIsNativeResolution = false;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.afeiya.yijing");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.afeiya.yijing");

            if (!File.Exists(GamePath))
            {
                var game = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.tag = "MainCamera";
                cameraObject.transform.position = new Vector3(0, 0, -10);
                var camera = cameraObject.GetComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.17f, 0.24f, 0.22f);
                cameraObject.AddComponent<UniversalAdditionalCameraData>();
                var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                EditorSceneManager.SaveScene(game, GamePath);
            }
            if (!File.Exists(BootstrapPath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var bootstrap = new GameObject("Bootstrap").AddComponent<Yijing.Application.Bootstrap>();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("configuration").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ConfigAssetPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, BootstrapPath);
            }
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(BootstrapPath, true),
                new EditorBuildSettingsScene(GamePath, true)
            };
            EditorSceneManager.OpenScene(BootstrapPath);
            AssetDatabase.SaveAssets();
            ValidateConfiguration();
            Debug.Log("Yijing M0 project foundation initialized.");
        }

        public static string SourceHash()
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(SourcePath))).Replace("-", "").ToLowerInvariant();
        }
    }
}
