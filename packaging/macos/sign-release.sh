#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
notarize=false

usage() {
  cat <<'EOF'
Usage:
  ./packaging/macos/sign-release.sh [--notarize] RELEASE_TAG

Examples:
  ./packaging/macos/sign-release.sh v1.1-rc7
  ./packaging/macos/sign-release.sh --notarize v1.1-rc7

The input is read from:
  artifacts/steam-release/<tag>/Burntime-macOS-arm64.dmg

The downloaded input is left untouched. The output is written to one of:
  artifacts/steam-release/<tag>/Burntime-macOS-arm64-signed.dmg
  artifacts/steam-release/<tag>/Burntime-macOS-arm64-notarized.dmg

The signature is created locally, with codesign contacting Apple's timestamp
service. --notarize additionally uploads the app and DMG to Apple, then staples
both notarization tickets. The notarization profile defaults to "burntime" and
can be changed with MACOS_NOTARY_PROFILE.

MACOS_SIGN_IDENTITY may specify the full Developer ID Application certificate
name. If omitted, the script uses the only valid Developer ID Application
identity in the login keychain.
EOF
}

if [[ "${1:-}" == "--notarize" ]]; then
  notarize=true
  shift
fi

if [[ $# -ne 1 ]]; then
  usage >&2
  exit 2
fi

release_tag="$1"
if [[ ! "$release_tag" =~ ^[a-zA-Z0-9._-]+$ ]]; then
  echo "Release tag may only contain letters, numbers, dots, underscores, and hyphens." >&2
  exit 2
fi

release_root="$repo_root/artifacts/steam-release/$release_tag"
input_dmg="$release_root/Burntime-macOS-arm64.dmg"
if [[ "$notarize" == true ]]; then
  output_dmg="$release_root/Burntime-macOS-arm64-notarized.dmg"
else
  output_dmg="$release_root/Burntime-macOS-arm64-signed.dmg"
fi
entitlements="$script_dir/Burntime.entitlements"
notary_profile="${MACOS_NOTARY_PROFILE:-burntime}"

if [[ ! -f "$input_dmg" ]]; then
  echo "Downloaded release DMG does not exist: $input_dmg" >&2
  echo "Run ./packaging/steam/download-release.sh $release_tag first." >&2
  exit 1
fi
if [[ -e "$output_dmg" ]]; then
  echo "Refusing to overwrite existing output: $output_dmg" >&2
  exit 1
fi

for command_name in codesign ditto hdiutil security xattr; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command is not installed: $command_name" >&2
    exit 1
  fi
done

signing_identity="${MACOS_SIGN_IDENTITY:-}"
if [[ -z "$signing_identity" ]]; then
  identity_list="$(
    security find-identity -v -p codesigning 2>/dev/null |
      sed -n 's/.*"\(Developer ID Application:.*\)"/\1/p'
  )"
  identity_count="$(printf '%s\n' "$identity_list" | sed '/^$/d' | wc -l | tr -d ' ')"
  if [[ "$identity_count" != "1" ]]; then
    echo "Expected exactly one valid Developer ID Application identity; found $identity_count." >&2
    echo "Set MACOS_SIGN_IDENTITY to the full certificate name." >&2
    exit 1
  fi
  signing_identity="$identity_list"
fi

work_root="$(mktemp -d "${TMPDIR:-/tmp}/burntime-sign.XXXXXX")"
mount_point="$work_root/mount"
app_path="$work_root/Burntime.app"
dmg_root="$work_root/dmg-root"
temporary_dmg="$work_root/$(basename "$output_dmg")"
mounted=false

cleanup() {
  if [[ "$mounted" == true ]]; then
    hdiutil detach "$mount_point" -quiet || true
  fi
  rm -rf "$work_root"
}
trap cleanup EXIT

mkdir -p "$mount_point"
hdiutil attach -nobrowse -readonly -mountpoint "$mount_point" "$input_dmg" >/dev/null
mounted=true
if [[ ! -d "$mount_point/Burntime.app" ]]; then
  echo "Burntime.app was not found at the root of $input_dmg" >&2
  exit 1
fi
ditto "$mount_point/Burntime.app" "$app_path"
hdiutil detach "$mount_point" -quiet
mounted=false

# Remove metadata inherited from the downloaded disk image before sealing the
# replacement bundle. This does not modify the downloaded DMG.
xattr -cr "$app_path"

echo "Signing Burntime.app with: $signing_identity"
while IFS= read -r native_file; do
  codesign --force --options runtime --timestamp \
    --sign "$signing_identity" "$native_file"
done < <(find "$app_path/Contents/MacOS" -type f \( -name '*.dylib' -o -name '*.so' \) -print)

codesign --force --options runtime --timestamp \
  --sign "$signing_identity" \
  --entitlements "$entitlements" \
  "$app_path"
codesign --verify --deep --strict --verbose=2 "$app_path"

if [[ "$notarize" == true ]]; then
  echo "Submitting Burntime.app to Apple using keychain profile: $notary_profile"
  ditto -c -k --keepParent "$app_path" "$work_root/Burntime-notarization.zip"
  xcrun notarytool submit "$work_root/Burntime-notarization.zip" \
    --keychain-profile "$notary_profile" \
    --wait
  xcrun stapler staple "$app_path"
  xcrun stapler validate "$app_path"
fi

mkdir -p "$dmg_root"
ditto "$app_path" "$dmg_root/Burntime.app"
ln -s /Applications "$dmg_root/Applications"
hdiutil create -quiet -volname "Burntime" -srcfolder "$dmg_root" \
  -format UDZO "$temporary_dmg"
codesign --force --timestamp --sign "$signing_identity" "$temporary_dmg"
codesign --verify --verbose=2 "$temporary_dmg"
hdiutil verify "$temporary_dmg" >/dev/null

if [[ "$notarize" == true ]]; then
  echo "Submitting signed DMG to Apple using keychain profile: $notary_profile"
  xcrun notarytool submit "$temporary_dmg" \
    --keychain-profile "$notary_profile" \
    --wait
  xcrun stapler staple "$temporary_dmg"
  xcrun stapler validate "$temporary_dmg"
fi

mv "$temporary_dmg" "$output_dmg"

echo "Created $output_dmg"
if [[ "$notarize" == false ]]; then
  echo "This DMG is Developer ID signed but not notarized."
fi
