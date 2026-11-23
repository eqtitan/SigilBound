# Hero art routes

How the 24 revamped heroes get made. The owner's constraints (2026-11-23):
- **No hiring.** The art is produced by agents, end to end. The "artist" route is dropped.
- Only routes an agent can run end to end: **(A) AI 3D generation by API** with automated rigging
  and animation; **(B) bought asset packs**, as a fallback the owner would purchase.
- Prep only: no accounts, keys, sign-ups or purchases yet.

**Recommendation in one line:** route A with **Meshy** on a paid plan (API, auto-rig and
animation in one service), driven by `unity/Tools~/heroforge`, with the CC0 Quaternius animation
library as the shared, consistent clip set; Tripo as the drop-in alternative; asset packs only if
the AI results fail the review. Steps for the owner are at the end.

## 1. What "agent-run end to end" means here

```
prompt (hero-prompts.json) -> generate (API) -> download GLB -> rig + animate (API)
  -> LODs (meshoptimizer) -> stage with names + provenance -> offline validation (artcheck)
  -> Unity import tool (batch mode) -> validation report -> prefab in Resources/Heroes
```

Every step is a script in this repo (`unity/ART_PIPELINE.md`). The only human steps are the
owner's: choosing the service, paying, creating the key and entering it in the session's secure
credential form, and approving the art in review.

**Mixamo is out of the automated route.** It has no official API, needs an Adobe account in a
browser, and its terms restrict redistributing raw files and bulk downloading. The agent-drivable
replacements are: the generator's own auto-rig API (Meshy rigging, Tripo `animate_rig`), and
Humanoid retargeting inside Unity (the import tool), with a CC0 clip library.

## 2. Comparison

| | A. AI 3D by API (Meshy / Tripo / Rodin) | B. Asset packs (owner buys) |
|---|---|---|
| Cost (24 heroes) | Meshy: Pro plan (help centre: $20/month, 1,000 credits) plus prepaid API credits; about 100 credits per hero with rig, weapon and 10 clips, x2-3 for rerolls, so roughly 5,000-7,000 credits. Dollar rate per API credit not found on official pages (a third-party comparison implies about $0.008, so on the order of $50-100 total). **Verify.** | one-off $25-$150 per pack; 1-3 packs |
| Time | pipeline ready now; about 15-30 min of API time per hero, run in parallel; 1-3 days for 24 heroes including review and rerolls | days to kitbash and recolour 24 heroes from modular parts, mostly scriptable |
| Quality | good stylized meshes and painted-looking textures; topology and UVs are generated (fine for mobile at 12-30k tris); hands, faces and thin props are the weak spots, so weapons are generated as separate meshes | consistent, clean, hand-made topology; but a pack's look, not ours: fewer distinct silhouettes |
| Consistency across 24 | the main risk; managed by one shared prompt template, one texture-style prompt, one shader that adds the shared lighting, and a review step | high within a pack; distinctness is the problem (shared bodies and faces) |
| Distinct silhouettes | high: every hero gets its own silhouette idea | medium-low: modular parts on 1-2 base bodies |
| Rig and animation | provider auto-rig (humanoid) + Unity Humanoid retargeting of one CC0 library; provider presets for extras | usually Mecanim Humanoid with included animations (verify per pack) |
| Licensing | paid plans: outputs owned / privately licensed (see section 3); free tiers are unsuitable | Asset Store EULA: use in games, never redistribute the raw files; seat limits on some packs |
| Originality risk | controlled by our prompts (no names, banned-terms test, no reference images of other games) | the same pack is in many other games |
| Agent can run it | yes, fully, once a key exists | yes after purchase (import, recolour, kitbash scripts), but the purchase and download need the owner |

## 3. Terms: who owns the outputs

I could not open the services' terms pages directly from the prep session (their hosts are not on
this session's network allowlist), so everything below comes from **web search results quoting the
services' own help and pricing pages**, read on 2026-11-23. **The owner must read the live terms
before paying** and keep a dated copy (screenshot or PDF) with the plan used.

