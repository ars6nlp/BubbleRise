# -*- coding: utf-8 -*-
"""
Страж и вид угроз (30.10): правила, которые видно из кода.

Что проверяется:

1. Захват стража держится за касание по id, а не за «главное касание»
   платформы; короткое пропадание касания (до guardianReleaseGrace,
   0.05–0.1 с) стража не отпускает; тот же палец под новым id подхватывается.
2. Нажатие, начатое над кнопкой (луч EventSystem по элементам, которые ловят
   нажатие), стража не хватает; дев-панель мешает только начать захват, а не
   роняет его; ведёт первый схвативший палец — второй и ладонь не сбивают.
3. EditMode-тест захвата есть (Assets/Scripts/Editor/Tests/GuardianGripTests.cs).
4. Отклик стража не медленнее прежнего: 90 % пути за ln10/λ ≤ 77 мс на любом
   пресете (λ ≥ 30), буфера ввода нет.
5. Контраст угроз к воде [РАСЧЁТ, WCAG]: середина тела каждой рыбы (с каждым
   оттенком), блока, звена и шара — к воде каждой ступени глубины, каждой
   локации и «Ледяного разлома» ≥ 3:1; цвет шаров — без белого (сумма ≤ 2.3).
6. Хитбоксы не изменены: размеры из GameConfig и HazardTable и код
   столкновений (без строк частиц) — те же, что в 9269c69
   (Tools/hitbox_ref_9269c69.txt).
7. Текстуры рыб, блоков, звеньев и шаров рисуются на загрузке (PrewarmSteps),
   размер ≥ 128 у мелких и ≥ 256 у крупных; у рыбы один добавочный рендерер
   (хвост), у шаров, блоков и звеньев — ни одного; Light2D нет.
8. Анимации плавные: хвост и щупальца — только поворот и масштаб, без смены
   яркости; шестерёнки ≤ 20 °/с, скорость меняется плавно; щупальца качаются
   циклом ≥ 2 с.

    python3 Tools/check_play.py [--root путь] [--write-ref]
"""
import hashlib
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if "--root" in sys.argv:
    ROOT = sys.argv[sys.argv.index("--root") + 1]
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")

S = os.path.join(ROOT, "Assets", "Scripts")
REF = os.path.join(ROOT, "Tools", "hitbox_ref_9269c69.txt")
fails = 0


def report(name, ok, detail=""):
    global fails
    if not ok:
        fails += 1
    print(("ОК: " if ok else "ПРОВАЛ: ") + name + " | " + (detail or "—"))


def strip(code):
    out, i, n = [], 0, len(code)
    while i < n:
        c = code[i]
        if c == '"':
            j = i + 1
            while j < n and code[j] != '"':
                j += 2 if code[j] == "\\" else 1
            out.append(code[i:j + 1]); i = j + 1
        elif code.startswith("//", i):
            j = code.find("\n", i); i = n if j < 0 else j
        elif code.startswith("/*", i):
            j = code.find("*/", i + 2); i = n if j < 0 else j + 2
        else:
            out.append(c); i += 1
    return "".join(out)


def src(name, raw=False):
    p = os.path.join(S, name)
    if not os.path.exists(p):
        return ""
    t = io.open(p, encoding="utf-8").read()
    return t if raw else strip(t)


def method(code, signature):
    i = code.find(signature)
    if i < 0:
        return ""
    j = code.find("{", i)
    depth = 0
    for k in range(j, len(code)):
        if code[k] == "{":
            depth += 1
        elif code[k] == "}":
            depth -= 1
            if depth == 0:
                return re.sub(r"\s+", " ", code[i:k + 1])
    return ""


def cfgf(name):
    m = re.search(r"public\s+float\s+" + name + r"\s*=\s*(-?[\d.]+)f", CFG)
    return float(m.group(1)) if m else None


def lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def Lrgb(r, g, b):
    return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)


def cr(a, b):
    return (max(a, b) + 0.05) / (min(a, b) + 0.05)


GUARD = src("GuardianOrb.cs")
POINTER = src("PointerInput.cs")
DEV = src("DevPanel.cs")
CFG = src("GameConfig.cs")
DIFF = src("DifficultyConfig.cs")
GM = src("GameManager.cs")
FISH = src("Fish.cs")
ZONE = src("OceanZone.cs")
SF = src("SpriteFactory.cs")
SFF = src("SpriteFactoryFish.cs")
SFC = src("SpriteFactoryCoral.cs")
HAZ = src("HazardTable.cs")
DEPTH = src("DepthLook.cs")
SPAWNER = src("ZoneSpawner.cs")

