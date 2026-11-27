// Import rule for the owner's painted backdrops and icons (Resources/Skin/bg_*.jpg, Resources/Skin/icons/):
// UI textures, never mip-mapped. Backdrops cap at 2048 px with ASTC 6x6 on Android (~0.8 MB each);
// icons cap at 128 px.
using UnityEditor;

namespace Sigilbound.EditorTools
{
    public sealed class SkinImportRules : AssetPostprocessor
    {
        const int Version = 1;
        public override uint GetVersion() => Version;

        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            bool bg = p.Contains("/Resources/Skin/bg_"), icon = p.Contains("/Resources/Skin/icons/");
            if (!bg && !icon) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.mipmapEnabled = false;
            int max = bg ? 2048 : 128;
            ti.maxTextureSize = max;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Android", overridden = true, maxTextureSize = max, format = TextureImporterFormat.ASTC_6x6 });
        }
    }
}
