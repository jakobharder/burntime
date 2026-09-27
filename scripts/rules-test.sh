#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/source/Burntime.Remaster.Tests/Burntime.Remaster.Tests.csproj"
build_root="$repo_root/artifacts/rules-tests"
app_dir="$build_root/app"
app_dll="$app_dir/Burntime.Remaster.Tests.dll"

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

exec dotnet "$app_dll"
