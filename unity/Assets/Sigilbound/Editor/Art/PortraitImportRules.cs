// Import rule for hero portraits (Resources/Portraits/portrait_<id>): small UI textures shown at
// 42 px (lists) up to 180x240 (a single summon card). Capped at 256 px, no mip maps, ASTC on Android, so all
// 151 heroes cost about 3 MB in the build instead of ~13 MB at the default settings.
using UnityEditor;

namespace Sigilbound.EditorTools
{
    public sealed class PortraitImportRules : AssetPostprocessor
    {
        const int Version = 1;
        public override uint GetVersion() => Version;

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Portraits/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 256;
            // Android: ASTC 6x6 at 192x256 is about 22 KB a portrait (~3 MB for all 151).
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = 256, format = TextureImporterFormat.ASTC_6x6 });
        }
    }
}
