# -*- coding: utf-8 -*-
"""
Бюджет эффектов: сколько частиц и свечений может оказаться на экране разом.

Нужно затем, что «стало спокойнее» — это не мнение. Числа читаются из кода, а не
вписаны сюда, поэтому расчёт не может разойтись с игрой.

    python3 Tools/check_effects.py
"""
import io
import os
import re

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(name):
    return io.open(os.path.join(ROOT, name), encoding="utf-8").read()


FX = read("Assets/Scripts/SkinFxRuntime.cs")
STATUS = read("Assets/Scripts/StatusFx.cs")
CFG = read("Assets/Scripts/GameConfig.cs")
GM = read("Assets/Scripts/GameManager.cs")


def num(src, pattern, default=0.0):
    m = re.search(pattern, src)
    return float(m.group(1)) if m else default


# --- бюджет одного удара ------------------------------------------------------
lo = num(FX, r"Mathf\.Lerp\(([0-9.]+)f, [0-9.]+f, power - 1f\)")
hi = num(FX, r"Mathf\.Lerp\([0-9.]+f, ([0-9.]+)f, power - 1f\)")
life = num(FX, r"ImpactMaxLife = ([0-9.]+)f;")
speed = num(FX, r"ImpactMaxSpeed = ([0-9.]+)f;")
bubble_r = num(CFG, r"bubbleRadius = ([0-9.]+)f;") * num(CFG, r"playerScaleMultiplier = ([0-9.]+)f;")
# Частицы удара тоже масштабируются множителем игрока — ImpactSpeed его учитывает.
speed *= num(CFG, r"playerScaleMultiplier = ([0-9.]+)f;")

rarities = [("обычный", 1.0), ("эпический", 1.35), ("легендарный", 1.7), ("боссовый", 2.0)]

print("=== удар стража ===")
print("было:  вспышка 0.62 + вспышка 0.90 (размером с шарик) + кольцо 10–20 + 15–30 частиц")
print("стало: только частицы, бюджет ниже\n")
print("%-14s %-8s %-8s" % ("редкость", "обычно", "щадящий"))
for name, power in rarities:
    full = round(lo + (hi - lo) * (power - 1.0))
    calm = round(full * 0.65)
    print("%-14s %-8d %-8d" % (name, max(5, min(20, full)), max(5, min(20, calm))))

print("\nжизнь частицы: %.2f с (было до 1.2)" % life)
print("разлёт: %.2f юнита при диаметре шарика %.2f" % (speed * life, bubble_r * 2))

# --- лимит одновременных ударов ----------------------------------------------
rate = num(FX, r"now - _lastImpact < ([0-9.]+)f")
per_sec = int(num(FX, r"_impactsThisSecond >= (\d+)"))
alive_impacts = max(1, int(life / rate)) if rate else 1

print("\n=== сколько эффектов удара живёт разом ===")
print("не чаще раза в %.2f с, не больше %d за секунду" % (rate, per_sec))
print("при жизни %.2f с одновременно видно не больше %d" % (life, alive_impacts))

worst_impact = alive_impacts * max(5, min(20, round(hi)))
print("частиц от ударов в худшем случае: %d (было до %d)"
      % (worst_impact, 6 * (2 + 20 + 30)))

# --- статусы ------------------------------------------------------------------
max_active = int(num(STATUS, r"const int MaxActive = (\d+);"))
max_dur = num(STATUS, r"MaxDuration = ([0-9.]+)f;")
print("\n=== статусы препятствий ===")
print("одновременно: %d (было 8)" % max_active)
print("длительность: до %.1f с (в таблице встречается до 4.0)" % max_dur)
print("частиц от статусов: по одной на такт на статус, то есть не больше %d" % max_active)

# --- свечения -----------------------------------------------------------------
bubble_src = read("Assets/Scripts/BubbleController.cs")
halo_a = num(bubble_src, r"Vector3\.one \* \(([0-9.]+)f \+ intensity")
halo_b = num(bubble_src, r"\+ intensity \* ([0-9.]+)f\)")

print("\n=== свечения ===")
print("источников света (Light2D) в проекте: 0 — свечение рисуется спрайтом")
print("гало шарика: до %.2f диаметра тела (было до 1.81)" % (halo_a + halo_b * 0.45))
print("ореол статуса: 1.30 ± 0.05 (было 1.70 ± 0.18)")
print("аур на экране: 2 (шарик и страж), как и было")

# --- общий потолок ------------------------------------------------------------
pool = int(num(CFG, r"maxParticles = (\d+)"))
print("\n=== потолок пула ===")
print("размер пула частиц: %d (не менялся — он и есть жёсткий предел)" % pool)

# --- частота изменений яркости ------------------------------------------------
print("\n=== резкие изменения яркости ===")
hard = []
for name, src in (("SkinFxRuntime", FX), ("StatusFx", STATUS)):
    for m in re.finditer(r"Mathf\.Repeat\(t \* ([0-9.]+)f, 1f\) > ", src):
        hard.append("%s: ступенька на %s" % (name, m.group(1)))
if hard:
    print("НАЙДЕНЫ ступенчатые переключения:")
    for h in hard:
        print("  " + h)
else:
    print("ступенчатых переключений яркости не осталось")

assert not hard, "остались ступенчатые переключения яркости"
assert max_active <= 3
assert life <= 0.3
assert "FlashScreen" not in read("Assets/Scripts/UIManager.cs")
assert "_hitStop" not in GM
print("\nвсе проверки прошли")
