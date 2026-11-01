# -*- coding: utf-8 -*-
"""Проверка готовности к публикации: то, что можно установить, не запуская игру.

    python3 Tools/audit.py

Не заменяет прогон в браузере — время загрузки, тач и поведение рекламы проверяются
только там. Но всё, что видно из исходников, ловит за секунды.
"""
import glob
import io
import os
import re
import sys

OK, WARN, FAIL = "OK  ", "ВНИМ", "ПЛОХ"
rows = []

# Консоль Windows по умолчанию cp1251, а в отчёте есть минус U+2212 и тире.
# Без этого аудит падает на выводе — то есть ровно тогда, когда все проверки
# уже прошли и смотреть их стало незачем.
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")


def check(name, status, detail=""):
    rows.append((status, name, detail))


def read(path):
    return io.open(path, encoding="utf-8").read()


def strip_comments(src):
    """Исходник C# без комментариев: проверки обязаны видеть код, а не описание.

    Раньше часть проверок находила нужную строку в закомментированном коде и
    молча проходила. Строковые литералы сохраняются как есть: внутри них «//»
    комментарием не является.
    """
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
        if c == "@" and i + 1 < n and src[i + 1] == '"':
            j = i + 2
            while j < n:
                if src[j] == '"' and j + 1 < n and src[j + 1] == '"':
                    j += 2
                    continue
                if src[j] == '"':
                    break
                j += 1
            out.append(src[i:j + 1])
            i = j + 1
            continue
        if c in "\"'":
            j = i + 1
            while j < n and src[j] != c:
                j += 2 if src[j] == "\\" else 1
            out.append(src[i:j + 1])
            i = j + 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def runtime_code():
    """Код всех рантайм-скриптов без комментариев: {имя файла: текст}."""
    return {os.path.basename(f): strip_comments(s) for f, s in scripts(runtime_only=True)}


def scripts(exclude=(), runtime_only=False):
    for f in sorted(glob.glob("Assets/Scripts/**/*.cs", recursive=True)):
        if os.path.basename(f) in exclude:
            continue
        # редакторные скрипты в билд не попадают, игрок их строк не видит
        if runtime_only and os.sep + "Editor" + os.sep in f:
            continue
        yield f, read(f)


html = read("Assets/WebGLTemplates/YandexGames/index.html")

# ------------------------------------------------------------ локализация
loc = read("Assets/Scripts/Loc.cs")
defined = set(re.findall(r'\{\s*"([a-z][\w.]*)"\s*,', loc))
used = set()
for f, src in scripts(exclude=("Loc.cs",)):
    used |= set(re.findall(r'Loc\.Get\(\s*"([^"]+)"', src))
    used |= set(re.findall(r'SkinDef\.(?:Depth|Trophy|Tally|Mastery)\(\s*"([^"]+)"', src))
    used |= set(re.findall(r'NameKey\s*=\s*"([^"]+)"', src))
used.discard("boss.")          # склеивается в рантайме, ключи проверяем ниже

missing = sorted(used - defined)
check("Локализация: все ключи определены",
      OK if not missing else FAIL,
      "" if not missing else "нет: " + ", ".join(missing))

boss_keys = ["jelly", "serpent", "octopus", "swordfish", "crab", "ink", "piranhas",
             "turtles", "shark", "manta"]
absent = [k for k in boss_keys if "boss." + k not in defined]
check("Локализация: имена всех боссов",
      OK if not absent else FAIL,
      "%d имён" % len(boss_keys) if not absent else "нет: " + str(absent))

# каждая строка таблицы обязана иметь ровно два перевода.
# Длинные переводы в Loc.cs склеены из нескольких литералов через "+", поэтому
# сначала схлопываем такие склейки: иначе кавычек выходит больше четырёх и
# совершенно исправная строка считалась бы битой.
pairs = re.findall(r'\{\s*"[a-z][\w.]*"\s*,\s*new\[\]\s*\{([^}]*)\}', loc)
bad = [p for p in pairs if re.sub(r'"\s*\+\s*"', '', p).count('"') != 4]
check("Локализация: у каждого ключа ровно RU и EN",
      OK if not bad else FAIL, "" if not bad else "битых строк: %d" % len(bad))
check("Локализация: размер словаря", OK, "%d ключей x 2 языка" % len(defined))

# Наличие двух строк ещё не значит, что вторая — перевод. Ловим самое частое:
# русский текст, скопированный в английскую колонку и забытый там.
untranslated = []
for key, body in re.findall(r'\{\s*"([a-z][\w.]*)"\s*,\s*new\[\]\s*\{([^}]*)\}', loc):
    parts = re.findall(r'"((?:[^"\\]|\\.)*)"', re.sub(r'"\s*\+\s*"', '', body))
    if len(parts) == 2 and (re.search("[Ѐ-ӿ]", parts[1]) or not parts[1].strip()):
        untranslated.append(key)
check("Локализация: английский действительно английский",
      OK if not untranslated else FAIL,
      "" if not untranslated else "кириллица или пусто: " + ", ".join(untranslated))

# --- язык площадки. Модерация открывает игру с ?lang=<код> и смотрит, на каком
# языке она открылась. Здесь ловим два способа это сломать, оба уже случались.
loc = read("Assets/Scripts/Loc.cs")

# Правила выбора языка обязаны жить в одном месте. Когда они были размазаны по
# Reload и ApplyPlatformLanguage, те разошлись между собой, и загрузка сохранения
# откатывала язык площадки обратно на русский.
resolve_calls = loc.count("Apply(Resolve())")
check("Локализация: язык решается в одном месте",
      OK if "static Language Resolve()" in loc and resolve_calls >= 2 else FAIL,
      "Reload и ApplyPlatformLanguage обязаны идти через Resolve, вызовов: %d" % resolve_calls)

# Язык площадки обязан побеждать всегда. Прежнее правило «выбор игрока сильнее,
# если сделан на той же версии площадки» стоило двух отказов по п. 2.14: модератор
# нажимает кнопку языка, чтобы проверить её, и сам себе записывает выбор, после
# которого игра навсегда открывается по-русски при ?lang=en.
check("Локализация: язык площадки сильнее выбора игрока",
      OK if "if (_platform.HasValue) return _platform.Value;" in loc
            and "ChoiceOrigin" not in loc else FAIL,
      "иначе проверяющий, однажды нажавший кнопку языка, всегда видит старый язык")

# Смотрим только тело requestLang: navigator.language законно встречается и выше,
# в скрипте экрана загрузки, который работает до инициализации SDK.
start = html.find("requestLang: function")
body = html[start:html.find("\n        },", start)] if start >= 0 else ""
i18n_at = body.find("ysdk.environment.i18n")
nav_at = body.find("navigator.language")
req = bool(body)
check("Локализация: язык площадки важнее языка браузера",
      OK if req and 0 <= i18n_at < nav_at else FAIL,
      "i18n.lang должен проверяться первым")

# ------------------------------------------- русские литералы в рантайме
cyr = re.compile(r'"[^"\n]*[А-Яа-яЁё][^"\n]*"')
leftovers = []
for f, src in scripts(exclude=("Loc.cs", "DevPanel.cs", "GameConfig.cs"), runtime_only=True):
    # Код внутри #if UNITY_EDITOR || DEVELOPMENT_BUILD в релиз не попадает —
    # игрок этих строк не увидит при всём желании. Раньше проверка их считала и
    # выдавала предупреждение, которое нечем было закрыть, кроме как убрав
    # отладочный вывод.
    dev_depth = 0
    for i, line in enumerate(src.splitlines(), 1):
        stripped = line.strip()
        if stripped.startswith("#if") and ("UNITY_EDITOR" in stripped or "DEVELOPMENT_BUILD" in stripped):
            dev_depth += 1
            continue
        if dev_depth > 0:
            if stripped.startswith("#endif"):
                dev_depth -= 1
            continue

        if "///" in line:
            continue
        code = line.split("//")[0]
        for m in cyr.findall(code):
            # Игрок этого не видит: имена объектов сцены, вывод в консоль и
            # подписи в инспекторе Unity. Последние — часть настроек разработчика
            # ровно как содержимое GameConfig, который и так в исключениях.
            if ("GameObject(" in code or "Debug." in code or "name =" in code
                    or "[Header(" in code or "[Tooltip(" in code):
                continue
            leftovers.append("%s:%d %s" % (os.path.basename(f), i, m[:40]))
check("Нет русских строк мимо словаря",
      OK if not leftovers else WARN,
      "" if not leftovers else "; ".join(leftovers[:3]))

# --------------------------------------------------------------- прогрев
sf = read("Assets/Scripts/SpriteFactory.cs")
prewarm = sf[sf.index("public static void Prewarm()"):]
prewarm = prewarm[:prewarm.index("\n        }")]

# Прогрев разбит по файлам вслед за самой фабрикой: награды греет PrewarmRewards,
# которую зовёт общий Prewarm. Без подстановки её тела проверка считала бы
# холодными все спрайты наград, хотя они греются.
for _pname, _pfile in (("PrewarmRewards()", "Assets/Scripts/SpriteFactoryRewards.cs"),
                       ("PrewarmShapes()", "Assets/Scripts/SpriteFactoryShapes.cs"),
                       ("PrewarmHazards()", "Assets/Scripts/SpriteFactoryHazards.cs")):
    if _pname in prewarm:
        _psrc = read(_pfile)
        _pbody = _psrc[_psrc.index("public static void " + _pname):]
        prewarm += _pbody[:_pbody.index("\n        }")]

declared = set()
for f, src in scripts():
    declared |= set(re.findall(r"public static Sprite (\w+)\(", src))
declared -= {"BubbleSkin", "GuardianSkin", "BubbleShell", "GuardianShell",
             "Hazard", "ShapeShell", "ShapeShellGuardian"}   # диспетчеры, зовут остальных
declared.discard("MaskOf")   # ищет готовую маску в кэше, сам ничего не рисует

cold = sorted(n for n in declared if n + "(" not in prewarm)
check("Прогрев спрайтов до старта",
      OK if not cold else WARN,
      "греются все %d" % len(declared) if not cold else "не греются: " + ", ".join(cold))

# --------------------------------------------------------- панель разработчика
dev = read("Assets/Scripts/DevPanel.cs")
guarded = dev.lstrip().startswith("#if UNITY_EDITOR || DEVELOPMENT_BUILD")
check("Панель разработчика вырезается из релиза",
      OK if guarded else FAIL, "весь файл под #if" if guarded else "файл НЕ обёрнут")

tools = read("Assets/Scripts/Editor/BubbleRiseEditorTools.cs")
check("Сборка принудительно не Development",
      OK if "EditorUserBuildSettings.development = false" in tools else FAIL)

# ----------------------------------------------------------------- внешние связи
external = []
for f, src in scripts():
    for m in re.findall(r'"(https?://[^"]+)"', src):
        external.append("%s: %s" % (os.path.basename(f), m))
for m in re.findall(r'src="(https?://[^"]+)"', html):
    if "yandex.ru/games/sdk" not in m:
        external.append("index.html: " + m)
check("Нет обращений на сторону, кроме SDK",
      OK if not external else FAIL, "" if not external else "; ".join(external))

# ------------------------------------------------------------ требования площадки
platform = [
    ("SDK из официального источника", "yandex.ru/games/sdk/v2" in html),
    ("LoadingAPI.ready с досылкой", "gameReadyPending" in html),
    ("GameplayAPI на прерываниях", "gameplayApi(paused ? 'stop' : 'start')" in html),
    ("Пауза и звук на рекламе", "onOpen: function () { setPause('ad', true); }" in html),
    ("Пауза при скрытой вкладке", "visibilitychange" in html),
    ("Глушение WebAudio", "ctx.suspend()" in html),
    ("Контекстное меню отключено", "contextmenu" in html),
    ("Промисы с .catch", html.count(".catch(") >= 4),
    ("Реклама не в начале сессии", "lastInterstitialAt = Date.now()" in html),
    ("Сторож против вечной паузы", "AD_PAUSE_WATCHDOG_MS" in html),
    ("Язык через i18n.lang", "environment.i18n" in html),
    ("Облачное сохранение профиля", "player.setData({ state: json }" in html),
    ("Баннер показывается и прячется", "showBannerAdv" in html and "hideBannerAdv" in html),
    # SDK отвечает за доли секунды, а window.unityInstance появляется через несколько
    # секунд. Без очереди язык площадки и облачное сохранение теряются молча: в логах
    # видно только направление «игра → SDK», оно работает всегда.
    ("Ответы SDK не теряются до загрузки Unity",
     "pendingMessages.push" in html and "flushPending();" in html),
]
for name, ok in platform:
    check("Площадка: " + name, OK if ok else FAIL)

# ---------------------------------------------------------------- мобильные
tpl_mobile = [
    ("viewport без масштабирования", "user-scalable=no" in html),
    ("touch-action на канвасе", "touch-action:none" in html),
    ("выделение и выноска iOS отключены", "-webkit-touch-callout" in html),
]
for name, ok in tpl_mobile:
    check("Мобильные: " + name, OK if ok else FAIL)

pointer = read("Assets/Scripts/PointerInput.cs")
check("Мобильные: тач через Input System",
      OK if "Touchscreen" in pointer else FAIL)

ui = read("Assets/Scripts/UIManager.cs")
check("Мобильные: интерфейс тянется под экран",
      OK if "ScaleMode.ScaleWithScreenSize" in ui else FAIL)

field = read("Assets/Scripts/Playfield.cs")
check("Мобильные: поле пересчитывается на resize",
      OK if "Screen.width != _lastW" in field else FAIL)

# --- раскладки экранов. Канвас тянется по высоте, значит опорная высота всегда RefH,
# а ширина плавает: на телефоне 21:9 она падает примерно до 550 единиц.
ref_h = int(re.search(r"const int RefH = (\d+);", ui).group(1))
screen_w = float(re.search(r"const float ScreenWidth = ([\d.]+)f;", ui).group(1))

check("Раскладка: канвас тянется по высоте",
      OK if "matchWidthOrHeight = 1f" in ui else FAIL,
      "иначе на широком экране высокие экраны обрежутся")

heights = [int(h) for h in re.findall(r"content\.sizeDelta = new Vector2\([^,]+,\s*(\d+)f\)", ui)]
tallest = max(heights) if heights else 0
check("Раскладка: экраны влезают по высоте",
      OK if tallest <= ref_h - 100 else FAIL,
      "самый высокий %d при опорных %d" % (tallest, ref_h))

# Сетка переехала в ShopLayout — ширину считаем по ней, а не выковыриваем из кода.
cols = int(re.search(r"(?<![A-Za-z])Columns = (\d+);", read("Assets/Scripts/ShopLayout.cs")).group(1))
step = float(re.search(r"CardStepX = ([0-9.]+)f;", read("Assets/Scripts/ShopLayout.cs")).group(1))
card = float(re.search(r"CardW = ([0-9.]+)f;", read("Assets/Scripts/ShopLayout.cs")).group(1))
span = (cols - 1) * step + card
check("Раскладка: сетка магазина влезает по ширине",
      OK if span <= screen_w else FAIL,
      "ряд %d при допустимых %d" % (span, screen_w))

# ------------------------------------------------------------- настройки проекта
ps = read("ProjectSettings/ProjectSettings.asset")


def setting(key):
    m = re.search(r"^\s*%s:\s*(\S+)" % key, ps, re.M)
    return m.group(1) if m else None


checks = [
    ("Шаблон WebGL — наш", setting("webGLTemplate") == "PROJECT:YandexGames"),
    ("Исключения не отключены", setting("webGLExceptionSupport") != "0"),
    ("Сжатие включено", setting("webGLCompressionFormat") in ("1", "2")),
    ("Decompression fallback", setting("webGLDecompressionFallback") == "1"),
    ("Ориентация: любая", setting("defaultScreenOrientation") in (None, "4", "5")),
]
for name, ok in checks:
    check("Сборка: " + name, OK if ok else WARN)

analytics = read("ProjectSettings/UnityConnectSettings.asset")
check("Аналитика Unity выключена",
      OK if "UnityAnalyticsSettings:\n    m_Enabled: 0" in analytics else WARN)

# Общий рубильник служб Unity, а не только аналитики. Сборка под Android однажды
# включила его сама, и это прямо противоречит тому, что мы обещаем в анкете о
# данных обеих площадок: игра не собирает и не передаёт ничего.
check("Службы Unity выключены целиком",
      OK if re.search(r"UnityConnectSettings:\n(?:.*\n)*?  m_Enabled: 0", analytics) else FAIL,
      "иначе в анкете Google Play «данные не собираются» становится неправдой")

# ---------------------------------------------------------- сохранения
save = read("Assets/Scripts/SaveData.cs")
fields = re.findall(r"public int (\w+)\s*(?:=[^;]+)?;", save)
check("Сохранение: состав профиля", OK, ", ".join(fields))
check("Сохранение: победы сливаются, а не затираются",
      OK if "state.bosses | PlayerPrefs.GetInt(BossesKey" in save else WARN)

# ------------------------------------------------------------------ боссы
boot = read("Assets/Scripts/GameBootstrap.cs")
m = re.search(r"new IBoss\[\]\s*\{(.*?)\}", boot, re.S)
# Комментарии выкидываем до подсчёта: в них попадаются запятые, и наивный split
# насчитывал элементов больше, чем боссов в массиве.
listing = re.sub(r"//[^\n]*", "", m.group(1)) if m else ""
count = len([x for x in listing.split(",") if x.strip()])
# Сверяем три списка между собой, а не с числом, зашитым здесь: босс, забытый
# в любом из них, тихо выпадает из ротации или остаётся без трофейных скинов.
kinds = set()
for f, src in scripts():
    kinds |= set(re.findall(r"BossKind Kind => BossKind\.(\w+)", src))
declared = set(re.findall(r"(\w+)\s*=\s*\d+,", read("Assets/Scripts/IBoss.cs")))
orphans = declared - kinds
check("Боссы: в ротации и с идентификаторами",
      OK if count == len(kinds) == len(declared) and not orphans else FAIL,
      "в массиве %d, с Kind %d, в BossKind %d%s" % (count, len(kinds), len(declared),
          ("; без контроллера: " + ", ".join(sorted(orphans))) if orphans else ""))

# Финал должен быть заперт: иначе «Великая тень» выходит десятым боссом
# у игрока, который и половины остальных не видел.
gm = read("Assets/Scripts/GameManager.cs")
check("Боссы: финал открывается только после всех",
      OK if "FinaleUnlocked()" in gm and "BossKind.Manta" in gm else FAIL,
      "PickBoss обязан пропускать Великую тень, пока маска побед неполная")

# Справочник считает глубину босса из его места в Codex.Order, поэтому этот список
# обязан повторять массив из GameBootstrap. Разойдутся — игрок получит статью с
# чужой глубиной и откроет её не на том боссе.
codex = read("Assets/Scripts/Codex.cs")
order = re.findall(r"BossKind\.(\w+),\s*//", codex)
check("Справочник: порядок совпадает с расписанием",
      OK if len(order) == count == len(set(order)) else FAIL,
      "в справочнике %d, в расписании %d" % (len(order), count))

# Каждой статье нужны все три блока на обоих языках: пустой раздел выглядит как
# недоделка ровно в тот момент, когда игрок пришёл за помощью.
holes = [k for k in order
         for part in ("what", "weak", "how")
         if '"codex.%s.%s"' % (k.lower(), part) not in loc]
check("Справочник: у каждого босса есть статья",
      OK if not holes else FAIL,
      "%d статей" % len(order) if not holes else "нет: " + ", ".join(holes))

# ---------------------------------------------------------------- скины
store = read("Assets/Scripts/SkinStore.cs")
# Перечисляем все способы завести скин: с новыми условиями (Tally, Mastery)
# старый список из двух недосчитывал их и молча пропускал бы переполнение сетки.
maker = r"SkinDef\.(?:Depth|Trophy|Tally|Mastery)\(\s*\""
bub = len(re.findall(maker + r"skin\.bubble", store))
gua = len(re.findall(maker + r"skin\.guardian", store))
tiles = re.search(r"ShopColumns = (\d+);[\s\S]*?ShopRows = (\d+);", ui)
slots = int(tiles.group(1)) * int(tiles.group(2)) if tiles else 0
# --------------------------------------------------------- сцена и конфиг
scene = read("Assets/Scenes/Main.unity")
check("Сцена не хранит устаревших значений конфига",
      OK if not re.search(r"^\s+(piranha|turtle|ink|crab|jelly)\w+:", scene, re.M) else WARN,
      "значения берутся из кода")

# --- реклама: три формата и их места
gm = read("Assets/Scripts/GameManager.cs")
ads = [
    ("Rewarded за жизнь", "_yandex.ShowRewarded(" in gm and "ContinueWithExtraLife" in gm),
    ("Межстраничная на старте партии", "interstitialOnRunStart" in gm),
    ("Межстраничная при входе в меню", "interstitialOnMenu" in gm),
    ("Межстраничная при входе в магазин", "interstitialOnShop" in gm),
    ("Межстраничная на N-й смерти", "interstitialEveryNDeaths" in gm),
    ("Баннер прячется в партии", "GameState.Playing || State == GameState.BossFight" in gm),
]
for name, ok in ads:
    check("Реклама: " + name, OK if ok else FAIL)

