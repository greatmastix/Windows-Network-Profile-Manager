"""Builds src/NetworkProfiles.App/Assets/app.ico: the app's dark tile with a red network triangle
(three nodes joined by links), in the same palette and style as the LTC-MTC icon.

Usage: python3 tools/make_icon.py src/NetworkProfiles.App/Assets/app.ico preview.png   (needs Pillow)
"""
import math
import sys
from PIL import Image, ImageDraw

BG, BORDER, RED = (21, 21, 23, 255), (42, 42, 47, 255), (239, 75, 85, 255)


def draw(size):
    S = 1024                                  # draw big, downsample: smooth edges at every size
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    small = size <= 32
    pad = 8 if small else 48
    d.rounded_rectangle([pad, pad, S - pad, S - pad], radius=190, fill=BG,
                        outline=None if small else BORDER, width=0 if small else 18)

    # Equilateral triangle, centred on the tile. Small sizes get thicker links and bigger nodes so they survive 16 px.
    link, node, r = (120, 150, 300) if small else (64, 96, 330)
    cx, cy = S / 2, S / 2 + r * 0.12          # nudge down: the triangle's visual centre sits below its circumcentre
    nodes = [(cx + r * math.cos(a), cy - r * math.sin(a)) for a in (math.pi / 2, math.pi * 7 / 6, math.pi * 11 / 6)]
    for (ax, ay), (bx, by) in zip(nodes, nodes[1:] + nodes[:1]):
        d.line([(ax, ay), (bx, by)], fill=RED, width=link)
    for x, y in nodes:
        d.ellipse([x - node, y - node, x + node, y + node], fill=RED)
    return im.resize((size, size), Image.LANCZOS)


sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
imgs = [draw(s) for s in sizes]
imgs[-1].save(sys.argv[1], format="ICO", sizes=[(s, s) for s in sizes], append_images=imgs[:-1])
sheet = Image.new("RGBA", (sum(sizes) + 20 * len(sizes) + 20, 300), (12, 12, 14, 255))
x = 20
for s, im in zip(sizes, imgs):
    sheet.alpha_composite(im, (x, 150 - s // 2))
    x += s + 20
sheet.save(sys.argv[2])
