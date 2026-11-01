# -*- coding: utf-8 -*-
"""Сборка WebGL и архив для загрузки в консоль Яндекс Игр.

    python3 Tools/build.py

Что делает:
  1. прогоняет Tools/check.py и Tools/audit.py — собирать заведомо сломанное незачем;
  2. запускает Unity в batch-режиме через BuildWebGLBatch;
  3. складывает СОДЕРЖИМОЕ Build/WebGL в Build/BubbleRise-YandexGames.zip,
     чтобы index.html оказался в корне архива — консоль требует именно так.

Редактор Unity в это время должен быть закрыт: он держит блокировку проекта, и
второй экземпляр её не получит.
"""
import io
import os
import subprocess
import sys
import time
import zipfile

UNITY = r"C:/Program Files/Unity/Hub/Editor/6000.2.13f1/Editor/Unity.exe"
METHOD = "BubbleRise.EditorTools.BubbleRiseEditorTools.BuildWebGLBatch"
PROJECT = os.path.abspath(".")
OUT_DIR = os.path.join(PROJECT, "Build", "WebGL")
ZIP_PATH = os.path.join(PROJECT, "Build", "BubbleRise-YandexGames.zip")
LOG = os.path.join(PROJECT, "Build", "unity-build.log")


def run_checks():
    for tool in ("Tools/check.py", "Tools/audit.py"):
        print("--- %s" % tool)
        if subprocess.call([sys.executable, tool]) != 0:
            sys.exit("проверка не прошла: " + tool)


def editor_running():
    """Unity держит блокировку проекта, пока открыт. Второй экземпляр её не получит."""
    try:
        out = subprocess.check_output(["tasklist"], text=True, errors="replace")
    except Exception:
        return False
    return any(line.startswith("Unity.exe") for line in out.splitlines())


def build():
    os.makedirs(os.path.dirname(LOG), exist_ok=True)
    cmd = [UNITY, "-batchmode", "-nographics", "-quit",
           "-projectPath", PROJECT,
           "-executeMethod", METHOD,
           "-logFile", LOG]

    print("--- сборка Unity (обычно 10-30 минут)")
    started = time.time()
    code = subprocess.call(cmd)
    print("--- Unity завершился с кодом %d за %.0f мин" % (code, (time.time() - started) / 60))

    if code != 0:
        tail(LOG, 40)
        sys.exit("сборка не удалась, полный лог: " + LOG)


def tail(path, lines):
    if not os.path.exists(path):
        return
    text = io.open(path, encoding="utf-8", errors="replace").read().splitlines()
    print("--- хвост лога")
    for l in text[-lines:]:
        print("   " + l)


def pack():
    index = os.path.join(OUT_DIR, "index.html")
    if not os.path.exists(index):
        sys.exit("в %s нет index.html — сборка не дошла до конца" % OUT_DIR)

    if os.path.exists(ZIP_PATH):
        os.remove(ZIP_PATH)

    total = 0
    with zipfile.ZipFile(ZIP_PATH, "w", zipfile.ZIP_DEFLATED) as z:
        for root, _, files in os.walk(OUT_DIR):
            for name in files:
                full = os.path.join(root, name)
                # путь внутри архива — относительно папки сборки, чтобы
                # index.html лежал в корне, как требует консоль
                z.write(full, os.path.relpath(full, OUT_DIR))
                total += os.path.getsize(full)

    print("--- архив: %s" % ZIP_PATH)
    print("    файлов распакованных: %.1f МБ, архив: %.1f МБ"
          % (total / 1e6, os.path.getsize(ZIP_PATH) / 1e6))


if __name__ == "__main__":
    run_checks()

    if editor_running():
        sys.exit("Unity открыт и держит проект. Закройте редактор и запустите снова.")

    build()
    pack()
    print("\nготово: этот zip и загружайте в консоль Яндекс Игр")
