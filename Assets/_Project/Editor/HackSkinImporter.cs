using UnityEditor;
using UnityEngine;

namespace Subnet.EditorTools
{
    /// <summary>Bullet art rendered in Blender (Resources/HackSkins/*.png) imports as crisp UI sprites.</summary>
    public class HackSkinImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/_Project/Resources/HackSkins";

        // PNGs that arrived before this script existed were imported as plain textures: fix them once.
        [InitializeOnLoadMethod]
        private static void FixOldImports()
        {
            EditorApplication.delayCall += () =>
            {
                if (!AssetDatabase.IsValidFolder(Folder)) return;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.textureType != TextureImporterType.Sprite)
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            };
        }

        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/HackSkins/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            bool tiled = assetPath.EndsWith("T.png");
            importer.wrapMode = tiled ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
