# macOS Packaging

## Build

Build an unsigned DMG for Apple Silicon:

```sh
./packaging/macos/package.sh arm64
```

The result is written to `artifacts/macos-arm64/`.

Intel builds are still supported with `./packaging/macos/package.sh x64`.

## Signing

Without a Developer ID the app uses an ad-hoc signature.

To sign a release, pass the full certificate name from Keychain:

```sh
MACOS_SIGN_IDENTITY="Developer ID Application: Name (TEAMID)" \
  ./packaging/macos/package.sh arm64
```

The DMG must also be notarized and stapled before release.

## Sign a downloaded release

The Steam release downloader saves the GitHub-built DMG below
`artifacts/steam-release/<tag>/`. Re-sign it locally without modifying the
downloaded file:

```sh
./packaging/macos/sign-release.sh v1.1-rc7
```

This creates `Burntime-macOS-arm64-signed.dmg` beside the downloaded DMG.
The signature is created locally; the app is not uploaded. `codesign` contacts
Apple's timestamp service to add the secure timestamp required for release.

To submit the app and DMG to Apple's notarization service and staple both
tickets, explicitly add `--notarize`:

```sh
./packaging/macos/sign-release.sh --notarize v1.1-rc7
```

This creates `Burntime-macOS-arm64-notarized.dmg`; the signed-only and original
DMGs remain untouched.

The command uses the `burntime` notarytool keychain profile by default. Set
`MACOS_NOTARY_PROFILE` to use another profile.
