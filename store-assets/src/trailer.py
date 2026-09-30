"""Builds the Store trailer + thumbnail from the real screen recording.

Input : capture/runtime/trailer/raw.mp4 + scenes.json   (capture/Record-Trailer.ps1)
Output: trailer/swifttill-trailer-1080p.mp4             (H.264 High, 30 fps, AAC silent track)
        trailer/trailer-thumbnail-1920x1080.png

The app footage is only cut, time-scaled and placed on the brand background (scaled to
1600x900 with rounded corners); nothing inside the app window is edited or drawn over.

    python store-assets/src/trailer.py
"""
import json
import pathlib
import subprocess

from PIL import Image, ImageDraw, ImageFilter, ImageFont

import frame
import render

SRC = pathlib.Path(__file__).resolve().parent
ROOT = SRC.parent
REC = ROOT / "capture" / "runtime" / "trailer"
WORK = REC / "build"
OUT = ROOT / "trailer"
FFMPEG = next((ROOT / "tools" / "imageio_ffmpeg" / "binaries").glob("ffmpeg*.exe"))

W, H = 1920, 1080
VX, VY, VW, VH, RADIUS = 160, 150, 1600, 900, 16
XFADE = 0.5
FPS = 30

# scene id, caption (<= 7 words), playback speed, trim from start / end (s)
SCENES = [
    ("billing",   "Scan, bill and give change in seconds", 1.3, 0.2, 0.0),
    ("receipt",   "Your shop's name on every receipt",     1.0, 0.1, 0.0),
    ("inventory", "See what needs restocking, instantly",  1.0, 0.1, 0.0),
    ("report",    "Sales and profit at a glance",          1.1, 0.1, 0.0),
    ("khata",     "Customer credit (khata) made simple",   1.0, 0.1, 0.0),
]
INTRO_S, OUTRO_S = 3.5, 4.5


def run(args: list) -> None:
    subprocess.run([str(FFMPEG), "-hide_banner", "-loglevel", "error", "-y", *map(str, args)], check=True)


def card_html(sub: str, extra: str = "") -> str:
    return f"""<!doctype html><meta charset="utf-8"><style>{render.BASE_CSS}
body{{{render.STAGE}display:flex;flex-direction:column;align-items:center;justify-content:center}}
.mark{{width:24vw}}
.name{{color:#fff;font-weight:600;font-size:7.4vw;letter-spacing:-.02em;line-height:1;margin-top:3vh}}
.name b{{color:#4DBB72;font-weight:600}}
.sub{{color:#C9D2E3;font-size:2.3vw;margin-top:3.2vh}}
.extra{{color:#8FA0BF;font-size:1.55vw;margin-top:3.2vh;letter-spacing:.02em}}
</style><div class="mark">{render.LOGO}</div><div class="name">Swift<b>Till</b></div>
<div class="sub">{sub}</div>{f'<div class="extra">{extra}</div>' if extra else ''}"""


def render_card(name: str, html: str) -> pathlib.Path:
    page = WORK / f"{name}.html"
    page.write_text(html, encoding="utf-8")
    png = WORK / f"{name}.png"
    subprocess.run([render.edge(), "--headless=new", "--disable-gpu", "--hide-scrollbars",
                    "--force-device-scale-factor=1", f"--window-size={W},{H}",
                    f"--screenshot={png}", page.as_uri()], check=True, capture_output=True)
    return png


def caption_bg(scene: str, caption: str) -> pathlib.Path:
    img = frame.background().resize((W, H))
    d = ImageDraw.Draw(img)
    font = ImageFont.truetype(frame.FONT, 58)
    tw = d.textlength(caption, font=font)
    d.text(((W - tw) / 2, 40), caption, font=font, fill="white")
    d.rounded_rectangle([(W - 110) / 2, 118, (W + 110) / 2, 123], radius=3, fill=(0x4D, 0xBB, 0x72))
    # soft shadow under the video area
    shadow = Image.new("L", (W, H), 0)
    ImageDraw.Draw(shadow).rounded_rectangle([VX, VY + 14, VX + VW, VY + VH + 14], RADIUS, fill=140)
    img.paste((5, 8, 15), (0, 0), shadow.filter(ImageFilter.GaussianBlur(20)))
    path = WORK / f"bg-{scene}.png"
    img.save(path)
    return path


