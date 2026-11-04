# -*- coding: utf-8 -*-
"""
Подводный мир: фон, локации, лёд — правила, которые видно из кода.

Что проверяется:

1. Параллакс: не меньше четырёх слоёв с разными множителями, по возрастанию;
   один материал (атлас) на все слои мира, у льда и у новых локаций — свой;
   атласы рисуются один раз (кэш).
2. Бюджет: ни одного Light2D и bloom в коде и настройках URP.
3. Детали фона: настройка «Высокие / Обычные» есть (по умолчанию «Высокие»),
   у неё есть кнопка и строки RU/EN, и слои её действительно читают.
4. Правила для глаз в фоне: дыхание лучей не быстрее 6 с и не больше 10 %,
   течение не быстрее 6 с, снег появляется и уходит за 0.15–0.6 с, каустики
   медленные, светящиеся точки локаций дышат не быстрее 9 с и не больше 10 %,
   вода локации меняется не быстрее 1.5 с.
5. Цвет вершин переводится в линейное пространство (проект в Linear): и у
   слоёв, и у стен локаций.
6. Контраст [РАСЧЁТ]: вода каждой ступени глубины и каждой локации к коралловой
   угрозе ≥ 3:1, к самой тёмной угрозе (ёж) ≥ 2.2:1; светлые цвета фона и
   льда — сумма каналов ≤ 2.3.
7. Угрозы локаций не изменились: FillContent, BuildPattern, PickOffset и
   AddBlob — тот же код, что в 4957e57 (сравнение без комментариев);
   порядок вызовов UnityEngine.Random в Spawn и AddWallDecor тот же;
   оформление берёт случайность только из System.Random.
8. Лёд: вторая локация есть, со своей фактурой стен, своим атласом и
   слоем параллакса, звуком, кнопкой в панели разработчика; угрозы — те же
   композиции. Виды локаций (ZoneCatalog) идут по очереди ZoneRotation:
   никогда два раза подряд, за круг — все; очередь на System.Random. Поведение
   проверяют EditMode-тесты Editor/Tests/ZoneTests.cs, здесь — только устройство.

Числа читаются из кода, комментарии вырезаются. Каждая строка итога
начинается с «ОК: » или «ПРОВАЛ: ».

    python3 Tools/check_world.py [--root путь_к_проекту]
"""
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
fails = 0


def report(name, ok, detail=""):
    global fails
    if not ok:
        fails += 1
    print(("ОК: " if ok else "ПРОВАЛ: ") + name + " | " + (detail or "—"))


def src(name):
    p = os.path.join(S, name)
    if not os.path.exists(p):
        return ""
    return strip(io.open(p, encoding="utf-8").read())


def strip(code):
    """Без комментариев; строковые литералы целы."""
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


def num(code, pattern, default=None):
    m = re.search(pattern, code)
    return float(m.group(1)) if m else default


def lin(c):
    c /= 255.0
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def L(h):
    r, g, b = (int(h[i:i + 2], 16) for i in (0, 2, 4))
    return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)


def cr(a, b):
    return (max(a, b) + 0.05) / (min(a, b) + 0.05)


def csum(h):
    return sum(int(h[i:i + 2], 16) for i in (0, 2, 4)) / 255.0


def method(code, signature):
    """Тело метода по началу сигнатуры: от первой { до парной }."""
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


layers = src("WorldLayers.cs")
light = src("OceanLight.cs")
snow = src("MarineSnow.cs")
current = src("OceanCurrent.cs")
batch = src("QuadBatch.cs")
world = src("SpriteFactoryWorld.cs")
walls = src("SpriteFactoryWalls.cs")
visuals = src("Visuals.cs")
ui = src("UIManager.cs")
loc = src("Loc.cs")
bg = src("BackgroundController.cs")
zone = src("OceanZone.cs")
spawner = src("ZoneSpawner.cs")
catalog = src("ZoneCatalog.cs")
rotation = src("ZoneRotation.cs")
dressing = src("ZoneDressing.cs")
shaped = src("ZoneDressingShaped.cs")
zones_art = src("SpriteFactoryZones.cs")
depth = src("DepthLook.cs")
audio = src("AudioManager.cs")
gm = src("GameManager.cs")
dev = src("DevPanel.cs")

# 1. Параллакс
m = re.search(r"Parallax\s*=\s*\{([^}]*)\}", layers)
par = [float(x.strip().rstrip("f")) for x in m.group(1).split(",")] if m else []
report("Параллакс: ≥ 4 слоя с разными множителями по возрастанию",
       len(par) >= 4 and all(a < b for a, b in zip(par, par[1:])), "множители %s" % par)
