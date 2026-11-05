# -*- coding: utf-8 -*-
"""
Змей бездны, грозовая медуза, чернильный морок: этапы, телеграфы, лимиты
яркости и проходимость новых атак.

Что проверяется:

1. У змея два этапа и переход между ними, во время перехода урона нет.
2. У каждой новой атаки телеграф не короче 0.6 с (хвост, кольцо, шары
   морока, удар головы из кольца).
3. Змей во втором этапе не бывает неуязвим дольше 4 с подряд; звенья лежат
   ниже пузырька и стража (C-10); первый этап не бьёт чаще прежнего максимума,
   а заходит со всех сторон, кроме сектора под полосой HP (C-09).
4. Молнии медузы: форма живёт не меньше 0.25 с, появление ≥ 0.15 с, угасание
   0.3–0.6 с, покачивание не чаще раза в 1.5 с, цвета без белого (сумма
   каналов ≤ 2.3) и не красные, кольцо прицела α ≤ 0.5.
5. Чернила (30.10): потолок затемнения 0.97 — в конфиге; облако расходится за
   0.3–0.5 с, сходят за 0.6–0.8 с, оба фронта и оба слоя по SmoothStep; пузырёк и
   страж обведены контуром поверх чернил (α 0.5–0.6); слои вместе не темнее
   потолка (раньше кляксы ложились
   друг на друга, и экран закрывался на 0.998).
6. Глаза морока разгораются не быстрее 0.3 с, стрелки струи не мигают чаще
   раза в 1.5 с, смерть 1.5–2 с, в темноте рыбы не атакуют (C-13).
7. Проходимость на узком экране (аспект 0.45 и уже): идеальный игрок с
   реакцией 0.5 с успевает отбить хвост, выйти из кольца и перекрыть шар;
   брошенный страж хвост сам не отбивает.

Числа читаются из кода, комментарии вырезаются — проверка смотрит на то, что
исполняется. Каждая строка итога начинается с «ОК: » или «ПРОВАЛ: ».

    python3 Tools/check_bosses.py
"""
import io
import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def read(name):
    path = os.path.join(ROOT, "Assets", "Scripts", name)
    if not os.path.exists(path):
        return ""
    return io.open(path, encoding="utf-8").read()


def strip_comments(src):
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        if c == "/" and i + 1 < n and src[i + 1] == "/":
            j = src.find("\n", i)
            i = n if j < 0 else j
            continue
        if c == "/" and i + 1 < n and src[i + 1] == "*":
            j = src.find("*/", i + 2)
            i = n if j < 0 else j + 2
            out.append(" ")
            continue
        if c == '"':
            j = i + 1
            while j < n and src[j] != '"':
                j += 2 if src[j] == "\\" else 1
            out.append(src[i:j + 1])
            i = j + 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def bodies(src):
    """Тела методов `тип Имя(...) { ... }` по счёту скобок."""
    found = {}
    for m in re.finditer(r"\b(?:void|bool|int|float|Vector2)\s+(\w+)\s*\([^)]*\)\s*\{", src):
        depth, i = 1, m.end()
        while i < len(src) and depth:
            depth += {"{": 1, "}": -1}.get(src[i], 0)
            i += 1
        found.setdefault(m.group(1), src[m.end():i - 1])
    return found


def num(src, name):
    """`Имя = 0.25f;` или `public float имя = 0.25f;` — первое совпадение."""
    m = re.search(r"\b%s\s*=\s*(-?\d+(?:\.\d+)?)f?\s*[;,]" % name, src)
    return float(m.group(1)) if m else None


def hex_rgb(src, name):
    m = re.search(r"\b%s\s*=\s*Palette\.Hex\(\"([0-9A-Fa-f]{6})\"\)" % name, src) or \
        re.search(r"\b%s\s*=\s*Hex\(\"([0-9A-Fa-f]{6})\"\)" % name, src)
    if not m:
        return None
    h = m.group(1)
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


CFG = strip_comments(read("GameConfig.cs"))
SERP = strip_comments(read("SerpentBossController.cs"))
SB = bodies(SERP)
JELLY = strip_comments(read("JellyBossController.cs"))
JB = bodies(JELLY)
PROJ = strip_comments(read("BossProjectile.cs"))
SFJ = strip_comments(read("SpriteFactoryJelly.cs"))
INK = strip_comments(read("InkBossController.cs"))
IB = bodies(INK)
SCREEN = strip_comments(read("InkScreen.cs"))
SCB = bodies(SCREEN)
GM = strip_comments(read("GameManager.cs"))
GMB = bodies(GM)
GUARD = strip_comments(read("GuardianOrb.cs"))
ORDERS = strip_comments(read("SortingOrders.cs"))
PALETTE = strip_comments(read("Palette.cs"))


def cfg(name):
    return num(CFG, name)


results = []


def report(ok, name, detail):
    results.append(ok)
    print("%s%s | %s" % ("ОК: " if ok else "ПРОВАЛ: ", name, detail))


def guarded(name, fn):
    """Проверка, которую не удалось разобрать, — провал, а не «нечего проверять»."""
    try:
        ok, detail = fn()
    except Exception as e:   # noqa: BLE001 — любой сбой разбора = провал
        ok, detail = False, "не разобрано: %s: %s" % (type(e).__name__, e)
    report(ok, name, detail)


# ------------------------------------------------------------------ размеры поля

