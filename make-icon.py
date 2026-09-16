#!/usr/bin/env python3
"""
Builds DragIn1.ico for the desktop app from the real brand artwork (Logo_raw.png),
so the desktop app and the DragIn1 Chrome extension read as one product.

Two things worth knowing:

1. Logo_raw.png carries a soft drop shadow. Windows draws its own shadows, and a
   baked-in one makes an icon look blurry and slightly small in the taskbar, so
   the plate is cropped away from it by thresholding alpha.

2. The outlined document survives downscaling to about 32px and turns to grey
   mush below that. So 32px and up use the real artwork; 24px and down use a
   bold solid arrow drawn in the logo's own gradient, sampled from the source
   rather than hardcoded. Same family, still legible at 16px.

Run:  python make-icon.py      (needs Pillow:  pip install pillow)
"""

from PIL import Image, ImageDraw

SRC = "Logo_raw.png"
OUT_ICO = "DragIn1.ico"
SIZES = [256, 128, 64, 48, 40, 32, 24, 20, 16]
SIMPLIFY_AT_OR_BELOW = 24


def load_plate():
    """The logo with its drop shadow trimmed off."""
    src = Image.open(SRC).convert("RGBA")
    solid = src.getchannel("A").point(lambda v: 255 if v > 200 else 0)
    box = solid.getbbox()
    if not box:
        raise SystemExit("Could not find the logo plate in " + SRC)
    return src.crop(box)


def sample_gradient(plate):
    n = plate.size[0]
    inset = max(8, n // 13)
    return (
        plate.getpixel((inset, inset))[:3],
        plate.getpixel((n - inset, n - inset))[:3],
    )


def simplified(size, c1, c2):
    """Bold down arrow on the brand gradient. Built at 512 then downscaled."""
    N = 512
    grad = Image.new("RGB", (N, N))
    px = grad.load()
    for y in range(N):
        for x in range(N):
            t = (x + y) / (2.0 * (N - 1))
            px[x, y] = (
                int(c1[0] + (c2[0] - c1[0]) * t),
                int(c1[1] + (c2[1] - c1[1]) * t),
                int(c1[2] + (c2[2] - c1[2]) * t),
            )

    img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    mask = Image.new("L", (N, N), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, N - 1, N - 1], radius=115, fill=255)
    img.paste(grad.convert("RGBA"), (0, 0), mask)

    d = ImageDraw.Draw(img)
    d.rounded_rectangle([226, 105, 286, 280], radius=27, fill=(255, 255, 255))
    d.polygon([(155, 250), (357, 250), (256, 400)], fill=(255, 255, 255))
    return img.resize((size, size), Image.LANCZOS)


def main():
    plate = load_plate()
    c1, c2 = sample_gradient(plate)
    print("plate %dpx   gradient #%02X%02X%02X -> #%02X%02X%02X"
          % (plate.size[0], c1[0], c1[1], c1[2], c2[0], c2[1], c2[2]))

    frames = []
    for s in SIZES:
        if s <= SIMPLIFY_AT_OR_BELOW:
            frames.append(simplified(s, c1, c2))
        else:
            frames.append(plate.resize((s, s), Image.LANCZOS))

    frames[0].save(
        OUT_ICO,
        format="ICO",
        sizes=[(f.width, f.height) for f in frames],
        append_images=frames[1:],
    )

    plate.resize((256, 256), Image.LANCZOS).save("DragIn1-256.png")

    # preview strip
    shown = [256, 128, 64, 48, 32, 24, 16]
    w = sum(shown) + 20 * (len(shown) + 1)
    sheet = Image.new("RGBA", (w, 300), (18, 18, 18, 255))
    x = 20
    for s in shown:
        f = frames[SIZES.index(s)]
        sheet.paste(f, (x, 24 + (256 - s) // 2), f)
        x += s + 20
    sheet.save("icon-preview.png")

    print("wrote " + OUT_ICO + " with sizes " + str(SIZES))


if __name__ == "__main__":
    main()