# ------------------------------------------------------------------ 1. захват
follow = method(GUARD, "bool FollowGrip(")
handle = method(GUARD, "void HandleInput(")
grace = cfgf("guardianReleaseGrace")
report("Захват: страж держится за касание по id, а не за главное касание",
       "PointerInput.ReadContacts" in handle and "_gripId" in follow and "TryRead" not in GUARD
       and "touches" in POINTER and "touchId" in POINTER,
       "ReadContacts + _gripId" if "_gripId" in follow else "захват по primaryTouch / нажатию указателя")
report("Захват: короткое пропадание касания не отпускает стража",
       grace is not None and 0.05 <= grace <= 0.1 and "_lostFor < _cfg.guardianReleaseGrace" in follow
       and "if (!pressed)" not in GUARD,
       "допуск %s с (нужно 0.05–0.1 из конфига)" % grace)
report("Захват: тот же палец под новым id подхватывается со смещением захвата",
       "_prevActiveIds.Contains" in follow and "GripRescues++" in follow and "_grabOffset" not in follow,
       "новое касание рядом с потерянным — продолжение ведения")

# ------------------------------------------------------------------ 2. UI и мультитач
start = method(GUARD, "void TryStartGrip(")
ui = method(GUARD, "bool OverInteractiveUi(")
report("Захват: нажатие над кнопкой стража не хватает (A-12)",
       "OverInteractiveUi(c.Screen)" in start and "RaycastAll" in ui and "IPointerClickHandler" in ui
       and start.find("OverInteractiveUi") < start.find("IsDragging = true"),
       "луч EventSystem до захвата" if ui else "проверки интерфейса нет")
report("Захват: дев-панель мешает только начать захват",
       "DevInputBlocked" not in GUARD and "DevInputBlocked" not in DEV and "DevUiRect.Contains(c.Screen)" in start)
report("Захват: ведёт первый палец, второй и ладонь не сбивают",
       "c.Id != _gripId" in follow and "_prevActiveIds.Contains(c.Id)" in start and "_ignoredIds" in start)
report("Захват: счётчик разрывов и причина — в дев-панели",
       "GripBreaks" in GUARD and "LastGripBreak" in GUARD and "GripBreaks" in DEV and "BreakText(" in DEV)

# ------------------------------------------------------------------ 3. тест
test = io.open(os.path.join(S, "Editor", "Tests", "GuardianGripTests.cs"), encoding="utf-8").read() \
    if os.path.exists(os.path.join(S, "Editor", "Tests", "GuardianGripTests.cs")) else ""
need = ["CanceledForOneFrame_ThenNewId_KeepsGrip", "EndedThenBeganThreeFramesLater_KeepsGrip",
        "PressStartedOverButton_DoesNotGrab", "FirstFingerUp_SecondStays_GuardianGoesHome",
        "HitsAndCooldown_DoNotBreakGrip", "Response90_NotSlowerThanBefore"]
have = [n for n in need if ("void " + n + "(") in test]
report("Захват: EditMode-тест с эмуляцией касаний", len(have) == len(need) and "QueueStateEvent" in test,
       "%d из %d сценариев" % (len(have), len(need)))

# ------------------------------------------------------------------ 4. отклик
lerps = [float(x) for x in re.findall(r"guardianFollowLerp\s*=\s*([\d.]+)f", DIFF)]
base = cfgf("guardianFollowLerp") or 26.0
worst = min([max(base, l) for l in lerps]) if lerps else base
report("Захват: отклик не медленнее прежнего (90 % пути ≤ 77 мс)",
       worst >= 30 and "Mathf.Max(_cfg.guardianFollowLerp, Difficulty.Current.guardianFollowLerp)" in GUARD,
       "λ от %.0f: 90 %% за %.0f мс" % (worst, 2.302585 / worst * 1000))

# ------------------------------------------------------------------ 5. контраст
def hexc(code, name):
    m = re.search(name + r'\s*=\s*Palette\.Hex\("(\w{6})"\)', code)
    return m.group(1) if m else None


def tints(code, name):
    m = re.search(name + r"\s*=\s*\{(.*?)\};", code, re.S)
    if not m:
        return [(1.0, 1.0, 1.0)]
    out = []
    for t in re.findall(r"Color\.white|new Color\(([^)]*)\)", m.group(0)):
        if t == "":
            out.append((1.0, 1.0, 1.0))
        else:
            v = [float(x.strip().rstrip("f")) for x in t.split(",")]
            out.append(tuple(v[:3]))
    return out or [(1.0, 1.0, 1.0)]


waters = {}
for i, m in enumerate(re.finditer(r'Make\("(\w{6})", "(\w{6})", "(\w{6})"', DEPTH)):
    waters["глубина%d верх" % i] = Lrgb(*rgb(m.group(1)))
    waters["глубина%d середина" % i] = Lrgb(*rgb(m.group(2)))