WORLD_H = cfg("worldHeight") or 10.0
DESIGN_ASPECT = cfg("designAspect") or 0.64
SCALE = cfg("playerScaleMultiplier") or 1.0
R_BUBBLE = (cfg("bubbleRadius") or 0.28) * SCALE
R_GUARD = (cfg("guardianRadius") or 0.45) * SCALE
FOLLOW = cfg("guardianFollowLerp") or 26.0
MIN_FROM_BUBBLE = (cfg("guardianMinDistanceFromBubble") or 0.85) * SCALE
_home = re.search(r"guardianHomeOffset\s*=\s*new Vector2\((-?[\d.]+)f,\s*(-?[\d.]+)f\)", CFG)
HOME = (float(_home.group(1)), float(_home.group(2))) if _home else (0.0, 0.95)
BUBBLE_Y = -WORLD_H / 2 + WORLD_H * (cfg("bubbleYFactor") or 0.32)

REACTION = 0.5     # реакция идеального игрока после начала телеграфа, с
FINGER = 6.0       # скорость пальца, юнитов/с: ~9 см/с на 6.5" — заведомо медленно
DT = 1.0 / 240.0
# Узкие форматы: 21:9, 20:9 (0.45), 19.5:9; плюс 16:9 и 4:3 для сравнения.
ASPECTS = ((u"21:9", 9 / 21.0), (u"0.45", 0.45), (u"19.5:9", 9 / 19.5), (u"16:9", 9 / 16.0), (u"4:3", 0.75))


def field(aspect):
    half_h = WORLD_H / 2
    half_w = half_h * aspect
    return {"hw": half_w, "phw": min(half_w, half_h * DESIGN_ASPECT), "top": half_h, "bottom": -half_h}


def order_value(expr):
    """`SortingOrders.Boss + 4` → число."""
    consts = dict((k, int(v)) for k, v in re.findall(r"public const int (\w+) = (-?\d+);", ORDERS))
    m = re.match(r"\s*SortingOrders\.(\w+)\s*(?:([+-])\s*(\d+))?\s*$", expr)
    if not m or m.group(1) not in consts:
        return None
    v = consts[m.group(1)]
    if m.group(2):
        v += int(m.group(3)) * (1 if m.group(2) == "+" else -1)
    return v


class Guardian(object):
    """Страж: палец едет к цели с постоянной скоростью, страж догоняет палец
    экспоненциально (guardianFollowLerp). Как в GuardianOrb, ближняя граница
    у пузырька действует на цель, а не на путь."""

    def __init__(self, pos, bubble):
        self.p = list(pos)
        self.finger = list(pos)
        self.bubble = bubble

    def step(self, target, dt):
        if target is not None:
            dx, dy = target[0] - self.finger[0], target[1] - self.finger[1]
            d = math.hypot(dx, dy)
            mv = min(d, FINGER * dt)
            if d > 1e-9:
                self.finger[0] += dx / d * mv
                self.finger[1] += dy / d * mv
        aim = list(self.finger)
        bx, by = aim[0] - self.bubble[0], aim[1] - self.bubble[1]
        bd = math.hypot(bx, by)
        if bd < MIN_FROM_BUBBLE:
            if bd < 1e-4:
                bx, by, bd = 0.0, 1.0, 1.0
            aim = [self.bubble[0] + bx / bd * MIN_FROM_BUBBLE, self.bubble[1] + by / bd * MIN_FROM_BUBBLE]
        k = 1 - math.exp(-FOLLOW * dt)
        self.p[0] += (aim[0] - self.p[0]) * k
        self.p[1] += (aim[1] - self.p[1]) * k


def smooth(x):
    x = max(0.0, min(1.0, x))
    return x * x * (3 - 2 * x)


# ========================================================== 1. змей: два этапа

def serpent_stages():
    enum = re.search(r"enum Phase\s*\{([^}]*)\}", SERP)
    phases = set(re.findall(r"\w+", enum.group(1))) if enum else set()
    need = ("Retreat", "Shift", "TailAim", "TailSwing", "RingForm", "RingSqueeze", "Stunned")
    bad = [u"нет фаз: " + ", ".join(p for p in need if p not in phases)] if not all(p in phases for p in need) else []
    at, shift = cfg("serpentStage2At"), cfg("serpentShiftTime")
    if at is None or abs(at - 0.5) > 1e-6:
        bad.append(u"второй этап не с 50 %% HP (%s)" % at)
    if shift is None or not 1.5 <= shift <= 2.0:
        bad.append(u"переход %s с, нужно 1.5–2" % shift)
    if "_shiftPending = true" not in SB.get("ApplyDamage", "") or "serpentStage2At" not in SB.get("ApplyDamage", ""):
        bad.append(u"урон не запускает переход")
    if not any("_shiftPending" in b and "StartRetreat()" in b for b in SB.values()):
        bad.append(u"переход не начинается уходом к краю")
    quiet = [n for n in ("StartRetreat", "TickRetreat", "StartShift", "TickShift", "ApplyShiftLook", "FinishShift")
             if any(x in SB.get(n, "") for x in ("BubbleHit", "ApplyDamage(", "CanDeflect"))]
    if quiet:
        bad.append(u"во время перехода есть урон: " + ", ".join(quiet))
    if any(n not in SB for n in ("TickRetreat", "TickShift")):
        bad.append(u"нет хода перехода")
    glow = hex_rgb(SERP, "ShiftGlow")
    if not glow or sum(glow) > 2.3 or not (glow[0] > glow[1] > glow[2]):
        bad.append(u"ореол перехода не тусклый красно-оранжевый")
    look = SB.get("ApplyShiftLook", "")
    if "SmoothStep" not in look or "Repeat(" in look:
        bad.append(u"ореол перехода не плавный или повторяется")
    if "Noise.Shift" not in SB.get("StartShift", "") or "PlaySerpentShift" not in GM:
        bad.append(u"нет низкого гула перехода")
    wave = num(SERP, "WaveHzMax")
    if wave is None or wave > 1.0 or "Mathf.Min(rate, Mathf.PI * 2f * WaveHzMax)" not in SB.get("UpdateSwim", ""):
        bad.append(u"волна по телу чаще раза в секунду — хвост мелькает")
    if bad:
        return False, u"; ".join(bad)
    return True, (u"второй этап с %.0f %% HP; уход к краю, переход %.1f с: тело темнеет, ореол %s проходит "
                  u"один раз, шипы растут, гул; урона нет; волна по телу ≤ %.1f Гц" % (at * 100, shift, "B4502A", wave))


