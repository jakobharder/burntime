#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
expected_hash="0d8a86dcc358fbcaa3a3e295152f1456d52d1c77aac129175a809725b8acde6a"
report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-refactor.XXXXXX")"
trap 'rm -f "$report"' EXIT

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
if [[ "$actual_hash" != "$expected_hash" ]]; then
  echo "AI behavior changed." >&2
  echo "Expected report hash: $expected_hash" >&2
  echo "Actual report hash:   $actual_hash" >&2
  exit 1
fi

echo "AI behavior baseline matched: $actual_hash"
