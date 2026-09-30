"""Renders every Store logo/tile/hero PNG from the SVG sources in this folder.

Each asset is an HTML page (written to src/html/) that inlines logo.svg / logo-simple.svg and is
screenshotted by Microsoft Edge in headless mode at the exact pixel size, device scale factor 1.
Sizes of 600px and up are rendered from the vector page at the target size. Edge headless has a
~500px minimum window width, so smaller tiles are rendered at 1200px and downscaled with Pillow
(LANCZOS).

    python store-assets/src/render.py
"""
import pathlib
import subprocess
import sys

from PIL import Image

SRC = pathlib.Path(__file__).resolve().parent
ROOT = SRC.parent
HTML = SRC / "html"
EDGE_CANDIDATES = [
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
]

LOGO = (SRC / "logo.svg").read_text(encoding="utf-8")
LOGO_SMALL = (SRC / "logo-simple.svg").read_text(encoding="utf-8")

BASE_CSS = """
*{margin:0;padding:0;box-sizing:border-box}
html,body{width:100vw;height:100vh;overflow:hidden}
body{font-family:'Segoe UI Variable Display','Segoe UI',system-ui,sans-serif}
svg{display:block}
"""

# Deep navy stage used behind the name on posters/box art (from the mark's own ink colours).
STAGE = """
background:
  radial-gradient(ellipse 70% 55% at 50% 38%, rgba(99,115,160,.55), transparent 70%),
  linear-gradient(165deg,#23314D 0%,#141D2E 100%);
"""


def poster_page() -> str:
    # 2:3 portrait: mark in the upper-middle, name below. All sizes in vh so every render matches.
    return f"""<!doctype html><meta charset="utf-8"><style>{BASE_CSS}
body{{{STAGE}display:flex;flex-direction:column;align-items:center;justify-content:center;gap:5vh}}
.mark{{width:62vw}}
.name{{color:#fff;font-weight:600;font-size:11.2vw;letter-spacing:-.02em;line-height:1}}
.name b{{color:#4DBB72;font-weight:600}}
.rule{{width:14vw;height:.5vh;border-radius:1vh;background:#4DBB72;margin-top:-1vh}}
</style><div class="mark">{LOGO}</div><div class="name">Swift<b>Till</b></div><div class="rule"></div>"""


def boxart_page() -> str:
    return f"""<!doctype html><meta charset="utf-8"><style>{BASE_CSS}
body{{{STAGE}display:flex;flex-direction:column;align-items:center;justify-content:center;gap:4vh}}
.mark{{width:54vw;margin-top:-3vh}}
.name{{color:#fff;font-weight:600;font-size:12.5vw;letter-spacing:-.02em;line-height:1}}
.name b{{color:#4DBB72;font-weight:600}}
</style><div class="mark">{LOGO}</div><div class="name">Swift<b>Till</b></div>"""


def tile_page(svg: str, inset: str) -> str:
    # Mark only, on the light slate plate the original icon uses.
    return f"""<!doctype html><meta charset="utf-8"><style>{BASE_CSS}
body{{background:linear-gradient(160deg,#E6EAF1,#D3D9E3);display:grid;place-items:center}}
.mark{{width:{inset};height:{inset}}}
.mark svg{{width:100%;height:100%}}
</style><div class="mark">{svg}</div>"""


