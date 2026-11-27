// Import settings for the generated sounds in Assets/Resources/Audio (Tools~/audio/make_real.py):
//   amb_*.ogg   ambience beds: streamed from disk (never decoded whole into memory), Vorbis 0.35
//   sfx_*.ogg   one-shots: decompressed on load if short (instant start, no decode cost mid-battle);
//               the long Titan / cinematic / boss-lair ones stay compressed in memory. Vorbis 0.5.
//   music_*.ogg, battle_theme   music loops: streamed from disk like the beds, Vorbis 0.45; the short
//               victory sting (played as a one-shot over the music) stays compressed in memory.
// All keep their stereo (forceToMono off): positional panning and the wide beds depend on it.
using System;
using UnityEditor;
using UnityEngine;

namespace Sigilbound.EditorTools
{
    public sealed class AudioImportRules : AssetPostprocessor
    {
        const string Dir = "Assets/Resources/Audio/";
        public override uint GetVersion() => 3;   // bump to reimport with new rules

        void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(Dir, StringComparison.Ordinal)) return;
            var file = path.Substring(Dir.Length);
            bool amb = file.StartsWith("amb_", StringComparison.Ordinal), sfx = file.StartsWith("sfx_", StringComparison.Ordinal);
            bool music = file.StartsWith("music_", StringComparison.Ordinal) || file.StartsWith("battle_theme", StringComparison.Ordinal);
            bool sting = file.StartsWith("music_victory", StringComparison.Ordinal);
            if (!amb && !sfx && !music) return;
            var ai = assetImporter as AudioImporter;
            if (ai == null) return;
            bool longOne = file.StartsWith("sfx_titan_", StringComparison.Ordinal) || file.StartsWith("sfx_earthsplit", StringComparison.Ordinal) || file.StartsWith("sfx_meteor", StringComparison.Ordinal)
                || file.StartsWith("sfx_lair_", StringComparison.Ordinal);   // lair set-pieces: long rings and halls
            ai.forceToMono = false;
            ai.loadInBackground = amb || music;
            var s = ai.defaultSampleSettings;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.loadType = sting ? AudioClipLoadType.CompressedInMemory : amb || music ? AudioClipLoadType.Streaming
                : longOne ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.quality = amb ? 0.35f : music ? 0.45f : 0.5f;
            ai.defaultSampleSettings = s;
        }
    }
}