makes = re.findall(r"Make\(Kind\.(\w+), root, \"\w+\", ([\w.]+),", layers)
world_mats = set(m for k, m in makes if k not in ("Ice", "Zone"))
ice_mats = set(m for k, m in makes if k == "Ice")
zone_mats = set(m for k, m in makes if k == "Zone")
report("Параллакс: один материал на слой — атлас мира, у льда и новых локаций свой атлас",
       len(makes) >= 4 and len(world_mats) == 1 and ice_mats <= {"SpriteFactory.IceMaterial"}
       and zone_mats == {"SpriteFactory.ZonesMaterial"},
       "%d слоёв, материалы %s" % (len(makes), sorted(world_mats | ice_mats | zone_mats)))
report("Атласы мира рисуются один раз (кэш)",
       "if (_worldAtlas != null) return;" in world and "if (_iceAtlas != null) return;" in world
       and "if (_zonesAtlas != null) return;" in zones_art
       and "if (_wallMats[i] != null) return _wallMats[i];" in walls)

# 2. Бюджет
# Bloom лежит в профиле по умолчанию из шаблона URP, но рисуется, только если у
# камеры включена постобработка; у камеры сцены она выключена, и код её не включает.
light2d = [f for f in os.listdir(S) if f.endswith(".cs") and "Light2D" in src(f)]
scene = io.open(os.path.join(ROOT, "Assets", "Scenes", "Main.unity"), encoding="utf-8", errors="replace").read() \
    if os.path.exists(os.path.join(ROOT, "Assets", "Scenes", "Main.unity")) else ""
post_on = re.findall(r"m_RenderPostProcessing: 1", scene)
post_code = [f for f in os.listdir(S) if f.endswith(".cs") and "renderPostProcessing = true" in src(f)]
report("Бюджет: нет Light2D, постобработка (bloom) у камеры выключена",
       not light2d and scene != "" and not post_on and not post_code,
       "Light2D в %s" % light2d if light2d else "постобработка в коде: %s" % post_code if post_code else "")

# 3. Детали фона
report("Детали фона: настройка с ключом, по умолчанию «Высокие»",
       re.search(r"PlayerPrefs\.GetInt\(BgDetailKey,\s*1\)", visuals) is not None)
report("Детали фона: кнопка в настройках и строки RU/EN",
       "BgDetailButton" in ui and loc.count('"settings.bgHigh"') >= 1 and loc.count('"settings.bgNormal"') >= 1)
readers = [n for n, c in (("WorldLayers", layers), ("OceanLight", light), ("MarineSnow", snow), ("BackgroundController", bg))
           if "highDetail" in c or "HighDetail" in c]
report("Детали фона: слои читают настройку", "BackgroundController" in readers and len(readers) >= 3,
       ", ".join(readers))

# 4. Глаза
amp = num(light, r"BreathAmplitude\s*=\s*([\d.]+)f")
per = num(light, r"BreathPeriodMin\s*=\s*([\d.]+)f")
report("Глаза: лучи дышат не быстрее 6 с и не больше 10 %",
       amp is not None and per is not None and amp <= 0.10 and per >= 6, "амплитуда %s, период от %s с" % (amp, per))
cur = num(current, r"Period\s*=\s*([\d.]+)f")
report("Глаза: течение не быстрее 6 с", cur is not None and cur >= 6, "период %s с" % cur)
fade = num(snow, r"Fade\s*=\s*([\d.]+)f")
report("Глаза: снег появляется и уходит за 0.15–0.6 с", fade is not None and 0.15 <= fade <= 0.6, "%s с" % fade)
cs = num(light, r"CausticSpeed\s*=\s*([\d.]+)f")
report("Глаза: каустики медленные (≤ 0.02 доли текстуры в секунду)", cs is not None and cs <= 0.02, "%s" % cs)
tf = num(bg, r"ThemeFade\s*=\s*([\d.]+)f")
report("Глаза: вода локации меняется не быстрее 1.5 с", tf is not None and tf >= 1.5 and "dt / ThemeFade" in bg,
       "%s с" % tf)
gp = num(zone, r"float period\s*=\s*([\d.]+)f")
gb = re.findall(r"Breath\s*=\s*([\d.]+)f", dressing)
report("Глаза: светящиеся точки дышат не быстрее 9 с и не больше 10 %",
       gp is not None and gp >= 9 and gb and max(float(x) for x in gb) <= 0.10,
       "период от %s с, размах %s" % (gp, gb))

