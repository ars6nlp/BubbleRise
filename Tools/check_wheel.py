# -*- coding: utf-8 -*-
"""
Колесо удачи: куда указывает стрелка, вокруг чего вращается, как быстро и
совпадают ли шансы с весами.

Вопросы, на которые нельзя ответить, глядя в код:

1. Вращается ли то, что вращается, вокруг центра диска. Прежняя проверка
   считала поворот вокруг центра, а опорная точка диска стояла на верхнем
   крае обода (H-01): диск носился по экрану, а проверка проходила. Теперь
   опорные точки читаются из кода построения.
2. Нет ли строба. Диск с секторами двух яркостей, крутящийся 4–6 оборотов за
   2.6 с, давал до 10 перепадов в секунду на каждой точке (E-04). Теперь
   вращаться может только указатель, и не быстрее полуоборота в секунду.
3. Встаёт ли указатель на выпавший сектор. 10 000 вращений подряд — каждое с
   того места, где остановилось прошлое, со случайным смещением внутри сектора,
   — по тем же формулам, что в коде.
4. Совпадают ли шансы с весами в таблице, и подписаны ли они (G-03).

Числа и формулы читаются из кода, а не вписаны сюда. Комментарии из кода
вырезаются: проверка смотрит на то, что исполняется.

    python3 Tools/check_wheel.py
"""
import io
import math
import os
import random
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(name):
    return io.open(os.path.join(ROOT, name), encoding="utf-8").read()


def strip_comments(src):
    src = re.sub(r"/\*.*?\*/", " ", src, flags=re.S)
    return re.sub(r"//[^\n]*", "", src)


def body(src, signature):
    """Тело метода по началу сигнатуры, по счёту скобок."""
    i = src.find(signature)
    if i < 0:
        return ""
    i = src.find("{", i)
    depth, j = 1, i + 1
    while j < len(src) and depth:
        depth += {"{": 1, "}": -1}.get(src[j], 0)
        j += 1
    return src[i + 1:j - 1]


def const(src, name):
    m = re.search(r"const float %s = (-?[0-9.]+)f;" % name, src)
    return float(m.group(1)) if m else None


TABLES = strip_comments(read("Assets/Scripts/RewardTables.cs"))
UI = strip_comments(read("Assets/Scripts/UIManagerRewards.cs"))
LOGIC = strip_comments(read("Assets/Scripts/UIManagerRewardsLogic.cs"))
SPRITE = strip_comments(read("Assets/Scripts/SpriteFactoryRewards.cs"))

failures = []


def fail(msg):
    failures.append(msg)
    print("ПРОВАЛ: " + msg)


# --- сектора из таблицы -------------------------------------------------------
block = re.search(r"WheelSectors =\s*\{(.*?)\};", TABLES, re.S).group(1)
SECTORS = [(float(w), int(c)) for w, c in re.findall(r"\(([0-9.]+)f,\s*(\d+)\)", block)]
N = len(SECTORS)
print("секторов в таблице: %d; значения: %s" % (N, ", ".join(str(c) for _, c in SECTORS)))

sprite_n = int(re.search(r"const int sectors = (\d+);", SPRITE).group(1))
if sprite_n != N:
    fail("спрайт рисует %d секторов, в таблице %d" % (sprite_n, N))

# --- опорные точки и что вращается -------------------------------------------
build = body(UI, "void BuildWheelPage(")
tick = body(LOGIC, "void TickWheel(")


def pivot_of(var):
    """Опорная точка RectTransform по последнему присваиванию в BuildWheelPage."""
    found = re.findall(r"\b%s\.pivot\s*=\s*new Vector2\(([-0-9.]+)f,\s*([-0-9.]+)f\)" % re.escape(var), build)
    if found:
        return tuple(float(x) for x in found[-1])
    # Anchor(rt, anchor, …) ставит pivot = (0.5, anchor.y)
    m = re.search(r"Anchor\(%s,\s*new Vector2\(([-0-9.]+)f,\s*([-0-9.]+)f\)" % re.escape(var), build)
    if m:
        return (0.5, float(m.group(2)))
    return None


