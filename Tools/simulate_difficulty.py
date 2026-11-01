# -*- coding: utf-8 -*-
"""
Прогон кривой сложности вне Unity.

Повторяет генератор препятствий: разгон прокрутки, сокращение интервала спавна,
мягкий старт, открытие видов по глубине. Нужно затем, что «стало ли легче» —
это не мнение, а числа: сколько препятствий встречает игрок на первых двухстах
метрах, когда появляется первый опасный вид, и остаётся ли на экране проход.

Числа не вшиты, а вычитываются из GameConfig.cs, DifficultyConfig.cs и
HazardTable.cs. Иначе прогон однажды разойдётся с игрой и начнёт успокаивать.

    python3 Tools/simulate_difficulty.py
"""
import io
import os
import random
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(name):
    return io.open(os.path.join(ROOT, name), encoding="utf-8").read()


def number(src, field, default=0.0):
    m = re.search(r"\b%s\s*=\s*(-?[0-9.]+)f?\s*;" % field, src)
    return float(m.group(1)) if m else default


CFG = read("Assets/Scripts/GameConfig.cs")
DIFF = read("Assets/Scripts/DifficultyConfig.cs")
HAZ = read("Assets/Scripts/HazardTable.cs")

# --- GameConfig ---------------------------------------------------------------
WORLD_H = number(CFG, "worldHeight", 10)
ASPECT = number(CFG, "designAspect", 0.64)
METERS = number(CFG, "metersPerUnit", 7.218)
BUBBLE_R = number(CFG, "bubbleRadius", 0.28)
BASE_SPEED = number(CFG, "baseScrollSpeed", 1.04)
ACCEL = number(CFG, "scrollAcceleration", 0.0161)
MAX_SPEED = number(CFG, "maxScrollSpeed", 4.56)
BASE_INTERVAL = number(CFG, "baseSpawnInterval", 1.47)
MIN_INTERVAL = number(CFG, "minSpawnInterval", 1.04)
DECAY = number(CFG, "spawnIntervalDecay", 0.00074)
OBST_MIN = number(CFG, "obstacleMinRadius", 0.21)
OBST_MAX = number(CFG, "obstacleMaxRadius", 0.37)

PLAY_HALF = (WORLD_H * 0.5) * ASPECT


# --- DifficultyConfig: три пресета -------------------------------------------
def preset(name):
    """Поля пресета: у easy значения в объявлении полей, у остальных — в блоке."""
    defaults = {}
    body = DIFF[DIFF.index("public class DifficultyPreset"):DIFF.index("public class DifficultyConfig")]
    for m in re.finditer(r"public (?:int|float) (\w+) = (-?[0-9.]+)f?;", body):
        defaults[m.group(1)] = float(m.group(2))

    if name == "easy":
        return defaults

    block = re.search(r"%s = new DifficultyPreset\s*\{(.*?)\n        \};" % name, DIFF, re.S)
    values = dict(defaults)
    if block:
        for m in re.finditer(r"(\w+)\s*=\s*(-?[0-9.]+)f?,", block.group(1)):
            values[m.group(1)] = float(m.group(2))
    return values


# --- HazardTable: виды и глубины ---------------------------------------------
def hazards():
    out = []
    for m in re.finditer(r"Type = ObstacleType\.(\w+),.*?UnlockDepth = (\d+),.*?Weight = ([0-9.]+)f,"
                         r"(?P<danger> Dangerous = true,)?", HAZ, re.S):
        out.append({
            "name": m.group(1),
            "depth": int(m.group(2)),
            "weight": float(m.group(3)),
            "dangerous": bool(m.group("danger")),
        })
    return out


HAZARDS = hazards()


def unlocked_at(index, depth, p):
    h = HAZARDS[index]
    if h["depth"] <= 0:
        return True
    if depth < p["warmupMeters"]:
        return False

    at = h["depth"] * max(0.1, p["unlockDepthScale"])
    if h["dangerous"]:
        at += p["dangerousExtraDepth"]

    order = sum(1 for i in range(index) if HAZARDS[i]["depth"] > 0)
    at = max(at, p["warmupMeters"] + order * p["newTypeEveryMeters"])
    return depth >= at


def spawn_factor(depth, p, relief=1.0):
    factor = p["spawnIntervalScale"] * relief
    if p["warmupMeters"] > 0 and depth < p["warmupMeters"]:
        t = min(1.0, depth / p["warmupMeters"])
        factor *= p["warmupSpawnFactor"] + (1.0 - p["warmupSpawnFactor"]) * (t * t)
    return factor


