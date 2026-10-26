"""Export platform icons from the two supplied PNG halves. Requires Pillow.

python tools/generate_app_icons.py --source <combined-transparent-png>
python tools/generate_app_icons.py  # regenerate from the checked-in halves
"""

import argparse
from pathlib import Path

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]
BRANDING = ROOT / "assets" / "branding"
RES = ROOT / "android" / "app" / "src" / "main" / "res"
WINDOWS_SIZES = (16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 128, 256)
DENSITIES = {"mdpi": 1, "hdpi": 1.5, "xhdpi": 2, "xxhdpi": 3, "xxxhdpi": 4}


def artwork(half):
    # Alpha=1 specks occur far outside the supplied artwork. Use alpha>=2 only
    # to locate its bounds, retaining all original pixels inside a 16px margin.
    bounds = half.getchannel("A").point(lambda alpha: 255 if alpha >= 2 else 0).getbbox()
    if bounds is None:
        raise ValueError("The supplied half has no visible artwork")
    left, top, right, bottom = bounds
    return half.crop((max(0, left - 16), max(0, top - 16),
                      min(half.width, right + 16), min(half.height, bottom + 16)))


def contain(image, canvas_size, artwork_size):
    scale = artwork_size / max(image.size)
    size = tuple(max(1, round(dimension * scale)) for dimension in image.size)
    # Premultiplied-alpha resizing avoids dark fringes on translucent glow.
    resized = image.convert("RGBa").resize(size, Image.Resampling.LANCZOS).convert("RGBA")
    result = Image.new("RGBA", (canvas_size, canvas_size))
    result.paste(resized, ((canvas_size - size[0]) // 2, (canvas_size - size[1]) // 2))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path)
    args = parser.parse_args()
    BRANDING.mkdir(parents=True, exist_ok=True)
    if args.source:
        with Image.open(args.source) as source:
            if source.mode != "RGBA":
                raise ValueError("The source must already have an RGBA transparency channel")
            middle = source.width // 2
            source.crop((0, 0, middle, source.height)).save(BRANDING / "desktop.png")
            source.crop((middle, 0, source.width, source.height)).save(BRANDING / "android.png")

    desktop = artwork(Image.open(BRANDING / "desktop.png").convert("RGBA"))
    android = artwork(Image.open(BRANDING / "android.png").convert("RGBA"))
    windows = BRANDING / "windows"
    windows.mkdir(exist_ok=True)
    frames = [contain(desktop, size, size) for size in WINDOWS_SIZES]
    frames[-1].save(windows / "desktop.ico", sizes=[(s, s) for s in WINDOWS_SIZES],
                    append_images=frames[:-1])
    frames[-1].save(windows / "desktop-256.png")

    for density, scale in DENSITIES.items():
        adaptive_size = round(108 * scale)
        folder = RES / f"drawable-{density}"
        folder.mkdir(exist_ok=True)
        contain(android, adaptive_size, round(66 * scale)).save(folder / "ic_launcher_foreground.png")
    print("Generated transparent Windows ICO/PNG and Android mdpi–xxxhdpi icons.")


if __name__ == "__main__":
    main()
