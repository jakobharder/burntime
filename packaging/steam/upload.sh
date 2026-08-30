#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "$0")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
if [[ -f "$script_dir/local.conf" ]]; then
  # shellcheck source=/dev/null
  source "$script_dir/local.conf"
fi

app_id="${STEAM_APP_ID:-3269080}"
branch="${STEAM_BRANCH:-test}"
prepare_only=false
dry_run=false
include_macos=true

usage() {
  cat <<'EOF'
Usage:
  ./packaging/steam/upload.sh [--prepare-only | --dry-run] [--skip-macos] RELEASE_TAG

  ./packaging/steam/upload.sh [--prepare-only | --dry-run] [--skip-macos] \
    CONTENT_BUILDER WINDOWS_ZIP MACOS_DMG_OR_APP LINUX_TAR_GZ

  ./packaging/steam/upload.sh --skip-macos \
    CONTENT_BUILDER WINDOWS_ZIP LINUX_TAR_GZ

Optional environment variables:
  STEAM_APP_ID       App ID (default: 3269080)
  STEAM_BRANCH       Public beta branch (default: test)
  STEAM_BUILD_DESC   Internal build description

Settings are loaded from packaging/steam/local.conf. Environment variables
override values from that file.

The Steam account password and Steam Guard code are entered directly into
SteamCMD; they are never accepted by or written by this script.
EOF
}

while [[ "${1:-}" == --* ]]; do
  case "$1" in
    --prepare-only) prepare_only=true ;;
    --dry-run) dry_run=true ;;
    --skip-macos) include_macos=false ;;
    *) echo "Unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
  shift
done

if [[ "$prepare_only" == true && "$dry_run" == true ]]; then
  echo "Use either --prepare-only or --dry-run, not both." >&2
  exit 2
fi

