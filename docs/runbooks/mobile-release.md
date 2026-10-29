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
