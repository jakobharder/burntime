#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$repo_root"
runtime="${ANDROID_RUNTIME_IDENTIFIER:-android-arm64}"
case "$runtime" in
  android-arm64|android-x64) ;;
  *) echo "Unsupported Android runtime: $runtime (use android-arm64 or android-x64)" >&2; exit 1 ;;
esac
output="${ANDROID_OUTPUT_DIRECTORY:-$repo_root/artifacts/$runtime}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
signing_args=(-p:AndroidKeyStore=false)
signing_label=development
if [[ -n "${ANDROID_SIGNING_KEYSTORE:-}" ]]; then
  [[ -f "$ANDROID_SIGNING_KEYSTORE" ]] || { echo "Signing keystore not found" >&2; exit 1; }
  : "${ANDROID_SIGNING_KEY_ALIAS:?Set ANDROID_SIGNING_KEY_ALIAS}"
  : "${ANDROID_SIGNING_STORE_PASSWORD:?Set ANDROID_SIGNING_STORE_PASSWORD}"
  : "${ANDROID_SIGNING_KEY_PASSWORD:?Set ANDROID_SIGNING_KEY_PASSWORD}"
  signing_args=(
    -p:AndroidKeyStore=true
    "-p:AndroidSigningKeyStore=$ANDROID_SIGNING_KEYSTORE"
    "-p:AndroidSigningKeyAlias=$ANDROID_SIGNING_KEY_ALIAS"
    -p:AndroidSigningStorePass=env:ANDROID_SIGNING_STORE_PASSWORD
    -p:AndroidSigningKeyPass=env:ANDROID_SIGNING_KEY_PASSWORD
  )
  export ANDROID_SIGNING_STORE_PASSWORD ANDROID_SIGNING_KEY_PASSWORD
  signing_label=release
elif [[ -n "${ANDROID_SIGNING_KEY_ALIAS:-}${ANDROID_SIGNING_STORE_PASSWORD:-}${ANDROID_SIGNING_KEY_PASSWORD:-}" ]]; then
  echo "Set ANDROID_SIGNING_KEYSTORE along with the other signing settings" >&2
  exit 1
fi
asset="$output/Burntime-Android-${runtime#android-}-$signing_label.apk"
for path in "$asset" "$asset.sha256" "$asset.commit.txt"; do
  [[ ! -e "$path" ]] || { echo "Refusing to overwrite $path" >&2; exit 1; }
done
work_root="$(mktemp -d "${TMPDIR:-/tmp}/burntime-android-package.XXXXXX")"
trap 'rm -rf "$work_root"' EXIT
# A fresh publish directory prevents picking up an APK left by a previous build.
dotnet publish source/Burntime.Android/Burntime.Android.csproj \
  -c Release -r "$runtime" -o "$work_root/publish" \
  -p:AndroidPackageFormats=apk -p:EmbedAssembliesIntoApk=true \
  "-p:ApplicationId=${ANDROID_APPLICATION_ID:-org.burntime.remastered.dev}" \
  "${signing_args[@]}" "$@"
shopt -s nullglob
packages=("$work_root/publish/"*-Signed.apk)
[[ ${#packages[@]} -eq 1 ]] || { echo "Expected exactly one signed APK, found ${#packages[@]}" >&2; exit 1; }
unzip -tq "${packages[0]}" >/dev/null
cp "${packages[0]}" "$asset"
git rev-parse HEAD > "$asset.commit.txt"
shasum -a 256 "$asset" > "$asset.sha256"
echo "Created $signing_label APK: $asset"
if [[ "$signing_label" == development ]]; then
  echo "Signed with the local Android development key. Use a persistent release keystore for distribution."
fi
