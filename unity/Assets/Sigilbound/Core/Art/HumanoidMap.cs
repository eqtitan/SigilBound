// Maps an arbitrary character skeleton onto Unity's humanoid bones, so heroes from any source
// (AI generators' auto-rigs, Mixamo-style rigs, Blender/Rigify, UE-style packs, the CC0 Quaternius
// models) get a Humanoid avatar and share one retargeted animation library.
// Names alone are ambiguous ("Leg" is the shin on a Mixamo rig, "Spine" is the pelvis on Rigify),
// so the mapper finds limbs by name and the trunk by the hierarchy: the hips are where the legs
// meet, the chest is where the arms meet, and the spine is whatever lies between.
// Plain C#: the editor tool feeds it Transforms, the tests feed it GLB node lists.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sigilbound.Core
{
    public static class HumanoidMap
    {
        // Unity's HumanBodyBones names (HumanTrait.BoneName spelling) that an avatar must have.
        public static readonly string[] Required =
        {
            "Hips", "Spine", "Head",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "RightUpperLeg", "RightLowerLeg", "RightFoot",
            "LeftUpperArm", "LeftLowerArm", "LeftHand", "RightUpperArm", "RightLowerArm", "RightHand",
        };

        // Sideless limb names after normalising (lower case, letters and digits only).
        static readonly Dictionary<string, string> Limb = new Dictionary<string, string>
        {
            ["shoulder"] = "Shoulder", ["clavicle"] = "Shoulder", ["collar"] = "Shoulder", ["collarbone"] = "Shoulder",
            ["upperarm"] = "UpperArm", ["arm"] = "UpperArm", ["uparm"] = "UpperArm", ["armupper"] = "UpperArm", ["humerus"] = "UpperArm",
            ["lowerarm"] = "LowerArm", ["forearm"] = "LowerArm", ["lowarm"] = "LowerArm", ["armlower"] = "LowerArm", ["elbow"] = "LowerArm",
            ["hand"] = "Hand", ["wrist"] = "Hand",
            ["upperleg"] = "UpperLeg", ["upleg"] = "UpperLeg", ["thigh"] = "UpperLeg", ["legupper"] = "UpperLeg", ["femur"] = "UpperLeg",
            ["lowerleg"] = "LowerLeg", ["leg"] = "LowerLeg", ["calf"] = "LowerLeg", ["shin"] = "LowerLeg", ["knee"] = "LowerLeg", ["leglower"] = "LowerLeg", ["lowleg"] = "LowerLeg",
            ["foot"] = "Foot", ["ankle"] = "Foot",
            ["toe"] = "Toes", ["toes"] = "Toes", ["toebase"] = "Toes", ["ball"] = "Toes",
        };
        static readonly Dictionary<string, string> Finger = new Dictionary<string, string>
        {
            ["thumb"] = "Thumb", ["index"] = "Index", ["middle"] = "Middle", ["ring"] = "Ring", ["pinky"] = "Little", ["little"] = "Little", ["small"] = "Little",
        };
        static readonly string[] Phalanx = { "Proximal", "Intermediate", "Distal" };

        static readonly Regex Prefixes = new Regex(@"^(mixamorig\d*[:_]?|def[-_.]|org[-_.]|mch[-_.]|bip0?1?[ _]?|cc_base_|armature[|_:]|rig[_:])", RegexOptions.IgnoreCase);

        // Splits a raw node name into side ('L', 'R' or '\0') and a normalised sideless name.
        public static (char Side, string Core) Normalize(string raw)
        {
            string n = raw ?? "";
            int cut = Math.Max(n.LastIndexOf(':'), n.LastIndexOf('|'));
            if (cut >= 0) n = n.Substring(cut + 1);
            for (int i = 0; i < 3; i++) { var m = Prefixes.Match(n); if (!m.Success || m.Length == 0) break; n = n.Substring(m.Length); }
            char side = '\0';
            var s = Regex.Match(n, @"^(left|right)", RegexOptions.IgnoreCase);
            if (s.Success) { side = char.ToUpperInvariant(s.Value[0]); n = n.Substring(s.Length); }
            else if ((s = Regex.Match(n, @"(left|right)$", RegexOptions.IgnoreCase)).Success) { side = char.ToUpperInvariant(s.Value[0]); n = n.Substring(0, n.Length - s.Length); }
            else if ((s = Regex.Match(n, @"^[lr][._\- ]", RegexOptions.IgnoreCase)).Success) { side = char.ToUpperInvariant(n[0]); n = n.Substring(2); }
            else if ((s = Regex.Match(n, @"[._\- ][lr](?=$|[._\- ]?\d*$)", RegexOptions.IgnoreCase)).Success) { side = char.ToUpperInvariant(s.Value[1]); n = n.Remove(s.Index, 2); }
            n = Regex.Replace(n.ToLowerInvariant(), "[^a-z0-9]", "");
            return (side, n);
        }

        // nodes: (name, parent index or -1). Returns HumanBodyBones name -> node index.
        public static Dictionary<string, int> Map(IReadOnlyList<(string Name, int Parent)> nodes)
        {
            var map = new Dictionary<string, int>();
            var norm = nodes.Select(n => Normalize(n.Name)).ToArray();
            int Depth(int i) { int d = 0; while (i >= 0 && d < 512) { i = nodes[i].Parent; d++; } return d; }

            // Limbs by name: keep the shallowest match (twist/roll helpers sit deeper or share a name).
            for (int i = 0; i < nodes.Count; i++)
            {
                var (side, core) = norm[i];
                if (side == '\0') continue;
                string sideName = side == 'L' ? "Left" : "Right";
                string key = core.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
                if (Limb.TryGetValue(core, out var limb) || Limb.TryGetValue(key, out limb) && core.Length - key.Length <= 2 && !core.EndsWith("2") && !core.EndsWith("3"))
                {
                    if (core.Contains("twist") || core.Contains("roll")) continue;
                    string hb = sideName + limb;
                    if (!map.TryGetValue(hb, out var prev) || Depth(i) < Depth(prev)) map[hb] = i;
                    continue;
                }
                // Fingers: thumb1 / index_01 / handindex1 / findex01 ...
                var f = Regex.Match(core, @"^(?:hand|f)?(thumb|index|middle|ring|pinky|little|small)(?:finger)?0?([1-3])$");
                if (f.Success) map[$"{sideName}{Finger[f.Groups[1].Value]}{Phalanx[int.Parse(f.Groups[2].Value) - 1]}"] = i;
            }
            int head = -1;
            for (int i = 0; i < nodes.Count; i++) if (norm[i].Side == '\0' && (norm[i].Core == "head" || norm[i].Core == "head01")) { if (head < 0 || Depth(i) < Depth(head)) head = i; }

            int Lca(int a, int b)
            {
                var up = new HashSet<int>(); for (int x = a; x >= 0; x = nodes[x].Parent) up.Add(x);
                for (int y = b; y >= 0; y = nodes[y].Parent) if (up.Contains(y)) return y;
                return -1;
            }
            List<int> PathUp(int from, int to) { var p = new List<int>(); for (int x = from; x >= 0 && x != to; x = nodes[x].Parent) p.Add(x); p.Reverse(); return p; } // to (exclusive) .. from (inclusive)

            // Hips: where the legs meet; else a node named hips/pelvis.
            int hips = map.TryGetValue("LeftUpperLeg", out var lul) && map.TryGetValue("RightUpperLeg", out var rul) ? Lca(lul, rul) : -1;
            if (hips < 0) for (int i = 0; i < nodes.Count; i++) if (norm[i].Core == "hips" || norm[i].Core == "pelvis" || norm[i].Core == "hip") { hips = i; break; }
            if (hips >= 0) map["Hips"] = hips;

            // Chest: where the arms meet. Shoulders sit between it and the upper arms when named or not.
            int chest = map.TryGetValue("LeftUpperArm", out var lua) && map.TryGetValue("RightUpperArm", out var rua) ? Lca(lua, rua) : -1;
            if (chest >= 0)
            {
                foreach (var s in new[] { "Left", "Right" })
                    if (!map.ContainsKey(s + "Shoulder") && map.TryGetValue(s + "UpperArm", out var ua) && nodes[ua].Parent >= 0 && nodes[ua].Parent != chest) map[s + "Shoulder"] = nodes[ua].Parent;
                if (hips >= 0)
                {
                    var spine = PathUp(chest, hips); // hips (excl) .. chest (incl)
                    if (spine.Count >= 1 && !spine.Contains(hips)) map["Spine"] = spine[0];
                    if (spine.Count == 2) map["Chest"] = spine[1];
                    if (spine.Count >= 3) { map["Chest"] = spine[spine.Count - 2]; map["UpperChest"] = spine[spine.Count - 1]; }
                }
                if (head >= 0)
                {
                    var neck = PathUp(head, chest); // chest (excl) .. head (incl)
                    if (neck.Count >= 2) map["Neck"] = neck[0];
                }
            }
            if (head >= 0) map["Head"] = head;
            // Drop shoulders that are the chest itself (some rigs parent arms straight to the chest).
            foreach (var s in new[] { "LeftShoulder", "RightShoulder" }) if (map.TryGetValue(s, out var v) && (v == chest || v == hips)) map.Remove(s);
            return map;
        }

        public static List<string> Missing(Dictionary<string, int> map) => Required.Where(r => !map.ContainsKey(r)).ToList();

        // Unity builds a Humanoid avatar only if every human bone sits below its human parent
        // (HumanTrait.GetParentBone). Rigs with IK feet parented to the root (the CC0 Quaternius
        // characters) fail this: they keep Generic animation with their own embedded clips.
        static readonly Dictionary<string, string[]> HumanParent = new Dictionary<string, string[]>
        {
            ["Spine"] = new[] { "Hips" }, ["Chest"] = new[] { "Spine" }, ["UpperChest"] = new[] { "Chest" },
            ["Neck"] = new[] { "UpperChest", "Chest", "Spine" }, ["Head"] = new[] { "Neck", "UpperChest", "Chest", "Spine" },
            ["Shoulder"] = new[] { "UpperChest", "Chest", "Spine" }, ["UpperArm"] = new[] { "Shoulder", "UpperChest", "Chest", "Spine" },
            ["LowerArm"] = new[] { "UpperArm" }, ["Hand"] = new[] { "LowerArm" },
            ["UpperLeg"] = new[] { "Hips" }, ["LowerLeg"] = new[] { "UpperLeg" }, ["Foot"] = new[] { "LowerLeg" }, ["Toes"] = new[] { "Foot" },
        };
        public static List<string> HierarchyProblems(Dictionary<string, int> map, IReadOnlyList<(string Name, int Parent)> nodes)
        {
            var o = new List<string>();
            bool Below(int child, int anc) { for (int x = nodes[child].Parent; x >= 0; x = nodes[x].Parent) if (x == anc) return true; return false; }
            foreach (var kv in map)
            {
                string side = kv.Key.StartsWith("Left") ? "Left" : kv.Key.StartsWith("Right") ? "Right" : "";
                string bone = kv.Key.Substring(side.Length);
                if (!HumanParent.TryGetValue(bone, out var parents)) continue;
                foreach (var p in parents)
                {
                    string pk = (p == "Shoulder" || p == "UpperArm" || p == "LowerArm" || p == "UpperLeg" || p == "LowerLeg" || p == "Foot") ? side + p : p;
                    if (!map.TryGetValue(pk, out var pi)) continue;
                    if (!Below(kv.Value, pi)) o.Add($"{kv.Key} ({nodes[kv.Value].Name}) is not below {pk} ({nodes[pi].Name})");
                    break;
                }
            }
            return o;
        }
    }
}
