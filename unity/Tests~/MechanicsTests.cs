// The roster mechanics (docs/sigilbound/ROSTER.md section 4; PLAN steps 25 and 31): Burn, Pierce,
// Execute, Pull, Spread, Counter, Revive, Rage and the Last Light passive, checked on test units with fixed rolls (no crits, no damage spread).
// They are Unity-only: none of the web game's 24 heroes may use them (web parity).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class MechanicsTests
{
    sealed class Fixed : Random { protected override double Sample() => 0.5; public override double NextDouble() => 0.5; }

    static HeroDef Def(string id, params SkillDef[] sk) => new HeroDef { Id = id, Name = id, Rarity = Rarity.Rare, Element = Element.Flame, Role = Role.Attack, Skills = sk };
    static SkillDef Basic(double mult = 1.0) => new SkillDef { Name = "Basic", Target = Target.Enemy, Mult = mult };
    static Unit U(bool ally, HeroDef d, int hp = 10000) =>
        Battle.MakeUnit(ally, d, new Stats { El = Element.Flame, Hp = hp, Atk = 1000, Def = 100, Spd = 100, Crit = 15, Cdmg = 50 });
    static Battle Fight(Unit a, params Unit[] foes) => new Battle(new List<Unit> { a }, new List<List<Unit>> { foes.ToList() }, new Fixed());
    static int Dmg(List<BattleEvent> ev, Unit t) => ev.Where(e => e.Type == EvType.Damage && e.Unit == t).Sum(e => e.Amount);

    public static string StageEnemies(int i) => string.Join("|", Game.StageWaves(i).Select(w => string.Join(",", w.Select(e => e.Template.Id + (e.Boss ? "*" : "")))));
    public static string StageEnemiesJson() => "{\n" + string.Join(",\n", Enumerable.Range(0, TotalStages).Select(i => $"  \"{i}\": \"{StageEnemies(i)}\"")) + "\n}";
    public static void Run(Action<bool, string> check)
    {
        Console.WriteLine("\nRoster mechanics:");
        var dummy = Def("dummy", Basic());

        // Burn: 5% max HP a turn, stacks with Poison, ignores shields, is a debuff (Cleanse removes it).
        {
            var t = U(false, dummy); var b = Fight(U(true, dummy), t);
            t.Effects.Add(new Effect { Type = FxType.Burn, Turns = 2 });
            var (ev1, _) = b.BeginTurn(t);
            t.Effects.Add(new Effect { Type = FxType.Poison, Turns = 2 }); t.Effects.Add(new Effect { Type = FxType.Shield, Val = 99999, Turns = 2 });
            var (ev2, _) = b.BeginTurn(t);
            // Ticks scale with the battle pace (PLAN step 98); a second Poison application stacks (x2 ticks, up to 3).
            int p5 = (int)Math.Round(500 * Battle.Pace), p4 = (int)Math.Round(400 * Battle.Pace);
            t.Effects.First(e => e.Type == FxType.Poison).Stacks = 2;
            var (ev3, _) = b.BeginTurn(t);
            check(Dmg(ev1, t) == p5 && Dmg(ev2, t) == p5 + p4 && Dmg(ev3, t) == p5 + 2 * p4 && !FxDef.IsBuff(FxType.Burn) && new FxDef { Type = FxType.Burn }.IsStatus,
                $"Burn: 5% max HP a turn x pace ({Dmg(ev1, t)}), with Poison 9% ({Dmg(ev2, t)}), two Poison stacks ({Dmg(ev3, t)}), through shields; a debuff");
            var healer = Def("cleanser", new SkillDef { Name = "Cleanse", Target = Target.Ally, Fx = new[] { new FxDef { Type = FxType.Cleanse } } });
            var c = U(true, healer); var bc = Fight(c, U(false, dummy));
            c.Effects.Add(new Effect { Type = FxType.Burn, Turns = 2 });
            bc.Act(c, 0, c);
            check(!c.Has(FxType.Burn), "Burn: Cleanse removes it");
        }

        // Pierce: the target, plus half a hit on the next enemy in line.
        {
            var piercer = Def("piercer", new SkillDef { Name = "Lance", Target = Target.Enemy, Mult = 1.0, Pierce = true });
            var a = U(true, piercer); var t1 = U(false, dummy); var t2 = U(false, dummy); var t3 = U(false, dummy);
            var ev = Fight(a, t1, t2, t3).Act(a, 0, t1);
            int d1 = Dmg(ev, t1), d2 = Dmg(ev, t2), d3 = Dmg(ev, t3);
            check(d1 > 0 && Math.Abs(d2 - d1 * 0.5) <= 1 && d3 == 0, $"Pierce: target {d1}, the one behind {d2} (half), others untouched");
            var a2 = U(true, Def("plain", Basic())); var p1 = U(false, dummy); var p2 = U(false, dummy);
            var ev2 = Fight(a2, p1, p2).Act(a2, 0, p1);
            check(Dmg(ev2, p2) == 0, "Pierce: off for skills without it");
        }

        // Execute: below the threshold after the hit, the target falls; bosses and the Titan are immune.
        {
            var exec = Def("exec", new SkillDef { Name = "Finish", Target = Target.Enemy, Mult = 0.01, Execute = 0.25 });
            var a = U(true, exec); var t = U(false, dummy); t.Hp = 2000;
            var ev = Fight(a, t).Act(a, 0, t);
            var a2 = U(true, exec); var boss = U(false, dummy); boss.Boss = true; boss.Hp = 2000;
            var ev2 = Fight(a2, boss).Act(a2, 0, boss);
            var a3 = U(true, exec); var healthy = U(false, dummy); healthy.Hp = 9000;
            Fight(a3, healthy).Act(a3, 0, healthy);
            check(!t.Alive && ev.Any(e => e.Text == "Executed") && boss.Alive && ev2.Any(e => e.Text == "Immune") && healthy.Alive,
                "Execute: below 25% HP the target falls; bosses are immune; healthy targets only take the hit");
        }

        // Pull: take turn meter from the target and give it to the attacker (no more than it has).
        {
            var puller = Def("puller", new SkillDef { Name = "Hook", Target = Target.Enemy, Mult = 0.1, Fx = new[] { new FxDef { Type = FxType.Pull, Val = 0.3 } } });
            var a = U(true, puller); var t = U(false, dummy); var b = Fight(a, t);
            a.Tm = 0; t.Tm = 500; b.Act(a, 0, t);
            bool full = a.Tm == 300 && t.Tm == 200;
            a.Tm = 0; t.Tm = 100; b.Act(a, 0, t);
            check(full && a.Tm == 100 && t.Tm == 0, "Pull: takes 30% turn meter for the attacker, never more than the target has");
        }

        // Spread: when the target dies, its Poison and Burn move to the next enemy.
        {
            var spreader = Def("spreader", new SkillDef { Name = "Kindle", Target = Target.Enemy, Mult = 5.0, Spread = true });
            var a = U(true, spreader); var t = U(false, dummy, 100); var next = U(false, dummy);
            t.Effects.Add(new Effect { Type = FxType.Poison, Turns = 2 }); t.Effects.Add(new Effect { Type = FxType.Burn, Turns = 3 });
            var ev = Fight(a, t, next).Act(a, 0, t);
            check(!t.Alive && next.Has(FxType.Poison) && next.Has(FxType.Burn) && ev.Any(e => e.Text == "Spread!"), "Spread: on a kill, Poison and Burn jump to the next enemy");
            var a2 = U(true, spreader); var alive = U(false, dummy, 1000000); var n2 = U(false, dummy);
            alive.Effects.Add(new Effect { Type = FxType.Poison, Turns = 2 });
            Fight(a2, alive, n2).Act(a2, 0, alive);
            check(!n2.Has(FxType.Poison), "Spread: nothing spreads if the target survives");
        }

        // Counter: hit by a single-target attack, the holder strikes back at half power; a buff (Strip removes it).
        {
            var a = U(true, dummy); var t = U(false, dummy);
            t.Effects.Add(new Effect { Type = FxType.Counter, Turns = 2 });
            var ev = Fight(a, t).Act(a, 0, t);
            int dealt = Dmg(ev, t), back = Dmg(ev, a);
            check(dealt > 0 && Math.Abs(back - dealt * 0.5) <= 1 && ev.Any(e => e.Text == "Counter!"), $"Counter: hit for {dealt}, struck back for {back} (half)");
            // The client's Counter animation finds the struck-back target from the first Damage after
            // "Counter!" whose Source is the counterer (BattleController.ShowEvents).
            int ci = ev.FindIndex(e => e.Text == "Counter!");
            var after = ci >= 0 ? ev.Skip(ci + 1).FirstOrDefault(e => e.Type == EvType.Damage && e.Source == t) : null;
            check(after != null && after.Unit == a, "Counter: the struck-back damage follows the Counter! event, from the counterer to the attacker");
            var aoe = U(true, Def("aoe", new SkillDef { Name = "Wave", Target = Target.AllEnemies, Mult = 1.0 })); var t2 = U(false, dummy);
            t2.Effects.Add(new Effect { Type = FxType.Counter, Turns = 2 });
            var ev2 = Fight(aoe, t2).Act(aoe, 0, t2);
            var stripper = U(true, Def("strip", new SkillDef { Name = "Strip", Target = Target.Enemy, Mult = 0, Fx = new[] { new FxDef { Type = FxType.Strip } } })); var t3 = U(false, dummy);
            t3.Effects.Add(new Effect { Type = FxType.Counter, Turns = 2 });
            Fight(stripper, t3).Act(stripper, 0, t3);
            check(Dmg(ev2, aoe) == 0 && FxDef.IsBuff(FxType.Counter) && !t3.Has(FxType.Counter), "Counter: not against area attacks; a buff that Strip removes");
        }

        // Revive (phase 3): on a lethal hit the holder comes back at its share of HP, once; a buff.
        {
            var a = U(true, Def("big", Basic(50))); var t = U(false, dummy);
            t.Effects.Add(new Effect { Type = FxType.Revive, Turns = 3, Val = 30 });
            var ev = Fight(a, t).Act(a, 0, t);
            bool first = t.Alive && t.Hp == 3000 && ev.Any(e => e.Text == "Revived!") && !t.Has(FxType.Revive);
            var ev2 = Fight(a, t).Act(a, 0, t);
            var giver = U(true, Def("giver", new SkillDef { Name = "Vigil", Target = Target.AllAllies, Fx = new[] { new FxDef { Type = FxType.Revive, Turns = 3, Pct = 0.25 } } }));
            Fight(giver, U(false, dummy)).Act(giver, 0, giver);
            var given = giver.Effects.FirstOrDefault(e => e.Type == FxType.Revive);
            check(first && !t.Alive && ev2.Any(e => e.Type == EvType.Death) && given != null && given.Val == 25 && FxDef.IsBuff(FxType.Revive),
                "Revive: a lethal hit leaves the holder at 30% HP once, then it falls; given by skills (Pct); a buff");
        }

        // Stacking (PLAN step 98): Poison/Burn stack to 3 on normal units; the Titan never stacks (a re-apply
        // only refreshes its turns), so its damage-based rewards stay as they were.
        {
            var poisoner = Def("poisoner", new SkillDef { Name = "Venom", Target = Target.Enemy, Mult = 0.0001, Fx = new[] { new FxDef { Type = FxType.Poison, Turns = 2 } } });
            var a = U(true, poisoner); var t = U(false, dummy, 1000000); var b = Fight(a, t);
            for (int k = 0; k < 4; k++) b.Act(a, 0, t);
            var titan = U(false, dummy, 1000000); titan.Titan = true; titan.Boss = true;
            var a2 = U(true, poisoner); var bt = new Battle(new List<Unit> { a2 }, new List<List<Unit>> { new List<Unit> { titan } }, new Fixed(), true, 20);
            bt.Act(a2, 0, titan); titan.Effects.First(e => e.Type == FxType.Poison).Turns = 1; bt.Act(a2, 0, titan);
            var tp = titan.Effects.First(e => e.Type == FxType.Poison);
            var (tev, _) = bt.BeginTurn(titan);
            check(t.Effects.First(e => e.Type == FxType.Poison).Stacks == Battle.MaxDotStacks && tp.Stacks == 1 && tp.Turns == 2 && tev.Count(e => e.Type == EvType.Damage && e.Dot) == 1,
                "Stacking: Poison stacks to 3 on a normal unit; on the Titan a re-apply only refreshes its turns (one tick a turn)");
        }

        // Rage (phase 3): damage grows with the attacker's missing HP.
        {
            var rager = Def("rager", new SkillDef { Name = "Fury", Target = Target.Enemy, Mult = 1.0, Rage = 1.0 });
            var a = U(true, rager); var t = U(false, dummy, 1000000);
            int full = Dmg(Fight(a, t).Act(a, 0, t), t);
            a.Hp = a.MaxHp / 2; int half = Dmg(Fight(a, t).Act(a, 0, t), t);
            check(Math.Abs(half - full * 1.5) <= 1, $"Rage: full HP {full}, half HP {half} (+50% at half HP with Rage 1.0)");
        }

        // Last Light (phase 3 passive): when an ally falls, the hero heals all living allies.
        {
            var ember = Def("ember", Basic()); ember.FallHeal = 0.1;
            var keeper = U(true, ember); var friend = U(true, dummy, 100); var hurt = U(true, dummy);
            keeper.Hp = 5000; hurt.Hp = 5000;
            var foe = U(false, Def("foe", Basic(5)));
            var b = new Battle(new List<Unit> { keeper, friend, hurt }, new List<List<Unit>> { new List<Unit> { foe } }, new Fixed());
            var ev = b.Act(foe, 0, friend);
            int healed = 5000 + (int)Math.Round(1000 * Battle.Pace); // 10% of 10000, times the battle pace (PLAN step 98)
            check(!friend.Alive && keeper.Hp == healed && hurt.Hp == healed && ev.Any(e => e.Text == "Last Light") && SkillText.Passive(ember) != null && SkillText.Passive(dummy) == null,
                "Last Light: an ally falls and the passive heals the living allies 10% (x battle pace); the hero screen shows the passive");
        }

        // Combat fixes (PLAN step 99).
        Console.WriteLine("\nCombat fixes (PLAN step 99):");
        {
            // A Break caused by a Counter hit lands on the attacker's own turn: it must still cost the next turn.
            var a = U(true, dummy); a.El = Element.Gale; a.Break = 90;
            var t = U(false, dummy); t.Effects.Add(new Effect { Type = FxType.Counter, Turns = 3 });
            var b = Fight(a, t);
            b.BeginTurn(a); var ev = b.Act(a, 0, t); b.EndTurn(a, out _);
            bool stillBroken = a.Has(FxType.Broken);
            var (_, skip) = b.BeginTurn(a); b.EndTurn(a, out _);
            check(ev.Any(e => e.Text == "Break!" && e.Unit == a) && stillBroken && skip && !a.Has(FxType.Broken) && a.BreakGuard == Battle.BreakGuardTurns,
                "Break from a Counter hit: survives the attacker's own turn and costs it the next one (guard starts after)");

            // BreakGuard counts down only on turns the unit acts, not on a turn lost to Stun.
            var x = U(true, dummy); var bx = Fight(x, U(false, dummy));
            x.BreakGuard = 2; x.Effects.Add(new Effect { Type = FxType.Stun, Turns = 1 });
            var (_, s1) = bx.BeginTurn(x); bx.EndTurn(x, out _); int afterStun = x.BreakGuard;
            var (_, s2) = bx.BeginTurn(x); bx.EndTurn(x, out _);
            check(s1 && afterStun == 2 && !s2 && x.BreakGuard == 1, $"BreakGuard: a stunned turn doesn't count ({afterStun}), an acted one does ({x.BreakGuard})");
        }
        {
            // Auto-battle kill estimate uses the real hit maths (Pace, enrage, scaling stat): it finishes the
            // enemy it can kill instead of the one with the lowest HP share.
            var a = U(true, dummy); var w1 = U(false, dummy, 2000); var w2 = U(false, dummy, 1200);
            w1.Hp = 1400;
            var b = Fight(a, w1, w2);
            var (_, pick) = b.AiChoose(a);
            var hp = U(true, Def("hpscaler", new SkillDef { Name = "Slam", Target = Target.Enemy, Mult = 0.1, Scale = Scale.Hp }), 20000);
            var v1 = U(false, dummy, 6000); var v2 = U(false, dummy, 2400); v1.Hp = 3000;
            var (_, pick2) = Fight(hp, v1, v2).AiChoose(hp);
            check(pick == w2 && pick2 == v2, $"AI kill estimate ({pick?.Hp}, {pick2?.Hp}): counts Pace and the skill's scaling stat (picks the killable foe, not the lowest HP share)");
        }
        {
            // Heals carry their healer (the results screen's Healing column).
            var healer = U(true, Def("healer", Basic(), new SkillDef { Name = "Mend", Target = Target.Ally, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.2 } } }));
            var hurt = U(true, dummy); hurt.Hp = 2000;
            var b = new Battle(new List<Unit> { healer, hurt }, new List<List<Unit>> { new List<Unit> { U(false, dummy) } }, new Fixed());
            var ev = b.Act(healer, 1, hurt);
            check(ev.Any(e => e.Type == EvType.Heal && e.Unit == hurt && e.Source == healer && e.Amount > 0), "Heal events name the healer (Source)");

            // Late enrage: healing and shields are ~0, so the AI stops casting them and no empty shield is added.
            var sh = U(true, Def("shielder", Basic(), new SkillDef { Name = "Mend", Target = Target.Ally, Cooldown = 3, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.2 } } },
                new SkillDef { Name = "Ward", Target = Target.AllAllies, Cooldown = 4, Fx = new[] { new FxDef { Type = FxType.Shield, Pct = 0.2, Turns = 2 } } }));
            var hurt2 = U(true, dummy); hurt2.Hp = 2000;
            var b2 = new Battle(new List<Unit> { sh, hurt2 }, new List<List<Unit>> { new List<Unit> { U(false, dummy) } }, new Fixed());
            var early = b2.AiChoose(sh).skill;
            b2.SkipToRound(Battle.EnrageFrom + 9);
            var late = b2.AiChoose(sh).skill;
            b2.Act(sh, 2, sh);
            check(early == 2 && late == 0 && b2.HealEnrage == 0 && !sh.Has(FxType.Shield) && !hurt2.Has(FxType.Shield),
                $"Late enrage: the AI casts its basic instead of heals and shields (skill {early} -> {late}); a zero shield isn't added");
        }

        // Balance patch (PLAN step 99): campaign easing, role speed shift, Act 2 rewards.
        {
            double m(int idx) => Game.StageWaves(idx)[0][0].Mul;
            bool ls = Math.Abs(m(18) / Game.Grade(18).Mul - Curve.LSEase) < 1e-9 && Math.Abs(m(12) / Game.Grade(12).Mul - 1) < 1e-9; // 4-1 Light vs 3-1 Tide
            double ch10 = m(54) / Curve.LSEase / (Game.Grade(54).Mul / Curve.Chapter[9]); // chapter 10 (Shadow): its chapter factor
            bool finale = Math.Abs(m(89) / Game.Grade(89).Mul - Curve.LSEase * Curve.StageEase[89]) < 1e-9 && Curve.StageEase[89] < 0.95 && Math.Abs(Curve.Act2Ramp - 0.015) < 1e-12;
            bool spd = RoleSpdShift(Role.Attack) == 8 && RoleSpdShift(Role.Defense) == -2 && RoleSpdShift(Role.Hp) == -4 && RoleSpdShift(Role.Support) == 0;
            var r61 = Game.StageRewards(61, false); var r89 = Game.StageRewards(89, false);
            check(ls && Math.Abs(ch10 - 0.98) < 1e-9 && finale && spd && r89.Gold > r61.Gold * 1.5 && r89.Xp > r61.Xp * 1.5 && Game.StageRewards(59, false).Gold == Js.RoundInt(200 + 58 * 60.0),
                $"balance patch: Light/Shadow chapters x{Curve.LSEase}, chapter 10 x{ch10:0.00}, 15-6 eased, role speed A+8/D-2/H-4/S0, Act 2 rewards grow ({r61.Gold} -> {r89.Gold} gold)");
        }

        // Stage enemy pools are frozen (PLAN step 110): new heroes never change an existing stage's line-up.
        {
            var fake = new HeroDef { Id = "zz_new", Name = "Zz New", Rarity = Rarity.Legendary, Element = Element.Shadow, Role = Role.Attack, Skills = Heroes[0].Skills };
            var appended = Heroes.Concat(new[] { fake }).ToArray();
            var inserted = Heroes.Take(30).Concat(new[] { fake }).Concat(Heroes.Skip(30)).ToArray();
            string ids(IReadOnlyList<HeroDef> roster, int i) => string.Join("|", Game.StageWaves(i, roster).Select(w => string.Join(",", w.Select(e => e.Template.Id + (e.Boss ? "*" : "") + ":" + e.El))));
            int moved = Enumerable.Range(0, TotalStages).Count(i => ids(appended, i) != ids(Heroes, i) || ids(inserted, i) != ids(Heroes, i));
            check(moved == 0 && Act2Pool.Length == Act2EnemyIds.Length && Act2EnemyIds.Distinct().Count() == Act2EnemyIds.Length,
                $"stage enemies: adding a hero to the roster (at the end or in the middle) changes no stage's waves ({moved} of {TotalStages} moved)");
            var golden = (Dictionary<string, object>)Json.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "stage-enemies.json")));
            var off = Enumerable.Range(0, TotalStages).Where(i => !golden.TryGetValue(i.ToString(), out var v) || (string)v != StageEnemies(i)).Select(Game.StageLabel).ToList();
            var newSeven = new[] { "vorgrim", "talvo", "wystan", "crusk", "hartwin", "nyssa", "vessa" };
            check(off.Count == 0 && golden.Count == TotalStages && !newSeven.Any(Act2EnemyIds.Contains),
                $"stage enemies match the golden list (fixtures/stage-enemies.json) and the seven newest heroes are not in Act II" + (off.Count > 0 ? $" (changed: {string.Join(", ", off.Take(8))})" : ""));
        }

        // Text and web parity.
        var demo = new SkillDef { Name = "Demo", Target = Target.Enemy, Mult = 1, Pierce = true, Execute = 0.25, Spread = true, Fx = new[] { new FxDef { Type = FxType.Pull, Val = 0.2 }, new FxDef { Type = FxType.Burn, Turns = 2 } } };
        var txt = SkillText.Describe(demo);
        check(txt.Contains("pierces") && txt.Contains("executes below 25%") && txt.Contains("spread") && txt.Contains("takes 20% Turn Meter") && txt.Contains("Burn")
              && SkillText.EffectShort(FxType.Burn) == "BRN" && SkillText.EffectShort(FxType.Counter) == "CTR", "skill text and status labels describe the new mechanics");
        bool webClean = Classic.All(h => h.FallHeal == 0 && h.Skills.All(s => !s.Pierce && s.Execute == 0 && !s.Spread && s.Rage == 0 && s.Fx.All(f => f.Type < FxType.Burn)));
        check(webClean && (int)FxType.Strip == 15, "the web game's 24 heroes use none of them, and the web effect order is unchanged");
    }
}
