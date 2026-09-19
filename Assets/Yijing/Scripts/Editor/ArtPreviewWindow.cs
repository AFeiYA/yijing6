using UnityEditor;
using UnityEngine;
using Yijing.Infrastructure;

namespace Yijing.Editor
{
    public sealed class ArtPreviewWindow : EditorWindow
    {
        private Vector2 scroll;

        [MenuItem("Yijing/Art/Open Asset Preview")]
        private static void Open()
        {
            GetWindow<ArtPreviewWindow>("Yijing Art");
        }

        private void OnGUI()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(ArtImporter.CatalogPath);
            if (catalog == null)
            {
                EditorGUILayout.HelpBox("Run Yijing > Art > Import and Validate first.", MessageType.Info);
                return;
            }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("YIJING / ART LIBRARY 01", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Production source assets. Background is a flattened base; renovation and parallax overlays are not included yet.", MessageType.None);
            DrawSprite("Sanctuary", catalog.sanctuaryBase, 360);
            DrawSprite("Elena", catalog.elenaPortrait, 250);
            EditorGUILayout.LabelField("Merge items / stable config IDs", EditorStyles.boldLabel);
            var columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 32) / 132));
            for (var i = 0; i < catalog.items.Length; i += columns)
            {
                EditorGUILayout.BeginHorizontal();
                for (var j = i; j < Mathf.Min(i + columns, catalog.items.Length); j++)
                {
                    EditorGUILayout.BeginVertical(GUILayout.Width(124));
                    DrawSprite(catalog.items[j].itemId, catalog.items[j].sprite, 112);
                    EditorGUILayout.EndVertical();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("Shared oracle backgrounds / add text and hexagrams in UI", EditorStyles.boldLabel);
            foreach (var background in catalog.oracleBackgrounds)
                DrawSprite(background.name, background, 300);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawSprite(string title, Sprite sprite, float height)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            var rect = GUILayoutUtility.GetRect(80, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.23f, 0.22f));
            if (sprite != null)
                GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
        }
    }
}