# ================================================= 2. змей: телеграфы новых атак

def serpent_telegraphs():
    tail, swing = cfg("serpentTailTelegraph"), cfg("serpentTailSwingTime")
    form, squeeze = cfg("serpentRingFormTime"), cfg("serpentRingSqueezeTime")
    gap = cfg("serpentRingGapDiameters")
    bad = []
    if tail is None or tail < 0.6:
        bad.append(u"хвост: телеграф %s с" % tail)
    if form is None or form < 0.6:
        bad.append(u"кольцо: укладка %s с" % form)
    close = re.search(r"InverseLerp\((0\.\d+)f,\s*1f,\s*k\)", SB.get("TickRingSqueeze", ""))
    close_at = float(close.group(1)) if close else None
    if close_at is None or squeeze is None or form is None or form + close_at * squeeze < 0.8:
        bad.append(u"проём виден меньше 0.8 с до смыкания")
    if gap is None or gap < 2.2 or "GuardianDiameter * Mathf.Max(2.2f, _cfg.serpentRingGapDiameters)" not in SERP:
        bad.append(u"проём уже 2.2 диаметра стража")
    strike = re.search(r"SetPhase\(Phase\.RingStrike,\s*([\d.]+)f\)", SERP)
    if not strike or float(strike.group(1)) < 0.6:
        bad.append(u"удар головы из кольца без телеграфа 0.6 с")
    if "_tele = Tele.Arc" not in SB.get("StartTailAim", "") or "_tele = Tele.Ring" not in SB.get("StartRingForm", ""):
        bad.append(u"пунктир дуги или кольца не включается с началом атаки")
    rise, fall = num(SERP, "TeleRise"), num(SERP, "TeleFall")
    hrise, hfall = num(SERP, "HaloRise"), num(SERP, "HaloFall")
    if None in (rise, fall, hrise, hfall) or rise < 0.15 or hrise < 0.15 or not 0.3 <= fall <= 0.6 \
            or not 0.3 <= hfall <= 0.6:
        bad.append(u"пунктир или контур головы появляются быстрее 0.15 с / гаснут не за 0.3–0.6 с")
    if bad:
        return False, u"; ".join(bad)
    return True, (u"хвост %.1f + %.1f с, кольцо %.1f + %.1f с, проём %.1f диаметра виден %.2f с до смыкания, "
                  u"удар из кольца %.1f с; пунктир %.2f/%.2f с" % (tail, swing, form, squeeze, gap,
                                                                 form + close_at * squeeze,
                                                                 float(strike.group(1)), rise, fall))


# ======================================== 3. змей: уязвимость, слои, первый этап

def serpent_vulnerable():
    stun = cfg("serpentStunTime")
    pmin, pmax, rmax = cfg("serpentStage2PauseMin"), cfg("serpentStage2PauseMax"), cfg("serpentRingPauseMax")
    tele, swing = cfg("serpentTailTelegraph"), cfg("serpentTailSwingTime")
    form, squeeze = cfg("serpentRingFormTime"), cfg("serpentRingSqueezeTime")
    coil = cfg("serpentCoilTime")
    cmax, cmin = cfg("serpentCircleTimeMax"), cfg("serpentCircleTimeMin")
    wmax, wmin = cfg("serpentWindupMax"), cfg("serpentWindupMin")
    hp = int(cfg("serpentHp") or 8)
    at = cfg("serpentStage2At") or 0.5
    if None in (stun, pmin, pmax, rmax, tele, swing, form, squeeze, coil, cmax, cmin, wmax, wmin):
        return False, u"нет чисел второго этапа"
    sched = SB.get("ScheduleStage2", "")
    if "_queued == Phase.TailAim ? _cfg.serpentStage2PauseMax : _cfg.serpentRingPauseMax" not in sched:
        return False, u"не разобрана пауза перед атакой"
    if "StartStunned()" not in SB.get("ResolveRing", "") or "StartStunned()" not in SB.get("TickTailSwing", ""):
        return False, u"после хвоста или кольца нет окна уязвимости"
    # Ярость на входе во второй этап — наименьшая из возможных (HP чётный и нечётный).
    rage = min(1 - math.ceil(m * at) / float(m) for m in (hp, hp - 1, hp + 1) if m > 1)
    lunge = rmax + coil + (cmax + (cmin - cmax) * rage) + (wmax + (wmin - wmax) * rage)
    cycles = {u"хвост": pmax + tele + swing, u"кольцо": rmax + form + squeeze, u"рывок": lunge}
    worst = max(cycles.values())
    if stun < 0.8 or worst > 4.0 + 1e-6:
        return False, u"окно %.1f с; без уязвимости до %.2f с (%s)" % (
            stun, worst, ", ".join(u"%s %.2f" % kv for kv in cycles.items()))
    return True, u"окно после атаки %.1f с; без уязвимости подряд: %s — не больше 4 с" % (
        stun, ", ".join(u"%s %.2f с" % kv for kv in cycles.items()))


