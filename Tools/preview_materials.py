# -*- coding: utf-8 -*-
"""Отрисовка материалов скинов вне Unity.

Повторяет математику SpriteFactoryMaterials.cs один в один, включая шум, —
нужно, чтобы смотреть на результат, а не угадывать его по формуле. Unity для
этого запускать не требуется, и правку материала видно сразу.

    python3 Tools/preview_materials.py
"""
import math
from PIL import Image

MASK = 0xFFFFFFFF


def i32(v):
    v &= MASK
    return v - 0x100000000 if v & 0x80000000 else v


def sar(v, n):
    """Арифметический сдвиг вправо, как у int в C#."""
    return v >> n if v >= 0 else ~((~v) >> n)


def hsh(x, y):
    h = i32(i32(x * 374761393) + i32(y * 668265263))
    h = i32((h ^ sar(h, 13)) * 1274126177)
    return ((h ^ sar(h, 16)) & 0x7FFFFFFF) / float(0x7FFFFFFF)


def vnoise(x, y):
    xi, yi = math.floor(x), math.floor(y)
    xf, yf = x - xi, y - yi
    u = xf * xf * (3 - 2 * xf)
    v = yf * yf * (3 - 2 * yf)
    a, b = hsh(xi, yi), hsh(xi + 1, yi)
    c, d = hsh(xi, yi + 1), hsh(xi + 1, yi + 1)
    ab = a + (b - a) * u
    cd = c + (d - c) * u
    return ab + (cd - ab) * v


def wisp(p, scale, sx=0.0, sy=0.0):
    n = (vnoise(p[0] * scale + sx, p[1] * scale + sy) * 0.55
         + vnoise(p[0] * scale * 2.3 + 31.7 + sx, p[1] * scale * 2.3 + 11.3 + sy) * 0.30
         + vnoise(p[0] * scale * 4.7 + 71.1 + sx, p[1] * scale * 4.7 + 53.9 + sy) * 0.15)
    return n * 2 - 1


def hx(h):
    return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def inv(a, b, v):
    return 0.0 if b == a else max(0.0, min(1.0, (v - a) / (b - a)))


def sstep(a, b, t):
    t = max(0.0, min(1.0, t))
    t = t * t * (3 - 2 * t)
    return a + (b - a) * t


def rep(v, m):
    return v - math.floor(v / m) * m


def dist(p, q):
    return math.hypot(p[0] - q[0], p[1] - q[1])


