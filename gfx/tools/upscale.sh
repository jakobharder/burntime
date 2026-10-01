#!/bin/sh
set -eu

TOOLS_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PYTHON="$TOOLS_DIR/.cache/venv/bin/python"

if [ ! -x "$PYTHON" ] || [ ! -x "$TOOLS_DIR/.cache/realesrgan/realesrgan-ncnn-vulkan" ]; then
    echo "Tools are not installed. Run $TOOLS_DIR/install.sh first." >&2
    exit 1
fi

exec "$PYTHON" "$TOOLS_DIR/upscale.py" "$@"

