#!/usr/bin/env bash
set -euo pipefail
[[ $# -ge 1 && $# -le 2 ]] || { echo "Usage: $0 RELEASE_TAG [OUTPUT_DIRECTORY]" >&2; exit 2; }
tag="$1"
repository="${IOS_GITHUB_REPOSITORY:-jakobharder/burntime}"
[[ "$tag" =~ ^[a-zA-Z0-9._-]+$ && "$repository" =~ ^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$ ]] || exit 2
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
output="${2:-$repo_root/artifacts/ios-release/$tag}"
mkdir -p "$output"
asset="Burntime-iPad-arm64-unsigned.zip"
for name in "$asset" "$asset.sha256"; do
  [[ ! -e "$output/$name" && ! -e "$output/$name.part" ]] || { echo "Refusing to overwrite $output/$name" >&2; exit 1; }
done
trap 'rm -f "$output/$asset.part" "$output/$asset.sha256.part"' EXIT
for name in "$asset" "$asset.sha256"; do
  curl --fail --location --show-error --output "$output/$name.part" \
    "https://github.com/$repository/releases/download/$tag/$name"
done
expected="$(awk 'NR == 1 {print $1}' "$output/$asset.sha256.part")"
actual="$(shasum -a 256 "$output/$asset.part" | awk '{print $1}')"
[[ "$expected" == "$actual" ]] || { echo "Checksum mismatch" >&2; exit 1; }
unzip -tq "$output/$asset.part" >/dev/null
for name in "$asset" "$asset.sha256"; do mv "$output/$name.part" "$output/$name"; done
echo "Downloaded and verified $output/$asset"
