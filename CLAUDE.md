# Sigilbound - instructions for every Claude session and agent

## The project brain (read first, write last)
`docs/sigilbound/brain/` is the shared memory of mistakes and solutions.
1. **Before starting:** read `docs/sigilbound/brain/MAP.md`, then every lesson in `LESSONS.md` linked from the
   systems you will touch. Follow their Prevention rules. If your plan repeats a known mistake, stop and say so.
2. **When finishing or blocked:** add or update lessons (format in `brain/README.md`) for anything that broke,
   fooled a check, cost time or surprised you; link them (Related, Systems, Evidence) and update MAP.md.
   Mention the lesson ids you added in your final report.

## Standing rules (each comes from a lesson)
- Before tests: `cd unity/Tools~/models && node prepare-models.mjs`. Push only when the LAST line of
  `cd unity/Tests~ && dotnet run -c Release` is "All checks passed." and CompileCheck has 0 errors (L001).
- Agents push their own branch only; the lead merges to main after owner approval (L014).
- Visual changes are proven in a real build (workflow_dispatch + smoke artifact), not only offline previews (L004).
  Dispatch once and wait - the CI queue keeps one pending run (L005).
- Credit generated assets in `public/game/models/CREDITS.md` (prepare-models copies it) (L007).
- Hero motion work follows `.claude/agents/character-td.md`: every frame, visual verdict (L008).
- Every hero gets the physics standard (body colliders + classified spring chains) as part of rigging (L033).
- After roster, enemy-pool, speed or curve changes run `-- campaign 60` and `-- roles` (L006, L020).
- Changelog lines start with new:/fix:/change:/balance:; check PLAN step numbers after merges (L013).
- Never put keys, tokens or personal data in the repo or the brain.
