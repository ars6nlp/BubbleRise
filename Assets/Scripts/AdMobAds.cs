using System;
using UnityEngine;
#if UNITY_ANDROID
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
#endif

namespace BubbleRise
{
    /// <summary>
    /// Реклама AdMob: межстраничная, ролик за жизнь и баннер в меню.
    ///
    /// Весь класс живёт под <c>#if UNITY_ANDROID</c>. В сборке для Яндекса реклама
    /// своя, от площадки, и тащить туда второй рекламный SDK незачем — он добавил бы
    /// вес и требования, которых у веб-версии нет.
    ///
    /// Три правила, на которых держится всё остальное:
    ///
    /// 1. **Объявление грузится заранее.** Показать можно только то, что уже
    ///    загружено; запрос занимает секунды, и делать его в момент показа значит
    ///    показывать пустоту. После каждого показа сразу заказывается следующее.
    /// 2. **Отказ не ломает игру.** Сети нет, объявлений нет, SDK молчит — везде
    ///    вызывается тот же обратный вызов, что и при успешном закрытии. Игрок
    ///    не должен застревать на экране поражения из-за того, что реклама не
    ///    доехала.
    /// 3. **Колбэки SDK приходят не из главного потока.** Unity этого не любит,
    ///    поэтому всё, что трогает игру, откладывается в очередь и выполняется
    ///    в Update.
    /// </summary>
    public class AdMobAds : MonoBehaviour
    {
        /// <summary>Готовность: до согласия и инициализации ничего не показываем.</summary>
        public bool Ready { get; private set; }

        /// <summary>Есть ли заряженный ролик — от этого зависит, рисовать ли кнопку жизни.</summary>
        public bool RewardedReady =>
#if UNITY_ANDROID
            _rewarded != null && _rewarded.CanShowAd();
#else
            false;
#endif

        readonly System.Collections.Generic.Queue<Action> _main =
            new System.Collections.Generic.Queue<Action>();

        /// <summary>
        /// Минимум между двумя межстраничными и отсрочка первой от запуска игры.
        ///
        /// На Яндексе этим занималась сама площадка: её шаблон не показывал объявление
        /// чаще раза в минуту и никогда — в первую минуту сессии. У AdMob такого
        /// ограничителя нет, он покажет столько раз, сколько попросят. А просят у нас
        /// в четырёх местах сразу: старт партии, возврат в меню, вход в магазин и
        /// каждый третий проигрыш. Игрок, трижды погибший за первую минуту, получил бы
        /// три полноэкранных ролика подряд — это ровно то, что Google называет
        /// навязчивой рекламой, и снимают за это всё приложение, а не одно объявление.
        /// </summary>
        const float MinGapSeconds = 60f;
        const float GraceSeconds = 60f;

        float _nextInterstitialAt = GraceSeconds;

#if UNITY_ANDROID
        InterstitialAd _interstitial;
        RewardedAd _rewarded;
        BannerView _banner;
        bool _bannerVisible;
#endif

        // ------------------------------------------------------------- запуск

        /// <summary>
        /// Спрашиваем согласие на сбор данных и только потом поднимаем SDK.
        ///
        /// Порядок обязателен для Европы и Великобритании: показывать
        /// персонализированную рекламу до ответа игрока там нельзя. За пределами
        /// этих стран окно не появится вовсе — форму решает выдавать сам UMP.
        /// </summary>
        public void Init()
        {
#if UNITY_ANDROID
            var request = new ConsentRequestParameters();

            ConsentInformation.Update(request, error =>
            {
                if (error != null)
                {
                    // Не смогли спросить — работаем без персонализации, но работаем.
                    Debug.LogWarning("[Ads] согласие не запрошено: " + error);
                    Enqueue(StartSdk);
                    return;
                }

                ConsentForm.LoadAndShowConsentFormIfRequired(formError =>
                {
                    if (formError != null)
                        Debug.LogWarning("[Ads] форма согласия не показана: " + formError);

                    Enqueue(StartSdk);
                });
            });
#endif
        }

#if UNITY_ANDROID
        void StartSdk()
        {
            MobileAds.Initialize(_ =>
            {
                Enqueue(() =>
                {
                    Ready = true;
                    LoadInterstitial();
                    LoadRewarded();
                    LoadBanner();
                });
            });
        }
#endif

        // ------------------------------------------------- межстраничная реклама

