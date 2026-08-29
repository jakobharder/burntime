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

if [[ ! -d "$fixture_dir" ]]; then
  echo "Save-game fixture folder does not exist: $fixture_dir" >&2
  exit 2
fi
fixture_dir="$(cd "$fixture_dir" && pwd)"

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

test_fixture_dir="$fixture_dir"
if find "$fixture_dir" -type f -iname '*.zip' -print -quit | grep -q .; then
  if ! command -v unzip >/dev/null 2>&1; then
    echo "Zip save-game fixtures require the 'unzip' command." >&2
    exit 2
  fi

  temporary_root="$(mktemp -d "${TMPDIR:-/tmp}/burntime-savegames.XXXXXX")"
  trap 'rm -rf "$temporary_root"' EXIT
  test_fixture_dir="$temporary_root/fixtures"
  archive_root="$temporary_root/archives"
  mkdir -p "$test_fixture_dir" "$archive_root"

  while IFS= read -r -d '' save_file; do
    relative_save="${save_file#"$fixture_dir"/}"
    mkdir -p "$test_fixture_dir/$(dirname "$relative_save")"
    ln -s "$save_file" "$test_fixture_dir/$relative_save"
  done < <(find "$fixture_dir" -type f -iname '*.sav' -print0)

  while IFS= read -r -d '' zip_file; do
    relative_zip="${zip_file#"$fixture_dir"/}"
    extracted_dir="$archive_root/$relative_zip"
    linked_dir="$test_fixture_dir/zipped/$relative_zip"
    mkdir -p "$extracted_dir" "$linked_dir"
    unzip -qq "$zip_file" -d "$extracted_dir"

    extracted_count=0
    while IFS= read -r -d '' extracted_save; do
      relative_save="${extracted_save#"$extracted_dir"/}"
      mkdir -p "$linked_dir/$(dirname "$relative_save")"
      ln -s "$extracted_save" "$linked_dir/$relative_save"
      extracted_count=$((extracted_count + 1))
    done < <(find "$extracted_dir" -type f -iname '*.sav' \
      ! -path '*/__MACOSX/*' ! -name '._*' -print0)

    if [[ $extracted_count -eq 0 ]]; then
      echo "Zip fixture contains no .sav files: $zip_file" >&2
      exit 2
    fi
  done < <(find "$fixture_dir" -type f -iname '*.zip' -print0)
fi

dotnet "$app_dll" --savegame-test "$test_fixture_dir" "$@"
