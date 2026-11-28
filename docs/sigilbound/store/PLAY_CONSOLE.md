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
3. **Package name.** Decided 2026-11-28: the store bundle is `com.sigilbound.game` (`BuildScript.StoreAppId`,
   permanent once uploaded). The GitHub test app keeps `com.sigilbound.unity`, so both sit side by side. The
   old Capacitor test app also used `com.sigilbound.game`; uninstall it before installing from Play.
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

## Store build (ready; needs your upload key)

The build is in place: `BuildScript.AndroidStore()` makes a Play **app bundle (.aab)** signed only with your
private upload key, and leaves the in-app updater out (define `SB_STORE`). The GitHub APK is unchanged.

1. **Make your upload key** once, on your own computer (keep the file and passwords safe and private;
   never commit them):

       keytool -genkeypair -v -keystore sigilbound-upload.keystore -alias upload -keyalg RSA -keysize 2048 -validity 10000

2. **Add four repository secrets** (GitHub > the repo > Settings > Secrets and variables > Actions):
   - `ANDROID_UPLOAD_KEYSTORE_BASE64`: the keystore file as base64 (`base64 -w0 sigilbound-upload.keystore`
     on Linux, `base64 -i sigilbound-upload.keystore` on macOS, `certutil -encode` on Windows, then paste
     the text without the header lines);
   - `ANDROID_UPLOAD_KEYSTORE_PASS`: the keystore password;
   - `ANDROID_UPLOAD_KEY_ALIAS`: `upload` (or the alias you chose);
   - `ANDROID_UPLOAD_KEY_PASS`: the key password (often the same as the keystore password).
3. **Build it**: Actions > Sigilbound Unity > Run workflow > task **build-store-aab**. When it finishes, download
   the `sigilbound-store-aab` artifact from the run page; that `.aab` is what you upload to the Play Console.
4. In the Play Console, turn on **Play App Signing** (the default) so Google holds the app signing key and your
   upload key can be reset if it is ever lost.
5. Package name: the bundle is `com.sigilbound.game` (it can never change after the first upload). Friends and
   chat aren't in the store build for v1, so the Data safety form is "no data collected" (READINESS.md item 8).

Later, optionally: upload to the internal track automatically with a Play service account, once the first
manual upload is done.
