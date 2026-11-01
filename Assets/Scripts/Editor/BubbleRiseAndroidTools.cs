using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BubbleRise.EditorTools
{
    /// <summary>
    /// Настройка и сборка пакета для Google Play.
    ///
    /// Всё, что расходится между площадками, живёт здесь, а не в общих настройках
    /// проекта: ориентация, архитектуры, формат пакета. Общие настройки остаются
    /// такими, какими их хочет видеть Яндекс, — иначе одна платформа ломала бы
    /// проверки другой.
    ///
    /// Подпись ключом сюда не зашита. Ключ и пароль берутся из переменных окружения
    /// (см. <see cref="ApplyKeystore"/>), поэтому в репозитории их нет и быть не может.
    /// Без них Unity подписывает отладочным ключом — этого хватает, чтобы поставить
    /// игру на телефон и проверить, но не хватает для загрузки в консоль.
    /// </summary>
    public static class BubbleRiseAndroidTools
    {
        const string MainScene = "Assets/Scenes/Main.unity";

        /// <summary>
        /// Идентификатор пакета. Меняется только до первой публикации: после неё
        /// это другое приложение, и перенести в него установки и отзывы нельзя.
        /// </summary>
        const string PackageName = "com.bubblerise.ascent";

        [MenuItem("Tools/BubbleRise/3. Настроить проект для Google Play", false, 20)]
        public static void Configure()
        {
            if (!File.Exists(MainScene))
            {
                Debug.LogError($"[BubbleRise] Не найдена сцена {MainScene}");
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };

            // Подпись под иконкой на телефоне. Английская и короткая — это два разных
            // требования, и оба важны.
            //
            // Английская, потому что основной язык магазина у нас английский; русский
            // игрок всё равно увидит игру по-русски, язык интерфейса берётся из системы.
            //
            // Короткая, потому что лаунчер обрезает подпись примерно на двенадцати
            // знаках: «Bubble Rise: Ascent» превратилось бы в «Bubble Ris…». В магазине
            // название полное, там лимит тридцать знаков, — это разные поля.
            //
            // У сборки для Яндекса имя своё, русское: его задаёт BubbleRiseEditorTools.
            PlayerSettings.productName = "Bubble Rise";
            PlayerSettings.companyName = "Bubble Rise";
            PlayerSettings.stripEngineCode = true;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetApplicationIdentifier(android, PackageName);
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(android, ApiCompatibilityLevel.NET_Standard);
            // Medium, как и в вебе: агрессивный стриппинг вырезает то, что зовётся
            // только через рефлексию, а ловить это уже на телефоне — худший вариант.
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Medium);

            // Обе архитектуры: ARM64 требует Google, ARMv7 добавляет старые телефоны,
            // которых в бюджетном сегменте ещё много. Размер от этого растёт, но
            // пакет и так крошечный — ассетов в нём нет.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            // Auto = самый свежий уровень, который умеет установленная Unity.
            // Google каждый год поднимает минимально допустимый target, и зашивать
            // сюда число значит однажды получить отказ на ровном месте.
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;

            // Рисуем под вырезом и под полосой жестов, а интерфейс сам держится
            // внутри безопасной зоны (UIManager). Иначе Android обводит игру
            // чёрными полями, и на современном телефоне она выглядит как окно.
            PlayerSettings.Android.renderOutsideSafeArea = true;

            // Ориентацию в общих настройках оставляем свободной — такой её требует
            // Яндекс. На телефоне её запирает AndroidPlatform уже в рантайме.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;

            PlayerSettings.Android.forceInternetPermission = false;
            PlayerSettings.Android.forceSDCardPermission = false;

            // Формат для консоли: Play принимает только Android App Bundle.
            EditorUserBuildSettings.buildAppBundle = true;

            BubbleRiseIcon.Generate();
            ApplyIcons(android);

            AssetDatabase.SaveAssets();
            Debug.Log($"[BubbleRise] Android настроен: {PackageName}, IL2CPP, ARMv7+ARM64, " +
                      $"minSdk 24, target Auto, AAB, портрет в рантайме.");
        }

        /// <summary>
        /// Иконка приложения на всех плотностях экрана.
        ///
        /// Адаптивную пару слоёв (фон + передний план) сознательно не задаём:
        /// её тип живёт в отдельной сборке модуля Android, из-за чего офлайн-проверка
        /// Tools/check.py перестала бы компилировать этот файл. Обычной иконки
        /// достаточно: лаунчер обрежет её своей маской, а на ней по краям только
        /// вода — терять там нечего, пузырёк и страж сидят в центре.
        /// </summary>
        static void ApplyIcons(NamedBuildTarget target)
        {
            var legacy = BubbleRiseIcon.LegacyTexture;
            if (legacy == null)
            {
                Debug.LogWarning("[BubbleRise] Иконка не найдена — пакет соберётся с иконкой Unity.");
                return;
            }

            int count = PlayerSettings.GetIconSizes(target, IconKind.Application).Length;
            if (count == 0) return;

            var icons = new Texture2D[count];
            for (int i = 0; i < count; i++) icons[i] = legacy;
            PlayerSettings.SetIcons(target, icons, IconKind.Application);
        }

        /// <summary>
        /// Ключ подписи из переменных окружения. Ни файл, ни пароли в репозиторий
        /// не попадают — этого нельзя допускать в принципе, потерянный или утёкший
        /// ключ означает, что обновить приложение больше нечем.
        ///
        ///     BUBBLERISE_KEYSTORE       путь к .keystore
        ///     BUBBLERISE_KEYSTORE_PASS  пароль хранилища
        ///     BUBBLERISE_KEY_ALIAS      имя ключа
        ///     BUBBLERISE_KEY_PASS       пароль ключа
        ///
        /// Переменных нет — собираем отладочной подписью: на телефон поставится,
        /// в консоль не примут.
        /// </summary>
        static void ApplyKeystore()
        {
            string path = Environment.GetEnvironmentVariable("BUBBLERISE_KEYSTORE");
            string storePass = Environment.GetEnvironmentVariable("BUBBLERISE_KEYSTORE_PASS");
            string alias = Environment.GetEnvironmentVariable("BUBBLERISE_KEY_ALIAS");
            string keyPass = Environment.GetEnvironmentVariable("BUBBLERISE_KEY_PASS");

            bool complete = !string.IsNullOrEmpty(path) && !string.IsNullOrEmpty(storePass)
                            && !string.IsNullOrEmpty(alias) && !string.IsNullOrEmpty(keyPass);

            if (!complete)
            {
                // Ключ мог быть настроен руками через Keystore Manager — тогда сбрасывать
                // его нельзя: сборка молча ушла бы с отладочной подписью, а разработчик
                // узнал бы об этом только от консоли, которая её не примет.
                if (PlayerSettings.Android.useCustomKeystore)
                {
                    Debug.Log("[BubbleRise] Переменные окружения не заданы, но в проекте уже настроен " +
                              "свой ключ — оставляем его. Пароль Unity спросит сама.");
                    return;
                }

                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogWarning("[BubbleRise] Ключ подписи не задан — сборка пойдёт с отладочной подписью. " +
                                 "Для загрузки в Google Play задайте BUBBLERISE_KEYSTORE и остальные переменные.");
                return;
            }

            if (!File.Exists(path))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                Debug.LogError($"[BubbleRise] Хранилище ключей не найдено: {path}");
                return;
            }

            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = path;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            Debug.Log("[BubbleRise] Сборка будет подписана ключом из " + path);
        }

        [MenuItem("Tools/BubbleRise/4. Собрать пакет для Google Play", false, 21)]
        public static void BuildAndroid()
        {
            Configure();
            ApplyKeystore();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                Debug.Log("[BubbleRise] Переключаю платформу на Android…");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    Debug.LogError("[BubbleRise] Не удалось переключиться на Android. " +
                                   "Установлен ли модуль Android Build Support в Unity Hub?");
                    _batchFailed = true;
                    return;
                }
            }

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputDir = Path.Combine(root, "Build");
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "BubbleRise.aab");

            // Та же страховка, что и в вебе: в Development Build попадает панель
            // разработчика, и уехать с ней в магазин нельзя.
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                double mb = summary.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[BubbleRise] Пакет готов: {outputPath}\n" +
                          $"Размер: {mb:F1} МБ, время: {summary.totalTime.TotalSeconds:F0} с.");
                if (!Application.isBatchMode) EditorUtility.RevealInFinder(outputPath);
            }
            else
            {
                _batchFailed = true;
                Debug.LogError($"[BubbleRise] Сборка не удалась: {summary.result}, ошибок: {summary.totalErrors}");
            }
        }

        static bool _batchFailed;

        /// <summary>
        /// Точка входа для сборки из командной строки, см. Tools/build_android.py.
        /// При неудаче завершает процесс ненулевым кодом: без этого -quit выходит
        /// нулём даже после провала, и скрипт снаружи считает поломанный пакет готовым.
        /// </summary>
        public static void BuildAndroidBatch()
        {
            _batchFailed = false;
            BuildAndroid();
            EditorApplication.Exit(_batchFailed ? 1 : 0);
        }
    }
}
