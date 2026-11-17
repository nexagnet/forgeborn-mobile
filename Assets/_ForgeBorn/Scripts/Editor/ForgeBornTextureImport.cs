using UnityEditor;
using UnityEngine;

namespace ForgeBorn.EditorTools
{
    /// <summary>
    /// Import rules for the CC0 art under Resources/Art: normal maps as normal maps, roughness linear, the workshop HDRI as a cubemap,
    /// 1024 px cap on mobile platforms (iOS/Android) to keep memory sane. Applied automatically on import.
    /// </summary>
    public class ForgeBornTextureImport : AssetPostprocessor
    {
        private const string ArtDir = "Assets/_ForgeBorn/Resources/Art/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ArtDir)) return;
            var ti = (TextureImporter)assetImporter;
            string file = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            if (file == "workshop")
            {
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.AutoCubemap;
                ti.sRGBTexture = false;
                ti.mipmapEnabled = true;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = 512;
                return;
            }

            if (file.EndsWith("_normal")) ti.textureType = TextureImporterType.NormalMap;
            else if (file.EndsWith("_rough")) ti.sRGBTexture = false;

            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.maxTextureSize = 2048;
            var mobile = new TextureImporterPlatformSettings { name = "iPhone", overridden = true, maxTextureSize = 1024, format = TextureImporterFormat.ASTC_6x6 };
            ti.SetPlatformTextureSettings(mobile);
            mobile.name = "Android";
            ti.SetPlatformTextureSettings(mobile);
        }
    }
}