# Что именно поворачивает TickWheel: имя поля -> локальная переменная построения.
rotated = re.findall(r"\b(_wheel\w+)\.localRotation\s*=\s*Quaternion\.Euler\(0f,\s*0f,\s*([^)]*)\)", tick)
local_of = {f: f for f in re.findall(r"\b(_wheel\w+)\s*=\s*NewRect\(", build)}
for field, var in re.findall(r"\b(_wheel\w+)\s*=\s*(\w+);", build):
    local_of.setdefault(field, var)

spins_face = any(f == "_wheelFace" and "_wheelAngle" in expr for f, expr in rotated)
spins_orbit = [f for f, expr in rotated if "_wheelAngle" in expr and f != "_wheelFace"]
print("вращается по _wheelAngle: %s" % (", ".join(f for f, e in rotated if "_wheelAngle" in e) or "ничего"))

for f, expr in rotated:
    if "_wheelAngle" not in expr:
        continue
    var = local_of.get(f, f)
    pv = pivot_of(var) or pivot_of(f)
    print("  %s (%s): опорная точка %s" % (f, var, pv))
    if pv != (0.5, 0.5):
        fail("%s вращается вокруг %s, а не вокруг центра" % (f, pv))

# Держатель указателя обязан стоять в центре диска.
if spins_orbit:
    orbit_var = local_of.get(spins_orbit[0], spins_orbit[0])
    if not re.search(r"\b%s\.anchoredPosition\s*=\s*_wheelFace\.anchoredPosition;" % re.escape(orbit_var), build):
        fail("держатель указателя не совмещён с центром диска")

# Строб: крутящийся диск допустим только при секторах одной яркости.
lum = None
m_l = re.search(r'Color light = Palette\.Hex\("([0-9A-Fa-f]{6})"\);\s*Color dark = Palette\.Hex\("([0-9A-Fa-f]{6})"\);',
                SPRITE)


def rel_lum(h):
    def lin(c):
        c /= 255.0
        return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)


if m_l:
    lum = abs(rel_lum(m_l.group(1)) - rel_lum(m_l.group(2)))
    print("разница яркости секторов: %.3f" % lum)
if spins_face and (lum is None or lum >= 0.03):
    fail("крутится диск с секторами разной яркости (ΔY %.3f ≥ 0.03) — строб" % (lum or 0))

# --- скорость -----------------------------------------------------------------
max_tps = const(LOGIC, "WheelMaxTurnsPerSecond")
peak = const(LOGIC, "WheelEasePeak")
dur = const(LOGIC, "WheelSpinDuration")
min_travel = const(LOGIC, "WheelMinTravel")
ease = body(LOGIC, "static float WheelEase(")
spin_time = re.search(r"static float SpinTime\(float travel\)\s*=>\s*([^;]+);", LOGIC)

if None in (max_tps, peak, dur, min_travel) or not spin_time:
    fail("нет ограничения скорости указателя (WheelMaxTurnsPerSecond, WheelEasePeak, SpinTime)")
elif "k * k * (3f - 2f * k)" not in ease:
    fail("кривая хода не 3k² − 2k³ — пик скорости неизвестен")
else:
    # Пик производной 3k² − 2k³ — 1.5 при k = 0.5. Сверяем с константой кода.
    true_peak = max(6 * k * (1 - k) for k in (i / 1000.0 for i in range(1001)))
    if abs(true_peak - peak) > 1e-3:
        fail("WheelEasePeak %.3f, а у кривой %.3f" % (peak, true_peak))
    if max_tps > 0.5:
        fail("скорость указателя %.2f об/с > 0.5" % max_tps)
    if "Mathf.Max(WheelSpinDuration, WheelEasePeak * (travel / 360f) / WheelMaxTurnsPerSecond)" not in spin_time.group(1):
        fail("длительность хода не выводится из предела скорости")

# --- формулы угла -------------------------------------------------------------
m_center = re.search(r"return \(index \+ 0\.5f\) \* 360f / n - ([0-9.]+)f;", LOGIC)
travel_b = body(LOGIC, "static float PointerTravel(")
under_b = body(LOGIC, "static int SectorUnderPointer(")
m_want = re.search(r"float want = SectorCenter\(sector\) \+ jitter - ([0-9.]+)f;", travel_b)
m_rep = re.search(r"float travel = Mathf\.Repeat\(from - want, 360f\);", travel_b)
m_min = re.search(r"if \(travel < WheelMinTravel\) travel \+= 360f;", travel_b)
m_jit = re.search(r"float jitter = UnityEngine\.Random\.Range\(-half \* ([0-9.]+)f", travel_b)
m_under = re.search(r"float local = Mathf\.Repeat\(([0-9.]+)f \+ angle \+ ([0-9.]+)f, 360f\);", under_b)
m_dir = re.search(r"_wheelTo = _wheelAngle - travel;", LOGIC)