# Внутри партии межстраничную звать нельзя ни при каких настройках — это прямой
# запрет площадки. Проверяем тело игрового такта целиком.
in_run = re.search(r"void TickPlaying[\s\S]{0,4000}?\n        \}", gm)
check("Реклама: не вызывается внутри партии",
      OK if in_run and "ShowInterstitial" not in in_run.group(0) else FAIL)

# ------------------------------------------------------------- Google Play
# Вторая площадка ничего не знает про первую, но код у них общий, и сломать
# android-специфику правкой «для веба» проще всего.
android_tools = read("Assets/Scripts/Editor/BubbleRiseAndroidTools.cs")
android_platform = read("Assets/Scripts/AndroidPlatform.cs")
bootstrap = read("Assets/Scripts/GameBootstrap.cs")
ui = read("Assets/Scripts/UIManager.cs")

play = [
    # Формат пакета: Play принимает только Android App Bundle, .apk не примет.
    ("пакет собирается как AAB", "buildAppBundle = true" in android_tools),
    # 64 бита обязательны, ARMv7 добавляет старые телефоны.
    ("обе архитектуры, включая ARM64",
     "ARM64" in android_tools and "ARMv7" in android_tools),
    # Зашитый target SDK однажды станет причиной отказа: Google поднимает планку
    # каждый год, Auto берёт максимум, который умеет установленная Unity.
    ("target SDK не зашит числом", "AndroidApiLevelAuto" in android_tools),
    # Отладочная панель не должна уехать в магазин.
    ("сборка принудительно не Development",
     "EditorUserBuildSettings.development = false" in android_tools),
    # Ключ подписи только из переменных окружения.
    ("ключ подписи не зашит в код",
     "GetEnvironmentVariable" in android_tools and "keystorePass =" in android_tools),
    # Иконка своя: иначе в списке приложений будет логотип Unity.
    ("иконка приложения задаётся", "SetIcons" in android_tools),
]
for name, ok in play:
    check("Google Play: " + name, OK if ok else FAIL)

check("Google Play: ключ подписи не попадёт в git",
      OK if "*.keystore" in read(".gitignore") and "*.jks" in read(".gitignore") else FAIL)

# Игре сеть не нужна, и в карточке не должно быть строки «полный доступ к сети»:
# модули Unity объявляют INTERNET сами, свой манифест его снимает.
manifest = read("Assets/Plugins/Android/AndroidManifest.xml")
# С появлением рекламы сеть нужна по-настоящему. Снятое разрешение ломает AdMob
# молча: в логах это «нет заполнения», а не «нет доступа», и ищут причину долго.
ads = read("Assets/Scripts/AdMobAds.cs")
check("Google Play: разрешение на сеть есть",
      OK if "android.permission.INTERNET" in manifest
            and 'tools:node="remove"' not in manifest else FAIL,
      "без него рекламный SDK молча не загружает объявления")

settings = read("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset")
check("Реклама: идентификатор приложения прописан",
      OK if "adMobAndroidAppId: ca-app-pub-" in settings else FAIL,
      "иначе сборка Android останавливается с ошибкой от плагина")

# Самая дорогая ошибка этого проекта: без строки в манифесте игра не запускается
# вообще. MobileAdsInitProvider стартует раньше нашего кода и падает, одинаково на
# всех устройствах. Вписывать её должен плагин, но однажды он молча этого не сделал,
# и сборка уехала бы в тестирование мёртвой.
app_id = ""
for line in settings.splitlines():
    if line.strip().startswith("adMobAndroidAppId:"):
        app_id = line.split(":", 1)[1].strip()

check("Реклама: идентификатор приложения в манифесте",
      OK if app_id and "com.google.android.gms.ads.APPLICATION_ID" in manifest
            and app_id in manifest else FAIL,
      "без него игра падает на заставке ещё до первого кадра")

# Клик по собственной настоящей рекламе — самая частая причина блокировки AdMob.
units = read("Assets/Scripts/AdUnits.cs")
check("Реклама: в отладке только тестовые блоки",
      OK if "#if UNITY_EDITOR || DEVELOPMENT_BUILD" in units
            and "ca-app-pub-3940256099942544" in units else FAIL,
      "подмена должна быть автоматической, а не памяткой разработчику")

# Баннер поверх поля, по которому таскают стража, запрещён правилами AdMob.
check("Реклама: баннер прячется в партии",
      OK if "SetBannerVisible" in ads and "_banner.Hide()" in ads else FAIL)

# Согласие на сбор данных обязательно в Европе до первого показа.
check("Реклама: окно согласия до инициализации",
      OK if "ConsentInformation.Update" in ads and "LoadAndShowConsentFormIfRequired" in ads else FAIL,
      "персонализированная реклама до ответа игрока в ЕС запрещена")

# Реклама не должна быть поводом застрять на экране поражения.
check("Реклама: отказ не запирает игрока",
      OK if ads.count("onClosed?.Invoke();") >= 3 else FAIL,
      "нет сети или нет объявлений — обратный вызов всё равно происходит")

# На Яндексе частоту межстраничной ограничивала сама площадка. У AdMob такого
# ограничителя нет, а просим мы её из четырёх мест сразу.
check("Реклама: межстраничная не чаще раза в минуту",
      OK if "_nextInterstitialAt" in ads and "MinGapSeconds" in ads
            and "GraceSeconds" in ads else FAIL,
      "иначе три проигрыша подряд дадут три полноэкранных ролика")

# Описание в магазине пережило появление рекламы и продолжало обещать её отсутствие.
# Пометка «Есть реклама» стояла в карточке рядом с абзацем «БЕЗ РЕКЛАМЫ» — для Google это
# вводящая в заблуждение карточка, а не опечатка. Проверяем только блоки текстов, не
# рассуждения вокруг них.
store_blocks = []
inside = False
for line in read("docs/PLAY_DRAFT.md").splitlines():
    if line.strip() == "```":
        inside = not inside
        continue
    if inside:
        store_blocks.append(line.lower())
store_text = "\n".join(store_blocks)

# Та же ложь жила и на странице игры на сайте — её ищет тот же набор слов.
# Страница публичная и указана в карточке как сайт разработчика, так что расхождение
# видно и игроку, и проверяющему.
site_text = read("docs/site/bubble-rise.html").lower()
stale = ["без рекламы", "no ads", "works offline", "no permissions"]

check("Консоль: описание не обещает отсутствие рекламы",
      FAIL if any(s in store_text for s in stale) else OK,
      "карточка с пометкой «Есть реклама» и таким текстом противоречит сама себе")

check("Сайт: страница игры не обещает отсутствие рекламы",
      FAIL if any(s in site_text for s in stale) else OK,
      "страница указана сайтом разработчика в карточке Google Play")

# ---------------------------------------------------------------- монеты и скины
import re as _re

skins = read("Assets/Scripts/SkinStore.cs")
fx = read("Assets/Scripts/SkinFx.cs")
hazards = read("Assets/Scripts/HazardTable.cs")
manager = read("Assets/Scripts/GameManager.cs")
obstacle = read("Assets/Scripts/Obstacle.cs")

# Эффект берётся по индексу скина в каталоге. Разойдись таблицы в длине — часть
# скинов молча получит чужую анимацию, а последние упрутся в край массива.
bubble_count = skins.count("SkinDef.Depth(\"skin.bubble") + skins.count("SkinDef.Tally(\"skin.bubble")              + skins.count("SkinDef.Trophy(\"skin.bubble") + skins.count("SkinDef.Mastery(\"skin.bubble")              + skins.count("SkinDef.Buy(\"skin.buy") // 2
fx_rows = len(_re.findall(r"Def\(SkinMotion\.", fx))

check("Скины: таблица эффектов совпадает с каталогом",
      OK if bubble_count == fx_rows and fx_rows > 0 else FAIL,
      "скинов %d, строк эффектов %d — расхождение отдаёт скину чужую анимацию"
      % (bubble_count, fx_rows))

# Требование задания: одинаковых эффектов быть не должно ни у одного скина.
pairs = _re.findall(r"Def\(SkinMotion\.(\w+),\s+SkinTrail\.(\w+)", fx)
check("Скины: ни одного повторяющегося эффекта",
      OK if len(pairs) == len(set(pairs)) else FAIL,
      "пара «движение + след» обязана быть уникальной у каждого скина")

# Особая анимация уничтожения — привилегия легендарных. Если она появится у
# эпического, разница между уровнями редкости перестанет быть видимой.
# Легендарных два рода: покупаемые (редкость в строке каталога) и награда за
# полную коллекцию — у неё редкость задана в самой фабрике, а не в строке.
legendary_fx = len(_re.findall(r"Finisher\.(?!None)\w+\)", fx))
legendary_skins = skins.count("Rarity.Legendary") // 2 + skins.count("SkinDef.Mastery(") // 2
check("Скины: финишеры только у легендарных",
      OK if legendary_fx == legendary_skins else FAIL,
      "финишеров %d, легендарных скинов %d" % (legendary_fx, legendary_skins))

# Главное правило экономики. Начисление живёт ровно в двух методах GameManager, и
# любой третий вызов означает, что монеты потекли откуда-то ещё.
earn_calls = manager.count("Wallet.Earn(")
check("Монеты: начисляются только за уничтоженное",
      OK if earn_calls == 2 else FAIL,
      "Wallet.Earn вызывается %d раз; ждём ровно два — препятствие и коралл" % earn_calls)

# Путь «врезался сам» обязан идти через Deflect, который награду выдать не может.
check("Монеты: за столкновение с пузырьком не платят",
      OK if "o.Deflect(away.normalized" in manager and "RewardHazard" in manager else FAIL,
      "иначе игрок зарабатывал бы на собственных ошибках")

# У каждого вида препятствия должен быть свой рисунок: без ветки он молча
# получит камень и станет неотличим от другого вида.
kinds = _re.findall(r"^\s+(\w+),", hazards[hazards.index("public enum ObstacleType"):hazards.index("public enum HazardMotion")],
                    _re.M)
kinds = [k for k in kinds if k not in ("Rock",)]
missing = [k for k in kinds if ("ObstacleType." + k) not in obstacle]
# Диспетчер спрайтов живёт в SpriteFactory: Obstacle только просит вид и
# вариацию. Если у вида нет своей ветки, он молча станет камнем — и заметить
# это можно будет только по тому, что на экране одни камни.
_hazards = read("Assets/Scripts/SpriteFactoryHazards.cs")
_haz_art = read("Assets/Scripts/SpriteFactoryHazardArt.cs")
_variants = int(_re.search(r"HazardVariants = (\d+);", _hazards).group(1))
_no_sprite = [t for t in ("Urchin", "Coral", "Spinner", "Barnacle", "Cinder",
                          "Anchor", "Mine", "Glowtrap", "Vortex", "Jaw")
              if "case ObstacleType.%s:" % t not in _hazards]

check("Препятствия: у каждого вида свой спрайт",
      OK if not _no_sprite else FAIL,
      "вариаций на вид: %d" % _variants if not _no_sprite
      else "без ветки вид молча станет камнем: " + ", ".join(_no_sprite))

# Вариация выбирается один раз за жизнь объекта и не переживает возврат в пул.
_obst = read("Assets/Scripts/Obstacle.cs")
check("Препятствия: вариация не меняется по ходу падения",
      OK if "_variant = Random.Range(0, SpriteFactory.VariantsFor(type));" in _obst
         and _obst.count("_variant = ") == 1 else FAIL,
      "выбирается в Activate, то есть при каждом появлении из пула")

# Две стадии трещин вместо одной проступающей: по прочному бьют дважды-трижды.
check("Препятствия: у прочных есть стадии повреждения",
      OK if "CracksStage(stage)" in _obst and "CracksStage(int stage)" in _haz_art else FAIL,
      "целое, трещины, почти разбито")

# Прочность и награда идут вместе: за три удара платят больше, чем за один.
hp_pairs = _re.findall(r"Hp = (\d+), RewardMin = (\d+)", hazards)
ok_pairs = all(int(r) >= int(h) for h, r in hp_pairs) and len(hp_pairs) >= 11
check("Препятствия: награда не ниже прочности",
      OK if ok_pairs else FAIL,
      "нижняя граница вилки обязана быть не меньше прочности: три удара — минимум три монеты")

# Баланс и покупки должны переживать переустановку и уезжать в облако Яндекса.
save = read("Assets/Scripts/SaveData.cs")
check("Сохранение: монеты и покупки в облаке",
      OK if all(k in save for k in ("coins", "ownedBubble", "ownedGuardian"))             and save.count("CoinsKey") >= 3 else FAIL,
      "иначе на втором устройстве игрок потеряет и деньги, и купленное")

# --------------------------------------------------- материалы, ауры, боссы

materials = read("Assets/Scripts/SpriteFactoryMaterials.cs")
sorting = read("Assets/Scripts/SortingOrders.cs")

# Материал — то, чем скины отличаются друг от друга по-настоящему. Два скина с
# одним материалом снова станут «тем же шариком другого цвета».
mat_used = _re.findall(r"SkinMaterial\.(\w+)\)", skins)
mat_used = [m for m in mat_used if m != "Glass"]
half = len(mat_used) // 2          # каталог продублирован: пузырёк и страж
check("Скины: ни один материал не повторяется",
      OK if len(set(mat_used)) == half and half > 0 else FAIL,
      "материалов %d, уникальных %d" % (half, len(set(mat_used))))

# Каждому материалу нужна своя начинка, иначе скин молча станет пустым стеклом.
declared_mats = _re.findall(r"case SkinMaterial\.(\w+): return Core\w+\(\);", materials)
missing_mats = [m for m in set(mat_used) if m not in declared_mats]
check("Скины: у каждого материала есть рисунок",
      OK if not missing_mats else FAIL,
      "нет начинки: " + ", ".join(missing_mats) if missing_mats else "")

# Таблица аур идёт по индексу материала — расхождение отдаст скину чужое свечение.
aura_rows = len(_re.findall(r"^\s+/\* \w+\s*\*/ D\(", fx, _re.M))
check("Скины: таблица аур покрывает все материалы",
      OK if aura_rows >= len(declared_mats) else FAIL,
      "строк аур %d, материалов %d" % (aura_rows, len(declared_mats)))

# Трофеи с боссов обязаны стоить дороже любого легендарного: их ещё и заслужить надо.
legendary_prices = [int(x) for x in _re.findall(r"Rarity\.Legendary,\s+(\d+)", skins)]
boss_prices = [int(x) for x in _re.findall(r"SkinArt\.\w+,\s+(\d+)\)", skins)]
check("Скины: трофеи дороже легендарных",
      OK if boss_prices and legendary_prices and min(boss_prices) > max(legendary_prices) else FAIL,
      "дешёвый трофей %s против дорогого легендарного %s"
      % (min(boss_prices) if boss_prices else "?", max(legendary_prices) if legendary_prices else "?"))

# Старые трофеи стали платными — у тех, кто их уже заслужил, отнимать нельзя.
check("Скины: прежние трофеи не отбираются",
      OK if "MigrateBossTrophies" in skins and manager.count("MigrateBossTrophies") >= 2 else FAIL,
      "перенос обязан вызываться и при старте, и после загрузки облака")

# Монеты лежали за игровым слоем — ровно та жалоба, ради которой завели свой слой.
check("Монеты: рисуются поверх игрового слоя",
      OK if "public const int Coin" in sorting and "SortingOrders.Coin" in read("Assets/Scripts/CoinFx.cs") else FAIL,
      "иначе награда улетает за рыб и за пузырёк")

# ------------------------------------------------- удар стража и витрина

runtime = read("Assets/Scripts/SkinFxRuntime.cs")

# Удар обязан рождаться в точке касания. Центр стража или центр препятствия — это
# «вспышка рядом», а не столкновение двух тел.
check("Удар: эффект в точке касания",
      OK if "GuardianImpact" in manager and "_guardian.Center + n *" in manager else FAIL,
      "точка считается по линии между центрами, а не берётся готовой")

# Страж бьёт по нескольку раз в секунду, по строю кораллов — по десятку.
check("Удар: частота ограничена",
      OK if "_impactsThisSecond" in runtime and "_lastImpact" in runtime else FAIL,
      "без потолка экран заливает искрами и следующая угроза теряется")

# Два скина с одинаковой парой «рисунок удара + цвет» снова станут неразличимы.
# Подпись D() растёт по мере появления новых полей — цепляемся за то, что в ней
# остаётся неизменным: цвет и рисунок удара.
impacts = _re.findall(r'"(\w+)",\s+"\w+",\s+\d+,\s+ImpactPattern\.(\w+)', fx)
pairs_impact = [(hot, imp) for hot, imp in impacts]
check("Удар: ни один не повторяется",
      OK if impacts and len(pairs_impact) == len(set(pairs_impact)) else FAIL,
      "строк %d, уникальных %d" % (len(pairs_impact), len(set(pairs_impact))))

# Витрина красила оболочку поверх цветной начинки — материал выглядел бледнее,
# чем в игре. Теперь правило живёт одной строкой в SkinLook.ShellColor, и обе
# стороны берут цвет оттуда.
_visuals = read("Assets/Scripts/SkinVisuals.cs")
check("Витрина: цвет кладётся одним слоем",
      OK if "Core != null ? Color.white : Tint" in _visuals
         and "look.ShellColor" in ui and "bubble.Tint" in gm else FAIL,
      "правило одно на обе стороны — SkinLook.ShellColor")

# Дышал только внешний слой, начинка стояла — слои разъезжались.
check("Витрина: оба слоя живут вместе",
      OK if "_shopPreviewHolder.localScale" in ui and "_shopPreviewCore.rectTransform.localRotation" in ui else FAIL,
      "анимация на держателе, вращение — на начинке")

# ------------------------------------------------------------- словарь

loc_src = read("Assets/Scripts/Loc.cs")
loc_keys = _re.findall(r'\{ "([\w.]+)",\s+new\[\]', loc_src)

# Повтор ключа валит словарь в статическом конструкторе, то есть игра не доживает
# до первого кадра. Компилятор такого не видит, а поймать можно было только запуском.
import collections as _collections
dupes = [k for k, n in _collections.Counter(loc_keys).items() if n > 1]
check("Словарь: ни одного повторяющегося ключа",
      OK if not dupes else FAIL,
      "строк %d" % len(loc_keys) if not dupes else "повторы: " + ", ".join(dupes))

# Ключ без строки показывается игроку как есть — «shop.demo» вместо надписи.
used = set()
for _f, _src in scripts():
    used |= set(_re.findall(r'Loc\.Get\("([\w.]+)"\)', _src))
missing_keys = sorted(k for k in used if k not in loc_keys)
check("Словарь: у каждого ключа есть строка",
      OK if not missing_keys else FAIL,
      "использовано %d ключей" % len(used) if not missing_keys
      else "нет перевода: " + ", ".join(missing_keys[:6]))

# ------------------------------------------------------ раскладка магазина

# Опорная точка у Anchor верхняя: блок висит ВНИЗ от указанного Y. Пока это
# понималось как «по центру», экран разъехался — вкладки редкостей оказались внутри
# панели предпросмотра, кнопка «Назад» легла на листалку, подписи наложились.
# Здесь полосы считаются по той же формуле и проверяются на пересечение.
layout = read("Assets/Scripts/ShopLayout.cs")

def _const(name, default=0.0):
    m = _re.search(r"%s\s*=\s*(-?[0-9.]+)f?;" % name, layout)
    return float(m.group(1)) if m else default

bands = []
for name in ("Header", "TypeTabs", "TabHint", "Preview", "Rarity", "Sort", "List"):
    top = _const(name + "Y")
    height = _const(name + "H")
    bands.append((name, top, top - height))

# «Назад» привязан к низу — переводим в ту же систему координат.
content_h = _const("ContentHeight", 1010)
back_bottom = _const("BackBottom", 40)
back_h = _const("BackH", 80)
back_top = -(content_h - back_bottom - back_h)
bands.append(("Back", back_top, back_top - back_h))

overlaps = []
for i in range(len(bands)):
    for j in range(i + 1, len(bands)):
        a, b = bands[i], bands[j]
        if a[2] < b[1] and b[2] < a[1]:
            overlaps.append("%s×%s" % (a[0], b[0]))

check("Магазин: полосы не накладываются",
      OK if bands and not overlaps else FAIL,
      "полос %d" % len(bands) if not overlaps else "пересечения: " + ", ".join(overlaps))

# Карточки живут внутри окна списка и обрезаются маской — проверяем, что окно
# вообще объявлено и что прокрутка на месте, а не вернулась листалка.
# Панель выбранного скина делится на две колонки: слева шарики, справа текст.
# Метки характеристик, поставленные по левому краю, однажды легли прямо на шарик —
# Anchor считает X как ЦЕНТР блока, а не как левый край. Проверяем просвет.
_left_x = _const("PreviewLeftX", 84)
_ball = _const("PreviewBallSize", 112)
_right_x = _const("PreviewRightX", 314)
_right_w = _const("PreviewRightW", 292)
_panel_w = _const("ContentWidth", 500) - _const("SidePadding", 12) * 2

_left_edge = _left_x + _ball * 0.5
_right_edge = _right_x - _right_w * 0.5
check("Магазин: колонки панели не налезают друг на друга",
      OK if _right_edge >= _left_edge + 8 and _right_x + _right_w * 0.5 <= _panel_w else FAIL,
      "шарик до %.0f, текст с %.0f, панель шириной %.0f" % (_left_edge, _right_edge, _panel_w))