if [[ $# -eq 1 ]]; then
  release_tag="$1"
  if [[ ! "$release_tag" =~ ^[a-zA-Z0-9._-]+$ ]]; then
    echo "Release tag may only contain letters, numbers, dots, underscores, and hyphens." >&2
    exit 2
  fi
  : "${STEAM_CONTENT_BUILDER:?Set STEAM_CONTENT_BUILDER in local.conf}"
  release_root="$repo_root/artifacts/steam-release/$release_tag"
  content_builder="$STEAM_CONTENT_BUILDER"
  windows_archive="$release_root/burntime-$release_tag.zip"
  linux_archive="$release_root/burntime-$release_tag-linux-x64.tar.gz"
  if [[ "$include_macos" == true ]]; then
    macos_source="$release_root/Burntime-macOS-arm64.dmg"
  fi
elif [[ $# -eq 4 ]]; then
  content_builder="$1"
  windows_archive="$2"
  macos_source="$3"
  linux_archive="$4"
elif [[ $# -eq 3 && "$include_macos" == false ]]; then
  content_builder="$1"
  windows_archive="$2"
  linux_archive="$3"
else
  usage >&2
  exit 2
fi

: "${STEAM_WINDOWS_DEPOT_ID:?Set STEAM_WINDOWS_DEPOT_ID}"
: "${STEAM_MACOS_DEPOT_ID:?Set STEAM_MACOS_DEPOT_ID}"
: "${STEAM_LINUX_DEPOT_ID:?Set STEAM_LINUX_DEPOT_ID}"

if [[ "$prepare_only" == false ]]; then
  : "${STEAM_ACCOUNT:?Set STEAM_ACCOUNT, or use --prepare-only}"
fi

if [[ ! "$app_id" =~ ^[0-9]+$ ]]; then
  echo "STEAM_APP_ID must be numeric: $app_id" >&2
  exit 2
fi
for depot_id in \
  "$STEAM_WINDOWS_DEPOT_ID" \
  "$STEAM_MACOS_DEPOT_ID" \
  "$STEAM_LINUX_DEPOT_ID"; do
  if [[ ! "$depot_id" =~ ^[0-9]+$ ]]; then
    echo "Depot IDs must be numeric: $depot_id" >&2
    exit 2
  fi
done
if [[ ! "$branch" =~ ^[a-zA-Z0-9_-]+$ ]]; then
  echo "STEAM_BRANCH may only contain letters, numbers, underscores, and hyphens." >&2
  exit 2
fi

required_paths=("$content_builder" "$windows_archive" "$linux_archive")
if [[ "$include_macos" == true ]]; then
  required_paths+=("$macos_source")
fi
for required_path in "${required_paths[@]}"; do
  if [[ ! -e "$required_path" ]]; then
    echo "Path does not exist: $required_path" >&2
    exit 1
  fi
done

content_builder="$(cd "$content_builder" && pwd)"
windows_archive="$(cd "$(dirname "$windows_archive")" && pwd)/$(basename "$windows_archive")"
linux_archive="$(cd "$(dirname "$linux_archive")" && pwd)/$(basename "$linux_archive")"
if [[ "$include_macos" == true ]]; then
  macos_source="$(cd "$(dirname "$macos_source")" && pwd)/$(basename "$macos_source")"
fi

staging_root="$content_builder/content/burntime-$app_id"
scripts_root="$content_builder/scripts/burntime-$app_id"
output_root="$content_builder/output/burntime-$app_id"

rm -rf "$staging_root" "$scripts_root"
mkdir -p \
  "$staging_root/windows" \
  "$staging_root/macos" \
  "$staging_root/linux" \
  "$scripts_root" \
  "$output_root"

unzip -q "$windows_archive" -d "$staging_root/windows"
tar -xzf "$linux_archive" -C "$staging_root/linux"

mounted_dmg=""
cleanup() {
  if [[ -n "$mounted_dmg" ]]; then
    hdiutil detach "$mounted_dmg" -quiet || true
  fi
}
trap cleanup EXIT

copy_app() {
  local source_app="$1"
  local target_app="$2"

  if command -v ditto >/dev/null 2>&1; then
    ditto "$source_app" "$target_app"
  else
    cp -a "$source_app" "$target_app"
  fi
}

if [[ "$include_macos" == true ]]; then
  if [[ -d "$macos_source" && "$macos_source" == *.app ]]; then
    copy_app "$macos_source" "$staging_root/macos/Burntime.app"
  elif [[ -f "$macos_source" && "$macos_source" == *.dmg ]]; then
    if ! command -v hdiutil >/dev/null 2>&1; then
      echo "A DMG can only be staged on macOS; pass Burntime.app instead." >&2
      exit 1
    fi
    mounted_dmg="$(mktemp -d "${TMPDIR:-/tmp}/burntime-steam-dmg.XXXXXX")"
    hdiutil attach -nobrowse -readonly -mountpoint "$mounted_dmg" "$macos_source" >/dev/null
    if [[ ! -d "$mounted_dmg/Burntime.app" ]]; then
      echo "Burntime.app was not found at the root of $macos_source" >&2
      exit 1
    fi
    copy_app "$mounted_dmg/Burntime.app" "$staging_root/macos/Burntime.app"
  else
    echo "macOS source must be a Burntime .app directory or .dmg file." >&2
    exit 2
  fi
fi

chmod +x "$staging_root/linux/Burntime"
if [[ "$include_macos" == true ]]; then
  chmod +x "$staging_root/macos/Burntime.app/Contents/MacOS/Burntime"
fi

build_description="${STEAM_BUILD_DESC:-Burntime multiplatform test}"
if [[ "$build_description" == *\"* || "$build_description" == *$'\n'* ]]; then
  echo "STEAM_BUILD_DESC cannot contain quotes or newlines." >&2
  exit 2
fi

write_depot_script() {
  local depot_id="$1"
  local platform="$2"
  local script_path="$3"

  cat >"$script_path" <<EOF
"DepotBuild"
{
    "DepotID" "$depot_id"
    "ContentRoot" "$staging_root/$platform"

    "FileMapping"
    {
        "LocalPath" "*"
        "DepotPath" "."
        "Recursive" "1"
    }
}
EOF
}

write_depot_script \
  "$STEAM_WINDOWS_DEPOT_ID" windows "$scripts_root/depot_windows.vdf"
write_depot_script \
  "$STEAM_LINUX_DEPOT_ID" linux "$scripts_root/depot_linux.vdf"
if [[ "$include_macos" == true ]]; then
  write_depot_script \
    "$STEAM_MACOS_DEPOT_ID" macos "$scripts_root/depot_macos.vdf"
  macos_depot_entry="        \"$STEAM_MACOS_DEPOT_ID\" \"depot_macos.vdf\""
else
  macos_depot_entry=""
fi

app_build_script="$scripts_root/app_build_$app_id.vdf"
if [[ "$dry_run" == true ]]; then
  release_directive='    "Preview" "1"'
else
  release_directive="    \"SetLive\" \"$branch\""
fi
cat >"$app_build_script" <<EOF
"AppBuild"
{
    "AppID" "$app_id"
    "Desc" "$build_description"
    "BuildOutput" "$output_root"
$release_directive

    "Depots"
    {
        "$STEAM_WINDOWS_DEPOT_ID" "depot_windows.vdf"
        "$STEAM_LINUX_DEPOT_ID" "depot_linux.vdf"
$macos_depot_entry
    }
}
EOF

echo "Prepared SteamPipe content in $staging_root"
echo "Generated SteamPipe scripts in $scripts_root"

if [[ "$prepare_only" == true ]]; then
  echo "Preparation complete; SteamCMD was not run."
  exit 0
fi

if [[ -x "$content_builder/builder_osx/steamcmd.sh" ]]; then
  steamcmd="$content_builder/builder_osx/steamcmd.sh"
elif [[ -x "$content_builder/builder_linux/steamcmd.sh" ]]; then
  steamcmd="$content_builder/builder_linux/steamcmd.sh"
elif [[ -x "$content_builder/builder_linux/steamcmd" ]]; then
  steamcmd="$content_builder/builder_linux/steamcmd"
else
  echo "No executable macOS or Linux SteamCMD was found below $content_builder." >&2
  echo "Run the Steamworks SDK SteamCMD once to bootstrap it, then retry." >&2
  exit 1
fi

"$steamcmd" \
  +login "$STEAM_ACCOUNT" \
  +run_app_build "$app_build_script" \
  +quit

if [[ "$dry_run" == true ]]; then
  echo "SteamPipe preview complete; no content was uploaded and no branch changed."
else
  echo "Uploaded build and set it live on branch '$branch'."
fi
