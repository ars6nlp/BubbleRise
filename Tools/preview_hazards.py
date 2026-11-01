# -*- coding: utf-8 -*-
"""
Препятствия вне Unity.

Повторяет SpriteFactoryHazardArt и SpriteFactoryHazards: тот же источник света,
тот же шум, те же формулы. Смысл тот же, что у предпросмотра силуэтов: формулу
объёма и фактуры на глаз не оценить, а на экране сразу видно, вышел камень или
клякса.

    python3 Tools/preview_hazards.py
"""
import colorsys
import math
import os

from PIL import Image

OUT = os.path.join(os.path.dirname(__file__), "preview_hazards.png")

MASK = 0xFFFFFFFF


def i32(v):
    v &= MASK
    return v - 0x100000000 if v & 0x80000000 else v


def sar(v, n):
    return v >> n if v >= 0 else ~((~v) >> n)


def hash2(x, y):
    """Повтор SpriteFactoryMaterials.Hash — целочисленно, как в C#."""
    h = i32(x * 374761393 + y * 668265263)
    h = i32((h ^ sar(h, 13)) * 1274126177)
    return ((h ^ sar(h, 16)) & 0x7FFFFFFF) / float(0x7FFFFFFF)


def vnoise(x, y):
    xi, yi = math.floor(x), math.floor(y)
    fx, fy = x - xi, y - yi
    ux = fx * fx * (3 - 2 * fx)
    uy = fy * fy * (3 - 2 * fy)
    a = hash2(xi, yi)
    b = hash2(xi + 1, yi)
    c = hash2(xi, yi + 1)
    d = hash2(xi + 1, yi + 1)
    return (a * (1 - ux) + b * ux) * (1 - uy) + (c * (1 - ux) + d * ux) * uy


def wisp(p, scale, sx=0.0, sy=0.0):
    """Три октавы, −1…1 — как Wisp в C#."""
    x, y = p[0] * scale + sx, p[1] * scale + sy
    n = vnoise(x, y) * 0.5
    n += vnoise(x * 2.1 + 5.3, y * 2.1 - 1.7) * 0.3
    n += vnoise(x * 4.3 - 2.9, y * 4.3 + 3.1) * 0.2
    return n * 2 - 1


def hexc(h):
    return (int(h[0:2], 16) / 255.0, int(h[2:4], 16) / 255.0, int(h[4:6], 16) / 255.0)


