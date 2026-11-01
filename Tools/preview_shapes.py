# -*- coding: utf-8 -*-
"""
Силуэты стихий вне Unity.

Повторяет SpriteFactoryShapes.ShapeEdge один в один. Форма — первое, по чему
игрок различает скины, и проверять её по формуле на глаз бессмысленно: «капля»
и «бугры» в коде выглядят одинаково убедительно, а на экране одна из них
окажется кляксой.

    python3 Tools/preview_shapes.py
"""
import math
import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "preview_shapes.png")

NAMES = ["Sphere", "Drop", "Faceted", "Spiky", "Ringed", "Wisp", "Lumpy", "Star", "Blocky"]


def shape_edge(shape, x, y):
    angle = math.atan2(y, x)
    mag = math.hypot(x, y)

    if shape == 1:      # Drop
        up = max(0.0, y / max(1e-4, mag))
        return 0.72 + 0.28 * (up ** 6)

    if shape == 2:      # Faceted
        sides = 8
        step = math.pi * 2 / sides
        local = ((angle + math.pi / sides) % step) - step * 0.5
        return 0.97 * math.cos(step * 0.5) / max(0.2, math.cos(local))

    if shape == 3:      # Spiky
        teeth = math.cos(angle * 11)
        return 0.84 + 0.16 * (max(0.0, teeth) ** 0.6)

    if shape == 4:      # Ringed
        band = abs(y + x * 0.2)
        return 1.0 if band < 0.17 else 0.70

    if shape == 5:      # Wisp
        return (0.86 + 0.04 * math.sin(angle * 5) + 0.04 * math.sin(angle * 11 + 1.3)
                + 0.03 * math.sin(angle * 19 + 0.4))

    if shape == 6:      # Lumpy
        return 0.82 + 0.16 * math.sin(angle * 3 + 0.7)

    if shape == 7:      # Star
        arms = abs(math.cos(angle * 2))
        return 0.40 + 0.60 * (arms ** 3)

    if shape == 8:      # Blocky
        ax = abs(x) / max(1e-4, mag)
        ay = abs(y) / max(1e-4, mag)
        side = 0.70 / max(1e-4, max(ax, ay))
        chamfer = 1.26 / max(1e-4, ax + ay)
        return min(1.0, side, chamfer)

    return 1.0


def shell(shape, core_rgb):
    """Стекло поверх цветной начинки — так скин и выглядит в игре."""
    def shade(p):
        x, y = p
        d = math.hypot(x, y)
        edge = shape_edge(shape, x, y)
        if d > edge:
            return (0, 0, 0, 0)

        t = d / edge if edge > 0.001 else 1.0

        # начинка под стеклом
        r, g, b = core_rgb
        k = 1.0 - t * 0.35
        under = (r * k, g * k, b * k)

        haze = 0.07 + (0.16 - 0.07) * t
        cr = under[0] * (1 - haze) + 0.82 * haze
        cg = under[1] * (1 - haze) + 0.98 * haze
        cb = under[2] * (1 - haze) + 0.96 * haze

        if t > 0.9:
            kk = min(1.0, max(0.0, (min(t, 0.97) - 0.9) / 0.07))
            a = 0.12 + kk * 0.5
            cr = cr * (1 - a) + a
            cg = cg * (1 - a) + a
            cb = cb * (1 - a) + a

        hx = (x + 0.32) / 0.22
        hy = (y - 0.36) / 0.13
        if hx * hx + hy * hy < 1.0 and t < 0.95:
            cr = cr * 0.2 + 0.8
            cg = cg * 0.2 + 0.8
            cb = cb * 0.2 + 0.8

        return (cr, cg, cb, 1.0)

    return shade


def render(shade, size=128, ss=3):
    img = Image.new("RGBA", (size, size))
    px = img.load()
    for yy in range(size):
        for xx in range(size):
            r = g = b = a = 0.0
            for sy in range(ss):
                for sx in range(ss):
                    u = (xx + (sx + 0.5) / ss) / size * 2 - 1
                    v = (yy + (sy + 0.5) / ss) / size * 2 - 1
                    c = shade((u, -v))
                    r += c[0] * c[3]
                    g += c[1] * c[3]
                    b += c[2] * c[3]
                    a += c[3]
            n = ss * ss
            if a > 1e-4:
                px[xx, yy] = (int(min(1, r / a) * 255), int(min(1, g / a) * 255),
                              int(min(1, b / a) * 255), int(a / n * 255))
            else:
                px[xx, yy] = (0, 0, 0, 0)
    return img


# цвет начинки каждой стихии — примерно как в MaterialFxTable
CORES = [
    (0.56, 0.85, 1.00),   # Sphere  вода
    (1.00, 0.36, 0.18),   # Drop    огонь
    (0.75, 0.92, 1.00),   # Faceted лёд
    (0.81, 0.89, 1.00),   # Spiky   электричество
    (1.00, 0.85, 0.54),   # Ringed  золото
    (0.36, 0.30, 0.52),   # Wisp    тень
    (0.66, 1.00, 0.36),   # Lumpy   кислота
    (0.72, 0.56, 1.00),   # Star    космос
    (0.36, 1.00, 0.60),   # Blocky  распад
]


def main():
    cell = 140
    cols = 5
    rows = (len(NAMES) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell, rows * cell), (8, 21, 34, 255))

    for i, name in enumerate(NAMES):
        img = render(shell(i, CORES[i]))
        cx = (i % cols) * cell + (cell - 128) // 2
        cy = (i // cols) * cell + (cell - 128) // 2
        sheet.alpha_composite(img, (cx, cy))
        print("%-9s" % name)

    sheet.save(OUT)
    print("\nсохранено: " + OUT)


if __name__ == "__main__":
    main()
