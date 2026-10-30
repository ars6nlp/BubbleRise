using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace BubbleRise
{
    /// <summary>
    /// Обёртка над Yandex Games SDK. Вся JS-логика живёт в WebGL-шаблоне
    /// (Assets/WebGLTemplates/YandexGames/index.html), сюда она пробрасывается
    /// через Assets/Plugins/WebGL/YandexSdk.jslib.
    ///
    /// В редакторе и в любой не-WebGL сборке SDK недоступен — все вызовы
    /// аккуратно деградируют: рекорд хранится в PlayerPrefs, реклама «показывается»
    /// мгновенно и без эффекта. Игра полностью работоспособна без SDK.
    ///
    /// ВАЖНО: имя GameObject должно быть ровно "YandexSdkManager" —
    /// по нему JS шлёт колбэки через unityInstance.SendMessage.
    /// </summary>
    public class YandexSdkManager : MonoBehaviour, IPlatform
    {
        public const string ObjectName = "YandexSdkManager";
        const string BestKey = SaveData.BestKey;

        /// <summary>
        /// Пауза между записями в облако. setData у площадки не бесплатен, а настройки
        /// игрок щёлкает пачками: без задержки одно открытие «Настроек» уходило бы
        /// в сеть пятью запросами подряд.
        /// </summary>
        const float SaveDebounce = 2f;

        /// <summary>SDK инициализирован и готов принимать вызовы.</summary>
        public bool IsReady { get; private set; }
        /// <summary>Рекорд загружен (из облака или из PlayerPrefs).</summary>
        public event Action<int> BestDepthLoaded;
        /// <summary>Сохранение применено к PlayerPrefs — подсистемам пора перечитать себя.</summary>
        public event Action StateLoaded;
        /// <summary>Код языка площадки ("ru", "en", ...) — приходит только в WebGL.</summary>
        public event Action<string> LanguageDetected;

        /// <summary>Игра на паузе по требованию площадки: показ рекламы или скрытая вкладка.</summary>
        public bool PlatformPaused { get; private set; }

        float _savedTimeScale = 1f;

        bool _saveDirty;
        float _saveTimer;

        Action _interstitialClosed;
        Action _rewardedGranted;
        Action _rewardedClosed;
        bool _gameReadySent;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int  YG_IsAvailable();
        [DllImport("__Internal")] static extern void YG_GameReady();
        [DllImport("__Internal")] static extern void YG_GameplayStart();
        [DllImport("__Internal")] static extern void YG_GameplayStop();
        [DllImport("__Internal")] static extern void YG_ShowInterstitial();
        [DllImport("__Internal")] static extern void YG_ShowRewarded();
        [DllImport("__Internal")] static extern void YG_ShowBanner();
        [DllImport("__Internal")] static extern void YG_HideBanner();
        [DllImport("__Internal")] static extern void YG_SaveState(string json);
        [DllImport("__Internal")] static extern void YG_RequestLang();
        [DllImport("__Internal")] static extern void YG_LoadState();
        [DllImport("__Internal")] static extern void YG_SubmitScore(string board, int value);
        static readonly bool WebGlBuild = true;
#else
        static int YG_IsAvailable() => 0;
        static void YG_GameReady() { }
        static void YG_GameplayStart() { }
        static void YG_GameplayStop() { }
        static void YG_ShowInterstitial() { }
        static void YG_ShowRewarded() { }
        static void YG_ShowBanner() { }
        static void YG_HideBanner() { }
        static void YG_SaveState(string json) { }
        static void YG_RequestLang() { }
        static void YG_LoadState() { }
        static void YG_SubmitScore(string board, int value) { }
        static readonly bool WebGlBuild = false;
#endif

        GameConfig _cfg;

        public void Init(GameConfig cfg)
        {
            _cfg = cfg;
            gameObject.name = ObjectName;

            if (Available())
            {
                IsReady = true;
                Safe(YG_RequestLang);
                Safe(YG_LoadState);
            }
            else
            {
                // Без SDK играем на локальном хранилище — оно и так полное.
                IsReady = false;
                BestDepthLoaded?.Invoke(PlayerPrefs.GetInt(BestKey, 0));
            }
        }

        static bool Available()
        {
            if (!WebGlBuild) return false;
            try { return YG_IsAvailable() != 0; }
            catch (Exception) { return false; }
        }

        /// <summary>Сообщить Яндексу, что игра загрузилась и в неё можно играть.</summary>
        public void NotifyGameReady()
        {
            if (_gameReadySent) return;
            _gameReadySent = true;
            Safe(YG_GameReady);
        }

        public void GameplayStart() => Safe(YG_GameplayStart);
        public void GameplayStop() => Safe(YG_GameplayStop);

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsReady)
            {
                onClosed?.Invoke();
                return;
            }
            _interstitialClosed = onClosed;
            Safe(YG_ShowInterstitial);
        }

        /// <summary>
        /// Можно ли предлагать ролик за жизнь. Без SDK кнопку не показываем вовсе:
        /// кнопка, которая ничего не делает, хуже её отсутствия.
        ///
        /// В редакторе и в отладочной сборке считаем, что можно, — иначе весь сценарий
        /// «умер, посмотрел, продолжил» невозможно ни разу проверить до публикации.
        /// </summary>
        public bool CanShowRewarded
        {
            get
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                return true;
#else
                return IsReady;
#endif
            }
        }

        /// <param name="onGranted">Награда честно просмотрена.</param>
        /// <param name="onClosed">Ролик закрыт (с наградой или без).</param>
        public void ShowRewarded(Action onGranted, Action onClosed)
        {
            if (!IsReady)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                // Роликов здесь нет, но проверить продолжение партии надо: считаем
                // просмотр состоявшимся. В релизной сборке этой ветки не существует.
                Debug.Log("[Yandex SDK] нет SDK — награда выдана как при просмотре (только для отладки)");
                onGranted?.Invoke();
#endif
                onClosed?.Invoke();
                return;
            }
            _rewardedGranted = onGranted;
            _rewardedClosed = onClosed;
            Safe(YG_ShowRewarded);
        }

        bool _bannerShown;

        /// <summary>
        /// Баннер видно только вне партии. В геймплее он перекрывал бы поле, по которому
        /// игрок таскает стража, — и модерации это не понравится, и играть будет нельзя.
        ///
        /// Состояние запоминаем: дёргать SDK на каждом переходе между меню и магазином
        /// незачем, показ уже идёт.
        /// </summary>
        public void SetBannerVisible(bool visible)
        {
            if (!IsReady || _bannerShown == visible) return;
            _bannerShown = visible;
            Safe(visible ? YG_ShowBanner : (Action)YG_HideBanner);
        }

        public void SaveBest(int depth)
        {
            PlayerPrefs.SetInt(BestKey, depth);
            PlayerPrefs.Save();
            MarkDirty(immediate: true);   // рекорд ждать не должен
        }

        /// <summary>
        /// Отправить локальное состояние в облако. Пишем не сразу: изменения
        /// копятся SaveDebounce секунд, иначе перещёлкивание настроек превращается
        /// в очередь запросов.
        /// </summary>
        public void MarkDirty(bool immediate = false)
        {
            _saveDirty = true;
            _saveTimer = immediate ? 0f : SaveDebounce;
            if (immediate) FlushSave();
        }

        void Update()
        {
            if (!_saveDirty) return;

            // Реальное время, а не игровое: во время рекламы timeScale = 0,
            // и сохранение зависло бы до её закрытия.
            _saveTimer -= Time.unscaledDeltaTime;
            if (_saveTimer <= 0f) FlushSave();
        }

        void FlushSave()
        {
            _saveDirty = false;
            _saveTimer = 0f;
            if (!IsReady) return;

            string json = SaveData.ToJson(SaveData.Capture());
            Safe(() => YG_SaveState(json));
        }

        void OnDisable()
        {
            if (_saveDirty) FlushSave();   // вкладку закрывают — успеть отдать прогресс
        }

        public void SubmitScore(int depth)
        {
            if (!IsReady) return;
            string board = string.IsNullOrEmpty(_cfg.leaderboardName) ? "depth" : _cfg.leaderboardName;
            Safe(() => YG_SubmitScore(board, depth));
        }

        static void Safe(Action call)
        {
            try { call(); }
            catch (Exception e) { Debug.LogWarning("[Yandex SDK] вызов не удался: " + e.Message); }
        }

        // ------------------------------------------------- колбэки из JS

        // ReSharper disable UnusedMember.Global — вызывается из JS через SendMessage
        [Preserve]
        public void OnSdkReady(string _)
        {
            IsReady = true;
            Safe(YG_RequestLang);
            Safe(YG_LoadState);
        }

        /// <summary>
        /// Облако прислало сохранение. Пустая строка — обычное дело: у игрока
        /// просто ещё нет облачных данных, играем на локальных.
        /// </summary>
        [Preserve]
        public void OnStateLoaded(string json)
        {
            var state = SaveData.FromJson(json);
            if (state != null) SaveData.Apply(state);

            StateLoaded?.Invoke();
            BestDepthLoaded?.Invoke(PlayerPrefs.GetInt(BestKey, 0));
        }

        [Preserve]
        public void OnLanguage(string code)
        {
            LanguageDetected?.Invoke(code);
        }

        /// <summary>
        /// Пауза от площадки. Требование Яндекс Игр: пока показывается реклама или
        /// пока вкладка скрыта, игра стоит и молчит — это проверяют руками.
        ///
        /// Обе причины сведены в один флаг ещё в шаблоне, сюда приходит уже готовое
        /// решение. Здесь важно другое: запомнить прежний timeScale и вернуть именно
        /// его, иначе панель разработчика с её ускорением времени ломалась бы
        /// после каждого ролика.
        /// </summary>
        [Preserve]
        public void OnPlatformPause(string _)
        {
            if (PlatformPaused) return;
            PlatformPaused = true;

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
        }

        [Preserve]
        public void OnPlatformResume(string _)
        {
            if (!PlatformPaused) return;
            PlatformPaused = false;

            Time.timeScale = _savedTimeScale > 0.01f ? _savedTimeScale : 1f;
            AudioListener.pause = false;
        }

        [Preserve]
        public void OnInterstitialClosed(string _)
        {
            var cb = _interstitialClosed;
            _interstitialClosed = null;
            cb?.Invoke();
        }

        [Preserve]
        public void OnRewarded(string _)
        {
            var cb = _rewardedGranted;
            _rewardedGranted = null;
            cb?.Invoke();
        }

        [Preserve]
        public void OnRewardedClosed(string _)
        {
            _rewardedGranted = null;
            var cb = _rewardedClosed;
            _rewardedClosed = null;
            cb?.Invoke();
        }

        [Preserve]
        public void OnSdkError(string message)
        {
            Debug.LogWarning("[Yandex SDK] " + message);
        }
        // ReSharper restore UnusedMember.Global
    }
}
