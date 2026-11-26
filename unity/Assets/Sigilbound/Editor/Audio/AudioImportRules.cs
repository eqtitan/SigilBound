// Import settings for the generated sounds in Assets/Resources/Audio (Tools~/audio/make_real.py):
//   amb_*.ogg   ambience beds: streamed from disk (never decoded whole into memory), Vorbis 0.35
//   sfx_*.ogg   one-shots: decompressed on load if short (instant start, no decode cost mid-battle);
//               the long Titan / cinematic ones stay compressed in memory. Vorbis 0.5.
// Both keep their stereo (forceToMono off): positional panning and the wide beds depend on it.
using System;
using UnityEditor;
using UnityEngine;

namespace Sigilbound.EditorTools
{
    public sealed class AudioImportRules : AssetPostprocessor
    {
        const string Dir = "Assets/Resources/Audio/";
        public override uint GetVersion() => 1;   // bump to reimport with new rules

        void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Dir, StringComparison.Ordinal)) return;
            var file = path.Substring(Dir.Length);
            bool amb = file.StartsWith("amb_", StringComparison.Ordinal), sfx = file.StartsWith("sfx_", StringComparison.Ordinal);
            if (!amb && !sfx) return;
            var ai = assetImporter as AudioImporter;
            if (ai == null) return;
            bool longOne = file.StartsWith("sfx_titan_", StringComparison.Ordinal) || file.StartsWith("sfx_earthsplit", StringComparison.Ordinal) || file.StartsWith("sfx_meteor", StringComparison.Ordinal);
            ai.forceToMono = false;
            ai.loadInBackground = amb;
            var s = ai.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.loadType = amb ? AudioClipLoadType.Streaming : longOne ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.quality = amb ? 0.35f : 0.5f;
            ai.defaultSampleSettings = s;
        }
    }
}
