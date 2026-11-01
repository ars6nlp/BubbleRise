namespace BubbleRise
{
    /// <summary>
    /// Идентификаторы рекламных блоков AdMob.
    ///
    /// **Это шаблон.** Скопируйте файл в <c>Assets/Scripts/AdUnits.cs</c> и
    /// впишите свои блоки вместо «настоящих». Сам <c>Assets/Scripts/AdUnits.cs</c>
    /// в репозиторий не входит: боевые блоки принадлежат конкретному аккаунту
    /// AdMob, и чужому они не нужны — своё приложение всё равно регистрируется
    /// заново. Подробности в <c>docs/BUILD.md</c>.
    ///
    /// Секретом рекламные блоки не являются: эти строки лежат внутри любого
    /// собранного APK, и прочитать их может каждый, кто скачает приложение. Это
    /// адреса, а не ключи — в отличие от пароля к хранилищу подписи, которого в
    /// репозитории нет и не будет.
    ///
    /// **В отладочных сборках используются только тестовые блоки Google.** Клик
    /// по собственной настоящей рекламе — самая частая причина блокировки
    /// аккаунта AdMob, и разбирательство редко заканчивается в пользу
    /// разработчика. Поэтому подмена сделана не «не забудь переключить перед
    /// релизом», а автоматически: пока сборка отладочная, до настоящих блоков
    /// дотянуться неоткуда.
    ///
    /// Как есть, без правок, файл собирается и работает: везде стоят тестовые
    /// блоки, так что релизная сборка покажет тестовые объявления, а не чужие.
    /// </summary>
    public static class AdUnits
    {
        // --- настоящие блоки вашего приложения: впишите свои
        const string AppReal          = "ca-app-pub-3940256099942544~3347511713";
        const string BannerReal       = "ca-app-pub-3940256099942544/6300978111";
        const string InterstitialReal = "ca-app-pub-3940256099942544/1033173712";
        const string RewardedReal     = "ca-app-pub-3940256099942544/5224354917";

        // --- официальные тестовые блоки Google, одинаковые для всех разработчиков
        const string AppTest          = "ca-app-pub-3940256099942544~3347511713";
        const string BannerTest       = "ca-app-pub-3940256099942544/6300978111";
        const string InterstitialTest = "ca-app-pub-3940256099942544/1033173712";
        const string RewardedTest     = "ca-app-pub-3940256099942544/5224354917";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Отладочная сборка: только тестовые объявления.</summary>
        public const bool UseTestAds = true;
#else
        public const bool UseTestAds = false;
#endif

        public static string App          => UseTestAds ? AppTest          : AppReal;
        public static string Banner       => UseTestAds ? BannerTest       : BannerReal;
        public static string Interstitial => UseTestAds ? InterstitialTest : InterstitialReal;
        public static string Rewarded     => UseTestAds ? RewardedTest     : RewardedReal;
    }
}
