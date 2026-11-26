// The scenery prop kit: meshes made procedurally in Blender (Tools~/scenery/make_props.py) with
// baked ambient occlusion and a painterly shade term, stored compactly in Resources/Scenery/props.bytes.
// Each vertex carries (shade, material row) as its UV, so one per-theme ramp texture
// (SceneryPaint.Ramp) paints every prop; submesh 1, where present, is the glowing part.
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Sigilbound.Client
{
    public static class SceneryKit
    {
        // Material rows (make_props.py).
        public const int Bark = 0, LeafA = 1, LeafB = 2, LeafC = 3, Rock = 4, Moss = 5, Wood = 6, Stone = 7, Cloth = 8, Metal = 9, Cap = 10, Stem = 11, Reed = 12, Earth = 13, Trim = 14, Coal = 15;

        static Dictionary<string, Mesh> _meshes;
        static bool _tried;

        public static bool Ready { get { Load(); return _meshes != null && _meshes.Count > 0; } }

        public static Mesh Get(string name)
        {
            Load();
            if (_meshes != null && _meshes.TryGetValue(name, out var m)) return m;
            foreach (var pack in _packs.Values) if (pack != null && pack.TryGetValue(name, out m)) return m;
            return null;
        }

        // The story packs (Resources/Scenery/story_act1.bytes, story_act2.bytes: the campaign stops' set-pieces,
        // Tools~/scenery/make_landmarks.py), loaded the first time a chapter of that act is built.
        static readonly Dictionary<string, Dictionary<string, Mesh>> _packs = new Dictionary<string, Dictionary<string, Mesh>>();
        public static void EnsurePack(string pack)
        {
            if (_packs.ContainsKey(pack)) return;
            _packs[pack] = null;
            var ta = Resources.Load<TextAsset>("Scenery/" + pack);
            if (ta == null) { Debug.LogWarning($"[scenery] Resources/Scenery/{pack}.bytes missing"); return; }
            try { _packs[pack] = Parse(ta.bytes); }
            catch (System.Exception e) { Debug.LogWarning($"[scenery] {pack} failed to load: " + e.Message); }
            Resources.UnloadAsset(ta);
        }

        static void Load()
        {
            if (_tried) return;
            _tried = true;
            var ta = Resources.Load<TextAsset>("Scenery/props");
            if (ta == null) { Debug.LogWarning("[scenery] Resources/Scenery/props.bytes missing; using the simple props"); return; }
            try { _meshes = Parse(ta.bytes); }
            catch (System.Exception e) { Debug.LogWarning("[scenery] prop kit failed to load: " + e.Message); _meshes = null; }
            Resources.UnloadAsset(ta);
        }

        public static Dictionary<string, Mesh> Parse(byte[] bytes)
        {
            var d = new Dictionary<string, Mesh>();
            using var br = new BinaryReader(new MemoryStream(bytes));
            if (new string(br.ReadChars(4)) != "SBP1") throw new IOException("bad header");
            int count = br.ReadInt32();
            for (int m = 0; m < count; m++)
            {
                string name = new string(br.ReadChars(br.ReadByte()));
                int vc = br.ReadInt32(); int subs = br.ReadByte();
                var ic = new int[subs]; for (int s = 0; s < subs; s++) ic[s] = br.ReadInt32();
                var lo = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                var sz = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                var pos = new Vector3[vc]; var nrm = new Vector3[vc]; var uv = new Vector2[vc];
                for (int i = 0; i < vc; i++)
                {
                    float qx = (br.ReadInt16() + 32768) / 65535f, qy = (br.ReadInt16() + 32768) / 65535f, qz = (br.ReadInt16() + 32768) / 65535f;
                    pos[i] = new Vector3(lo.x + qx * sz.x, lo.y + qy * sz.y, lo.z + qz * sz.z);
                    nrm[i] = new Vector3(br.ReadSByte(), br.ReadSByte(), br.ReadSByte()).normalized;
                    float shade = br.ReadByte() / 255f; int row = br.ReadByte();
                    uv[i] = new Vector2(shade, (row + 0.5f) / SceneryPaint.Rows);
                }
                var mesh = new Mesh { name = name };
                mesh.vertices = pos; mesh.normals = nrm; mesh.uv = uv;
                mesh.subMeshCount = subs;
                for (int s = 0; s < subs; s++)
                {
                    var idx = new int[ic[s]];
                    for (int i = 0; i < idx.Length; i++) idx[i] = br.ReadUInt16();
                    mesh.SetTriangles(idx, s, false);
                }
                mesh.RecalculateBounds();
                d[name] = mesh;
            }
            return d;
        }

        // The kit's names that start with `prefix` (oak0, oak1 ...).
        public static List<string> Variants(string prefix)
        {
            Load();
            var l = new List<string>();
            if (_meshes != null) foreach (var k in _meshes.Keys) if (k.StartsWith(prefix)) l.Add(k);
            l.Sort();
            return l;
        }

        // A prop under root: one shared ramp material, plus the glow material on submesh 1.
        public static GameObject Place(Transform root, string name, Vector3 pos, float scale, float yaw, Material ramp, Material glow)
        {
            var mesh = Get(name);
            if (mesh == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos; go.transform.localScale = Vector3.one * scale; go.transform.localEulerAngles = new Vector3(0, yaw, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { ramp, glow } : new[] { ramp };
            return go;
        }
    }
}
