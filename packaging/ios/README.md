# iOS Packaging

Landscape iPad app sharing the desktop game and renderer. Steamworks is excluded.
Run commands from the repository root.

Partial trimming removes unused SDK and MonoGame APIs. Game assemblies and the
save formatter are preserved for reflection-based resources and save compatibility.

## Requirements

- .NET SDK from `global.json`, Xcode 26.2 with iOS support.
- iOS workload: `dotnet workload install ios --version 10.0.101.1`.
- For device installs: Apple Development certificate and development profile.
- For TestFlight: Apple Distribution certificate and App Store Connect profile.

Upgrade Xcode and the iOS workload together. Run `dotnet workload repair` if packs
are missing after installation.

## Build and sign a release

```sh
./packaging/ios/package.sh
```

Performs a clean device build, then creates
`artifacts/ios-arm64/Burntime-iPad-arm64-unsigned.zip` with the app,
available dSYM symbols, and commit ID. Keep it for crash symbolication.
The bundle ID defaults to `org.burntime`; override it with `IOS_APPLICATION_ID`.
The bundle ID must match the registered App ID and provisioning profile.

GitHub Actions only validates Debug simulator builds on PRs and manual runs.
Build release bundles locally from the release tag using the command above.

Sign locally with a certificate in Keychain and a matching profile:

```sh
IOS_SIGN_IDENTITY="Apple Distribution: Your Name (TEAMID)" \
IOS_PROVISION_PROFILE="/absolute/path/Burntime-AppStore.mobileprovision" \
  ./packaging/ios/sign-release.sh artifacts/ios-arm64/Burntime-iPad-arm64-unsigned.zip
```

The signer also accepts a downloaded release tag. It validates the profile and
signatures, then creates `Burntime-iPad-arm64-signed.ipa` beside the ZIP.
Neither script overwrites existing outputs.

Upload the IPA through Apple's Transporter, then configure TestFlight in
App Store Connect. Local signing does not confirm Apple upload or review acceptance.

## Development device

Connect and trust the iPad, enable Developer Mode, and sign into Xcode.
Find its UDID with `xcrun devicectl list devices`. Generate a development profile:

```sh
xcodebuild -project packaging/ios/SigningHelper.xcodeproj \
  -scheme SigningHelper -configuration Debug \
  -destination 'id=YOUR_IPAD_UDID' \
  -derivedDataPath /tmp/burntime-ios-signing \
  DEVELOPMENT_TEAM=YOUR_TEAM_ID \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build
```

Use the profile UUID from the build log; do not install the helper app.

```sh
dotnet build source/Burntime.iOS/Burntime.iOS.csproj \
  -c Release -r ios-arm64 -p:CodesignProvision=YOUR_PROFILE_UUID
xcrun devicectl device install app --device YOUR_IPAD_UDID \
  source/Burntime.iOS/bin/Release/net10.0-ios/ios-arm64/Burntime.app
xcrun devicectl device process launch --device YOUR_IPAD_UDID \
  org.burntime.remastered.dev
```

Development and release bundle IDs have separate saves. Updates preserve saves;
deleting the app removes them. Versions come from the nearest Git tag and commit count.

## Testing

With an iPad simulator booted:

```sh
dotnet build source/Burntime.iOS/Burntime.iOS.csproj \
  -c Release -r iossimulator-arm64
xcrun simctl install booted \
  source/Burntime.iOS/bin/Release/net10.0-ios/iossimulator-arm64/Burntime.app
SIMCTL_CHILD_BURNTIME_IOS_SMOKE_TEST=1 xcrun simctl launch --console-pty \
  booted org.burntime.remastered.dev
```

Look for `PASS` or `FAIL`. Logs are in `Documents/.config/BurntimeSmokeTest` inside
the app data container; ordinary game logs use `Documents/.config/Burntime`.
Omit the environment variable for normal play.
Add `SIMCTL_CHILD_BURNTIME_IOS_SMOKE_SAVE_ONLY=1` for a focused save/load check.

Before release, check on an iPad:

- Tap to select items/world-map locations, tap again for the primary action;
  hold for secondary actions. Dragging must not issue commands.
- Scroll the manual/save list and switch trader inventories by tapping the background.
- Cancel hold menus with a second finger or backgrounding; no action should execute.
- Save/load after restarting and updating; check audio, performance, and resume.

## App icon

Regenerate the checked-in iPad and marketing icons from the shared artwork:

```sh
swift packaging/ios/create-app-icons.swift source/Burntime.MonoGame/Icon.ico \
  source/Burntime.iOS/Assets.xcassets/AppIcon.appiconset
```
