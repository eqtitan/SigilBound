# Borrowed from Claude Code Game Studios

These files come from [Claude Code Game Studios](https://github.com/Donchitos/Claude-Code-Game-Studios)
by Donchitos (commit abc0de7), MIT licence. See `LICENSE-claude-code-game-studios` in this folder.
They're copied as-is:

- agents: unity-specialist, unity-ui-specialist, unity-shader-specialist, technical-artist, qa-tester, performance-analyst
- skills: smoke-check, playtest-report, bug-report, perf-profile
- rules: ui-code, shader-code, gameplay-code, engine-code, test-standards

## Using them in Sigilbound

They were written for that framework's own project layout. In this repo, map their paths like this:

| They mention | In Sigilbound |
|---|---|
| `design/gdd/`, game design docs | `docs/sigilbound/` (ROADMAP.md, ART_DIRECTION.md, art/) |
| `production/`, sprints, stories, milestones | `docs/sigilbound/PLAN.md` (one step per session) |
| `project.yaml`, engine setup | Unity 6000.0.58f1, URP, project in `unity/` (see unity/README.md) |
| `src/`, game code | `unity/Assets/Sigilbound/` (Core = rules, Client = UI and 3D) |
| tests | `dotnet run --project unity/Tests~` plus the CompileCheck projects |
| running or playtesting the game | `tools/unity-shots.sh` (screenshots of the Linux preview build) |

PLAN.md always wins over these files on process: one step per session, original content only,
no paid services. Agents cost extra usage when spawned, so use them only when a step needs a
specialist.