# Правая колонка идёт сверху вниз одной лесенкой. Если какая-то строка переедет
# на соседнюю, это снова будет «подписи наложились» — то, с чего всё начиналось.
_rows = [("имя", _const("PreviewNameY"), 26),
         ("редкость", _const("PreviewRarityY"), 18),
         ("статус", _const("PreviewStatusY"), 26),
         ("метки", _const("PreviewTraitsY"), 16 + 3 * _const("PreviewTraitStep", 18)),
         ("кнопка", _const("PreviewActionY"), _const("PreviewActionH", 42)),
         ("примерить", _const("PreviewButtonsY"), _const("PreviewButtonsH", 36))]
_bad = []
for _i in range(len(_rows) - 1):
    _name, _top, _h = _rows[_i]
    _nextName, _nextTop, _ = _rows[_i + 1]
    if _top - _h < _nextTop:
        _bad.append("%s + %s" % (_name, _nextName))

_lastTop, _lastH = _rows[-1][1], _rows[-1][2]
if _lastTop - _lastH < -_const("PreviewH", 248):
    _bad.append("нижняя строка вне панели")

check("Магазин: строки панели идут лесенкой",
      OK if not _bad else FAIL,
      "строк %d, низ на %.0f при высоте %.0f" % (len(_rows), _lastTop - _lastH, _const("PreviewH", 248))
      if not _bad else "пересечения: " + ", ".join(_bad))

# Строка редкостей обязана быть видна всегда. Выпадающая панель накрывала
# верхний ряд карточек, а до фильтров нужно было сначала догадаться.
check("Магазин: строка редкостей всегда видна",
      OK if "ShopLayout.RarityY" in ui and "BuildFilterPanel" not in ui
         and "_shopFilterPanel" not in ui else FAIL,
      "шесть чипов в ряд, без выпадающей панели поверх списка")

# Шесть чипов обязаны помещаться в ширину холста: ряд прокручиваемый, но
# прокрутка нужна только на случай седьмой редкости, а не каждый день.
_chips = int(_const("FilterCount", 6))
_chip_step = _const("FilterChipStepX", 80)
check("Магазин: чипы редкости помещаются в ряд",
      OK if _chips * _chip_step <= _const("ContentWidth", 500) else FAIL,
      "%d по %.0f = %.0f при ширине %.0f"
      % (_chips, _chip_step, _chips * _chip_step, _const("ContentWidth", 500)))

# Сортировка не должна накрывать карточки ни на мгновение.
check("Магазин: сортировка не перекрывает список",
      OK if _const("SortY") - _const("SortH") >= _const("ListY") else FAIL,
      "полоса сортировки кончается на %.0f, список начинается с %.0f"
      % (_const("SortY") - _const("SortH"), _const("ListY")))

# Выключенная кнопка должна оставаться кнопкой, а не превращаться в текст,
# висящий поверх панели: именно так читалось «ВЫБРАНО» в витрине.
check("Магазин: выключенная кнопка видна как кнопка",
      OK if "DisabledFill" in read("Assets/Scripts/UIManagerRewardsLogic.cs")
         and "enabledFill" in ui else FAIL,
      "у выключенного состояния свой тон, а не прозрачность")

# Порядок по умолчанию — порядок полезности, а не порядок каталога.
check("Магазин: сначала то, что можно купить",
      OK if "int ShopRank(" in ui and "_shopRecommended" in ui else FAIL,
      "каталог начинался с давно купленных скинов")

# Вкладки без пояснения не объясняли ничего: «Пузырь» и «Шарик» — это две разные
# вещи, и какая из них чья, по названию не понять.
check("Магазин: у вкладок есть пояснение",
      OK if "shop.hintBubble" in ui and "shop.hintGuardian" in ui and "TabIcon(" in ui else FAIL,
      "иконка и строка под вкладками")

check("Магазин: список прокручивается, а не листается",
      OK if "ScrollRect" in ui and "RectMask2D" in ui and "_shopPage" not in ui else FAIL,
      "страницы заставляли угадывать, на какой из них нужный скин")

# ------------------------------------------------- статусы препятствий

status_src = read("Assets/Scripts/StatusFx.cs")

# Статус жил внутри Obstacle и доставался только обломкам. Блоки строя, сегменты
# шпилей, шары локаций и рыбы оставались невозмутимыми, сколько по ним ни бей, —
# ровно то, что было видно на экране. Теперь это общий компонент, и проверка
# следит, чтобы его звали для каждого вида, а не для одного.
ignite_calls = manager.count("Ignite(")
check("Статус: накладывается на все виды препятствий",
      OK if ignite_calls >= 6 else FAIL,
      "вызовов Ignite %d; нужны обломок, блок строя, сегмент шпиля, шар локации, рыба" % ignite_calls)

# Накладка берёт спрайт самой цели — отсюда совпадение с формой и вращение даром.
check("Статус: повторяет форму препятствия",
      OK if "Skin.sprite = source.sprite" in status_src else FAIL,
      "иначе слой не совпадёт с силуэтом и поедет при вращении")

# Цель может уйти в пул раньше статуса: слой обязан отцепиться сам, иначе уедет
# вместе с переиспользованным объектом и загорится на другом препятствии.
check("Статус: отцепляется от цели, ушедшей в пул",
      OK if "activeInHierarchy" in status_src and "SetParent(transform, false)" in status_src else FAIL,
      "иначе горение переедет на чужой объект")

# Три статуса на экране — потолок. Было восемь: столько горящих и бьющих током
# препятствий разом читались уже не как эффект скина, а как пёстрая каша поверх
# того, от чего игрок уворачивается.
_max_active = int(_re.search(r"const int MaxActive = (\d+);", status_src).group(1))
check("Статус: число одновременных ограничено",
      OK if 1 <= _max_active <= 3 else FAIL,
      "одновременно не больше %d" % _max_active)

# Длительность статуса тоже под потолком: в таблице материалов есть значения до
# четырёх секунд, а просили не больше трёх.
_max_dur = float(_re.search(r"MaxDuration = ([0-9.]+)f;", status_src).group(1))
check("Статус: длительность не больше трёх секунд",
      OK if _max_dur <= 3.0 and "StatusFx.MaxDuration" in gm else FAIL,
      "потолок %.1f с, применяется в Ignite" % _max_dur)

# Два скина с одинаковой парой «статус + цвет» снова станут неразличимы.
statuses = _re.findall(r'"(\w+)",\s+"\w+",\s+\d+,\s+ImpactPattern\.\w+,\s+StatusKind\.(\w+)', fx)
check("Статус: ни один не повторяется",
      OK if statuses and len(statuses) == len(set(statuses)) else FAIL,
      "строк %d, уникальных %d" % (len(statuses), len(set(statuses))))

# --------------------------------------------------- один путь отрисовки скина

# Путей отрисовки было два: игра складывала слои в GameManager.ApplySkins,
# витрина — в UIManager.RefreshPreview. Они разошлись, и страж в бою носил
# материал пузырька. Теперь вид собирает только SkinVisuals, и обойти его
# нельзя, не нарушив эту проверку.
fx_src = read("Assets/Scripts/SkinFxRuntime.cs")
visuals_src = read("Assets/Scripts/Visuals.cs")

_hand_built = []
for _f, _src in (("GameManager.cs", gm), ("UIManager.cs", ui)):
    if "SpriteFactory.MaterialCore(" in _src:
        _hand_built.append(_f + ": MaterialCore")
    if "SpriteFactory.BubbleShell(" in _src or "SpriteFactory.GuardianShell(" in _src:
        _hand_built.append(_f + ": Shell")

check("Скины: вид собирает только SkinVisuals",
      OK if not _hand_built and "SkinVisuals." in gm and "SkinVisuals." in ui else FAIL,
      "игра и витрина зовут одну функцию"
      if not _hand_built else "собирают сами: " + ", ".join(_hand_built))

# Половинки выбираются независимо, и каждая обязана брать СВОЙ материал. Раньше
# обе получали материал пузырька: у игрока со стеклянной «Пеной» купленный страж
# оставался в бою вообще без начинки.
check("Скины: у каждой половины свой материал",
      OK if "SkinVisuals.Bubble()" in gm and "SkinVisuals.Guardian()" in gm
         and "_bubbleLook" in fx_src and "_guardianLook" in fx_src else FAIL,
      "пузырёк и страж разрешаются по отдельности")

# Удар, звук, вибрация и статус — от стража: он единственный, кто бьёт. Раньше
# брались от пузырька, и витрина на вкладке «Шарик» обещала не то.
_from_guardian = all(("_guardianLook.Material." + _p) in fx_src
                     for _p in ("Status", "Impact", "Hot", "StatusTime"))
check("Скины: удар и статус идут от стража",
      OK if _from_guardian and "_guardianLook.Archetype" in fx_src else FAIL,
      "по препятствиям бьёт страж, значит и стихия на них его")

# Разрушение — от пузырька: его редкость решает, насколько зрелищно рассыпается
# цель, и смотрит игрок весь заплыв именно на него.
check("Скины: разрушение идёт от пузырька",
      OK if "PlayDestroy(_bubbleLook.Material" in fx_src
         and "_bubbleLook.Rarity" in fx_src else FAIL,
      "редкость пузырька решает зрелищность финала")

# Движение, след и вращение начинки обязаны идти в витрине и в игре одинаково.
check("Скины: движение и след общие у витрины и игры",
      OK if "SkinFxRuntime.MotionScale" in ui and "SkinFxRuntime.SampleTrail" in ui
         and "SkinFxRuntime.AuraIntensity" in ui else FAIL,
      "витрина зовёт те же функции, а не повторяет формулы")

check("Скины: начинка вращается с одной скоростью",
      OK if "SkinVisuals.CoreSpin" in ui
         and "SkinVisuals.CoreSpin" in read("Assets/Scripts/BubbleController.cs")
         and "SkinVisuals.CoreSpin" in read("Assets/Scripts/GuardianOrb.cs") else FAIL,
      "было 14 у пузырька, 20 у стража и 14 в витрине")

# Множитель размера игрока применяется ровно один раз на слой. Дважды — и слои
# скина разъедутся: тело уменьшится вдвое сильнее следа.
_scale_uses = {_f: read("Assets/Scripts/" + _f).count("PlayerSize.Scale")
               for _f in ("BubbleController.cs", "GuardianOrb.cs", "SkinFxRuntime.cs",
                          "ObstacleSpawner.cs", "PlayerSize.cs")}
check("Скины: множитель 0.8 применяется один раз на слой",
      OK if _scale_uses["BubbleController.cs"] == 0 and _scale_uses["GuardianOrb.cs"] <= 1
         and _scale_uses["SkinFxRuntime.cs"] >= 2 else FAIL,
      "тело через PlayerSize.Bubble/Guardian, след и частицы удара — множителем: " +
      ", ".join("%s=%d" % (_k, _v) for _k, _v in sorted(_scale_uses.items())))

# Базовый образ скина не должен отключаться ни одной настройкой: иначе в тихом
# режиме все тридцать шесть скинов выглядят одинаково.
check("Скины: щадящий режим не стирает базовый образ",
      OK if "BaseFloor" in visuals_src and "Visuals.BaseFloor" in fx_src
         and "Visuals.Calm" not in fx_src else FAIL,
      "аура приглушается, но не гаснет; след и движение не трогаются вовсе")

# Два объявленных Activity с категорией LAUNCHER = две иконки в списке приложений.
check("Google Play: одна точка входа",
      OK if manifest.count("android.intent.category.LAUNCHER") == 1 else FAIL,
      "иначе игра появится в списке приложений дважды")

android_rt = [
    ("площадка выбирается препроцессором",
     "UNITY_ANDROID" in bootstrap and "AndroidPlatform" in bootstrap),
    # Вертикальная игра в альбомной ориентации превращается в полоску.
    ("портрет запирается в рантайме", "ScreenOrientation.Portrait" in android_platform),
    # Свернули игру — она обязана замереть и замолчать.
    ("пауза при сворачивании", "OnApplicationPause" in android_platform),
    # Прогресс может не пережить закрытие процесса системой.
    ("сохранение при сворачивании", "PlayerPrefs.Save()" in android_platform),
    # Кнопка ролика показывается, только когда ролик реально заряжен: нажатие,
    # ведущее к «реклама недоступна», сразу после проигрыша — второе разочарование.
    ("кнопка ролика только при заряженном ролике",
     "_ads.RewardedReady" in android_platform),
]
for name, ok in android_rt:
    check("Android: " + name, OK if ok else FAIL)

check("Android: интерфейс держится в безопасной зоне",
      OK if "Screen.safeArea" in ui and "renderOutsideSafeArea" in android_tools else FAIL,
      "иначе HUD уедет под вырез, а кнопки под полосу жестов")

# Смена языка пересобирает интерфейс целиком, а память о прежнем размере
# безопасной зоны живёт в UIManager и пересборку переживает. Без сброса новый
# прямоугольник остаётся размером со свежий RectTransform, и кнопки паузы и
# звука схлопываются в центр экрана. Ловили это уже на живом телефоне.
# Два вхождения: объявление поля и сброс при пересборке. Одно означает, что
# сброс потеряли.
check("Интерфейс: безопасная зона пересчитывается после пересборки",
      OK if ui.count("_lastSafeArea = new Rect") >= 2 and "Stretch(_safeArea)" in ui else FAIL,
      "иначе после смены языка кнопки уезжают в центр экрана")

# Разгон прокрутки без потолка растёт всю партию: на десяти тысячах метров экран
# обновлялся бы дважды в секунду, и дело было бы не в мастерстве, а в том, что
# увидеть летящее физически не успеваешь.
check("Сложность: у скорости прокрутки есть потолок",
      OK if "Mathf.Min(_speedCap," in gm
            and "_cfg.scrollAcceleration * Difficulty.Current.accelerationScale" in gm
            and "Mathf.Max(1f, _cfg.maxScrollSpeedGrowth)" in gm else FAIL,
      "разгон упирается в текущий потолок, а сам потолок — в предел роста за боссов")

audio = read("Assets/Scripts/AudioManager.cs")
check("Звук: музыка возвращается той темой, что уместна",
      OK if "_wantedMusic" in audio and "_wantedMusic = clip" in audio else FAIL,
      "иначе включение музыки посреди боя даёт спокойную тему подъёма")
check("Звук: глушилка обрывает и уже звучащее",
      OK if re.search(r"if \(!on && _pool != null\)", audio) else FAIL,
      "иначе длинный эффект доигрывает после нажатия, и кнопка кажется сломанной")

gm = read("Assets/Scripts/GameManager.cs")
check("Android: системная кнопка «Назад» обработана",
      OK if "BackPressed()" in gm and "TryGoBack" in ui else FAIL,
      "из партии — пауза, из экрана — шаг назад, из меню — выход")

# -------------------------------------------------- тексты для консоли Яндекса
# Поля в консоли с жёсткими лимитами, и превышение обнаруживается только при
# попытке сохранить форму — то есть уже руками и в последний момент. Черновик
# держит лимит прямо в заголовке блока, оттуда его и читаем.
draft = read("docs/YANDEX_DRAFT.md")
play_draft = read("docs/PLAY_DRAFT.md")
blocks = re.findall(r'### ([^\n(]+)\(до (\d+)\)\s*\n\s*```\n(.*?)\n```', draft + play_draft, re.S)
over = ["%s %d>%s" % (n.strip(), len(b.strip()), lim)
        for n, lim, b in blocks if len(b.strip()) > int(lim)]
check("Консоль: тексты влезают в лимиты полей",
      OK if blocks and not over else FAIL,
      "%d блоков в обеих консолях" % len(blocks) if not over
      else "длиннее лимита: " + ", ".join(over))

# Число боссов в описании обязано совпадать с игрой: расхождение — это то самое
# несоответствие описания игре, за которое площадка возвращает сборку.
words = {8: ["восемь", "восьмерых", "eight"], 9: ["девять", "девятерых", "nine"],
         10: ["десять", "десятерых", "ten"]}
wrong = [w for n, ws in words.items() if n != count for w in ws
         if re.search(r"\b%s\b\s+босс|\b%s\b\s+bosses|побеждай %s|Их %s\b|There are %s" % (w, w, w, w, w),
                      draft, re.I)]
check("Консоль: число боссов в описании верное",
      OK if not wrong else FAIL,
      "боссов в игре %d" % count if not wrong else "в тексте: " + ", ".join(sorted(set(wrong))))

# --------------------------------------------------- система возвращения

tables = read("Assets/Scripts/RewardTables.cs")
hub = read("Assets/Scripts/RewardsHub.cs")
state_src = read("Assets/Scripts/RewardState.cs")
timesrc = read("Assets/Scripts/TimeService.cs")
rl = read("Assets/Scripts/RewardsLayout.cs")
rewards_ui = read("Assets/Scripts/UIManagerRewards.cs")
rewards_logic = read("Assets/Scripts/UIManagerRewardsLogic.cs")
notif = read("Assets/Scripts/NotificationService.cs")

# --- экономика. Главное правило этой правки: награды добавляют 20–35 % сверх
# дневного заработка игрой, иначе магазин перестаёт зарабатываться игрой. Числа
# разбросаны по таблицам, и сойти с этой доли легче всего незаметно — поэтому
# доля считается здесь заново, из самих таблиц, а не сверяется с комментарием.

def _rl_const(name, default=0.0):
    m = _re.search(r"%s\s*=\s*(-?[0-9.]+)f?;" % name, rl)
    return float(m.group(1)) if m else default

def _tbl_const(name, default=0):
    m = _re.search(r"%s\s*=\s*(-?[0-9]+)\s*;" % name, tables)
    return int(m.group(1)) if m else default

def _weighted(block_name):
    m = _re.search(r"%s\s*=\s*\{(.*?)\};" % block_name, tables, _re.S)
    if not m:
        return 0.0
    pairs = _re.findall(r"\(([0-9.]+)f,\s*(\d+)\)", m.group(1))
    total = sum(float(w) for w, _ in pairs)
    return sum(float(w) * int(c) for w, c in pairs) / total if total else 0.0

spin_worth = _tbl_const("SpinWorth", 11)
chest_worth = _tbl_const("ChestWorth", 11)

chest_avg = _weighted("ChestLoot")
wheel_avg = _weighted("WheelSectors")

# Календарь: Coins берём числом, Spin и Chest — по объявленной стоимости.
cal = _re.search(r"DailyCalendar\s*=\s*\{(.*?)\};", tables, _re.S)
cal_items = _re.findall(r"RewardGrant\.(Coins\((\d+)\)|Spin\(\)|Chest\(\))", cal.group(1)) if cal else []
cal_total = 0
for kind, coins in cal_items:
    if kind.startswith("Coins"):
        cal_total += int(coins)
    elif kind.startswith("Spin"):
        cal_total += spin_worth
    else:
        cal_total += chest_worth
cal_avg = cal_total / len(cal_items) if cal_items else 0.0

# Квесты: по одному каждой сложности в день, плюс бонус за все три.
daily_block = _re.search(r"DailyQuests\s*=\s*\{(.*?)\n        \};", tables, _re.S)
daily_rewards = {}
for m in _re.finditer(r"Reward = (\d+),\s*Tier = (\d+)", daily_block.group(1) if daily_block else ""):
    daily_rewards.setdefault(int(m.group(2)), int(m.group(1)))
quests_day = sum(daily_rewards.values()) + _tbl_const("DailySweepReward", 0)

weekly_block = _re.search(r"WeeklyQuests\s*=\s*\{(.*?)\n        \};", tables, _re.S)
weekly_day = sum(int(x) for x in _re.findall(r"Reward = (\d+)", weekly_block.group(1) if weekly_block else "")) / 7.0

# Сундуки. Считать «три за вечер» неверно: сундук зреет час и копится до трёх,
# поэтому за день их забирают столько, сколько раз зашли. Берём два захода —
# утром и вечером: это и есть поведение, ради которого система сделана.
# Прогон по дням — в Tools/simulate_rewards.py.
VISITS_PER_DAY = 2
chest_day = chest_avg * _tbl_const("ChestMaxStored", 3) * VISITS_PER_DAY
wheel_day = wheel_avg * _tbl_const("FreeSpinsPerDay", 1)

rewards_day = chest_day + cal_avg + quests_day + wheel_day + weekly_day

# Дневной заработок игрой: заплыв до первого босса и четыре заплыва за вечер.
PLAY_PER_RUN = 145.0
RUNS_PER_DAY = 4
play_day = PLAY_PER_RUN * RUNS_PER_DAY
share = rewards_day / play_day * 100.0

check("Награды: доля в дневном заработке 20–35 %",
      OK if 20.0 <= share <= 35.0 else FAIL,
      "%.0f монет наград против %.0f игрой — %.1f %%" % (rewards_day, play_day, share))

# Кто заходит чаще, забирает больше сундуков — это замысел, а не дыра. Но и у
# такого игрока доля обязана остаться внутри полосы, иначе магазин начнёт
# зарабатываться расписанием.
_heavy = rewards_day + chest_avg * _tbl_const("ChestMaxStored", 3)
_heavy_share = _heavy / play_day * 100.0
check("Награды: три захода в день не выбивают долю",
      OK if _heavy_share <= 35.0 else FAIL,
      "%.0f монет — %.1f %%" % (_heavy, _heavy_share))