def seg(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    l2 = dx * dx + dy * dy
    t = 0 if l2 == 0 else max(0, min(1, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / l2))
    return math.hypot(p[0] - (a[0] + dx * t), p[1] - (a[1] + dy * t))


# ---------------------------------------------------------------- материалы

def coral(p):
    ang = math.atan2(p[1], p[0]); r = math.hypot(*p)
    branch = abs(math.sin(ang * 2.5 + r * 2.2))
    c = lerp(hx("B83F5E"), hx("FF9EB5"), branch)
    if wisp(p, 9) > 0.75:
        c = lerp(c, hx("FFD8E0"), 0.6)
    return c, 0.92


def pearl(p):
    ang = math.atan2(p[1], p[0]); r = math.hypot(*p)
    t = rep(ang / math.tau + r * 0.35, 1)
    a, b, c3 = hx("FFF2E0"), hx("E8D8FF"), hx("D8F0E8")
    c = lerp(a, b, t / 0.33) if t < 0.33 else (
        lerp(b, c3, (t - 0.33) / 0.33) if t < 0.66 else lerp(c3, a, (t - 0.66) / 0.34))
    return lerp((1, 1, 1), c, min(1, r * 1.4)), 0.95


def sand(p):
    layer = rep(p[1] * 3.4 + math.sin(p[0] * 2) * 0.2, 1)
    c = lerp(hx("C9A86B"), hx("F0DFB0"), layer)
    if wisp(p, 17, 11) > 0.6:
        c = lerp(c, hx("8F7340"), 0.45)
    return c, 0.93


def tide(p):
    surface = math.sin(p[0] * 3.2) * 0.08 + 0.12
    if p[1] > surface:
        return hx("BFEFFF"), 0.55
    depth = inv(surface, -0.95, p[1])
    c = lerp(hx("6FC8E8"), hx("2F7FB8"), depth)
    if p[1] > surface - 0.08:
        c = lerp(c, (1, 1, 1), 0.4)
    return c, 0.92


def neon(p):
    r = math.hypot(*p); c = hx("1A0A28")
    for rr, col in ((0.28, hx("FF5BE8")), (0.48, hx("5BF0FF")), (0.68, hx("FFE05B"))):
        d = abs(r - rr)
        if d < 0.045:
            c = lerp(c, col, 1 - d / 0.045)
        elif d < 0.1:
            c = lerp(c, col, (1 - d / 0.1) * 0.3)
    return c, 0.95


def resin(p):
    c = lerp(hx("8F4F10"), hx("FFC878"), inv(-0.9, 0.7, p[1] + p[0] * 0.3))
    if math.sin(p[0] * 6 + p[1] * 2) > 0.55:
        c = lerp(c, hx("FFE8B0"), 0.35)
    if dist(p, (-0.2, 0.22)) < 0.07 or dist(p, (0.3, -0.26)) < 0.05:
        c = hx("4A2A08")
    return c, 0.94


def plasma(p):
    c = hx("1A1030")
    for i in range(3):
        phase = i * 2.1
        field = math.sin(p[0] * 5 + phase) + math.sin(p[1] * 6.5 - phase) * 0.8
        d = abs(field)
        if d < 0.08:
            c = lerp(c, (1, 1, 1), 1 - d / 0.08)
        elif d < 0.3:
            c = lerp(c, hx("9ADFFF"), (1 - d / 0.3) * 0.45)
    return c, 0.95


def pixel(p):
    cell = 0.22
    gx = math.floor((p[0] + 1) / cell); gy = math.floor((p[1] + 1) / cell)
    h = rep(math.sin(gx * 12.9898 + gy * 78.233) * 43758.5453, 1)
    c = hx("1F7F4F") if h < 0.45 else hx("5BFF9A") if h < 0.75 else hx("FF5BD8") if h < 0.92 else hx("0F2A1F")
    fx = rep((p[0] + 1) / cell, 1); fy = rep((p[1] + 1) / cell, 1)
    if fx < 0.08 or fy < 0.08:
        c = lerp(c, (0, 0, 0), 0.35)
    return c, 0.96


def aurora(p):
    band = p[0] + math.sin(p[1] * 2.6) * 0.35
    t = rep(band * 1.2 + 0.5, 1)
    a, b, c3 = hx("8FFFD8"), hx("FF8FE8"), hx("8FB8FF")
    c = lerp(a, b, t / 0.33) if t < 0.33 else (
        lerp(b, c3, (t - 0.33) / 0.33) if t < 0.66 else lerp(c3, a, (t - 0.66) / 0.34))
    fade = inv(-0.9, 0.6, p[1])
    return lerp(hx("0A1A2A"), c, 0.25 + fade * 0.75), 0.94


def abyss(p):
    r = math.hypot(*p)
    c = lerp(hx("000006"), hx("2A2440"), min(1, r * 1.3))
    if abs(r - 0.52) < 0.04:
        c = lerp(c, hx("6F5BD0"), 0.8)
    if rep(math.sin(p[0] * 61.3 + p[1] * 37.9) * 43758.5453, 1) > 0.988:
        c = lerp(c, hx("C8B8FF"), 0.9)
    return c, 0.97


def verdigris(p):
    n = wisp(p, 3.4, 53) * 0.5 + 0.5
    c = lerp(hx("1F6B4F"), hx("8CF0B4"), n)
    if n < 0.22:
        c = lerp(c, hx("B87333"), (0.22 - n) * 3.5)
    return c, 0.94


def cinnabar(p):
    c = lerp(hx("6B1208"), hx("FF6B4A"), inv(-0.9, 0.9, p[1] + p[0] * 0.4))
    vein = abs(math.sin(p[0] * 7 + p[1] * 4))
    if vein > 0.86:
        c = lerp(c, hx("FFC8A8"), (vein - 0.86) * 7)
    return c, 0.95


def obsidian(p):
    c = hx("14171C")
    d = dist(p, (-0.25, 0.3))
    shell = rep(d * 5.5, 1)
    c = lerp(c, hx("3A4150"), shell * 0.55)
    if shell > 0.92:
        c = lerp(c, hx("8FA8CF"), 0.5)
    return c, 0.96


def brass(p):
    sheen = abs(math.sin(p[1] * 2.2 - p[0] * 0.6))
    c = lerp(hx("7A5516"), hx("FFD98A"), sheen)
    if rep(p[1] * 23 + p[0] * 3, 1) > 0.93:
        c = lerp(c, hx("FFF4D0"), 0.4)
    return c, 0.96


def starlight(p):
    r = math.hypot(*p); ang = math.atan2(p[1], p[0])
    rays = abs(math.sin(ang * 5))
    c = lerp(hx("FFC83D"), hx("FFF6C0"), rays * (1 - r * 0.6))
    c = lerp(c, (1, 1, 1), max(0, min(1, 1 - r * 3.2)))
    if rep(math.sin(p[0] * 73.1 + p[1] * 29.7) * 43758.5453, 1) > 0.99:
        c = (1, 1, 1)
    return c, 0.96


MATERIALS = [
    ("coral", coral), ("pearl", pearl), ("sand", sand), ("tide", tide), ("neon", neon),
    ("resin", resin), ("plasma", plasma), ("pixel", pixel), ("aurora", aurora),
    ("abyss", abyss), ("verdigris", verdigris), ("cinnabar", cinnabar),
    ("obsidian", obsidian), ("brass", brass), ("starlight", starlight),
]


def render(path, items, size=110, pad=7, per_row=8):
    rows = (len(items) + per_row - 1) // per_row
    sheet = Image.new("RGB", (per_row * (size + pad) + pad, rows * (size + pad) + pad), (10, 18, 28))
    for k, (name, fn) in enumerate(items):
        img = Image.new("RGB", (size, size), (10, 18, 28))
        px = img.load()
        for yy in range(size):
            for xx in range(size):
                x = (xx / (size - 1)) * 2 - 1
                y = 1 - (yy / (size - 1)) * 2
                d = math.hypot(x, y)
                if d > 0.92:
                    continue
                c, a = fn((x, y))
                a *= sstep(1, 0, inv(0.74, 0.92, d))
                bg = (10 / 255, 18 / 255, 28 / 255)
                px[xx, yy] = tuple(int(max(0, min(1, bg[i] * (1 - a) + c[i] * a)) * 255) for i in range(3))
        col = k % per_row
        row = k // per_row
        sheet.paste(img, (pad + col * (size + pad), pad + row * (size + pad)))
    sheet.save(path)
    return len(items)


if __name__ == "__main__":
    n = render("Build/preview-materials-all.png", MATERIALS)
    print("отрисовано материалов:", n)