def serpent_layers():
    exprs = re.findall(r"(?:const int (?:HeadOrder|BodyOrder) = |sortingOrder = |NewChild\([^,]+,[^,]+,\s*)"
                       r"(SortingOrders\.\w+(?:\s*[+-]\s*\d+)?)", SERP)
    dyn = re.findall(r"sortingOrder = SortingOrders\.\w+\s*\+\s*\(", SERP)
    vals = [order_value(e) for e in exprs]
    head = re.search(r"const int HeadOrder = (SortingOrders\.\w+\s*\+\s*\d+)", SERP)
    body = re.search(r"const int BodyOrder = (SortingOrders\.\w+\s*\+\s*\d+)", SERP)
    bubble = order_value("SortingOrders.Bubble")
    if dyn or not head or not body or None in vals:
        return False, u"порядок звеньев считается от числа звеньев (до %s и выше стража)" % (
            "Boss + count" if dyn else "?")
    h, b = order_value(head.group(1)), order_value(body.group(1))
    if not (5 <= b <= 9 and 5 <= h <= 9) or max(vals) >= bubble:
        return False, u"голова %s, тело %s, максимум %s при пузырьке %s" % (h, b, max(vals), bubble)
    return True, u"голова %d, тело %d, всё змеиное не выше %d — под пузырьком (%d) и стражем" % (
        h, b, max(vals), bubble)


def serpent_stage1():
    rmax, rmin = cfg("serpentRoamTimeMax"), cfg("serpentRoamTimeMin")
    pick = SB.get("PickStrikeAngle", "")
    rng = re.search(r"Random\.Range\(0f,\s*([\d.]+)f\)", pick)
    skip = re.search(r"if \(deg >= ([\d.]+)f\) deg \+= ([\d.]+)f;", pick)
    bad = []
    # Прежде плавание 4.6–2.6 с: при полном HP 53 % боя (C-09). Самое частое —
    # 2.6 с; чаще этого рывков быть не должно.
    if rmax is None or rmin is None or rmax > 3.0 or rmin < 2.6:
        bad.append(u"плавание %s–%s с (нужно не дольше 3 и не короче прежних 2.6)" % (rmax, rmin))
    if not rng or not skip or "PickStrikeAngle()" not in SB.get("StartCoil", ""):
        bad.append(u"угол замаха не выбирается по кругу")
    else:
        top, at, jump = float(rng.group(1)), float(skip.group(1)), float(skip.group(2))
        lo, hi = at, at + jump
        if abs(top + jump - 360) > 1e-6 or not (lo >= 200 and hi <= 340 and hi - lo >= 40):
            bad.append(u"исключён сектор %.0f–%.0f°" % (lo, hi))
    if bad:
        return False, u"; ".join(bad)
    return True, u"плавание %.1f с (было 4.6 → 2.6), заход по кругу кроме %.0f–%.0f° под пузырьком" % (
        rmax, float(skip.group(1)), float(skip.group(1)) + float(skip.group(2)))


# =============================================================== 4. медуза

def jelly_bolts():
    step, fin, fout = num(PROJ, "StormStep"), num(PROJ, "StormFadeIn"), num(PROJ, "StormFadeOut")
    recolor, sway = num(PROJ, "StormRecolor"), num(PROJ, "SwayPeriod")
    bad = []
    if step is None or step < 0.25:
        bad.append(u"форма живёт %s с (нужно ≥ 0.25, не чаще 4 раз в секунду)" % step)
    if fin is None or fin < 0.15:
        bad.append(u"появление %s с" % fin)
    if fout is None or not 0.3 <= fout <= 0.6:
        bad.append(u"угасание %s с" % fout)
    if recolor is None or recolor < 0.15:
        bad.append(u"перекраска при отражении %s с" % recolor)
    if sway is None or sway < 1.5:
        bad.append(u"покачивание %s с" % sway)
    tick = PROJ
    if tick.count("SmoothStep(0f, 1f, Mathf.InverseLerp(") < 2:
        bad.append(u"формы сменяются без перетекания")
    colors = {}
    for n in ("StormBodyHostile", "StormCoreHostile", "StormBodyReflected", "StormCoreReflected", "StormOutline"):
        colors[n] = hex_rgb(SFJ, n)
    if None in colors.values():
        bad.append(u"нет цветов молнии")
    else:
        for n, c in colors.items():
            if sum(c) > 2.3:
                bad.append(u"%s сумма %.2f" % (n, sum(c)))
        for n in ("StormBodyHostile", "StormCoreHostile"):
            r, g, b = colors[n]
            if r > b or r > g + 0.15:
                bad.append(u"%s красный" % n)
    ring = num(JELLY, "AimRingAlpha")
    if ring is None or ring > 0.5:
        bad.append(u"кольцо прицела α %s" % ring)
    veins = re.search(r"_veinsShown = Mathf\.MoveTowards\([^;]*dt / ([\d.]+)f\)", JELLY)
    tele = num(JELLY, "TelegraphTime")
    if not veins or float(veins.group(1)) < 0.3 or tele is None or tele < 0.6:
        bad.append(u"свет по жилкам быстрее 0.3 с или телеграф короче 0.6 с")
    imp = num(JELLY, "ImpactTime")
    hit = re.search(r"Burst\(at, core, (\d+),", JB.get("Impact", ""))
    refl = re.search(r"Burst\(p\.Center, SpriteFactory\.StormCoreReflected, (\d+),", JELLY)
    if imp is None or abs(imp - 0.35) > 0.01 or not hit or not 6 <= int(hit.group(1)) <= 12:
        bad.append(u"попадание: не 6–12 искр и кольцо 0.35 с")
    if not refl or not 6 <= int(refl.group(1)) <= 10:
        bad.append(u"отражение: не 6–10 искр")
    if bad:
        return False, u"; ".join(bad[:6])
    return True, (u"форма %.2f с (≤ %.1f раза/с), появление %.2f с, угасание %.2f с, перекраска %.2f с, "
                  u"качание раз в %.1f с; цвета ≤ 2.3 и не красные; кольцо α %.2f; жилки %.2f с; "
                  u"искры %s/%s, кольцо %.2f с" % (step, 1 / step, fin, fout, recolor, sway, ring,
                                                    float(veins.group(1)), hit.group(1), refl.group(1), imp))


