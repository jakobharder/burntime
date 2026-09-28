# Personal iPad test build

This is a landscape iPad development app, sharing the desktop game and renderer.
Items use a single tap to show the normal tooltip, a second tap on the selected item for the primary
action, and a 0.5-second long press for the secondary action. The second tap has
no timing limit and can land anywhere on the selected item/location.
World-map taps select and show name/textbars; tap the selected location again to Travel/Enter and
long press for Info/Radio. Location-map taps and long-press context menus retain
their existing behavior. Location entrances always show small touch markers; all
nearby entrance names fade in around the selected character. Tap a marker or name
to walk there and enter. The chosen entrance stays highlighted until that command
ends or is replaced. Moving before the hold threshold pans the map or scrolls
the manual/save list. Touch has no mouse cursor. Player/save names use their
defaults without native text entry. Steamworks and optional desktop shaders are excluded.

On narrow trader layouts, tap the exchange background to switch between player and
trader inventories. Occupied exchange slots use the item gestures. The switching
tap never changes an offer. Buttons use
touch targets whose width and height are independently at least 25 game pixels,
without changing their visible size. For example, 100-by-8 becomes 100-by-25,
8-by-100 becomes 25-by-100, and 32-by-32 remains unchanged. Direct hits beat
expanded targets, and overlapping padding picks the nearest control. Item hit
areas retain their original size.

The prototype uses Mono AOT compilation with interpreter fallback for dynamic
code (`MtouchInterpreter=-all`) and preserves the game assemblies to support
the existing reflection-based state system and BinaryFormatter saves.
LLVM's extra optimization pass is disabled for practical development build times;
the assemblies are still compiled ahead of time into native code.
This is deliberately not a trimmed/NativeAOT or App Store release configuration.

## App icon

The iPad app icon uses the same artwork and dark background as the macOS build.
The checked-in asset catalog includes all iPad icon sizes and an opaque 1024-pixel
marketing icon. To regenerate it after updating the shared artwork:

```sh
swift packaging/ios/create-app-icons.swift source/Burntime.MonoGame/Icon.ico \
  source/Burntime.iOS/Assets.xcassets/AppIcon.appiconset
```

## Toolchain

- .NET SDK from the repository's `global.json`.
- Xcode 26.2 and its iOS platform support.
- Matching .NET iOS workload: `dotnet workload install ios --version 10.0.101.1`.
- Apple Development signing certificate and an iOS development profile.

Run commands below from the repository root. If a workload installation reports
success but missing packs, run `dotnet workload repair`. When upgrading Xcode,
also select a matching .NET iOS workload.

## Register and provision the iPad

Connect/unlock the iPad, trust the Mac, and sign into Xcode's Accounts settings.
Enable **Settings → Privacy & Security → Developer Mode** on the iPad; restart
and confirm activation. `xcrun devicectl list devices` lists connected devices.

The helper project only asks Xcode to create signing assets; do not install its
app. Substitute your team ID and the iPad's hardware UDID:

```sh
xcodebuild -project packaging/ios/SigningHelper.xcodeproj \
  -scheme SigningHelper -configuration Debug \
  -destination 'id=YOUR_IPAD_UDID' \
  -derivedDataPath /tmp/burntime-ios-signing \
  DEVELOPMENT_TEAM=YOUR_TEAM_ID \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build
```

Xcode's build log reports the generated profile. Pass its UUID to .NET:

```sh
dotnet build source/Burntime.iOS/Burntime.iOS.csproj \
  -c Release -r ios-arm64 -p:CodesignProvision=YOUR_PROFILE_UUID

xcrun devicectl device install app --device YOUR_IPAD_UDID \
  source/Burntime.iOS/bin/Release/net10.0-ios/ios-arm64/Burntime.app

xcrun devicectl device process launch --device YOUR_IPAD_UDID \
  org.burntime.remastered.dev
```

For subsequent builds, reuse the profile while it is valid. The bundle identifier
is `org.burntime.remastered.dev`. Installing updates preserves app data; deleting
the app deletes its local saves. Development builds do not use Steam Cloud.

## Simulator and integration smoke test

```sh
dotnet build source/Burntime.iOS/Burntime.iOS.csproj \
  -c Release -r iossimulator-arm64
xcrun simctl install booted \
  source/Burntime.iOS/bin/Release/net10.0-ios/iossimulator-arm64/Burntime.app
SIMCTL_CHILD_BURNTIME_IOS_SMOKE_TEST=1 xcrun simctl launch --console-pty \
  booted org.burntime.remastered.dev
```

The opt-in smoke test uses its own user directory. It opens menu, map, location,
inventory and options fixtures, checks touch selection, second-tap primary actions,
long-press secondary actions and trader switching, then saves and reloads a new game. Look for
`PASS` or `FAIL` in its console. It leaves the app running so it can be inspected;
terminate it with `xcrun simctl terminate booted org.burntime.remastered.dev`.
Launch without the environment variable for ordinary gameplay.

Results and logs are under `Documents/.config/BurntimeSmokeTest` in the app data
container (`xcrun simctl get_app_container booted org.burntime.remastered.dev data`).
The normal profile/log is under `Documents/.config/Burntime`.

## Device acceptance check

1. Tap a world-map destination: only its selection/name/textbars should change.
2. Tap the selected world-map location again to Travel/Enter; long press for Info/Radio.
   A single tap and a long-press release must never travel.
3. Drag the map and confirm releasing does not issue a movement command.
4. Tap items to show their normal tooltip; tap the selected item again to transfer, long press to
   execute the secondary action. Check overlapping items and screen edges.
   Entrances/NPCs on the location map should retain immediate tap actions.
5. Swipe the manual text and save list vertically; verify that swiping a save row
   does not select it. Player/save names use their defaults, without a native keyboard.
6. Save, quit via the iPad app switcher, reopen, and load the save.
7. Background/resume during play and check game/audio resume without stuck input.

The automated smoke test does not verify physical touch accuracy, scrolling,
audio interruptions, or performance on the iPad. Test these on-device.
