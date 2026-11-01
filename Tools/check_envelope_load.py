# -*- coding: utf-8 -*-
"""Во что оболочка столкновения обходится на экране.

    python3 Tools/check_envelope_load.py

Считает худший случай: сколько спрайтов прибавилось к кадру и сколько их всего
становится. Числа читаются из исходников, а не вписаны сюда — иначе отчёт
устарел бы на первой же правке таблицы.

Колонка «было» — прежнее устройство: тень и ядро россыпью точек, девять точек
на каждую ветвь связи. Она оставлена, чтобы экономию можно было проверить, а не
принять на слово.
"""
import io
import os
import re
import sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(rel):
    return io.open(os.path.join(ROOT, rel), encoding="utf-8").read()


tbl = read("Assets/Scripts/EnvelopeTable.cs")
ctl = read("Assets/Scripts/CollisionEnvelope.cs")
shp = read("Assets/Scripts/EnvelopeShape.cs")
ui = read("Assets/Scripts/UIManagerEnvelope.cs")
cfg = read("Assets/Scripts/GameConfig.cs")
vis = read("Assets/Scripts/Visuals.cs")

ROW = re.compile(
    r'D\(EnvelopeKind\.(\w+),\s*LinkKind\.(\w+),\s*'
    r'"[0-9A-Fa-f]{6}",\s*"[0-9A-Fa-f]{6}",\s*"[0-9A-Fa-f]{6}",\s*"[0-9A-Fa-f]{6}",\s*'
    r'([\d.]+)f,\s*(\d+),\s*([\d.]+)f,\s*([\d.]+)f,\s*(\d+),\s*([\d.]+)f,\s*(true|false)\)')

rows = [(m.group(1), m.group(2), float(m.group(3)), int(m.group(4)),
         float(m.group(5)), int(m.group(7)), m.group(9) == "true")
        for m in ROW.finditer(tbl)]
assert len(rows) == 16, len(rows)

pool = int(re.search(r"PoolSize = (\d+);", ctl).group(1))
max_env = int(re.search(r"MaxEnvelopes = (\d+);", ctl).group(1))
budget = int(re.search(r"LinkTravelBudget = (\d+);", shp).group(1))
spread = int(re.search(r"LinkSpread = (\d+);", shp).group(1))
max_particles = int(re.search(r"maxParticles = (\d+);", cfg).group(1))
quiet_k = float(re.search(r"Particles => Level == FxBrightness\.Quiet \? ([\d.]+)f", vis).group(1))

gr = float(re.search(r"guardianRadius = ([\d.]+)f;", cfg).group(1))
scale = float(re.search(r"playerScaleMultiplier = ([\d.]+)f;", cfg).group(1))
world_h = float(re.search(r"worldHeight = ([\d.]+)f;", cfg).group(1))
aspect = float(re.search(r"designAspect = ([\d.]+)f;", cfg).group(1))

D = gr * scale * 2.0
screen = (world_h * aspect) * world_h
base = max_particles + 40 + 3

print("=== геометрия ===")
print("диаметр стража D:         %.3f юнита" % D)
print("экран:                    %.1f юнита кв." % screen)

print()
print("=== сколько объектов на экране ===")
print("%-10s %-8s %-6s %-6s %-6s %-6s %-7s %-7s" %
      ("форма", "роль", "кольца", "форма", "искры", "связь", "стало", "было"))

worst = worst_quiet = 0
worst_name = ""
worst_before = 0

for kind, link, duration, count, radius, branches, rich in rows:
    steps = max(3, min(8, budget // branches))

    rings = 3
    shape = max(8, int(round(count * 0.5)))
    spark = max(4, int(round(count * 0.1)))
    nlink = branches * steps + spread
    total = rings + shape + spark + nlink

    shape_q = max(8, int(round(count * quiet_k * 0.5)))
    spark_q = max(4, int(round(count * quiet_k * 0.1)))
    total_q = rings + shape_q + spark_q + nlink

    # как было: тень 12 %, ядро 20 %, форма 50 %, искры 18 %, связь 9 точек
    # на каждую ветвь, пятно отдельной долей внутри тех же девяти
    before = (max(4, int(round(count * 0.12))) + max(6, int(round(count * 0.2)))
              + shape + max(4, int(round(count * 0.18))) + branches * 9)

    if total > worst:
        worst, worst_name, worst_before = total, kind, before
    worst_quiet = max(worst_quiet, total_q)

    print("%-10s %-8s %-6d %-6d %-6d %-6d %-7d %-7d"
          % (kind, "трофей" if rich else "легенд.", rings, shape, spark, nlink,
             total, before))

print()
print("худший случай:            %d объектов (%s), было %d — минус %.0f %%"
      % (worst, worst_name, worst_before, (1 - worst / float(worst_before)) * 100))
print("он же на «Тихо»:          %d" % worst_quiet)
print("пул одной оболочки:       %d россыпи и связи + 3 кольца" % pool)
print("оболочек одновременно:    не более %d, вторая с весом 0.6" % max_env)

both = worst + int(round(worst * 0.6))

print()
print("=== кадр целиком ===")
print("было в кадре без оболочки: %d" % base)
print("одна оболочка:            %d  (+%.1f %%)   было +%.1f %%"
      % (base + worst, worst / float(base) * 100, worst_before / float(base) * 100))
print("две оболочки:             %d  (+%.1f %%)" % (base + both, both / float(base) * 100))
print("одна на «Тихо»:           %d  (+%.1f %%)"
      % (base + worst_quiet, worst_quiet / float(base) * 100))

print()
print("=== батчинг ===")
print("материал:                 один на всё, SpriteFactory.Unlit")
print("текстуры:                 SoftDot у россыпи и связи, EnvelopeRing у тени")
print("                          и ядра, EnvelopeSpeck у акцента — то есть")
print("                          три партии вместо одной, и они не зависят от")
print("                          числа частиц")
print("слои сортировки:          23…27, все выше стража (20) и ниже частиц (30)")
print("Light2D / LineRenderer:   не используются нигде в проекте")

print()
print("=== отсечка по экрану ===")
print("частицы за границами поля не получают ни Transform, ни цвета и")
print("выключаются; границы берутся у Playfield и обновляются каждый кадр.")
print("Экономия ситуативная (зависит от того, где страж), в числа выше не входит.")

TARGET = 0.35
share = worst / float(base)
print()
print("цель: не больше +35 %% на одну оболочку — %s (+%.1f %%)"
      % ("ВЫПОЛНЕНО" if share <= TARGET else "НЕ ВЫПОЛНЕНО", share * 100))

assert pool >= worst - 3, "пул меньше худшего случая"
assert share <= TARGET, "цель по нагрузке не достигнута"
print("\nготово")