# 5. Линейный цвет вершин
report("Цвет вершин слоёв — в линейном пространстве", "c.linear" in batch and "ColorSpace.Linear" in batch)
report("Цвет вершин стен локаций — в линейном пространстве",
       "QuadBatch.ToVertex(theme.Wall" in zone and "colors32" in zone)

# 6. Контраст
CORAL, URCHIN = L("B5573F"), L("7A5A8A")
steps = re.findall(r'Make\("(\w{6})", "(\w{6})", "(\w{6})", "(\w{6})", "(\w{6})"', depth)
worst = min((min(cr(CORAL, L(c)) for c in s[:3]), s[0]) for s in steps) if steps else (0, "")
worstU = min((min(cr(URCHIN, L(c)) for c in s[:3]), s[0]) for s in steps) if steps else (0, "")
report("Контраст: вода ступеней глубины к кораллу ≥ 3:1, к ежу ≥ 2.2:1",
       len(steps) >= 5 and worst[0] >= 3 and worstU[0] >= 2.2,
       "%d ступеней; худшее %.1f:1 и %.1f:1" % (len(steps), worst[0], worstU[0]))
fogs = [(s[4], L(s[4])) for s in steps]
report("Контраст: туман ступеней не светлее L 0.02 (дальний план тонет в воде, а не светлеет)",
       len(fogs) >= 5 and all(l <= 0.02 for _, l in fogs), ", ".join("%s %.3f" % f for f in fogs))
themes = re.findall(r'NameKey = "(zone\.\w+)",\s*WaterTop = Hex\("(\w{6})"\), WaterBottom = Hex\("(\w{6})"\)',
                    spawner + catalog)
bad = ["%s %.1f/%.1f" % (k, cr(CORAL, L(t)), cr(URCHIN, L(t))) for k, t, b in themes
       if cr(CORAL, L(t)) < 3 or cr(URCHIN, L(t)) < 2.2]
report("Контраст: вода локаций к кораллу ≥ 3:1, к ежу ≥ 2.2:1", len(themes) >= 9 and not bad,
       "%d тем%s" % (len(themes), "; мимо: " + ", ".join(bad) if bad else ""))
light_hex = re.findall(r'Hex\("(\w{6})"\)', world + walls + dressing + zones_art + shaped)
over = sorted(set(h for h in light_hex if csum(h) > 2.3))
report("Цвета мира без белого пересвета (сумма каналов ≤ 2.3)", not over and len(light_hex) > 20,
       "%d цветов%s" % (len(set(light_hex)), "; мимо: " + ", ".join(over) if over else ""))

# 7. Угрозы локаций не изменились
OLD_THREATS = os.path.join(ROOT, "Tools", "zone_threats_4957e57.txt")
if os.path.exists(OLD_THREATS):
    # после checkout с autocrlf эталон может прийти с CRLF — разбор от этого не зависит
    old = io.open(OLD_THREATS, encoding="utf-8").read().replace("\r\n", "\n").split("\n=====\n")
    now = [method(spawner, "void FillContent("), method(spawner, "List<Vector3> BuildPattern("),
           method(spawner, "float PickOffset("), method(zone, "public void AddBlob(")]
    same = [a.strip() == b.strip() for a, b in zip(old, now)]
    report("Угрозы локаций: шары ставит тот же код, что в 4957e57", len(old) == 4 and all(same),
           "совпало %d из 4" % sum(same))
else:
    report("Угрозы локаций: шары ставит тот же код, что в 4957e57", False, "нет Tools/zone_threats_4957e57.txt")
deco = method(spawner, "void AddWallDecor(")
calls = re.findall(r"UnityEngine\.Random\.(Range|value)\s*(\([^)]*\))?", deco)
seq = ["%s%s" % (a, b.replace(" ", "")) for a, b in calls]
report("Угрозы локаций: AddWallDecor тянет Random в прежнем порядке",
       seq == ["Range(0.03f,0.97f)", "value", "Range(2.4f,3.6f)", "Range(1.8f,2.8f)", "Range(-0.15f,0.35f)"], " ".join(seq))
report("Оформление локаций не трогает UnityEngine.Random",
       dressing != "" and shaped != "" and all("UnityEngine.Random" not in d and "Random." not in d.replace("System.Random", "")
                                               for d in (dressing, shaped)))
spawn = method(spawner, "public OceanZone Spawn(")
order = re.findall(r"UnityEngine\.Random\.Range\((0, Themes\.Length|0, Profiles\.Length|0, 10000)\)", spawn)
report("Угрозы локаций: Spawn тянет тему, профиль и зерно в прежнем порядке",
       order[:3] == ["0, Themes.Length", "0, Profiles.Length", "0, 10000"], " → ".join(order))

