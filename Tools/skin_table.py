# -*- coding: utf-8 -*-
"""
Таблица скинов из кода.

Собирается разбором SkinStore, SkinFx и Archetypes, а не пишется руками: иначе
она разойдётся с игрой на первом же новом скине, и отчёт начнёт врать.

Заодно проверяет, что у каждого скина заполнены все каналы различия и что нет
двух полностью одинаковых.

    python3 Tools/skin_table.py
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(name):
    return io.open(os.path.join(ROOT, name), encoding="utf-8").read()


SK = read("Assets/Scripts/SkinStore.cs")
FX = read("Assets/Scripts/SkinFx.cs")
AR = read("Assets/Scripts/Archetypes.cs")
LOC = read("Assets/Scripts/Loc.cs")

names = {}
for m in re.finditer(r'\{\s*"([\w.]+)",\s*new\[\]\s*\{\s*"([^"]*)"', LOC):
    names[m.group(1)] = m.group(2)

# --- материал → статус --------------------------------------------------------
fx_block = FX[FX.index("public static readonly MaterialFxDef[] All ="):FX.index("Эффекты трофейных скинов")]
mat_status, mat_death = {}, {}
for name, args in re.findall(r"/\* (\w+)\s*\*/ D\((.*?)\),\n", fx_block, re.S):
    st = re.search(r"StatusKind\.(\w+)", args)
    de = re.search(r"DestroyPattern\.(\w+)", args)
    mat_status[name] = st.group(1) if st else "?"
    mat_death[name] = de.group(1) if de else "?"

boss_block = FX[FX.index("public static readonly MaterialFxDef[] Bosses ="):]
boss = []
for name, args in re.findall(r"/\* (\w+)\s*\*/ D\((.*?)\),\n", boss_block, re.S):
    st = re.search(r"StatusKind\.(\w+)", args)
    de = re.search(r"DestroyPattern\.(\w+)", args)
    boss.append((st.group(1) if st else "?", de.group(1) if de else "?"))

# --- движение и след по индексу скина ----------------------------------------
sk_block = FX[FX.index("public static readonly SkinFxDef[] All ="):]
motion_trail = []
for m in re.finditer(r"Def\(SkinMotion\.(\w+),\s*SkinTrail\.(\w+)", sk_block):
    motion_trail.append((m.group(1), m.group(2)))

# --- стихии -------------------------------------------------------------------
arch = {}
for m in re.finditer(
        r'/\* (\w+)\s*\*/ D\("([\w.]+)", SkinShape\.(\w+), HapticPattern\.(\w+), ([0-9.]+)f,'
        r'\s*"(\w+)", [0-9.]+f, [0-9.]+f,\s*([0-9.]+)f, "([\w.]+)", "([\w.]+)"\)', AR):
    arch[m.group(1)] = {
        "name": names.get(m.group(2), m.group(2)),
        "shape": m.group(3), "haptic": m.group(4), "scale": float(m.group(5)),
        "pitch": float(m.group(7)),
        "sound": names.get(m.group(8), m.group(8)),
        "trail": names.get(m.group(9), m.group(9)),
    }

# --- скины --------------------------------------------------------------------
blk = re.search(r"readonly SkinDef\[\] Bubble =\s*\{(.*?)\n        \};", SK, re.S).group(1)
rows, boss_i, index = [], 0, 0
for line in blk.splitlines():
    if "SkinDef." not in line:
        continue
    key = re.search(r'"([\w.]+)"', line)
    if not key:
        continue

    rar = re.search(r"Rarity\.(\w+)", line)
    rarity = rar.group(1) if rar else "Common"
    mat = re.search(r"SkinMaterial\.(\w+)", line)

    if "Trophy(" in line:
        status, death = boss[boss_i] if boss_i < len(boss) else ("?", "?")
        boss_i += 1
        rarity, material = "Boss", "—"
    else:
        material = mat.group(1) if mat else "Glass"
        status = mat_status.get(material, "?")
        death = mat_death.get(material, "?")

    a = arch.get(status, {})
    motion, trail = motion_trail[index] if index < len(motion_trail) else ("?", "?")
    index += 1

    rows.append({
        "name": names.get(key.group(1), key.group(1)),
        "rarity": rarity, "material": material, "element": a.get("name", status),
        "shape": a.get("shape", "?"), "motion": motion, "trail": trail,
        "sound": a.get("sound", "?"), "pitch": a.get("pitch", 0),
        "haptic": a.get("haptic", "?"), "death": death,
        "scale": a.get("scale", 1.0),
    })

# --- вывод --------------------------------------------------------------------
print("| Скин | Редкость | Материал | Стихия | Форма | Движение | След | Звук | Вибрация | Разрушение |")
print("|---|---|---|---|---|---|---|---|---|---|")
for r in rows:
    print("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
        r["name"], r["rarity"], r["material"], r["element"], r["shape"],
        r["motion"], r["trail"], r["sound"], r["haptic"], r["death"]))

# --- проверки -----------------------------------------------------------------
print("\nвсего скинов: %d" % len(rows))

missing = [r["name"] for r in rows
           if "?" in (r["element"], r["shape"], r["motion"], r["trail"],
                      r["sound"], r["haptic"], r["death"])]
print("с незаполненными каналами: %d%s"
      % (len(missing), (" — " + ", ".join(missing)) if missing else ""))

# Два скина считаем одинаковыми, только если совпало всё: и материал, и стихия,
# и движение, и след. Совпадение одной стихии — не дубль: у них разные начинки.
seen, dupes = {}, []
for r in rows:
    k = (r["material"], r["element"], r["motion"], r["trail"])
    if k in seen:
        dupes.append("%s = %s" % (r["name"], seen[k]))
    seen[k] = r["name"]
print("полных совпадений: %d%s" % (len(dupes), (" — " + "; ".join(dupes)) if dupes else ""))

# Сколько скинов приходится на каждый канал — так видно, где различие слабее.
for field, label in (("shape", "форм"), ("element", "стихий"),
                     ("haptic", "рисунков вибрации"), ("motion", "характеров движения"),
                     ("trail", "видов следа")):
    print("%s: %d" % (label, len({r[field] for r in rows})))

sys.exit(1 if missing or dupes else 0)