| Service | Free tier | Paid tier | Commercial use | Status |
|---|---|---|---|---|
| **Meshy** | outputs under **CC BY 4.0** (commercial use allowed with credit, e.g. "Model created with Meshy - CC BY 4.0") | Pro and above: **"Private" license, user owns the output**, no attribution; models made while subscribed keep those rights after downgrading | yes on both (free needs attribution) | from Meshy's help centre and pricing docs via search; Terms of Use (updated 2026-03-07) not read in full. A third-party site says free accounts can't download most models: unconfirmed |
| **Tripo** | Tripo's help pages: **Tripo retains all rights to free users' inputs and outputs** (Terms 5.2.1), models public under CC BY 4.0; Tripo's own blog pages contradict each other on commercial use | **user holds all rights** incl. commercial use (Terms 5.2.2); not used for training | paid only, to be safe | from Tripo help/blog pages via search; full terms not read. Whether the free API credits given with a first key count as "free tier" is **unknown**: don't ship assets made with them |
| **Hyper3D Rodin** | a free plan exists; free previews of some tiers; terms for free outputs **not found** | API access appears on the **Business** plan ($120/month on the subscribe page); a third-party summary (Scenario) says Rodin outputs can be used commercially without restrictions | probably yes on paid; **unverified** | Hyper3D's own copyright-license page returned only its title; treat as unverified |
| **Mixamo** (Adobe) | free with an Adobe ID | n/a | Adobe's FAQ: characters and animations **royalty free for personal, commercial and non-profit projects, including video games**; no credit required; don't redistribute the raw files; no bulk downloading; not for training ML models | official FAQ via search; the FAQ thread people cite dates from the "technical preview" era. Not used in the automated route (no API) |

Rules the pipeline enforces: `heroforge` requires `--plan-tier` for any real provider and refuses
`free` unless `--allow-free-tier` (for throwaway tests only); `stage` writes `PROVENANCE.json`
(provider, plan tier, task ids, prompt, date) for every hero.

## 4. API capabilities (as found during prep)

| | Meshy | Tripo | Rodin |
|---|---|---|---|
| Text-to-3D | `POST /openapi/v2/text-to-3d` preview, then refine (textures); `pose_mode` a-pose/t-pose; `topology` triangle/quad; `target_polycount`, `should_remesh` | task `text_to_model` with `negative_prompt`, `face_limit`, texture options (v2 task API; a v3 API is also documented) | `POST /api/v2/rodin` with tier, `mesh_mode` (Raw/Quad), quality; status and download endpoints |
| Rigging | `POST /openapi/v1/rigging` (humanoid bipeds; under 300k faces) | task `animate_rig` | **none**: rig with Meshy or Tripo |
| Animation | animation API, library of actions by id (3 credits per action) | `animate_retarget` presets: idle, walk, run, slash, shoot, hurt, fall, jump, dive, climb, turn, chop | none |
| Credits (API) | preview 20, texture 10, rig 5, animation 3 per action (meshy-6/7 class; API pricing page via search) | about $0.01 per credit top-up (Tripo announcement); per-task costs not found | $1.5 per direct credit; per-task costs not found |
| Deprecations seen | `lowpoly` retires 2026-10-30; meshy-7 as a 3D model retires 2026-12-31 | - | - |

Field names the adapters couldn't confirm are marked `VERIFY` in
`unity/Tools~/heroforge/providers/*.mjs`. The adapters find output files by extension, so a
renamed response field doesn't break downloads; a wrong request field shows up as an HTTP 400 on
the first pilot hero.

## 5. Asset pack shortlist (fallback, not purchased)

Prices and details are from store search results on 2026-11-23 and change often; verify on the
store page. None is hand-painted in exactly our style; packs trade distinctness for speed.

