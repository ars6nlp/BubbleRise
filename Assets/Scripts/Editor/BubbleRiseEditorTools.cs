using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BubbleRise.EditorTools
{
    /// <summary>
    /// Настройка проекта и сборка WebGL для Яндекс Игр — из меню, без ручного клика по инспектору.
    ///
    /// ВНИМАНИЕ: требования Яндекса к сборке (формат сжатия, лимит размера, обязательные вызовы SDK)
    /// периодически меняются. Перед финальной публикацией сверьтесь с yandex.ru/dev/games.
    /// </summary>
    public static class BubbleRiseEditorTools
    {
        const string MainScene = "Assets/Scenes/Main.unity";
        const string TemplateName = "PROJECT:YandexGames";

        [MenuItem("Tools/BubbleRise/1. Настроить проект для Яндекс Игр", false, 10)]
        public static void Configure()
        {
            if (!File.Exists(MainScene))
            {
                Debug.LogError($"[BubbleRise] Не найдена сцена {MainScene}");
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };

            // Название обязано совпадать с полем «Название» в консоли, с заголовком
            // на экране загрузки и с заголовком в меню (п. 5.1.3 требований площадки).
            PlayerSettings.productName = "Пузырёк: Подъём из глубин";
            PlayerSettings.companyName = "Bubble Rise";
            PlayerSettings.stripEngineCode = true;

            // runInBackground оставлен включённым намеренно. Паузу и глушение звука
            // на время рекламы и скрытой вкладки делает шаблон явно, через
            // OnPlatformPause/OnPlatformResume, — так поведение предсказуемо и
            // колбэки SDK гарантированно доходят до игры. Если отдать это движку,
            // Unity замирал бы ещё и от клика мимо канваса.
            PlayerSettings.runInBackground = true;

            var webgl = NamedBuildTarget.WebGL;
            PlayerSettings.SetScriptingBackend(webgl, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(webgl, ApiCompatibilityLevel.NET_Standard);
            // Medium, а не High: колбэки Yandex SDK вызываются из JS через SendMessage,
            // агрессивный стриппинг может их выкинуть (на всякий случай они ещё и [Preserve]).
            PlayerSettings.SetManagedStrippingLevel(webgl, ManagedStrippingLevel.Medium);

            // Gzip + Decompression Fallback — самый совместимый вариант для Яндекс Игр.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = false;
            // Не None. При None IL2CPP выкидывает поддержку исключений целиком, и
            // try/catch перестаёт работать — вместе с ним и защитный Safe() вокруг
            // вызовов SDK: любой отказ Яндекса не гасился бы, а ронял игру.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.threadsSupport = false;
            PlayerSettings.WebGL.template = TemplateName;

            AssetDatabase.SaveAssets();
            Debug.Log("[BubbleRise] Настройки применены: сцена Main в билде, WebGL-шаблон YandexGames, IL2CPP, Gzip + fallback.");
        }

        [MenuItem("Tools/BubbleRise/2. Собрать WebGL", false, 11)]
        public static void BuildWebGL()
        {
            Configure();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                Debug.Log("[BubbleRise] Переключаю платформу на WebGL…");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                {
                    Debug.LogError("[BubbleRise] Не удалось переключиться на WebGL. Установлен ли модуль WebGL Build Support в Unity Hub?");
                    _batchFailed = true;
                    return;
                }
            }

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(root, "Build", "WebGL");
            Directory.CreateDirectory(outputPath);

            // Страховка от главной ошибки релиза: в Development Build попадает панель
            // разработчика (#if DEVELOPMENT_BUILD). Гасим галку и в настройках окна
            // сборки, а не только в опциях вызова.
            EditorUserBuildSettings.development = false;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                double mb = summary.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[BubbleRise] Сборка готова: {outputPath}\nРазмер: {mb:F1} МБ, время: {summary.totalTime.TotalSeconds:F0} с.\n" +
                          "Для загрузки на Яндекс Игры заархивируйте содержимое папки (index.html должен лежать в корне архива).");
                // В batch-режиме открывать проводник некому и незачем.
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
        /// Точка входа для сборки из командной строки:
        ///
        ///     Unity.exe -batchmode -quit -projectPath &lt;путь&gt;
        ///               -executeMethod BubbleRise.EditorTools.BubbleRiseEditorTools.BuildWebGLBatch
        ///
        /// От пункта меню отличается одним: при неудаче завершает процесс ненулевым
        /// кодом. Без этого -quit выходит нулём даже после провала сборки, и скрипт
        /// снаружи считает поломанный билд успешным.
        /// </summary>
        public static void BuildWebGLBatch()
        {
            _batchFailed = false;
            BuildWebGL();
            EditorApplication.Exit(_batchFailed ? 1 : 0);
        }

        [MenuItem("Tools/BubbleRise/Открыть сцену Main", false, 30)]
        public static void OpenMainScene()
        {
            if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(MainScene);
        }
    }
}