# --- кошелёк. Правило «монеты только за уничтоженное» теперь звучит иначе: есть
# ровно два входа, и видно, какой из них чей. Если награды начнут звать Earn,
# заработок за заплыв на экране итогов станет враньём, а доля выше — неверной.
earn_callers = [f for f, src in scripts(runtime_only=True)
                if _re.search(r"Wallet\.Earn\(", src)]
reward_callers = [f for f, src in scripts(runtime_only=True)
                  if "Wallet.EarnFromReward(" in src]
check("Награды: монеты идут через отдельный вход",
      OK if sorted(os.path.basename(f) for f in earn_callers) == ["GameManager.cs"]
         and sorted(os.path.basename(f) for f in reward_callers) == ["RewardsHub.cs"] else FAIL,
      "Earn: %s; EarnFromReward: %s" % (
          ", ".join(sorted(os.path.basename(f) for f in earn_callers)) or "никто",
          ", ".join(sorted(os.path.basename(f) for f in reward_callers)) or "никто"))

# --- выдача. Порядок «сначала на диск, потом монеты» — единственное, что стоит
# между игроком и наградой, которая «была и пропала» после вылета.
commit = _re.search(r"static void Commit\(RewardGrant grant\)\s*\{(.*?)\n        \}", hub, _re.S)
commit_body = commit.group(1) if commit else ""
check("Награды: состояние пишется до начисления",
      OK if "RewardStore.Save()" in commit_body and "Wallet.EarnFromReward" in commit_body
         and commit_body.find("RewardStore.Save()") < commit_body.find("Wallet.EarnFromReward") else FAIL,
      "иначе вылет между начислением и записью съедает награду")

# --- время. Перевод часов назад не должен открывать календарь за минуту.
check("Награды: перевод часов назад не даёт наград",
      OK if "RollbackDetected" in timesrc and "if (TimeService.RollbackDetected) return;" in hub else FAIL,
      "монотонный счётчик против часов устройства")

check("Награды: сутки считаются по UTC",
      OK if "DateTimeKind.Utc" in timesrc and "DateTime.UtcNow" in timesrc
         and "DateTime.Now" not in timesrc else FAIL,
      "смена часового пояса не должна ни рвать серию, ни дарить день")

# Дев-сдвиг обязан жить отдельно от якоря: иначе проверка суток выглядит для
# самой системы как перевод часов, и проверять становится нечего.
check("Награды: отладочный сдвиг не выглядит как обман",
      OK if "_devOffset" in timesrc and "UNITY_EDITOR || DEVELOPMENT_BUILD" in timesrc else FAIL,
      "сдвиг отдельно от последнего виденного времени")

# --- сохранение
check("Награды: состояние переживает перезапуск",
      OK if "SaveData.RewardsKey" in state_src and "JsonUtility.ToJson" in state_src else FAIL,
      "одной строкой JSON в том же PlayerPrefs, что и профиль")

check("Награды: битое сохранение не роняет запуск",
      OK if "catch (Exception)" in state_src and "Normalize(" in state_src else FAIL,
      "подпись не сошлась или таблица выросла — начинаем заново, а не падаем")

# --- квесты
check("Награды: невыполнимый квест не выдаётся",
      OK if "NeedsBoss" in hub and "PickStatus" in hub and "SkinStore.IsUnlocked" in hub else FAIL,
      "боссы — только видевшим боссов, стихия — только доступная игроку")

check("Награды: набор квестов не перетряхивается перезапуском",
      OK if "new System.Random(seed" in hub and "TimeService.DayNumber" in hub else FAIL,
      "иначе игра перезапускалась бы, пока не выпадут удобные задания")

# Прогресс в бою не должен будить событие на каждое препятствие: за партию их
# сотни, и значок пересчитывался бы сотни раз.
check("Награды: событие будится по выполнению, а не по каждому шагу",
      OK if "if (!quest.Done) continue;" in hub and "if (finished) Changed?.Invoke();" in hub else FAIL,
      "иначе сотни пересчётов значка за партию")

# Доп. жизнь за рекламу продолжает тот же заплыв, и GameOver по нему случается
# дважды. Без разницы метры и монеты первой половины зачлись бы второй раз.
check("Награды: доп. жизнь не удваивает зачёт заплыва",
      OK if "_reportedDistance" in manager and "_reportedCoins" in manager
         and "ReportCoinsEarned" in manager else FAIL,
      "иначе «проплыть 3000 м» закрывается одним заплывом на 1600 и рекламой")

# --- раскладка экрана: та же проверка, что спасла магазин
rbands = []
for name in ("Header", "Tabs", "Page"):
    top = _rl_const(name + "Y")
    height = _rl_const(name + "H")
    rbands.append((name, top, top - height))

r_content_h = _rl_const("ContentHeight", 1010)
r_back_top = -(r_content_h - _rl_const("BackBottom", 40) - _rl_const("BackH", 80))
rbands.append(("Back", r_back_top, r_back_top - _rl_const("BackH", 80)))

r_overlaps = []
for i in range(len(rbands)):
    for j in range(i + 1, len(rbands)):
        a, b = rbands[i], rbands[j]
        if a[2] < b[1] and b[2] < a[1]:
            r_overlaps.append("%s + %s" % (a[0], b[0]))

check("Награды: полосы экрана не накладываются",
      OK if rbands and not r_overlaps else FAIL,
      "полос %d" % len(rbands) if not r_overlaps else "пересечения: " + ", ".join(r_overlaps))

# Вкладка «Подарки» делит страницу на две панели — они тоже обязаны не лезть
# друг на друга и не вылезать за низ страницы.
chest_top, chest_h = _rl_const("ChestPanelY"), _rl_const("ChestPanelH")
cal_top, cal_h = _rl_const("CalendarPanelY"), _rl_const("CalendarPanelH")
page_h = _rl_const("PageH", 744)
gifts_ok = (chest_top - chest_h >= cal_top) and (cal_top - cal_h >= -page_h)
check("Награды: сундук и календарь помещаются на странице",
      OK if gifts_ok else FAIL,
      "сундук %.0f…%.0f, календарь %.0f…%.0f, страница до %.0f" % (
          chest_top, chest_top - chest_h, cal_top, cal_top - cal_h, -page_h))

# Клетки календаря: четыре в ряд должны влезать по ширине панели.
cell_step = _rl_const("CellStepX", 112)
cells_top = _rl_const("CellsTopRow", 4)
panel_w = _rl_const("ContentWidth", 500) - _rl_const("SidePadding", 12) * 2
check("Награды: ряд календаря влезает по ширине",
      OK if cells_top * cell_step <= panel_w else FAIL,
      "%d по %.0f = %.0f при ширине панели %.0f" % (cells_top, cell_step, cells_top * cell_step, panel_w))

# Полоса прогресса идёт слева, кнопка «Забрать» прижата к правому нижнему углу
# карточки — на одной высоте. У достижений полоса однажды оказалась длиннее
# просвета и ушла под кнопку. Считаем обе колонки по таблице.
_gap = _rl_const("ActionGap", 10)
_act_w = _rl_const("ActionW", 132)
_act_m = _rl_const("ActionMargin", 16)
_collisions = []
for _kind, _card_w, _left, _bar in (
        ("задание", _rl_const("QuestCardW", 476), _rl_const("QuestTextLeft"), _rl_const("QuestBarW")),
        ("достижение", _rl_const("AchCardW", 476), _rl_const("AchTextLeft"), _rl_const("AchBarW"))):
    _action_left = _card_w - _act_m - _act_w
    if _left + _bar + _gap > _action_left:
        _collisions.append("%s: полоса до %.0f, кнопка с %.0f" % (_kind, _left + _bar, _action_left))

check("Награды: полоса прогресса не уходит под кнопку",
      OK if not _collisions else FAIL,
      "просвет не меньше %.0f" % _gap if not _collisions else "; ".join(_collisions))

# Списки заданий и достижений длиннее страницы — без прокрутки нижние карточки
# просто недостижимы.
check("Награды: длинные списки прокручиваются",
      OK if "ScrollRect" in rewards_ui and "RectMask2D" in rewards_ui else FAIL,
      "задания и достижения не помещаются на страницу целиком")

# --- интерфейс: каждая вкладка должна быть собрана и переключаться
for _name, _needle in (("подарки", "BuildGiftsPage"), ("задания", "BuildQuestsPage"),
                       ("колесо", "BuildWheelPage"), ("достижения", "BuildAchievementsPage")):
    check("Награды: вкладка «%s» собрана" % _name,
          OK if _needle + "(" in rewards_ui else FAIL)

check("Награды: значок на кнопке меню обновляется",
      OK if "RefreshRewardsBadge" in rewards_logic and "RefreshRewardsBadge();" in ui else FAIL,
      "иначе число готовых наград врёт до перезахода в меню")

# Колесо решает приз до анимации, иначе вылет посреди вращения съедает вращение.
check("Награды: колесо объявляет уже решённое",
      OK if "_popupHeld" in rewards_logic and "RewardsHub.Spin(out int sector" in rewards_logic else FAIL,
      "приз определяется при нажатии, анимация только показывает")

# --- уведомления
check("Награды: уведомления не будят ночью",
      OK if "AvoidQuietHours" in notif and "QuietStartHour" in notif else FAIL,
      "ночное напоминание о пяти монетах — это отключённые уведомления навсегда")

check("Награды: уведомления собираются без пакета",
      OK if "BUBBLERISE_NOTIFICATIONS" in notif and notif.count("#if UNITY_ANDROID") >= 2 else FAIL,
      "пакета уведомлений в проекте нет — без дефайна класс остаётся пустышкой")

# --- локализация: все ключи системы переведены на оба языка
rewards_keys = set(_re.findall(
    r'Loc\.Get\("((?:hub|chest|daily|quest|wheel|ach|back|reward|time|notif|status)\.[a-zA-Z0-9]+)"\)',
    rewards_ui + rewards_logic + hub + notif + manager))
loc_src = read("Assets/Scripts/Loc.cs")
missing_rewards = sorted(k for k in rewards_keys if '"%s"' % k not in loc_src)
check("Награды: все строки переведены",
      OK if rewards_keys and not missing_rewards else FAIL,
      "ключей %d" % len(rewards_keys) if not missing_rewards
      else "нет перевода: " + ", ".join(missing_rewards[:6]))

# --- дев-панель: без неё проверять сутки пришлось бы сутками
dev_src = read("Assets/Scripts/DevPanel.cs")
check("Награды: дев-панель умеет двигать время",
      OK if "TimeService.DevShift" in dev_src and "DevResetChestTimer" in dev_src
         and "DevCompleteQuests" in dev_src and "DevResetAll" in dev_src else FAIL,
      "сдвиг суток, сундук, квесты и полный сброс")

# --------------------------------------------------- сложность и стихии

diff_src = read("Assets/Scripts/DifficultyConfig.cs")
arch_src = read("Assets/Scripts/Archetypes.cs")
shapes_src = read("Assets/Scripts/SpriteFactoryShapes.cs")
haptics_src = read("Assets/Scripts/Haptics.cs")
haz = read("Assets/Scripts/HazardTable.cs")
skinfx = read("Assets/Scripts/SkinFx.cs")


def _diff_preset(name):
    """Числа пресета: у easy — значения полей, у остальных — свой блок."""
    body = diff_src[diff_src.index("public class DifficultyPreset"):
                    diff_src.index("public class DifficultyConfig")]
    values = {}
    for m in _re.finditer(r"public (?:int|float) (\w+) = (-?[0-9.]+)f?;", body):
        values[m.group(1)] = float(m.group(2))
    if name == "easy":
        return values

    blk = _re.search(r"%s = new DifficultyPreset\s*\{(.*?)\n        \};" % name, diff_src, _re.S)
    if blk:
        for m in _re.finditer(r"(\w+)\s*=\s*(-?[0-9.]+)f?,", blk.group(1)):
            values[m.group(1)] = float(m.group(2))
    return values


easy = _diff_preset("easy")
normal = _diff_preset("normal")
hard = _diff_preset("hard")

# Режимы обязаны идти по возрастанию трудности. Один переставленный множитель —
# и «лёгкая» оказывается тяжелее «сложной», причём заметит это только игрок.
_order = []
for _field, _nicer in (("spawnIntervalScale", "больше"), ("warmupMeters", "больше"),
                       ("unlockDepthScale", "больше"), ("coyoteTime", "больше"),
                       ("invulnerabilityTime", "больше"), ("bubbleHitboxScale", "меньше"),
                       ("accelerationScale", "меньше"), ("speedCapScale", "меньше")):
    e, n, h = easy.get(_field, 0), normal.get(_field, 0), hard.get(_field, 0)
    ok = (e >= n >= h) if _nicer == "больше" else (e <= n <= h)
    if not ok:
        _order.append("%s: %.2f/%.2f/%.2f" % (_field, e, n, h))

check("Сложность: режимы идут по возрастанию",
      OK if not _order else FAIL,
      "лёгкая мягче обычной, обычная мягче сложной"
      if not _order else "нарушено: " + "; ".join(_order))

# Проход должен быть заметно шире пузырька — это и просили числом.
check("Сложность: проход шире полутора диаметров пузырька",
      OK if easy["minGapInBubbles"] >= 1.5 and normal["minGapInBubbles"] >= 1.5 else FAIL,
      "лёгкая %.1f, обычная %.1f диаметра" % (easy["minGapInBubbles"], normal["minGapInBubbles"]))

# Хитбокс меньше рисунка на 10–15 %: так и просили, и ровно столько, чтобы
# касание по краю прощалось, а промах мимо цели — нет.
_shrink = [1 - easy["bubbleHitboxScale"], 1 - easy["hazardHitboxScale"]]
check("Сложность: хитбоксы меньше рисунка на 10–15 %",
      OK if all(0.09 <= x <= 0.16 for x in _shrink) else FAIL,
      "пузырёк −%.0f %%, препятствие −%.0f %%" % (_shrink[0] * 100, _shrink[1] * 100))

# Сложность обязана быть подключена всюду, где она что-то решает. Один
# пропущенный спавнер — и «пологая кривая» перестаёт быть пологой наполовину.
_wired = [f for f in ("ObstacleSpawner.cs", "FormationSpawner.cs", "SpireSpawner.cs",
                      "ZoneSpawner.cs", "FishSpawner.cs", "GuardianOrb.cs",
                      "BubbleController.cs", "GameManager.cs")
          if "Difficulty." not in read("Assets/Scripts/" + f)]
check("Сложность: подключена во всех спавнерах",
      OK if not _wired else FAIL,
      "восемь мест" if not _wired else "не подключена: " + ", ".join(_wired))

# Награда за препятствие не зависит от режима: иначе лёгкая превращается в способ
# быстрее накопить, и выбор сложности становится выбором заработка.
_reward_block = haz[haz.index("public int RewardMin;"):haz.index("public static ObstacleType Roll(")]
check("Сложность: награда не зависит от режима",
      OK if "Difficulty" not in read("Assets/Scripts/Wallet.cs")
         and "Difficulty" not in _reward_block else FAIL,
      "монеты за препятствие одни и те же на всех режимах")

check("Сложность: помощь после серии проигрышей молчит",
      OK if "ReliefActive" in diff_src and "Difficulty.ReportRunEnd" in gm
         and "ReliefActive" not in ui else FAIL,
      "игроку о подстройке не сообщается")

check("Сложность: вступление показывается один раз",
      OK if "SaveData.TutorialKey" in read("Assets/Scripts/UIManagerTutorial.cs")
         and "TutorialPending" in read("Assets/Scripts/GameBootstrap.cs") is False
         or "TryShowTutorial" in read("Assets/Scripts/GameBootstrap.cs") else FAIL,
      "отметка едет в облако вместе с профилем")

# --- стихии -------------------------------------------------------------------

# Архетип выводится из статуса материала, а не хранится вторым списком: два
# списка, обязанных совпадать, однажды разойдутся.
_statuses = _re.findall(r"^\s{8}(\w+),", skinfx[skinfx.index("public enum StatusKind"):
                                                 skinfx.index("public enum StatusKind") + 1800],
                        _re.M)
_arch_rows = len(_re.findall(r"\n\s+/\* \w+\s+\*/ D\(", arch_src))
check("Стихии: таблица покрывает все статусы",
      OK if _arch_rows == len(_statuses) and _arch_rows > 0 else FAIL,
      "статусов %d, строк в таблице %d" % (len(_statuses), _arch_rows))

check("Стихии: архетип берётся из материала, а не прописывается",
      OK if "ArchetypeDef Of(SkinDef skin)" in arch_src
         and "MaterialFxTable.Of(skin.Material)" in arch_src else FAIL,
      "новый скин получает стихию сам")

# Пять каналов различия: форма, цвет, движение, звук, вибрация. Четыре из них
# приходят из стихии, цвет и движение — из материала и SkinFxTable.
for _label, _needle, _where in (
        ("форма", "SkinShape.", arch_src),
        ("вибрация", "HapticPattern.", arch_src),
        ("звук", "SoundPitch", arch_src),
        ("цвет", "Tone = C(tone)", arch_src)):
    check("Стихии: %s задана в таблице" % _label,
          OK if _needle in _where else FAIL)

# Силуэты обязаны различаться. Проверяем, что у каждой формы своя ветка: пока
# «блок» считался тем же выражением, что «огранка», это были два имени одного
# восьмиугольника.
_branches = len(_re.findall(r"case SkinShape\.\w+:", shapes_src))
_shape_count = len(_re.findall(r"^\s{8}(\w+)[,=]", arch_src[arch_src.index("public enum SkinShape"):
                                                             arch_src.index("public struct ArchetypeDef")],
                               _re.M))
check("Стихии: у каждого силуэта своя ветка",
      OK if _branches >= _shape_count - 1 else FAIL,
      "форм %d, веток %d (круг идёт по умолчанию)" % (_shape_count, _branches))

# Шипы и лучи торчат за круг только на глаз: хитбокс у скинов общий.
check("Стихии: форма не трогает хитбокс",
      OK if "bubbleRadius" not in arch_src and "HitRadius" not in arch_src
         and "VisualScale" in arch_src else FAIL,
      "иначе дорогой скин был бы ещё и легче в игре")

# Пара «пузырёк + страж» обязана иметь один материал: начинку им выдают общую, и
# разные материалы дали бы форму от одного, а содержимое от другого.
_sk = read("Assets/Scripts/SkinStore.cs")


def _materials(_name):
    _blk = _re.search(r"readonly SkinDef\[\] %s =\s*\{(.*?)\n        \};" % _name, _sk, _re.S)
    _rows = [l for l in _blk.group(1).splitlines() if "SkinDef." in l]
    return [(_re.search(r"SkinMaterial\.(\w+)", l).group(1) if "SkinMaterial." in l else "-")
            for l in _rows]


_mb, _mg = _materials("Bubble"), _materials("Guardian")
_mismatch = [i for i, (a, b) in enumerate(zip(_mb, _mg)) if a != b]
# У каждого скина обязана быть своя строка эффектов: добавить скин в каталог и
# забыть строку в SkinFxTable — значит выдать ему чужое движение и чужой след.
_fx_rows = len(_re.findall(r"/\* \d+\s", skinfx[skinfx.index("public static readonly SkinFxDef[] All ="):]))
_catalog = len([l for l in _sk[_sk.index("readonly SkinDef[] Bubble ="):
                              _sk.index("readonly SkinDef[] Guardian =")].splitlines()
                if "SkinDef." in l])
check("Стихии: у каждого скина своя строка эффектов",
      OK if _fx_rows == _catalog and _catalog > 0 else FAIL,
      "скинов %d, строк эффектов %d" % (_catalog, _fx_rows))

check("Стихии: у половинок пары один материал",
      OK if len(_mb) == len(_mg) and not _mismatch else FAIL,
      "скинов %d" % len(_mb) if not _mismatch else "расходятся: %s" % _mismatch[:5])

# Вибрация уважает настройку и не частит: страж бьёт несколько раз в секунду.
check("Стихии: вибрация уважает настройку и не частит",
      OK if "SaveData.VibrationKey" in haptics_src and "MinInterval" in haptics_src else FAIL,
      "иначе телефон дрожит всю партию")

# --- щадящий режим ------------------------------------------------------------

visuals_src = read("Assets/Scripts/Visuals.cs")
fx_src = read("Assets/Scripts/SkinFxRuntime.cs")

# Вспышка цветом стихии на весь экран убрана целиком: от неё болели глаза.
# Проверяем именно отсутствие, а не «слабость» — ослабленная вспышка однажды
# вернулась бы к прежней яркости одной правкой числа.
check("Вспышки: нет вспышки на весь экран",
      OK if "FlashScreen" not in ui and "_flashPeak" not in ui
         and "FlashStrength" not in arch_src else FAIL,
      "ни метода, ни слоя, ни числа в таблице стихий")

# Остановка кадра давала рывки на каждом ударе, а ударов несколько в секунду.
check("Вспышки: нет остановки кадра при ударе",
      OK if "_hitStop" not in gm else FAIL,
      "hit-stop убран полностью")

# Белая частица на тёмной воде читается как вспышка. В эффектах удара,
# разрушения и статусов чистого белого быть не должно — только светлый оттенок
# самой стихии.
_white_fx = len(_re.findall(r"Color\.white", fx_src))
_white_status = len(_re.findall(r"Color\.white", status_src))
check("Вспышки: в эффектах нет чистого белого",
      OK if _white_fx <= 2 and _white_status == 0 else FAIL,
      "в эффектах %d (два поля-заглушки), в статусах %d" % (_white_fx, _white_status))