# ================================================================ 5. чернила

def splat_overlap():
    """Сколько клякс InkScreen ложится на одну точку — по раскладке из кода."""
    cols, rows = num(SCREEN, "Columns"), num(SCREEN, "Rows")
    spl = SCB.get("Splash", "")
    size_k = re.search(r"Mathf\.Max\(cellW, cellH\) \* ([\d.]+)f", spl)
    jit = re.search(r"Random\.Range\(-cellW \* ([\d.]+)f", spl)
    sc = re.search(r"size \* Random\.Range\(([\d.]+)f, ([\d.]+)f\)", spl)
    if None in (cols, rows) or not size_k or not jit or not sc:
        return None
    cols, rows = int(cols), int(rows)
    worst = 0
    import random
    rnd = random.Random(7)
    for aspect in (0.42, 0.45, 0.5625, 0.75, 1.0, 1.78):
        h = WORLD_H
        w = h * aspect
        cw, ch = w / cols, h / rows
        size = max(cw, ch) * float(size_k.group(1))
        for _ in range(12):
            sp = []
            for i in range(cols * rows):
                c, r = i % cols, i // cols
                x = -w / 2 + cw * (c + 0.5) + rnd.uniform(-1, 1) * cw * float(jit.group(1))
                y = -h / 2 + ch * (r + 0.5) + rnd.uniform(-1, 1) * ch * float(jit.group(1))
                s = size * rnd.uniform(float(sc.group(1)), float(sc.group(2)))
                sp.append((x, y, s * 0.5 * 0.95))   # кромка кляксы не дальше 0.95 половины спрайта
            for gx in range(20):
                for gy in range(36):
                    px = -w / 2 + w * (gx + 0.5) / 20
                    py = -h / 2 + h * (gy + 0.5) / 36
                    n = sum(1 for x, y, r in sp if (px - x) ** 2 + (py - y) ** 2 < r * r)
                    worst = max(worst, n)
    return worst


def ink_darkness():
    rise, alpha, fade = cfg("inkRiseTime"), cfg("inkBlackoutAlpha"), cfg("inkFadeTime")
    tick = SCB.get("Tick", "")
    bad = []
    if rise is None or not 0.3 <= rise <= 0.5 or "_rise + dt / _riseTime" not in tick \
            or "Mathf.SmoothStep(0f, 1f, _rise)" not in tick:
        bad.append(u"облако закрывает экран за %s с (нужно 0.3–0.5 из конфига, по SmoothStep)" % rise)
    if alpha is None or not 0.95 <= alpha <= 0.97 + 1e-6:
        bad.append(u"потолок затемнения %s (нужно 0.95–0.97 в конфиге)" % alpha)
    if fade is None or not 0.6 <= fade <= 0.8:
        bad.append(u"чернила сходят за %s с (нужно 0.6–0.8)" % fade)
    if "Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_elapsed / _fade))" not in tick or "_fade * 0.5f" in tick:
        bad.append(u"затухание не по SmoothStep или подложка уходит быстрее inkFadeTime")
    apply_ = SCB.get("Apply", "")
    overlap = num(SCREEN, "MaxOverlap")
    share = num(SCREEN, "BaseShare") or 1.0
    real = splat_overlap()
    room = "Mathf.Pow((1f - _peakAlpha) / (1f - b), 1f / MaxOverlap)" in apply_ and "InkColor(room * k)" in \
        apply_.replace("float a = room * k;", "").replace("InkColor(a)", "InkColor(room * k)")
    worst = None
    if alpha is not None and real is not None:
        if room and overlap:
            # худший случай: подложка b (до доли BaseShare потолка) и real клякс по доле остатка
            worst = 0.0
            for i in range(101):
                b = alpha * share * i / 100.0
                s_ = 0.0 if b >= alpha else 1 - ((1 - alpha) / (1 - b)) ** (1.0 / overlap)
                worst = max(worst, 1 - (1 - b) * (1 - s_) ** real)
        else:
            worst = 1 - (1 - alpha) ** (1 + real)   # прежняя схема: каждая клякса с альфой подложки
        if worst > alpha + 1e-6:
            bad.append(u"слои вместе закрывают экран на %.3f (подложка и до %d клякс)" % (worst, real))
    else:
        bad.append(u"раскладка клякс не разобрана")
    if bad:
        return False, u"; ".join(bad)
    return True, (u"потолок %.2f (конфиг), облако %.1f с и сход %.1f с по SmoothStep; до %d клякс на точку — "
                  u"вместе не темнее %.3f" % (alpha, rise, fade, real, worst))


