#!/usr/bin/env python3

from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]


def extend_center(source: Path, destination: Path, extra_width: int) -> None:
    with Image.open(source) as image:
        center = image.width // 2
        extended = Image.new(image.mode, (image.width + extra_width, image.height))
        if image.mode == "P" and image.palette is not None:
            extended.putpalette(image.palette)

        extended.paste(image.crop((0, 0, center, image.height)), (0, 0))
        center_column = image.crop((center, 0, center + 1, image.height))
        for x in range(center, center + extra_width):
            extended.paste(center_column, (x, 0))
        extended.paste(
            image.crop((center, 0, image.width, image.height)),
            (center + extra_width, 0),
        )

        destination.parent.mkdir(parents=True, exist_ok=True)
        save_options = {}
        if "transparency" in image.info:
            save_options["transparency"] = image.info["transparency"]
        extended.save(destination, **save_options)


def main() -> None:
    classic_source = ROOT / "gfx/original/MUNT.RAW"
    classic_destination = ROOT / "resources/game/classic/gfx/ui"
    newgfx = ROOT / "resources/game/classic_newgfx/gfx/ui"
    extra_width = 25

    frames = {
        "menu_top": "24.png",
        "menu_middle": "25.png",
        "menu_bottom": "26.png",
    }
    for name, frame in frames.items():
        extend_center(
            classic_source / frame,
            classic_destination / f"{name}_wide.png",
            extra_width=extra_width,
        )
        extend_center(
            newgfx / f"{name}.png",
            newgfx / f"{name}_wide.png",
            extra_width=round(extra_width * 1.875),
        )


if __name__ == "__main__":
    main()
