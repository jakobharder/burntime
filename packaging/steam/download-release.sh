#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
if [[ -f "$script_dir/local.conf" ]]; then
  # shellcheck source=/dev/null
  source "$script_dir/local.conf"
fi

usage() {
  cat <<'EOF'
Usage:
  ./packaging/steam/download-release.sh [--skip-macos] RELEASE_TAG [OUTPUT_DIRECTORY]

Optional environment variables:
  STEAM_GITHUB_REPOSITORY  GitHub owner/repository
                           (default: jakobharder/burntime)

The script downloads and verifies the Windows, macOS, and Linux assets created
by the GitHub release workflow. It uses public release URLs and requires no
GitHub authentication.

Settings are loaded from packaging/steam/local.conf. Environment variables
override values from that file.
EOF
}

include_macos=true
if [[ "${1:-}" == "--skip-macos" ]]; then
  include_macos=false
  shift
fi

if [[ $# -lt 1 || $# -gt 2 ]]; then
  usage >&2
  exit 2
fi

release_tag="$1"
repository="${STEAM_GITHUB_REPOSITORY:-jakobharder/burntime}"

if [[ ! "$release_tag" =~ ^[a-zA-Z0-9._-]+$ ]]; then
  echo "Release tag may only contain letters, numbers, dots, underscores, and hyphens." >&2
  exit 2
fi
if [[ ! "$repository" =~ ^[a-zA-Z0-9_.-]+/[a-zA-Z0-9_.-]+$ ]]; then
  echo "STEAM_GITHUB_REPOSITORY must have the form owner/repository." >&2
  exit 2
fi

repo_root="$(cd "$script_dir/../.." && pwd)"
output_directory="${2:-$repo_root/artifacts/steam-release/$release_tag}"
mkdir -p "$output_directory"
output_directory="$(cd "$output_directory" && pwd)"

windows_name="burntime-$release_tag.zip"
macos_name="Burntime-macOS-arm64.dmg"
linux_name="burntime-$release_tag-linux-x64.tar.gz"
asset_names=("$windows_name" "$linux_name")
if [[ "$include_macos" == true ]]; then
  asset_names+=("$macos_name")
fi

for command_name in curl unzip tar; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    echo "Required command is not installed: $command_name" >&2
    exit 1
  fi
done

for asset_name in "${asset_names[@]}"; do
  if [[ -e "$output_directory/$asset_name" || -e "$output_directory/$asset_name.part" ]]; then
    echo "Refusing to overwrite existing asset: $output_directory/$asset_name" >&2
    echo "Remove it or choose another output directory." >&2
    exit 1
  fi
done

cleanup() {
  for asset_name in "${asset_names[@]}"; do
    rm -f "$output_directory/$asset_name.part"
  done
}
trap cleanup EXIT

for asset_name in "${asset_names[@]}"; do
  asset_url="https://github.com/$repository/releases/download/$release_tag/$asset_name"
  echo "Downloading $asset_name"
  curl --fail --location --show-error \
    --output "$output_directory/$asset_name.part" \
    "$asset_url"
done

for asset_name in "${asset_names[@]}"; do
  mv "$output_directory/$asset_name.part" "$output_directory/$asset_name"
done

for asset_name in "${asset_names[@]}"; do
  if [[ ! -s "$output_directory/$asset_name" ]]; then
    echo "Release asset is missing or empty: $asset_name" >&2
    exit 1
  fi
done

unzip -tq "$output_directory/$windows_name" >/dev/null
tar -tzf "$output_directory/$linux_name" >/dev/null
if [[ "$include_macos" == true ]] && command -v hdiutil >/dev/null 2>&1; then
  hdiutil verify "$output_directory/$macos_name" >/dev/null
fi

cat <<EOF
Downloaded and verified Steam upload inputs:
  Windows: $output_directory/$windows_name
  Linux:   $output_directory/$linux_name
EOF
if [[ "$include_macos" == true ]]; then
  echo "  macOS:   $output_directory/$macos_name"
fi
