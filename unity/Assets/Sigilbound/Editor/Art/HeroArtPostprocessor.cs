// Import settings for hero art dropped under Assets/Sigilbound/Art (ART_BIBLE.md, sections 5-7),
// applied automatically by Unity whenever such a file is imported or re-imported:
//   SB_Hero_<id>_LOD<n>.fbx   Humanoid rig (avatar from this model), file scale, no cameras/lights,
//                              4 bone weights, mesh compression, materials replaced by the pipeline
//   SB_Anim_*.fbx             Humanoid clip renamed to its battle state, in place (root locked),
//                              looping per Core.HeroClips: the shared, retargeted animation library
//   T_Hero_<id>_<map>.*       albedo sRGB, normal map, linear mask; 2048 max, 1024 ASTC on Android
// .glb files are imported by glTFast's importer (no Humanoid setting there); the pipeline builds
// their avatar itself from Core.HumanoidMap.
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.EditorTools
{
    public sealed class HeroArtPostprocessor : AssetPostprocessor
    {
        public const string ArtRoot = "Assets/Sigilbound/Art";
        static bool Ours(string path) => path.Replace('\\', '/').StartsWith(ArtRoot + "/", StringComparison.Ordinal);

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            var p = ArtSpec.Parse(Path.GetFileName(assetPath));
            if (mi == null || p == null) return;
            mi.importCameras = false;
            mi.importLights = false;
            mi.useFileScale = true;
            mi.globalScale = 1f;
            mi.isReadable = false;
            mi.optimizeGameObjects = false;      // sockets attach to bone transforms
            mi.skinWeights = ModelImporterSkinWeights.Standard;
            if (p.Kind == "hero" || p.Kind == "gear")
            {
                mi.meshCompression = ModelImporterMeshCompression.Medium;
                mi.importBlendShapes = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; // read by the pipeline, then replaced
            }
            if (p.Kind == "hero")
            {
                // Keep a Humanoid avatar mapping the pipeline may have stored (userData "sb-humanoid")
                // for rigs Unity's auto-mapper misreads; otherwise let Unity map it.
                if (mi.animationType != ModelImporterAnimationType.Human) mi.animationType = ModelImporterAnimationType.Human;
                if (mi.userData != "sb-humanoid") mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;       // embedded clips are a fallback source
            }
            else if (p.Kind == "anim")
            {
                mi.animationType = ModelImporterAnimationType.Human;
                mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                mi.importAnimation = true;
                mi.materialImportMode = ModelImporterMaterialImportMode.None;
            }
            else if (p.Kind == "gear") mi.animationType = ModelImporterAnimationType.Generic;
        }

        void OnPreprocessAnimation()
        {
            if (!Ours(assetPath)) return;
            var mi = assetImporter as ModelImporter;
            var p = ArtSpec.Parse(Path.GetFileName(assetPath));
            if (mi == null || p == null || p.Kind != "anim" || !HeroClips.ByName.TryGetValue(p.Clip, out var spec)) return;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            // One clip per file: the longest take (Mixamo-style exports sometimes carry a short T-pose take).
            var c = clips[0];
            foreach (var k in clips) if (k.lastFrame - k.firstFrame > c.lastFrame - c.firstFrame) c = k;
            c.name = p.Clip;
            c.loopTime = spec.Loop;
            c.loopPose = spec.Loop;
            // In place: the battle code moves the unit, the clip only animates the body.
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.keepOriginalPositionY = true;
            c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
            mi.clipAnimations = new[] { c };
        }

        void OnPreprocessTexture()
        {
            if (!Ours(assetPath)) return;
            var ti = assetImporter as TextureImporter;
            var p = ArtSpec.Parse(Path.GetFileName(assetPath));
            if (ti == null || p == null || p.Kind != "texture") return;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = ArtBudget.TextureMax;
            switch (p.Map)
            {
                case "Normal": ti.textureType = TextureImporterType.NormalMap; break;
                case "Mask": ti.textureType = TextureImporterType.Default; ti.sRGBTexture = false; break;
                default: ti.textureType = TextureImporterType.Default; ti.sRGBTexture = true; break;
            }
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = ArtBudget.TextureShipped,
                format = p.Map == "Normal" ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6,
            });
        }
    }
}