def ink_outline():
    """Пузырёк и страж видны в темноте: мягкий светлый контур поверх чернил."""
    bad = []
    a = cfg("inkOutlineAlpha")
    if a is None or not 0.5 <= a <= 0.6:
        bad.append(u"яркость контура %s (нужно 0.5–0.6 в конфиге)" % a)
    tick = IB.get("TickOutlines", "")
    for who in ("_bubbleOutline", "_guardianOutline"):
        if who not in INK or who not in tick:
            bad.append(u"нет контура %s" % who)
    if not re.search(r"_bubbleOutline = NewRenderer\([^;]*SortingOrders\.Ink \+ [2-4]", INK):
        bad.append(u"контур не поверх клякс (порядок Ink+2…Ink+4)")
    if "_ink.Coverage" not in tick or "_cfg.inkOutlineAlpha" not in tick:
        bad.append(u"контур не следует за плотностью чернил")
    if "TickOutlines();" not in IB.get("Tick", ""):
        bad.append(u"контур не обновляется в такте")
    if bad:
        return False, u"; ".join(bad)
    return True, u"пузырёк и страж — светлый контур поверх клякс, α %.2f × плотность чернил" % a


def ink_look():
    bad = []
    eye = num(INK, "EyeRise")
    if eye is None or eye < 0.3 or "dt / EyeRise" not in IB.get("ApplyLook", ""):
        bad.append(u"глаза разгораются быстрее 0.3 с")
    eyes = num(INK, "EyeCount")
    arms = num(INK, "ArmCount")
    # 30.10: снова спрут — два глаза, как у прежнего морока, и 6–8 щупалец
    if eyes is None or eyes != 2 or arms is None or not 6 <= arms <= 8:
        bad.append(u"глаз %s, щупалец %s (нужно 2 и 6–8)" % (eyes, arms))
    if re.search(r"InkBoss\(\)|SpriteFactory\.InkBoss\b", INK):
        bad.append(u"прежний спрайт с прямоугольными глазами")
    wave = num(INK, "ChevronWavePeriod")
    if wave is None or wave < 1.5 or "_pulse / ChevronWavePeriod" not in IB.get("UpdateChevrons", ""):
        bad.append(u"стрелки струи мигают чаще раза в 1.5 с")
    for w, a in re.findall(r"Mathf\.Sin\(_pulse \* ([\d.]+)f\) \* ([\d.]+)f", INK) + \
            [(w, a) for a, w in re.findall(r"([\d.]+)f \* Mathf\.Sin\(_pulse \* ([\d.]+)f\)", INK)]:
        if float(w) > 2 * math.pi / 1.5 and float(a) > 0.005:
            bad.append(u"пульсация %s рад/с" % w)
    death = num(INK, "DeathTime")
    if death is None or not 1.5 <= death <= 2.0:
        bad.append(u"смерть %s с" % death)
    # При гибели чернила раньше смывались вшестеро быстрее — экран светлел за 0.1 с.
    if re.search(r"_ink\.Tick\([^;]*\*", INK) or "_ink.EndHold()" not in IB.get("ApplyDamage", ""):
        bad.append(u"при гибели чернила сходят быстрее inkFadeTime")
    if "_tint.Hit()" not in INK:
        bad.append(u"удар по мороку не через плавный подмес")
    dark = re.search(r"_darkShown = Mathf\.MoveTowards\([^;]*dt / ([\d.]+)f\)", INK)
    if not dark or float(dark.group(1)) < 0.3:
        bad.append(u"точки глаз в чернилах появляются быстрее 0.3 с")
    if bad:
        return False, u"; ".join(bad)
    return True, (u"%d глаза, %d щупалец; глаза %.2f с, точки в чернилах %.1f с, стрелки — проход за %.1f с, "
                  u"смерть %.1f с, удар — подмес" % (eyes, arms, eye, float(dark.group(1)), wave, death))


def ink_fish():
    tick = GMB.get("TickFish", "") or GM
    bad = []
    if "f.Flee(" not in tick or "_inkBoss.InkCoverage" not in tick:
        bad.append(u"рыбы в темноте не уплывают")
    if not re.search(r"toBubble\.magnitude / closing < 0\.5f", tick):
        bad.append(u"нет следа пузырьков за 0.5 с до подлёта")
    if "_inkBoss.HoldsFish" not in GM:
        bad.append(u"рыбы выпускаются с замаха")
    if bad:
        return False, u"; ".join(bad)
    return True, u"новых рыб нет с замаха до просвета, живые уплывают, за 0.5 с до подлёта — след пузырьков"


def ink_balls():
    tmin = cfg("inkSearchTimeMin")
    bad = []
    if "StartBalls()" not in IB.get("TickSearch", ""):
        return False, u"новой атаки нет"
    lane = re.search(r"_laneShown = Mathf\.MoveTowards\([^;]*dt / \(want \? ([\d.]+)f : ([\d.]+)f\)\)", INK)
    if tmin is None or tmin < 0.6 or not lane or float(lane.group(1)) < 0.15 or not 0.3 <= float(lane.group(2)) <= 0.6:
        bad.append(u"пути шаров видны меньше 0.6 с или появляются за кадр")
    if "Palette.Coral" not in IB.get("TickLanes", ""):
        bad.append(u"настоящий путь не отличается от ложных")
    if bad:
        return False, u"; ".join(bad)
    return True, u"пути видны %.1f–%.1f с до броска (появление %.1f с): настоящий — коралловые точки, ложные — кольца" % (
        tmin, cfg("inkSearchTimeMax") or 0, float(lane.group(1)))


# ========================================================== 7. проходимость

