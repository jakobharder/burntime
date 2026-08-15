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
