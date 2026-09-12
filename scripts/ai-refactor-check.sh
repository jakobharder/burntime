#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "$0")/.." && pwd)"
expected_modern_hash="a5200e0c79d36ceef0aef21a851cee495c133b65df861dae7548f0c55d450603"
expected_dos_hash="5a8299af78789977d401cf5b8dabf42317c04d3b5b8f07db37da68cd9746eb48"
expected_amiga_hash="8a67a89a941c6799edcc92e68660b81fa6d1e6b2a596669c093e44b912bd5c93"
modern_report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-modern.XXXXXX")"
dos_report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-dos.XXXXXX")"
amiga_report="$(mktemp "${TMPDIR:-/tmp}/burntime-ai-amiga.XXXXXX")"
trap 'rm -f "$modern_report" "$dos_report" "$amiga_report"' EXIT

report_hash() {
  sed -E \
    -e '/^- Longest turn:/d' \
    -e '/ took [0-9]+ ms\.$/d' \
    -e '/slow AI turn [0-9]+ ms:/d' \
    "$1" | shasum -a 256 | awk '{print $1}'
}

check_profile() {
  local profile="$1"
  local rules="$2"
  local expected="$3"
  local report="$4"

  bash "$repo_root/scripts/ai-simulate.sh" \
    --turns 100 \
    --difficulty hard \
    --rules "$rules" \
    --ai "$profile" \
    --seed 123 \
    --report "$report"

  local actual
  actual="$(report_hash "$report")"
  if [[ "$actual" != "$expected" ]]; then
    echo "$profile AI behavior changed." >&2
    echo "Expected report hash: $expected" >&2
    echo "Actual report hash:   $actual" >&2
    exit 1
  fi
  echo "$profile AI behavior baseline matched: $actual"
}

check_profile modern extended "$expected_modern_hash" "$modern_report"
check_profile dos dos "$expected_dos_hash" "$dos_report"
check_profile amiga amiga "$expected_amiga_hash" "$amiga_report"
