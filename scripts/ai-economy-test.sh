#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
build_root="$repo_root/artifacts/ai-economy-test"
python3 -m unittest discover -s "$repo_root/tests/ai-economy" -p 'test_*.py'
dotnet build "$repo_root/source/Burntime.MonoGame/Burntime.MonoGame.csproj" \
  --configuration Debug --artifacts-path "$build_root/sdk" --output "$build_root/app" \
  --disable-build-servers --maxcpucount:1 --consoleLoggerParameters:'ErrorsOnly;Summary'
python3 "$repo_root/scripts/ai-economy-test.py" --app "$build_root/app/Burntime.dll" \
  --output "$build_root/results" "$@"