for m in re.finditer(r'NameKey = "zone\.(\w+)",.*?WaterTop = Hex\("(\w{6})"\), WaterBottom = Hex\("(\w{6})"\).*?BlobTint = Hex\("(\w{6})"\)',
                     SPAWNER, re.S):
    waters["локация %s" % m.group(1)] = Lrgb(*rgb(m.group(2)))
zone_tints = {m.group(1): m.group(4) for m in re.finditer(
    r'NameKey = "zone\.(\w+)",.*?WaterTop = Hex\("(\w{6})"\), WaterBottom = Hex\("(\w{6})"\).*?BlobTint = Hex\("(\w{6})"\)', SPAWNER, re.S)}
zone_water = {k: v for k, v in waters.items() if k.startswith("локация")}
WATER_MAX = max(waters.values()) if waters else 0.03


def tinted(h, t):
    r, g, b = rgb(h)
    return Lrgb(min(255, r * t[0]), min(255, g * t[1]), min(255, b * t[2]))


items = []
pm, lm = hexc(SFF, "FishPiranhaMid"), hexc(SFF, "FishLurkerMid")
if pm and lm:
    for t in tints(SFF, "PiranhaTints"):
        items.append(("пиранья", tinted(pm, t)))
    for t in tints(SFF, "LurkerTints"):
        items.append(("луркер", tinted(lm, t)))
    jb = re.search(r'JellyBell\s*=\s*Hex\("(\w{6})"\)', src("Palette.cs"))
    alpha = re.search(r"c\.a = Mathf\.Lerp\(([\d.]+)f, [\d.]+f, Mathf\.SmoothStep\(0\.6f, 1f, d\)\)", SFF)
    if jb and alpha:
        a = float(alpha.group(1))
        for t in tints(SFF, "JellyTints"):
            items.append(("медуза (α %.2f)" % a, a * tinted(jb.group(1), t) + (1 - a) * WATER_MAX))
bm, lim = hexc(SFC, "CoralBlockMid"), hexc(SFC, "CoralLimbMid")
if bm and lim:
    tile_min = re.search(r"float tint = Random\.Range\(([\d.]+)f", src("CoralFormation.cs"))
    k = float(tile_min.group(1)) if tile_min else 1.0
    items.append(("блок (тон ×%.2f)" % k, tinted(bm, (k, k * 0.98, k * 0.96))))
    items.append(("звено", tinted(lim, (1.0, 1.0, 1.0))))
metal = hexc(SFC, "BlobMetal")
blob_rows = []
if metal:
    for zname, tint in zone_tints.items():
        t = tuple(c / 255.0 for c in rgb(tint))
        blob_rows.append((zname, tinted(metal, t) * 0.88, zone_water.get("локация " + zname, WATER_MAX)))
    ice = re.search(r'IceTheme\s*=\s*new ZoneTheme\s*\{(.*?)\};', SPAWNER, re.S)
    if ice:
        iw = re.search(r'WaterTop = Hex\("(\w{6})"\)', ice.group(1))
        ib = re.search(r'BlobTint = Hex\("(\w{6})"\)', ice.group(1))
        if iw and ib:
            waters["лёд"] = Lrgb(*rgb(iw.group(1)))
            blob_rows.append(("ice", tinted(metal, tuple(c / 255.0 for c in rgb(ib.group(1)))) * 0.88, waters["лёд"]))
WATER_MAX = max(waters.values()) if waters else 0.03

if items and blob_rows:
    worst_item = min(items, key=lambda it: it[1])
    worst_blob = min(blob_rows, key=lambda b: cr(b[1], b[2]))
    ok = cr(worst_item[1], WATER_MAX) >= 3.0 and cr(worst_blob[1], worst_blob[2]) >= 3.0
    report("Контраст: рыбы, блоки, звенья и шары к воде обеих локаций ≥ 3:1", ok,
           "худшая угроза — %s %.2f:1 к самой светлой воде (L %.4f); худший шар — %s %.2f:1" % (
               worst_item[0], cr(worst_item[1], WATER_MAX), WATER_MAX, worst_blob[0],
               cr(worst_blob[1], worst_blob[2])))
else:
    report("Контраст: рыбы, блоки, звенья и шары к воде обеих локаций ≥ 3:1", False,
           "нет именованной палитры рыб, блоков и шаров (FishPiranhaMid, CoralBlockMid, BlobMetal)")
report("Контраст: шары без белого (сумма каналов ≤ 2.3)",
       bool(metal) and sum(rgb(metal)) / 255.0 <= 2.3,
       "BlobMetal %s, сумма %.2f" % (metal, sum(rgb(metal)) / 255.0) if metal else "шары почти белые (0.949, 0.969, 0.953)")

# ------------------------------------------------------------------ 6. хитбоксы
SIZES = ["fishRadius", "jellyfishRadius", "formationCellSize", "formationBlockRadius", "spireSegmentSize",
         "spireSegmentSpacing", "zoneBlobMinRadius", "zoneBlobMaxRadius", "guardianRadius", "bubbleRadius"]