# Тот, кто САМ включал щадящий режим, обязан остаться на «Тихо» после
# появления трёх ступеней. Отобрать это молча нельзя: о боли в глазах просят
# не ради красоты, и ключ яркости тут не при чём — его у такого игрока нет.
check("Вспышки: прежний щадящий режим не отбирают",
      OK if 'PlayerPrefs.HasKey(SaveData.CalmFxKey)' in visuals_src
         and 'choseCalm ? FxBrightness.Quiet' in visuals_src
         and 'public int calmFx = 1;' in read("Assets/Scripts/SaveData.cs") else FAIL,
      "ключа яркости нет, а щадящий включал сам — значит «Тихо»")

# Настройка одна, а не три отдельные: «вспышки», «тряска» и «частицы» по
# отдельности заставляют игрока разбираться, что именно ему мешает. Теперь это
# одна кнопка на три ступени, и перебирает она их по кругу.
check("Вспышки: яркость — одна кнопка на три ступени",
      OK if "Visuals.LevelKey(Visuals.Level)" in ui
         and ui.count("Visuals.CycleLevel()") == 1
         and "Visuals.Toggle()" not in ui
         and "settings.fxQuiet" in visuals_src else FAIL,
      "Тихо / Обычно / Ярко, выбор сохраняется")

# Тряски камеры нет ни на одной ступени: от неё у владельца болят глаза, и
# «совсем слабая тряска» всё равно остаётся тряской. Проверяем поведение, а не
# имя одного файла: тряску можно вернуть новым компонентом, методом мира или
# прямой записью в камеру — ловим все три пути. Камеру ставит только
# GameBootstrap, один раз при запуске.
_rt = runtime_code()
_shake_bad = []
for _n, _s in sorted(_rt.items()):
    for _m in _re.finditer(r"\bclass\s+(\w*Shake\w*)", _s):
        _shake_bad.append("%s: класс %s" % (_n, _m.group(1)))
    for _m in _re.finditer(r"\b(?:void|float|bool)\s+(Shake|Kick)\s*\(", _s):
        _shake_bad.append("%s: метод %s" % (_n, _m.group(1)))
    for _m in _re.finditer(r"\.(Shake|Kick)\s*\(", _s):
        _shake_bad.append("%s: вызов %s" % (_n, _m.group(1)))
    for _m in _re.finditer(r"\bAddComponent<(\w*Shake\w*)>", _s):
        _shake_bad.append("%s: компонент %s" % (_n, _m.group(1)))
    if _n == "GameBootstrap.cs":
        continue
    # Компонент на самой камере пишет в свой transform без слова «cam».
    _on_camera = "RequireComponent(typeof(Camera))" in _s or "GetComponent<Camera>()" in _s
    for _m in _re.finditer(r"(?i)\b(\w*cam\w*\.)?transform\.(position|localPosition|rotation|localRotation)"
                           r"\s*[-+]?=(?!=)", _s):
        if _m.group(1) or _on_camera:
            _shake_bad.append("%s: камера двигается (%s)" % (_n, _m.group(2)))
_arch_struct = _rt["Archetypes.cs"]
_arch_struct = _arch_struct[_arch_struct.index("public struct ArchetypeDef"):]
_arch_struct = _arch_struct[:_arch_struct.index("\n    }")]
if _re.search(r"\bpublic\s+float\s+\w*Shake\w*", _arch_struct):
    _shake_bad.append("ArchetypeDef: поле тряски")
if _re.search(r"\bpublic\s+float\s+\w*[Ss]hake\w*\s*=", _rt["GameConfig.cs"]):
    _shake_bad.append("GameConfig: сила тряски")
check("Вспышки: тряски камеры нет ни на одной ступени",
      OK if not _shake_bad else FAIL,
      "камера неподвижна, вес удара — звук, вибрация, частицы" if not _shake_bad
      else "; ".join(_shake_bad[:6]))

# Неуязвимость мигала семь раз в секунду почти две секунды подряд — самый
# сильный строб в игре.
_bubble_src = read("Assets/Scripts/BubbleController.cs")
_breathe_w = [float(w) for w in _re.findall(r"Mathf\.Sin\(_invuln \* ([\d.]+)f\)", strip_comments(_bubble_src))]
check("Вспышки: неуязвимость не мигает",
      OK if "_invuln * 14f" not in _bubble_src and _breathe_w and max(_breathe_w) <= 4.2
         and _re.search(r"SetAlpha\((?:Mathf\.Lerp\(1f,\s*)?breathe", _bubble_src) else FAIL,
      "вместо мигания — спокойная полупрозрачность, %s рад/с" % (max(_breathe_w) if _breathe_w else "?"))

# --- лимиты эффекта удара -----------------------------------------------------

_budget = _re.search(r"Mathf\.Lerp\(([0-9.]+)f, ([0-9.]+)f, power - 1f\)", fx_src)
_lo, _hi = (float(_budget.group(1)), float(_budget.group(2))) if _budget else (99, 99)
check("Удар: бюджет частиц в пределах 5–20",
      OK if 5 <= _lo <= 12 and _hi <= 20 else FAIL,
      "обычный %.0f, боссовый %.0f" % (_lo, _hi))

_life = float(_re.search(r"ImpactMaxLife = ([0-9.]+)f;", fx_src).group(1))
_speed = float(_re.search(r"ImpactMaxSpeed = ([0-9.]+)f;", fx_src).group(1))
_bubble_r = float(_re.search(r"bubbleRadius = ([0-9.]+)f;", read("Assets/Scripts/GameConfig.cs")).group(1))
check("Удар: эффект не длиннее 0.3 с и не шире шарика",
      OK if _life <= 0.3 and _speed * _life <= _bubble_r * 2.2 else FAIL,
      "жизнь %.2f с, разлёт %.2f при диаметре %.2f" % (_life, _speed * _life, _bubble_r * 2))

# Ударная волна и вспышка в точке касания убраны: первая закрывала препятствие,
# вторая била по глазам.
_play_impact = fx_src[fx_src.index("void PlayImpact(MaterialFxDef m"):
                      fx_src.index("int ImpactBudget()")]
check("Удар: нет ударной волны и вспышки",
      OK if "EmitRing" not in _play_impact and "0.9f * power" not in _play_impact else FAIL,
      "остались только частицы")

# Разрушенное с одного удара препятствие не должно получать ещё удар и статус.
check("Удар: разрушенное не получает три слоя сразу",
      OK if "bool destroyed = false)" in gm and "if (destroyed) return;" in gm
         and "if (!broke)" in gm else FAIL,
      "при разрушении показывается только разрушение")

# --------------------------------------- анимация скина и положение монеты

_bubble_src = read("Assets/Scripts/BubbleController.cs")
_guard_src = read("Assets/Scripts/GuardianOrb.cs")
_haz_src = read("Assets/Scripts/SpriteFactoryHazards.cs")

# Начинка контр-масштабировалась делением на SkinScale целиком — это гасило не
# только перекос, но и само дыхание. Оболочка дышала, материал под ней стоял, и
# скин в бою выглядел мёртвым, хотя в витрине двигался.
_breathes = all("mean / SkinScale.x" in _s for _s in (_bubble_src, _guard_src))
check("Анимация: материал дышит вместе с оболочкой",
      OK if _breathes and "1f / SkinScale.x" not in _bubble_src
         and "1f / SkinScale.x" not in _guard_src else FAIL,
      "компенсируется только перекос, а не весь масштаб")

# Страж каждый кадр пересобирал цвет оболочки и затирал то, что поставил скин:
# оттенок домножался второй раз поверх окрашенной начинки.
check("Анимация: цвет оболочки не затирается каждый кадр",
      OK if "Color skinColor = _core != null && _core.enabled" in _guard_src
         and "Color.white, charge) * Tint" not in _guard_src else FAIL,
      "перезарядка показывается яркостью, а не подмешиванием оттенка")

# Движение скина не должно тонуть в собственной пульсации заряда.
_charge = _re.search(r"Radius \* 2f \* \(([0-9.]+)f \+ ([0-9.]+)f \* (?:charge|_chargeShown)\)", _guard_src)
_swing = float(_charge.group(2)) if _charge else 1.0
check("Анимация: пульсация заряда не глушит скин",
      OK if _swing <= 0.1 else FAIL,
      "размах заряда ±%.0f %% против ±2…6 %% у движения скина" % (_swing * 100))

# Базовая анимация идёт на нескалированном времени: пауза её гасит объектом, а
# не временем, и замедление из дев-панели не должно её тормозить.
check("Анимация: вращение материала не зависит от timeScale",
      OK if "Time.unscaledDeltaTime * SkinVisuals.CoreSpin" in _bubble_src
         and "Time.unscaledDeltaTime * SkinVisuals.CoreSpin" in _guard_src else FAIL,
      "в витрине время тоже нескалированное")

# --- монета -------------------------------------------------------------------

# Иконка и число ставились на глаз в семи местах. В HUD это кончилось
# наложением: Anchor центрирует прямоугольник, и «левый край» текста оказался
# левее иконки.
check("Монета: строку собирает общий помощник",
      OK if "static void LayoutCoinRow(" in ui and ui.count("LayoutCoinRow(") >= 5 else FAIL,
      "вызовов %d" % ui.count("LayoutCoinRow("))

_gap = float(_re.search(r"const float CoinGap = ([0-9.]+)f;", ui).group(1))
check("Монета: отступ до числа 4–8 точек",
      OK if 4 <= _gap <= 8 else FAIL,
      "%.0f точек" % _gap)

# Ширина числа берётся у самого текста, иначе отступ плавает: «7» и «7000»
# отличаются втрое.
check("Монета: отступ не зависит от длины числа",
      OK if "number.preferredWidth" in ui else FAIL,
      "ширина берётся у текста, а не задана числом")

# Цель полёта монет была приближением «от края экрана» и промахивалась мимо
# плашки на телефонах с вырезом: плашка живёт в безопасной зоне.
check("Монета: цель полёта берётся у иконки",
      OK if "CoinTargetWorld" in ui and "_ui.CoinTargetWorld(" in gm
         and "_field.HalfWidth + 0.7f" not in gm else FAIL,
      "позиция RectTransform, а не число от края экрана")

# --- камень и шипы --------------------------------------------------------------

# Упрощённые типы не должны нести фактуру: именно она превращалась в рябь.
_noisy = [_n for _n in ("RockV", "UrchinV", "BarnacleV", "CinderV")
          if _re.search(r"public static Sprite %s\(int variant\)(.*?)\n        \}" % _n,
                        _haz_src, _re.S)
          and any(_bad in _re.search(r"public static Sprite %s\(int variant\)(.*?)\n        \}" % _n,
                                     _haz_src, _re.S).group(1)
                  for _bad in ("Grain(", "Crust(", "Crack("))]
check("Камни: мелкая фактура убрана",
      OK if not _noisy else FAIL,
      "камень, ёж, нарост и уголёк идут плоско-объёмной заливкой"
      if not _noisy else "осталась фактура: " + ", ".join(_noisy))

check("Камни: плоско-объёмная заливка применена",
      OK if _haz_src.count("SimpleVolume(") >= 4 and "SimpleVolume" in _haz_art else FAIL,
      "мягкий градиент, тень, обводка темнее основного цвета")

# Остальные препятствия трогать не просили — проверяем, что фактура у них на
# месте, иначе «упростил только камни» окажется неправдой.
# Ловушка в список не входит: у неё фактуры не было и раньше — это
# полупрозрачный купол, зерно по нему не читается. Мина тоже: её шипы входят в
# семью «шипастых», и у неё фактуру убрали намеренно.
_kept = [_n for _n in ("CoralChunkV", "SpinnerV", "AnchorWeightV", "VortexV", "JawV")
         if "Grain(" not in _re.search(
             r"public static Sprite %s\(int variant\)(.*?)\n        \}" % _n,
             _haz_src, _re.S).group(1)]
check("Камни: остальные препятствия не тронуты",
      OK if not _kept else FAIL,
      "коралл, вертушка, якорь, воронка и створки сохранили фактуру"
      if not _kept else "потеряли фактуру: " + ", ".join(_kept))

_simple_v = int(_re.search(r"SimpleVariants = (\d+);", _haz_src).group(1))
check("Камни: 2–3 вариации у упрощённых",
      OK if 2 <= _simple_v <= 3 else FAIL,
      "вариаций %d" % _simple_v)

# ------------------------------------------------ раскладка карточки магазина
# Карточка собрана из блоков, привязанных к её верху, и единственный способ
# проверить её без запуска — сложить числа. Так и нашлось, что подсказка
# «РЕКОМЕНДУЕМ» налезала на строку цены и упиралась в обрез карточки.
_ui_src = read("Assets/Scripts/UIManager.cs")
_card_src = _re.search(r"ShopTile BuildCard\(.*?\n        \}", _ui_src, _re.S).group(0)
_card_blocks = [(m.group(1), float(m.group(2)), float(m.group(3)))
                for m in _re.finditer(
                    r'Anchor\(([\w.]+),\s*new Vector2\(0\.5f, 1f\),\s*'
                    r'new Vector2\(0f, (-?[\d.]+)f\),\s*'
                    r'new Vector2\([\d.]+f, ([\d.]+)f\)\)', _card_src)]
_card_h = float(_re.search(r"CardH = ([\d.]+)f;", read("Assets/Scripts/ShopLayout.cs")).group(1))

check(u"Карточка: все блоки разобраны",
      OK if len(_card_blocks) == 5 else FAIL,
      u"шарик, имя, редкость, цена, подсказка" if len(_card_blocks) == 5
      else u"нашлось %d" % len(_card_blocks))

_card_bad = []
for _i in range(len(_card_blocks) - 1):
    _n, _y, _h = _card_blocks[_i]
    _n2, _y2, _ = _card_blocks[_i + 1]
    if _y2 > _y - _h:            # оси вниз отрицательные
        _card_bad.append(u"%s заходит на %s" % (_n2, _n))

check(u"Карточка: блоки не налезают друг на друга",
      OK if not _card_bad else FAIL,
      u" · ".join(u"%s %.0f…%.0f" % (n, y, y - h) for n, y, h in _card_blocks)
      if not _card_bad else u", ".join(_card_bad))

_last_n, _last_y, _last_h = _card_blocks[-1]
_card_tail = _card_h - (abs(_last_y) + _last_h)
check(u"Карточка: подсказка не упирается в нижний край",
      OK if _card_tail >= 6 else FAIL,
      u"под «%s» остаётся %.0f точек из %.0f" % (_last_n, _card_tail, _card_h))

# Названия редкостей в одну строку: «ОБЫЧ/НЫЙ» в две читается как две кнопки.
_chip_src = _re.search(r'NewButton\("Rarity" \+ i.*?_shopTabLabels\[i\]', _ui_src, _re.S).group(0)
check(u"Магазин: редкости пишутся в одну строку",
      OK if "HorizontalWrapMode.Wrap" not in _chip_src
      and "resizeTextForBestFit = true" in _chip_src else FAIL,
      u"длинное слово ужимается кеглем, а не переносится")

# -------------------------------------------- оболочка столкновения стража
# Оболочка — та часть скина, которую игрок видит только в бою, и единственный
# способ проверить её без запуска — сверить таблицу с правилами и убедиться,
# что её вообще кто-то зовёт.
_env_tbl = read("Assets/Scripts/EnvelopeTable.cs")
_env_ctl = read("Assets/Scripts/CollisionEnvelope.cs")
_env_shp = read("Assets/Scripts/EnvelopeShape.cs")
_env_ui = read("Assets/Scripts/UIManagerEnvelope.cs")
_env_vis = read("Assets/Scripts/Visuals.cs")
_cfg_src0 = read("Assets/Scripts/GameConfig.cs")

_ENV_ROW = _re.compile(
    r'D\(EnvelopeKind\.(\w+),\s*LinkKind\.(\w+),\s*'
    r'"([0-9A-Fa-f]{6})",\s*"([0-9A-Fa-f]{6})",\s*"([0-9A-Fa-f]{6})",\s*"([0-9A-Fa-f]{6})",\s*'
    r'([\d.]+)f,\s*(\d+),\s*([\d.]+)f,\s*([\d.]+)f,\s*(\d+),\s*([\d.]+)f,\s*(true|false)\)')

_env_rows = [dict(kind=m.group(1), link=m.group(2),
                  hot=m.group(3), cool=m.group(4), accent=m.group(5), edge=m.group(6),
                  duration=float(m.group(7)), count=int(m.group(8)),
                  radius=float(m.group(9)), speed=float(m.group(10)),
                  branches=int(m.group(11)), linkw=float(m.group(12)),
                  rich=m.group(13) == "true")
             for m in _ENV_ROW.finditer(_env_tbl)]

_env_leg = [r for r in _env_rows if not r["rich"]]
_env_boss = [r for r in _env_rows if r["rich"]]

check(u"Оболочка: шестнадцать штук, 6 легендарных и 10 трофеев",
      OK if (len(_env_leg), len(_env_boss)) == (6, 10) else FAIL,
      u"легендарных %d, трофеев %d" % (len(_env_leg), len(_env_boss)))

# Повтор формы означал бы, что два скина за семь и за тринадцать тысяч бьют
# одинаково — ровно то, чего задача требует избежать.
_env_kinds = [r["kind"] for r in _env_rows]
_env_dup = sorted(set(k for k in _env_kinds if _env_kinds.count(k) > 1))
check(u"Оболочка: ни одной повторяющейся формы",
      OK if not _env_dup else FAIL,
      u"%d разных форм" % len(set(_env_kinds)) if not _env_dup
      else u"повторы: " + u", ".join(_env_dup))

# Пара «форма + связь» тоже обязана быть уникальной: одинаковая связь при
# похожей форме читается как один и тот же эффект.
_env_pairs = [(r["kind"], r["link"]) for r in _env_rows]
check(u"Оболочка: пары «форма + связь» не повторяются",
      OK if len(set(_env_pairs)) == len(_env_pairs) else FAIL,
      u"%d пар" % len(set(_env_pairs)))

# Все легендарные материалы и все боссы обязаны быть покрыты.
_skin_src = read("Assets/Scripts/SkinStore.cs")
_leg_mats = set(_re.findall(r'Rarity\.Legendary,\s*\d+,\s*SkinMaterial\.(\w+)', _skin_src))
_leg_mats.add("Starlight")   # награда за коллекцию, задаётся в Mastery()
_tbl_mats = set(_re.findall(r'\(SkinMaterial\.(\w+),', _env_tbl))
_env_missing = sorted(_leg_mats - _tbl_mats)
check(u"Оболочка: покрыты все легендарные материалы",
      OK if not _env_missing else FAIL,
      u", ".join(sorted(_leg_mats)) if not _env_missing
      else u"без оболочки: " + u", ".join(_env_missing))

_boss_kinds = len(set(_re.findall(r'BossKind\.(\w+),\s*SkinArt\.', _skin_src)))
check(u"Оболочка: покрыты все трофеи боссов",
      OK if len(_env_boss) == _boss_kinds else FAIL,
      u"боссов %d, оболочек %d" % (_boss_kinds, len(_env_boss)))

# --- размер. Это главное, ради чего правка и делалась: раньше радиус хранился
# в радиусах стража, а назывался диаметрами, и оболочка выходила вчетверо
# мельче — меньше самого шарика.
# Радиусы урезаны на четверть: оболочка закрывала обзор. Пределы сдвинуты
# вместе с ними, и на прежних значениях (2.6–3.4) эта проверка падает.
_bad = [u"%s %.2f" % (r["kind"], r["radius"]) for r in _env_leg
        if not (1.9 <= r["radius"] <= 2.2)]
_bad += [u"%s %.2f" % (r["kind"], r["radius"]) for r in _env_boss
         if not (2.1 <= r["radius"] <= 2.6)]
check(u"Оболочка: радиус 1.9–2.6 D",
      OK if not _bad else FAIL,
      u"от %.1f до %.1f D" % (min(r["radius"] for r in _env_rows),
                              max(r["radius"] for r in _env_rows))
      if not _bad else u", ".join(_bad))

check(u"Оболочка: радиус считается в диаметрах, а не в радиусах",
      OK if "RadiusD" in _env_tbl and "RadiusD" in _env_shp
      and "_root.localScale = Vector3.one * scale" in _env_ctl
      and "guardianRadius * 2f" in _env_ctl else FAIL,
      u"корень масштабируется на диаметр ровно в одном месте")

_bad = [u"%s %.2f с" % (r["kind"], r["duration"]) for r in _env_leg
        if not (1.5 <= r["duration"] <= 2.2)]
_bad += [u"%s %.2f с" % (r["kind"], r["duration"]) for r in _env_boss
         if not (2.0 <= r["duration"] <= 3.0)]
check(u"Оболочка: длительность 1.5–2.2 с и 2.0–3.0 с",
      OK if not _bad else FAIL,
      u"легендарные %.1f–%.1f, трофеи %.1f–%.1f"
      % (min(r["duration"] for r in _env_leg), max(r["duration"] for r in _env_leg),
         min(r["duration"] for r in _env_boss), max(r["duration"] for r in _env_boss))
      if not _bad else u", ".join(_bad))

_bad = [u"%s %d" % (r["kind"], r["count"]) for r in _env_leg
        if not (60 <= r["count"] <= 90)]
_bad += [u"%s %d" % (r["kind"], r["count"]) for r in _env_boss
         if not (100 <= r["count"] <= 150)]
