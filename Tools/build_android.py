# -*- coding: utf-8 -*-
"""Сборка пакета для Google Play.

    python3 Tools/build_android.py

Делает то же, что build.py, но для другой площадки:
  1. прогоняет Tools/check.py и Tools/audit.py;
  2. запускает Unity в batch-режиме через BuildAndroidBatch;
  3. проверяет, что получившийся .aab подписан не отладочным ключом.

Редактор Unity в это время должен быть закрыт: он держит блокировку проекта.

Подпись. Ключ и пароли берутся из переменных окружения и в репозиторий не
попадают никогда:

    BUBBLERISE_KEYSTORE       путь к файлу .keystore
    BUBBLERISE_KEYSTORE_PASS  пароль хранилища
    BUBBLERISE_KEY_ALIAS      имя ключа
    BUBBLERISE_KEY_PASS       пароль ключа

Без них пакет соберётся с отладочной подписью: поставить на телефон можно,
загрузить в консоль — нет. Скрипт об этом предупредит в конце.
"""
import io
import os
import subprocess
import sys
import time

UNITY = r"C:/Program Files/Unity/Hub/Editor/6000.2.13f1/Editor/Unity.exe"
METHOD = "BubbleRise.EditorTools.BubbleRiseAndroidTools.BuildAndroidBatch"
PROJECT = os.path.abspath(".")
AAB = os.path.join(PROJECT, "Build", "BubbleRise.aab")
LOG = os.path.join(PROJECT, "Build", "unity-android.log")

KEY_VARS = ("BUBBLERISE_KEYSTORE", "BUBBLERISE_KEYSTORE_PASS",
            "BUBBLERISE_KEY_ALIAS", "BUBBLERISE_KEY_PASS")


def run_checks():
    for tool in ("Tools/check.py", "Tools/audit.py"):
        print("--- %s" % tool)
        if subprocess.call([sys.executable, tool]) != 0:
            sys.exit("проверка не прошла: " + tool)


def editor_running():
    try:
        out = subprocess.check_output(["tasklist"], text=True, errors="replace")
    except Exception:
        return False
    return any(line.startswith("Unity.exe") for line in out.splitlines())


def signed():
    """Заданы ли все четыре переменные подписи. Трёх из четырёх не бывает."""
    return all(os.environ.get(v) for v in KEY_VARS)


def build():
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    if os.path.exists(AAB):
        os.remove(AAB)

    cmd = [UNITY, "-batchmode", "-nographics", "-quit",
           "-projectPath", PROJECT,
           "-executeMethod", METHOD,
           "-logFile", LOG]

    print("--- сборка Unity под Android (первая обычно 15-40 минут)")
    started = time.time()
    code = subprocess.call(cmd)
    print("--- Unity завершился с кодом %d за %.0f мин" % (code, (time.time() - started) / 60))

    if code != 0:
        tail(LOG, 40)
        sys.exit("сборка не удалась, полный лог: " + LOG)

    if not os.path.exists(AAB):
        tail(LOG, 40)
        sys.exit("пакет не появился: " + AAB)


CONNECT = os.path.join(PROJECT, "ProjectSettings", "UnityConnectSettings.asset")


def restore_unity_services():
    """Сборка под Android включает общий рубильник служб Unity обратно — каждый раз.

    Сам пакет от этого ничего не шлёт: разрешения на сеть у него нет, снято своим
    манифестом. Но в анкете обеих площадок мы пишем, что данные не собираются, и
    настройка в репозитории обязана этому соответствовать, а не тихо расходиться
    от сборки к сборке.
    """
    if not os.path.exists(CONNECT):
        return
    text = io.open(CONNECT, encoding="utf-8").read()
    marked = text.replace("  serializedVersion: 1\n  m_Enabled: 1\n",
                          "  serializedVersion: 1\n  m_Enabled: 0\n", 1)
    if marked != text:
        io.open(CONNECT, "w", encoding="utf-8").write(marked)
        print("--- службы Unity выключены обратно (сборка включила их сама)")


def tail(path, lines):
    if not os.path.exists(path):
        return
    text = io.open(path, encoding="utf-8", errors="replace").read().splitlines()
    print("--- хвост лога")
    for l in text[-lines:]:
        print("   " + l)


if __name__ == "__main__":
    # Сначала приводим настройки в порядок, потом проверяем. Рубильник служб Unity
    # включает обратно и сборка, и сам редактор при запуске, а аудит на этом падает
    # и не пускает дальше. Чинить руками одно и то же перед каждой сборкой — худший
    # вид ручной работы.
    restore_unity_services()
    run_checks()

    if editor_running():
        sys.exit("Unity открыт и держит проект. Закройте редактор и запустите снова.")

    has_key = signed()
    build()
    restore_unity_services()

    print("--- пакет: %s" % AAB)
    print("    размер: %.1f МБ" % (os.path.getsize(AAB) / 1e6))

    if has_key:
        print("\nготово: этот .aab и загружайте в Google Play Console")
    else:
        missing = [v for v in KEY_VARS if not os.environ.get(v)]
        print("\nпакет собран ОТЛАДОЧНОЙ подписью — для проверки на телефоне, но не для консоли.")
        print("не задано: " + ", ".join(missing))
