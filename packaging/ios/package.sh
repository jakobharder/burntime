#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo_root"
output="${IOS_OUTPUT_DIRECTORY:-$repo_root/artifacts/ios-arm64}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
asset="$output/Burntime-iPad-arm64-unsigned.zip"
[[ ! -e "$asset" ]] || { echo "Refusing to overwrite $asset" >&2; exit 1; }
mkdir -p "$output"
dotnet build source/Burntime.iOS/Burntime.iOS.csproj -c Release -r ios-arm64 \
  -p:EnableCodeSigning=false -p:BuildIpa=false \
  -p:ApplicationId="${IOS_APPLICATION_ID:-org.burntime.remastered}" \
  -p:NoDSymUtil=false
build_dir="$repo_root/source/Burntime.iOS/bin/Release/net10.0-ios/ios-arm64"
work_root="$(mktemp -d "${TMPDIR:-/tmp}/burntime-ios-package.XXXXXX")"
trap 'rm -rf "$work_root"' EXIT
mkdir -p "$work_root/Payload"
ditto "$build_dir/Burntime.app" "$work_root/Payload/Burntime.app"
rm -rf "$work_root/Payload/Burntime.app/_CodeSignature"
rm -f "$work_root/Payload/Burntime.app/embedded.mobileprovision"
if [[ -d "$build_dir/Burntime.app.dSYM" ]]; then
  ditto "$build_dir/Burntime.app.dSYM" "$work_root/dSYMs/Burntime.app.dSYM"
fi
git rev-parse HEAD > "$work_root/commit.txt"
(cd "$work_root" && ditto -c -k --sequesterRsrc . "$asset")
unzip -tq "$asset" >/dev/null
shasum -a 256 "$asset" > "$asset.sha256"
echo "Created unsigned iPad app: $asset"
