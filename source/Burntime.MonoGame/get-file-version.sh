#!/bin/sh
set -eu

# v1.2.3 -> 1.2.3
# v1.2.3-rc1 -> 1.2.3
# v1.2.3-1-asdf -> 1.2.3.1
# v1.2.3-rc1-1-asdf -> 1.2.3.1
# v1.2-1-asdf -> 1.2.0.1

description=$(git describe --tags)
version=${description#v}
version=${version%%+*}

commit=$(printf '%s\n' "$version" | sed -nE 's/^.*-([0-9]+)-g[0-9a-f]+(-dirty)?$/\1/p')
tag=$(printf '%s\n' "$version" | sed -E 's/-[0-9]+-g[0-9a-f]+(-dirty)?$//')
base=${tag%%-*}

if [ -z "$commit" ]; then
    printf '%s\n' "$base"
elif [ "${base#*.*.}" = "$base" ]; then
    printf '%s.0.%s\n' "$base" "$commit"
else
    printf '%s.%s\n' "$base" "$commit"
fi
