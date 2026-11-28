// Enemy intent (UX review #5/#9, PLAN P2-15): Battle.PreviewAction must be exactly what AiChoose picks when the
// unit's turn starts, and asking for it must never change the battle.
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class IntentTests
{
    static Battle RandomBattle(int seed, bool titan = false)
    {
        var rng = new Random(seed);
        var a = Heroes.OrderBy(_ => rng.Next()).Take(4).Select(t => CombatStats.Mk(t, true)).ToList();
        var b = titan ? new List<Unit> { Battle.TitanUnit(40000, Element.Flame) } : Heroes.OrderBy(_ => rng.Next()).Take(4 + seed % 2).Select(t => CombatStats.Mk(t, false)).ToList();
        return new Battle(a, new List<List<Unit>> { b }, new Random(seed * 31 + 1), titan, titan ? TitanTurns : 0);
    }

    static Battle StageBattle(int idx, int seed)
    {
        var g = new Game(seed: seed);
        g.S.team = new[] { "maelis", "oriel", "aurelia", "vaelith" }.Select(id => { var h = g.AddHero(id); h.stars = 5; h.level = 50; return h.uid; }).ToList();
        return Battle.ForStage(g, idx);
    }

    // Runs a battle turn by turn; before each turn previews every living unit (the HUD does the foes) and checks
    // the actor's preview against what really happens. Returns (turns checked, mismatches, stun previews, kinds seen).
    static (int turns, int bad, int stunned, HashSet<IntentKind> kinds) Walk(Battle bt, bool previewAll = true)
    {
        int turns = 0, bad = 0, stunned = 0; var kinds = new HashSet<IntentKind>();
        for (int n = 0; !bt.Over && n < 3000; n++)
        {
            var u = bt.NextActor(); if (u == null) break;
            if (previewAll) foreach (var x in Battle.Living(bt.Units).ToList()) if (x != u) bt.PreviewAction(x);
            var p = bt.PreviewAction(u);
            var (_, skip) = bt.BeginTurn(u);
            if (skip)
            {
                if (u.Alive && p.Kind != IntentKind.Stunned) bad++;
                if (!u.Alive && p.Kind != IntentKind.None) bad++;
                if (p.Kind == IntentKind.Stunned) stunned++;
            }
            else
            {
                var c = bt.AiChoose(u);
                var sk = u.Template.Skills[c.skill];
                bool single = sk.Target == Target.Enemy || sk.Target == Target.Ally;
                if (p.Skill != c.skill || p.Target != (single ? c.target : null) || p.Unit != u) bad++;
                kinds.Add(p.Kind); kinds.Add(p.Does); // an ultimate still says what it does
                if (p.Debuffs) kinds.Add(IntentKind.Debuff); // attacks that also debuff carry the debuff mark
                bt.Act(u, c.skill, c.target);
            }
            turns++;
            bt.EndTurn(u, out _);
        }
        return (turns, bad, stunned, kinds);
    }

    static string Fingerprint(Battle bt) =>
        bt.Result + "|" + string.Join(",", bt.Allies.Concat(bt.Waves.SelectMany(w => w)).Select(u => $"{u.Hp}/{u.DmgDealt}/{u.Effects.Count}/{u.Tm:0.###}")) + "|" + bt.TitanDamage;

    public static void Run(Action<bool, string> check)
    {
        int turns = 0, bad = 0, stunned = 0; var kinds = new HashSet<IntentKind>();
        for (int seed = 1; seed <= 300; seed++)
        {
            var r = Walk(RandomBattle(seed)); turns += r.turns; bad += r.bad; stunned += r.stunned; kinds.UnionWith(r.kinds);
        }
        for (int idx = 0; idx < TotalStages; idx += 7)
        {
            var r = Walk(StageBattle(idx, 500 + idx)); turns += r.turns; bad += r.bad; stunned += r.stunned; kinds.UnionWith(r.kinds);
        }
        for (int seed = 1; seed <= 6; seed++) { var r = Walk(RandomBattle(seed, true)); turns += r.turns; bad += r.bad; kinds.UnionWith(r.kinds); }
        check(turns > 5000 && bad == 0, $"intent: PreviewAction equals the AI's real choice (skill and target) on every turn ({turns} turns, {bad} mismatches)");
        check(stunned > 0, $"intent: a turn lost to Stun or Broken previews as Stunned ({stunned} seen)");
        check(new[] { IntentKind.Attack, IntentKind.AoE, IntentKind.Heal, IntentKind.Buff, IntentKind.Debuff, IntentKind.Ultimate }.All(kinds.Contains),
            $"intent: every icon kind (or its debuff mark) shows up in real battles ({string.Join(",", kinds.OrderBy(k => k))})");

        // Previewing is read-only: the same seeded battles end identically with and without previews on every turn.
        int same = 0, n = 0;
        for (int seed = 1; seed <= 60; seed++)
        {
            var a = RandomBattle(seed); Walk(a, true);
            var b = RandomBattle(seed); Walk(b, false);
            var c = RandomBattle(seed); c.RunAuto();
            n++; if (Fingerprint(a) == Fingerprint(b) && Fingerprint(b) == Fingerprint(c)) same++;
        }
        check(same == n, $"intent: previews never change a battle (HP, damage, effects, turn meters, result: {same}/{n} identical to plain auto)");

        // The kinds come from the skill itself.
        var hit = new SkillDef { Target = Target.Enemy, Mult = 1 };
        var aoe = new SkillDef { Target = Target.AllEnemies, Mult = 0.6 };
        var heal = new SkillDef { Target = Target.AllAllies, Fx = new[] { new FxDef { Type = FxType.Heal, Pct = 0.2 } } };
        var buff = new SkillDef { Target = Target.AllAllies, Fx = new[] { new FxDef { Type = FxType.AtkUp, Turns = 2 } } };
        var deb = new SkillDef { Target = Target.Enemy, Fx = new[] { new FxDef { Type = FxType.DefDown, Turns = 2 } } };
        check(Battle.Classify(hit) == IntentKind.Attack && Battle.Classify(aoe) == IntentKind.AoE && Battle.Classify(heal) == IntentKind.Heal
            && Battle.Classify(buff) == IntentKind.Buff && Battle.Classify(deb) == IntentKind.Debuff
            && Battle.Debuffs(deb) && !Battle.Debuffs(buff) && !Battle.Debuffs(hit) && Battle.Debuffs(new SkillDef { Target = Target.Enemy, Mult = 1, Fx = new[] { new FxDef { Type = FxType.Strip } } }), "intent: skills classify as attack / AoE / heal / buff / debuff");
        var dead = RandomBattle(3); var foe = dead.Enemies[0]; foe.Alive = false;
        check(dead.PreviewAction(foe).Kind == IntentKind.None && dead.PreviewAction(null).Kind == IntentKind.None, "intent: no preview for a fallen unit");
    }
}
