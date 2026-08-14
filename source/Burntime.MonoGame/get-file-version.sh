#!/bin/sh
set -eu

# v1.2.3 -> 1.2.3
# v1.2.3-rc1 -> 1.2.3
# v1.2.3-1-asdf -> 1.2.3.1
# v1.2-1-asdf -> 1.2.0.1

description=$(git describe --tags)
version=${description#v}
version=${version%%+*}

old_ifs=$IFS
IFS=-
set -- $version
IFS=$old_ifs

base=$1
suffix=${2-}
commit=${3-}

if [ -z "$suffix" ] || [ -z "$commit" ]; then
    printf '%s\n' "$base"
elif [ "${base#*.*.}" = "$base" ]; then
    printf '%s.0.%s\n' "$base" "$suffix"
else
    printf '%s.%s\n' "$base" "$suffix"
fi
