#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/source/Burntime.MonoGame/Burntime.MonoGame.csproj"
build_root="$repo_root/artifacts/ai-simulation"
app_dir="$build_root/app"
app_dll="$app_dir/Burntime.dll"

# The project graph has occasionally deadlocked during parallel builds. Keep this
# validation entry point single-node and independent from persistent build servers.
# Its dedicated output also prevents a successfully built library from being
# tested through an older copy in the normal bin/Debug directory.
dotnet build "$project" \
  --configuration Debug \
  --artifacts-path "$build_root/sdk" \
  --output "$app_dir" \
  --disable-build-servers \
  --maxcpucount:1 \
  --consoleLoggerParameters:'ErrorsOnly;Summary'

if [[ ! -f "$app_dll" ]]; then
  echo "Build succeeded but did not produce $app_dll" >&2
  exit 1
fi

exec dotnet "$app_dll" --ai-simulate "$@"