def lerp(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def mul(c, k):
    return tuple(min(1.0, c[i] * k) for i in range(3))


def inv(a, b, v):
    if a == b:
        return 0.0
    return max(0.0, min(1.0, (v - a) / (b - a)))


def smooth(a, b, t):
    t = max(0.0, min(1.0, t))
    return a + (b - a) * (t * t * (3 - 2 * t))


def shift(c, dh, ds, dv):
    h, s, v = colorsys.rgb_to_hsv(*c)
    return colorsys.hsv_to_rgb((h + dh) % 1.0, max(0, min(1, s + ds)), max(0, min(1, v + dv)))


FOAM = hexc("EAF6F2")
HAZE = hexc("4E8FA0")
DEEP = hexc("081522")
ROCK = hexc("7D8B93")
URCHIN = hexc("3A2540")
CORAL_BODY = hexc("B5573F")
CORAL_TIP = hexc("D97B5F")
GOLD = hexc("FFD166")

L = (-0.55, 0.84)
LN = math.hypot(*L)
L = (L[0] / LN, L[1] / LN)


def volume(p, edge, base, relief=1.0):
    h = math.sqrt(max(0.0, 1 - edge * edge))
    m = math.hypot(*p)
    n = (p[0] / m, p[1] / m) if m > 0.01 else (0.0, 1.0)
    lam = (n[0] * L[0] + n[1] * L[1]) * (1 - h) + h * 0.55
    lam = max(0.0, min(1.0, 0.5 + lam * 0.5))
    lit = lerp(mul(base, 1 - 0.42 * relief), lerp(base, FOAM, 0.3 * relief), lam)
    bounce = max(0.0, -(n[0] * L[0] + n[1] * L[1])) * edge * edge
    lit = lerp(lit, lerp(base, HAZE, 0.55), bounce * 0.3 * relief)
    if edge > 0.88:
        lit = lerp(lit, mul(base, 0.45), inv(0.88, 1.0, edge) * 0.75)
    return lit


def underwater(c, p, strength=0.12):
    return lerp(c, HAZE, inv(0.6, -1.0, p[1]) * strength)


def spec(c, p, at, radius, power=0.22):
    d = math.hypot(p[0] - at[0], p[1] - at[1])
    if d > radius:
        return c
    return lerp(c, lerp(c, FOAM, 0.85), smooth(1.0, 0.0, d / radius) * power)


def grain(c, p, scale, amount, seed=0):
    n = wisp(p, scale, seed * 3.1, seed * 7.7)
    return lerp(c, mul(c, 1.16) if n > 0 else mul(c, 0.84), abs(n) * amount)


def crack(c, p, scale, width, seed=0):
    n = wisp(p, scale, seed * 11.3, seed * 5.9)
    d = abs(n)
    if d > width:
        return c
    return lerp(c, mul(c, 0.42), smooth(1.0, 0.0, d / width))


def crust(c, p, col, scale, amount, seed=0):
    n = wisp(p, scale, 17 + seed * 2.3, 29 + seed * 4.1)
    top = inv(-0.4, 0.9, p[1])
    k = max(0.0, min(1.0, (n - 0.18) * 2.4)) * top * amount
    return lerp(c, col, k)


def simple_volume(p, edge, base):
    m = math.hypot(*p)
    if m < 0.01:
        lit = 0.75
    else:
        n = (p[0] / m, p[1] / m)
        lit = max(0.0, min(1.0, 0.5 + (n[0] * L[0] + n[1] * L[1]) * 0.5))
    dark = mul(base, 0.62)
    light = lerp(base, FOAM, 0.22)
    c = lerp(dark, light, smooth(0.0, 1.0, lit))
    if edge > 0.9:
        c = lerp(c, mul(base, 0.4), inv(0.9, 1.0, edge) * 0.8)
    return c


def chip(c, p, through, normal, lighten):
    side = (p[0] - through[0]) * normal[0] + (p[1] - through[1]) * normal[1]
    if side < 0:
        return c
    if side < 0.05:
        return lerp(c, mul(c, 0.55), smooth(1.0, 0.0, side / 0.05))
    return lerp(c, lerp(c, FOAM, 0.3), lighten)


def outline(points, variant, rough=0.2):
    poly = []
    for i in range(points):
        a = i / points * math.pi * 2
        r = (0.78 + rough * math.sin(i * 2.1 + variant * 1.7)
             + rough * 0.4 * math.sin(i * 3.7 + variant * 2.9))
        poly.append((math.cos(a) * r, math.sin(a) * r))
    return poly


def in_poly(p, poly):
    inside = False
    j = len(poly) - 1
    for i in range(len(poly)):
        a, b = poly[i], poly[j]
        if (a[1] > p[1]) != (b[1] > p[1]):
            x = (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]
            if p[0] < x:
                inside = not inside
        j = i
    return inside


def seg_d(p, a, b):
    abx, aby = b[0] - a[0], b[1] - a[1]
    ln = abx * abx + aby * aby
    t = 0.0 if ln < 1e-4 else max(0.0, min(1.0, ((p[0] - a[0]) * abx + (p[1] - a[1]) * aby) / ln))
    return math.hypot(p[0] - (a[0] + abx * t), p[1] - (a[1] + aby * t))


def poly_edge(p, poly):
    best = 1e9
    for i in range(len(poly)):
        best = min(best, seg_d(p, poly[i], poly[(i + 1) % len(poly)]))
    reach = sum(math.hypot(*q) for q in poly) / len(poly)
    return max(0.0, min(1.0, 1 - best / max(0.05, reach)))


# --- сами препятствия ---------------------------------------------------------
def rock(v):
    poly = outline(6 + v % 2, v * 5 + 1, 0.16)
    body = shift(ROCK, 0, -0.03 + v * 0.03, -0.05 + v * 0.05)
    a = 0.6 + v * 1.9
    n = (math.cos(a), math.sin(a))
    through = (n[0] * (0.28 + v * 0.06), n[1] * (0.28 + v * 0.06))

    def sh(p):
        if not in_poly(p, poly):
            return None
        e = poly_edge(p, poly)
        c = simple_volume(p, e, body)
        c = chip(c, p, through, n, 0.26)
        c = spec(c, p, (-0.3, 0.34), 0.42, 0.22)
        return underwater(c, p, 0.1)
    return sh


def urchin(v):
    spines = 6 + v
    spine_len = 0.46 - v * 0.03
    body = shift(URCHIN, 0, -0.05 + v * 0.05, v * 0.05)
    tip = shift(hexc("E8A0C0"), 0, -0.05, 0.04)

    def sh(p):
        d = math.hypot(*p)
        a = math.atan2(p[1], p[0])
        wave = math.cos(a * spines + v * 0.7)
        reach = 0.52 + spine_len * (max(0.0, wave) ** 1.6)
        if d > reach:
            return None
        if d > 0.52:
            t = inv(0.52, reach, d)
            return underwater(lerp(mul(body, 1.3), tip, smooth(0.0, 1.0, t)), p, 0.12)
        c = simple_volume(p, d / 0.52, body)
        c = spec(c, p, (-0.18, 0.2), 0.3, 0.26)
        return underwater(c, p, 0.1)
    return sh


def barnacle(v):
    poly = outline(6, v * 7 + 23, 0.1)
    body = shift(hexc("C9B89A"), 0, -0.05, -0.03 + v * 0.04)
    inner = hexc("8F7F66")
    cones = 2 + v % 2

    def sh(p):
        if not in_poly(p, poly):
            return None
        e = poly_edge(p, poly)
        c = simple_volume(p, e, body)
        for i in range(cones):
            a = i / cones * math.pi * 2 + v * 1.1
            at = (math.cos(a) * 0.26, math.sin(a) * 0.26)
            dd = math.hypot(p[0] - at[0], p[1] - at[1])
            if dd > 0.34:
                continue
            t = dd / 0.34
            shell = lerp(lerp(body, FOAM, 0.26), mul(body, 0.7), t)
            if t < 0.28:
                shell = lerp(shell, inner, 1 - t / 0.28)
            c = lerp(c, shell, 0.9)
        c = spec(c, p, (-0.3, 0.3), 0.34, 0.2)
        return underwater(c, p, 0.1)
    return sh


def cinder(v):
    poly = outline(6, v * 11 + 31, 0.13)
    cc = shift(hexc("3A2B26"), 0, 0, v * 0.03)
    glow = shift(hexc("FF8A4A"), 0, 0, -0.04)
    a = 0.4 + v * 1.1
    d0 = (math.cos(a), math.sin(a))
    nr = (-d0[1], d0[0])

    def sh(p):
        if not in_poly(p, poly):
            return None
        e = poly_edge(p, poly)
        c = simple_volume(p, e, cc)
        along = p[0] * d0[0] + p[1] * d0[1]
        across = abs(p[0] * nr[0] + p[1] * nr[1])
        width = 0.13 + 0.05 * math.sin(along * 3.1)
        if across < width:
            k = smooth(1.0, 0.0, across / width)
            c = lerp(c, glow, k * 0.85)
            if k > 0.72:
                c = lerp(c, lerp(glow, GOLD, 0.35), (k - 0.72) * 2)
        return underwater(c, p, 0.06)
    return sh


def coral(v):
    poly = outline(9, v + 11, 0.24)
    body = shift(CORAL_BODY, v * 0.005, 0.06 - v * 0.03, -0.04 + v * 0.045)
    tipc = shift(CORAL_TIP, v * 0.005, 0, 0.05)

    def sh(p):
        if not in_poly(p, poly):
            return None
        e = poly_edge(p, poly)
        c = volume(p, e, body, 0.9)
        seg = wisp(p, 5.5 + v, 7 * v, 3 * v)
        c = lerp(c, tipc, max(0.0, min(1.0, seg)) * 0.45)
        c = lerp(c, mul(body, 0.72), max(0.0, min(1.0, -seg)) * 0.4)
        c = grain(c, p, 14, 0.35, v + 2)
        c = crust(c, p, hexc("A8C8A0"), 6, 0.22, v)
        c = spec(c, p, (-0.26, 0.3), 0.34, 0.2)
        return underwater(c, p)
    return sh


def mine(v):
    spikes = 5 + v % 3
    shell = shift(hexc("3E4A55"), v * 0.01, 0.02, v * 0.02)
    warn = shift(hexc("E05A4A"), v * 0.012, 0, -0.04)

    def sh(p):
        d = math.hypot(*p)
        a = math.atan2(p[1], p[0])
        spike = math.cos(a * spikes + v * 0.6)
        reach = 0.7 + 0.3 * (max(0.0, spike) ** 1.8)
        if d > reach:
            return None
        if d > 0.7:
            t = inv(0.7, reach, d)
            return underwater(lerp(mul(shell, 1.15), warn, t), p, 0.1)
        e = d / 0.7
        c = volume(p, e, shell, 1.2)
        if abs(p[1]) < 0.1:
            c = lerp(c, warn, 0.5)
        for i in range(6):
            aa = i / 6 * math.pi * 2
            at = (math.cos(aa) * 0.46, math.sin(aa) * 0.46)
            dd = math.hypot(p[0] - at[0], p[1] - at[1])
            if dd < 0.08:
                c = lerp(c, lerp(shell, FOAM, 0.4), smooth(1.0, 0.2, dd / 0.08))
        c = spec(c, p, (-0.26, 0.3), 0.3, 0.26)
        return underwater(c, p, 0.1)
    return sh


def vortex(v):
    sw = shift(hexc("6E7BE8"), v * 0.02, 0, v * 0.03)

    def sh(p):
        d = math.hypot(*p)
        if d > 1:
            return None
        a = math.atan2(p[1], p[0])
        arm = math.sin((a + d * (6 + v)) * (2 + v % 2))
        c = lerp(mul(sw, 0.45), sw, max(0.0, min(1.0, arm * 0.5 + 0.5)))
        c = lerp(DEEP, c, smooth(0.0, 1.0, inv(0.1, 0.5, d)))
        if d > 0.84:
            c = lerp(c, lerp(sw, FOAM, 0.45), inv(0.84, 0.97, d) * 0.6)
        return grain(c, p, 8, 0.2, v + 29)
    return sh


def jaw(v):
    shell = shift(hexc("D8C7A8"), v * 0.008, -0.02 + v * 0.03, -0.05 + v * 0.035)
    pearl = hexc("F0E8DF")
    hinge = (-0.82, 0.0)
    opn = 0.16 + v * 0.03
    span = 1.0

    def sh(p):
        fx, fy = p[0] - hinge[0], p[1] - hinge[1]
        d = math.hypot(fx, fy)
        if d > 1.72:
            return None
        a = math.atan2(fy, fx)
        absA = abs(a)
        if absA < opn or absA > opn + span:
            return None
        t = inv(opn, opn + span, absA)
        reach = 1.72 * (0.55 + 0.45 * math.sin(t * math.pi))
        if d > reach:
            return None
        e = max(d / reach, 1 - math.sin(max(0.0, min(1.0, t)) * math.pi))
        c = volume(p, max(0.0, min(1.0, e)), shell, 1.05)
        rib = abs(math.sin((absA - opn) / span * math.pi * (6 + v)))
        c = lerp(c, mul(c, 0.8), max(0.0, min(1.0, rib - 0.5)) * 1.8)
        ring = (d * (4.5 + v * 0.5)) % 1.0
        c = lerp(c, mul(c, 0.9), max(0.0, min(1.0, (0.12 - ring) * 4)))
        c = lerp(c, pearl, inv(opn + 0.3, opn, absA) * 0.55)
        if d < 0.4:
            c = lerp(c, mul(shell, 0.75), inv(0.4, 0.1, d) * 0.5)
        c = grain(c, p, 11, 0.25, v + 37)
        c = crust(c, p, hexc("8FA87F"), 5, 0.22, v)
        c = spec(c, p, (-0.1, 0.42), 0.34, 0.22)
        return underwater(c, p)
    return sh


def render(shade, size=128, ss=2):
    img = Image.new("RGBA", (size, size))
    px = img.load()
    for yy in range(size):
        for xx in range(size):
            r = g = b = a = 0.0
            for sy in range(ss):
                for sx in range(ss):
                    u = (xx + (sx + 0.5) / ss) / size * 2 - 1
                    vv = (yy + (sy + 0.5) / ss) / size * 2 - 1
                    c = shade((u, -vv))
                    if c is None:
                        continue
                    r += c[0]
                    g += c[1]
                    b += c[2]
                    a += 1
            n = ss * ss
            if a > 0:
                px[xx, yy] = (int(min(1, r / a) * 255), int(min(1, g / a) * 255),
                              int(min(1, b / a) * 255), int(a / n * 255))
            else:
                px[xx, yy] = (0, 0, 0, 0)
    return img


def main():
    rows = [("камень", rock), ("ёж", urchin), ("нарост", barnacle),
            ("уголёк", cinder), ("коралл", coral), ("мина", mine)]
    cell = 136
    cols = 3
    sheet = Image.new("RGBA", (cols * cell + 120, len(rows) * cell), (8, 21, 34, 255))

    for r, (name, fn) in enumerate(rows):
        for v in range(cols):
            img = render(fn(v))
            sheet.alpha_composite(img, (120 + v * cell + 4, r * cell + 4))
        print("%-10s 4 вариации" % name)

    sheet.save(OUT)
    print("\nсохранено: " + OUT)


if __name__ == "__main__":
    main()
