# The project brain

Sigilbound's shared memory: every mistake we made, what really caused it, how we fixed it, and how we stop it
coming back - linked to each other and to the systems they touch. Every agent and every session uses it.

**Before you start work:** read `MAP.md` for the systems you will touch, then every lesson linked there.
If a lesson says "always do X", do X. If you are about to do something a lesson warns about, stop and say so.

**When you finish (or hit a problem):** add or update lessons in `LESSONS.md`:
- A new lesson for anything that cost time, broke a build, fooled a check, or surprised you.
- Update an existing lesson when you learn more (root cause found, better fix, it happened again).
- Link it: `Related:` other lesson ids, `Systems:` names from MAP.md, `Evidence:` commits, reports, files.
- Add it to the system rows in `MAP.md` (and the graph if it connects two systems).
Keep entries short and factual. Never put keys, tokens, emails or other secrets here.

**Entry format** (the tests check it - `unity/Tests~` BrainTests):

```
## L042 - Short title
- Status: solved | open | decision
- Systems: hero-models, ci
- Symptom: what we saw
- Cause: the real root cause (not the first guess)
- Fix: what fixed it (commit / file)
- Prevention: the test, guard, rule or checklist item that stops a repeat
- Related: L002, L016
- Evidence: commit abc1234, docs/sigilbound/qa/inhouse-....md
```
