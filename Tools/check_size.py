# -*- coding: utf-8 -*-
"""
Что сделало с игрой уменьшение шарика и пузыря.

Хитбокс пузырька меньше — уворачиваться легче. Хитбокс стража меньше — отбивать
труднее. Эти два эффекта тянут в разные стороны, и на глаз их не сложить.

    python3 Tools/check_size.py
"""
import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(name):
    return io.open(os.path.join(ROOT, name), encoding="utf-8").read()


CFG = read("Assets/Scripts/GameConfig.cs")
DIFF = read("Assets/Scripts/DifficultyConfig.cs")


def num(src, field, default=0.0):
    m = re.search(r"\b%s\s*=\s*(-?[0-9.]+)f?\s*;" % field, src)
    return float(m.group(1)) if m else default


SCALE = num(CFG, "playerScaleMultiplier", 1.0)
BUBBLE = num(CFG, "bubbleRadius", 0.28)
GUARD = num(CFG, "guardianRadius", 0.45)
OBST_MIN = num(CFG, "obstacleMinRadius", 0.21)
OBST_MAX = num(CFG, "obstacleMaxRadius", 0.37)
OBST = (OBST_MIN + OBST_MAX) * 0.5


def preset(name):
    body = DIFF[DIFF.index("public class DifficultyPreset"):DIFF.index("public class DifficultyConfig")]
    values = {}
    for m in re.finditer(r"public (?:int|float) (\w+) = (-?[0-9.]+)f?;", body):
        values[m.group(1)] = float(m.group(2))
    if name == "easy":
        return values
    blk = re.search(r"%s = new DifficultyPreset\s*\{(.*?)\n        \};" % name, DIFF, re.S)
    if blk:
        for m in re.finditer(r"(\w+)\s*=\s*(-?[0-9.]+)f?,", blk.group(1)):
            values[m.group(1)] = float(m.group(2))
    return values


print("множитель размера: %.2f" % SCALE)
print("пузырёк: %.3f -> %.3f" % (BUBBLE, BUBBLE * SCALE))
print("страж:   %.3f -> %.3f" % (GUARD, GUARD * SCALE))

print("\n=== уворот: во что врезается пузырёк ===")
print("%-10s %-12s %-12s %-10s" % ("режим", "было", "стало", "разница"))
for name, key in (("ЛЁГКАЯ", "easy"), ("ОБЫЧНАЯ", "normal"), ("СЛОЖНАЯ", "hard")):
    p = preset(key)
    hit = p["bubbleHitboxScale"]
    haz = p["hazardHitboxScale"]
    before = OBST * haz + BUBBLE * hit
    after = OBST * haz + BUBBLE * SCALE * hit
    print("%-10s %-12.4f %-12.4f %+.1f %%" % (name, before, after, (after / before - 1) * 100))

print("\n=== защита: чем страж достаёт препятствие ===")
print("%-10s %-12s %-12s %-10s" % ("режим", "было", "стало", "разница"))
for name, key in (("ЛЁГКАЯ", "easy"), ("ОБЫЧНАЯ", "normal"), ("СЛОЖНАЯ", "hard")):
    p = preset(key)
    reach = p.get("guardianReachScale", 1.0)
    before = OBST + GUARD
    after = OBST + GUARD * SCALE * reach
    print("%-10s %-12.4f %-12.4f %+.1f %%" % (name, before, after, (after / before - 1) * 100))

print("\nБез поправки guardianReachScale перехват падал на 12.2 %, то есть игра")
print("становилась не легче, а сложнее: уворот выигрывал 10 %, а защита теряла 12.")

print("\n=== ширина прохода ===")
for name, key in (("ЛЁГКАЯ", "easy"), ("ОБЫЧНАЯ", "normal"), ("СЛОЖНАЯ", "hard")):
    p = preset(key)
    gap_before = BUBBLE * 2 * p["minGapInBubbles"]
    gap_after = BUBBLE * SCALE * 2 * p["minGapInBubbles"]
    print("%-10s требуемый проход %.3f -> %.3f, запас к ширине зоны 6.40: %.1fx -> %.1fx"
          % (name, gap_before, gap_after, 6.4 / gap_before, 6.4 / gap_after))

print("\n=== компенсация ===")
for name, key in (("ЛЁГКАЯ", "easy"), ("ОБЫЧНАЯ", "normal"), ("СЛОЖНАЯ", "hard")):
    p = preset(key)
    print("%-10s bubbleHitboxScale = %.3f, guardianReachScale = %.2f"
          % (name, p["bubbleHitboxScale"], p.get("guardianReachScale", 1.0)))

assert 0.5 <= SCALE <= 1.5
print("\nготово")