# --- сам прогон ---------------------------------------------------------------
def run(p, seed, limit_m=5000.0, dt=1.0 / 60.0):
    rng = random.Random(seed)

    speed = BASE_SPEED
    cap = MAX_SPEED * p["speedCapScale"]
    interval = BASE_INTERVAL
    timer = interval
    distance = 0.0

    alive = []            # (x, y, radius)
    per100 = {}
    alive_sum = 0.0
    alive_ticks = 0
    speed_at_1000 = None
    seconds = 0.0
    spawned = 0
    worst_gap = PLAY_HALF * 2
    worst_gap_at = 0
    first_danger = None

    top = WORLD_H * 0.5

    while distance < limit_m:
        speed = min(cap, speed + ACCEL * p["accelerationScale"] * dt)
        interval = max(MIN_INTERVAL, interval - dt * DECAY * p["spawnDecayScale"])
        distance += speed * dt * METERS
        seconds += dt
        if speed_at_1000 is None and distance >= 1000:
            speed_at_1000 = speed

        # движение и уборка
        for o in alive:
            o[1] -= speed * dt
        alive = [o for o in alive if o[1] > -top - 1.0]

        timer -= dt
        if timer <= 0:
            timer = interval * spawn_factor(distance, p) * rng.uniform(0.8, 1.2)

            pool = [i for i in range(len(HAZARDS)) if unlocked_at(i, distance, p)]
            weights = [HAZARDS[i]["weight"] for i in pool]
            pick = rng.choices(pool, weights=weights)[0]
            if HAZARDS[pick]["dangerous"] and first_danger is None:
                first_danger = int(distance)

            radius = rng.uniform(OBST_MIN, OBST_MAX)
            limit = max(0.05, PLAY_HALF - radius - 0.15)
            x = rng.uniform(-limit, limit)

            # отступ от края — как в ObstacleSpawner
            edge_keep = BUBBLE_R * 2 * p["minGapInBubbles"] * 0.5
            if limit > edge_keep:
                x = max(-limit + edge_keep * 0.5, min(limit - edge_keep * 0.5, x))

            alive.append([x, top + radius + 0.3, radius])
            spawned += 1

            bucket = int(distance // 100)
            per100[bucket] = per100.get(bucket, 0) + 1

        # Самый узкий проход: берём горизонтальную полосу высотой с пузырёк вокруг
        # его линии и смотрим, сколько свободного места между препятствиями в ней.
        alive_sum += len(alive)
        alive_ticks += 1

        band = [o for o in alive if abs(o[1] - (-top * 0.36)) < o[2] + BUBBLE_R]
        if band:
            gap = widest_gap(band)
            if gap < worst_gap:
                worst_gap = gap
                worst_gap_at = int(distance)

    return {
        "per100": per100,
        "worst_gap": worst_gap,
        "worst_gap_at": worst_gap_at,
        "first_danger": first_danger,
        "end_speed": speed,
        "avg_alive": alive_sum / max(1, alive_ticks),
        "speed_1000": speed_at_1000 or speed,
        "seconds_to_end": seconds,
        "per_second": spawned / max(0.001, seconds),
    }


def widest_gap(band):
    """Самый широкий свободный коридор по горизонтали в этой полосе."""
    blocked = sorted((o[0] - o[2], o[0] + o[2]) for o in band)
    best = 0.0
    cursor = -PLAY_HALF
    for a, b in blocked:
        if a > cursor:
            best = max(best, a - cursor)
        cursor = max(cursor, b)
    best = max(best, PLAY_HALF - cursor)
    return best


def report(name, p, runs=5):
    print("\n=== %s ===" % name)

    totals = {}
    worst = 99.0
    worst_at = 0
    danger = []
    alive = []
    speeds = []
    rates = []
    minutes = []

    for seed in range(runs):
        r = run(p, seed)
        for k, v in r["per100"].items():
            totals[k] = totals.get(k, 0) + v
        if r["worst_gap"] < worst:
            worst = r["worst_gap"]
            worst_at = r["worst_gap_at"]
        if r["first_danger"]:
            danger.append(r["first_danger"])
        alive.append(r["avg_alive"])
        speeds.append(r["speed_1000"])
        rates.append(r["per_second"])
        minutes.append(r["seconds_to_end"])

    print("препятствий на 100 м (среднее по %d прогонам):" % runs)
    for bucket in sorted(totals)[:6]:
        print("  %4d–%4d м: %4.1f" % (bucket * 100, bucket * 100 + 100, totals[bucket] / runs))

    # Главное число: не сколько их на метр, а сколько времени между ними. Именно
    # его игрок и чувствует как «не успеваю».
    rate = sum(rates) / len(rates)
    print("темп: %.2f препятствия в секунду (%.1f с на каждое)" % (rate, 1.0 / rate))
    print("на экране одновременно: %.1f" % (sum(alive) / len(alive)))
    print("скорость прокрутки на 1000 м: %.2f юнита/с" % (sum(speeds) / len(speeds)))
    print("до 5000 м: %.0f с" % (sum(minutes) / len(minutes)))

    need = BUBBLE_R * 2 * p["minGapInBubbles"]
    ok = worst >= need
    print("самый узкий проход: %.2f юнита на %d м (нужно %.2f) — %s"
          % (worst, worst_at, need, "хватает" if ok else "УЗКО"))
    print("первый опасный вид: %s м" % (int(sum(danger) / len(danger)) if danger else "не встретился"))

    types = [h["name"] for i, h in enumerate(HAZARDS) if unlocked_at(i, 200, p)]
    print("видов открыто на 200 м: %d (%s)" % (len(types), ", ".join(types)))

    return ok


def main():
    print("мир %.0f юнитов, игровая зона %.2f юнита в ширину, пузырёк R=%.2f"
          % (WORLD_H, PLAY_HALF * 2, BUBBLE_R))

    results = []
    for name, key in (("ЛЁГКАЯ", "easy"), ("ОБЫЧНАЯ", "normal"), ("СЛОЖНАЯ", "hard")):
        results.append(report(name, preset(key)))

    print("\nпроход не уже требуемого на лёгкой и обычной: %s"
          % ("да" if results[0] and results[1] else "НЕТ"))

    assert results[0], "на лёгкой сложности встретился непроходимый участок"
    assert results[1], "на обычной сложности встретился непроходимый участок"
    print("все проверки прошли")


if __name__ == "__main__":
    main()