# 8. Лёд
report("Лёд: своя фактура стен и свой атлас", "WallStyle.Ice" in spawner and "IceMaterial" in zone)
report("Лёд: свой слой параллакса", "IceMaterial" in layers and "LayerIce" in layers)
report("Лёд: свой звук", "_iceSource" in audio and re.search(r"SetWorldMix\([^;]*IsIce", gm) is not None)
report("Лёд: кнопка в панели разработчика", "DevSpawnIceZone" in dev and "DevSpawnIceZone" in gm)
ice_kit = set(re.findall(r"IcePiece\.(\w+)", dressing))
ice_base = sorted(set(re.sub(r"[A-C]$", "", n) for n in ice_kit))
report("Лёд: 8–15 элементов декора с вариантами", 8 <= len(ice_base) <= 15,
       "%d элементов (%d рисунков): %s" % (len(ice_base), len(ice_kit), ", ".join(ice_base)))
kinds = re.findall(r"new ZoneDef\s*\{\s*Kind = ZoneKind\.(\w+)", catalog)

# Новые локации: у каждой 8–15 элементов декора с вариантами (рисунки атласа по
# префиксу вида: W — корабль, R — рёбра, G — сад), свой слой параллакса, своя
# фактура стен (у сада стен нет), кнопка в панели разработчика, плавный свет.
art = re.search(r"enum ZonePiece\s*\{(.*?)Count", zones_art, re.S)
pieces = re.findall(r"\b([WRG][A-Z]\w+)", art.group(1)) if art else []
for prefix, name in (("W", "корабль"), ("R", "рёбра"), ("G", "сад")):
    base = sorted(set(re.sub(r"[A-C]$", "", n) for n in pieces if n[0] == prefix))
    report("Новая локация «%s»: 8–15 элементов декора с вариантами" % name, 8 <= len(base) <= 15,
           "%d элементов (%d рисунков): %s" % (len(base), sum(1 for n in pieces if n[0] == prefix), ", ".join(base)))
report("Новые локации: свой слой параллакса, свой атлас, свет локации плавно (ThemeFade)",
       "Kind.Zone" in layers and "ZoneKit" in layers and "SetZoneKind(" in bg and "_zoneMix" in bg
       and "dt / ThemeFade" in bg and "SetZoneKind(" in gm)
report("Новые локации: фактуры стен дерево и кость",
       "WallStyle.Wood" in catalog and "WallStyle.Bone" in catalog and "WoodTex(" in walls and "BoneTex(" in walls)
report("Новые локации: кнопки в панели разработчика",
       all('DevSpawnZone(ZoneKind.%s)' % k in dev for k in ("Wreck", "Ribs", "Garden")))
pick = method(rotation, "int PickFromCircle(")
report("Виды локаций: не меньше пяти, очередь без двух подряд",
       len(set(kinds)) >= 5 and "_rotation.Next()" in method(spawner, "public OceanZone Spawn(")
       and "IsLast(i)" in pick and "_rotation.Played(" in spawner,
       "%d видов: %s" % (len(set(kinds)), ", ".join(kinds)))
report("Виды локаций: случайность очереди — не UnityEngine.Random",
       rotation != "" and "UnityEngine.Random" not in rotation and "new System.Random(seed)" in rotation
       and "System.Random" in spawner)
