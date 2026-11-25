// The hero art standard from docs/sigilbound/art/ART_BIBLE.md in code form, so the Unity import
// tool (Editor/Art), the runtime loader (Client/HeroRig) and the offline checks (Tests~,
// Tools~/heroforge) all agree on one set of names, budgets and animation states.
// Plain C#: no Unity types, tested by `dotnet run --project unity/Tests~`.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sigilbound.Core
{
    // Mobile budgets for a Samsung Galaxy Note20 baseline (ART_BIBLE.md section 6).
    public static class ArtBudget
    {
        // Triangles per hero, everything included (body, hair, weapon, worn gear).
        public const int TrisLod0 = 30000;   // showcase: hero viewer, summon reveal, card photo
        public const int TrisLod1 = 12000;   // battle (up to 10 units on screen)
        public const int TrisLod2 = 5000;    // menu lineup, back row, low quality tier
        public static int Tris(int lod) => lod <= 0 ? TrisLod0 : lod == 1 ? TrisLod1 : TrisLod2;
        public const double HardFactor = 1.5; // over budget = warning; over budget x 1.5 = error

        public const int Bones = 75;              // humanoid with fingers (about 65) plus up to 10 secondary (cape, hair, tabard)
        public const int BoneWeights = 4;         // per vertex
        public const int Materials = 2;           // body atlas (opaque) + optional alpha-clip cards (hair, feathers, fringe)
        public const int SkinnedRenderers = 2;    // per LOD
        public const int DrawCallsPerHero = 3;    // per pass, before VFX: 2 materials + 1 rigid weapon/gear mesh
        public const int TextureMax = 2048;       // source albedo; shipped at 1024 on Android, 2048 on the Ultra tier
        public const int TextureShipped = 1024;
        public const int TextureMin = 512;
        public const double HeightMin = 0.4, HeightMax = 4.0; // metres, a sanity window for unit scale mistakes
        // Weapon size by gear rarity (0 = none, 1..5 = Worn..Mythic): the same steps the code-built kit uses.
        public static readonly double[] WeaponScale = { 1, 1, 1.04, 1.08, 1.13, 1.18 };
    }

    // One battle animation state: its canonical name, whether it loops, what stands in when it is
    // missing, and the animation policy: Shared states (idle, waiting for a turn, hit reactions,
    // run-in) come from one library for every hero; all other states should be unique per hero.
    public sealed class ClipSpec
    {
        public string Name; public bool Loop; public bool Clamp; public bool Required; public string[] Fallbacks = new string[0]; public string When;
        public bool Shared;
        public bool Unique => !Shared;
        public string Fallback => Fallbacks.Length > 0 ? Fallbacks[0] : null;
        public bool SpeedParam => !(Name == "Idle" || Name == "Victory"); // idle and victory always play at 1x
    }

    public static class HeroClips
    {
        // The states the battle and hero views trigger (CharacterView / BattleController / HeroModel).
        // Skill_<n> is the hero's own clip for skill n (1 = basic attack, 3 = usually the ultimate):
        // when present it plays instead of the generic Attack/Cast state for that skill.
        public static readonly ClipSpec[] All =
        {
            new ClipSpec { Name = "Idle", Loop = true, Required = true, Shared = true, When = "standing in battle, waiting for a turn, hero viewer, menu lineup" },
            new ClipSpec { Name = "Idle_Fidget", Shared = true, Fallbacks = new[] { "Idle" }, When = "occasional idle variation (web: every 8-20 s)" },
            new ClipSpec { Name = "Run", Loop = true, Required = true, Shared = true, When = "melee run-in toward the target (0.26 s)" },
            new ClipSpec { Name = "Run_Back", Loop = true, Shared = true, Fallbacks = new[] { "Run" }, When = "backpedal home after a melee strike (0.25 s)" },
            new ClipSpec { Name = "Skill_1", When = "skill 1 (the basic attack): the hero's own motion" },
            new ClipSpec { Name = "Skill_2", When = "skill 2: the hero's own motion" },
            new ClipSpec { Name = "Skill_3", When = "skill 3 (usually the ultimate): the hero's own motion" },
            new ClipSpec { Name = "Skill_4", When = "skill 4 (reserved for heroes with four skills)" },
            new ClipSpec { Name = "Attack_1", Required = true, Fallbacks = new[] { "Skill_1" }, When = "single-target damage skill, melee heroes, when there is no Skill_<n> clip; strike rotation 1" },
            new ClipSpec { Name = "Attack_2", Fallbacks = new[] { "Attack_1" }, When = "strike rotation 2" },
            new ClipSpec { Name = "Attack_3", Fallbacks = new[] { "Attack_1" }, When = "strike rotation 3 (often a kick or shield bash)" },
            new ClipSpec { Name = "Cast", Required = true, Fallbacks = new[] { "Skill_2", "Skill_3", "Skill_1" }, When = "ranged attacks and all-enemies skills without a Skill_<n> clip" },
            new ClipSpec { Name = "Cast_Ult", Fallbacks = new[] { "Skill_3", "Cast" }, When = "third skill or all-enemies skill (camera zooms toward the caster)" },
            new ClipSpec { Name = "Cast_Support", Fallbacks = new[] { "Cast" }, When = "no-damage skills: heals, buffs, shields, taunts" },
            new ClipSpec { Name = "Hit", Required = true, Shared = true, When = "taking damage" },
            new ClipSpec { Name = "Hit_Big", Shared = true, Fallbacks = new[] { "Hit" }, When = "crit or a hit over 14% of max HP" },
            new ClipSpec { Name = "Dodge", Shared = true, When = "glancing blow (weak element or under 4% HP), 35% chance; skipped if missing" },
            new ClipSpec { Name = "Death", Clamp = true, Required = true, When = "HP reaches 0; holds the last frame, then the unit sinks" },
            new ClipSpec { Name = "Victory", Loop = true, Fallbacks = new[] { "Idle" }, When = "winners after the last wave (staggered)" },
        };
        public static readonly Dictionary<string, ClipSpec> ByName = All.ToDictionary(c => c.Name);
        public static IEnumerable<ClipSpec> UniqueStates => All.Where(c => c.Unique);

        // Source clip names we recognise inside a model file, per canonical state, in order of preference:
        // the CC0 Quaternius character clips and Universal Animation Library, generator presets
        // (idle/walk/run/slash/shoot/hurt/fall style names) and common Mixamo-style names.
        public static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            ["Idle"] = new[] { "Idle", "Idle_Sword", "Idle_Loop", "Idle_Neutral", "idle", "Breathing_Idle", "Standing_Idle" },
            ["Idle_Fidget"] = new[] { "Idle_Fidget", "Idle_Neutral", "Interact", "Idle_Look" },
            ["Run"] = new[] { "Run", "Running", "run", "Run_Forward", "Jog_Fwd", "Sprint" },
            ["Run_Back"] = new[] { "Run_Back", "Running_Backward", "Walk_Back", "Jog_Bwd" },
            ["Skill_1"] = new[] { "Skill_1" }, ["Skill_2"] = new[] { "Skill_2" }, ["Skill_3"] = new[] { "Skill_3" }, ["Skill_4"] = new[] { "Skill_4" },
            ["Attack_1"] = new[] { "Attack_1", "Sword_Slash", "Slash", "slash", "Attack", "Sword_Attack", "Punch_Right", "Punch", "chop" },
            ["Attack_2"] = new[] { "Attack_2", "Punch_Left", "Sword_Attack_2", "Kick_Right" },
            ["Attack_3"] = new[] { "Attack_3", "Kick_Right", "Kick_Left", "Shield_Bash" },
            ["Cast"] = new[] { "Cast", "Spell_Simple_Shoot", "Gun_Shoot", "Shoot", "shoot", "Spell", "Magic_Attack", "Bow_Shoot" },
            ["Cast_Ult"] = new[] { "Cast_Ult", "Spell_Double_Shoot", "Ultimate", "Big_Cast" },
            ["Cast_Support"] = new[] { "Cast_Support", "Spell_Simple_Enter", "Interact", "Buff", "Heal", "Cheer" },
            ["Hit"] = new[] { "Hit", "Hit_Chest", "HitRecieve", "HitReact", "Hit_Reaction", "hurt", "Hurt" },
            ["Hit_Big"] = new[] { "Hit_Big", "Hit_Head", "HitRecieve_2", "Hit_Knockback" },
            ["Dodge"] = new[] { "Dodge", "Roll", "Dodge_Back", "dive" },
            ["Death"] = new[] { "Death", "Death01", "Die", "Dying", "fall" },
            ["Victory"] = new[] { "Victory", "Wave", "Cheer", "Yes", "Victory_Pose" },
        };

        // Resolves canonical states to clips present in a file (names may carry "Armature|" prefixes).
        public static Dictionary<string, string> Resolve(IEnumerable<string> available)
        {
            var list = available.Where(n => !string.IsNullOrEmpty(n)).ToList();
            var byShort = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in list) { var s = Short(n); if (!byShort.ContainsKey(s)) byShort[s] = n; }
            var outp = new Dictionary<string, string>();
            foreach (var c in All)
                foreach (var a in Aliases[c.Name])
                    if (byShort.TryGetValue(a, out var hit)) { outp[c.Name] = hit; break; }
            return outp;
        }
        public static string Short(string clipName) => clipName.Split('|').Last().Trim();

        // The canonical state that actually plays for `name`, following fallbacks (null if none present).
        public static string Effective(string name, ICollection<string> present)
        {
            var seen = new HashSet<string>();
            string Walk(string n)
            {
                if (n == null || !seen.Add(n)) return null;
                if (present.Contains(n)) return n;
                if (!ByName.TryGetValue(n, out var c)) return null;
                foreach (var f in c.Fallbacks) { var r = Walk(f); if (r != null) return r; }
                return null;
            }
            return Walk(name);
        }
    }

    // Weapon/animation families; the shared library can hold one clip set per archetype
    // (SB_Anim_<Archetype>_<Clip>), so a spear thrust and a hammer swing don't share one attack.
    public enum Archetype { Blade, Polearm, Brawler, Daggers, ShieldBlade, Heavy, Caster, Archer }

    public static class ArtSpec
    {
        public static readonly string[] Sockets = { "Socket_HandR", "Socket_HandL", "Socket_Back", "Socket_Head", "Socket_Hip", "Socket_Cast" };
        public static readonly string[] RequiredSockets = { "Socket_HandR", "Socket_HandL", "Socket_Back", "Socket_Head" };
        public static readonly string[] GearSlots = { "Weapon", "Offhand", "Helm", "Chest", "Boots", "Back" };
        public static readonly string[] TextureMaps = { "Albedo", "Normal", "Mask", "Emission" };

        static readonly Regex HeroFile = new Regex(@"^SB_Hero_(?<id>[a-z]+)_LOD(?<lod>[0-2])\.(?<ext>glb|gltf|fbx)$", RegexOptions.IgnoreCase);
        static readonly Regex AnimFile = new Regex(@"^SB_Anim_(?:(?<owner>[A-Za-z]+)_)?(?<clip>[A-Za-z]+(?:_[A-Za-z0-9]+)?)(?:@(?<variant>[a-z0-9-]+))?\.(?<ext>glb|fbx)$", RegexOptions.IgnoreCase);
        static readonly Regex MetaFile = new Regex(@"^SB_Hero_(?<id>[a-z]+)\.json$", RegexOptions.IgnoreCase);
        static readonly Regex TexFile = new Regex(@"^T_Hero_(?<id>[a-z]+)_(?<map>Albedo|Normal|Mask|Emission)\.(?<ext>png|tga|jpg|jpeg)$", RegexOptions.IgnoreCase);
        static readonly Regex GearFile = new Regex(@"^SB_Gear_(?<id>[a-z]+)_(?<slot>Weapon|Offhand|Helm|Chest|Boots|Back)(?:_r(?<r>[1-5]))?\.(?<ext>glb|fbx)$", RegexOptions.IgnoreCase);

        public static string HeroFileName(string heroId, int lod, string ext = "glb") => $"SB_Hero_{heroId}_LOD{lod}.{ext}";
        public static string AnimFileName(string clip, string owner = null, string ext = "fbx") => owner == null ? $"SB_Anim_{clip}.{ext}" : $"SB_Anim_{owner}_{clip}.{ext}";
        public static string TextureFileName(string heroId, string map) => $"T_Hero_{heroId}_{map}.png";
        public static string MaterialName(string heroId, string slot) => $"M_Hero_{heroId}_{slot}";

        public sealed class Parsed { public string Kind, HeroId, Owner, Clip, Variant, Map, Slot, Ext; public int Lod = -1, Rarity; }
        public static string MetaFileName(string heroId) => $"SB_Hero_{heroId}.json";

        // Classifies a file name under the convention, or returns null (unknown files are reported, not imported).
        public static Parsed Parse(string fileName)
        {
            var m = HeroFile.Match(fileName);
            if (m.Success) return new Parsed { Kind = "hero", HeroId = m.Groups["id"].Value.ToLowerInvariant(), Lod = int.Parse(m.Groups["lod"].Value), Ext = m.Groups["ext"].Value.ToLowerInvariant() };
            m = MetaFile.Match(fileName);
            if (m.Success) return new Parsed { Kind = "meta", HeroId = m.Groups["id"].Value.ToLowerInvariant(), Ext = "json" };
            m = TexFile.Match(fileName);
            if (m.Success) return new Parsed { Kind = "texture", HeroId = m.Groups["id"].Value.ToLowerInvariant(), Map = Cap(m.Groups["map"].Value), Ext = m.Groups["ext"].Value.ToLowerInvariant() };
            m = GearFile.Match(fileName);
            if (m.Success) return new Parsed { Kind = "gear", HeroId = m.Groups["id"].Value.ToLowerInvariant(), Slot = Cap(m.Groups["slot"].Value), Rarity = m.Groups["r"].Success ? int.Parse(m.Groups["r"].Value) : 0, Ext = m.Groups["ext"].Value.ToLowerInvariant() };
            m = AnimFile.Match(fileName);
            if (m.Success)
            {
                string owner = m.Groups["owner"].Success ? m.Groups["owner"].Value : null, clip = m.Groups["clip"].Value;
                // "SB_Anim_Run_Back" parses as owner Run + clip Back: prefer the reading that names a real clip.
                if (owner != null && !HeroClips.ByName.ContainsKey(clip) && HeroClips.ByName.ContainsKey(owner + "_" + clip)) { clip = owner + "_" + clip; owner = null; }
                if (!HeroClips.ByName.ContainsKey(clip)) return null;
                if (owner != null && !Enum.TryParse<Archetype>(owner, true, out _)) owner = owner.ToLowerInvariant();
                return new Parsed { Kind = "anim", Owner = owner, Clip = clip, Variant = m.Groups["variant"].Success ? m.Groups["variant"].Value.ToLowerInvariant() : null, Ext = m.Groups["ext"].Value.ToLowerInvariant() };
            }
            return null;
        }
        static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1).ToLowerInvariant();

        static readonly HashSet<string> BrawlerIds = new HashSet<string> { "odo", "kettle", "gorrum", "thrain" };
        public static Archetype ArchetypeOf(string heroId)
        {
            var look = Looks.Of(heroId);
            string p = look.Prop;
            if (heroId == "gorrum" || heroId == "thrain") return Archetype.Heavy;
            if (BrawlerIds.Contains(heroId)) return Archetype.Brawler;
            if (p == "daggers") return Archetype.Daggers;
            if (p == "spear" || p == "trident") return Archetype.Polearm;
            if (p == "shield") return Archetype.ShieldBlade;
            if (p == "bow") return Archetype.Archer;
            if (Looks.IsCaster(heroId)) return Archetype.Caster;
            return Archetype.Blade;
        }

        // Where a clip came from: the hero's own file, a tag-matched archetype variant, the plain
        // archetype clip, or the shared library. For unique states anything but "hero" is a fallback.
        public enum ClipOrigin { Hero, Tagged, Archetype, Shared, Embedded }

        // Clip lookup order for a hero (file names; the caller checks which exist):
        //   shared states: shared library, then archetype, then the hero's own (consistency first);
        //   unique states: the hero's own, then the archetype set (variants chosen by PickVariant), then shared.
        public static IEnumerable<(string File, ClipOrigin Origin)> AnimSources(string heroId, string clip)
        {
            var arche = ArchetypeOf(heroId).ToString();
            bool shared = HeroClips.ByName.TryGetValue(clip, out var spec) && spec.Shared;
            if (shared) { yield return (AnimFileName(clip), ClipOrigin.Shared); yield return (AnimFileName(clip, arche), ClipOrigin.Archetype); yield return (AnimFileName(clip, heroId), ClipOrigin.Hero); }
            else { yield return (AnimFileName(clip, heroId), ClipOrigin.Hero); yield return (AnimFileName(clip, arche), ClipOrigin.Archetype); yield return (AnimFileName(clip), ClipOrigin.Shared); }
        }
        public static IEnumerable<string> AnimCandidates(string heroId, string clip) => AnimSources(heroId, clip).Select(x => x.File);

        // Tag-based fallback for unique states when a hero's own clip couldn't be generated: among an
        // archetype's variants for the state (SB_Anim_<Archetype>_<State>@<variant>, tagged in the
        // library index), pick the one sharing the most motion tags with the hero; ties are spread
        // by a stable hash of the hero id, so 100 heroes don't all fall back to the same clip.
        public static string PickVariant(string heroId, IEnumerable<string> heroTags, IReadOnlyList<(string File, string[] Tags)> variants)
        {
            if (variants == null || variants.Count == 0) return null;
            var tags = new HashSet<string>((heroTags ?? Enumerable.Empty<string>()).Select(t => t.ToLowerInvariant()));
            int best = variants.Max(v => v.Tags.Count(t => tags.Contains(t.ToLowerInvariant())));
            var top = variants.Where(v => v.Tags.Count(t => tags.Contains(t.ToLowerInvariant())) == best).OrderBy(v => v.File, StringComparer.Ordinal).ToList();
            return top[(int)(StableHash(heroId) % (uint)top.Count)].File;
        }
        public static uint StableHash(string s) { uint h = 2166136261; foreach (char c in s ?? "") { h ^= c; h *= 16777619; } return h; }

        // Battle height the runtime scales a hero prefab to (the current models: head at 1.85 m, about
        // 2.05 m to the crown), times the hero's build from the web art (BODY h).
        public const double BaseHeight = 2.05;
        public static double TargetHeight(string heroId) => BaseHeight * HeroArt.Body(heroId).H;
    }

    public enum ArtSeverity { Info, Warning, Error }
    public sealed class ArtIssue
    {
        public ArtSeverity Severity; public string Code, Message;
        public override string ToString() => $"{Severity.ToString().ToUpperInvariant()} {Code}: {Message}";
    }

    // What the import tool (or the offline GLB reader) measured for one hero.
    public sealed class HeroAssetStats
    {
        public string HeroId, Source = "", AnimBackend = "";
        public int[] LodTris = { -1, -1, -1 };               // -1 = that LOD file is missing
        public int Bones, Materials, SkinnedRenderers, MaxBoneWeights = 4;
        public double Height;
        public readonly List<(string Name, int W, int H)> Textures = new List<(string, int, int)>();
        public readonly HashSet<string> Clips = new HashSet<string>();      // canonical states that resolved
        public readonly Dictionary<string, string> ClipOrigins = new Dictionary<string, string>(); // state -> hero/tagged/archetype/shared/embedded
        public readonly HashSet<string> Sockets = new HashSet<string>();
        public readonly List<string> MissingHumanBones = new List<string>();
        public readonly List<string> HumanoidProblems = new List<string>();  // bones outside their human parent (no Humanoid avatar)
        public readonly List<string> Notes = new List<string>();
    }

    public static class ArtValidation
    {
        public static List<ArtIssue> Validate(HeroAssetStats s)
        {
            var o = new List<ArtIssue>();
            void Add(ArtSeverity sev, string code, string msg) => o.Add(new ArtIssue { Severity = sev, Code = code, Message = msg });
            if (GameData.Hero(s.HeroId) == null) Add(ArtSeverity.Error, "unknown-hero", $"'{s.HeroId}' is not one of the 24 hero ids");
            for (int lod = 0; lod < 3; lod++)
            {
                int t = s.LodTris[lod], b = ArtBudget.Tris(lod);
                if (t < 0) { Add(lod == 0 ? ArtSeverity.Error : ArtSeverity.Warning, "lod-missing", lod == 0 ? "LOD0 model missing" : $"LOD{lod} missing (generate it with Tools~/heroforge lods, or LOD0 is used at every distance)"); continue; }
                if (t > b * ArtBudget.HardFactor) Add(ArtSeverity.Error, "tris", $"LOD{lod}: {t:N0} tris, over 1.5x the {b:N0} budget");
                else if (t > b) Add(ArtSeverity.Warning, "tris", $"LOD{lod}: {t:N0} tris, over the {b:N0} budget");
            }
            // With only LOD0, battle uses it: flag when that blows the battle budget.
            if (s.LodTris[0] > 0 && s.LodTris[1] < 0 && s.LodTris[0] > ArtBudget.TrisLod1 * ArtBudget.HardFactor)
                Add(ArtSeverity.Warning, "battle-tris", $"no LOD1, so battle draws LOD0 at {s.LodTris[0]:N0} tris (battle budget {ArtBudget.TrisLod1:N0})");
            if (s.Bones > ArtBudget.Bones) Add(ArtSeverity.Warning, "bones", $"{s.Bones} bones (budget {ArtBudget.Bones})");
            if (s.Bones == 0) Add(ArtSeverity.Error, "no-skin", "no skinned mesh/skeleton found");
            if (s.MaxBoneWeights > ArtBudget.BoneWeights) Add(ArtSeverity.Info, "weights", $"up to {s.MaxBoneWeights} bone weights per vertex; Unity will use {ArtBudget.BoneWeights}");
            if (s.Materials > ArtBudget.Materials + 1) Add(ArtSeverity.Error, "materials", $"{s.Materials} materials (budget {ArtBudget.Materials}, +1 for a rigid weapon)");
            else if (s.Materials > ArtBudget.Materials) Add(ArtSeverity.Warning, "materials", $"{s.Materials} materials (budget {ArtBudget.Materials})");
            if (s.SkinnedRenderers > ArtBudget.SkinnedRenderers) Add(ArtSeverity.Warning, "renderers", $"{s.SkinnedRenderers} skinned meshes per LOD (budget {ArtBudget.SkinnedRenderers}); merge them");
            foreach (var t in s.Textures)
            {
                int big = Math.Max(t.W, t.H);
                if (big > ArtBudget.TextureMax) Add(ArtSeverity.Warning, "texture-size", $"{t.Name}: {t.W}x{t.H}, over {ArtBudget.TextureMax}");
                else if (big < ArtBudget.TextureMin) Add(ArtSeverity.Info, "texture-size", $"{t.Name}: {t.W}x{t.H}, under {ArtBudget.TextureMin} (blurry on the showcase)");
                if (!IsPow2(t.W) || !IsPow2(t.H)) Add(ArtSeverity.Warning, "texture-pow2", $"{t.Name}: {t.W}x{t.H} is not a power of two (no ETC2/ASTC mip chain)");
            }
            if (s.Height > 0 && (s.Height < ArtBudget.HeightMin || s.Height > ArtBudget.HeightMax)) Add(ArtSeverity.Warning, "scale", $"model is {s.Height:0.00} m tall; check the export units (the prefab is rescaled, but bones and physics follow the file)");
            // Without a Humanoid avatar the shared (retargeted) library can't drive the hero; that is
            // fine when the file brings its own clips for every required state.
            bool ownClips = s.AnimBackend.StartsWith("embedded", StringComparison.Ordinal) && HeroClips.All.Where(c => c.Required).All(c => s.Clips.Contains(c.Name));
            var hsev = ownClips ? ArtSeverity.Warning : ArtSeverity.Error;
            foreach (var bone in s.MissingHumanBones) Add(hsev, "humanoid", $"no bone found for {bone}{(ownClips ? " (embedded clips used; no shared-library retargeting)" : "")}");
            foreach (var p in s.HumanoidProblems.Take(4)) Add(hsev, "humanoid", $"{p}{(ownClips ? " (embedded clips used; no shared-library retargeting)" : "")}");
            foreach (var c in HeroClips.All)
            {
                if (s.Clips.Contains(c.Name)) continue;
                if (c.Required) Add(ArtSeverity.Error, "anim-missing", $"required animation {c.Name} missing ({c.When})");
                else if (!c.Name.StartsWith("Skill_", StringComparison.Ordinal) || c.Name == "Skill_4")
                    Add(ArtSeverity.Info, "anim-fallback", c.Fallback != null ? $"{c.Name} missing, plays {HeroClips.Effective(c.Name, s.Clips) ?? "nothing"}" : $"{c.Name} missing, skipped");
            }
            // Uniqueness policy: skills, death and victory should be the hero's own motion.
            var borrowed = UniqueFallbacks(s);
            if (borrowed.Count > 0) Add(ArtSeverity.Warning, "anim-not-unique", $"using fallback motion for {string.Join(", ", borrowed)}");
            foreach (var sock in ArtSpec.RequiredSockets) if (!s.Sockets.Contains(sock)) Add(ArtSeverity.Warning, "socket", $"{sock} not created");
            return o;
        }
        // The unique states a hero plays with someone else's motion: missing own clips for skills,
        // death and victory, or ones filled from an archetype variant or the shared library.
        public static List<string> UniqueFallbacks(HeroAssetStats s)
        {
            var o = new List<string>();
            foreach (var name in new[] { "Skill_1", "Skill_2", "Skill_3", "Death", "Victory" })
            {
                if (!s.Clips.Contains(name)) { o.Add(name + " (missing)"); continue; }
                if (s.ClipOrigins.TryGetValue(name, out var org) && org != "hero" && org != "embedded") o.Add($"{name} ({org})");
            }
            return o;
        }
        public static int UniqueCount(HeroAssetStats s) => 5 - UniqueFallbacks(s).Count;

        static bool IsPow2(int v) => v > 0 && (v & (v - 1)) == 0;

        public static bool Passes(IEnumerable<ArtIssue> issues) => issues.All(i => i.Severity != ArtSeverity.Error);

        // Markdown report: a summary table, then the issues per hero.
        public static string Markdown(IEnumerable<HeroAssetStats> all, string title)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {title}").AppendLine();
            sb.AppendLine($"Budgets: LOD0 {ArtBudget.TrisLod0:N0} / LOD1 {ArtBudget.TrisLod1:N0} / LOD2 {ArtBudget.TrisLod2:N0} tris, {ArtBudget.Bones} bones, {ArtBudget.Materials} materials, textures {ArtBudget.TextureMin}-{ArtBudget.TextureMax} px.").AppendLine();
            sb.AppendLine("Unique = own motion for Skill_1-3, Death and Victory (out of 5); anything less means the hero is using fallbacks.").AppendLine();
            sb.AppendLine("| Hero | Result | LOD0 | LOD1 | LOD2 | Bones | Mats | Textures | Clips | Unique | Sockets | Anim | Source |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var details = new System.Text.StringBuilder();
            foreach (var s in all)
            {
                var issues = Validate(s);
                string T(int i) => s.LodTris[i] < 0 ? "-" : s.LodTris[i].ToString("N0");
                int errs = issues.Count(i => i.Severity == ArtSeverity.Error), warns = issues.Count(i => i.Severity == ArtSeverity.Warning);
                sb.AppendLine($"| {s.HeroId} | {(errs > 0 ? $"FAIL ({errs})" : warns > 0 ? $"ok, {warns} warn" : "ok")} | {T(0)} | {T(1)} | {T(2)} | {s.Bones} | {s.Materials} | {(s.Textures.Count == 0 ? "none" : string.Join(", ", s.Textures.Select(t => $"{t.W}")))} | {s.Clips.Count}/{HeroClips.All.Length} | {UniqueCount(s)}/5{(UniqueCount(s) < 5 ? " FALLBACK" : "")} | {s.Sockets.Count} | {s.AnimBackend} | {s.Source} |");
                details.AppendLine($"## {s.HeroId}").AppendLine();
                foreach (var i in issues) details.AppendLine($"- {i}");
                foreach (var n in s.Notes) details.AppendLine($"- NOTE: {n}");
                details.AppendLine();
            }
            sb.AppendLine().Append(details);
            return sb.ToString();
        }
    }
}