def tail_sim(aspect, side, start, reaction):
    """Удар хвостом: True, если страж встретил лезвие раньше, чем оно пузырёк."""
    f = field(aspect)
    lead, follow = num(SERP, "TailArcLead"), num(SERP, "TailArcFollow")
    radius = min(num(SERP, "TailArcRadius"), f["phw"] - 0.25)
    bubble = (0.0, BUBBLE_Y)
    pivot = (0.0, BUBBLE_Y + radius)
    phi = -math.pi / 2
    a0 = phi - side * math.radians(lead)
    a1 = phi + side * math.radians(follow)
    tele, swing = cfg("serpentTailTelegraph"), cfg("serpentTailSwingTime")
    blade = cfg("serpentTailBladeRadius")
    g = Guardian(start, bubble)
    block = (pivot[0] + radius * math.cos(phi - side * math.radians(28)),
             pivot[1] + radius * math.sin(phi - side * math.radians(28)))
    t = 0.0
    while t < tele + swing:
        g.step(block if reaction is not None and t >= reaction else None, DT)
        t += DT
        if t < tele:
            continue
        ang = a0 + (a1 - a0) * smooth((t - tele) / swing)
        tip = (pivot[0] + radius * math.cos(ang), pivot[1] + radius * math.sin(ang))
        if math.hypot(tip[0] - g.p[0], tip[1] - g.p[1]) < blade + R_GUARD:
            return True
        if math.hypot(tip[0] - bubble[0], tip[1] - bubble[1]) < blade + R_BUBBLE:
            return False
    return False


def latest(fn):
    """Самая поздняя реакция, с которой атака ещё отбивается, с (до 0.01 с).
    Позже начал — позже успел: успех монотонен, поэтому хватает деления пополам."""
    if not fn(0.0):
        return None
    lo, hi = 0.0, 3.0
    if fn(hi):
        return hi
    while hi - lo > 0.01:
        mid = (lo + hi) / 2
        if fn(mid):
            lo = mid
        else:
            hi = mid
    return lo


def ring_sim(aspect, gap_angle, reaction):
    """Кольцо: True, если страж вышел через проём до смыкания."""
    f = field(aspect)
    r0, r1 = cfg("serpentRingRadius"), cfg("serpentRingEndRadius")
    form, squeeze = cfg("serpentRingFormTime"), cfg("serpentRingSqueezeTime")
    width = 2 * R_GUARD * max(2.2, cfg("serpentRingGapDiameters"))
    close_at = float(re.search(r"InverseLerp\((0\.\d+)f,\s*1f,\s*k\)", SB.get("TickRingSqueeze", "")).group(1))
    bubble = (0.0, BUBBLE_Y)
    home = (HOME[0], BUBBLE_Y + HOME[1])
    mx = max(0.05, f["hw"] - r0 - 0.45)
    arena_bottom = f["bottom"] + 1.6
    c = (max(-mx, min(mx, home[0])), max(arena_bottom + r0, min(f["top"] - r0 - 0.5, home[1])))
    g = Guardian(home, bubble)
    inside = math.hypot(g.p[0] - c[0], g.p[1] - c[1]) < r0 - 0.22
    target = (c[0] + math.cos(gap_angle) * (r0 + 0.8), c[1] + math.sin(gap_angle) * (r0 + 0.8))
    t = 0.0
    while t < form + squeeze:
        k = 0.0 if t < form else (t - form) / squeeze
        radius = r0 + (r1 - r0) * smooth(k)
        w = width * (1 - smooth((k - close_at) / (1 - close_at))) if t >= form else width
        half = math.asin(min(1.0, w * 0.5 / max(radius, 0.01)))
        g.step(target if t >= reaction else None, DT)
        if inside:
            dx, dy = g.p[0] - c[0], g.p[1] - c[1]
            d = math.hypot(dx, dy)
            off = abs((math.atan2(dy, dx) - gap_angle + math.pi) % (2 * math.pi) - math.pi)
            shoulder = math.asin(min(1.0, R_GUARD / max(d, R_GUARD)))
            wall = max(0.0, radius - 0.22 - R_GUARD)
            if d > wall and off >= half - shoulder:
                g.p = [c[0] + dx / d * wall, c[1] + dy / d * wall]
            elif d > radius - 0.22 + R_GUARD:
                inside = False
        t += DT
    return not inside


def ring_gaps(aspect):
    """Углы проёма, которые выберет PickGapAngle (место за проёмом > 0.25)."""
    f = field(aspect)
    r0 = cfg("serpentRingRadius")
    home = (HOME[0], BUBBLE_Y + HOME[1])
    mx = max(0.05, f["hw"] - r0 - 0.45)
    arena_bottom = f["bottom"] + 1.6
    c = (max(-mx, min(mx, home[0])), max(arena_bottom + r0, min(f["top"] - r0 - 0.5, home[1])))
    clear = num(SERP, "GapBubbleClear") or 0.0
    to_b = math.atan2(BUBBLE_Y - c[1], 0.0 - c[0])
    near = math.hypot(c[0], BUBBLE_Y - c[1]) < r0 + 0.9
    out, rest = [], []
    for i in range(36):
        a = math.radians(i * 10)
        ox, oy = c[0] + math.cos(a) * (r0 + 0.9), c[1] + math.sin(a) * (r0 + 0.9)
        if min(f["hw"] - abs(ox), f["top"] - oy, oy - arena_bottom) <= 0.25:
            continue
        off = abs((a - to_b + math.pi) % (2 * math.pi) - math.pi)
        (rest if near and math.degrees(off) < clear else out).append(a)
    return out or rest


