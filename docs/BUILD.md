# Сборка

Что нужно получить отдельно — этого нет и не будет в репозитории.

| Что | Зачем | Как получить |
|---|---|---|
| `Assets/Scripts/AdUnits.cs` | рекламные блоки AdMob | скопировать `BuildSetup/AdUnits.Example.cs`. Как есть он собирается и работает: всюду тестовые блоки Google. Свои боевые блоки впишите вместо «настоящих» |
| `Assets/GoogleMobileAds/`, `Assets/Plugins/Android/GoogleMobileAdsPlugin.androidlib/`, `googlemobileads-unity.aar`, `Assets/Plugins/iOS/` | SDK рекламы | импортировать **Google Mobile Ads for Unity** |
| Идентификатор приложения AdMob | без него игра не стартует вовсе | вписать в `Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset` **и** в `Assets/Plugins/Android/AndroidManifest.xml` (сейчас там тестовый Google) |
| Хранилище подписи (`.keystore`) | подпись релиза | создать своё: Player → Publishing Settings → Keystore Manager. **Никогда не коммитить** |
| Ссылка на политику конфиденциальности | требование Google Play | своя страница; образец — `docs/privacy-policy.md` |

Пароли к хранилищу Unity не хранит и в `ProjectSettings` не пишет — их вводят при сборке
или передают через переменные окружения.

## Шаги

1. Поставить **Unity 6000.2.13f1** с Android Build Support (OpenJDK, SDK/NDK). WebGL нужен
   только для сборки под Яндекс Игры.
2. Открыть проект, переключиться на Android (File → Build Profiles).
3. Импортировать Google Mobile Ads и создать `AdUnits.cs` (см. таблицу).
4. Assets → External Dependency Manager → Android Resolver → Force Resolve.
5. Player Settings: своё **имя пакета**, версия, keystore (Publishing Settings → Custom
   Keystore) и пароли к нему.
6. Сборка: `python3 Tools/build.py` — проверки, сборка и архив для консоли.
   Для Android отдельно: `python3 Tools/build_android.py`.
7. Поднимать **Bundle Version Code** перед каждой загрузкой.

В обоих скриптах путь к Unity задан константой `UNITY` в начале файла — поправьте под себя,
если редактор стоит не в `C:/Program Files/Unity/Hub/Editor/`.

## Проверки без редактора

| Команда | Что делает |
|---|---|
| `python3 Tools/check.py` | компиляция обеих сборок бандлом Roslyn из Unity + `node --check` для шаблона WebGL |
| `python3 Tools/audit.py` | три сотни проверок готовности к публикации: локализация, экономика, раскладка, эффекты, правила безопасности для глаз |
| `python3 Tools/preview_*.py` | рисует процедурную графику в PNG тем же кодом, что и игра (нужен Pillow) |
| `python3 Tools/simulate_*.py` | прогоняет экономику наград и генератор препятствий по числам из конфига |

`check.py` и `audit.py` возвращают ненулевой код при провале и годятся для CI.

## Правила, которые проверяет аудит

Часть проверок — про безопасность для глаз: нет вспышек на весь экран, нет строба и тряски
камеры, не больше трёх резких изменений яркости в секунду, нет белого пересвета, появление
эффектов не быстрее 0.15–0.2 с, затухание 0.4–0.6 с. Это не стиль, а требование: ломать их
не следует даже ради красивого эффекта.
