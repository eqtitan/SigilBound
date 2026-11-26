# Google Play internal testing: what the owner does

This is the path to put Sigilbound on a Play **internal testing** track (up to 100 testers, no review wait).
Everything here needs your Google account; the repository side is ready except the items under
"Decisions" below.

## Decisions first (read before uploading)

1. **The in-app updater can't ship on Google Play.** Today's APK downloads new builds from GitHub and installs
   them itself, using the `REQUEST_INSTALL_PACKAGES` permission (`Editor/AndroidUpdatePermission.cs`,
   `Client/Updater.cs`). Play's policies forbid an app updating itself outside Play and restrict that
   permission. A Play build must turn both off: proposed below as a `SB_STORE` build (the GitHub APK keeps
   its updater).
2. **Signing.** CI signs the APK with `mobile/android/app/sigilbound-test.keystore`, which is committed to the
   repository with its password in `BuildScript.cs`. That's fine for testing, but anyone could sign an app
   with it. For Play:
   - use **Play App Signing** (Google holds the real app key), and
   - create a **new private upload key** that is never committed; store it as GitHub secrets.
3. **Package name.** `com.sigilbound.unity` (`ProjectSetup.AppId`) becomes permanent once uploaded. Decide
   now if the store app should be `com.sigilbound.game` or similar. A different name also lets the store
   app and the GitHub test app sit side by side.
4. **AAB.** Play requires an **Android App Bundle (.aab)**, not an APK. Hero models are about 130 MB packed,
   under Play's 200 MB base-module limit for the compressed download, so no asset packs are needed yet.

## One-time setup

1. Go to <https://play.google.com/console>, pay the one-time US$25 developer fee and verify your identity
   (personal account; it can take a few days).
2. **Create app**: name *Sigilbound*, default language English, **Game**, **Free**, accept the declarations.
3. **Make an upload key** on your computer (keep the file and passwords private; don't put them in the repo):
   `keytool -genkeypair -v -keystore sigilbound-upload.jks -alias upload -keyalg RSA -keysize 2048 -validity 10000`
4. Add four **GitHub secrets** (repo → Settings → Secrets and variables → Actions):
   `ANDROID_UPLOAD_KEYSTORE_BASE64` (`base64 -w0 sigilbound-upload.jks`), `ANDROID_UPLOAD_KEYSTORE_PASS`,
   `ANDROID_UPLOAD_KEY_ALIAS` (`upload`), `ANDROID_UPLOAD_KEY_PASS`.
5. Ask me to add the **store build** (below). It produces `Sigilbound-store.aab` as a CI artifact.

## Store listing (fill once)

- **Main store listing**: text from `LISTING.md`; icon `icon_512.png`; feature graphic `feature_1024x500.png`;
  2–8 phone screenshots from `screenshots/` (recapture from a current build first).
- **App content**: privacy policy URL (the published `PRIVACY.md` page; fill in your contact email first),
  ads: no, app access: all features available without login, content rating questionnaire (answers in
  LISTING.md), target audience 13+, data safety (answers in LISTING.md), government apps: no.

## Each test release

1. Play Console → **Testing → Internal testing → Create new release**.
2. First time: accept **Play App Signing** (Google manages the app signing key; your upload key signs uploads).
3. Upload `Sigilbound-store.aab` from the latest CI run (Actions → the run → Artifacts).
4. Release name: the build number; release notes: paste the latest "What's new" entry.
5. **Save → Review release → Start rollout to Internal testing**.
6. **Testers** tab: create an email list (e.g. you and your brother), save, and copy the **opt-in link**.
   Testers open it on their phone, accept, then install from the Play Store link it shows.
7. Each new upload needs a higher **version code**. CI already sets it from the build number.

## Proposed CI change (not made yet: needs your secrets and the decisions above)

- `BuildScript.AndroidStore()`: like `Android()`, plus
  - `EditorUserBuildSettings.buildAppBundle = true`;
  - the scripting define `SB_STORE`, under which `Updater.Boot` returns at once and
    `AndroidUpdatePermission` adds neither the permission nor the APK provider;
  - signing with the upload key read from environment variables `ANDROID_UPLOAD_*` (decoded from the
    secrets into a temp file in the workflow);
  - optionally the store package name.
- A new workflow job `android-store` (manual `workflow_dispatch`, or on tags `store-*`) that runs that build
  with game-ci (`androidExportType: androidAppBundle`) and uploads the `.aab` as an artifact. It stays out of
  the per-commit pipeline so normal pushes don't build twice.
- Later, optionally: upload to the internal track automatically with a Play service account
  (`r0adkll/upload-google-play`), once the first manual upload is done.
