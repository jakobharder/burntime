#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
check_script="$repo_root/scripts/ai-refactor-check.sh"
report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-refactor.XXXXXX")"
updated_check="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-refactor-check.XXXXXX")"
trap 'rm -f "$report" "$updated_check"' EXIT

bash "$repo_root/scripts/ai-simulate.sh" \
  --turns 100 \
  --difficulty hard \
  --seed 123 \
  --report "$report"

actual_hash="$(sed -E \
  -e '/^- Longest turn:/d' \
  -e '/ took [0-9]+ ms\.$/d' \
  -e '/slow AI turn [0-9]+ ms:/d' \
  "$report" | shasum -a 256 | awk '{print $1}')"

if [[ "$(grep -Ec '^expected_hash="[0-9a-f]{64}"$' "$check_script")" -ne 1 ]]; then
  echo "Could not find exactly one AI baseline hash in $check_script" >&2
  exit 1
fi

sed -E \
  "s/^expected_hash=\"[0-9a-f]{64}\"$/expected_hash=\"$actual_hash\"/" \
  "$check_script" > "$updated_check"
cp "$updated_check" "$check_script"

echo "Updated AI behavior baseline: $actual_hash"
