# -*- coding: utf-8 -*-
"""
Отрисовать графику наград вне Unity.

Спрайты процедурные: это функции от точки, и повторить их на Python — дословный
перенос, а не приближение. Смысл в том, чтобы увидеть сундук, колесо и медали
до сборки проекта: внутри Unity это проверяется только запуском, а ошибка в
формуле видна сразу.

Запуск:  python Tools/preview_rewards.py
Итог:    Tools/preview_rewards.png
"""
import math
import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "preview_rewards.png")


def hexc(h):
    return (int(h[0:2], 16) / 255.0, int(h[2:4], 16) / 255.0, int(h[4:6], 16) / 255.0)


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return (a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t)


def inv_lerp(a, b, v):
    if a == b:
        return 0.0
    return max(0.0, min(1.0, (v - a) / (b - a)))


def smoothstep(a, b, t):
    t = max(0.0, min(1.0, t))
    return a + (b - a) * (t * t * (3 - 2 * t))


def clear():
    return (0.0, 0.0, 0.0, 0.0)


def rgba(c, a=1.0):
    return (c[0], c[1], c[2], a)


# --- палитра (Palette.cs) -----------------------------------------------------
DEEP = hexc("081522")
DEEP2 = hexc("0D2436")
AQUA = hexc("5BE9D1")
CORAL = hexc("FF6B5B")
FOAM = hexc("EAF6F2")
GOLD = hexc("FFD166")

CHEST_WOOD = hexc("5A3A22")
CHEST_WOOD_DARK = hexc("32200F")
CHEST_BAND = hexc("C9922E")
CHEST_BAND_DARK = hexc("7A5516")
CHEST_PATINA = hexc("4FBFA8")


# --- спрайты (SpriteFactoryRewards.cs) ---------------------------------------
def chest(open_):
    def shade(p):
        x, y = p
        body = abs(x) <= 0.78 and -0.78 <= y <= 0.1

        lid_cy = 0.46 if open_ else 0.12
        lid_tilt = 0.38 if open_ else 0.0
        lx = (x - lid_tilt) * 1.0
        ly = (y - lid_cy) * 1.9
        lmag = math.hypot(lx, ly)
        lid = lmag <= 0.78 and (ly >= -0.55 if open_ else ly >= -0.05)

        if not body and not lid:
            if open_:
                glow = math.hypot(x, (y - 0.08) * 1.4)
                if glow < 0.95:
                    return rgba(GOLD, smoothstep(0.55, 0.0, inv_lerp(0.0, 0.95, glow)) * 0.75)
            return clear()

        c = lerp(CHEST_WOOD_DARK, CHEST_WOOD, inv_lerp(-0.8, 0.3, y))
        plank = ((x + 1.0) * 2.6) % 1.0
        if plank < 0.08 or plank > 0.92:
            c = lerp(c, CHEST_WOOD_DARK, 0.7)

        if lid and ly > 0.35:
            c = lerp(c, FOAM, 0.18)

        band_h = body and (abs(y + 0.62) < 0.09 or abs(y + 0.18) < 0.07)
        band_v = abs(abs(x) - 0.52) < 0.07
        if band_h or band_v or (lid and abs(ly - 0.42) < 0.12):
            c = lerp(CHEST_BAND_DARK, CHEST_BAND, inv_lerp(-0.6, 0.6, y - x * 0.3))
            stain = math.sin(x * 6.1 + y * 3.7) * math.cos(y * 5.3 - x * 2.9)
            if stain > 0.3:
                c = lerp(c, CHEST_PATINA, (stain - 0.3) * 1.2)

        if body and abs(x) < 0.16 and -0.34 < y < 0.06:
            c = lerp(CHEST_BAND_DARK, CHEST_BAND, 0.7)
            if math.hypot(x, y + 0.14) < 0.07:
                c = CHEST_WOOD_DARK

        edge = max(abs(x) - 0.72, -(y + 0.72))
        if body and edge > 0:
            c = lerp(c, CHEST_WOOD_DARK, 0.8)

        return rgba(c)

    return shade