check(u"Оболочка: частиц 60–90 легендарным и 100–150 трофеям",
      OK if not _bad else FAIL,
      u"легендарные %d–%d, трофеи %d–%d"
      % (min(r["count"] for r in _env_leg), max(r["count"] for r in _env_leg),
         min(r["count"] for r in _env_boss), max(r["count"] for r in _env_boss))
      if not _bad else u", ".join(_bad))

_bad = [u"%s %.2f" % (r["kind"], r["linkw"]) for r in _env_rows if r["linkw"] < 0.25]
check(u"Оболочка: связь не тоньше 0.25 D у основания",
      OK if not _bad else FAIL,
      u"от %.2f до %.2f D" % (min(r["linkw"] for r in _env_rows),
                              max(r["linkw"] for r in _env_rows))
      if not _bad else u", ".join(_bad))

_bad = [u"%s %d" % (r["kind"], r["branches"]) for r in _env_rows
        if not (4 <= r["branches"] <= 7)]
check(u"Оболочка: ветвей и языков 4–7",
      OK if not _bad else FAIL,
      u"от %d до %d" % (min(r["branches"] for r in _env_rows),
                        max(r["branches"] for r in _env_rows))
      if not _bad else u", ".join(_bad))

# Четыре слоя — иначе силуэт на тёмной воде не читается. Два из них теперь
# кольца: тень и ядро — это фигуры, а не россыпь, и рисовать их полусотней
# точек было чистой тратой.
_bad = [n for n in ("Shadow", "Core", "Shape", "Spark") if n not in _env_shp]
check(u"Оболочка: четыре слоя — тень, ядро, форма, искры",
      OK if not _bad and "Rings(" in _env_shp and "SampleSpark" in _env_shp
      and "SampleShape" in _env_shp else FAIL,
      u"тень и ядро кольцами, форма и искры россыпью"
      if not _bad else u"нет слоёв: " + u", ".join(_bad))

check(u"Оболочка: кольца рисуются одним спрайтом, а не россыпью",
      OK if "EnvelopeRing()" in _env_ctl and "EnvelopeSpeck()" in _env_ctl
      and "PlaceRings" in _env_ctl
      and "SampleShadow" not in _env_shp and "SampleCore" not in _env_shp else FAIL,
      u"три объекта на оба слоя вместо полусотни точек")

# --- пределы безопасности.
def _env_white(h):
    return int(h[0:2], 16) > 244 and int(h[2:4], 16) > 244 and int(h[4:6], 16) > 244

_bad = [u"%s #%s" % (r["kind"], h) for r in _env_rows
        for h in (r["hot"], r["cool"], r["accent"], r["edge"]) if _env_white(h)]
check(u"Оболочка: ни одного белого цвета",
      OK if not _bad else FAIL,
      u"%d цветов проверено" % (len(_env_rows) * 4) if not _bad else u", ".join(_bad))

# Тёмный край обязан быть реально тёмным: иначе обводки нет, а есть второй
# светлый слой, и силуэт снова расплывается.
_bad = [u"%s #%s" % (r["kind"], r["edge"]) for r in _env_rows
        if (int(r["edge"][0:2], 16) + int(r["edge"][2:4], 16) + int(r["edge"][4:6], 16)) > 330]
check(u"Оболочка: тёмная обводка действительно тёмная",
      OK if not _bad else FAIL,
      u"сумма каналов не выше 330 у всех шестнадцати"
      if not _bad else u", ".join(_bad))

_env_fade_in = float(_re.search(r"FadeIn = ([\d.]+)f", _env_ctl).group(1))
_env_fade_out = float(_re.search(r"FadeOut = ([\d.]+)f", _env_ctl).group(1))
_env_hz = float(_re.search(r"ArcHz = ([\d.]+)f", _env_ctl).group(1))
_env_core_a = float(_re.search(r"CoreAlpha = ([\d.]+)f", _env_ctl).group(1))
_env_spark_a = float(_re.search(r"SparkAlpha = ([\d.]+)f", _env_ctl).group(1))

check(u"Оболочка: появление не быстрее 0.2 с",
      OK if _env_fade_in >= 0.2 else FAIL, u"%.2f с" % _env_fade_in)
check(u"Оболочка: затухание 0.5–0.8 с",
      OK if 0.5 <= _env_fade_out <= 0.8 else FAIL, u"%.2f с" % _env_fade_out)
check(u"Оболочка: форма дуг обновляется не чаще 4 раз в секунду",
      OK if _env_hz <= 4.0 else FAIL,
      u"%.0f Гц, то есть дуга живёт %.2f с" % (_env_hz, 1.0 / _env_hz))
check(u"Оболочка: край просвечивает, за ним видно препятствия",
      OK if _env_spark_a <= 0.7 and _env_core_a <= 1.0 else FAIL,
      u"ядро %.2f, искры %.2f" % (_env_core_a, _env_spark_a))

check(u"Оболочка: дыхание ядра — один цикл за 1.5–2 с",
      OK if _re.search(r"Mathf\.Sin\(age \* 3\.59f\)", _env_shp) else FAIL,
      u"3.59 рад/с = 1.75 с на цикл")

check(u"Оболочка: альфа зажата по слоям",
      OK if _env_ctl.count("Mathf.Clamp01") >= 4 and "CeilingOf(layer)" in _env_ctl else FAIL,
      u"ни один слой не пробивает свой потолок")

# Тряска камеры от оболочки не идёт вовсе: в задаче это отдельным пунктом.
_bad = [n for n, s in (("CollisionEnvelope.cs", _env_ctl),
                       ("EnvelopeShape.cs", _env_shp),
                       ("UIManagerEnvelope.cs", _env_ui))
        if "Shake" in s]
check(u"Оболочка: тряски камеры нет",
      OK if not _bad else FAIL,
      u"ни одного обращения к CameraShake" if not _bad else u", ".join(_bad))

# Размер игрока применяется один раз: он уже внутри Radius стража.
_bad = [n for n, s in (("CollisionEnvelope.cs", _env_ctl), ("EnvelopeShape.cs", _env_shp))
        if "PlayerSize" in s or "playerScaleMultiplier" in s]
check(u"Оболочка: масштаб 0.68 не применяется дважды",
      OK if not _bad else FAIL,
      u"размер берётся только из Radius стража" if not _bad else u", ".join(_bad))

# --- настройки яркости: размер не режется НИКОГДА.
check(u"Настройки: «Тихо» не трогает размер оболочки",
      OK if "Visuals." not in _env_shp.split("public static int Count")[1].split("public static Split")[0].replace("Visuals.Particles", "")
      else FAIL,
      u"EnvelopeShape смотрит на настройку только ради числа частиц")

_bad = [n for n in ("Size =", "RadiusD") if False]
check(u"Настройки: яркость не входит в формулу размера",
      OK if "Visuals.Glow" not in _env_shp and "Visuals.EnvelopeAlpha" not in _env_shp else FAIL,
      u"в EnvelopeShape нет ни Glow, ни EnvelopeAlpha — резать размер нечем")

_quiet_particles = float(_re.search(
    r"Particles => Level == FxBrightness\.Quiet \? ([\d.]+)f", _env_vis).group(1))
check(u"Настройки: «Тихо» убавляет частицы не больше чем на четверть",
      OK if _quiet_particles >= 0.75 else FAIL,
      u"множитель %.2f" % _quiet_particles)

check(u"Настройки: три ступени яркости и сохранение выбора",
      OK if "enum FxBrightness" in _env_vis and "FxBrightnessKey" in _env_vis
      and "CycleLevel" in _env_vis else FAIL,
      u"Тихо / Обычно / Ярко")

check(u"Настройки: по умолчанию «Обычно»",
      OK if "_level = FxBrightness.Normal;" in _env_vis else FAIL,
      u"а тем, кто сам включал щадящий режим, достаётся «Тихо»")

_save = read("Assets/Scripts/SaveData.cs")
check(u"Настройки: яркость переживает облако и старые профили",
      OK if "fxBrightness" in _save and "state.fxBrightness >= 0" in _save
      and "FxBrightnessKey" in _save else FAIL,
      u"нет ключа — ступень выводится из старого calmFx, выбор не теряется")

check(u"Настройки: старый переключатель щадящего режима на месте",
      OK if "public static bool Calm" in _env_vis and "settings.calmHint" in read("Assets/Scripts/Loc.cs")
      else FAIL, u"«Тихо» — это он и есть")

# --- подключение. Таблица без вызова — мёртвый код.
_gm = read("Assets/Scripts/GameManager.cs")
_env_calls = _gm.count("GuardianImpact(")
check(u"Оболочка: удар проводится через один GuardianImpact",
      OK if _env_calls >= 6 and _gm.count("_envelope.Play(") <= 2 else FAIL,
      u"мест вызова удара %d, мест запуска оболочки %d"
      % (_env_calls - 1, _gm.count("_envelope.Play(")))

check(u"Оболочка: связь тянется к центру цели и растекается по ней",
      OK if "targetRadius" in _gm and "targetCenter" in _gm
      and "targetRadiusD" in _env_shp and "OnTarget" in _env_shp else FAIL,
      u"точка касания лежит в полурадиусе от центра стража — связь, нацеленная"
      u" туда, была короче самого шарика")

for _what, _needle in ((u"такт идёт каждый кадр", "_envelope.Tick(dt)"),
                       (u"сброс при начале заплыва", "_envelope.Clear()"),
                       (u"берётся из выбранного стража", "EnvelopeTable.TryGet(")):
    check(u"Оболочка: " + _what, OK if _needle in _gm else FAIL, _needle)

check(u"Оболочка: не больше двух одновременно",
      OK if "MaxEnvelopes = 2" in _env_ctl and "class EnvelopeBank" in _env_ctl else FAIL,
      u"третья легла бы поверх первых двух, ничего не добавив")

# Витрина обязана брать то же описание: разошлись бы — игрок увидел бы в бою
# не то, за что заплатил. Это в проекте уже случалось со следом и с аурой.
check(u"Оболочка: витрина считает по тому же EnvelopeShape",
      OK if "EnvelopeShape.Sample(" in _env_ui and "EnvelopeShape.Sample(" in _env_ctl
      else FAIL, u"одно описание на бой и на витрину")

check(u"Оболочка: витрина берёт пределы из CollisionEnvelope",
      OK if all(("CollisionEnvelope." + c) in _env_ui
                for c in ("FadeIn", "FadeOut", "CoreAlpha", "ArcHz")) else FAIL,
      u"потолки слоёв общие")

_ui_src2 = read("Assets/Scripts/UIManager.cs")
check(u"Оболочка: её показывают и «Примерить», и «Сравнить»",
      OK if _ui_src2.count("PlayShopEnvelope()") >= 2 else FAIL,
      u"обе кнопки зовут один показ")

# Нагрузка. Считаем так же, как Tools/check_envelope_load.py: кольца три,
# форма половина плотности, искры десятая, связь по бюджету.
_env_pool = int(_re.search(r"PoolSize = (\d+);", _env_ctl).group(1))
_env_budget = int(_re.search(r"LinkTravelBudget = (\d+);", _env_shp).group(1))
_env_spot = int(_re.search(r"LinkSpread = (\d+);", _env_shp).group(1))