def balls_sim(aspect, corner, reaction):
    """Шары: True, если страж из угла перекрыл настоящий путь до пузырька."""
    f = field(aspect)
    hover = cfg("inkHoverFactor")
    span = cfg("inkBodySpan")
    search = cfg("inkSearchTimeMin")
    speed = cfg("inkStrikeSpeed")
    jet = cfg("inkJetRadius")
    body = f["phw"] * 2 * span
    muzzle = (0.0, f["top"] - WORLD_H * hover - body * 0.12)
    bubble = (0.0, BUBBLE_Y)
    mxg, myg = f["hw"] * 0.22, WORLD_H * 0.18
    start = ((-1 if corner in (0, 3) else 1) * (f["hw"] - mxg), f["top"] - myg if corner < 2 else f["bottom"] + myg)
    dx, dy = bubble[0] - muzzle[0], bubble[1] - muzzle[1]
    L = math.hypot(dx, dy)
    ux, uy = dx / L, dy / L
    block = (bubble[0] - ux * 1.3, bubble[1] - uy * 1.3)
    g = Guardian(start, bubble)
    t = 0.0
    ball = list(muzzle)
    while t < search + 3.0:
        g.step(block if t >= reaction else None, DT)
        t += DT
        if t < search:
            continue
        ball[0] += ux * speed * DT
        ball[1] += uy * speed * DT
        if math.hypot(ball[0] - g.p[0], ball[1] - g.p[1]) < jet + R_GUARD:
            return True
        if math.hypot(ball[0] - bubble[0], ball[1] - bubble[1]) < jet + R_BUBBLE:
            return False
    return False


def decoys_miss(aspect):
    f = field(aspect)
    spread = num(INK, "LaneSpread")
    body = f["phw"] * 2 * cfg("inkBodySpan")
    muzzle_y = f["top"] - WORLD_H * cfg("inkHoverFactor") - body * 0.12
    L = muzzle_y - BUBBLE_Y
    # крайний путь проходит от пузырька на L·sin(spread), а лопается на 1.15 L
    return L * math.sin(math.radians(spread)), cfg("inkJetRadius") + R_BUBBLE


def passability():
    if "TailAim" not in SERP or num(SERP, "TailArcRadius") is None:
        return False, u"у змея нет удара хвостом"
    if "StartBalls()" not in INK:
        return False, u"у морока нет новой атаки"
    lines, bad = [], []
    for label, aspect in ASPECTS:
        f = field(aspect)
        home = (HOME[0], BUBBLE_Y + HOME[1])
        far = (f["hw"] - 0.5, BUBBLE_Y + 0.4)
        tail = []
        for side in (1, -1):
            if tail_sim(aspect, side, home, None):
                bad.append(u"%s: брошенный страж сам отбивает хвост" % label)
            for st in (home, (side * far[0], far[1])):
                r = latest(lambda x: tail_sim(aspect, side, st, x))
                tail.append(r if r is not None else -1)
        ring = [latest(lambda x: ring_sim(aspect, a, x)) for a in ring_gaps(aspect)]
        ring = [r if r is not None else -1 for r in ring] or [-1]
        balls = [latest(lambda x: balls_sim(aspect, c, x)) for c in range(4)]
        balls = [r if r is not None else -1 for r in balls]
        miss, need = decoys_miss(aspect)
        worst = (min(tail), min(ring), min(balls))
        # Требование — на узких экранах; на широких страж может стоять дальше от
        # дуги, чем палец пройдёт за полсекунды, и это показывается, но не валит.
        narrow = aspect <= 9 / 19.5 + 1e-6
        for name, v in zip((u"хвост", u"кольцо", u"шары"), worst):
            if v < REACTION and (narrow or name != u"хвост"):
                bad.append(u"%s %s: реакция до %.2f с" % (label, name, v))
        if not narrow and min(tail[0], tail[2]) < REACTION:
            bad.append(u"%s хвост от места стража: реакция до %.2f с" % (label, min(tail[0], tail[2])))
        if miss <= need:
            bad.append(u"%s: ложный шар задевает пузырёк" % label)
        lines.append(u"%s хвост %.2f, кольцо %.2f, шары %.2f" % ((label,) + worst))
    print(u"   самая поздняя реакция, с которой атака отбивается (палец %.0f юн/с, страж %.0f/с): %s"
          % (FINGER, FOLLOW, u"; ".join(lines)))
    if bad:
        return False, u"; ".join(bad[:5])
    narrow = [l for l in lines if l.startswith((u"21:9", u"0.45", u"19.5:9"))]
    return True, u"идеальному игроку хватает реакции ≥ 0.5 с: " + u"; ".join(narrow) + \
        u"; брошенный страж хвост не отбивает"


CHECKS = (
    (u"Змей: два этапа и переход без урона", serpent_stages),
    (u"Змей: телеграф каждой новой атаки ≥ 0.6 с", serpent_telegraphs),
    (u"Змей: неуязвим не дольше 4 с подряд", serpent_vulnerable),
    (u"Змей: звенья под пузырьком и стражем (C-10)", serpent_layers),
    (u"Змей: первый этап — короче паузы, заход по кругу (C-09)", serpent_stage1),
    (u"Медуза: молнии — частота формы, фронты, цвет", jelly_bolts),
    (u"Морок: затемнение — нарастание, спад, потолок", ink_darkness),
    (u"Морок: глаза, стрелки и смерть без мигания", ink_look),
    (u"Морок: пузырёк и страж видны поверх чернил", ink_outline),
    (u"Морок: новая атака с телеграфом ≥ 0.6 с", ink_balls),
    (u"Морок: рыбы в темноте не атакуют (C-13)", ink_fish),
    (u"Боссы: новые атаки проходимы на узком экране", passability),
)

if __name__ == "__main__":
    for name, fn in CHECKS:
        guarded(name, fn)
    bad = results.count(False)
    print(u"\nпроверок %d, провалов %d" % (len(results), bad))
    sys.exit(1 if bad else 0)
