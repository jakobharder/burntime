#!/bin/sh
set -eu

# v1.2.3 -> 1.2.3
# v1.2.3-rc1 -> 1.2.3-rc1
# v1.2.3-1-asdf -> 1.2.3-1-asdf

description=$(git describe --tags)
version=${description#v}
printf '%s\n' "${version%%+*}"