def hero_page() -> str:
    # 16:9, NO TEXT. Key visual sits upper-left/centre; bottom quarter and bottom-right are left
    # as calm background because the Store overlays the title, buttons and rating there.
    receipt_lines = "".join(
        f'<rect x="34" y="{110 + i * 34}" width="{w}" height="12" rx="6" fill="#CBD2DE"/>'
        f'<rect x="{230 - 40}" y="{110 + i * 34}" width="40" height="12" rx="6" fill="#CBD2DE"/>'
        for i, w in enumerate([120, 96, 132, 84, 110])
    )
    receipt = f"""<svg viewBox="0 0 264 420" xmlns="http://www.w3.org/2000/svg">
      <path d="M0 0H264V396 l-22 24 -22 -24 -22 24 -22 -24 -22 24 -22 -24 -22 24 -22 -24 -22 24 -22 -24 -22 24 -22 -24Z" fill="#fff"/>
      <rect x="82" y="34" width="100" height="16" rx="8" fill="#52618A"/>
      <rect x="62" y="62" width="140" height="10" rx="5" fill="#DCE1EA"/>
      {receipt_lines}
      <rect x="34" y="290" width="196" height="3" fill="#DCE1EA"/>
      <rect x="34" y="312" width="90" height="18" rx="9" fill="#1F2A3F"/>
      <rect x="160" y="312" width="70" height="18" rx="9" fill="#4DBB72"/>
      <g fill="#1F2A3F">{''.join(f'<rect x="{40 + x}" y="350" width="{w}" height="28"/>' for x, w in [(0,4),(8,2),(13,6),(23,2),(29,4),(37,2),(42,6),(52,2),(58,4),(66,2),(71,6),(81,2),(87,4),(95,2),(100,6),(110,2),(116,4),(124,2),(129,6),(139,2),(145,4),(153,2),(158,6),(168,2),(174,4),(182,2)])}</g>
    </svg>"""
    scanner_beam = """<svg viewBox="0 0 400 60" xmlns="http://www.w3.org/2000/svg">
      <defs><linearGradient id="b" x1="0" x2="1"><stop offset="0" stop-color="#4DBB72" stop-opacity="0"/><stop offset=".5" stop-color="#6BE39A"/><stop offset="1" stop-color="#4DBB72" stop-opacity="0"/></linearGradient></defs>
      <rect y="26" width="400" height="8" rx="4" fill="url(#b)"/></svg>"""
    return f"""<!doctype html><meta charset="utf-8"><style>{BASE_CSS}
body{{position:relative;background:
  radial-gradient(ellipse 45% 60% at 30% 36%, rgba(77,187,114,.22), transparent 70%),
  radial-gradient(ellipse 60% 70% at 62% 30%, rgba(99,115,160,.45), transparent 72%),
  linear-gradient(170deg,#23314D 0%,#141D2E 78%)}}
.grid{{position:absolute;inset:0;background-image:
  linear-gradient(rgba(255,255,255,.035) 1px,transparent 1px),
  linear-gradient(90deg,rgba(255,255,255,.035) 1px,transparent 1px);
  background-size:4.2vw 4.2vw;mask-image:linear-gradient(180deg,#000 0%,#000 55%,transparent 75%)}}
.mark{{position:absolute;left:24vw;top:13vh;width:30vw;filter:drop-shadow(0 2.4vh 3vh rgba(0,0,0,.35))}}
.receipt{{position:absolute;left:55.5vw;top:9vh;width:13.2vw;transform:rotate(7deg);filter:drop-shadow(0 2vh 2.6vh rgba(0,0,0,.35))}}
.beam{{position:absolute;left:52vw;top:30vh;width:20vw;transform:rotate(7deg);opacity:.9}}
.dot{{position:absolute;border-radius:50%;background:#4DBB72}}
</style>
<div class="grid"></div>
<div class="receipt">{receipt}</div>
<div class="beam">{scanner_beam}</div>
<div class="mark">{LOGO}</div>
<div class="dot" style="left:18vw;top:12vh;width:1.1vw;height:1.1vw;opacity:.8"></div>
<div class="dot" style="left:71vw;top:15vh;width:.7vw;height:.7vw;opacity:.6"></div>
<div class="dot" style="left:15vw;top:52vh;width:.6vw;height:.6vw;opacity:.5"></div>"""


PAGES = {
    "poster": poster_page(),
    "boxart": boxart_page(),
    "tile": tile_page(LOGO, "84vw"),
    "tile-small": tile_page(LOGO_SMALL, "88vw"),
    "hero": hero_page(),
}

# (page, output path relative to store-assets, width, height)
ASSETS = [
    ("poster", "logos/poster-9x16-1440x2160.png", 1440, 2160),
    ("poster", "logos/poster-9x16-720x1080.png", 720, 1080),
    ("boxart", "logos/boxart-1x1-2160x2160.png", 2160, 2160),
    ("boxart", "logos/boxart-1x1-1080x1080.png", 1080, 1080),
    ("tile", "logos/tile-300.png", 300, 300),
    ("tile", "logos/tile-150.png", 150, 150),
    ("tile-small", "logos/tile-71.png", 71, 71),
    ("hero", "hero/superhero-16x9-3840x2160.png", 3840, 2160),
    ("hero", "hero/superhero-16x9-1920x1080.png", 1920, 1080),
]


def edge() -> str:
    for c in EDGE_CANDIDATES:
        if pathlib.Path(c).exists():
            return c
    sys.exit("Microsoft Edge / Chrome not found")


def main() -> None:
    HTML.mkdir(exist_ok=True)
    for name, html in PAGES.items():
        (HTML / f"{name}.html").write_text(html, encoding="utf-8")

    exe = edge()
    for page, out, w, h in ASSETS:
        target = ROOT / out
        target.parent.mkdir(parents=True, exist_ok=True)
        small = min(w, h) < 600
        rw, rh = (1200, round(1200 * h / w)) if small else (w, h)
        shot = target.with_suffix(".tmp.png") if small else target
        subprocess.run([
            exe, "--headless=new", "--disable-gpu", "--hide-scrollbars",
            "--force-device-scale-factor=1", f"--window-size={rw},{rh}",
            f"--screenshot={shot}", (HTML / f"{page}.html").as_uri(),
        ], check=True, capture_output=True)
        if small:
            with Image.open(shot) as im:
                im.convert("RGB").resize((w, h), Image.LANCZOS).save(target, optimize=True)
            shot.unlink()
        print(f"rendered {out}")


if __name__ == "__main__":
    main()
