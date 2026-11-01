# -*- coding: utf-8 -*-
"""Отрисовка всех шестнадцати оболочек столкновения.

    python3 Tools/preview_envelopes.py

Unity здесь запустить нельзя, а вопрос «правда ли эффект крупный и разный»
решается только глазами. Поэтому траектории из EnvelopeShape.cs перенесены сюда
один в один, а числа читаются из EnvelopeTable.cs — подменить их и нарисовать
красивое, но не то, что в игре, не получится.

Всё в диаметрах стража (D). Серый круг в центре — сам страж, его диаметр ровно
единица. Коричневый круг справа — препятствие, по нему растекается связь.

Пишет Tools/out/envelopes_t{...}.png: сетка 4x4, по кадру на оболочку,
три момента времени подряд.
"""
import io
import math
import os
import re

from PIL import Image, ImageDraw, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Tools", "out")

# Потолки по слоям — из CollisionEnvelope.cs
CORE_A, SHAPE_A, SPARK_A, SHADOW_A, LINK_A = 0.95, 0.8, 0.62, 0.6, 0.85
ENVELOPE_ALPHA = 1.0          # ступень «Обычно»
PARTICLES = 1.0               # она же
LINK_TRAVEL_BUDGET = 32
LINK_SPREAD = 8

TILE = 300
UNIT = 40.0                   # точек на диаметр стража
COLS = 4

TARGET = (1.55, 0.0)          # препятствие, в D
TARGET_R = 0.42               # его радиус, в D


# ----------------------------------------------------------------- Mathf

def repeat(t, length):
    return t - math.floor(t / length) * length


def lerp(a, b, t):
    return a + (b - a) * max(0.0, min(1.0, t))


def clamp01(v):
    return max(0.0, min(1.0, v))


def lerp_color(a, b, t):
    t = clamp01(t)
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


def hash01(v):
    v &= 0xFFFFFFFF
    h = (v * 374761393) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return ((h ^ (h >> 16)) & 0x7FFFFFFF) / float(0x7FFFFFFF)


def smoothstep(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3.0 - 2.0 * x)


def step_noise(salt, step, stride):
    # Как EnvelopeShape.StepNoise: между формами — SmoothStep, а не скачок.
    i = int(math.floor(step))
    f = step - i
    a = hash01(salt + i * stride)
    b = hash01(salt + (i + 1) * stride)
    return a + (b - a) * smoothstep(f)


def fade(life):
    return min(1.0, life * 6.0) * (1.0 - life * 0.8)


def breath(age):
    return 0.82 + 0.18 * math.sin(age * 3.59)


# ------------------------------------------------------------- таблица

ROW = re.compile(
    r'D\(EnvelopeKind\.(\w+),\s*LinkKind\.(\w+),\s*'
    r'"([0-9A-Fa-f]{6})",\s*"([0-9A-Fa-f]{6})",\s*"([0-9A-Fa-f]{6})",\s*"([0-9A-Fa-f]{6})",\s*'
    r'([\d.]+)f,\s*(\d+),\s*([\d.]+)f,\s*([\d.]+)f,\s*(\d+),\s*([\d.]+)f,\s*(true|false)\)')


def hexcolor(h):
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16))


def table():
    src = io.open(os.path.join(ROOT, "Assets", "Scripts", "EnvelopeTable.cs"),
                  encoding="utf-8").read()
    rows = []
    for m in ROW.finditer(src):
        rows.append(dict(
            kind=m.group(1), link=m.group(2),
            hot=hexcolor(m.group(3)), cool=hexcolor(m.group(4)),
            accent=hexcolor(m.group(5)), edge=hexcolor(m.group(6)),
            duration=float(m.group(7)), count=int(m.group(8)),
            radius=float(m.group(9)), speed=float(m.group(10)),
            branches=int(m.group(11)), linkw=float(m.group(12)),
            rich=m.group(13) == "true"))
    assert len(rows) == 16, "ожидалось 16 оболочек, нашлось %d" % len(rows)
    return rows


