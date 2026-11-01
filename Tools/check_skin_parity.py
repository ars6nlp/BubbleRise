# -*- coding: utf-8 -*-
"""
Сверка скинов: что задано в данных и что из этого читает игра.

Повторяет SkinVisuals.Resolve разбором кода и проверяет три вещи:

1. У каждого скина заполнены все поля: форма, материал, движение, след, аура,
   звук, вибрация, удар, разрушение, статус.
2. Нет двух скинов с одинаковым набором признаков.
3. Каждое поле SkinLook реально читается в игровом пути, а не только в витрине.

Третья проверка — ровно та, из-за отсутствия которой материал стража молча
пропадал в бою: поле в данных было, витрина его показывала, а игра не читала.

    python3 Tools/check_skin_parity.py
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
RUNTIME = read("Assets/Scripts/SkinFxRuntime.cs")
VISUALS = read("Assets/Scripts/SkinVisuals.cs")
GM = read("Assets/Scripts/GameManager.cs")
UI = read("Assets/Scripts/UIManager.cs")
BUBBLE = read("Assets/Scripts/BubbleController.cs")
GUARD = read("Assets/Scripts/GuardianOrb.cs")

names = {}
for m in re.finditer(r'\{\s*"([\w.]+)",\s*new\[\]\s*\{\s*"([^"]*)"', LOC):
    names[m.group(1)] = m.group(2)

# --- материал -> аура, удар, статус, разрушение -------------------------------
fx_block = FX[FX.index("public static readonly MaterialFxDef[] All ="):FX.index("Эффекты трофейных скинов")]
MAT = {}
for name, args in re.findall(r"/\* (\w+)\s*\*/ D\((.*?)\),\n", fx_block, re.S):
    MAT[name] = {
        "aura": re.search(r"AuraStyle\.(\w+)", args).group(1),
        "impact": re.search(r"ImpactPattern\.(\w+)", args).group(1),
        "status": re.search(r"StatusKind\.(\w+)", args).group(1),
        "destroy": re.search(r"DestroyPattern\.(\w+)", args).group(1),
    }

boss_block = FX[FX.index("public static readonly MaterialFxDef[] Bosses ="):]
BOSS = []
for name, args in re.findall(r"/\* (\w+)\s*\*/ D\((.*?)\),\n", boss_block, re.S):
    BOSS.append({
        "aura": re.search(r"AuraStyle\.(\w+)", args).group(1),
        "impact": re.search(r"ImpactPattern\.(\w+)", args).group(1),
        "status": re.search(r"StatusKind\.(\w+)", args).group(1),
        "destroy": re.search(r"DestroyPattern\.(\w+)", args).group(1),
    })

# --- движение и след по номеру скина ------------------------------------------
sk_block = FX[FX.index("public static readonly SkinFxDef[] All ="):]
MOTION = [(m.group(1), m.group(2))
          for m in re.finditer(r"Def\(SkinMotion\.(\w+),\s*SkinTrail\.(\w+)", sk_block)]

# --- стихии -------------------------------------------------------------------
ARCH = {}
for m in re.finditer(
        r'/\* (\w+)\s*\*/ D\("([\w.]+)", SkinShape\.(\w+), HapticPattern\.(\w+), ([0-9.]+)f,'
        # тон, высота звука, звук, след. Тряски камеры в таблице больше нет.
        r'\s*"\w+", [0-9.]+f, "([\w.]+)", "([\w.]+)"\)', AR):
    ARCH[m.group(1)] = {
        "name": names.get(m.group(2), m.group(2)),
        "shape": m.group(3), "haptic": m.group(4), "scale": float(m.group(5)),
        "sound": names.get(m.group(6), m.group(6)),
        "trail": names.get(m.group(7), m.group(7)),
    }

# --- каталог ------------------------------------------------------------------
rows, boss_i, index = [], 0, 0
blk = re.search(r"readonly SkinDef\[\] Bubble =\s*\{(.*?)\n        \};", SK, re.S).group(1)
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
        fx = BOSS[boss_i] if boss_i < len(BOSS) else {}
        boss_i += 1
        rarity, material = "Boss", "—"
    else:
        material = mat.group(1) if mat else "Glass"
        fx = MAT.get(material, {})

    a = ARCH.get(fx.get("status", "?"), {})
    motion, trail = MOTION[index] if index < len(MOTION) else ("?", "?")
    index += 1

    rows.append({
        "name": names.get(key.group(1), key.group(1)), "rarity": rarity,
        "material": material, "element": a.get("name", fx.get("status", "?")),
        "shape": a.get("shape", "?"), "motion": motion, "trail": trail,
        "aura": fx.get("aura", "?"), "impact": fx.get("impact", "?"),
        "destroy": fx.get("destroy", "?"), "status": fx.get("status", "?"),
        "sound": a.get("sound", "?"), "haptic": a.get("haptic", "?"),
        "scale": a.get("scale", 0.0),
    })

# --- проверка 1: все поля заполнены -------------------------------------------
FIELDS = ("element", "shape", "motion", "trail", "aura", "impact", "destroy",
          "status", "sound", "haptic")
empty = [(r["name"], f) for r in rows for f in FIELDS if r[f] in ("?", "", None)]
empty += [(r["name"], "scale") for r in rows if r["scale"] <= 0]

# --- проверка 2: нет двух одинаковых ------------------------------------------
seen, dupes = {}, []
for r in rows:
    k = (r["material"], r["motion"], r["trail"], r["status"])
    if k in seen:
        dupes.append("%s = %s" % (r["name"], seen[k]))
    seen[k] = r["name"]

# --- проверка 3: каждое поле читается игровым путём ---------------------------
# Пузырёк и страж берут вид через SkinVisuals, а дальше поля читаются в
# SkinFxRuntime и контроллерах. Проверяем именно чтение, а не наличие.
READS = [
    ("форма", "Shell", "look.Shell" in VISUALS or "bubble.Shell" in GM, GM, "bubble.Shell"),
    ("материал", "Core", True, GM, "bubble.Core"),
    ("оттенок", "Tint", True, GM, "bubble.Tint"),
    ("движение", "Fx.Motion", True, RUNTIME, "MotionScale(look.Fx"),
    ("след", "Fx.Trail", True, RUNTIME, "fx.Trail"),
    ("аура", "Material.Aura", True, RUNTIME, "_bubbleLook.Material.Aura"),
    ("аура стража", "Material.Aura", True, RUNTIME, "_guardianLook.Material.Aura"),
    ("удар", "Material.Impact", True, RUNTIME, "_guardianLook.Material.Impact"),
    ("статус", "Material.Status", True, RUNTIME, "_guardianLook.Material.Status"),
    ("разрушение", "Material.Pattern", True, RUNTIME, "PlayDestroy(_bubbleLook.Material"),
    ("размер стихии", "VisualScale", True, RUNTIME, "look.VisualScale"),
    ("звук", "Archetype.SoundPitch", True, GM, "arch.SoundPitch"),
    ("вибрация", "Archetype.Haptic", True, GM, "arch.Haptic"),
]
unread = [label for label, _f, _ok, src, needle in READS if needle not in src]

# --- вывод --------------------------------------------------------------------
print("| Скин | Редкость | Материал | Стихия | Форма | Движение | След | Аура | Удар | Разрушение | Статус | Звук | Вибро |")
print("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
for r in rows:
    print("| %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s | %s |" % (
        r["name"], r["rarity"], r["material"], r["element"], r["shape"], r["motion"],
        r["trail"], r["aura"], r["impact"], r["destroy"], r["status"],
        r["sound"], r["haptic"]))

print("\nскинов: %d" % len(rows))
print("с незаполненными полями: %d%s"
      % (len(empty), ("  " + ", ".join("%s/%s" % e for e in empty[:5])) if empty else ""))
print("полных совпадений: %d%s"
      % (len(dupes), ("  " + "; ".join(dupes)) if dupes else ""))
print("полей, не читаемых игрой: %d%s"
      % (len(unread), ("  " + ", ".join(unread)) if unread else ""))

print("\nразнообразие: форм %d, стихий %d, движений %d, следов %d, аур %d, ударов %d"
      % tuple(len({r[f] for r in rows})
              for f in ("shape", "element", "motion", "trail", "aura", "impact")))

assert not empty, "есть скины с незаполненными полями"
assert not dupes, "есть скины с одинаковым набором признаков"
assert not unread, "есть поля, которые игра не читает"
print("\nвсе проверки прошли")
sys.exit(0)
