import java.io.FileInputStream
import java.util.Properties

plugins {
    id("com.android.application")
    // The Flutter Gradle Plugin must be applied after the Android and Kotlin Gradle plugins.
    id("dev.flutter.flutter-gradle-plugin")
}

// Release signing (D297, docs/runbooks/mobile-release.md). The upload key comes from `android/key.properties`
// (git-ignored; storeFile is relative to `android/` or absolute) or, when that file is absent, from the
// FRESH_ORBIT_UPLOAD_* environment variables (CI secrets). Nothing falls back to the debug key: without all four
// values a release build stops with the message below (rule 7). Debug and profile builds are not affected.
val keyProperties = Properties().apply {
    val file = rootProject.file("key.properties")
    if (file.exists()) FileInputStream(file).use { load(it) }
}

fun releaseSetting(property: String, environment: String): String? =
    (if (keyProperties.isEmpty()) System.getenv(environment) else keyProperties.getProperty(property))
        ?.takeIf { it.isNotBlank() }

val releaseSettings = linkedMapOf(
    "storeFile (FRESH_ORBIT_UPLOAD_STORE_FILE)" to releaseSetting("storeFile", "FRESH_ORBIT_UPLOAD_STORE_FILE"),
    "storePassword (FRESH_ORBIT_UPLOAD_STORE_PASSWORD)" to
        releaseSetting("storePassword", "FRESH_ORBIT_UPLOAD_STORE_PASSWORD"),
    "keyAlias (FRESH_ORBIT_UPLOAD_KEY_ALIAS)" to releaseSetting("keyAlias", "FRESH_ORBIT_UPLOAD_KEY_ALIAS"),
    "keyPassword (FRESH_ORBIT_UPLOAD_KEY_PASSWORD)" to releaseSetting("keyPassword", "FRESH_ORBIT_UPLOAD_KEY_PASSWORD"),
)
val missingReleaseSettings = releaseSettings.filterValues { it == null }.keys
val releaseStoreFile = releaseSettings.values.first()?.let { rootProject.file(it) }
if (releaseStoreFile != null && !releaseStoreFile.exists()) {
    throw GradleException("Release signing: the keystore file $releaseStoreFile does not exist; fix storeFile in android/key.properties or FRESH_ORBIT_UPLOAD_STORE_FILE.")
}
val releaseSigningReady = missingReleaseSettings.isEmpty()

// Demo signing (D297, MA-15): the demo APK workflow sets FRESH_ORBIT_DEMO_* from repository secrets, so every demo
// APK carries the same key and installs over the previous one. Without them, debug and profile builds keep the
// machine's own debug key (the workflow says so in its summary).
val demoSettings = listOf(
    "FRESH_ORBIT_DEMO_STORE_FILE",
    "FRESH_ORBIT_DEMO_STORE_PASSWORD",
    "FRESH_ORBIT_DEMO_KEY_ALIAS",
    "FRESH_ORBIT_DEMO_KEY_PASSWORD",
).associateWith { System.getenv(it)?.takeIf { value -> value.isNotBlank() } }
val demoSigningRequested = demoSettings.values.any { it != null }
if (demoSigningRequested && demoSettings.values.any { it == null }) {
    val missing = demoSettings.filterValues { it == null }.keys.joinToString()
    throw GradleException("Demo signing: set all four FRESH_ORBIT_DEMO_* variables or none; missing: $missing.")
}

android {
    namespace = "in.freshorbit.app"
    compileSdk = flutter.compileSdkVersion
    ndkVersion = flutter.ndkVersion

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }

    defaultConfig {
        applicationId = "in.freshorbit.app"
        // You can update the following values to match your application needs.
        // For more information, see: https://flutter.dev/to/review-gradle-config.
        minSdk = flutter.minSdkVersion
        targetSdk = flutter.targetSdkVersion
        // Uses the version code from pubspec.yaml. When using split APKs, 1000 * ABI_VERSION
        // is added automatically by Flutter. (https://developer.android.com/studio/build/configure-apk-splits#configure-APK-versions)
        // You can force using the value of versionCode by specifying the `-P force-version-code-ignoring-abi=true`
        // flag during build.
        versionCode = flutter.versionCode
        versionName = flutter.versionName
        // App Links host (D308, MB-4): the website's domain, from -PfreshOrbitAppLinkHost=shop.example.in or the
        // FRESH_ORBIT_APP_LINK_HOST environment variable. Unset, a reserved .invalid name: no link opens the app and
        // nothing is guessed (rule 7). Steps: docs/runbooks/mobile-release.md, "App links".
        manifestPlaceholders["appLinkHost"] =
            (project.findProperty("freshOrbitAppLinkHost") as String?)?.takeIf { it.isNotBlank() }
                ?: System.getenv("FRESH_ORBIT_APP_LINK_HOST")?.takeIf { it.isNotBlank() }
                ?: "app-links-not-configured.invalid"
    }

    signingConfigs {
        if (demoSigningRequested) {
            // Flutter's profile build type starts as a copy of debug, so it shares this signing config.
            getByName("debug") {
                storeFile = file(demoSettings.getValue("FRESH_ORBIT_DEMO_STORE_FILE")!!)
                storePassword = demoSettings.getValue("FRESH_ORBIT_DEMO_STORE_PASSWORD")
                keyAlias = demoSettings.getValue("FRESH_ORBIT_DEMO_KEY_ALIAS")
                keyPassword = demoSettings.getValue("FRESH_ORBIT_DEMO_KEY_PASSWORD")
            }
        }
        if (releaseSigningReady) {
            create("release") {
                storeFile = releaseStoreFile
                storePassword = releaseSettings.values.elementAt(1)
                keyAlias = releaseSettings.values.elementAt(2)
                keyPassword = releaseSettings.values.elementAt(3)
            }
        }
    }

    buildTypes {
        release {
            // Never the debug key (D297). Without the upload key the release package task fails below.
            signingConfig = if (releaseSigningReady) signingConfigs.getByName("release") else null
        }
    }
}

// A release APK or bundle without the upload key stops here with the list of missing values, instead of
// producing an unsigned or debug-signed package. Only the release packaging tasks check, so debug and profile
// builds (and `flutter analyze`) never need the key.
if (!releaseSigningReady) {
    val message = "Release signing is not configured. Missing: ${missingReleaseSettings.joinToString()}. " +
        "Create android/key.properties or set the FRESH_ORBIT_UPLOAD_* environment variables " +
        "(docs/runbooks/mobile-release.md). Release builds are never signed with the debug key."
    tasks.matching { it.name == "packageRelease" || it.name == "packageReleaseBundle" || it.name == "signReleaseBundle" }
        .configureEach { doFirst { throw GradleException(message) } }
}

kotlin {
    compilerOptions {
        jvmTarget = org.jetbrains.kotlin.gradle.dsl.JvmTarget.JVM_17
    }
}

flutter {
    source = "../.."
}
