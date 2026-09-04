# SteamPipe upload

The upload script stages the Windows, macOS, and Linux release artifacts,
generates SteamPipe VDF files, and optionally uploads the combined build to a
non-default Steam branch.

## Prerequisites

- Download and extract the Steamworks SDK.
- Create the Windows, macOS, and Linux depots in Steamworks and publish those
  configuration changes.
- Add all three depots to the package used by testers.
- Create the target branch before uploading. The default branch name used by
  the script is `test`.
- Bootstrap SteamCMD once. On macOS, run
  `tools/ContentBuilder/builder_osx/steamcmd.sh`, then exit.

Fill in `packaging/steam/local.conf` with the three depot IDs, Steam account
name, and the absolute path to `tools/ContentBuilder`. The file is ignored by
Git. Never put a password, Steam Guard code, or SteamCMD login token in it.

## Prepare and upload

Using the artifacts from GitHub Actions is recommended because each executable
is built on its native runner and the macOS app goes through the repository's
bundle and signing process. Download the three assets for a release tag with:

```sh
./packaging/steam/download-release.sh v1.1-preview2
```

By default they are saved below `artifacts/steam-release/<tag>/`. The script
uses `curl` with public GitHub release URLs and verifies the ZIP and tar
archive; on macOS it also verifies the DMG. No GitHub authentication is needed.

Then run the upload script from the repository root:

```sh
./packaging/steam/upload.sh v1.1-preview2
```

On macOS, sign the downloaded release before uploading it:

```sh
./packaging/macos/sign-release.sh --notarize v1.1-preview2
```

This preserves the downloaded DMG and creates
`Burntime-macOS-arm64-notarized.dmg` beside it. For a test build, the upload
script prefers the notarized DMG and falls back to
`Burntime-macOS-arm64-signed.dmg`. It never uploads the unsigned DMG.

The longer form accepting an explicit ContentBuilder path and three artifact
paths remains available for one-off uploads.

The explicit macOS argument must be named either
`Burntime-macOS-arm64-signed.dmg` or
`Burntime-macOS-arm64-notarized.dmg`. Credentials and Steam Guard codes are
entered into SteamCMD and are not stored by the script.

To inspect the staged files and generated VDF scripts without uploading, omit
`STEAM_ACCOUNT` and pass `--prepare-only` before the four paths.

To run SteamCMD's SteamPipe preview, which validates the build and produces
manifests without uploading content or changing a branch, use:

```sh
./packaging/steam/upload.sh --dry-run v1.1-preview2
```

The generated app build includes `SetLive` for the selected test branch. It
does not modify Steam's `default` branch.

To upload a public candidate, requiring a notarized DMG, use:

```sh
./packaging/steam/upload.sh --public v1.1-preview2
```

This omits `SetLive`, so the build is uploaded but not made live. Assign it to
Steam's default branch manually after reviewing it. If the notarized DMG is
missing, the script exits before SteamCMD runs. `--skip-macos` intentionally
allows a Windows/Linux-only public candidate.

For a Windows/Linux-only test build, skip downloading and uploading macOS with:

```sh
./packaging/steam/download-release.sh --skip-macos v1.1-preview2
./packaging/steam/upload.sh --skip-macos v1.1-preview2
```
