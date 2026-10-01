#!/bin/sh
set -eu

TOOLS_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
CACHE_DIR="$TOOLS_DIR/.cache"
DOWNLOAD_DIR="$CACHE_DIR/downloads"
RUNTIME_DIR="$CACHE_DIR/realesrgan"
MODEL_DIR="$RUNTIME_DIR/models"
VENV_DIR="$CACHE_DIR/venv"

if [ "$(uname -s)" != "Darwin" ]; then
    echo "This installer currently supports macOS only." >&2
    exit 1
fi

mkdir -p "$DOWNLOAD_DIR" "$MODEL_DIR"

download_checked() {
    url=$1
    output=$2
    expected=$3

    if [ -f "$output" ]; then
        actual=$(shasum -a 256 "$output" | awk '{print $1}')
        if [ "$actual" = "$expected" ]; then
            return
        fi
        rm "$output"
    fi

    echo "Downloading $(basename "$output")..."
    curl -fL "$url" -o "$output"
    actual=$(shasum -a 256 "$output" | awk '{print $1}')
    if [ "$actual" != "$expected" ]; then
        echo "Checksum mismatch for $output" >&2
        exit 1
    fi
}

OFFICIAL_ZIP="$DOWNLOAD_DIR/realesrgan-ncnn-vulkan-20211212-macos.zip"
download_checked \
    "https://github.com/xinntao/Real-ESRGAN/releases/download/v0.2.3.0/realesrgan-ncnn-vulkan-20211212-macos.zip" \
    "$OFFICIAL_ZIP" \
    "de6e546f3e9f582faf7b59e7e93b8d72afffcf3d69845efc6f663bc647959686"

unzip -p "$OFFICIAL_ZIP" realesrgan-ncnn-vulkan > "$RUNTIME_DIR/realesrgan-ncnn-vulkan"
chmod +x "$RUNTIME_DIR/realesrgan-ncnn-vulkan"

for model_file in \
    realesrnet-x4plus.bin \
    realesrnet-x4plus.param \
    realesrgan-x4plus.bin \
    realesrgan-x4plus.param \
    realesrgan-x4plus-anime.bin \
    realesrgan-x4plus-anime.param
do
    unzip -p "$OFFICIAL_ZIP" "models/$model_file" > "$MODEL_DIR/$model_file"
done

download_checked \
    "https://raw.githubusercontent.com/upscayl/custom-models/4b6d2cfa59c7442af115dfc6e50fd8d7d40b96ef/models/RealESRGAN_General_WDN_x4_v3.bin" \
    "$MODEL_DIR/RealESRGAN_General_WDN_x4_v3.bin" \
    "74eae91a6230cbe5b931be6c49141faaff0034976933d4da9accb77c329c8a78"
download_checked \
    "https://raw.githubusercontent.com/upscayl/custom-models/4b6d2cfa59c7442af115dfc6e50fd8d7d40b96ef/models/RealESRGAN_General_WDN_x4_v3.param" \
    "$MODEL_DIR/RealESRGAN_General_WDN_x4_v3.param" \
    "22174924330297357434ad21ed0af7f4b820008d2a502b492754d130d4142714"
download_checked \
    "https://raw.githubusercontent.com/upscayl/custom-models/4b6d2cfa59c7442af115dfc6e50fd8d7d40b96ef/models/4x_NMKD-Siax_200k.bin" \
    "$MODEL_DIR/4x_NMKD-Siax_200k.bin" \
    "b2abdffa30fb15be752ac681e0c0edf8b543745c33a989aadc9d7d3394dd3110"
download_checked \
    "https://raw.githubusercontent.com/upscayl/custom-models/4b6d2cfa59c7442af115dfc6e50fd8d7d40b96ef/models/4x_NMKD-Siax_200k.param" \
    "$MODEL_DIR/4x_NMKD-Siax_200k.param" \
    "519bffb4912e295155d3cb5d6a29639d217ccca55f80962d8f677ef777a65b7a"

if [ ! -x "$VENV_DIR/bin/python" ]; then
    python3 -m venv "$VENV_DIR"
fi
"$VENV_DIR/bin/python" -m pip install --disable-pip-version-check -r "$TOOLS_DIR/requirements.txt"

echo "Installed Real-ESRGAN and models under $CACHE_DIR"
echo "Run: $TOOLS_DIR/upscale.sh --help"
