# -*- coding: utf-8 -*-
"""Проверка проекта без запуска редактора Unity.

    python3 Tools/check.py

Компилирует обе сборки скриптов бандлом Roslyn из самого Unity (по .rsp, который
редактор уже сгенерировал в Library/Bee) и прогоняет node --check по скриптам
WebGL-шаблона. Полезно, когда правки делаются вне редактора.
"""
import glob
import io
import os
import re
import subprocess
import sys

UNITY = r"C:/Program Files/Unity/Hub/Editor/6000.2.13f1/Editor/Data"
DOTNET = UNITY + "/NetCoreRuntime/dotnet.exe"
CSC = UNITY + "/DotNetSdkRoslyn/csc.dll"
OUT = "Temp/check"


def find_rsp(name):
    """Самый свежий список ссылок, а не первый по алфавиту.

    Unity держит несколько папок .dag сразу, и в старых лежат ссылки на прежний
    состав проекта. После импорта Google Mobile Ads алфавитно первая папка ещё не
    знала про его библиотеки, и проверка валилась на «тип не найден», хотя сам
    редактор собирал проект без единой ошибки.
    """
    hits = glob.glob("Library/Bee/artifacts/*.dag/" + name)
    if not hits:
        sys.exit("не найден " + name + " — откройте проект в Unity хотя бы раз")
    return max(hits, key=os.path.getmtime)


def sources(pattern, exclude_editor=False):
    """Файлы под сборку. Редакторские скрипты в рантайм-сборку не входят —
    ровно как у самой Unity: Assembly-CSharp не видит UnityEditor, и попытка
    собрать их вместе разваливается, как только активной платформой становится
    не редактор, а телефон."""
    files = sorted(glob.glob(pattern, recursive=True))
    if exclude_editor:
        files = [f for f in files if "Editor" not in f.replace("\\", "/").split("/")]
    return files


# Дефайны редактора. Без них компилятор видит то же, что увидит релизная сборка
# под телефон: ветки `#if UNITY_ANDROID && !UNITY_EDITOR` (площадка, реклама,
# вибрация) и `#else` у дев-кода. Rsp из Library собран для редактора, и раньше
# эти ветки не компилировались вовсе — ошибка в них всплыла бы только при сборке.
RELEASE_DROP = ("-define:UNITY_EDITOR", "-define:DEVELOPMENT_BUILD", "-define:UNITY_INCLUDE_TESTS")


def compile_assembly(rsp_name, sources_glob, out_dll, extra_refs=(), exclude_editor=False,
                     release=False):
    src = io.open(find_rsp(rsp_name), encoding="utf-8").read().splitlines()
    opts = [l for l in src
            if not l.startswith("-out:") and not l.startswith("-refout:")
            and not l.strip().startswith('"Assets')
            # Ссылку на готовую Assembly-CSharp убираем: редакторская сборка должна
            # видеть ровно те типы, которые мы только что скомпилировали в Check.dll.
            # Иначе любой runtime-тип в скрипте из Assets/Scripts/Editor становится
            # «объявлен в двух сборках сразу» — ошибка на ровном месте.
            and not (l.startswith("-r:") and "Assembly-CSharp" in l)
            and not (release and any(l.startswith(d) for d in RELEASE_DROP))
            and not (release and l.startswith("-r:") and "UnityEditor" in l)]
    files = sources(sources_glob, exclude_editor)
    opts.append('-out:"%s/%s"' % (OUT, out_dll))
    opts += ['-r:"%s/%s"' % (OUT, r) for r in extra_refs]
    opts += ['"%s"' % f.replace("\\", "/") for f in files]

    rsp = "%s/%s.rsp" % (OUT, out_dll)
    io.open(rsp, "w", encoding="utf-8").write("\n".join(opts) + "\n")

    log = "%s/%s.log" % (OUT, out_dll)
    with io.open(log, "w", encoding="utf-8") as f:
        result = subprocess.run([DOTNET, CSC, "@" + rsp], stdout=f, stderr=subprocess.STDOUT)

    text = io.open(log, encoding="utf-8", errors="replace").read()
    errors = [l for l in text.splitlines() if "error CS" in l]
    # Компилятор мог упасть, не напечатав ни одной «error CS»: тогда считать
    # прогон чистым нельзя — его просто не было.
    if result.returncode != 0 and not errors:
        errors = ["csc вернул код %d, подробности в %s" % (result.returncode, log)]

    # Предупреждения считаем тоже: неиспользованное поле или недостижимый код —
    # обычный след недоделанной правки, и ловить его глазами в логе Unity дороже,
    # чем увидеть число здесь. Дубли убираем: одна и та же строка повторяется у
    # каждой сборки, где файл участвует.
    warnings = sorted({l.strip() for l in text.splitlines() if "warning CS" in l})

    print("%-22s файлов: %-3d ошибок: %-2d предупреждений: %d"
          % (out_dll, len(files), len(errors), len(warnings)))
    for e in errors[:10]:
        print("   " + e.strip())
    for w in warnings[:10]:
        print("   " + w)
    return len(errors)


def check_template():
    """Блок с createUnityInstance пропускаем: там директивы Unity и плейсхолдеры."""
    html = io.open("Assets/WebGLTemplates/YandexGames/index.html", encoding="utf-8").read()
    blocks = re.findall(r"<script>(.*?)</script>", html, re.S)
    bad = 0
    for i, b in enumerate(blocks):
        if "createUnityInstance" in b:
            continue
        path = "%s/tpl_%d.js" % (OUT, i)
        io.open(path, "w", encoding="utf-8").write(b)
        r = subprocess.run(["node", "--check", path], capture_output=True, text=True)
        if r.returncode:
            bad += 1
            print("шаблон, блок %d: ОШИБКА\n%s" % (i, r.stderr[:600]))
    print("%-22s блоков: %-3d ошибок: %d" % ("index.html", len(blocks), bad))
    return bad


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    bad = compile_assembly("Assembly-CSharp.rsp", "Assets/Scripts/**/*.cs", "Check.dll",
                           exclude_editor=True)
    bad += compile_assembly("Assembly-CSharp-Editor.rsp", "Assets/Scripts/Editor/**/*.cs",
                            "CheckEditor.dll", extra_refs=["Check.dll"])
    bad += compile_assembly("Assembly-CSharp.rsp", "Assets/Scripts/**/*.cs", "CheckRelease.dll",
                            exclude_editor=True, release=True)
    bad += check_template()
    print("ИТОГ:", "всё чисто" if bad == 0 else "проблем: %d" % bad)
    sys.exit(1 if bad else 0)