def corner_mask(bg: pathlib.Path, scene: str) -> pathlib.Path:
    """Background pixels only in the four rounded corners of the video rect; transparent elsewhere."""
    base = Image.open(bg).convert("RGBA")
    hole = Image.new("L", (W, H), 255)
    ImageDraw.Draw(hole).rounded_rectangle([VX, VY, VX + VW - 1, VY + VH - 1], RADIUS, fill=0)
    outside = Image.new("L", (W, H), 0)
    ImageDraw.Draw(outside).rectangle([VX, VY, VX + VW - 1, VY + VH - 1], fill=255)
    alpha = Image.composite(hole, Image.new("L", (W, H), 0), outside)
    base.putalpha(alpha)
    path = WORK / f"mask-{scene}.png"
    base.save(path)
    return path


def still_segment(png: pathlib.Path, seconds: float, out: pathlib.Path) -> float:
    run(["-loop", "1", "-t", seconds, "-i", png, "-vf", f"fps={FPS},format=yuv420p",
         "-c:v", "libx264", "-crf", "12", "-preset", "fast", out])
    return seconds


def scene_segment(scene, marks, out: pathlib.Path) -> float:
    sid, caption, speed, trim_a, trim_b = scene
    start = marks[sid]["start"] + trim_a
    end = marks[sid]["end"] - trim_b
    dur = (end - start) / speed
    bg = caption_bg(sid, caption)
    mask = corner_mask(bg, sid)
    graph = (f"[1:v]setpts=(PTS-STARTPTS)/{speed},fps={FPS},scale={VW}:{VH}:flags=lanczos[v];"
             f"[0:v][v]overlay={VX}:{VY}:shortest=1[b];[b][2:v]overlay=0:0,format=yuv420p")
    run(["-loop", "1", "-i", bg, "-ss", f"{start:.3f}", "-t", f"{end - start:.3f}", "-i", REC / "raw.mp4",
         "-loop", "1", "-i", mask, "-filter_complex", graph, "-t", f"{dur:.3f}",
         "-c:v", "libx264", "-crf", "12", "-preset", "fast", out])
    return dur


def main() -> None:
    WORK.mkdir(parents=True, exist_ok=True)
    OUT.mkdir(exist_ok=True)
    marks = json.loads((REC / "scenes.json").read_text(encoding="utf-8-sig"))

    intro = render_card("intro", card_html("Point of sale for small shops on Windows"))
    outro = render_card("outro", card_html("Fast billing · Stock alerts · Khata · Reports",
                                           "Works offline &nbsp;·&nbsp; Available on the Microsoft Store"))

    segs, durs = [], []
    durs.append(still_segment(intro, INTRO_S, WORK / "s0.mp4")); segs.append(WORK / "s0.mp4")
    for i, sc in enumerate(SCENES, 1):
        p = WORK / f"s{i}.mp4"
        durs.append(scene_segment(sc, marks, p)); segs.append(p)
        print(f"scene {sc[0]:<10} {durs[-1]:5.2f}s")
    p = WORK / f"s{len(segs)}.mp4"
    durs.append(still_segment(outro, OUTRO_S, p)); segs.append(p)

    # xfade chain
    inputs, parts, prev, t = [], [], "[n0]", 0.0
    for k, s in enumerate(segs):
        inputs += ["-i", s]
        parts.append(f"[{k}:v]fps={FPS},settb=AVTB,setsar=1,format=yuv420p[n{k}]")
    for k in range(1, len(segs)):
        t += durs[k - 1] - XFADE
        label = f"[x{k}]"
        parts.append(f"{prev}[n{k}]xfade=transition=fade:duration={XFADE}:offset={t:.3f}{label}")
        prev = label
    total = sum(durs) - XFADE * (len(segs) - 1)
    parts.append(f"{prev}fade=in:st=0:d=0.6,fade=out:st={total - 0.8:.3f}:d=0.8,format=yuv420p[vout]")
    final = OUT / "swifttill-trailer-1080p.mp4"
    run([*inputs, "-f", "lavfi", "-t", f"{total:.3f}", "-i", "anullsrc=channel_layout=stereo:sample_rate=48000",
         "-filter_complex", ";".join(parts), "-map", "[vout]", "-map", f"{len(segs)}:a",
         "-c:v", "libx264", "-profile:v", "high", "-level", "4.1", "-preset", "slow", "-crf", "18",
         "-r", FPS, "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "128k", "-shortest",
         "-movflags", "+faststart", final])
    print(f"trailer {final.name}: {total:.1f}s")

    # Thumbnail: the intro card (logo + name), PNG under 2 MB.
    thumb = OUT / "trailer-thumbnail-1920x1080.png"
    Image.open(intro).convert("RGB").save(thumb, optimize=True)
    print(f"thumbnail {thumb.name}: {thumb.stat().st_size / 1024:.0f} KB")


if __name__ == "__main__":
    main()
