// The roster mechanics (docs/sigilbound/ROSTER.md section 4; PLAN steps 25 and 31): Burn, Pierce,
// Execute, Pull, Spread, Counter, Revive, Rage and the Last Light passive, checked on test units with fixed rolls (no crits, no damage spread).
// They are Unity-only: none of the web game's 24 heroes may use them (web parity).
using System;
using System.Collections.Generic;
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
            check(Dmg(ev1, t) == 500 && Dmg(ev2, t) == 900 && !FxDef.IsBuff(FxType.Burn) && new FxDef { Type = FxType.Burn }.IsStatus,
                $"Burn: 5% max HP a turn ({Dmg(ev1, t)}), with Poison 9% ({Dmg(ev2, t)}), through shields; a debuff");
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
            check(!friend.Alive && keeper.Hp == 6000 && hurt.Hp == 6000 && ev.Any(e => e.Text == "Last Light") && SkillText.Passive(ember) != null && SkillText.Passive(dummy) == null,
                "Last Light: an ally falls and the passive heals the living allies 10%; the hero screen shows the passive");
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
