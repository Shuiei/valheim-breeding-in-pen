# Draws thunderstore/icon.png (256x256): a piglet with a heart, inside a wooden pen fence.
# python3 thunderstore/make_icon.py   (needs Pillow)
import os
from PIL import Image, ImageDraw

S = 4  # draw at 4x, then shrink for smooth edges
W = 256 * S
img = Image.new("RGBA", (W, W), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
def b(x0, y0, x1, y1):
    return (x0 * S, y0 * S, x1 * S, y1 * S)
d.rounded_rectangle(b(8, 8, 248, 248), radius=40 * S, fill=(34, 50, 36, 255), outline=(96, 130, 86, 255), width=4 * S)
d.rounded_rectangle(b(10, 170, 246, 246), radius=38 * S, fill=(58, 84, 50, 255), corners=(False, False, True, True))  # grass
d.rounded_rectangle(b(8, 8, 248, 248), radius=40 * S, outline=(96, 130, 86, 255), width=4 * S)
wood, dark = (176, 128, 80, 255), (120, 84, 52, 255)
# Back fence: posts and two rails.
for x in range(28, 236, 40):
    d.rounded_rectangle(b(x, 92, x + 14, 196), radius=3 * S, fill=wood, outline=dark, width=2 * S)
for y in (112, 150):
    d.rounded_rectangle(b(20, y, 236, y + 12), radius=3 * S, fill=wood, outline=dark, width=2 * S)
# Piglet.
pink, pinkd = (244, 168, 176, 255), (214, 128, 140, 255)
d.ellipse(b(70, 140, 170, 206), fill=pink, outline=pinkd, width=2 * S)  # body
for x in (84, 104, 132, 150):
    d.rounded_rectangle(b(x, 196, x + 12, 216), radius=4 * S, fill=pinkd)
d.ellipse(b(140, 124, 196, 178), fill=pink, outline=pinkd, width=2 * S)  # head
d.polygon([(146 * S, 132 * S), (150 * S, 112 * S), (162 * S, 128 * S)], fill=pinkd)  # ear
d.polygon([(176 * S, 126 * S), (184 * S, 108 * S), (190 * S, 130 * S)], fill=pinkd)  # ear
d.ellipse(b(176, 146, 202, 166), fill=(232, 140, 154, 255), outline=pinkd, width=2 * S)  # snout
for x in (183, 192):
    d.ellipse(b(x, 153, x + 4, 159), fill=(120, 60, 70, 255))
d.ellipse(b(160, 140, 168, 148), fill=(40, 28, 30, 255))  # eye
d.arc(b(58, 150, 76, 168), start=90, end=330, fill=pinkd, width=3 * S)  # tail
# Heart above.
cx, cy = 128, 64
red = (230, 70, 90, 255)
d.ellipse(b(cx - 24, cy - 20, cx + 2, cy + 6), fill=red)
d.ellipse(b(cx - 2, cy - 20, cx + 24, cy + 6), fill=red)
d.polygon([(cx - 23) * S, (cy - 3) * S, (cx + 23) * S, (cy - 3) * S, cx * S, (cy + 24) * S], fill=red)
img.resize((256, 256), Image.LANCZOS).save(os.path.join(os.path.dirname(os.path.abspath(__file__)), "icon.png"))
