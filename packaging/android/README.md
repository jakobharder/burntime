# Android APK packaging

Landscape Android app sharing the game, renderer, touch gestures, and mobile
pause/resume code with iOS. Steamworks is excluded. Android 6.0/API 23 or newer
is required. Run commands from the repository root.

## Requirements

- .NET SDK from `global.json`.
- `dotnet workload install android --version 10.0.101.1`.
- Android SDK and JDK 21. To install the project's matching dependencies:

```sh
dotnet build source/Burntime.Android/Burntime.Android.csproj \
  -t:InstallAndroidDependencies \
  -p:AndroidSdkDirectory=/absolute/path/android-sdk \
  -p:JavaSdkDirectory=/absolute/path/jdk \
  -p:AcceptAndroidSdkLicenses=True
```

Set `ANDROID_HOME` and `JAVA_HOME` to those paths, or pass
`-p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...` to the packaging script.

## Build an installable APK

```sh
bash packaging/android/package.sh
```

Creates `artifacts/android-arm64/Burntime-Android-arm64-development.apk`, a SHA-256
checksum, and a commit ID. This is a self-contained Release build signed with the
local development key. It does not need a debugger or the .NET runtime installed
on the device. Existing artifacts are never overwritten.

The default application ID is `org.burntime.remastered.dev`. Override it with
`ANDROID_APPLICATION_ID`; IDs have separate saves. Updates with the same ID and
signing key preserve saves. Uninstalling removes app data.

For an x64 emulator, set `ANDROID_RUNTIME_IDENTIFIER=android-x64`. Override the
output directory with `ANDROID_OUTPUT_DIRECTORY`.

Install and launch with the SDK's `adb`:

```sh
adb install -r artifacts/android-arm64/Burntime-Android-arm64-development.apk
adb shell monkey -p org.burntime.remastered.dev -c android.intent.category.LAUNCHER 1
```

## GitHub Actions

The Android workflow builds an arm64 APK for relevant pull requests, pushes to
`main`, and manual runs. Download `Burntime-Android-arm64` from the completed run's
Artifacts section. The tag-release pipeline calls the same workflow, waits for
Android packaging to pass, and attaches the APK, checksum, and commit ID to the
existing draft GitHub release alongside the desktop builds. CI verifies the APK
signature, packaged game assets, native arm64 libraries, and checksum before upload.

No Android developer account or repository secrets are needed for these builds.
The Android SDK generates a self-signed development key on the runner and the
packaging tools sign the APK automatically. This is a Release configuration build
with a development signature, suitable for direct installation and testing.

GitHub-hosted runners are fresh machines: the development signing key changes
between runs and differs from a locally generated key. Android requires the same
application ID and signing key to install an update over an existing app. With a
different key, uninstall the previous app before installing the new APK; this
removes its local saves. For updates that preserve saves, create one permanent
keystore and configure release signing using GitHub Secrets. A Google Play
account is only needed if distributing through Google Play; the keystore itself
can be created locally without an account.

## Release signing

Create and securely retain a keystore once. Keep it outside the repository:

```sh
keytool -genkeypair -v -keystore /absolute/private/path/burntime.keystore \
  -alias burntime -keyalg RSA -keysize 2048 -validity 10000
```

Export these variables in your shell or obtain them from your secret manager:

- `ANDROID_SIGNING_KEYSTORE`: absolute keystore path.
- `ANDROID_SIGNING_KEY_ALIAS`: key alias.
- `ANDROID_SIGNING_STORE_PASSWORD`: keystore password.
- `ANDROID_SIGNING_KEY_PASSWORD`: key password.
- `ANDROID_APPLICATION_ID`: production ID, e.g. `org.burntime`.

Then run the same packaging command. It creates
`Burntime-Android-arm64-release.apk`. Passwords are referenced through environment
variables rather than placed on the command line. Retain the same key for updates.
This workflow produces APKs for direct installation; Play Store AAB publishing is
not included.

## Layout and resources

`SmallestScreenWidthDp < 600` selects the existing compact UI and fixed 160-unit
logical viewport. At 600dp and above, the app uses the larger mobile layout and
automatic scaling. The activity is recreated for screen-size changes, rebuilding
the game with the appropriate layout, including when switching foldable modes.
This returns to the startup menu; seamless preservation of an unsaved session
across activity recreation is not implemented.

On first launch after an install/update, bundled assets are copied into app-private
storage so the existing filesystem and package code can read them. A completed
copy is marked atomically, previous resources are removed after success, and saves
are stored separately. No shared-storage permission is needed.

## Validation

CI builds and uploads a development-signed arm64 APK. Before distributing, test on
an Android phone and tablet: launch, inventory/trading, tap/hold/swipe, Back,
background/resume, audio, save/load, and an update installed over an existing app.
Check the compact boundary on both sides of 600dp and foldable resizing. Native
Android mouse/trackpad integration beyond MonoGame touch input is not implemented.