        /// <param name="onClosed">Позовём в любом случае: и после показа, и если показать нечего.</param>
        public void ShowInterstitial(Action onClosed)
        {
#if UNITY_ANDROID
            // Время берём реальное: во время показа объявления игра стоит на паузе с
            // нулевым timeScale, и игровое время на этой минуте попросту не идёт.
            if (Time.realtimeSinceStartup < _nextInterstitialAt)
            {
                onClosed?.Invoke();
                return;
            }

            if (_interstitial != null && _interstitial.CanShowAd())
            {
                _nextInterstitialAt = Time.realtimeSinceStartup + MinGapSeconds;

                _interstitial.OnAdFullScreenContentClosed += () =>
                {
                    Enqueue(() => { LoadInterstitial(); onClosed?.Invoke(); });
                };
                _interstitial.OnAdFullScreenContentFailed += _ =>
                {
                    Enqueue(() => { LoadInterstitial(); onClosed?.Invoke(); });
                };
                _interstitial.Show();
                return;
            }

            // Нечего показывать — заказываем на будущее и пропускаем игрока дальше.
            LoadInterstitial();
#endif
            onClosed?.Invoke();
        }

#if UNITY_ANDROID
        void LoadInterstitial()
        {
            if (_interstitial != null) { _interstitial.Destroy(); _interstitial = null; }

            InterstitialAd.Load(AdUnits.Interstitial, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[Ads] межстраничная не загрузилась: " + error);
                    return;
                }
                _interstitial = ad;
            });
        }
#endif

        // ------------------------------------------------------ ролик за жизнь

        /// <param name="onReward">Ролик досмотрен честно — выдаём жизнь.</param>
        /// <param name="onClosed">Ролик закрыт, с наградой или без.</param>
        public void ShowRewarded(Action onReward, Action onClosed)
        {
#if UNITY_ANDROID
            if (_rewarded != null && _rewarded.CanShowAd())
            {
                bool granted = false;

                _rewarded.OnAdFullScreenContentClosed += () =>
                {
                    Enqueue(() =>
                    {
                        LoadRewarded();
                        if (granted) onReward?.Invoke();
                        onClosed?.Invoke();
                    });
                };
                _rewarded.OnAdFullScreenContentFailed += _ =>
                {
                    Enqueue(() => { LoadRewarded(); onClosed?.Invoke(); });
                };

                // Награду не выдаём сразу: сначала игрок должен закрыть ролик,
                // иначе жизнь появится поверх ещё открытого объявления.
                _rewarded.Show(_ => granted = true);
                return;
            }

            LoadRewarded();
#endif
            onClosed?.Invoke();
        }

#if UNITY_ANDROID
        void LoadRewarded()
        {
            if (_rewarded != null) { _rewarded.Destroy(); _rewarded = null; }

            RewardedAd.Load(AdUnits.Rewarded, new AdRequest(), (ad, error) =>
            {
                if (error != null || ad == null)
                {
                    Debug.LogWarning("[Ads] ролик не загрузился: " + error);
                    return;
                }
                _rewarded = ad;
            });
        }
#endif

        // ------------------------------------------------------------- баннер

        /// <summary>
        /// Баннер виден только вне партии. В игре он перекрывал бы поле, по которому
        /// игрок таскает стража, — и правилами AdMob это прямо запрещено.
        /// </summary>
        public void SetBannerVisible(bool visible)
        {
#if UNITY_ANDROID
            if (_banner == null || _bannerVisible == visible) return;

            _bannerVisible = visible;
            if (visible) _banner.Show(); else _banner.Hide();
#endif
        }

#if UNITY_ANDROID
        void LoadBanner()
        {
            if (_banner != null) { _banner.Destroy(); _banner = null; }

            // Адаптивный баннер: высота подбирается под ширину экрана, поэтому он
            // одинаково уместен и на узком телефоне, и на планшете.
            AdSize size = AdSize.GetCurrentOrientationAnchoredAdaptiveBannerAdSizeWithWidth(
                AdSize.FullWidth);

            _banner = new BannerView(AdUnits.Banner, size, AdPosition.Bottom);
            _banner.LoadAd(new AdRequest());
            _banner.Hide();
            _bannerVisible = false;
        }
#endif

        // ------------------------------------------- возврат в главный поток

        /// <summary>
        /// Колбэки SDK приходят из потока рекламы, а трогать игру оттуда нельзя.
        /// Складываем действия в очередь и разбираем её в Update.
        /// </summary>
        void Enqueue(Action action)
        {
            lock (_main) _main.Enqueue(action);
        }

        void Update()
        {
            while (true)
            {
                Action action;
                lock (_main)
                {
                    if (_main.Count == 0) return;
                    action = _main.Dequeue();
                }
                try { action(); }
                catch (Exception e) { Debug.LogWarning("[Ads] " + e.Message); }
            }
        }

        void OnDestroy()
        {
#if UNITY_ANDROID
            _interstitial?.Destroy();
            _rewarded?.Destroy();
            _banner?.Destroy();
#endif
        }
    }
}
