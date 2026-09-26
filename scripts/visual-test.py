#!/usr/bin/env python3
"""Run the rendered scene suite, review exact pixel diffs, and accept selected images."""
import argparse
from datetime import datetime, timezone
import html
import json
from pathlib import Path
import shutil
import subprocess
import sys

from PIL import Image, ImageChops

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "artifacts/visual-build"
BASELINES = ROOT / "tests/visual/baselines"
MODES = ["classic", "newgfx", "classic-no-hints"]


def compare(actual, baseline, diff):
    """Compare decoded RGBA pixels (PNG metadata/compression is irrelevant)."""
    with Image.open(actual) as image:
        current = image.convert("RGBA")
    if not baseline.exists():
        return {"status": "missing-baseline"}
    with Image.open(baseline) as image:
        expected = image.convert("RGBA")
    if current.size != expected.size:
        return {"status": "size-mismatch", "actual_size": current.size,
                "baseline_size": expected.size}
    delta = ImageChops.difference(current, expected)
    channels = delta.split()
    mask = channels[0]
    for channel in channels[1:]:
        mask = ImageChops.lighter(mask, channel)
    mask = mask.point(lambda value: 255 if value else 0)
    changed = mask.histogram()[255]
    if changed:
        highlight = current.convert("RGB").point(lambda value: value // 3)
        highlight.paste((255, 0, 255), mask=mask)
        highlight.save(diff)
    return {"status": "different" if changed else "match", "changed_pixels": changed,
            "total_pixels": current.width * current.height}


def report(run, results):
    (run / "results.json").write_text(json.dumps(results, indent=2) + "\n")
    body = ["<!doctype html><meta charset='utf-8'><title>Burntime visual tests</title>",
            "<style>body{font:16px system-ui;background:#202124;color:#eee;margin:24px}"
            ".images{display:flex;gap:12px}figure{margin:0;flex:1;min-width:0}"
            "img{width:100%;image-rendering:pixelated}pre{white-space:pre-wrap}</style>",
            "<h1>Burntime visual tests</h1><p>Baseline / actual / highlighted difference. "
            "Open an image for full resolution. Resource errors cannot be accepted as baselines.</p>"]
    for mode, data in results.items():
        body.append(f"<h2>{html.escape(mode)}</h2><pre>{html.escape(chr(10).join(data['errors']))}</pre>")
        for case in data["scenarios"]:
            name = case["name"]
            body.append(f"<h3>{html.escape(name)} — {case['status']}</h3><div class='images'>")
            for label, suffix in [("Baseline", "baseline"), ("Actual", "actual"), ("Diff", "diff")]:
                path = f"{mode}/{suffix}/{name}.png"
                body.append(f"<figure><figcaption>{label}</figcaption>")
                if (run / path).exists():
                    body.append(f"<a href='{path}'><img src='{path}'></a>")
                body.append("</figure>")
            body.append("</div>")
    (run / "report.html").write_text("\n".join(body))


def run_tests(args):
    if not args.skip_build:
        subprocess.run(["dotnet", "build", str(ROOT / "source/Burntime.MonoGame/Burntime.MonoGame.csproj"),
                        "--configuration", "Debug", "--artifacts-path", str(BUILD / "sdk"),
                        "--output", str(BUILD / "app"), "--disable-build-servers", "--maxcpucount:1",
                        "--consoleLoggerParameters:ErrorsOnly;Summary"], check=True, cwd=ROOT)
    run = args.output or ROOT / "artifacts/visual" / datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S%fZ")
    run = run.resolve()
    run.mkdir(parents=True, exist_ok=False)  # never mix new captures with an older run
    results = {}
    modes = MODES if args.mode == "all" else MODES[:2] if args.mode == "both" else [args.mode]
    for mode in modes:
        folder = run / mode
        actual = folder / "actual"
        actual.mkdir(parents=True)
        (folder / "baseline").mkdir()
        (folder / "diff").mkdir()
        errors = []
        with (folder / "process.log").open("w") as log:
            try:
                process = subprocess.run(["dotnet", str(BUILD / "app/Burntime.dll"),
                                          "--visual-test", mode, str(actual)] + (["--native-filter"] if args.native_filter else []),
                                         stdout=log, stderr=subprocess.STDOUT, timeout=240, cwd=ROOT)
                if process.returncode:
                    errors.append(f"Game exited with code {process.returncode}; see process.log and actual/error.txt.")
            except subprocess.TimeoutExpired:
                errors.append("Game timed out after 240 seconds; see process.log.")
        resource_log = actual / "resources.log"
        if resource_log.exists():
            scenario = "startup"
            for line in resource_log.read_text(errors="replace").splitlines():
                if "VISUAL SCENARIO:" in line:
                    scenario = line.split("VISUAL SCENARIO:", 1)[1].strip()
                if "[warning]" in line:
                    errors.append(f"{scenario}: {line}")
        else:
            errors.append("Resource log is missing.")
        manifest = actual / "captures.json"
        if not manifest.exists():
            errors.append("Capture suite did not complete.")
        else:
            expected = json.loads(manifest.read_text())
            if not expected or set(expected) != {image.stem for image in actual.glob("*.png")}:
                errors.append("Capture manifest does not match the screenshots on disk.")
        cases = []
        # Also show partial captures after a crash or timeout.
        for image in sorted(actual.glob("*.png")):
            baseline = args.baselines / mode / image.name
            if baseline.exists():
                shutil.copy2(baseline, folder / "baseline" / image.name)
            result = compare(image, baseline, folder / "diff" / image.name)
            cases.append({"name": image.stem, **result})
        if not cases:
            errors.append("No screenshots captured.")
        results[mode] = {"errors": errors, "scenarios": cases}
    report(run, results)
    print(f"Review: {run / 'report.html'}")
    failed = any(data["errors"] or any(case["status"] != "match" for case in data["scenarios"])
                 for data in results.values())
    print("FAIL (review report)" if failed else "PASS")
    return int(failed)


def accept(args):
    results = json.loads((args.run / "results.json").read_text())
    data = results[args.mode]
    if data["errors"]:
        raise ValueError("Cannot accept a mode with resource errors or an incomplete capture run.")
    names = {case["name"] for case in data["scenarios"]}
    selected = names if args.all else set(args.scenario)
    if not selected or not selected <= names:
        raise ValueError(f"Select existing scenarios: {', '.join(sorted(names))}")
    destination = args.baselines / args.mode
    destination.mkdir(parents=True, exist_ok=True)
    for name in sorted(selected):
        shutil.copy2(args.run / args.mode / "actual" / (name + ".png"), destination / (name + ".png"))
        print(f"Accepted {args.mode}/{name}")
    return 0


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    run = commands.add_parser("run")
    run.add_argument("--mode", choices=MODES + ["both", "all"], default="all")
    run.add_argument("--skip-build", action="store_true")
    run.add_argument("--native-filter", action="store_true", help="Use normal game filtering for classic too (newgfx always uses xBR2)")
    run.add_argument("--output", type=Path, help="New, not-yet-existing run directory")
    run.add_argument("--baselines", type=Path, default=BASELINES)
    approval = commands.add_parser("accept")
    approval.add_argument("--run", type=Path, required=True)
    approval.add_argument("--mode", choices=MODES, required=True)
    selection = approval.add_mutually_exclusive_group(required=True)
    selection.add_argument("--scenario", action="append")
    selection.add_argument("--all", action="store_true")
    approval.add_argument("--baselines", type=Path, default=BASELINES)
    args = parser.parse_args()
    try:
        return run_tests(args) if args.command == "run" else accept(args)
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        return 2


if __name__ == "__main__":
    sys.exit(main())
