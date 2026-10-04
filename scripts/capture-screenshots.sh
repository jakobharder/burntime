#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo_root"
python="${PYTHON:-$repo_root/artifacts/visual-build/venv/bin/python}"
"$python" -c 'from PIL import Image' || { echo "Install the visual-test Python dependencies; see scripts/README.md" >&2; exit 1; }
mode="${1:-all}"
case "$mode" in
  all) modes=(ipad steam) ;;
  ipad|steam) modes=("$mode") ;;
  *) echo "Usage: $0 [all|ipad|steam] [OUTPUT_DIRECTORY]" >&2; exit 2 ;;
esac
[[ $# -le 2 ]] || { echo "Usage: $0 [all|ipad|steam] [OUTPUT_DIRECTORY]" >&2; exit 2; }
output="${2:-$repo_root/artifacts/screenshots/$(date +%Y%m%d-%H%M%S)}"
mkdir -p "$output"
output="$(cd "$output" && pwd)"
for target in "${modes[@]}"; do
  [[ ! -e "$output/$target" ]] || { echo "Use a fresh directory: $output/$target already exists" >&2; exit 1; }
done
dotnet build source/Burntime.MonoGame/Burntime.MonoGame.csproj -c Release
git rev-parse HEAD > "$output/commit.txt"
for target in "${modes[@]}"; do
  dotnet bin/Release/Burntime.dll --store-capture "$target" "$output/$target/raw"
  "$python" scripts/export-screenshots.py "$output/$target/raw" "$output/$target"
done
echo "Screenshots: $output"
