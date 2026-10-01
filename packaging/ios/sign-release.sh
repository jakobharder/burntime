#!/usr/bin/env bash
set -euo pipefail
[[ $# -eq 1 ]] || { echo "Usage: IOS_SIGN_IDENTITY='Apple Distribution: ...' IOS_PROVISION_PROFILE=/path/profile.mobileprovision $0 RELEASE_TAG_OR_ZIP" >&2; exit 2; }
repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
input="$1"
if [[ "$input" != *.zip ]]; then
  [[ "$input" =~ ^[a-zA-Z0-9._-]+$ ]] || exit 2
  input="$repo_root/artifacts/ios-release/$input/Burntime-iPad-arm64-unsigned.zip"
fi
exec python3 "$repo_root/packaging/ios/sign-release.py" "$input"