def wheel_face():
    sectors = 6
    rim_outer = hexc("1A4A58")
    rim_inner = hexc("0D2436")
    light = hexc("1C7A8E")
    dark = hexc("0A2B38")

    def shade(p):
        x, y = p
        r = math.hypot(x, y)
        if r > 1.0:
            return clear()

        if r > 0.86:
            notch = (math.atan2(y, x) * sectors * 3.0 / math.pi) % 1.0
            rim = lerp(rim_inner, rim_outer, 1.0 if notch < 0.5 else 0.45)
            fade = smoothstep(1.0, 0.0, inv_lerp(0.96, 1.0, r))
            return rgba(rim, fade)

        angle = math.atan2(y, x) + math.pi
        index = int(angle / (math.pi * 2) * sectors) % sectors
        c = light if index % 2 == 0 else dark

        within = (angle / (math.pi * 2) * sectors) % 1.0
        if within < 0.02 or within > 0.98:
            c = lerp(c, AQUA, 0.5)

        if r < 0.2:
            c = lerp(DEEP2, AQUA, inv_lerp(0.2, 0.0, r) * 0.35)
        if abs(r - 0.2) < 0.02:
            c = lerp(c, AQUA, 0.6)

        return rgba(c)

    return shade


def medal(tier):
    if tier == 3:
        face, edge, rays = hexc("FFD166"), hexc("A8761C"), 12
    elif tier == 2:
        face, edge, rays = hexc("D7E3EA"), hexc("7C8F9C"), 8
    else:
        face, edge, rays = hexc("C9922E"), hexc("6E4A12"), 6

    def shade(p):
        x, y = p
        r = math.hypot(x, y)
        if r > 1.0:
            return clear()

        if r > 0.68:
            angle = math.atan2(y, x)
            wave = math.cos(angle * rays) * 0.5 + 0.5
            reach = 0.68 + (1.0 - 0.68) * wave
            if r > reach:
                return clear()
            return rgba(edge, 0.85)

        c = lerp(edge, face, inv_lerp(-0.7, 0.7, y - x * 0.4))
        if abs(r - 0.62) < 0.05:
            c = lerp(c, edge, 0.7)

        dx, dy = x, (y - 0.1) * 0.8
        drop = math.hypot(dx, dy) - max(0.0, dy) * 0.85
        if drop < 0.24:
            c = lerp(c, edge, 0.6)
        if drop < 0.14:
            c = lerp(c, face, 0.55)

        return rgba(c)

    return shade


def gift_box():
    box = hexc("2A7E8C")
    box_dark = hexc("12414A")

    def shade(p):
        x, y = p
        lid = 0.34 < y < 0.62 and abs(x) < 0.82
        body = -0.78 <= y <= 0.34 and abs(x) < 0.72

        if y > 0.55:
            ll = math.hypot(x + 0.19, y - 0.73)
            lr = math.hypot(x - 0.19, y - 0.73)
            if ll < 0.17 or lr < 0.17:
                return rgba(GOLD)
            if ll < 0.22 or lr < 0.22:
                return rgba(lerp(GOLD, box_dark, 0.45))

        if not lid and not body:
            return clear()

        c = lerp(box_dark, box, inv_lerp(-0.8, 0.6, y))
        if lid:
            c = lerp(c, box, 0.3)
        if abs(x) < 0.13:
            c = GOLD
        if lid and abs(y - 0.48) < 0.06:
            c = lerp(c, GOLD, 0.6)

        if abs(x) > 0.66 and body:
            c = lerp(c, box_dark, 0.6)
        return rgba(c)

    return shade


def sparkle():
    def shade(p):
        x, y = p
        arms = 1.0 / (1.0 + abs(x * y) * 42.0)
        falloff = smoothstep(1.0, 0.0, min(1.0, math.hypot(x, y)))
        a = arms * falloff
        if a < 0.02:
            return clear()
        return (1.0, 1.0, 1.0, min(1.0, a))

    return shade


