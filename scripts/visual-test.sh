#!/usr/bin/env bash
set -euo pipefail
repo_root="$(cd "$(dirname "$0")/.." && pwd)"
python="${PYTHON:-$repo_root/artifacts/visual-build/venv/bin/python}"
exec "$python" "$repo_root/scripts/visual-test.py" run "$@"
