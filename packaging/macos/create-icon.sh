#!/bin/bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: $0 SOURCE_IMAGE OUTPUT.icns" >&2
  exit 2
fi

source_image="$1"
output_icon="$2"
work_dir="$(mktemp -d "${TMPDIR:-/tmp}/burntime-icon.XXXXXX")"
iconset="$work_dir/Burntime.iconset"
trap 'rm -rf "$work_dir"' EXIT

mkdir -p "$iconset"

make_icon() {
  local pixels="$1"
  local name="$2"
  sips -s format png -z "$pixels" "$pixels" "$source_image" --out "$iconset/$name" >/dev/null
}

make_icon 16 icon_16x16.png
make_icon 32 icon_16x16@2x.png
make_icon 32 icon_32x32.png
make_icon 64 icon_32x32@2x.png
make_icon 128 icon_128x128.png
make_icon 256 icon_128x128@2x.png
make_icon 256 icon_256x256.png
make_icon 512 icon_256x256@2x.png
make_icon 512 icon_512x512.png
make_icon 1024 icon_512x512@2x.png

mkdir -p "$(dirname "$output_icon")"
iconutil -c icns "$iconset" -o "$output_icon"
