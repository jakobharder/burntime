#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/source/Burntime.MonoGame/Burntime.MonoGame.csproj"
build_root="$repo_root/artifacts/savegame-tests"
app_dir="$build_root/app"
app_dll="$app_dir/Burntime.dll"
fixture_dir="$repo_root/tests/savegames"

if [[ $# -gt 0 && "$1" != --* ]]; then
  fixture_dir="$1"
  shift
fi

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

exec dotnet "$app_dll" --savegame-test "$fixture_dir" "$@"
