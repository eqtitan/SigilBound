// Checks for the hero art pipeline's plain-C# half (Core/Art): naming convention, animation state
// resolution, humanoid bone mapping across rig styles, the validation rules, and a dry run of the
// import checks on the CC0 Quaternius models the game already ships (what the Unity tool would
// report for them, measured from the .glb files without Unity).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class ArtTests
{
    static List<(string, int)> Chain(params (string Name, string Parent)[] bones)
    {
        var idx = bones.Select((b, i) => (b.Name, i)).ToDictionary(x => x.Name, x => x.i);
        return bones.Select(b => (b.Name, b.Parent == null ? -1 : idx[b.Parent])).ToList();
    }
    static bool MapsAll(List<(string, int)> nodes, out string missing, params (string Bone, string Node)[] expect)
    {
        var m = HumanoidMap.Map(nodes);
        var miss = HumanoidMap.Missing(m);
        var wrong = expect.Where(e => !m.TryGetValue(e.Bone, out var i) || nodes[i].Item1 != e.Node).Select(e => $"{e.Bone}->{(m.TryGetValue(e.Bone, out var j) ? nodes[j].Item1 : "none")}").ToList();
        missing = string.Join(",", miss.Concat(wrong));
        return miss.Count == 0 && wrong.Count == 0;
    }

    public static void Run(Action<bool, string> check)
    {
        Console.WriteLine("\nHero art pipeline (Core/Art):");

        // Naming convention.
        var h = ArtSpec.Parse("SB_Hero_ysolde_LOD1.glb");
        var a1 = ArtSpec.Parse("SB_Anim_Attack_1.fbx");
        var a2 = ArtSpec.Parse("SB_Anim_Run_Back.fbx");
        var a3 = ArtSpec.Parse("SB_Anim_Caster_Cast_Support.fbx");
        var a4 = ArtSpec.Parse("SB_Anim_brannoc_Idle_Fidget.glb");
        var t = ArtSpec.Parse("T_Hero_maelis_Mask.png");
        var g = ArtSpec.Parse("SB_Gear_hask_Helm_r4.fbx");
        check(h?.Kind == "hero" && h.HeroId == "ysolde" && h.Lod == 1 && a1?.Clip == "Attack_1" && a1.Owner == null && a2?.Clip == "Run_Back" && a2.Owner == null
              && a3?.Owner == "Caster" && a3.Clip == "Cast_Support" && a4?.Owner == "brannoc" && a4.Clip == "Idle_Fidget"
              && t?.Map == "Mask" && g?.Slot == "Helm" && g.Rarity == 4 && ArtSpec.Parse("SB_Anim_Dance.fbx") == null && ArtSpec.Parse("hero.glb") == null,
              "file naming convention parses heroes, shared/archetype/hero animations, textures and gear");
        check(GameData.Heroes.All(x => ArtSpec.Parse(ArtSpec.HeroFileName(x.Id, 0))?.HeroId == x.Id), "all 24 hero ids round-trip through the file names");
        var arch = GameData.Heroes.GroupBy(x => ArtSpec.ArchetypeOf(x.Id)).ToDictionary(x => x.Key, x => x.Count());
        check(ArtSpec.ArchetypeOf("tamsin") == Archetype.Archer && ArtSpec.ArchetypeOf("hask") == Archetype.ShieldBlade && ArtSpec.ArchetypeOf("nyx") == Archetype.Daggers
              && ArtSpec.ArchetypeOf("seraphine") == Archetype.Caster && ArtSpec.ArchetypeOf("gorrum") == Archetype.Heavy && ArtSpec.ArchetypeOf("brannoc") == Archetype.Polearm && arch.Count == 8,
              $"animation archetypes ({string.Join(", ", arch.Select(x => $"{x.Key} {x.Value}"))})");
        check(ArtSpec.AnimCandidates("kaito", "Attack_1").SequenceEqual(new[] { "SB_Anim_kaito_Attack_1.fbx", "SB_Anim_Blade_Attack_1.fbx", "SB_Anim_Attack_1.fbx" }), "clip lookup: hero override, then archetype, then shared");

        // Animation states.
        var res = HeroClips.Resolve(new[] { "CharacterArmature|Idle", "CharacterArmature|Run", "CharacterArmature|Sword_Slash", "CharacterArmature|HitRecieve", "CharacterArmature|Death", "CharacterArmature|Wave" });
        check(res["Idle"] == "CharacterArmature|Idle" && res["Attack_1"] == "CharacterArmature|Sword_Slash" && res["Victory"] == "CharacterArmature|Wave" && !res.ContainsKey("Cast"), "embedded clips resolve to battle states by alias");
        var present = new HashSet<string> { "Idle", "Attack_1", "Cast", "Hit" };
        check(HeroClips.Effective("Attack_3", present) == "Attack_1" && HeroClips.Effective("Cast_Ult", present) == "Cast" && HeroClips.Effective("Victory", present) == "Idle" && HeroClips.Effective("Dodge", present) == null,
              "missing optional states fall back (Attack_3 -> Attack_1, Cast_Ult -> Cast, Victory -> Idle, Dodge skipped)");
        var tripo = HeroClips.Resolve(new[] { "idle", "run", "slash", "shoot", "hurt", "fall" });
        check(new[] { "Idle", "Run", "Attack_1", "Cast", "Hit", "Death" }.All(tripo.ContainsKey), "generator preset names (idle/run/slash/shoot/hurt/fall) cover the required states");

        // Humanoid mapping across rig naming styles.
        var mixamo = Chain(("mixamorig:Hips", null), ("mixamorig:Spine", "mixamorig:Hips"), ("mixamorig:Spine1", "mixamorig:Spine"), ("mixamorig:Spine2", "mixamorig:Spine1"),
            ("mixamorig:Neck", "mixamorig:Spine2"), ("mixamorig:Head", "mixamorig:Neck"),
            ("mixamorig:LeftShoulder", "mixamorig:Spine2"), ("mixamorig:LeftArm", "mixamorig:LeftShoulder"), ("mixamorig:LeftForeArm", "mixamorig:LeftArm"), ("mixamorig:LeftHand", "mixamorig:LeftForeArm"),
            ("mixamorig:LeftHandIndex1", "mixamorig:LeftHand"), ("mixamorig:LeftHandIndex2", "mixamorig:LeftHandIndex1"),
            ("mixamorig:RightShoulder", "mixamorig:Spine2"), ("mixamorig:RightArm", "mixamorig:RightShoulder"), ("mixamorig:RightForeArm", "mixamorig:RightArm"), ("mixamorig:RightHand", "mixamorig:RightForeArm"),
            ("mixamorig:LeftUpLeg", "mixamorig:Hips"), ("mixamorig:LeftLeg", "mixamorig:LeftUpLeg"), ("mixamorig:LeftFoot", "mixamorig:LeftLeg"), ("mixamorig:LeftToeBase", "mixamorig:LeftFoot"),
            ("mixamorig:RightUpLeg", "mixamorig:Hips"), ("mixamorig:RightLeg", "mixamorig:RightUpLeg"), ("mixamorig:RightFoot", "mixamorig:RightLeg"), ("mixamorig:RightToeBase", "mixamorig:RightFoot"));
        check(MapsAll(mixamo, out var m1, ("Hips", "mixamorig:Hips"), ("Spine", "mixamorig:Spine"), ("Chest", "mixamorig:Spine1"), ("UpperChest", "mixamorig:Spine2"), ("Neck", "mixamorig:Neck"),
              ("LeftUpperArm", "mixamorig:LeftArm"), ("LeftLowerArm", "mixamorig:LeftForeArm"), ("LeftLowerLeg", "mixamorig:LeftLeg"), ("LeftShoulder", "mixamorig:LeftShoulder"),
              ("LeftToes", "mixamorig:LeftToeBase"), ("LeftIndexProximal", "mixamorig:LeftHandIndex1")), $"humanoid map: Mixamo-style rig {m1}");
        var ue = Chain(("root", null), ("pelvis", "root"), ("spine_01", "pelvis"), ("spine_02", "spine_01"), ("spine_03", "spine_02"), ("neck_01", "spine_03"), ("head", "neck_01"),
            ("clavicle_l", "spine_03"), ("upperarm_l", "clavicle_l"), ("upperarm_twist_01_l", "upperarm_l"), ("lowerarm_l", "upperarm_l"), ("hand_l", "lowerarm_l"),
            ("clavicle_r", "spine_03"), ("upperarm_r", "clavicle_r"), ("lowerarm_r", "upperarm_r"), ("hand_r", "lowerarm_r"),
            ("thigh_l", "pelvis"), ("calf_l", "thigh_l"), ("foot_l", "calf_l"), ("ball_l", "foot_l"), ("thigh_r", "pelvis"), ("calf_r", "thigh_r"), ("foot_r", "calf_r"));
        check(MapsAll(ue, out var m2, ("Hips", "pelvis"), ("Spine", "spine_01"), ("UpperChest", "spine_03"), ("LeftUpperArm", "upperarm_l"), ("LeftLowerLeg", "calf_l"), ("LeftShoulder", "clavicle_l")), $"humanoid map: UE-style rig with twist bones {m2}");
        var rigify = Chain(("DEF-spine", null), ("DEF-spine.001", "DEF-spine"), ("DEF-spine.002", "DEF-spine.001"), ("DEF-spine.003", "DEF-spine.002"), ("DEF-spine.004", "DEF-spine.003"), ("DEF-spine.006", "DEF-spine.004"),
            ("DEF-shoulder.L", "DEF-spine.003"), ("DEF-upper_arm.L", "DEF-shoulder.L"), ("DEF-upper_arm.L.001", "DEF-upper_arm.L"), ("DEF-forearm.L", "DEF-upper_arm.L.001"), ("DEF-hand.L", "DEF-forearm.L"),
            ("DEF-shoulder.R", "DEF-spine.003"), ("DEF-upper_arm.R", "DEF-shoulder.R"), ("DEF-forearm.R", "DEF-upper_arm.R"), ("DEF-hand.R", "DEF-forearm.R"),
            ("DEF-thigh.L", "DEF-spine"), ("DEF-shin.L", "DEF-thigh.L"), ("DEF-foot.L", "DEF-shin.L"), ("DEF-thigh.R", "DEF-spine"), ("DEF-shin.R", "DEF-thigh.R"), ("DEF-foot.R", "DEF-shin.R"));
        // Rigify has no bone called "head" in the DEF layer (spine.006 is the head): the mapper reports it instead of guessing.
        var rm = HumanoidMap.Map(rigify);
        check(HumanoidMap.Missing(rm).SequenceEqual(new[] { "Head" }) && rigify[rm["Hips"]].Item1 == "DEF-spine" && rigify[rm["LeftLowerArm"]].Item1 == "DEF-forearm.L" && rigify[rm["Spine"]].Item1 == "DEF-spine.001",
              "humanoid map: Rigify DEF rig (hips found where the legs meet; a missing head is reported, not guessed)");

        // Validation rules.
        var s = new HeroAssetStats { HeroId = "kaito", Bones = 70, Materials = 2, SkinnedRenderers = 1 };
        s.LodTris[0] = 28000; s.LodTris[1] = 11000; s.LodTris[2] = 4500;
        foreach (var c in HeroClips.All) s.Clips.Add(c.Name);
        foreach (var k in ArtSpec.RequiredSockets) s.Sockets.Add(k);
        s.Textures.Add(("T_Hero_kaito_Albedo", 2048, 2048));
        var clean = ArtValidation.Validate(s);
        s.LodTris[1] = 19000; s.Materials = 4; s.Textures.Add(("T_Hero_kaito_Mask", 1000, 1000)); s.Clips.Remove("Death"); s.MissingHumanBones.Add("LeftHand");
        var bad = ArtValidation.Validate(s);
        check(clean.Count == 0 && !ArtValidation.Passes(bad) && bad.Any(i => i.Code == "tris" && i.Severity == ArtSeverity.Error) && bad.Any(i => i.Code == "materials")
              && bad.Any(i => i.Code == "texture-pow2") && bad.Any(i => i.Code == "anim-missing") && bad.Any(i => i.Code == "humanoid"),
              $"validation: an on-budget hero is clean, budget/animation/rig problems are errors ({bad.Count} issues)");

        // Dry run of the import checks on the shipped CC0 models (what Unity's tool would see).
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "models");
        var stats = new List<HeroAssetStats>();
        foreach (var (file, hero) in new[] { ("adventurer.glb", "brannoc"), ("monarch.glb", "hask"), ("hooded_adventurer.glb", "wren") })
        {
            var info = GlbInfo.Read(File.ReadAllBytes(Path.Combine(dir, file)));
            var st = info.ToStats(hero, file);
            foreach (var k in ArtSpec.RequiredSockets) st.Sockets.Add(k); // created by the tool from the mapped hand/chest/head bones
            stats.Add(st);
            var j = info.JointHierarchy();
            var map = HumanoidMap.Map(j);
            var probs = HumanoidMap.HierarchyProblems(map, j);
            // Quaternius rigs: legs hang off "Body" (so it is the effective hips) and the feet are IK
            // bones under Root, so Unity can't make a Humanoid avatar; the tool keeps them Generic.
            check(HumanoidMap.Missing(map).Count == 0 && j[map["Hips"]].Item1 == "Body" && j[map["LeftHand"]].Item1 == "Wrist.L" && j[map["UpperChest"]].Item1 == "Chest"
                  && j[map["LeftLowerLeg"]].Item1 == "LowerLeg.L" && map.ContainsKey("LeftThumbProximal") && probs.Count == 2 && probs.All(p => p.Contains("Foot")),
                  $"{file}: all {HumanoidMap.Required.Length} required humanoid bones found ({map.Count} mapped); IK feet flagged as non-Humanoid ({probs.Count} problems)");
            var issues = ArtValidation.Validate(st);
            var errors = issues.Where(i => i.Severity == ArtSeverity.Error).Select(i => i.Code).Distinct().ToList();
            // The placeholder models are solid-colour multi-material meshes: the material budget is the
            // one rule they break (exactly what the import report should tell an agent).
            check(errors.SequenceEqual(new[] { "materials" }) && st.Clips.Count >= 12 && info.Triangles > 0 && issues.Any(i => i.Code == "humanoid" && i.Severity == ArtSeverity.Warning),
                  $"{file} as {hero}: only the material budget fails ({info.Triangles:N0} tris, {st.Bones} bones, {st.Materials} materials, {st.Clips.Count}/{HeroClips.All.Length} states from embedded clips)");
        }
        var report = ArtValidation.Markdown(stats, "Dry-run import report: CC0 Quaternius models");
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "art-dryrun-report.md"), report);
        check(report.Contains("| brannoc |") && report.Contains("## wren"), "markdown validation report renders (bin/.../art-dryrun-report.md)");

        // Hero sheets and prompts (docs/sigilbound/art/heroes): every hero, and no other game's names.
        var promptsPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "hero-prompts.json");
        if (File.Exists(promptsPath))
        {
            var doc = (Dictionary<string, object>)Json.Parse(File.ReadAllText(promptsPath));
            var heroes = Json.Arr(doc, "heroes");
            var ids = heroes.Select(x => Json.Str(x, "id")).ToList();
            check(ids.Count == 24 && GameData.Heroes.All(x => ids.Contains(x.Id)), "hero art data covers all 24 heroes");
            var banned = Json.Arr(doc, "bannedTerms").Select(x => ((string)x).ToLowerInvariant()).ToList();
            doc.Remove("bannedTerms");
            var text = Json.Write(doc).ToLowerInvariant(); // templates, element words and every hero's prompt text
            var hits = banned.Where(b => text.Contains(b)).ToList();
            check(banned.Count >= 10 && hits.Count == 0, $"prompts name no other game, studio or artist ({banned.Count} banned terms checked{(hits.Count > 0 ? ": " + string.Join(", ", hits) : "")})");
        }
        else check(false, "docs/sigilbound/art/heroes/hero-prompts.json missing");
    }
}
