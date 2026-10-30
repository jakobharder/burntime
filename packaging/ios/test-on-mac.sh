#!/usr/bin/env bash
set -euo pipefail

[[ "$(uname -m)" == arm64 ]] || { echo "An Apple silicon Mac is required." >&2; exit 1; }
: "${IOS_DEVELOPMENT_TEAM:?Set IOS_DEVELOPMENT_TEAM to your Apple Developer team ID}"
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo_root"
output="${IOS_MAC_TEST_OUTPUT_DIRECTORY:-$repo_root/artifacts/ios-mac-test/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
bundle_id=org.burntime.remastered.dev

# Register this Mac for the iOS development profile. Xcode's Designed for iPad
# destination uses the device SDK, not the simulator or Mac Catalyst.
if ! xcodebuild -project packaging/ios/SigningHelper.xcodeproj \
  -scheme SigningHelper -configuration Debug \
  -destination 'platform=macOS,arch=arm64' \
  -derivedDataPath "$output/signing" \
  DEVELOPMENT_TEAM="$IOS_DEVELOPMENT_TEAM" \
  PRODUCT_BUNDLE_IDENTIFIER="$bundle_id" \
  -allowProvisioningUpdates -allowProvisioningDeviceRegistration build \
  > "$output/provisioning.log" 2>&1; then
  tail -40 "$output/provisioning.log" >&2
  echo "Resolve the Xcode signing error above, then run this script again." >&2
  exit 1
fi
profile="$output/signing/Build/Products/Debug-iphoneos/SigningHelper.app/embedded.mobileprovision"
[[ -f "$profile" ]] || { echo "Xcode did not produce an iOS development profile." >&2; exit 1; }

# Use the same clean, trimmed, AOT Release packaging as the store build. Keep
# the development bundle ID separate so testing cannot replace store saves.
IOS_OUTPUT_DIRECTORY="$output" IOS_APPLICATION_ID="$bundle_id" \
  ./packaging/ios/package.sh
IOS_SIGN_IDENTITY="${IOS_SIGN_IDENTITY:-Apple Development}" \
  IOS_PROVISION_PROFILE="$profile" \
  bash ./packaging/ios/sign-release.sh "$output/Burntime-iPad-arm64-unsigned.zip"

# Apple's installer creates the Designed for iPad wrapper in /Applications.
# Launching that wrapper mounts the iOS bundle read-only, matching the store
# installation. Opening the bare iOS .app is rejected as an executable format
# error, even with a valid signature and profile.
open -W "$output/Burntime-iPad-arm64-signed.ipa"
open -b "$bundle_id"
echo "Installed and requested launch of the Release iPad app ($bundle_id)."
echo "macOS names the installed app Burntime, adding a number if needed."
echo "Signed IPA: $output/Burntime-iPad-arm64-signed.ipa"
