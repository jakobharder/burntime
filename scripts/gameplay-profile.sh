#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
if [[ $# -lt 1 || $# -gt 2 ]]; then
  echo "Usage: scripts/gameplay-profile.sh OUTPUT_DIRECTORY [30|60]" >&2
  exit 2
fi
fps="${2:-30}"
if [[ "$fps" != 30 && "$fps" != 60 ]]; then
  echo "Frame rate must be 30 or 60." >&2
  exit 2
fi
if [[ "$(uname -s)" != Darwin ]]; then
  echo "Gameplay profiling currently requires macOS CPU clocks." >&2
  exit 2
fi
mkdir -p "$1"
output="$(cd "$1" && pwd)"
project="$repo_root/source/Burntime.MonoGame/Burntime.MonoGame.csproj"
dotnet build "$project" -c Release --disable-build-servers --maxcpucount:1 \
  --consoleLoggerParameters:'ErrorsOnly;Summary'
for scene in map location; do
  mkdir -p "$output/$scene"
  echo "Profiling $scene at $fps FPS (12-second warmup, then 30-second sample)."
  BURNTIME_PROFILE_GAMEPLAY="$scene" \
  BURNTIME_PROFILE_FPS="$fps" \
  BURNTIME_PROFILE_OUTPUT="$output/$scene" \
    dotnet "$repo_root/bin/Release/Burntime.dll" --steam-deck --no-shader \
      > "$output/$scene/console.log" 2>&1
done
