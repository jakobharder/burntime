#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
project="$repo_root/source/Burntime.MonoGame/Burntime.MonoGame.csproj"
build_root="$repo_root/artifacts/ai-smoke-test"
app_dir="$build_root/app"
app_dll="$app_dir/Burntime.dll"
results_dir="$build_root/results"
mkdir -p "$results_dir"
scratch_dir="$(mktemp -d "${TMPDIR:-/tmp}/burntime-ai-smoke.XXXXXX")"
trap 'rm -rf "$scratch_dir"' EXIT

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

run_leg() {
  local label="$1"
  shift
  local log="$results_dir/${label}.log"
  local report="$results_dir/${label}.txt"
  rm -f "$report"

  if ! dotnet "$app_dll" --ai-simulate --report "$report" "$@" >"$log" 2>&1; then
    echo "FAIL: $label" >&2
    sed -n '1,240p' "$log" >&2
    exit 1
  fi
}

# Controlled opportunities supplement the normal campaign/save-load matrix.
for difficulty in normal hard; do
  for seed in 29 71 123; do
    run_leg "frontier-$difficulty-$seed" --weak-frontier-test --rules extended \
      --ai modern --difficulty "$difficulty" --seed "$seed" --turns 10
    echo "PASS weak frontier: $difficulty / seed $seed"
  done
done

scenario_count=0
for rules in dos amiga classic extended; do
  for profile in dos amiga modern; do
    for seed in 11 29 47; do
      scenario_count=$((scenario_count + 1))
      label="${rules}-${profile}-${seed}"
      save="$scratch_dir/scenario_${scenario_count}.sav"
      common=(--smoke-test --early-death-turn 60 --rules "$rules" --ai "$profile" --difficulty hard --seed "$seed")

      run_leg "${label}-new" "${common[@]}" --turns 30 --save-at-end "$save"
      run_leg "${label}-loaded" "${common[@]}" --turns 30 --load-save "$save"
      echo "PASS: $rules rules / $profile AI / seed $seed"
    done
  done
done

for rules in dos amiga classic extended; do
  scenario_count=$((scenario_count + 1))
  label="${rules}-mixed-71"
  save="$scratch_dir/scenario_${scenario_count}.sav"
  common=(--smoke-test --early-death-turn 60 --rules "$rules" --ai-profiles dos,amiga,modern,none \
    --difficulty hard --seed 71)

  run_leg "${label}-new" "${common[@]}" --turns 30 --save-at-end "$save"
  run_leg "${label}-loaded" "${common[@]}" --turns 30 --load-save "$save"
  echo "PASS: $rules rules / mixed AI profiles / seed 71"
done

echo "AI smoke matrix: $scenario_count scenarios and $((scenario_count * 2)) save/load legs, plus 6 weak-frontier scenarios passed."
