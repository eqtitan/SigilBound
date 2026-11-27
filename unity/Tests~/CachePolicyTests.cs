// The high-detail model cache stays bounded (code review H1): models in use are kept, unused ones go
// least recently used first down to the cap, and just-used ones get a grace period up to a hard cap.
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;

static class CachePolicyTests
{
    public static void Run(Action<bool, string> check)
    {
        const int cap = 8, hard = 16; const float recent = 30f;
        // 150 heroes browsed one after another (the hero list), all unused now, long ago.
        var browsed = Enumerable.Range(0, 150).Select(i => new CachePolicy.Entry("h" + i, 0, i)).ToList();
        var ev = CachePolicy.Evict(browsed, 1000, cap, hard, recent);
        check(ev.Count == 142 && !ev.Contains("h149") && ev.Contains("h0") && !ev.Intersect(Enumerable.Range(142, 8).Select(i => "h" + i)).Any(), "model cache: 150 browsed heroes shrink to the 8 most recent");
        // Models in use are never freed, however many.
        var battle = Enumerable.Range(0, 12).Select(i => new CachePolicy.Entry("b" + i, 1, 0)).Concat(Enumerable.Range(0, 3).Select(i => new CachePolicy.Entry("i" + i, 0, 0))).ToList();
        check(CachePolicy.Evict(battle, 1000, cap, hard, recent).Count == 0, "model cache: models on screen are never freed");
        // A stage's 12 enemies preloaded just now stay until the hard cap; older idle ones go first.
        var pre = Enumerable.Range(0, 12).Select(i => new CachePolicy.Entry("e" + i, 0, 995)).Concat(Enumerable.Range(0, 6).Select(i => new CachePolicy.Entry("old" + i, 0, 100 + i))).ToList();
        var ev2 = CachePolicy.Evict(pre, 1000, cap, hard, recent);
        check(ev2.Count == 6 && ev2.All(k => k.StartsWith("old")), "model cache: just-preloaded models wait for their battle; stale ones go first");
        var many = Enumerable.Range(0, 20).Select(i => new CachePolicy.Entry("r" + i, 0, 990 + i * 0.1f)).ToList();
        check(CachePolicy.Evict(many, 1000, cap, hard, recent).Count == 4, "model cache: even recent models are capped at 16 unused");
        check(CachePolicy.Evict(Enumerable.Range(0, 8).Select(i => new CachePolicy.Entry("x" + i, 0, 0)), 1000, cap, hard, recent).Count == 0, "model cache: nothing freed at or under the cap");
    }
}
