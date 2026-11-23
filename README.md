# Sigilbound

An original, fully free-to-play hero-collector RPG: no shop, no paid currency, everything is earned by playing.

| Path | What it is |
|---|---|
| `public/game/` | The game (HTML/JS), playable in a browser and wrapped as the Android/iOS app |
| `game3d/` | 3D source (Three.js), bundled into `public/game/three3d.js` with `npm run build:game3d` |
| `mobile/` | Capacitor Android/iOS app; CI builds the APK and publishes it to the `android-latest` release |
| `unity/` | Native Unity 6 version (in progress; builds publish to `unity-latest`) |
| `functions/api/sb/` | Friends & chat API (Cloudflare Pages Functions + D1) |
| `docs/sigilbound/` | Roadmap, changelog and art direction |
| `tools/` | Versioning, balance sim, QA and preview tools |

**Install on Android:** https://github.com/eqtitan/sigilbound/releases/download/android-latest/Sigilbound.apk

Versioning: `node tools/sigilbound-version.cjs <major|minor|patch> "what changed"`. See `docs/sigilbound/CHANGELOG.md`.