if not (m_center and m_want and m_rep and m_min and m_jit and m_under and m_dir):
    fail("формулы указателя не разобраны: PointerTravel / SectorUnderPointer / направление хода")
    print("\nпроверок не прошло: %d" % len(failures))
    sys.exit(1)

center_shift = float(m_center.group(1))
want_shift = float(m_want.group(1))
jit = float(m_jit.group(1))
under_a, under_b2 = float(m_under.group(1)), float(m_under.group(2))
if abs(center_shift - 180.0) > 0.01:
    fail("центр сектора смещён на %g, а спрайт рисует со сдвигом 180" % center_shift)
if jit >= 1.0:
    fail("смещение внутри сектора ±%.0f %% полусектора — может встать на границу" % (jit * 100))


def sector_center(i):
    return (i + 0.5) * 360.0 / N - center_shift


def sprite_sector(screen_deg):
    """Сектор по формуле спрайта: int((Atan2(y, x) + π) / 2π · N)."""
    a = math.radians(screen_deg)
    ang = math.atan2(math.sin(a), math.cos(a)) + math.pi
    return int(ang / (2 * math.pi) * N) % N


def under(angle):
    local = (under_a + angle + under_b2) % 360.0
    return min(N - 1, max(0, int(local / (360.0 / N))))


def travel(sector, frm, rng):
    half = 360.0 / N * 0.5
    jitter = rng.uniform(-half * jit, half * jit)
    want = sector_center(sector) + jitter - want_shift
    t = (frm - want) % 360.0
    if t < min_travel:
        t += 360.0
    return t


rng = random.Random(20261029)
total_w = sum(w for w, _ in SECTORS)
counts = [0] * N
ROLLS = 10000
angle = 0.0
miss = 0
miss_sprite = 0
fastest = 0.0
longest = 0.0
for _ in range(ROLLS):
    pick = rng.random() * total_w
    sector = N - 1
    for i, (w, _c) in enumerate(SECTORS):
        pick -= w
        if pick <= 0:
            sector = i
            break
    counts[sector] += 1
    t = travel(sector, angle, rng)
    angle -= t
    if under(angle) != sector:
        miss += 1
    # Указатель стоит на экранном угле 90 + angle: что там нарисовал спрайт?
    if sprite_sector(90.0 + angle) != sector:
        miss_sprite += 1
    secs = max(dur, peak * (t / 360.0) / max_tps)
    longest = max(longest, secs)
    fastest = max(fastest, peak * (t / 360.0) / secs)

print("\n=== %d вращений подряд ===" % ROLLS)
print("указатель не на выпавшем секторе: по коду %d, по спрайту %d" % (miss, miss_sprite))
print("самая высокая скорость: %.3f об/с, самое долгое вращение: %.2f с" % (fastest, longest))
if miss or miss_sprite:
    fail("указатель промахивается: %d / %d из %d" % (miss, miss_sprite, ROLLS))
if fastest > 0.5 + 1e-6:
    fail("указатель быстрее полуоборота в секунду: %.3f" % fastest)

print("\n%-8s %-8s %-9s %-9s" % ("монет", "вес", "ожидали", "выпало"))
worst = 0.0
for i, (w, c) in enumerate(SECTORS):
    expect = w / total_w * 100
    got = counts[i] / ROLLS * 100
    worst = max(worst, abs(expect - got))
    print("%-8d %-8.0f %-9.2f%% %-9.2f%%" % (c, w, expect, got))
if worst >= 1.5:
    fail("распределение разошлось с весами на %.2f п.п." % worst)

# --- подпись шансов (G-03) ----------------------------------------------------
if not re.search(r"WheelSectors\[i\]\.Weight / totalWeight \* 100f", build):
    fail("шанс сектора не подписан на колесе")

avg = sum(SECTORS[i][1] * SECTORS[i][0] for i in range(N)) / total_w
print("\nсреднее за вращение: %.2f монеты" % avg)

if failures:
    print("\nпроверок не прошло: %d" % len(failures))
    sys.exit(1)
print("\nвсе проверки прошли")
sys.exit(0)
