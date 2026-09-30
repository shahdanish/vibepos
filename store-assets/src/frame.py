"""Builds screenshots/framed/*.png: each raw capture on a brand background with a short caption.

The raw 1920x1080 capture is pasted pixel-for-pixel (no scaling, no retouching) onto a
2560x1440 canvas, so the app UI inside the frame is exactly what was captured.

    python store-assets/src/frame.py
"""
import pathlib

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = pathlib.Path(__file__).resolve().parent.parent
SHOTS = ROOT / "screenshots"
OUT = SHOTS / "framed"
W, H = 2560, 1440
SHOT_W, SHOT_H = 1920, 1080
TOP = 250                      # caption band; screenshot sits below it
FONT = r"C:\Windows\Fonts\seguisb.ttf"

# <= 6 words each, and every claim is backed by the screen underneath it.
CAPTIONS = {
    "01-billing.png":      "Scan, bill and print in seconds",
    "02-receipt.png":      "Your shop's name on every receipt",
    "03-inventory.png":    "Know what to reorder, instantly",
    "04-sales-report.png": "Sales and profit at a glance",
    "05-customers.png":    "Customer credit (khata) made simple",
    "06-users.png":        "Staff logins with role permissions",
    "07-backup.png":       "One-click backup and restore",
}


def background() -> Image.Image:
    top, bottom = (0x23, 0x31, 0x4D), (0x14, 0x1D, 0x2E)
    bg = Image.new("RGB", (W, H))
    px = ImageDraw.Draw(bg)
    for y in range(H):
        t = y / (H - 1)
        px.line([(0, y), (W, y)], fill=tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)))
    return bg


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype(FONT, 96)
    x0 = (W - SHOT_W) // 2
    for name, caption in CAPTIONS.items():
        raw = Image.open(SHOTS / name).convert("RGB")
        assert raw.size == (SHOT_W, SHOT_H), f"{name} is {raw.size}, expected 1920x1080"

        canvas = background()
        # soft shadow behind the capture
        shadow = Image.new("L", (W, H), 0)
        ImageDraw.Draw(shadow).rectangle([x0, TOP + 24, x0 + SHOT_W, TOP + SHOT_H + 24], fill=150)
        canvas.paste((5, 8, 15), (0, 0), shadow.filter(ImageFilter.GaussianBlur(28)))
        canvas.paste(raw, (x0, TOP))                       # 1:1, unaltered

        d = ImageDraw.Draw(canvas)
        tw = d.textlength(caption, font=font)
        d.text(((W - tw) / 2, 64), caption, font=font, fill="white")
        d.rounded_rectangle([(W - 160) / 2, 196, (W + 160) / 2, 204], radius=4, fill=(0x4D, 0xBB, 0x72))

        # Prove the capture is untouched inside the frame.
        assert list(canvas.crop((x0, TOP, x0 + SHOT_W, TOP + SHOT_H)).get_flattened_data()) == list(raw.get_flattened_data())
        canvas.save(OUT / name, optimize=True)
        print(f"framed {name}")


if __name__ == "__main__":
    main()