def seg_dist(p, a, b):
    abx, aby = b[0] - a[0], b[1] - a[1]
    ln = abx * abx + aby * aby
    t = 0.0
    if ln > 1e-4:
        t = max(0.0, min(1.0, ((p[0] - a[0]) * abx + (p[1] - a[1]) * aby) / ln))
    return math.hypot(p[0] - (a[0] + abx * t), p[1] - (a[1] + aby * t))


def check_mark():
    def shade(p):
        d = min(seg_dist(p, (-0.55, 0.05), (-0.12, -0.45)),
                seg_dist(p, (-0.12, -0.45), (0.62, 0.52)))
        if d > 0.17:
            return clear()
        return (1.0, 1.0, 1.0, smoothstep(1.0, 0.0, inv_lerp(0.11, 0.17, d)))

    return shade


def streak_flame():
    def shade(p):
        x, y = p
        wobble = math.sin(y * 4.2) * 0.07
        xx = x + wobble
        ball = math.hypot(xx, y + 0.3) - 0.5
        half = 0.0 + (0.5 - 0.0) * inv_lerp(0.92, -0.3, y)
        cone = max(abs(xx) - half, -(y + 0.3))
        shape = min(ball, cone)
        if shape > 0.0:
            return clear()
        depth = inv_lerp(0.0, -0.42, shape)
        c = lerp(CORAL, GOLD, depth)
        if depth > 0.62:
            c = lerp(c, FOAM, (depth - 0.62) * 1.6)
        return rgba(c, smoothstep(0.0, 1.0, inv_lerp(0.0, -0.05, shape)))

    return shade


def wheel_pointer():
    def shade(p):
        x, y = p
        half = 0.62 + (0.0 - 0.62) * inv_lerp(0.7, -0.8, y)
        if y > 0.7 or y < -0.8 or abs(x) > half:
            return clear()
        c = lerp(GOLD, CORAL, inv_lerp(0.7, -0.8, y) * 0.6)
        if abs(x) > half - 0.12:
            c = lerp(c, DEEP, 0.45)
        return rgba(c)

    return shade


# --- отрисовка ----------------------------------------------------------------
def render(shade, w, h, ss=3):
    img = Image.new("RGBA", (w, h))
    px = img.load()
    aspect_u = 1.0 if w >= h else w / float(h)
    aspect_v = h / float(w) if w >= h else 1.0

    for yy in range(h):
        for xx in range(w):
            r = g = b = a = 0.0
            for sy in range(ss):
                for sx in range(ss):
                    u = ((xx + (sx + 0.5) / ss) / w * 2 - 1) * aspect_u
                    v = ((yy + (sy + 0.5) / ss) / h * 2 - 1) * aspect_v
                    c = shade((u, -v))          # экранный Y вниз
                    r += c[0] * c[3]
                    g += c[1] * c[3]
                    b += c[2] * c[3]
                    a += c[3]
            n = ss * ss
            if a > 1e-4:
                px[xx, yy] = (int(r / a * 255), int(g / a * 255), int(b / a * 255), int(a / n * 255))
            else:
                px[xx, yy] = (0, 0, 0, 0)
    return img


def main():
    items = [
        ("chest closed", chest(False), 128, 112),
        ("chest open", chest(True), 128, 112),
        ("wheel", wheel_face(), 128, 128),
        ("pointer", wheel_pointer(), 48, 64),
        ("medal 1", medal(1), 128, 128),
        ("medal 2", medal(2), 128, 128),
        ("medal 3", medal(3), 128, 128),
        ("gift", gift_box(), 128, 128),
        ("sparkle", sparkle(), 128, 128),
        ("check", check_mark(), 128, 128),
        ("flame", streak_flame(), 128, 128),
    ]

    cell = 140
    cols = 6
    rows = (len(items) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * cell, rows * cell), (13, 36, 54, 255))

    for i, (name, shade, w, h) in enumerate(items):
        img = render(shade, w, h)
        cx = (i % cols) * cell + (cell - w) // 2
        cy = (i // cols) * cell + (cell - h) // 2
        sheet.alpha_composite(img, (cx, cy))
        print("%-14s %dx%d" % (name, w, h))

    sheet.save(OUT)
    print("\nсохранено: " + OUT)


if __name__ == "__main__":
    main()
