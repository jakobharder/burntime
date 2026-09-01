#!/bin/bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
architecture="${1:-arm64}"
configuration="${CONFIGURATION:-Release}"
runtime="osx-$architecture"
project="$repo_root/source/Burntime.MonoGame/Burntime.MonoGame.csproj"
artifacts_dir="${ARTIFACTS_DIR:-$repo_root/artifacts/macos-$architecture}"
publish_dir="$artifacts_dir/publish"
app_path="$artifacts_dir/Burntime.app"
dmg_root="$artifacts_dir/dmg-root"
dmg_path="$artifacts_dir/Burntime-macOS-$architecture.dmg"
signing_identity="${MACOS_SIGN_IDENTITY:--}"
restore_args=()
if [[ "${SKIP_RESTORE:-false}" == "true" ]]; then
  restore_args+=(--no-restore)
fi

case "$architecture" in
  arm64|x64) ;;
  *) echo "Unsupported architecture: $architecture (use arm64 or x64)" >&2; exit 2 ;;
esac

version="${VERSION:-$(git -C "$repo_root" describe --tags --always --dirty | sed -E 's/^v//; s/-[0-9]+-g[0-9a-f]+.*$//; s/[^0-9.].*$//')}"
version="${version:-0.0.0}"
build_version="${BUILD_VERSION:-$(git -C "$repo_root" rev-list --count HEAD)}"

rm -rf "$artifacts_dir"
mkdir -p "$publish_dir" "$app_path/Contents/MacOS" "$app_path/Contents/Resources" "$dmg_root"

dotnet publish "$project" \
  ${restore_args[@]+"${restore_args[@]}"} \
  --configuration "$configuration" \
  --runtime "$runtime" \
  --self-contained true \
  --output "$publish_dir" \
  -p:DebugSymbols=false \
  -p:PublishSingleFile=true

for license_name in \
  THIRD-PARTY-NOTICES.txt \
  DOTNET-THIRD-PARTY-NOTICES.txt \
  OPENAL-SOFT-LICENSE.txt; do
  license_path="$publish_dir/licenses/$license_name"
  if [[ ! -f "$license_path" ]]; then
    echo "Missing packaged third-party license: $license_path" >&2
    exit 1
  fi
done

if [[ ! -f "$publish_dir/libsteam_api.dylib" ]]; then
  echo "Missing packaged Steam API runtime: $publish_dir/libsteam_api.dylib" >&2
  exit 1
fi

dotnet build "$repo_root/source/PakConverter/PakConverter.csproj" \
  ${restore_args[@]+"${restore_args[@]}"} \
  --configuration "$configuration" \
  -p:DebugSymbols=false

pushd "$publish_dir/game" >/dev/null
for package_name in classic classic_de classic_es classic_newgfx amiga music; do
  dotnet "$repo_root/bin/tools/$configuration/PakConverter.dll" "$package_name"
done
popd >/dev/null

rm -rf \
  "$publish_dir/game/classic" \
  "$publish_dir/game/classic_newgfx" \
  "$publish_dir/game/classic_de" \
  "$publish_dir/game/classic_es" \
  "$publish_dir/game/classic_jp" \
  "$publish_dir/game/amiga" \
  "$publish_dir/game/music"

find "$publish_dir" -name '*.pdb' -delete
cp -R "$publish_dir"/. "$app_path/Contents/MacOS/"
# Keep non-code data in the bundle's resource area. The game link preserves the
# existing AppContext.BaseDirectory/game lookup without putting data in MacOS.
mv "$app_path/Contents/MacOS/game" "$app_path/Contents/Resources/game"
ln -s ../Resources/game "$app_path/Contents/MacOS/game"
mv "$app_path/Contents/MacOS/licenses" "$app_path/Contents/Resources/licenses"
sed -e "s/@VERSION@/$version/g" -e "s/@BUILD_VERSION@/$build_version/g" \
  "$repo_root/packaging/macos/Info.plist.in" > "$app_path/Contents/Info.plist"
mkdir -p "$artifacts_dir/swift-cache"
SWIFT_MODULECACHE_PATH="$artifacts_dir/swift-cache" \
CLANG_MODULE_CACHE_PATH="$artifacts_dir/swift-cache" \
  swift "$repo_root/packaging/macos/create-macos-icon.swift" \
    "$repo_root/source/Burntime.MonoGame/Icon.ico" \
    "$artifacts_dir/Icon-macOS.png"
"$repo_root/packaging/macos/create-icon.sh" \
  "$artifacts_dir/Icon-macOS.png" \
  "$app_path/Contents/Resources/Burntime.icns"

chmod +x "$app_path/Contents/MacOS/Burntime"

# Sign native libraries first, then seal the outer bundle. MACOS_SIGN_IDENTITY=-
# is an ad-hoc signature suitable for local testing.
while IFS= read -r native_file; do
  codesign --force --timestamp=none --sign "$signing_identity" "$native_file"
done < <(find "$app_path/Contents/MacOS" -type f \( -name '*.dylib' -o -name '*.so' \) -print)

if [[ "$signing_identity" == "-" ]]; then
  codesign --force --sign - --entitlements "$repo_root/packaging/macos/Burntime.entitlements" "$app_path"
else
  codesign --force --options runtime --timestamp --sign "$signing_identity" \
    --entitlements "$repo_root/packaging/macos/Burntime.entitlements" "$app_path"
fi

cp -R "$app_path" "$dmg_root/"
ln -s /Applications "$dmg_root/Applications"
hdiutil create -quiet -volname "Burntime" -srcfolder "$dmg_root" -ov -format UDZO "$dmg_path"

if [[ "$signing_identity" != "-" ]]; then
  codesign --force --timestamp --sign "$signing_identity" "$dmg_path"
fi

codesign --verify --deep --strict --verbose=2 "$app_path"
echo "Created $dmg_path"
