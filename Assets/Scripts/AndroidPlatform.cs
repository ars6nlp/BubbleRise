using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Площадка для сборки под Google Play: реклама AdMob и локальное сохранение.
    ///
    /// Облака и таблицы рекордов здесь нет сознательно — весь прогресс лежит в
    /// PlayerPrefs, которые на Android сохраняются сами, а заводить ради рекордов
    /// вход через аккаунт Google значит менять анкету о данных и просить у игрока
    /// то, без чего он прекрасно обходится.
    ///
    /// Реклама живёт в <see cref="AdMobAds"/>, здесь только проброс. Обратите
    /// внимание на <see cref="CanShowRewarded"/>: он отвечает «да» лишь когда ролик
    /// реально заряжен. Кнопка, ведущая к «реклама недоступна», хуже отсутствующей —
    /// игрок жмёт её сразу после проигрыша и получает второе разочарование.
    ///
    /// Второе назначение этого объекта — поведение приложения на телефоне:
    /// ориентация, пауза при сворачивании, экран, который не должен гаснуть.
    /// В вебе всем этим занимается вкладка браузера, здесь — некому.
    /// </summary>
    public class AndroidPlatform : MonoBehaviour, IPlatform
    {
        const string BestKey = SaveData.BestKey;

        /// <summary>
        /// Готовность площадки. Для GameManager это значит «можно звать рекламу»:
        /// облака и лидерборда здесь нет, а вот реклама появилась.
        /// </summary>
        public bool IsReady => _ads != null && _ads.Ready;

        /// <summary>
        /// Предлагать ролик стоит, только когда он реально заряжен. Кнопка, ведущая
        /// к «реклама недоступна», хуже отсутствующей кнопки: игрок нажимает её в
        /// тот момент, когда только что проиграл, и получает второе разочарование.
        /// </summary>
        public bool CanShowRewarded => _ads != null && _ads.RewardedReady;

        public event Action<int> BestDepthLoaded;
        public event Action StateLoaded;
        public event Action<string> LanguageDetected;

        float _savedTimeScale = 1f;
        bool _paused;
        AdMobAds _ads;

        public void Init(GameConfig cfg)
        {
            LockPortrait();

            // Реклама поднимается отдельным объектом: она переживает смену состояний
            // игры и сама держит загруженные объявления наготове.
            _ads = gameObject.AddComponent<AdMobAds>();
            _ads.Init();

            // Язык берём из системы один раз при запуске. Дальше его меняет только
            // сам игрок кнопкой в настройках, и этот выбор Loc уважает.
            LanguageDetected?.Invoke(SystemLanguageCode());

            // Рекорд отдаём сразу и синхронно: ждать нечего, а от него зависят
            // и магазин, и экран поражения.
            BestDepthLoaded?.Invoke(PlayerPrefs.GetInt(BestKey, 0));

            // Профиль уже в PlayerPrefs — подсистемам сообщаем, что можно читать.
            StateLoaded?.Invoke();
        }

        /// <summary>
        /// Игра вертикальная: пузырёк поднимается снизу вверх, и в альбомной
        /// ориентации от неё остаётся широкая полоска без запаса по высоте.
        /// Ориентацию в настройках проекта не трогаем — там она нужна свободной
        /// для Яндекса, — а здесь запираем на лету.
        /// </summary>
        static void LockPortrait()
        {
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = false;
            Screen.autorotateToLandscapeRight = false;
            Screen.orientation = ScreenOrientation.Portrait;
        }

        /// <summary>
        /// Язык системы в тот же вид, что присылает Яндекс: "ru" или "en".
        /// Всё, что не русский, ведём на английский — он у нас второй язык списка.
        /// </summary>
        static string SystemLanguageCode()
            => Application.systemLanguage == SystemLanguage.Russian ? "ru" : "en";

        /// <summary>Загрузчика у приложения нет — сообщать некому.</summary>
        public void NotifyGameReady() { }

        public void GameplayStart() { }
        public void GameplayStop() { }

        /// <summary>
        /// Если показать нечего — обратный вызов происходит немедленно, и игрок идёт
        /// дальше. Реклама не должна быть поводом застрять.
        /// </summary>
        public void ShowInterstitial(Action onClosed)
        {
            if (_ads != null) _ads.ShowInterstitial(onClosed);
            else onClosed?.Invoke();
        }

        /// <summary>Жизнь выдаётся только за честно досмотренный ролик.</summary>
        public void ShowRewarded(Action onGranted, Action onClosed)
        {
            if (_ads != null) _ads.ShowRewarded(onGranted, onClosed);
            else onClosed?.Invoke();
        }

        public void SetBannerVisible(bool visible) => _ads?.SetBannerVisible(visible);

        public void SaveBest(int depth)
        {
            PlayerPrefs.SetInt(BestKey, depth);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Отправлять некуда, но записать на диск нужно: Android может убить процесс
        /// в любой момент, и несохранённый прогресс пропадёт молча.
        /// </summary>
        public void MarkDirty(bool immediate = false) => PlayerPrefs.Save();

        /// <summary>Таблицы рекордов в этой сборке нет.</summary>
        public void SubmitScore(int depth) { }

        // ------------------------------------------------- жизненный цикл приложения

        /// <summary>
        /// Свернули приложение или пришёл звонок — игра обязана замереть и замолчать.
        /// Без этого пузырёк продолжает всплывать в кармане и погибает где-то там,
        /// а звук играет поверх чужого приложения.
        ///
        /// Прежний timeScale запоминаем, а не ставим единицу вслепую: панель
        /// разработчика умеет ускорять время, и после сворачивания оно должно
        /// вернуться таким, каким было.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (paused) Suspend(); else Resume();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) Resume(); else Suspend();
        }

        void Suspend()
        {
            if (_paused) return;
            _paused = true;

            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;

            // Момент, когда процесс может не пережить сворачивания.
            PlayerPrefs.Save();
        }

        void Resume()
        {
            if (!_paused) return;
            _paused = false;

            Time.timeScale = _savedTimeScale > 0.01f ? _savedTimeScale : 1f;
            AudioListener.pause = false;
        }
    }
}
