#!/usr/bin/env python3
"""Validate a store capture run and export its PNG masters as quality-90 JPEGs."""
import json
import sys
from pathlib import Path

from PIL import Image


def export(source: Path, destination: Path) -> None:
    manifest = json.loads((source / "manifest.json").read_text())
    names = json.loads((source / "captures.json").read_text())
    expected = {"ipad": (2752, 2064), "steam": (1920, 1080),
                "macos": (2560, 1600)}[manifest["mode"]]
    if len(names) != 4 or len(set(names)) != 4:
        raise ValueError("Incomplete or duplicate capture scenarios.")
    warnings = [line for line in (source / "resources.log").read_text().splitlines()
                if "[warning]" in line]
    if warnings:
        raise ValueError("Capture has resource warnings:\n" + "\n".join(warnings))
    for name in names:
        if (destination / (name + ".jpg")).exists():
            raise ValueError("Refusing to overwrite " + name + ".jpg")
        with Image.open(source / (name + ".png")) as image:
            if image.size != expected:
                raise ValueError(f"{name}: expected {expected}, got {image.size}")
    destination.mkdir(parents=True, exist_ok=True)
    for name in names:
        with Image.open(source / (name + ".png")) as image:
            image.convert("RGB").save(destination / (name + ".jpg"),
                                      quality=90, subsampling=0, optimize=True)
    manifest.update(format="JPEG", quality=90, subsampling="4:4:4",
                    files=[name + ".jpg" for name in names], masters="raw")
    (destination / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Exported {len(names)} quality-90 JPEGs to {destination}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("Usage: export-screenshots.py RAW_CAPTURE_DIRECTORY OUTPUT_DIRECTORY")
    export(Path(sys.argv[1]), Path(sys.argv[2]))
