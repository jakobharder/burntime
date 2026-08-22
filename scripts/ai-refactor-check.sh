#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
expected_hash="4d2ba3d6680bc83c54e75fbb9f7bbbc58267d154d584a62d2786a1b5bf0c6a85"
report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-refactor.XXXXXX")"
trap 'rm -f "$report"' EXIT

"$repo_root/scripts/ai-simulate.sh" \
  --turns 100 \
  --difficulty hard \
  --seed 123 \
  --report "$report"

actual_hash="$(shasum -a 256 "$report" | awk '{print $1}')"
if [[ "$actual_hash" != "$expected_hash" ]]; then
  echo "AI behavior changed." >&2
  echo "Expected report hash: $expected_hash" >&2
  echo "Actual report hash:   $actual_hash" >&2
  exit 1
fi

echo "AI behavior baseline matched: $actual_hash"
