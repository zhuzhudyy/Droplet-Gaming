"""Measure same-camera, same-time reflection OFF/ON PNG evidence; never edits images.

Requires the already-installed Pillow package. A numeric difference is evidence of
response, not an automatic verdict that the reflection is artistically sufficient.
"""

import argparse
import json
from pathlib import Path

from PIL import Image


def metrics(before, after):
    changed = warm = red = 0
    total = [0, 0, 0]
    maximum = 0
    pixels = before.width * before.height
    flatten = lambda image: image.get_flattened_data() if hasattr(image, "get_flattened_data") else image.getdata()
    for old, new in zip(flatten(before), flatten(after)):
        difference = [new[channel] - old[channel] for channel in range(3)]
        if any(difference):
            changed += 1
        if difference[0] > 2:
            red += 1
        if difference[0] > difference[2] + 2 and difference[0] > 2:
            warm += 1
        maximum = max(maximum, *(abs(value) for value in difference))
        for channel in range(3):
            total[channel] += difference[channel]
    return {
        "pixels": pixels,
        "changedPixels": changed,
        "redIncreaseGreaterThan2of255Pixels": red,
        "redIncreaseExceedsBlueBy2of255Pixels": warm,
        "maximumAbsoluteChannelDeltaOf255": maximum,
        "meanSignedRgbDeltaOf255": [value / pixels for value in total],
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("off", type=Path)
    parser.add_argument("on", type=Path)
    parser.add_argument("--roi", nargs=4, type=int, metavar=("LEFT", "TOP", "RIGHT", "BOTTOM"))
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    off, on = Image.open(args.off).convert("RGB"), Image.open(args.on).convert("RGB")
    if off.size != on.size:
        parser.error("Both images must have identical dimensions.")
    report = {"off": str(args.off), "on": str(args.on), "width": off.width, "height": off.height,
              "scope": "RGB8 PNG comparison of an explicitly frozen actual rendered OFF/ON pair; no image pixels were changed. Numeric difference does not establish visual quality; inspect both source images.",
              "fullImage": metrics(off, on)}
    if args.roi:
        x0, y0, x1, y1 = args.roi
        if not (0 <= x0 < x1 <= off.width and 0 <= y0 < y1 <= off.height):
            parser.error("ROI must be strictly inside the source image bounds.")
        report["roi"] = {"bounds": args.roi, **metrics(off.crop(args.roi), on.crop(args.roi))}
    result = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(result, encoding="utf-8")
        print(args.output)
    else:
        print(result)


if __name__ == "__main__":
    main()
