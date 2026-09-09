#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
check_script="$repo_root/scripts/ai-refactor-check.sh"
extended_report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-extended.XXXXXX")"
dos_report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-dos.XXXXXX")"
amiga_report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-amiga.XXXXXX")"
updated_check="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-refactor-check.XXXXXX")"
trap 'rm -f "$extended_report" "$dos_report" "$amiga_report" "$updated_check"' EXIT

report_hash() {
  sed -E \
    -e '/^- Longest turn:/d' \
    -e '/ took [0-9]+ ms\.$/d' \
    -e '/slow AI turn [0-9]+ ms:/d' \
    "$1" | shasum -a 256 | awk '{print $1}'
}

generate_profile() {
  local profile="$1"
  local rules="$2"
  local report="$3"
  bash "$repo_root/scripts/ai-simulate.sh" \
    --turns 100 \
    --difficulty hard \
    --rules "$rules" \
    --ai "$profile" \
    --seed 123 \
    --report "$report"
}

generate_profile extended extended "$extended_report"
generate_profile dos dos "$dos_report"
generate_profile amiga amiga "$amiga_report"

extended_hash="$(report_hash "$extended_report")"
dos_hash="$(report_hash "$dos_report")"
amiga_hash="$(report_hash "$amiga_report")"

if [[ "$(grep -Ec '^expected_(extended|dos|amiga)_hash="[0-9a-f]{64}"$' "$check_script")" -ne 3 ]]; then
  echo "Could not find all three AI baseline hashes in $check_script" >&2
  exit 1
fi

sed -E \
  -e "s/^expected_extended_hash=\"[0-9a-f]{64}\"$/expected_extended_hash=\"$extended_hash\"/" \
  -e "s/^expected_dos_hash=\"[0-9a-f]{64}\"$/expected_dos_hash=\"$dos_hash\"/" \
  -e "s/^expected_amiga_hash=\"[0-9a-f]{64}\"$/expected_amiga_hash=\"$amiga_hash\"/" \
  "$check_script" > "$updated_check"
cp "$updated_check" "$check_script"

echo "Updated Extended AI behavior baseline: $extended_hash"
echo "Updated DOS AI behavior baseline:      $dos_hash"
echo "Updated Amiga AI behavior baseline:    $amiga_hash"