def hitbox_snapshot():
    out = ["%s=%s" % (n, cfgf(n)) for n in SIZES]
    for m in re.finditer(r"Type = ObstacleType\.(\w+),.*?Scale = ([\d.]+)f", HAZ, re.S):
        out.append("scale.%s=%s" % (m.group(1), m.group(2)))
    for sig in ("bool HitsBubble(", "void TickFish(", "void TickZone(", "void TickSpires(", "void TickFormations(",
                "void TickObstacles("):
        body = method(GM, sig)
        body = re.sub(r"_fx\.Burst\([^;]*;", "", body)   # цвет частиц — не хитбокс
        body = re.sub(r"\s*CountBlocked\(UnlockKind\.\w+\);", "", body)   # счёт отбитого для скинов — не хитбокс
        out.append("%s%s" % (sig, hashlib.sha1(body.encode("utf-8")).hexdigest()[:16]))
    fish_act = method(FISH, "public void Activate(")
    out.append("fish.radius=%s" % ("Radius = radius;" in fish_act))
    zone_add = method(ZONE, "public void AddBlob(")
    out.append("zone.blob=%s" % hashlib.sha1(zone_add.encode("utf-8")).hexdigest()[:16])
    return out


snap = hitbox_snapshot()
if "--write-ref" in sys.argv:
    io.open(REF, "w", encoding="utf-8").write("\n".join(snap) + "\n")
    print("записан " + REF)
if os.path.exists(REF):
    ref = io.open(REF, encoding="utf-8").read().splitlines()
    diff = [b for a, b in zip(ref, snap) if a != b] + ([] if len(ref) == len(snap) else ["число строк"])
    report("Хитбоксы: размеры и код столкновений — как в 9269c69", not diff,
           "совпало %d из %d" % (len(snap) - len(diff), len(snap)) if not diff else "расходится: " + "; ".join(diff[:4]))
else:
    report("Хитбоксы: размеры и код столкновений — как в 9269c69", False, "нет Tools/hitbox_ref_9269c69.txt")

# ------------------------------------------------------------------ 7. текстуры и бюджет
pre = method(SF, "IEnumerable<float> PrewarmSteps(")
sizes = {n: int(v) for n, v in re.findall(r"const int (FishSize|CoralSize|LimbSize|BlobSize) = (\d+);", SFF + SFC)}
report("Текстуры: рыбы, блоки, звенья и шары рисуются на загрузке",
       all(x in pre for x in ("FishBody(f)", "FishTail(f)", "CoralTile(i)", "CoralLimb(i)", "CoralBlob(0)", "CoralBlob(1)")))
report("Текстуры: ≥ 128 у мелких, ≥ 256 у крупных, Bilinear без mip",
       sizes.get("FishSize", 0) >= 128 and sizes.get("CoralSize", 0) >= 128 and sizes.get("LimbSize", 0) >= 256
       and sizes.get("BlobSize", 0) >= 256 and "FilterMode.Bilinear" in method(SFF, "static Sprite BuildPivot(")
       and "TextureFormat.RGBA32, false" in method(SFF, "static Sprite BuildPivot("),
       ", ".join("%s %d" % kv for kv in sorted(sizes.items())))
setup = method(FISH, "public void Setup(")
report("Бюджет: у рыбы один добавочный рендерер, у шаров, блоков и звеньев — ни одного",
       setup.count("AddComponent<SpriteRenderer>") == 2 and "AddComponent<SpriteRenderer>" not in method(ZONE, "public void AddBlob(")
       and "Light2D" not in (FISH + ZONE + SFF + SFC),
       "рыба: %d рендерера" % setup.count("AddComponent<SpriteRenderer>"))

# ------------------------------------------------------------------ 8. анимации
tick = method(FISH, "public bool Tick(")
spin = re.search(r"float spin = \(([\d.]+)f \+ \(i \* 37 % (\d+)\)\)", ZONE)
spin_max = (float(spin.group(1)) + int(spin.group(2)) - 1) * 1.3 if spin else 0
sway = re.search(r"_tail\.transform\.localRotation = Quaternion\.Euler\(0f, 0f, Mathf\.Sin\(_phase \* ([\d.]+)f \+ _wanderSeed\)", tick)
report("Анимации: хвост и щупальца без смены яркости, шестерёнки ≤ 20 °/с, щупальца — цикл ≥ 2 с",
       "_tail.transform.localRotation" in tick and "_tail.color" not in tick and spin is not None and spin_max <= 20
       and sway is not None and 2 * 3.14159 / float(sway.group(1)) >= 2.0,
       "шестерёнки до %.0f °/с; щупальца — %.1f с" % (spin_max, 2 * 3.14159 / float(sway.group(1)) if sway else 0))

sys.exit(1 if fails else 0)