| Pack | Price seen | License | Polys | Rig | Covers 24 distinct heroes? |
|---|---|---|---|---|---|
| **Synty POLYGON - Modular Fantasy Hero Characters** (Unity Asset Store #143468; also Synty store) | $74.99 on sale, $149.99 list; or Synty subscription from $30/month | Asset Store listing: "Restricted Single Entity"; Synty store: 5 seats per copy | low-poly; exact count not found | Synty characters ship Humanoid-compatible (verify) | partly: 720 modular pieces and a colour shader make 24 variants, but one flat low-poly look and shared faces; far from hand-painted |
| **Dungeon Mason - Modular RPG Hero PBR** (#124718) | $25 (snapshot) | Asset Store standard EULA | about 5,000 tris per related Dungeon Mason hero (this pack's own count not found) | Mecanim Humanoid with about 200 animations in 6 weapon stances (from a third-party repost: verify) | partly: stylized PBR, modular, mobile-light; good for weapon-stance animations; silhouettes limited by one body |
| **Modular Stylized Character 2 - Fantasy** (#294319) | $40 | Asset Store standard EULA | not found | Humanoid rigs, facial blendshapes | low-medium: one male, one female base with fantasy outfits; heavy kitbashing for 24 |
| **Customizable Adventure Hero** (#20199) | $40 | Asset Store standard EULA | "low polygon" | rigged and animated (Humanoid not stated) | low: one customizable hero, hand-painted textures |
| **Character Pack - Fantasy RPG** (#141079) | price not shown | Asset Store standard EULA | not found | Mecanim Humanoid | low: 8 hand-painted characters for 24 heroes |
| **Quaternius Modular Character Outfits - Fantasy + Universal Base Characters** | free / pay what you want ($15 minimum for the Source version) | **CC0** | low-poly stylized | Humanoid rig | medium: 12 outfits from 62 parts on 6 base bodies; same family as today's models; zero license risk, but not the "top mobile" fidelity the owner wants |

If route B is ever used: Dungeon Mason (stances and animations) or Synty (breadth) are the
practical picks; expect to restyle with our CharacterPainted shader and gear sockets, and accept
less distinct silhouettes.

## 6. Recommendation

**Route A, Meshy, paid plan with API credits.** Reasons:
1. One service covers generation, textures, humanoid auto-rigging and animations, so the agent
   needs one key and one set of terms.
2. Paid outputs are privately licensed to the user (owned), per Meshy's own help pages.
3. T-pose/A-pose generation and a polycount target match our rig-friendly prompts and budgets.
4. The cost for 24 heroes with rerolls is small next to any alternative (verify the credit rate).

Supporting choices:
- **Animation consistency:** use the CC0 Quaternius Universal Animation Library (already credited
  in `unity/Assets/StreamingAssets/Models/CREDITS.md`; re-download from quaternius.com before a
  store release) as the shared Humanoid library (`SB_Anim_<State>.fbx` and per-archetype sets),
  retargeted by Unity. Meshy's animation actions are optional per-hero extras.
- **Weapons as separate meshes** (`SB_Gear_<id>_Weapon.glb`), never fused to hands.
- **Pilot first:** 2 heroes of different builds (Ysolde and Gorrum) before the batch.
- **Alternative:** Tripo on a paid plan (same flow, `--provider tripo`). Rodin only for the
  generation step and only with a second provider's rig API, so not recommended as the primary.
- **Fallback:** if after two reroll rounds a hero still fails the review, either keep the current
  code-built hero for that slot (the game falls back automatically) or buy one pack from
  section 5 for those heroes only.

## 7. Exact steps for the owner (Meshy route)

1. **Read and save the terms.** Open Meshy's Terms of Use and pricing page; confirm that the paid
   plan gives you ownership / a private license of outputs and that the API is included. Save a
   dated PDF or screenshot (keep it outside the repo or in a private place).
2. **Subscribe and buy API credits.** Create the Meshy account, choose the plan that includes API
   access (Pro or above per the help centre), and buy API credits (about 7,000 to be comfortable;
   check the current per-credit price; set any spending cap the dashboard offers).
3. **Create an API key** in Meshy's API settings. Don't paste it into chat, a file or GitHub.
4. **Start a Claude Code session on this repo** and ask: "Generate the pilot heroes with
   heroforge (Meshy, paid plan)." The agent will:
   - ask for the key through the session's **secure credential form** for `api.meshy.ai` (you paste
     it there; it is stored as a network secret, never in the repo);
   - propose allowing the Meshy hosts it needs (`api.meshy.ai` and the download host the first
     response reveals), which you approve;
   - run `node unity/Tools~/heroforge/heroforge.mjs all --provider meshy --plan-tier paid --proxy-auth --heroes ysolde,gorrum`.
5. **Review the pilot** (screenshots, the import report and the checklist in ART_BIBLE section
   12). Say "go" or what to change; prompt changes go into `hero-prompts.json`.
6. **Batch the rest:** the agent runs the remaining 22 heroes, rerolls failures, and commits the
   staged files and reports on a branch.
7. **Unity import:** where a Unity 6000.0.58f1 editor is available (your computer, or a CI job
   using the existing `UNITY_EMAIL` / `UNITY_PASSWORD` secrets), run the batch import command
   that heroforge prints, or open the project and use **Sigilbound > Art > Import Hero Folder**.
   Heroes that pass appear in the game; failing ones stay on today's look until fixed.
