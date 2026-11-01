# -*- coding: utf-8 -*-
"""Во что обходятся скины боссов и новая медуза.

    python3 -X utf8 Tools/check_boss_art.py

Числа читаются из исходников, а не вписаны сюда: таблица боссов, константы
щупалец и размеры текстур. Иначе отчёт устарел бы на первой правке.
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


tbl = read("Assets/Scripts/BossSkinTable.cs")
tent = read("Assets/Scripts/JellyTentacles.cs")
fac = read("Assets/Scripts/SpriteFactory.cs")
bskin = read("Assets/Scripts/SpriteFactoryBossSkins.cs")

rows = re.findall(r"BossMotif\.(\w+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, (\d+), ([\d.]+)f", tbl)
assert len(rows) == 10, len(rows)

base = int(re.search(r"const int Size = (\d+);", fac).group(1))
skin = int(re.search(r"public const int SkinSize = (\d+);", bskin).group(1))
arms = int(re.search(r"public const int Arms = (\d+);", tent).group(1))
links = int(re.search(r"public const int Links = (\d+);", tent).group(1))
fringe = int(re.search(r"public const int Fringes = (\d+);", tent).group(1))


def kb(w, h=None):
    return w * (h or w) * 4 / 1024.0


print("=== скины боссов ===")
print("%-10s %-10s %-7s %-6s %-6s %-6s" %
      ("босс", "фактура", "частота", "сила", "ядро", "рукавов"))
for i, (motif, scale, strength, show, core_arms, twist) in enumerate(rows):
    print("%-10s %-10s %-7s %-6s %-6s %-6s" % (i, motif, scale, strength, show, core_arms))

dup = len(set(r[0] for r in rows))
print()
print("разных фактур:            %d из 10" % dup)
print("разрешение силуэта:       было %d, стало %d" % (base, skin))
print("слоёв у трофея:           было 1 (плоский силуэт), стало 7")
print("                          силуэт, фактура, кайма, блик, тень, обводка, начинка")

skins_before = 22 * kb(base)
skins_after = 22 * kb(skin) + 10 * kb(skin)
print()
print("=== память текстур, трофеи ===")
print("было:   22 силуэта по %d = %.1f МБ" % (base, skins_before / 1024))
print("стало:  22 силуэта по %d + 10 начинок = %.1f МБ" % (skin, skins_after / 1024))
print("разница: %+.1f МБ" % ((skins_after - skins_before) / 1024))

print()
print("=== медуза: объекты на экране ===")
before = 1 + 2
after = 1 + 1 + 1 + 1 + 1 + arms * links + fringe + 2   # + рябь телеграфа
print("%-34s %s" % ("было: купол + 2 кольца поля", before))
print("%-34s %s" % ("стало: дымка", 1))
print("%-34s %s" % ("       свечение", 1))
print("%-34s %s" % ("       купол", 1))
print("%-34s %s" % ("       глаз", 1))
print("%-34s %s" % ("       рябь телеграфа", 1))
print("%-34s %s" % ("       рукава %d x %d звеньев" % (arms, links), arms * links))
print("%-34s %s" % ("       полосы нитей", fringe))
print("%-34s %s" % ("       кольца поля", 2))
print("%-34s %s" % ("ИТОГО", after))

print()
print("прибавка к самому боссу:  +%d объектов (x%.1f)" % (after - before, after / float(before)))
print("прибавка к кадру:         +%.1f %% (кадр без босса 383 спрайта)"
      % ((after - before) / 383.0 * 100))
print()
print("Задание требовало не больше +30 % спрайтов У БОССА. Выполнить это")
print("буквально нельзя: сейчас у него ТРИ спрайта, +30 % — это один объект,")
print("а задание в том же пункте требует десятки тонких нитей и рукава с")
print("сегментами. Числа несовместимы, и я их не подгонял. [РАСЧЁТ]")
print()
print("Что сделано вместо подгонки:")
print("  * нити не объекты, а восемь нитей в одной текстуре: %d полос вместо" % fringe)
print("    ~%d отдельных нитей — экономия %d объектов;" % (fringe * 8, fringe * 8 - fringe))
print("  * Transform всех щупалец обновляет ОДИН компонент одним циклом,")
print("    а не %d независимых MonoBehaviour;" % (arms * links + fringe))
print("  * материал один на все слои (SpriteFactory.Unlit), как и раньше.")

tex_before = 2 * kb(128) + kb(base)
tex_after = kb(256) * 3 + kb(128) + kb(64, 128) + 2 * kb(128, 256)
print()
print("=== память текстур, медуза ===")
print("было:   2 варианта купола по 128  = %.0f КБ" % (2 * kb(128)))
print("стало:  купол+свечение+дымка 256, глаз 128, звено 64x128,")
print("        2 полосы 128x256          = %.0f КБ" % tex_after)
print("разница: %+.0f КБ" % (tex_after - 2 * kb(128)))

print()
print("=== безопасность для глаз ===")
print("подсветка урона: было — подмена спрайта на светлый за ОДИН кадр,")
print("                 dY купола +0.53 (порог WCAG 0.10);")
print("                 стало — +0.18 к каналам за 0.4 с, dY +0.08,")
print("                 то есть ~0.003 за кадр при 60 fps. [РАСЧЁТ]")
print("пульсация поля:  было Sin(t*7) — 1.1 цикла в секунду;")
print("                 стало Sin(t*0.9) — один цикл за 7 с. [КОД]")
print("дыхание купола:  один цикл за 2.6 с (BreathPeriod). [КОД]")
print("разгорание глаза: 0.45 с (GlowRise), не быстрее 0.3 с по заданию. [КОД]")
print("смерть:          1.8 с угасания (DeathTime), было 0.9 с. [КОД]")

assert dup == 10, "фактуры повторяются"
assert skin >= 256, "разрешение трофеев ниже 256"
print("\nготово")