def layers(d):
    n = max(24, min(150, int(round(d["count"] * PARTICLES))))
    return max(8, int(round(n * 0.5))), max(4, int(round(n * 0.1)))


def rings(d, age):
    """Тень и ядро: не россыпь точек, а по одному спрайту."""
    br = breath(age)
    core_r = 0.72 * br + 0.05
    return dict(shadow_scale=0.8 / 0.36 * 1.12, shadow_alpha=0.55,
                core_scale=core_r / 0.36, core_alpha=0.92 * br,
                accent_alpha=0.85 * br)


def link_steps(d):
    return max(3, min(8, LINK_TRAVEL_BUDGET // strands_of(d)))


def strands_of(d):
    return max(1, min(8, d["branches"]))


# ------------------------------------------------- общие слои

def sample_spark(d, index, count, age, seed):
    life = repeat(index / float(max(1, count)) + age * 0.55, 1.0)
    a = seed * 6.2832 + life * 1.4
    rr = lerp(0.85, d["radius"] * 1.08, life)
    twinkle = 0.75 + 0.25 * math.sin(age * 3.0 + seed * 9.0)
    return (math.cos(a) * rr, math.sin(a) * rr, 0.2 + 0.12 * seed,
            d["accent"] if index % 3 == 0 else d["cool"],
            fade(life) * twinkle)


# ------------------------------------------- внешняя форма, 16 видов

def sample_shape(d, index, count, age, seed, arc_step):
    count = max(1, count)
    slot = index / float(count)
    t = age * d["speed"]
    angle = (slot + t) * math.pi * 2.0
    r = d["radius"]
    kind = d["kind"]
    hot, cool, accent = d["hot"], d["cool"], d["accent"]

    strands = strands_of(d)
    strand = index % strands
    per = max(1, int(math.ceil(count / float(strands))))
    along = clamp01((index // strands) / float(per))

    if kind == "Flame":
        rise = repeat(along + age * 0.75 + strand * 0.13, 1.0)
        lean = math.sin(strand * 2.3 + age * 1.1) * 0.55
        spread = (strand / float(strands) - 0.5) * 1.7
        ember = index % 5 == 0
        return ((spread + lean * rise) * (1.0 - rise * 0.35),
                -0.45 + rise * (r * 0.85 + 1.0),
                0.16 if ember else lerp(0.56, 0.18, rise),
                accent if ember else lerp_color(hot, cool, rise * 0.85),
                0.9 if ember else fade(rise))

    if kind == "Spiral":
        a = along * 5.0 + strand / float(strands) * math.pi * 2.0 + age * 1.2
        rr = lerp(0.75, r, along)
        return (math.cos(a) * rr, math.sin(a) * rr, lerp(0.42, 0.18, along),
                accent if index % 5 == 0 else lerp_color(hot, cool, along),
                0.95 - along * 0.25)

    if kind == "Ribbon":
        a = (along * 0.42 + t) * math.pi * 2.0 + strand * 1.9
        wave = math.sin(along * math.pi * 2.0 + age * 1.6 + strand)
        rr = r * (0.78 + 0.22 * wave)
        col = (hot if strand == 0 else cool if strand == 1
               else accent if strand == 2 else lerp_color(hot, accent, 0.5))
        return (math.cos(a) * rr, math.sin(a) * rr, 0.44 + 0.12 * wave, col,
                0.62 + 0.28 * abs(wave))

    if kind == "Vortex":
        sink = repeat(slot + age * 0.5, 1.0)
        a = seed * 6.2832 + angle - sink * 4.0
        rr = lerp(r, 0.55, sink)
        return (math.cos(a) * rr, math.sin(a) * rr * 0.55 - sink * r * 0.85,
                lerp(0.44, 0.16, sink),
                accent if index % 6 == 0 else lerp_color(hot, cool, sink),
                fade(sink) * 0.95)

    if kind == "Coils":
        phase = repeat(age * 0.7 + strand / float(strands), 1.0)
        a = (along + t) * math.pi * 2.0
        rr = lerp(0.8, r, phase)
        return (math.cos(a) * rr, math.sin(a) * rr, lerp(0.42, 0.2, phase),
                accent if index % 7 == 0 else lerp_color(hot, cool, phase),
                fade(phase) * 0.95)

    if kind == "Starfall":
        out01 = repeat(slot + age * 0.4, 1.0)
        a = seed * 6.2832 + angle * 0.6
        rr = lerp(0.75, r, out01)
        twinkle = 0.86 + 0.14 * math.sin(age * 3.0 + seed * 9.0)
        return (math.cos(a) * rr,
                math.sin(a) * rr - out01 * out01 * r * 0.55,
                0.3 + 0.24 * seed,
                accent if index % 3 == 0 else hot,
                fade(out01) * twinkle)

    if kind == "Arc":
        tilt = (step_noise(strand * 31, arc_step, 7) - 0.5) * 0.5
        kink = (step_noise(strand * 131 + int(along * 5.0), arc_step, 17) - 0.5) * 0.3
        a = strand / float(strands) * math.pi * 2.0 + arc_step * 0.21 + tilt + kink * along
        rr = lerp(0.6, r, along)
        fork = kink * 2.6 * along if index % 3 == 2 else 0.0
        return (math.cos(a + fork) * rr, math.sin(a + fork) * rr,
                lerp(0.34, 0.22, along),
                accent if along > 0.8 else lerp_color(hot, cool, along),
                0.92 - along * 0.15)

    if kind == "Serpent":
        a = (along * 0.5 + t) * math.pi * 2.0 + strand * 1.3
        wave = math.sin(along * 9.0 + age * 3.0) * 0.42
        drop = index % 6 == 0
        x, y = math.cos(a) * (r * 0.86 + wave), math.sin(a) * (r * 0.86 + wave)
        if drop:
            y -= 0.5
        return (x, y, 0.2 if drop else lerp(0.46, 0.2, along),
                accent if drop else lerp_color(hot, cool, along),
                0.8 if drop else 0.92 - along * 0.3)

    if kind == "Tentacle":
        reach = 0.6 + 0.4 * math.sin(age * 1.5 + strand * 1.25)
        a = strand / float(strands) * math.pi * 2.0 + math.sin(along * 3.0 + age) * 0.35
        rr = lerp(0.7, r * reach, along)
        cup = (index // strands) % 3 == 0
        return (math.cos(a) * rr, math.sin(a) * rr,
                0.3 if cup else lerp(0.5, 0.18, along),
                accent if cup else lerp_color(hot, cool, along),
                0.9 - along * 0.25)

    if kind == "Blade":
        a = strand / float(strands) * math.pi * 2.0 + age * 0.9
        dx, dy = math.cos(a), math.sin(a)
        tx, ty = -dy, dx
        swing = repeat(age * 1.4 + strand * 0.2, 1.0)
        base = lerp(1.1, r, swing)
        off = (along - 0.5) * r * 0.72
        return (dx * base + tx * off, dy * base + ty * off,
                math.sin(along * math.pi) * 0.4 + 0.12,
                cool if along > 0.6 else hot,
                clamp01((0.4 + math.sin(along * math.pi) * 0.6)
                        * (0.45 + fade(swing) * 0.55)))

    if kind == "Pincer":
        beat = repeat(age * 1.3 + strand * 0.17, 1.0)
        punch = math.pow(max(0.0, math.sin(beat * math.pi)), 0.6)
        a = strand / float(strands) * math.pi * 2.0 + (along - 0.5) * 0.45
        rr = lerp(0.85, r, punch)
        return (math.cos(a) * rr, math.sin(a) * rr, 0.44 - punch * 0.12,
                accent if index % 5 == 0 else lerp_color(cool, hot, punch),
                0.35 + punch * 0.55)

    if kind == "Ink":
        spread = repeat(slot + age * 0.35, 1.0)
        a = seed * 6.2832
        lobe = 0.7 + 0.4 * math.sin(a * 3.0 + age)
        spark = index % 9 == 0
        rr = lerp(0.7, r * lobe, spread)
        return (math.cos(a) * rr, math.sin(a) * rr,
                0.18 if spark else lerp(0.34, 0.78, spread),
                accent if spark else lerp_color(hot, cool, spread),
                0.95 if spark else fade(spread) * 0.85)

    if kind == "Swarm":
        own = seed * 6.2832
        a = angle * 1.3 + own
        dart = math.pow(abs(math.sin(age * 2.2 + own)), 3.0)
        rr = r * (0.62 + 0.38 * math.sin(own + age * 0.7)) * (0.9 + dart * 0.22)
        return (math.cos(a) * rr, math.sin(a) * rr, 0.22 + 0.12 * seed,
                accent if index % 4 == 0 else lerp_color(hot, cool, seed),
                0.75 + 0.2 * dart)

    if kind == "Shell":
        a = (slot + t) * math.pi * 2.0
        bob = math.sin(age * 1.1 + slot * 6.2832) * 0.22
        rr = r * 0.9 + bob
        return (math.cos(a) * rr, math.sin(a) * rr,
                0.52 + 0.18 * math.sin(slot * 13.0),
                accent if index % 4 == 0 else lerp_color(hot, cool, slot),
                0.8)

    if kind == "Fin":
        sweep = repeat(age * 1.2 + strand / float(strands), 1.0)
        a = (strand / float(strands) + sweep * 0.45) * math.pi * 2.0 + along * 0.55
        rr = r * (0.82 + 0.18 * math.sin(along * math.pi))
        return (math.cos(a) * rr, math.sin(a) * rr,
                math.sin(along * math.pi) * 0.46 + 0.14,
                accent if along > 0.7 else lerp_color(cool, hot, along),
                fade(sweep) * 0.95)

    # Wing
    wave = repeat(age * 0.5 + strand / float(strands), 1.0)
    a = (along * 0.85 + strand * 0.08) * math.pi * 2.0
    rr = lerp(0.75, r, wave)
    return (math.cos(a) * rr, math.sin(a) * rr * 0.6, 0.46 - wave * 0.16,
            accent if strand % 3 == 0 else lerp_color(hot, cool, wave),
            fade(wave) * 0.9)


# -------------------------------------------- связь с препятствием

def travel(d, frm, to, strand, strands, k, arc_step):
    fx, fy = to[0] - frm[0], to[1] - frm[1]
    fl = math.hypot(fx, fy) or 1.0
    start = (frm[0] + fx / fl * 0.5, frm[1] + fy / fl * 0.5)

    dx, dy = to[0] - start[0], to[1] - start[1]
    ln = math.hypot(dx, dy) or 1.0
    nx, ny = -dy / ln, dx / ln
    sx, sy = start[0] + dx * k, start[1] + dy * k
    side = 0.0 if strands <= 1 else (strand / float(strands - 1) - 0.5) * 2.0
    side *= 0.4 + 0.6 * clamp01(ln)
    bow = math.sin(k * math.pi)
    kind = d["link"]
    frm = start

    if kind == "Bolt":
        j = step_noise(strand * 53 + int(k * 11.0), arc_step, 13) - 0.5
        o = (side * 0.55 + j * 0.5) * bow
    elif kind == "Reach":
        o = side * 0.7 * bow
    elif kind == "Jet":
        o = (side * 0.35 + math.sin(k * math.pi * 3.0) * 0.18) * bow
    elif kind == "Slash":
        u = 0.35 + k * 0.65
        return (frm[0] + dx * u + nx * side * 0.3 * bow,
                frm[1] + dy * u + ny * side * 0.3 * bow)
    elif kind == "Tongue":
        o = (side * 0.45 + math.sin(k * math.pi * 2.0) * 0.22) * bow * k
    else:
        o = side * 0.28 * bow

    return (sx + nx * o, sy + ny * o)


def link_count(d):
    return strands_of(d) * link_steps(d) + LINK_SPREAD


def link_at(d, index, frm, to, arc_step, target_r):
    """Точка связи по сквозному номеру: (x, y, размер, на_цели)."""
    strands = strands_of(d)
    steps = link_steps(d)
    travel_n = strands * steps

    if index < travel_n:
        strand = index // steps
        step = index % steps
        k = 1.0 if steps <= 1 else step / float(steps - 1)
        pos = travel(d, frm, to, strand, strands, k, arc_step)

        ln = max(0.001, math.hypot(to[0] - frm[0], to[1] - frm[1]) - 0.5)
        gap = 0.0 if steps <= 1 else ln / (steps - 1)
        return (pos[0], pos[1], max(link_width(d, k), gap * 1.15), False)

    spot = index - travel_n
    shimmer = d["link"] in ("Bolt", "Tongue")
    stride = 7 if shimmer else 0
    spin = step_noise(spot * 31, arc_step, stride) * 6.2832
    rr = target_r * math.sqrt(step_noise(spot * 31 + 17, arc_step, stride))
    return (to[0] + math.cos(spin) * rr, to[1] + math.sin(spin) * rr,
            max(d["linkw"], target_r * 0.75), True)


def link_width(d, k):
    return d["linkw"] * lerp(1.0, 0.6, k)


# -------------------------------------------------------------- отрисовка

_DOT_CACHE = {}


def soft_dot(size_px, color, alpha):
    """Тот же softdot: сплошная середина и мягкий край."""
    size_px = max(3, int(round(size_px)))
    if size_px not in _DOT_CACHE:
        mask = Image.new("L", (size_px, size_px), 0)
        px = mask.load()
        half = size_px / 2.0
        for y in range(size_px):
            for x in range(size_px):
                dx = (x + 0.5 - half) / half
                dy = (y + 0.5 - half) / half
                dd = math.hypot(dx, dy)
                if dd > 1.0:
                    continue
                k = clamp01((dd - 0.35) / 0.65)
                px[x, y] = int(255 * (1.0 - k * k * (3 - 2 * k)))
        _DOT_CACHE[size_px] = mask

    mask = _DOT_CACHE[size_px]
    img = Image.new("RGBA", (size_px, size_px),
                    (int(color[0]), int(color[1]), int(color[2]), 0))
    img.putalpha(mask.point(lambda v: int(v * clamp01(alpha))))
    return img


def blit(tile, cx, cy, x, y, size, color, alpha):
    if alpha <= 0.012:
        return
    dot = soft_dot(size * UNIT, color, alpha)
    tile.alpha_composite(dot, (int(cx + x * UNIT - dot.size[0] / 2.0),
                               int(cy - y * UNIT - dot.size[1] / 2.0)))


def ring_band(tile, cx, cy, scale, color, alpha, specks=0):
    """Кольцевой спрайт: полоса на 0.72 половины, гаснет к 0.52 и 0.92."""
    if alpha <= 0.012:
        return

    half = scale * 0.5 * UNIT
    size = int(half * 2)
    if size < 4:
        return

    img = Image.new("RGBA", (size, size), (int(color[0]), int(color[1]), int(color[2]), 0))
    px = img.load()
    for y in range(size):
        for x in range(size):
            dx = (x + 0.5 - half) / half
            dy = (y + 0.5 - half) / half
            dd = math.hypot(dx, dy)
            if dd > 1.0:
                continue
            a = clamp01(1.0 - abs(dd - 0.72) / 0.2)
            if a <= 0.0:
                continue
            a = a * a * (3 - 2 * a)
            if specks:
                ang = math.atan2(dy, dx) / (math.pi * 2.0) + 0.5
                slot = repeat(ang * specks, 1.0)
                cut = clamp01((0.45 - abs(slot - 0.5)) / 0.2)
                a *= cut * cut * (3 - 2 * cut)
            px[x, y] = (int(color[0]), int(color[1]), int(color[2]),
                        int(255 * a * clamp01(alpha)))

    tile.alpha_composite(img, (int(cx - half), int(cy - half)))


def draw_one(d, age, arc_step):
    tile = Image.new("RGBA", (TILE, TILE), (10, 18, 34, 255))
    dr = ImageDraw.Draw(tile)
    cx = cy = TILE / 2.0

    # препятствие
    tx, ty = cx + TARGET[0] * UNIT, cy - TARGET[1] * UNIT
    tr = TARGET_R * UNIT
    dr.ellipse([tx - tr, ty - tr, tx + tr, ty + tr], fill=(74, 64, 58, 255))

    # страж: диаметр ровно 1 D
    gr = 0.5 * UNIT
    dr.ellipse([cx - gr, cy - gr, cx + gr, cy + gr],
               fill=(52, 72, 96, 255), outline=(110, 140, 170, 255))

    n_shape, n_spark = layers(d)

    # Кольца: тень и ядро одним спрайтом каждое. Рисуем ровно ту полосу, что
    # задаёт EnvelopeRing: пик на 0.72 половины, гаснет к 0.52 и 0.92.
    rg = rings(d, age)
    ring_band(tile, cx, cy, rg["shadow_scale"], d["edge"],
              rg["shadow_alpha"] * SHADOW_A * ENVELOPE_ALPHA)
    ring_band(tile, cx, cy, rg["core_scale"], d["hot"],
              rg["core_alpha"] * CORE_A * ENVELOPE_ALPHA)
    ring_band(tile, cx, cy, rg["core_scale"] * 1.02, d["accent"],
              rg["accent_alpha"] * CORE_A * ENVELOPE_ALPHA, specks=9)

    for i in range(n_shape):
        seed = (i * 0.6180339) % 1.0
        x, y, s, c, a = sample_shape(d, i, n_shape, age, seed, arc_step)
        blit(tile, cx, cy, x, y, s, c, clamp01(a) * SHAPE_A * ENVELOPE_ALPHA)

    for i in range(n_spark):
        seed = (i * 0.6180339) % 1.0
        x, y, s, c, a = sample_spark(d, i, n_spark, age, seed)
        blit(tile, cx, cy, x, y, s, c, clamp01(a) * SPARK_A * ENVELOPE_ALPHA)

    for i in range(link_count(d)):
        lx, ly, w, on_target = link_at(d, i, (0.0, 0.0), TARGET, arc_step, TARGET_R)
        col = d["accent"] if on_target else d["hot"]
        blit(tile, cx, cy, lx, ly, w, col, LINK_A * ENVELOPE_ALPHA)

    return tile


def label(tile, text, sub):
    dr = ImageDraw.Draw(tile)
    try:
        font = ImageFont.truetype("arial.ttf", 15)
        small = ImageFont.truetype("arial.ttf", 12)
    except Exception:
        font = small = ImageFont.load_default()
    dr.text((8, 6), text, fill=(235, 245, 255, 255), font=font)
    dr.text((8, 26), sub, fill=(150, 180, 210, 255), font=small)
    return tile


def main():
    if not os.path.isdir(OUT):
        os.makedirs(OUT)

    rows = table()

    for age in (0.35, 0.9, 1.6):
        arc_step = age * 2.0         # ArcHz = 2, номер формы с дробной частью
        sheet = Image.new("RGBA", (TILE * COLS, TILE * 4), (6, 12, 24, 255))

        for i, d in enumerate(rows):
            tile = draw_one(d, age, arc_step)
            label(tile, "%d. %s" % (i + 1, d["kind"]),
                  "%s  R=%.1fD  %.1fs  n=%d  ветвей %d  %s"
                  % (d["link"], d["radius"], d["duration"], d["count"],
                     d["branches"], "boss" if d["rich"] else "leg"))
            sheet.alpha_composite(tile, ((i % COLS) * TILE, (i // COLS) * TILE))

        path = os.path.join(OUT, "envelopes_t%03d.png" % int(age * 100))
        sheet.convert("RGB").save(path)
        print("записано:", path)

    print("шестнадцать оболочек, три момента времени")


if __name__ == "__main__":
    main()
