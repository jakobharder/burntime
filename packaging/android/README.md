# Android APK

Requires the SDK from `global.json`, Android SDK, JDK 21, and:

```sh
dotnet workload install android --version 10.0.101.1
```

Set `ANDROID_HOME` and `JAVA_HOME`. From the repository root:

```sh
bash packaging/android/package.sh
adb install -r artifacts/android-arm64/Burntime-Android-arm64-development.apk
```

Targets Android 6+ on arm64. Outputs APK, checksum, and commit ID under
`artifacts/android-arm64/`. Default app ID: `org.burntime.remastered.dev`.
Local builds use a development key. For permanent signing, set
`ANDROID_SIGNING_KEYSTORE`, `ANDROID_SIGNING_KEY_ALIAS`,
`ANDROID_SIGNING_STORE_PASSWORD`, and `ANDROID_SIGNING_KEY_PASSWORD`;
the output filename ends in `-release.apk`.

GitHub Actions uploads `Burntime-Android-arm64`; tagged releases attach it to the
draft release. PRs use temporary keys. Main, manual, and release builds require:

- `ANDROID_SIGNING_KEYSTORE_BASE64`: base64 PKCS12 keystore, alias `burntime`.
- `ANDROID_SIGNING_PASSWORD`: shared keystore/key password.

Keep the permanent key backed up privately. Updates require the same app ID and
signing key; uninstalling removes saves.