ice_theme = re.search(r'IceTheme\s*=\s*new ZoneTheme\s*\{(.*?)\};', spawner, re.S)
it = ice_theme.group(1) if ice_theme else ""
iw = re.search(r'WaterTop = Hex\("(\w{6})"\)', it)
ic = re.search(r'Crust = Hex\("(\w{6})"\)', it)
ib = re.search(r'BlobTint = Hex\("(\w{6})"\)', it)
ok = bool(iw and ic and ib)
# Шар: прежде — почти белый спрайт (сумма каналов 2.86), в среднем темнее тона на
# четверть [ТЕСТ, этап 5], и к корке он давал 3:1. По решению владельца 30.10 шары
# без белого — лавандово-серый BlobMetal, сумма ≤ 2.3 (Tools/check_play.py). Самый
# светлый такой серый даёт к светлой корке льда (L 0.18) не больше ≈2.3:1, поэтому
# порог «шар к корке» — 2:1, а главный — шар к воде ≥ 3:1. Вынесено в отчёт как
# вопрос владельцу (docs/PLAY_REPORT.md).
metal = re.search(r'BlobMetal\s*=\s*Palette\.Hex\("(\w{6})"\)', src("SpriteFactoryCoral.cs"))
blob_crust_min = 2.0 if metal else 3.0
if ok:
    if metal:
        m, t = metal.group(1), ib.group(1)
        r, g, b = (int(m[i:i + 2], 16) * int(t[i:i + 2], 16) / 255.0 for i in (0, 2, 4))
        blob = (0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b)) * 0.88   # к краю шар темнее
    else:
        blob = L(ib.group(1)) * 0.75
    det = "коралл/вода %.1f, ёж/вода %.1f, шар/вода %.1f, шар/корка %.1f (порог %.0f)" % (
        cr(CORAL, L(iw.group(1))), cr(URCHIN, L(iw.group(1))), cr(blob, L(iw.group(1))),
        cr(blob, L(ic.group(1))), blob_crust_min)
    ok = cr(CORAL, L(iw.group(1))) >= 3 and cr(URCHIN, L(iw.group(1))) >= 2.2 \
        and cr(blob, L(iw.group(1))) >= 3 and cr(blob, L(ic.group(1))) >= blob_crust_min
else:
    det = "нет IceTheme"
report("Лёд: контраст угроз к воде ≥ 3:1 (ёж ≥ 2.2:1), шара к корке ≥ %.0f:1" % blob_crust_min, ok, det)

# 9. Запечённые атласы, загрузка, смена экранов, свет угроз (решения владельца 30.10)
baked_src = src("SpriteFactoryBaked.cs")
gen = re.search(r"GeneratorSources\s*=\s*\{([^}]*)\}", baked_src)
gen_files = re.findall(r'"([\w.]+)"', gen.group(1)) if gen else []


def source_hash(files):
    """Тот же расчёт, что SpriteFactory.SourceHash: имя и текст файла, переводы строк — \\n."""
    import hashlib
    buf = []
    for f in files:
        text = io.open(os.path.join(S, f), encoding="utf-8-sig", newline="").read().replace("\r\n", "\n")
        buf.append(f + "\n" + text + "\n")
    return hashlib.sha256("".join(buf).encode("utf-8")).hexdigest()[:16]


BAKED = os.path.join(ROOT, "Assets", "Resources", "BakedWorld")
meta_path = os.path.join(BAKED, "meta.txt")
if gen_files and os.path.exists(meta_path):
    meta = io.open(meta_path, encoding="utf-8").read().splitlines()
    version = next((l.split(" ", 1)[1].strip() for l in meta if l.startswith("version ")), "")
    images = [l.split()[1] for l in meta if l.startswith("image ")]
    missing = [n for n in images if not os.path.exists(os.path.join(BAKED, n + ".png.bytes"))]
    now = source_hash(gen_files)
    report("Атласы мира запечены и совпадают с генератором",
           version == now and len(images) >= 7 and not missing,
           "версия %s, генератор %s, картинок %d%s" % (version, now, len(images),
                                                       "; нет: " + ", ".join(missing) if missing else ""))
else:
    report("Атласы мира запечены и совпадают с генератором", False,
           "нет Assets/Resources/BakedWorld/meta.txt — Tools → BubbleRise → 5. Запечь атласы мира")
walls_src = walls
report("Без запечённых файлов игра рисует атласы сама",
       "LoadBaked(" in world and "PaintAtlas(" in method(world, "static Texture2D BakedOrPaintAtlas(")
       and "?? MakeTexture(\"caustics\"" in world and "?? MakeTexture(\"wall_\"" in walls_src)
boot = src("GameBootstrap.cs")
loading = src("LoadingScreen.cs")
fade_out = num(loading, r"FadeOut\s*=\s*([\d.]+)f")
report("Экран загрузки с прогрессом, уходит плавно (≥ 0.3 с)",
       "PrewarmSteps()" in boot and "SetProgress(" in boot and fade_out is not None and fade_out >= 0.3,
       "тает за %s с" % fade_out)
report("Смена экрана на экран — мгновенная (без наплыва CanvasGroup)",
       "alpha = swapped ? 1f : 0f" in ui and "_ghost" not in ui and "BeginSwap" not in ui)
art = src("SpriteFactoryHazardArt.cs")
obstacle = src("Obstacle.cs")
report("D-17: свет угроз не вращается вместе с обломком",
       "LightDir" not in art and "_glint" in obstacle and "t.rotation = Quaternion.identity" in obstacle)

sys.exit(1 if fails else 0)
