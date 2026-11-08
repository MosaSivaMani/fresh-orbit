# Mobile app signing and release builds

How the Android app is signed (D297). Two keys, kept apart:

| Key | Signs | Where it lives | Who makes it |
|---|---|---|---|
| **Demo key** | The demo APKs from `.github/workflows/mobile-demo.yml` (profile builds) | Four GitHub Actions repository secrets `DEMO_*` | The owner, once (part 1) |
| **Upload key** | Release builds for Google Play (`flutter build appbundle --release`) | `apps/mobile/android/key.properties` + a `.jks` file on the build machine, or `FRESH_ORBIT_UPLOAD_*` environment variables in CI | The owner, before the first Play upload (part 2) |

Never commit a keystore, `key.properties` or a password. `apps/mobile/android/.gitignore` ignores `key.properties`,
`*.jks` and `*.keystore`. Never use the upload key for demo builds.

A release build **without** the upload key stops with "Release signing is not configured. Missing: …"; it is never
signed with the debug key (rule 7). The demo workflow proves this on every run (step "A release build without the
upload key is refused").

---

## Part 1. The demo key (so a new demo APK installs over the old one)

Without it, every workflow run signs with a new throwaway key, and the phone says "App not installed" until the
old APK is uninstalled (MA-15). The workflow then shows the warning **No demo signing key**.

Do this on your own computer (or in a GitHub Codespace terminal), not on the company laptop. `keytool` comes with
Java.

1. Make the demo keystore. Choose one password (at least 6 characters) and type it when asked:

   ```sh
   keytool -genkeypair -v -keystore fresh-orbit-demo.jks -storetype PKCS12 \
     -keyalg RSA -keysize 2048 -validity 10000 -alias demo \
     -dname "CN=Fresh Orbitt demo, O=Fresh Orbitt, C=IN"
   ```

   A PKCS12 keystore uses the same password for the store and the key.

2. Turn it into one line of text:

   ```sh
   base64 -i fresh-orbit-demo.jks | tr -d '\n' > fresh-orbit-demo.b64     # macOS
   base64 -w0 fresh-orbit-demo.jks > fresh-orbit-demo.b64                  # Linux / Codespace
   ```

3. Add four repository secrets. On GitHub open the repository › **Settings** › **Secrets and variables** ›
   **Actions** › **New repository secret**, once for each:

   | Name | Value |
   |---|---|
   | `DEMO_KEYSTORE_BASE64` | the whole content of `fresh-orbit-demo.b64` |
   | `DEMO_KEYSTORE_PASSWORD` | the password from step 1 |
   | `DEMO_KEY_ALIAS` | `demo` |
   | `DEMO_KEY_PASSWORD` | the same password from step 1 |

   Or with the GitHub CLI, from the folder with the files:

   ```sh
   gh secret set DEMO_KEYSTORE_BASE64 < fresh-orbit-demo.b64
   gh secret set DEMO_KEYSTORE_PASSWORD
   gh secret set DEMO_KEY_ALIAS --body demo
   gh secret set DEMO_KEY_PASSWORD
   ```

4. Keep `fresh-orbit-demo.jks` and the password in your password manager; delete `fresh-orbit-demo.b64`.
5. Build a new demo APK: **Actions** › **Mobile demo APK** › **Run workflow** on `main`. The run summary says
   "Signed with the stable demo key" and lists the certificate SHA-256; it stays the same on every later run.
6. On the phone, uninstall the old demo app **once** (it was signed with an old throwaway key), then install the
   new APK. From then on, every new demo APK installs over the previous one and keeps the sign-in.

If only some of the four secrets exist, the workflow stops with **Demo signing key incomplete** and names the
missing ones. Secrets are not given to pull requests from forks, so those runs use the throwaway key and warn.

---

## Part 2. The upload key (Google Play release builds)

Google Play App Signing keeps the real app signing key; we sign uploads with an **upload key**. If the upload key
is lost, Play can reset it, but keep a backup anyway.

1. Make the upload keystore (on the build machine, not the company laptop):

   ```sh
   keytool -genkeypair -v -keystore upload.jks -storetype PKCS12 \
     -keyalg RSA -keysize 2048 -validity 10000 -alias upload \
     -dname "CN=Fresh Orbitt, O=Fresh Orbitt, C=IN"
   ```

2. Either put it next to the Android project and write `apps/mobile/android/key.properties`:

   ```properties
   storeFile=upload.jks
   storePassword=<the password>
   keyAlias=upload
   keyPassword=<the password>
   ```

   (`storeFile` is relative to `apps/mobile/android/`, or an absolute path.)

   Or, in CI, set these environment variables (from secrets; decode the keystore to a file first, like the demo
   step in `mobile-demo.yml`). They are read only when `key.properties` does not exist:

   | Variable | Value |
   |---|---|
   | `FRESH_ORBIT_UPLOAD_STORE_FILE` | path of the decoded `.jks` |
   | `FRESH_ORBIT_UPLOAD_STORE_PASSWORD` | store password |
   | `FRESH_ORBIT_UPLOAD_KEY_ALIAS` | `upload` |
   | `FRESH_ORBIT_UPLOAD_KEY_PASSWORD` | key password |

3. Build the bundle with both addresses (no default exists for either; D299):

   ```sh
   cd apps/mobile
   flutter build appbundle --release \
     --dart-define=API_BASE_URL=https://<api host> \
     --dart-define=WEBSITE_BASE_URL=https://<website host>
   ```

   `WEBSITE_BASE_URL` makes the privacy policy, terms and other policies reachable from the sign-in, Settings and
   Help, which Google Play's User Data policy requires. Without it those links are hidden.

4. In Play Console create the app `in.freshorbit.app`, enrol in **Play App Signing**, and upload the bundle to the
   internal testing track first. Give Play the privacy policy address `https://<website host>/support/privacy-policy`.

## Launcher icons

The icons come from the owner's logo (D119, D298): `docs/design/brand/logo-master.png`, mark in a white disc on the
brand's ocean teal `#073B4C`. Android: legacy `ic_launcher.png` and `ic_launcher_round.png` in every `mipmap-*`
density, and an adaptive icon (`mipmap-anydpi-v26/`, foreground `ic_launcher_foreground.png`, background colour
`ic_launcher_background` in `values/colors.xml`). iOS: every size in `AppIcon.appiconset/Contents.json`, opaque.
The Play Store listing icon (512 × 512) is `docs/design/brand/app-icon-512.png`.

## Store build (D309, MA-7)

`.github/workflows/mobile-release.yml` builds the signed Google Play bundle. Manual only: **Actions › Mobile release ›
Run workflow** on `main`. Once, before the first run, the owner adds:

| Kind | Name | Value |
|---|---|---|
| Secret | `UPLOAD_KEYSTORE_BASE64` | `base64 -i upload.jks` of the upload key from part 2 |
| Secret | `UPLOAD_KEYSTORE_PASSWORD`, `UPLOAD_KEY_ALIAS`, `UPLOAD_KEY_PASSWORD` | as in part 2 |
| Variable | `API_BASE_URL` | the production API origin |
| Variable | `WEBSITE_BASE_URL` | the production website origin (policy links, D299) |
| Variable | `APP_LINK_HOST` | the website host only, for example `shop.example.in` (App links, below) |
| Variable (optional) | `CRASH_REPORT_DSN` | the GlitchTip / Sentry DSN (Crash reports, below); empty = device log only |

The run refuses to start when a required value is missing. It uploads two artifacts: the `.aab` (upload it in Play
Console › Internal testing) and the `debug-info` symbols (keep them: `flutter symbolize -i <stack> -d debug-info/`
turns an obfuscated crash stack back into file and line). The build number is the run number plus the
`build-number-offset` input (default 100); raise the offset if Play says the version code was used.

## App links (D308, MB-4)

Website links (`/products/...`, `/shop/...`, `/orders/...`, `/search`, `/cart`) open the app once the website proves
it belongs with the app. Until then the app is built with the reserved host `app-links-not-configured.invalid` and
every link opens the browser.

1. Choose the host: the website's domain without `https://` (the same one as `WEBSITE_BASE_URL`).
2. **Android.** Set it for the build: the repository variable `APP_LINK_HOST` (store build), or
   `flutter build appbundle ... ` with the environment variable `FRESH_ORBIT_APP_LINK_HOST=<host>` (or
   `-PfreshOrbitAppLinkHost=<host>` to Gradle). Then serve this file from the website at
   `https://<host>/.well-known/assetlinks.json` (content type `application/json`, no redirect):

   ```json
   [{
     "relation": ["delegate_permission/common.handle_all_urls"],
     "target": {
       "namespace": "android_app",
       "package_name": "in.freshorbit.app",
       "sha256_cert_fingerprints": ["<SHA-256 of the app signing key>"]
     }
   }]
   ```

   With Play App Signing the fingerprint is the **app signing key** from Play Console › Setup › App signing (not the
   upload key). Add the demo key's SHA-256 too if demo APKs should open links. Check with
   `adb shell pm get-app-links in.freshorbit.app` (state `verified`).
3. **iOS** (needs the Apple team, below). Set `APP_LINK_HOST = <host>` in `apps/mobile/ios/Flutter/AppLinks.xcconfig`,
   turn on **Associated Domains** for the App ID, and serve `https://<host>/.well-known/apple-app-site-association`
   (no extension, `application/json`, no redirect):

   ```json
   { "applinks": { "details": [ { "appIDs": ["<TEAM_ID>.in.freshorbit.app"],
     "components": [ { "/": "/products/*" }, { "/": "/shop*" }, { "/": "/orders*" }, { "/": "/search*" }, { "/": "/cart" } ] } ] } }
   ```

4. Both files belong to the website (`apps/web/public/.well-known/`); add them in a website change once the domain and
   the fingerprints are known. Test: tap a product link in an SMS or WhatsApp on a phone with the app installed, cold
   start and warm.

## Crash reports (D309, ME-7, MA-5)

Without `CRASH_REPORT_DSN` errors go to the device log only (`adb logcat -s fresh_orbit.crash`). To collect them:
run GlitchTip (free, MIT; one Docker container plus Postgres and Redis, on the same server as the API, or its free
hosted plan), create a project, and copy its DSN (`https://<key>@<host>/<project>`) into the repository variable
`CRASH_REPORT_DSN`. The app sends only scrubbed text: no phone numbers, codes, tokens, e-mail addresses or user ids.

## Push notifications: what is ready (MA-6)

Code side, done without an account: the `PushService` seam (`lib/core/push_service.dart`, no-op today), the
device-token calls after sign-in and sign-out, and the Android 13 `POST_NOTIFICATIONS` permission in the manifest
(asked at a natural moment once push works, never at launch). Owner side, still needed: the Firebase project and the
APNs key; then follow `docs/design/mobile.md` §10.

## iOS team (MA-4)

No archive can be made until the owner's Apple Developer account exists. Then: open `apps/mobile/ios/Runner.xcworkspace`
in Xcode on a Mac, Runner target › Signing & Capabilities › Team, keep **Automatically manage signing**, add
**Associated Domains** (`applinks:<host>`, App links above) and, with push, **Push Notifications**. Xcode writes
`DEVELOPMENT_TEAM` into `project.pbxproj`; commit that change.
