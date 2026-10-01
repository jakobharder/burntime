#!/bin/sh
set -eu

repository=$(CDPATH= cd -- "$(dirname "$0")/../.." && pwd)

case "${1:-}" in
    display)
        # CFBundleShortVersionString only accepts a numeric release version.
        # Keep prerelease and commit-distance details in the assembly metadata.
        tag=$(git -C "$repository" describe --tags --abbrev=0)
        version=${tag#v}
        version=${version%%-*}
        case "$version" in
            ''|*[!0-9.]*)
                echo "Unsupported iOS version tag: $tag" >&2
                exit 1
                ;;
        esac
        printf '%s\n' "$version"
        ;;
    build)
        # A numeric, monotonically increasing value suitable for CFBundleVersion.
        git -C "$repository" rev-list --count HEAD
        ;;
    *)
        echo "Usage: $0 display|build" >&2
        exit 2
        ;;
esac