def _env_cost(r):
    steps = max(3, min(8, _env_budget // r["branches"]))
    return (max(8, int(round(r["count"] * 0.5)))
            + max(4, int(round(r["count"] * 0.1)))
            + r["branches"] * steps + _env_spot)


_env_worst = max(_env_cost(r) for r in _env_rows)
check(u"Оболочка: пул покрывает худший случай",
      OK if _env_pool >= _env_worst else FAIL,
      u"пул %d, худший случай %d точек россыпи и связи" % (_env_pool, _env_worst))

# Потолок нагрузки: не больше трети кадра сверху на одну оболочку.
_fx_pool = int(_re.search(r"maxParticles = (\d+);", _cfg_src0).group(1))
_frame_base = _fx_pool + 40 + 3
_env_share = (_env_worst + 3) / float(_frame_base)
check(u"Оболочка: прибавка к кадру не больше 35 %",
      OK if _env_share <= 0.35 else FAIL,
      u"%d объектов против %d в кадре, +%.1f %%"
      % (_env_worst + 3, _frame_base, _env_share * 100))

# Бюджет связи общий на все ветви: иначе у скина с семью ветвями одна только
# связь стоила шестидесяти трёх точек.
check(u"Оболочка: бюджет связи не растёт с числом ветвей",
      OK if "LinkTravelBudget / Strands(def)" in _env_shp
      and max(r["branches"] * max(3, min(8, _env_budget // r["branches"]))
              for r in _env_rows) <= _env_budget + 8 else FAIL,
      u"не больше %d точек на ход прядей плюс %d на пятно" % (_env_budget + 8, _env_spot))

# Отсечка по экрану: частицы за краем не получают ни Transform, ни цвета.
check(u"Оболочка: частицы за краем экрана не обновляются",
      OK if "SetBounds" in _env_ctl and "leftD" in _env_ctl
      and "_envelope.SetBounds(" in _gm else FAIL,
      u"границы берутся у Playfield")

# Один материал на всё: иначе каждая частица — своя партия отрисовки.
check(u"Оболочка: один материал на все слои",
      OK if _env_ctl.count("SpriteFactory.Unlit") >= 2
      and "sharedMaterial" in _env_ctl else FAIL,
      u"SpriteFactory.Unlit, три текстуры на всю оболочку")

# Доля экрана: верхняя оценка по сплошному кругу. 25 % — предел из задания.
_cfg_src = read("Assets/Scripts/GameConfig.cs")
_gr = float(_re.search(r"guardianRadius = ([\d.]+)f;", _cfg_src).group(1))
_psc = float(_re.search(r"playerScaleMultiplier = ([\d.]+)f;", _cfg_src).group(1))
_wh = float(_re.search(r"worldHeight = ([\d.]+)f;", _cfg_src).group(1))
_asp = float(_re.search(r"designAspect = ([\d.]+)f;", _cfg_src).group(1))
_screen = _wh * _asp * _wh
_biggest = max(r["radius"] for r in _env_rows) * _gr * _psc * 2.0
_share = 3.14159 * _biggest * _biggest / _screen * 100

check(u"Оболочка: не закрывает больше 12 % экрана",
      OK if _share <= 12 else FAIL,
      u"самая крупная занимает %.1f %% площади (было 21.3 %%)" % _share)

# Детали урезаны вместе с радиусом, но слабее — иначе эффект рассыпается.
_detail = float(_re.search(r"DetailScale = ([\d.]+)f", _env_shp).group(1))
check(u"Оболочка: детали урезаны, но не в ноль",
      OK if 0.8 <= _detail <= 0.95 else FAIL,
      u"размер частиц и толщина связи x%.2f" % _detail)

_edge = float(_re.search(r"EdgeFade = ([\d.]+)f", _env_shp).group(1))
check(u"Оболочка: сквозь край видно препятствия",
      OK if 0.4 <= _edge <= 0.75 else FAIL,
      u"прозрачность внешнего края x%.2f" % _edge)

# Большой показ в витрине: эффект обязан быть виден почти в натуральную
# величину и не обрезаться. Оба числа считаются, а не берутся на веру.
_demo_scale = float(_re.search(r"DemoScale = ([\d.]+)f;", _env_ui).group(1))
_demo_ball = float(_re.search(r"DemoBallSize = ([\d.]+)f;", _env_ui).group(1))
_shop_scale = float(_re.search(r"ShopScale = ([\d.]+)f;", _env_ui).group(1))

check(u"Витрина: большой показ не мельче 0.7 натуральной величины",
      OK if _demo_scale >= 0.7 else FAIL,
      u"%.2f против %.2f в маленькой панели" % (_demo_scale, _shop_scale))

# Вылет: самый большой радиус плюс восемь процентов, которые добирают искры.
_demo_reach = max(r["radius"] for r in _env_rows) * 1.08 * _demo_scale * _demo_ball
check(u"Витрина: большой показ не обрезается",
      OK if _demo_reach <= 250 else FAIL,
      u"вылет %.0f точек при запасе 250 до края панели" % _demo_reach)

check(u"Витрина: в большом показе есть шарик и препятствие",
      OK if "_bigBallShell" in _env_ui and "SpriteFactory.Rock()" in _env_ui
      and "OpenBigDemo" in _ui_src2 else FAIL,
      u"шарик %.0f точек, мишень 0.9 от него" % _demo_ball)

check(u"Витрина: сетку карточек большой показ не трогает",
      OK if "BuildBigDemo(_shopScreen)" in _ui_src2
      and "_shopListContent" not in _env_ui else FAIL,
      u"лежит поверх экрана последним ребёнком")

# ------------------------------------------------- регрессии P0 (аудит 2026-10-28)
# Обе проверки закрыты по умолчанию: если код не удалось разобрать — это провал,
# а не «нечего проверять». Иначе проверка однажды молча перестанет что-либо
# проверять, как уже случалось.

def _method_bodies(src):
    """Тела методов вида `... Имя(...) { ... }` — по счёту скобок."""
    bodies = {}
    for m in re.finditer(r"\b(?:void|bool|int|float|string)\s+(\w+)\s*\([^)]*\)\s*\{", src):
        depth, i = 1, m.end()
        while i < len(src) and depth:
            depth += {"{": 1, "}": -1}.get(src[i], 0)
            i += 1
        bodies.setdefault(m.group(1), src[m.end():i - 1])
    return bodies


# 1. После паузы пузырёк и страж оставались невидимыми до конца заплыва: пауза
#    ставит модальный экран 4, а ShowHud не возвращал немодальный, и
#    ModalChanged(false) не приходил. Проверяем всю цепочку, а не одну строку.
_ui_p0 = read("Assets/Scripts/UIManager.cs")
_gm_p0 = read("Assets/Scripts/GameManager.cs")
_ui_bodies = _method_bodies(_ui_p0)
_modal = re.search(r"IsModalScreen\(int screen\)\s*=>\s*([^;]+);", _ui_p0)
_nonmodal = set(int(x) for x in re.findall(r"screen\s*!=\s*(\d+)", _modal.group(1))) if _modal else set()
_hud_on = [n for n, b in _ui_bodies.items() if "_hud.SetActive(true)" in b]
_hud_bad = [n for n in _hud_on
            if not any("SetScreen(%d)" % s in _ui_bodies[n] for s in _nonmodal)]
_chain = ("ModalChanged?.Invoke" in _ui_bodies.get("SetScreen", "")
          and "_ui.ModalChanged += OnModalChanged" in _gm_p0
          and "_bubble.SetVisible(!modal)" in _gm_p0)
check(u"Интерфейс: возврат в HUD снимает модальность",
      OK if _modal and _hud_on and not _hud_bad and _chain and 3 in _nonmodal else FAIL,
      (u"методов, включающих HUD: %d, все ставят немодальный экран" % len(_hud_on))
      if _modal and _hud_on and not _hud_bad and _chain
      else u"не ставят немодальный экран: %s; цепочка ModalChanged %s"
           % (", ".join(_hud_bad) or u"—", u"на месте" if _chain else u"НЕ НАЙДЕНА"))

# 2. Купленный трофей-страж не открывался: IndexOf искал ключ только среди
#    пузырьков, а у трофеев стража ключ свой. Повторяем поиск так, как его
#    делает код (в тех списках и в том порядке, что в теле IndexOf), и требуем,
#    чтобы каждый скин обоих списков находился по своему индексу.
_store_p0 = read("Assets/Scripts/SkinStore.cs")
_store_bodies = _method_bodies(_store_p0)


def _skin_keys(name):
    m = re.search(r"public static readonly SkinDef\[\] %s =\s*\{(.*?)\n\s*\};" % name, _store_p0, re.S)
    return re.findall(r'SkinDef\.\w+\(\s*"([^"]+)"', m.group(1)) if m else []


_lists = {"Bubble": _skin_keys("Bubble"), "Guardian": _skin_keys("Guardian")}
_search = re.findall(r"for \(int i = 0; i < (\w+)\.Length", _store_bodies.get("IndexOf", ""))


def _index_of(key):
    for lst in _search:
        if key in _lists.get(lst, []):
            return _lists[lst].index(key)
    return -1


_lost = ["%s[%d] %s" % (lst, i, k) for lst, keys in _lists.items()
         for i, k in enumerate(keys) if _index_of(k) != i]
_uses = "IsOwned(IndexOf(skin))" in _store_bodies.get("IsUnlocked", "")
_parsed = all(_lists.values()) and len(_lists["Bubble"]) == len(_lists["Guardian"]) and _search
check(u"Скины: каждая половина пары находит свою покупку",
      OK if _parsed and _uses and not _lost else FAIL,
      (u"скинов %d + %d, IndexOf ищет в: %s" % (len(_lists["Bubble"]), len(_lists["Guardian"]), ", ".join(_search)))
      if _parsed and not _lost
      else u"не находятся: %s" % (", ".join(_lost[:4]) if _lost else u"списки или IndexOf не разобраны"))

# ------------------------------------------------ скины боссов и медуза
_bs_tbl = read("Assets/Scripts/BossSkinTable.cs")
_bs_fin = read("Assets/Scripts/SpriteFactoryBossSkins.cs")
_bs_skins = read("Assets/Scripts/SpriteFactorySkins.cs")
_bs_vis = read("Assets/Scripts/SkinVisuals.cs")
_jl_spr = read("Assets/Scripts/SpriteFactoryJelly.cs")
_jl_ctl = read("Assets/Scripts/JellyBossController.cs")
_jl_ten = read("Assets/Scripts/JellyTentacles.cs")

_BS_ROW = _re.compile(r"BossMotif\.(\w+), ([\d.]+)f, ([\d.]+)f, ([\d.]+)f, (\d+), ([\d.]+)f")
_bs_rows = [(m.group(1), float(m.group(2)), float(m.group(3)), float(m.group(4)),
             int(m.group(5)), float(m.group(6))) for m in _BS_ROW.finditer(_bs_tbl)]

check(u"Трофеи: паспорт у каждого из десяти боссов",
      OK if len(_bs_rows) == 10 else FAIL,
      u"строк в таблице %d" % len(_bs_rows))

_bs_motifs = [r[0] for r in _bs_rows]
_bs_dup = sorted(set(m for m in _bs_motifs if _bs_motifs.count(m) > 1))
check(u"Трофеи: ни одной повторяющейся фактуры",
      OK if not _bs_dup and len(_bs_rows) == 10 else FAIL,
      u"%d разных" % len(set(_bs_motifs)) if not _bs_dup else u"повторы: " + u", ".join(_bs_dup))

# Палитра: пять цветов на строку, и ни один не задан вслепую белым.
_bs_cols = _re.findall(r"D\((Palette\.[\w.()\"]+,\s*){4}", _bs_tbl)
check(u"Трофеи: палитра берётся у самого босса",
      OK if _bs_tbl.count("Palette.") >= 40 else FAIL,
      u"цветов из палитры боссов: %d" % _bs_tbl.count("Palette."))

_bad = [r[0] for r in _bs_rows if not (0.08 <= r[2] <= 0.3)]
check(u"Трофеи: фактура не превращается в шум",
      OK if not _bad else FAIL,
      u"сила от %.2f до %.2f" % (min(r[2] for r in _bs_rows), max(r[2] for r in _bs_rows))
      if not _bad else u", ".join(_bad))

_bad = [r[0] for r in _bs_rows if not (0.6 <= r[3] <= 0.95)]
check(u"Трофеи: середина просвечивает, видно начинку",
      OK if not _bad else FAIL,
      u"прозрачность середины %.2f…%.2f" % (min(r[3] for r in _bs_rows),
                                            max(r[3] for r in _bs_rows))
      if not _bad else u", ".join(_bad))

_bs_size = int(_re.search(r"public const int SkinSize = (\d+);", _bs_fin).group(1))
check(u"Трофеи: текстура не меньше 256",
      OK if _bs_size >= 256 else FAIL, u"%d x %d" % (_bs_size, _bs_size))

check(u"Трофеи: все слои накладываются одной функцией",
      OK if "BossSkinFinish(px" in _bs_skins and "MotifAt(" in _bs_fin
      and "EdgeNear(" in _bs_fin and "NearBody(" in _bs_fin else FAIL,
      u"кайма, фактура, блик, тень и обводка — один проход на все двадцать силуэтов")

check(u"Трофеи: у каждого есть начинка",
      OK if "SpriteFactory.BossCore(skin.UnlockBoss)" in _bs_vis
      and "public static Sprite BossCore(" in _bs_fin else FAIL,
      u"раньше Trophy() оставлял материал стеклом, и начинки не было ни у кого")

check(u"Трофеи: игра и витрина берут один путь отрисовки",
      OK if _bs_vis.count("SpriteFactory.GuardianShell(skin)") == 1
      and _bs_vis.count("SpriteFactory.BubbleShell(skin)") == 1 else FAIL,
      u"SkinVisuals.Resolve — единственная точка сборки вида")

check(u"Трофеи: обводка темнее тела",
      OK if "def.Deep.WithAlpha" in _bs_fin else FAIL,
      u"цвет обводки — Deep из строки босса")

check(u"Трофеи: блик не белый",
      OK if "Lighten(def.Body" in _bs_fin and "Color.white, gloss" not in _bs_fin else FAIL,
      u"блик — осветлённый тон тела, а не белое пятно")

# ------------------------------------------------------------- медуза
check(u"Медуза: собрана из слоёв, а не из одного спрайта",
      OK if all(n in _jl_spr for n in ("JellyBell", "JellyGlow", "JellyEye",
                                       "JellyArm", "JellyFringe", "JellyHaze")) else FAIL,
      u"купол, свечение, глаз, рукав, бахрома, дымка")

check(u"Медуза: подмены спрайта при уроне больше нет",
      OK if "JellyBoss(true)" not in _jl_ctl and "JellyBoss(false)" not in _jl_ctl
      and "_tint.Hit()" in _method_bodies(strip_comments(_jl_ctl)).get("ApplyDamage", "") else FAIL,
      u"купол получает общий подмес HitTint, пределы — в проверке «Вспышки: попадание по боссу»")

_glow_rise = float(_re.search(r"const float GlowRise = ([\d.]+)f;", _jl_ctl).group(1))
check(u"Медуза: глаз разгорается не быстрее 0.3 с",
      OK if _glow_rise >= 0.3 else FAIL, u"%.2f с" % _glow_rise)

_breath = float(_re.search(r"const float BreathPeriod = ([\d.]+)f;", _jl_ctl).group(1))
check(u"Медуза: дыхание свечения не чаще цикла за 2 с",
      OK if _breath >= 2.0 else FAIL, u"цикл за %.1f с" % _breath)

_tel = float(_re.search(r"const float TelegraphTime = ([\d.]+)f;", _jl_ctl).group(1))
check(u"Медуза: атака телеграфируется заранее",
      OK if _tel >= 0.5 and "_squashWant = _telegraph" in _jl_ctl
      and "_arms.SetPull(" in _jl_ctl else FAIL,
      u"за %.2f с купол втягивается, глаз разгорается, щупальца подтягиваются" % _tel)

check(u"Медуза: телеграф не сдвигает выстрел",
      OK if "_shootTimer <= TelegraphTime" in _jl_ctl
      and "_telegraph = _shootTimer <= TelegraphTime" in _jl_ctl else FAIL,
      u"показ укладывается в прежний интервал, темп и урон не меняются")

_death = float(_re.search(r"const float DeathTime = ([\d.]+)f;", _jl_ctl).group(1))
check(u"Медуза: смерть медленная, без вспышки",
      OK if _death >= 1.5 and "Color.white" not in _jl_ctl.split("TickDying")[1][:900] else FAIL,
      u"угасание %.1f с" % _death)

# Белого в палитре медузы быть не должно ни в одном слое.
_jl_hex = _re.findall(r'Palette\.Hex\("([0-9A-Fa-f]{6})"\)', _jl_spr)
_jl_white = [h for h in _jl_hex
             if int(h[0:2], 16) > 240 and int(h[2:4], 16) > 240 and int(h[4:6], 16) > 240]
check(u"Медуза: в палитре нет белого",
      OK if _jl_hex and not _jl_white else FAIL,
      u"%d цветов проверено" % len(_jl_hex) if not _jl_white else u", ".join(_jl_white))

# Пульсации поля: быстрый синус по времени — это мерцание.
_jl_fast = _re.findall(r"Mathf\.Sin\(_timer \* ([\d.]+)f\)", _jl_ctl)
_bad = [s for s in _jl_fast if float(s) > 3.0]
check(u"Медуза: поле не мерцает",
      OK if not _bad else FAIL,
      u"частоты по времени: " + (u", ".join(_jl_fast) if _jl_fast else u"нет")
      if not _bad else u"быстрее 3 рад/с: " + u", ".join(_bad))

_t_arms = int(_re.search(r"public const int Arms = (\d+);", _jl_ten).group(1))
_t_links = int(_re.search(r"public const int Links = (\d+);", _jl_ten).group(1))
_t_fr = int(_re.search(r"public const int Fringes = (\d+);", _jl_ten).group(1))

check(u"Медуза: щупальца двух ярусов",
      OK if _t_arms >= 3 and _t_links >= 3 and _t_fr >= 4 else FAIL,
      u"%d рукава по %d звеньев и %d полос нитей" % (_t_arms, _t_links, _t_fr))

check(u"Медуза: все щупальца обновляет один компонент",
      OK if "class JellyTentacles" in _jl_ten
      and _jl_ten.count("void Update(") == 0
      and "public void Tick(float dt)" in _jl_ten else FAIL,
      u"один цикл на %d объектов, без собственных Update" % (_t_arms * _t_links + _t_fr))

check(u"Медуза: волна с запаздыванием, а не строем",
      OK if "Lag" in _jl_ten and "_phase - k * Lag" in _jl_ten else FAIL,
      u"звено повторяет движение предыдущего через четверть периода")

check(u"Медуза: размер на экране не вырос",
      OK if "jellyBellSpan" not in _jl_ctl.split("Spawn()")[1][:1200].replace(
          "_bellRadius = column * _cfg.jellyBellSpan;", "") else FAIL,
      u"купол считается из тех же долей колонки, что и раньше")

check(u"Медуза: дымка не закрывает поле",
      OK if "0.16f * _bodyAlpha" in _jl_ctl and "ring" in _jl_spr.split("JellyHaze")[1][:600]
      else FAIL,
      u"кольцо, а не диск: середина свободна для снарядов")

# ------------------------------------- медуза: посадка в кадр и слои
_jl_sort = read("Assets/Scripts/SortingOrders.cs")
_jl_fit = read("Assets/Scripts/JellyFit.cs")

# Корневая причина «босс виден не целиком»: TickDying уводил _bodyAlpha в ноль,
# Spawn его не возвращал, и во втором бою тело рисовалось прозрачным.
_jl_spawn = _jl_ctl[_jl_ctl.index("public void Spawn()"):_jl_ctl.index("public void Tick(float dt)")]
check(u"Медуза: прозрачность тела сбрасывается при появлении",
      OK if "_bodyAlpha = 1f;" in _jl_spawn else FAIL,
      u"иначе во втором бою купол, свечение и глаз невидимы")

# Все слои гасятся и зажигаются явно — иначе повторный бой наследует смерть.
_bad = [n for n in ("_arms.SetAlpha(1f)", "_arms.SetVisible(true)", "_haze.enabled = true")
        if n not in _jl_spawn]
check(u"Медуза: все слои включаются при появлении",
      OK if not _bad else FAIL,
      u"тело, щупальца, дымка" if not _bad else u"не включены: " + u", ".join(_bad))

# Порядок отрисовки: равные номера у двух слоёв означают неопределённый порядок.
_jl_orders = dict(_re.findall(r"public const int (Jelly\w+) = (-?\d+);", _jl_sort))
_jl_orders["Bell"] = _re.search(r"public const int Boss = (-?\d+);", _jl_sort).group(1)
_vals = [int(v) for v in _jl_orders.values()]
check(u"Медуза: у каждого слоя свой порядок отрисовки",
      OK if len(set(_vals)) == len(_vals) and len(_vals) >= 6 else FAIL,
      u"%d слоёв, номера %s" % (len(_vals), sorted(_vals)))

# И ни один не совпадает с чужими слоями сцены.
# Boss и BossAura — слои ДРУГИХ боссов, а на экране одновременно бывает только
# один босс, поэтому совпадение номера с ними ничего не ломает. Всё остальное
# (препятствия, строй, рыбы, снаряды) живёт вместе с медузой и делить номер не
# имеет права.
_other = {}
for _n, _v in _re.findall(r"public const int (\w+) = (-?\d+);", _jl_sort):
    if not _n.startswith("Jelly") and _n not in ("Boss", "BossAura"):
        _other.setdefault(int(_v), []).append(_n)
_clash = [u"%s=%d вместе с %s" % (n, int(v), u"/".join(_other[int(v)]))
          for n, v in _jl_orders.items() if n != "Bell" and int(v) in _other]
check(u"Медуза: слои не делят номер с чужими",
      OK if not _clash else FAIL,
      u"пересечений нет" if not _clash else u", ".join(_clash))

# Посадка считается одной функцией на рантайм и на проверку форматов.
check(u"Медуза: посадка в кадр считается общей функцией",
      OK if "JellyFit.Solve(" in _jl_ctl and "JellyFit.Solve(" in read("Assets/Scripts/JellyFitReport.cs")
      and "class JellyFit" in _jl_fit else FAIL,
      u"рантайм и проверка по форматам берут одну формулу")

check(u"Медуза: ужим вида не трогает хитбокс",
      OK if "_visualFit" in _jl_ctl
      and "_hitRadius = _bellRadius * _cfg.jellyHitFraction" in _jl_ctl
      and "_hitRadius" not in _jl_fit else FAIL,
      u"_hitRadius считается от _bellRadius и множителем вида не умножается")

# Форматы: список должен покрывать и портрет, и альбом.
_fmt = read("Assets/Scripts/JellyFitReport.cs")
_n_fmt = len(_re.findall(r'\("[^"]+", \d', _fmt))
check(u"Медуза: проверка покрывает портрет и альбом",
      OK if _n_fmt >= 5 and "альбом" in _fmt and "портрет" in _fmt else FAIL,
      u"форматов в списке: %d" % _n_fmt)

# Купол с юбкой: прямой горизонтальный обрез читался как срезанный купол.
check(u"Медуза: низ купола идёт дугой, а не прямой",
      OK if "skirt" in _jl_spr and "p.x * p.x" in _jl_spr else FAIL,
      u"граница опускается к бокам, по ней же пущены фестоны")

# Телеграф: рябь появляется только на подготовке и не включается рывком.
check(u"Медуза: рябь привязана к телеграфу",
      OK if "JellyRipple" in _jl_spr and "_ripple.enabled = r > 0.02f" in _jl_ctl else FAIL,
      u"появляется и уходит вместе с подготовкой")

check(u"Медуза: зрачок сужается без подмены спрайта",
      OK if "Mathf.Lerp(1f, 0.9f, dilate)" in _jl_ctl else FAIL,
      u"неравномерный масштаб глаза вместо второго спрайта")

check(u"Медуза: дрожь нитей меняет угол, а не яркость",
      OK if "SetTremble" in _jl_ten and "tilt +=" in _jl_ten else FAIL,
      u"правило о трёх изменениях яркости в секунду не задето")

# Связь оболочки: на далёкой цели точки раздувались в кляксы через полэкрана.
check(u"Оболочка: точки связи не раздуваются на далёкой цели",
      OK if "def.LinkWidthD * 2.5f" in _env_shp else FAIL,
      u"размер зажат между толщиной и двумя с половиной толщинами")

# ------------------------------------------------- этап A: правила для глаз
# Проверки этого раздела смотрят на код без комментариев и разбирают числа,
# а не ищут подстроку: каждая падает на коде до этапа A и проходит после.
_rtA = runtime_code()


def _cs_float(src, name):
    """Значение `Имя = 0.25f;` из кода без комментариев, или None."""
    m = _re.search(r"\b%s\s*=\s*(-?\d+(?:\.\d+)?)f?\s*;" % name, src)
    return float(m.group(1)) if m else None


def _hex_sum(h):
    return sum(int(h[i:i + 2], 16) for i in (0, 2, 4)) / 255.0


def _palette_hex(name):
    m = _re.search(r"\b%s\s*=\s*Hex\(\"([0-9A-Fa-f]{6})\"" % name, _rtA.get("Palette.cs", ""))
    return m.group(1) if m else None


# 2. Попадание по боссу подменяло спрайт светлой (местами белой) копией на
#    0.22–0.25 с: два фронта по 0 мс, у ската на трети экрана (E-03). Теперь
#    поверх тела ложится силуэт с подмесом Gold. Проверяем числа импульса,
#    цвет, частоту и то, что ни один босс больше не ставит светлую копию.
_ht = _rtA.get("HitTint.cs", "")
_ht_b = _method_bodies(_ht)
_ht_v = {n: _cs_float(_ht, n) for n in ("Rise", "Fall", "MinGap", "MaxMix")}
_gold = _palette_hex("Gold")
_ht_bad = []
if not _ht:
    _ht_bad.append(u"HitTint нет")
else:
    if None in _ht_v.values():
        _ht_bad.append(u"не разобраны числа импульса")
    else:
        if not 0.06 <= _ht_v["Rise"] <= 0.1:
            _ht_bad.append(u"нарастание %.2f с" % _ht_v["Rise"])
        if not 0.25 <= _ht_v["Fall"] <= 0.3:
            _ht_bad.append(u"спад %.2f с" % _ht_v["Fall"])
        if _ht_v["MaxMix"] > 0.25:
            _ht_bad.append(u"подмес %.0f %%" % (_ht_v["MaxMix"] * 100))
        # 0.4–0.5 с по заданию, и не меньше 0.5: иначе в одну секунду
        # влезают три импульса, то есть шесть перепадов.
        if not 0.5 <= _ht_v["MinGap"] <= 0.5:
            _ht_bad.append(u"интервал %.2f с" % _ht_v["MinGap"])
    if _ht_b.get("Pulse", "").count("SmoothStep") < 2:
        _ht_bad.append(u"импульс без плавных фронтов")
    if "MinGap" not in _ht_b.get("Hit", "") or "return false" not in _ht_b.get("Hit", ""):
        _ht_bad.append(u"частота не ограничена")
    if "MaxMix * Visuals.BossFlash" not in _ht_b.get("Apply", ""):
        _ht_bad.append(u"потолок подмеса не применяется")
    # Цвет и сила подмеса считаются так, как их смешивает Unity: проект в
    # линейном пространстве, и прирост яркости тёмного тела (Y ≈ 0.045) за
    # импульс обязан быть ниже чувствительного порога 0.05. [РАСЧЁТ]
    _tone_k = _cs_float(_ht, "ToneLevel")
    _dY_hit = None
    if "SafeLight(" not in _ht_b.get("Tone", _ht) or not _gold or _tone_k is None:
        _ht_bad.append(u"цвет подмеса не ограничен")
    else:
        _tone = [int(_gold[i:i + 2], 16) / 255.0 * _tone_k for i in (0, 2, 4)]
        _lin = [c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4 for c in _tone]
        _y_tone = 0.2126 * _lin[0] + 0.7152 * _lin[1] + 0.0722 * _lin[2]
        _dY_hit = (_ht_v["MaxMix"] or 0) * (_y_tone - 0.045)
        if sum(_tone) > 2.3:
            _ht_bad.append(u"подмес белит: сумма %.2f" % sum(_tone))
        if _dY_hit >= 0.05:
            _ht_bad.append(u"импульс светлит тёмное тело на %.3f" % _dY_hit)

_bossA = ("CrabBossController.cs", "InkBossController.cs", "JellyBossController.cs",
          "MantaBossController.cs", "OctopusBossController.cs", "SerpentBossController.cs",
          "SharkBossController.cs", "SwordfishBossController.cs")
for _n in _bossA:
    _src = _rtA.get(_n, "")
    _dmg = _method_bodies(_src).get("ApplyDamage", "")
    if "_tint.Hit()" not in _dmg:
        _ht_bad.append(u"%s: попадание без подмеса" % _n.replace("BossController.cs", ""))
    if ".sprite" in _dmg or "Refresh" in _dmg:
        _ht_bad.append(u"%s: попадание меняет спрайт" % _n.replace("BossController.cs", ""))
    if "_tint.Tick(" not in _src or "_tint.Track(" not in _src:
        _ht_bad.append(u"%s: подмес не ведётся" % _n.replace("BossController.cs", ""))

# Смерть пираньи и черепахи тоже подменяла спрайт светлой копией.
for _n in ("PiranhaBossController.cs", "TurtleBossController.cs"):
    if ".sprite" in _method_bodies(_rtA.get(_n, "")).get("Kill", ""):
        _ht_bad.append(u"%s: смерть подменяет спрайт" % _n.replace("BossController.cs", ""))

# В фабриках боссов не осталось светлых копий: параметр стал маской, а маска
# берёт у формы только прозрачность.
_fac = "".join(s for n, s in _rtA.items() if n.startswith("SpriteFactory"))
_flash_fns = _re.findall(r"public static Sprite (\w+)\([^)]*\bbool flash\b", _fac)
if _flash_fns:
    _ht_bad.append(u"светлые копии: " + ", ".join(sorted(set(_flash_fns))[:4]))
_mask_fns = sorted(set(_re.findall(r"public static Sprite (\w+)\([^)]*\bbool mask\b", _fac)))
_masked = _rtA.get("SpriteFactory.cs", "")
_masked = _masked[_masked.find("Masked(string key"):][:600] if "Masked(string key" in _masked else ""
if not _mask_fns or "new Color(1f, 1f, 1f, c.a)" not in _masked:
    _ht_bad.append(u"маски нет")
else:
    for _n, _src in _rtA.items():
        if _n.startswith("SpriteFactory"):
            continue
        for _m in _re.finditer(r"SpriteFactory\.(%s)\(([^()]*)\)" % "|".join(_mask_fns), _src):
            _args = _m.group(2).strip()
            if _args and _args.split(",")[-1].strip() != "false":
                _ht_bad.append(u"%s: %s(%s)" % (_n, _m.group(1), _args))
check(u"Вспышки: попадание по боссу — плавный подмес, а не подмена",
      OK if not _ht_bad else FAIL,
      (u"золото ≤ %.0f %% (+%.3f яркости), %.2f с вверх, %.2f с вниз, не чаще раза в %.2f с, боссов %d"
       % (_ht_v["MaxMix"] * 100, _dY_hit, _ht_v["Rise"], _ht_v["Fall"], _ht_v["MinGap"], len(_bossA)))
      if not _ht_bad else u"; ".join(_ht_bad[:6]))

# 3. Колесо (E-04, H-01, E-25, G-03). Геометрию, опорные точки, скорость и
#    10 000 вращений считает Tools/check_wheel.py — здесь его итог. Плюс
#    «Без вспышек»: указатель не бежит, а проявляется на выпавшем секторе.
import subprocess as _sp
_cw = _sp.run([sys.executable, "-I", "-X", "utf8", os.path.join("Tools", "check_wheel.py")],
              capture_output=True, text=True, encoding="utf-8", errors="replace")
_cw_fail = [l[len(u"ПРОВАЛ: "):] for l in _cw.stdout.splitlines() if l.startswith(u"ПРОВАЛ: ")]
_cw_speed = _re.search(u"самая высокая скорость: ([0-9.]+) об/с", _cw.stdout)
check(u"Колесо: без строба, вокруг центра, стрелка на выпавшем секторе",
      OK if _cw.returncode == 0 else FAIL,
      (u"крутится указатель, ≤ %s об/с; 10 000 вращений без промаха; шансы подписаны"
       % (_cw_speed.group(1) if _cw_speed else "?"))
      if _cw.returncode == 0 else (u"; ".join(_cw_fail[:4]) or _cw.stderr.strip()[-200:]))

_rw = _method_bodies(_rtA.get("UIManagerRewardsLogic.cs", ""))
_rw_ok = ("_wheelInstant = Visuals.NoFlashes" in _rw.get("OnSpinClicked", "")
          and "_wheelInstant" in _rw.get("TickWheel", "")
          and _rw.get("TickWheel", "").count("SmoothStep") >= 2)
check(u"Колесо: «Без вспышек» — результат сразу, с плавным появлением",
      OK if _rw_ok else FAIL,
      u"указатель гаснет и проявляется на выпавшем секторе, без бега по кругу")

# 4. Оболочка (E-05, E-07). Дуги перестраивались скачком 4 раза в секунду;
#    продление поднимало яркость за кадр в обход FadeIn.
_ce = _rtA.get("CollisionEnvelope.cs", "")
_es = _rtA.get("EnvelopeShape.cs", "")
_eu = _rtA.get("UIManagerEnvelope.cs", "")
_arc_hz = _cs_float(_ce, "ArcHz")
_sn = _es[_es.find("static float StepNoise("):][:500] if "static float StepNoise(" in _es else ""
_arc_bad = []
if _arc_hz is None or _arc_hz > 2:
    _arc_bad.append(u"ArcHz %s" % _arc_hz)
if _re.search(r"\bHash\([^;]*\barcStep\b", _es):
    _arc_bad.append(u"шум скачет по arcStep")
if _re.search(r"\bint\s+arcStep\b", _es):
    _arc_bad.append(u"номер формы целый")
if "SmoothStep" not in _sn:
    _arc_bad.append(u"нет плавного шума")
for _n, _src in (("бой", _ce), ("витрина", _eu)):
    if not _re.search(r"\bfloat\s+_arcStep\b", _src) or not _re.search(r"_arcStep\s*\+=\s*dt\s*\*", _src):
        _arc_bad.append(u"%s: форма меняется ступенькой" % _n)
check(u"Оболочка: дуги без скачков — 2 Гц и плавный шум",
      OK if not _arc_bad else FAIL,
      u"%.0f Гц, на «Тихо» вдвое реже; между формами SmoothStep" % _arc_hz
      if not _arc_bad else u"; ".join(_arc_bad))

_play = _method_bodies(_ce).get("Play", "")
_ext = _play[:_play.find("_life = def.Duration")] if "_life = def.Duration" in _play else ""
check(u"Оболочка: продление не поднимает яркость за кадр",
      OK if _re.search(r"if\s*\(extending\)\s*\{[^}]*_age\s*=\s*Mathf\.Min\(_age,\s*\w+\s*\*\s*FadeIn\)", _ext)
      else FAIL,
      u"возраст откатывается к текущей видимости, дальше обычные 0.2 с")

# 7 и 4 (E-09, E-11). Полноэкранные затемнения: тень ската снималась за кадр и
#    наползала за 0.2 с; чернила закрывали экран за один кадр.
_mt = _rtA.get("MantaBossController.cs", "")
_mt_b = _method_bodies(_mt)
_ecl_rise = _cs_float(_mt, "EclipseRise")
_ink = _rtA.get("InkScreen.cs", "")
_ink_b = _method_bodies(_ink)
_ink_rise = _cs_float(_ink, "RiseTime")
_dark_bad = []
if _re.search(r"_eclipseLevel\s*=\s*0f", _mt_b.get("TickArrival", "")):
    _dark_bad.append(u"тень ската снимается за кадр")
if _ecl_rise is None or _ecl_rise < 0.5 or "full / EclipseRise" not in _mt_b.get("TickEclipse", ""):
    _dark_bad.append(u"затмение быстрее 0.5 с")
if _ink_rise is None or _ink_rise < 0.2 or "Apply(1f, 1f)" in _ink_b.get("Splash", "") \
        or "RiseTime" not in _ink_b.get("Tick", ""):
    _dark_bad.append(u"чернила закрывают экран за кадр")
check(u"Вспышки: полноэкранные затемнения плавные",
      OK if not _dark_bad else FAIL,
      u"чернила %.1f с, затмение ската %.1f с, тень гаснет за mantaEclipseFade" % (_ink_rise, _ecl_rise)
      if not _dark_bad else u"; ".join(_dark_bad))

# 5. Белый пересвет (E-08): у 20 из 36 цветов удара сумма каналов была выше
#    2.7, у восьми — FFFFFF. Потолок 2.3 обязан стоять в каждой точке, где цвет
#    эффекта уходит на экран, а в таблицах не должно остаться чистого белого.
_pl = _rtA.get("Palette.cs", "")
_pl_lim = _cs_float(_pl, "LightLimit")
_sl = _method_bodies(_pl).get("SafeLight", "") if "SafeLight" in _pl else ""
_sl = _pl[_pl.find("static Color SafeLight("):][:400] if "static Color SafeLight(" in _pl else ""
_fx_b = _method_bodies(_rtA.get("FxManager.cs", ""))
_st_b = _method_bodies(_rtA.get("StatusFx.cs", ""))
_white_bad = []
if _pl_lim is None or _pl_lim > 2.3 or "limit / sum" not in _sl:
    _white_bad.append(u"SafeLight нет или потолок выше 2.3")
for _m in ("Burst", "Emit"):
    if "SafeLight(" not in _fx_b.get(_m, ""):
        _white_bad.append(u"частицы %s без потолка" % _m)
if _st_b.get("Paint", "").count("SafeLight(") < 2:
    _white_bad.append(u"статусы без потолка")
if not _re.search(r"SetHitColor\(Color color\)\s*=>\s*_flashColor\s*=\s*Palette\.SafeLight\(", _rtA.get("Obstacle.cs", "")):
    _white_bad.append(u"вспышка препятствия без потолка")
for _n in ("SkinFx.cs", "EnvelopeTable.cs"):
    if "Hot = Palette.SafeLight(C(hot))" not in _rtA.get(_n, ""):
        _white_bad.append(u"%s: таблица без потолка" % _n)
for _n in ("SkinFx.cs", "EnvelopeTable.cs", "Archetypes.cs"):
    _k = _rtA.get(_n, "").count('"FFFFFF"')
    if _k:
        _white_bad.append(u"%s: чистый белый ×%d" % (_n, _k))
# Стихии различимы: у каждого материала свой цвет удара и после потолка.
_mat_rows = _re.findall(r'D\("([0-9A-F]{6})",\s*AuraStyle\.\w+,\s*DestroyPattern\.\w+,\s*"([0-9A-F]{6})"',
                        _rtA.get("SkinFx.cs", ""))


def _safe_hex(h):
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    s = sum(c)
    k = 2.3 / s if s > 2.3 else 1.0
    return tuple(round(x * k, 3) for x in c)


# Потолок не имеет права слить два разных цвета в один: сколько было разных
# цветов удара в таблице, столько и осталось после него.
_hots = [_safe_hex(h) for _a, h in _mat_rows]
_raw_hots = set(h for _a, h in _mat_rows)
if len(set(_hots)) < len(_raw_hots):
    _white_bad.append(u"потолок слил %d цветов удара" % (len(_raw_hots) - len(set(_hots))))
check(u"Вспышки: белого пересвета нет — сумма каналов ≤ 2.3",
      OK if not _white_bad else FAIL,
      u"частицы, статусы, вспышка препятствия, таблицы; разных цветов удара %d из %d строк"
      % (len(set(_hots)), len(_hots))
      if not _white_bad else u"; ".join(_white_bad[:5]))

# 6. Честное «Тихо» (E-10) и «Без вспышек» (K-02). «Тихо» прежде не трогало
#    альфу частиц, разрушение, вспышки боссов, статусы, дуги и Flicker.
_vs = _rtA.get("Visuals.cs", "")
_quiet = dict((n, float(v)) for n, v in _re.findall(
    r"public static float (\w+) => Level == FxBrightness\.Quiet \? ([\d.]+)f : 1f;", _vs))
_q_want = {"ParticleAlpha": 0.75, "StatusAlpha": 0.75, "BossFlash": 0.5, "ArcRate": 0.5, "Particles": 0.75,
           "Motion": 0.5}
_q_bad = [u"%s=%s" % (n, _quiet.get(n)) for n, v in _q_want.items()
          if _quiet.get(n) is None or _quiet[n] > v + 1e-6]
_sfx = _rtA.get("SkinFxRuntime.cs", "")
_sfx_b = _method_bodies(_sfx)
_uses = (("FxManager.cs", "Visuals.ParticleAlpha"), ("StatusFx.cs", "Visuals.StatusAlpha"),
         ("HitTint.cs", "Visuals.BossFlash"), ("CollisionEnvelope.cs", "Visuals.ArcRate"),
         ("UIManagerEnvelope.cs", "Visuals.ArcRate"))
_q_bad += [u"%s не берёт %s" % (n, v) for n, v in _uses if v not in _rtA.get(n, "")]
if "SetDensity(Visuals.Particles)" not in _sfx_b.get("OnHazardDestroyed", ""):
    _q_bad.append(u"разрушение не редеет")
_ms = _sfx[_sfx.find("public static Vector2 MotionScale("):][:3000]
_flk = _ms[_ms.find("case SkinMotion.Flicker:"):][:900]
_flk_max = _cs_float(_sfx, "FlickerMaxSpeed")
if "Visuals.Motion" not in _ms:
    _q_bad.append(u"движение скина не тише")
if "Repeat(" in _flk or "? 1f : 0f" in _flk or _flk_max is None or 2.0 / _flk_max < 1.5:
    _q_bad.append(u"Flicker — ступенька или чаще раза в 1.5 с")
check(u"Вспышки: «Тихо» убавляет то, что вредит глазам",
      OK if not _q_bad else FAIL,
      u"частицы и статусы ×0.75, разрушение реже на четверть, подмес боссов ×0.5, дуги 1 Гц, "
      u"Flicker — дыхание" if not _q_bad else u"; ".join(_q_bad[:6]))

_sd = _rtA.get("SaveData.cs", "")
_nf_key = _re.search(r'NoFlashKey\s*=\s*"([^"]+)"', _sd)
_other_keys = set(_re.findall(r'const string (?!NoFlashKey)\w+Key\s*=\s*"([^"]+)"', _sd))
_ui_main = _rtA.get("UIManager.cs", "")
_nf_bad = []
if not _nf_key or _nf_key.group(1) in _other_keys:
    _nf_bad.append(u"нет своего ключа")
if not _re.search(r"PlayerPrefs\.GetInt\(SaveData\.NoFlashKey,\s*1\)\s*!=\s*0", _vs):
    _nf_bad.append(u"по умолчанию не включён")
if "Visuals.NoFlashes = !Visuals.NoFlashes" not in _ui_main or "settings.noFlashOn" not in _ui_main:
    _nf_bad.append(u"нет переключателя в настройках")
if "Visuals.NoFlashes" not in _method_bodies(_rtA.get("HitTint.cs", "")).get("Apply", ""):
    _nf_bad.append(u"босс не переходит на контур")
if "NoFlashes ? 0.5f : 1f" not in _vs:
    _nf_bad.append(u"оболочки не ×0.5")
check(u"Вспышки: «Без вспышек» есть и включён у всех по умолчанию",
      OK if not _nf_bad else FAIL,
      u"ключ %s, нет ключа — значит вкл (и у новых, и у прежних игроков); контур, колесо без вращения, оболочки ×0.5"
      % (_nf_key.group(1) if _nf_key else "?") if not _nf_bad else u"; ".join(_nf_bad))

# 8. Мелкие фронты (E-12…E-17). Каждый по отдельности мал, вместе — фоновая
#    рябь резких изменений. Числа разбираются из кода.
_fr_bad = []
_jl_b = _method_bodies(_rtA.get("JellyBossController.cs", ""))
if "_fieldAlpha = Mathf.MoveTowards(" not in _jl_b.get("TickFight", ""):
    _fr_bad.append(u"кольца поля медузы прыгают")
for _n, _var in (("SharkBossController.cs", "_breath"), ("InkBossController.cs", "_pulse")):
    _src = _rtA.get(_n, "")
    for _w, _a in _re.findall(r"Mathf\.Sin\(%s \* ([\d.]+)f\) \* ([\d.]+)f" % _var, _src):
        if float(_w) >= 8 and (float(_w) > 14 or float(_a) > 0.03):   # дрожь, а не дыхание
            _fr_bad.append(u"%s: дрожь %s рад/с ×%s" % (_n.replace("BossController.cs", ""), _w, _a))
    for _a, _w in _re.findall(r"([\d.]+)f \* Mathf\.Sin\(%s \* ([\d.]+)f\)" % _var, _src):
        if float(_w) >= 8 and (float(_w) > 14 or float(_a) > 0.03):   # дрожь, а не дыхание
            _fr_bad.append(u"%s: дрожь %s рад/с ×%s" % (_n.replace("BossController.cs", ""), _w, _a))
# Акула, смотрящая почти вертикально, переворачивала спрайт туда-обратно
# при колебании курса около нуля — до 8 перепадов в секунду. [ТЕСТ]
_sh = _rtA.get("SharkBossController.cs", "")
_flip_h = _cs_float(_sh, "FlipHysteresis")
if "bool flip = _forward.x < 0f" in _sh or _flip_h is None or _flip_h < 0.1:
    _fr_bad.append(u"акула переворачивается без запаса")
# Частицы проявляются, а не возникают за кадр.
_fx_src = _rtA.get("FxManager.cs", "")
_fx_fade = _cs_float(_fx_src, "FadeIn")
if _fx_fade is None or _fx_fade < 0.05 or "p.Age / fadeIn" not in _fx_b.get("Tick", ""):
    _fr_bad.append(u"частицы возникают за кадр")
_go = _rtA.get("GuardianOrb.cs", "")
if not _re.search(r"Mathf\.MoveTowards\(_chargeShown,\s*charge,\s*dt\s*/\s*0\.0[5-9]f\)|"
                  r"Mathf\.MoveTowards\(_chargeShown,\s*charge,\s*dt\s*/\s*0\.1f\)", _go):
    _fr_bad.append(u"страж темнеет за кадр")
_ob = _rtA.get("Obstacle.cs", "")
_ob_b = _method_bodies(_ob)
_fl_rise = _cs_float(_ob, "FlashRise")
if _fl_rise is None or not 0.05 <= _fl_rise <= 0.1 or "_flash = 1f" in _ob_b.get("Hit", ""):
    _fr_bad.append(u"вспышка препятствия за кадр")
if _re.search(r"float glow = _lit \?", _ob):
    _fr_bad.append(u"ступень ловушки")
_sw = _rtA.get("SwordfishBossController.cs", "")
if not _re.search(r"SmoothStep\(0f, 1f, Mathf\.Clamp01\(behind / 0\.\d+f\)\)", _sw):
    _fr_bad.append(u"пила шевронов меч-рыбы")
_st_src = _rtA.get("StatusFx.cs", "")
_st_rise = _cs_float(_st_src, "Rise")
if _st_rise is None or _st_rise < 0.15 or "Shown = Mathf.MoveTowards(" not in _st_b.get("Paint", ""):
    _fr_bad.append(u"повторный статус гасит накладку")
if "Repeat(t * 3.5f" in _st_src:
    _fr_bad.append(u"пила звёздной пыли")
_um = _method_bodies(_ui_main)
_fade_in = _cs_float(_ui_main, "ScreenFadeIn")
# Поверх игры экран проявляется с нуля; при смене экрана на экран — сразу,
# иначе между старым и новым на 0.2 с виден мир.
if _fade_in is None or _fade_in < 0.2 \
        or not _re.search(r"_fadeGroups\[i\]\.alpha = (?:swapped \? 1f : )?0f;", _um.get("LateUpdate", "")) \
        or "_bannerAge / ScreenFadeIn" not in _ui_main:
    _fr_bad.append(u"экраны и баннеры появляются за кадр")
check(u"Вспышки: мелких фронтов за кадр нет",
      OK if not _fr_bad else FAIL,
      u"статусы %.2f с, препятствие %.2f с, страж 0.08 с, экраны %.1f с, дрожь ≤ 14 рад/с и 3 %%"
      % (_st_rise, _fl_rise, _fade_in) if not _fr_bad else u"; ".join(_fr_bad[:6]))

# 9. Чем заменить вспышки и тряску (A-08, A-09, D-16). Урон был всплеском Aqua
#    (цвет «добра»), значок жизни гас за кадр, неуязвимость у покупных скинов
#    не была видна вовсе.
_gm_b = _method_bodies(_rtA.get("GameManager.cs", ""))
_bc = _rtA.get("BubbleController.cs", "")
_bc_b = _method_bodies(_bc)
_hit_bad = []
if "Palette.Coral" not in _gm_b.get("DamageBubble", "") or "Palette.Aqua" in _gm_b.get("DamageBubble", ""):
    _hit_bad.append(u"урон не Coral")
_bd = _gm_b.get("OnBossDamaged", "")
if ("Palette.Gold" not in _bd and "HitTint.Tone" not in _bd) or "Palette.Coral" in _bd:
    _hit_bad.append(u"удар по боссу не Gold")
_ht_time = _cs_float(_bc, "HurtTime")
_ht_sq = _cs_float(_bc, "HurtSquash")
_ht_pk = _cs_float(_bc, "HurtSquashPeak")
if _ht_time is None or abs(_ht_time - 0.35) > 0.01 or "Palette.Coral.WithAlpha(0.6f" not in _bc_b.get("TickHurtRing", ""):
    _hit_bad.append(u"нет кольца урона Coral 0.35 с")
if _ht_sq is None or abs(_ht_sq - 0.08) > 0.005 or _ht_pk is None or abs(_ht_pk - 0.12) > 0.005:
    _hit_bad.append(u"нет сжатия 8 % за 0.12 с")
if "_hurtAge = 0f" not in _bc_b.get("TakeDamage", ""):
    _hit_bad.append(u"урон не запускает отклик")
_inv_fade = _cs_float(_bc, "InvulnFade")
if "Mathf.Lerp(1f, 0.75f, _invulnShown)" not in _bc or "WithAlpha(0.35f * _invulnShown)" not in _bc \
        or "DashedRing()" not in _bc or _inv_fade is None or _inv_fade < 0.15:
    _hit_bad.append(u"неуязвимость не видна у скинов")
_life_fade = _cs_float(_ui_main, "LifeFade")
if _life_fade is None or abs(_life_fade - 0.4) > 0.01 or "TickLives(dt)" not in _um.get("Update", ""):
    _hit_bad.append(u"значок жизни гаснет за кадр")
check(u"Удар: отклик без вспышек — кольцо, сжатие, плавный значок",
      OK if not _hit_bad else FAIL,
      u"урон Coral, по боссу Gold; кольцо 0.35 с α 0.6, сжатие 8 %%, неуязвимость — пунктир и начинка ×0.75, "
      u"значок %.1f с" % _life_fade if not _hit_bad else u"; ".join(_hit_bad))

# I-01. Haptics зовёт vibrator.vibrate(long[], -1); без VIBRATE в манифесте
#    Android бросает SecurityException, и вибрации нет вовсе.
_man = read("Assets/Plugins/Android/AndroidManifest.xml")
_man_code = _re.sub(r"<!--.*?-->", "", _man, flags=_re.S)
_hap = strip_comments(read("Assets/Scripts/Haptics.cs"))
_needs_vibrate = '"vibrate"' in _hap or "vibrate(" in _hap
check(u"Вибрация: разрешение VIBRATE в манифесте",
      OK if not _needs_vibrate or _re.search(
          r'<uses-permission\s+android:name="android\.permission\.VIBRATE"\s*/>', _man_code) else FAIL,
      u"Haptics зовёт vibrator.vibrate — без разрешения его глушит SecurityException")

# ------------------------------------------------------------------- вывод
width = max(len(r[1]) for r in rows)
for status, name, detail in rows:
    print("[%s] %-*s %s" % (status, width, name, detail))

bad_count = sum(1 for r in rows if r[0] == FAIL)
warn_count = sum(1 for r in rows if r[0] == WARN)
print("\nитого: проверок %d, провалов %d, предупреждений %d" % (len(rows), bad_count, warn_count))
sys.exit(1 if bad_count else 0)
