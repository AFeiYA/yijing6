using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Yijing.Infrastructure;

namespace Yijing.Editor
{
    public static class ArtImporter
    {
        public const string CatalogPath = "Assets/Yijing/Data/ArtCatalog.asset";
        private const string Root = "Assets/Yijing/Art/";

        [MenuItem("Yijing/Art/Import and Validate")]
        public static void ImportAndValidate()
        {
            AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ProjectSetup.ConfigAssetPath);
            if (config == null) throw new InvalidDataException("Import the prototype configuration first.");
            var definitions = config.CreateSnapshot().items;

            var room = Import(Root + "Sanctuary/sanctuary_base_v1.png", false, 2048);
            var portrait = Import(Root + "Characters/elena_portrait_v1.png", true, 1024);
            var items = definitions.Select(item => new ItemArt
            {
                itemId = item.id,
                sprite = Import(Root + "Board/Items/" + item.id + "_v1.png", true, 512)
            }).ToArray();
            var cards = new[] { "mist", "water", "bamboo" }.Select(name =>
                Import(Root + "Cards/oracle_" + name + "_v1.png", false, 2048)).ToArray();

            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArtCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.sanctuaryBase = room;
            catalog.elenaPortrait = portrait;
            catalog.items = items;
            catalog.oracleBackgrounds = cards;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Validate();
        }

        [MenuItem("Yijing/Art/Validate Catalog")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ArtCatalog>(CatalogPath);
            if (catalog == null || catalog.sanctuaryBase == null || catalog.elenaPortrait == null)
                throw new InvalidDataException("Missing sanctuary or character sprite.");
            if (catalog.oracleBackgrounds == null || catalog.oracleBackgrounds.Length != 3 ||
                catalog.oracleBackgrounds.Any(x => x == null))
                throw new InvalidDataException("Expected three shared oracle backgrounds.");
            var config = AssetDatabase.LoadAssetAtPath<PrototypeConfigAsset>(ProjectSetup.ConfigAssetPath);
            var ids = config.CreateSnapshot().items.Select(x => x.id).ToArray();
            if (catalog.items == null || catalog.items.Length != ids.Length ||
                catalog.items.Any(x => x == null || x.sprite == null) ||
                catalog.items.Select(x => x.itemId).Distinct().Count() != ids.Length ||
                ids.Any(id => catalog.FindItem(id) == null))
                throw new InvalidDataException("Item sprites must exactly cover the configured stable IDs.");
            Debug.Log("Yijing art: 10 item sprites, 1 sanctuary, 1 portrait and 3 oracle backgrounds validated.");
        }

        private static Sprite Import(string path, bool transparent, int maxSize)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Missing generated art asset", path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidDataException("Not a texture: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = transparent;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) throw new InvalidDataException("Sprite import failed: " + path + "; objects: " + string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(path).Select(x => x.GetType().FullName + ":" + x.name)));
            return sprite;
        }
    }
}
