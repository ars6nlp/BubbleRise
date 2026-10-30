using System;

namespace BubbleRise
{
    /// <summary>
    /// Всё, что игра хочет от площадки, на которой её запустили: сохранение,
    /// реклама, таблица рекордов, язык интерфейса.
    ///
    /// Интерфейс появился, когда к Яндекс Играм добавился Google Play. Реализаций
    /// две — <see cref="YandexSdkManager"/> для веба и <see cref="AndroidPlatform"/>
    /// для телефона, — и выбор между ними делается ровно в одном месте,
    /// в <see cref="GameBootstrap"/>. Остальная игра про площадку не знает ничего.
    ///
    /// Половина методов у Android-реализации пустая, и это нормально: игра обязана
    /// работать там, где рекламы и облака нет вовсе. Пустой вызов лучше проверки
    /// «а мы сейчас где» в двадцати местах подряд.
    /// </summary>
    public interface IPlatform
    {
        /// <summary>Площадка на связи: можно звать рекламу и писать в облако.</summary>
        bool IsReady { get; }

        /// <summary>Есть ли смысл предлагать ролик за жизнь. Нет — кнопку не рисуем.</summary>
        bool CanShowRewarded { get; }

        /// <summary>Рекорд загружен — из облака или из локального хранилища.</summary>
        event Action<int> BestDepthLoaded;

        /// <summary>Профиль применён к PlayerPrefs, подсистемам пора перечитать себя.</summary>
        event Action StateLoaded;

        /// <summary>Код языка ("ru", "en", ...) от площадки или от системы.</summary>
        event Action<string> LanguageDetected;

        void Init(GameConfig cfg);

        /// <summary>Игра загрузилась и в неё можно играть — можно убирать загрузчик.</summary>
        void NotifyGameReady();

        /// <summary>Начался и закончился активный геймплей. Яндексу это нужно для рекламы.</summary>
        void GameplayStart();
        void GameplayStop();

        /// <param name="onClosed">Позвать, когда ролик закрыт. Без рекламы — сразу.</param>
        void ShowInterstitial(Action onClosed);

        /// <param name="onGranted">Награда честно просмотрена.</param>
        /// <param name="onClosed">Ролик закрыт, с наградой или без.</param>
        void ShowRewarded(Action onGranted, Action onClosed);

        void SetBannerVisible(bool visible);

        /// <summary>Сохранить рекорд. Локально всегда, в облако — если оно есть.</summary>
        void SaveBest(int depth);

        /// <summary>Профиль изменился и его стоит отправить в облако.</summary>
        void MarkDirty(bool immediate = false);

        void SubmitScore(int depth);
    }
}
