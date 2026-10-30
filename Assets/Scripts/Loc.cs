using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    public enum Language { Ru = 0, En = 1 }

    /// <summary>
    /// Локализация. Все видимые строки лежат здесь и нигде больше — так их можно
    /// пересчитать глазами и не потерять при переводе.
    ///
    /// Язык берётся от площадки и только от неё: SDK присылает язык интерфейса при
    /// инициализации, и он побеждает всегда. Это требование площадки (п. 2.14): игра
    /// из списка английских игр обязана открываться по-английски, что бы игрок ни
    /// нажимал раньше. Кнопка в настройках меняет язык до перезагрузки.
    ///
    /// Название игры тоже локализовано: по п. 5.1.3 оно должно совпадать с полем
    /// «Название» того же языка в консоли разработчика.
    /// </summary>
    public static class Loc
    {
        const string Key = SaveData.LangKey;

        /// <summary>Язык сменился — интерфейс нужно перестроить.</summary>
        public static event Action Changed;

        static Language _current = Language.Ru;
        static bool _loaded;

        /// <summary>
        /// Язык, который прислала площадка. Храним отдельно от _current, потому что
        /// колбэки SDK приходят в непредсказуемом порядке: язык отвечает синхронно,
        /// а сохранение — через промис, то есть позже. Без этого поля загрузка
        /// сохранения затирала язык площадки, и игра всегда открывалась по-русски.
        /// </summary>
        static Language? _platform;

        public static Language Current
        {
            get
            {
                if (!_loaded)
                {
                    _current = (Language)PlayerPrefs.GetInt(Key, (int)Language.Ru);
                    _loaded = true;
                }
                return _current;
            }
        }

        /// <summary>Игрок хоть раз переключал язык кнопкой. На выбор языка не влияет.</summary>
        public static bool ChosenByPlayer => PlayerPrefs.HasKey(Key);

        /// <summary>
        /// Игрок переключил язык кнопкой. Действует сразу и до конца сессии; при
        /// следующем запуске язык площадки возьмёт своё — см. <see cref="Resolve"/>.
        /// Записываем всё равно: там, где площадки нет, это единственный источник.
        /// </summary>
        public static void Set(Language language)
        {
            _current = language;
            _loaded = true;
            PlayerPrefs.SetInt(Key, (int)language);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Какой язык должен действовать прямо сейчас.
        ///
        /// **Язык площадки сильнее всего остального.** Сообщила площадка язык — игра
        /// открывается на нём, и никакой прошлый выбор игрока это не перебивает.
        ///
        /// Раньше было иначе: выбор кнопкой побеждал, если сделан на той же языковой
        /// версии площадки. Звучало разумно, а на деле стоило нам двух отказов подряд
        /// по п. 2.14. Модератор открывает игру с ?lang=en, идёт в настройки и жмёт
        /// кнопку смены языка — именно затем, чтобы проверить, что она работает. Этим
        /// он сам себе записывает выбор «русский, сделан на английской версии», и с
        /// этого момента игра открывается по-русски при любом ?lang=en. Со стороны
        /// это неотличимо от неработающего автоопределения, и вердикт справедлив.
        ///
        /// Выбор игрока теперь действует до перезагрузки и как запасной вариант, когда
        /// площадки нет вовсе — в редакторе или в сборке без SDK.
        /// </summary>
        static Language Resolve()
        {
            if (_platform.HasValue) return _platform.Value;

            if (PlayerPrefs.HasKey(Key)) return (Language)PlayerPrefs.GetInt(Key);
            return _current;
        }

        public static void Toggle() => Set(Current == Language.Ru ? Language.En : Language.Ru);

        /// <summary>
        /// Перечитать язык после того, как облако принесло сохранение: игрок мог
        /// выбрать язык на другом устройстве.
        ///
        /// Если выбора нет, язык НЕ трогаем. Раньше здесь читалось значение с
        /// дефолтом «русский», и у нового игрока это откатывало язык площадки обратно
        /// на русский — сохранение приходит позже языка. Из-за этого игра открывалась
        /// по-русски даже при ?lang=en, и модерация справедливо вернула сборку.
        /// </summary>
        public static void Reload()
        {
            _loaded = true;
            Apply(Resolve());
        }

        /// <summary>Язык интерфейса от Yandex Games SDK: "ru", "en", "tr" и так далее.</summary>
        public static void ApplyPlatformLanguage(string code)
        {
            if (string.IsNullOrEmpty(code)) return;

            // Всё, что не русский, ведём на английский: он у нас второй язык списка.
            var language = code.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                ? Language.Ru : Language.En;

            // Запоминаем всегда, даже когда применить не можем: сохранение придёт
            // позже и позовёт Reload, которому нужно знать, куда возвращаться.
            _platform = language;

            // Видно в консоли браузера прямо на площадке. Без этой строки причину
            // «игра открылась не на том языке» приходится угадывать снаружи.
            Debug.Log("[Loc] язык площадки: " + code + " -> " + language
                + " (применяется всегда, выбор игрока его не перебивает)");

            _loaded = true;
            Apply(Resolve());
        }

        /// <summary>Сменить язык без записи в хранилище.</summary>
        static void Apply(Language language)
        {
            _loaded = true;
            if (language == _current) return;

            _current = language;
            Changed?.Invoke();
        }

        public static string Get(string key)
        {
            if (Table.TryGetValue(key, out var pair)) return pair[(int)Current];
            Debug.LogWarning("[Loc] нет строки: " + key);
            return key;
        }

        // ------------------------------------------------------------- таблица
        // [0] — русский, [1] — английский.

        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            // Название игры. Обязано совпадать с полем «Название» в консоли (п. 5.1.3):
            // русское — «Пузырёк: Подъём из глубин», английское — «Bubble Rise: Ascent from the Depths».
            { "game.title",        new[] { "ПУЗЫРЁК",          "BUBBLE RISE" } },
            { "game.subtitle",     new[] { "ПОДЪЁМ ИЗ ГЛУБИН", "ASCENT FROM THE DEPTHS" } },

            { "menu.play",         new[] { "ВСПЛЫВАТЬ", "RISE UP" } },
            { "menu.shop",         new[] { "МАГАЗИН",   "SHOP" } },
            { "menu.settings",     new[] { "НАСТРОЙКИ", "SETTINGS" } },
            { "menu.codex",        new[] { "СПРАВОЧНИК", "BESTIARY" } },
            { "menu.record",       new[] { "РЕКОРД: ",  "BEST: " } },
            { "menu.hint",         new[] {
                "Тяни СТРАЖА и прикрывай пузырёк.",
                "Drag the ORB to shield the bubble." } },

            { "hud.meters",        new[] { " м", " m" } },
            { "hud.metersZero",    new[] { "0 м", "0 m" } },
            { "hud.boss",          new[] { "БОСС", "BOSS" } },

            { "settings.title",    new[] { "НАСТРОЙКИ", "SETTINGS" } },
            { "settings.soundOn",  new[] { "ЗВУК: ВКЛ",    "SOUND: ON" } },
            { "settings.soundOff", new[] { "ЗВУК: ВЫКЛ",   "SOUND: OFF" } },
            { "settings.musicOn",  new[] { "МУЗЫКА: ВКЛ",  "MUSIC: ON" } },
            { "settings.musicOff", new[] { "МУЗЫКА: ВЫКЛ", "MUSIC: OFF" } },
            { "settings.language", new[] { "ЯЗЫК: РУССКИЙ", "LANGUAGE: ENGLISH" } },
            { "settings.note",     new[] {
                "Рекорд и выбранные скины сохраняются сами.",
                "Your best depth and chosen skins are saved automatically." } },

            { "pause.title",       new[] { "ПАУЗА",             "PAUSED" } },
            { "pause.resume",      new[] { "ПРОДОЛЖИТЬ",       "RESUME" } },
            { "pause.quit",        new[] { "ВЫЙТИ В МЕНЮ",     "QUIT TO MENU" } },
            { "common.back",       new[] { "НАЗАД", "BACK" } },

            { "shop.title",        new[] { "МАГАЗИН",  "SHOP" } },
            { "shop.tabBubble",    new[] { "ПУЗЫРЁК",  "BUBBLE" } },
            { "shop.tabGuardian",  new[] { "СТРАЖ",    "ORB" } },
            { "shop.selected",     new[] { "ВЫБРАНО",  "SELECTED" } },

            // --- магазин за монеты
            { "shop.buyFor",       new[] { "КУПИТЬ ЗА ",   "BUY FOR " } },
            { "shop.needCoins",    new[] { "НЕ ХВАТАЕТ ",  "NEED " } },
            { "menu.coins",        new[] { "Монеты: ",     "Coins: " } },

            // --- редкости и фильтры магазина
            { "rarity.common",     new[] { "ОБЫЧНЫЙ",    "COMMON" } },
            { "rarity.rare",       new[] { "РЕДКИЙ",     "RARE" } },
            { "rarity.epic",       new[] { "ЭПИЧЕСКИЙ",  "EPIC" } },
            { "rarity.legendary",  new[] { "ЛЕГЕНДАРНЫЙ","LEGENDARY" } },
            { "rarity.boss",       new[] { "БОСС",       "BOSS" } },
            { "shop.filterAll",    new[] { "ВСЕ",        "ALL" } },
            { "shop.sortDefault",  new[] { "ПО ПОРЯДКУ", "DEFAULT" } },
            { "shop.sortCheap",    new[] { "ЦЕНА ↑",     "PRICE UP" } },
            { "shop.sortRich",     new[] { "ЦЕНА ↓",     "PRICE DOWN" } },
            { "shop.ownedAll",     new[] { "ВСЕ",        "ALL" } },
            { "shop.ownedYes",     new[] { "КУПЛЕНО",    "OWNED" } },
            { "shop.ownedNo",      new[] { "НЕ КУПЛЕНО", "NOT OWNED" } },
            { "shop.owned",        new[] { "КУПЛЕНО",    "OWNED" } },
            { "shop.chosen",       new[] { "ВЫБРАНО",    "SELECTED" } },
            { "shop.select",       new[] { "ВЫБРАТЬ",    "SELECT" } },
            { "shop.locked",       new[] { "ЗАКРЫТО",    "LOCKED" } },
            { "shop.demo",         new[] { "ПОКАЗАТЬ ЭФФЕКТЫ", "SHOW EFFECTS" } },
            { "over.earned",       new[] { "МОНЕТ ЗА ЗАПЛЫВ: +", "COINS EARNED: +" } },

            // Имена покупаемых скинов. Ключ один на пару: пузырёк и страж — это одна
            // вещь, купленная один раз, и называться по-разному они не должны.
            { "skin.buy.coral",    new[] { "Коралл",      "Coral" } },
            { "skin.buy.pearl",    new[] { "Жемчуг",      "Pearl" } },
            { "skin.buy.silt",     new[] { "Тина",        "Silt" } },
            { "skin.buy.sand",     new[] { "Песок",       "Sand" } },
            { "skin.buy.tide",     new[] { "Прилив",      "Tide" } },
            { "skin.buy.frost",    new[] { "Иней",        "Frost" } },
            { "skin.buy.toxin",    new[] { "Яд",          "Toxin" } },
            { "skin.buy.neon",     new[] { "Неон",        "Neon" } },
            { "skin.buy.resin",    new[] { "Смола",       "Resin" } },
            { "skin.buy.storm",    new[] { "Гроза",       "Storm" } },
            { "skin.buy.ember",    new[] { "Уголь",       "Ember" } },
            { "skin.buy.volt",     new[] { "Разряд",      "Volt" } },
            { "skin.buy.wraith",   new[] { "Призрак",     "Wraith" } },
            { "skin.buy.prism",    new[] { "Призма",      "Prism" } },
            { "skin.buy.pixel",    new[] { "Пиксель",     "Pixel" } },
            { "skin.buy.nebula",   new[] { "Туманность",  "Nebula" } },
            { "skin.buy.magma",    new[] { "Магма",       "Magma" } },
            { "skin.buy.aurora",   new[] { "Сияние",      "Aurora" } },
            { "skin.buy.abyss",    new[] { "Бездна",      "Abyss" } },
            { "skin.buy.leviath",  new[] { "Левиафан",    "Leviathan" } },
            { "shop.unlockBoss",   new[] { "ПОБЕДИТЕ: ",        "DEFEAT: " } },
            { "shop.unlockAt",     new[] { "ОТКРОЕТСЯ НА ", "UNLOCKS AT " } },
            { "shop.unlockFish",   new[] { "ОТБЕЙТЕ РЫБ: ",     "FISH BLOCKED: " } },
            { "shop.unlockDebris", new[] { "ОТБЕЙТЕ ОБЛОМКОВ: ", "DEBRIS BLOCKED: " } },
            { "shop.unlockAll",    new[] { "ОТКРОЙТЕ ВСЕ ОСТАЛЬНЫЕ СКИНЫ", "UNLOCK EVERY OTHER SKIN" } },

            // --- сложность, вибрация, подсказки --------------------------------
            { "settings.difficulty", new[] { "СЛОЖНОСТЬ: ", "DIFFICULTY: " } },
            { "settings.calmOn",   new[] { "МЕНЬШЕ ВСПЫШЕК: ВКЛ",  "REDUCED FLASHING: ON" } },
            { "settings.calmOff",  new[] { "МЕНЬШЕ ВСПЫШЕК: ВЫКЛ", "REDUCED FLASHING: OFF" } },
            { "settings.fxQuiet",  new[] { "ЭФФЕКТЫ: ТИХО",   "EFFECTS: QUIET" } },
            { "settings.fxNormal", new[] { "ЭФФЕКТЫ: ОБЫЧНО", "EFFECTS: NORMAL" } },
            { "settings.fxBright", new[] { "ЭФФЕКТЫ: ЯРКО",   "EFFECTS: BRIGHT" } },
            { "settings.noFlashOn",  new[] { "БЕЗ ВСПЫШЕК: ВКЛ",  "NO FLASHES: ON" } },
            { "settings.noFlashOff", new[] { "БЕЗ ВСПЫШЕК: ВЫКЛ", "NO FLASHES: OFF" } },
            { "settings.calmHint", new[] {
                "Без вспышек: у босса контур, колесо стоит",
                "No flashes: boss outline, still wheel" } },
            { "settings.vibrationOn",  new[] { "ВИБРАЦИЯ: ВКЛ",  "VIBRATION: ON" } },
            { "settings.vibrationOff", new[] { "ВИБРАЦИЯ: ВЫКЛ", "VIBRATION: OFF" } },
            { "diff.easy",         new[] { "ЛЁГКАЯ",  "EASY" } },
            { "diff.normal",       new[] { "ОБЫЧНАЯ", "NORMAL" } },
            { "diff.hard",         new[] { "СЛОЖНАЯ", "HARD" } },
            { "diff.hint",         new[] {
                "На лёгкой препятствия реже и проходы шире. Награда за препятствие одна и та же.",
                "On easy there are fewer obstacles and wider gaps. The coin reward never changes." } },

            // Вступление: три экрана, пропускаются одним нажатием.
            { "tutor.skip",        new[] { "ПРОПУСТИТЬ", "SKIP" } },
            { "tutor.next",        new[] { "ДАЛЬШЕ",     "NEXT" } },
            { "tutor.play",        new[] { "ПОПЛЫЛИ!",   "LET'S GO!" } },
            { "tutor.title1",      new[] { "ВЕДИТЕ СТРАЖА", "DRAG THE ORB" } },
            { "tutor.body1",       new[] {
                "Держите палец на экране и ведите шарик-страж. Пузырёк плывёт сам — его нужно беречь.",
                "Hold your finger on the screen and drag the orb. The bubble rises on its own — keep it safe." } },
            { "tutor.title2",      new[] { "ПОДСТАВЛЯЙТЕ ПОД УДАР", "BLOCK WITH THE ORB" } },
            { "tutor.body2",       new[] {
                "Страж разбивает препятствия. Всё, до чего он не успел, бьёт по пузырьку.",
                "The orb smashes obstacles. Anything it misses hits the bubble instead." } },
            { "tutor.title3",      new[] { "СОБИРАЙТЕ МОНЕТЫ", "COLLECT COINS" } },
            { "tutor.body3",       new[] {
                "Монеты дают только разбитые препятствия. На них в магазине покупают новые скины.",
                "Coins come only from smashed obstacles. Spend them on new skins in the shop." } },

            // --- стихии: подписи характеристик в магазине ----------------------
            { "fx.impact",         new[] { "Удар: ",  "Impact: " } },
            { "fx.element",        new[] { "Стихия: ",     "Element: " } },
            { "fx.destroy",        new[] { "Разрушение: ", "Shatter: " } },
            { "fx.trail",          new[] { "След: ",  "Trail: " } },
            { "fx.sound",          new[] { "Звук: ",  "Sound: " } },
            { "fx.compare",        new[] { "СРАВНИТЬ", "COMPARE" } },
            { "fx.tryOn",          new[] { "ПРИМЕРИТЬ", "TRY IT" } },
            { "fx.close",          new[] { "ЗАКРЫТЬ",  "CLOSE" } },
            { "fx.bigHint",        new[] { "ЭФФЕКТ ПОЧТИ В НАТУРАЛЬНУЮ ВЕЛИЧИНУ",
                                           "EFFECT SHOWN NEARLY LIFE-SIZE" } },
            { "fx.current",        new[] { "СЕЙЧАС",   "CURRENT" } },
            { "fx.table",          new[] { "ЧЕМ ОТЛИЧАЕТСЯ", "WHAT MAKES IT DIFFERENT" } },
            { "fx.shape",          new[] { "Форма: ",  "Shape: " } },
            { "fx.vibro",          new[] { "Отклик: ", "Haptics: " } },

            { "fx.sound.crackle",  new[] { "треск",    "crackle" } },
            { "fx.sound.chime",    new[] { "звон",     "chime" } },
            { "fx.sound.zap",      new[] { "разряд",   "zap" } },
            { "fx.sound.hiss",     new[] { "шипение",  "hiss" } },
            { "fx.sound.ring",     new[] { "звон меди", "brass ring" } },
            { "fx.sound.thud",     new[] { "глухой удар", "thud" } },
            { "fx.sound.hum",      new[] { "гул",      "hum" } },
            { "fx.sound.splash",   new[] { "всплеск",  "splash" } },
            { "fx.sound.breath",   new[] { "выдох",    "breath" } },
            { "fx.sound.shimmer",  new[] { "перелив",  "shimmer" } },

            { "fx.trail.embers",   new[] { "угольки",  "embers" } },
            { "fx.trail.shards",   new[] { "осколки",  "shards" } },
            { "fx.trail.sparks",   new[] { "искры",    "sparks" } },
            { "fx.trail.droplets", new[] { "капли",    "droplets" } },
            { "fx.trail.dust",     new[] { "пыль",     "dust" } },
            { "fx.trail.veil",     new[] { "вуаль",    "veil" } },
            { "fx.trail.stardust", new[] { "звёздная пыль", "stardust" } },
            { "fx.trail.bubbles",  new[] { "пузырьки", "bubbles" } },
            { "fx.trail.rings",    new[] { "кольца",   "rings" } },
            { "fx.trail.motes",    new[] { "взвесь",   "motes" } },

            { "shape.sphere",      new[] { "шар",       "sphere" } },
            { "shape.drop",        new[] { "капля",     "drop" } },
            { "shape.faceted",     new[] { "огранка",   "faceted" } },
            { "shape.spiky",       new[] { "шипы",      "spikes" } },
            { "shape.ringed",      new[] { "кольцо",    "ringed" } },
            { "shape.wisp",        new[] { "дымка",     "wisp" } },
            { "shape.lumpy",       new[] { "бугры",     "lumpy" } },
            { "shape.star",        new[] { "звезда",    "star" } },
            { "shape.blocky",      new[] { "блок",      "block" } },

            // --- магазин: пояснения разделов и подсказки -----------------------
            { "shop.hintBubble",   new[] {
                "то, что вы защищаете",
                "the one you protect" } },
            { "shop.hintGuardian", new[] {
                "ваш защитник — он бьёт препятствия",
                "your defender — it smashes obstacles" } },
            { "shop.recommend",    new[] { "РЕКОМЕНДУЕМ", "PICKED FOR YOU" } },
            { "shop.short",        new[] { "НЕ ХВАТАЕТ ", "NEED " } },
            { "shop.filters",      new[] { "ФИЛЬТРЫ",   "FILTERS" } },
            { "shop.filtersOff",   new[] { "СБРОСИТЬ",  "RESET" } },

            // --- система возвращения -------------------------------------------
            { "hub.title",         new[] { "НАГРАДЫ",    "REWARDS" } },
            { "hub.button",        new[] { "НАГРАДЫ",    "REWARDS" } },
            { "hub.tabGifts",      new[] { "ПОДАРКИ",    "GIFTS" } },
            { "hub.tabQuests",     new[] { "ЗАДАНИЯ",    "QUESTS" } },
            { "hub.tabWheel",      new[] { "КОЛЕСО",     "WHEEL" } },
            { "hub.tabAch",        new[] { "ДОСТИЖЕНИЯ", "AWARDS" } },
            { "hub.nothing",       new[] { "ПОКА НИЧЕГО НЕ ГОТОВО", "NOTHING READY YET" } },

            { "chest.title",       new[] { "СУНДУК С ОТЛИВА", "TIDE CHEST" } },
            { "chest.hint",        new[] { "НОВЫЙ СУНДУК КАЖДЫЙ ЧАС, КОПИТСЯ ДО ТРЁХ",
                                           "A NEW CHEST EVERY HOUR, UP TO THREE" } },
            { "chest.open",        new[] { "ОТКРЫТЬ",    "OPEN" } },
            { "chest.ready",       new[] { "ГОТОВ",      "READY" } },
            { "chest.stored",      new[] { "НАКОПЛЕНО: ", "STORED: " } },
            { "chest.full",        new[] { "СУНДУКИ ПОЛНЫ", "CHESTS ARE FULL" } },
            { "chest.wait",        new[] { "СОЗРЕЕТ ЧЕРЕЗ ", "RIPENS IN " } },

            { "daily.title",       new[] { "ВХОД КАЖДЫЙ ДЕНЬ", "DAILY LOGIN" } },
            { "daily.claim",       new[] { "ЗАБРАТЬ",    "CLAIM" } },
            { "daily.claimed",     new[] { "ЗАБРАНО",    "CLAIMED" } },
            { "daily.next",        new[] { "СЛЕДУЮЩИЙ ДЕНЬ ЧЕРЕЗ ", "NEXT DAY IN " } },
            { "daily.streak",      new[] { "СЕРИЯ: ",    "STREAK: " } },
            { "daily.day",         new[] { "ДЕНЬ ",      "DAY " } },
            { "daily.rescue",      new[] { "СПАСАТЕЛЬНЫЙ ПУЗЫРЬ СОХРАНИЛ СЕРИЮ",
                                           "A RESCUE BUBBLE SAVED YOUR STREAK" } },

            { "quest.title",       new[] { "ЗАДАНИЯ",    "QUESTS" } },
            { "quest.daily",       new[] { "НА СЕГОДНЯ", "TODAY" } },
            { "quest.weekly",      new[] { "НА НЕДЕЛЮ",  "THIS WEEK" } },
            { "quest.claim",       new[] { "ЗАБРАТЬ",    "CLAIM" } },
            { "quest.claimed",     new[] { "ЗАБРАНО",    "CLAIMED" } },
            { "quest.reroll",      new[] { "ЗАМЕНИТЬ",   "REPLACE" } },
            { "quest.resetIn",     new[] { "ОБНОВЯТСЯ ЧЕРЕЗ ", "RESET IN " } },
            { "quest.sweep",       new[] { "ВСЕ ТРИ ЗАДАНИЯ", "ALL THREE QUESTS" } },
            { "quest.done",        new[] { "ВЫПОЛНЕНО!", "COMPLETE!" } },

            // Текст задания: ключ, затем «: цель». Числа подставляются кодом.
            { "quest.destroy",     new[] { "РАЗБИТЬ ПРЕПЯТСТВИЙ", "DESTROY OBSTACLES" } },
            { "quest.runs",        new[] { "СЫГРАТЬ ЗАПЛЫВОВ",    "PLAY RUNS" } },
            { "quest.fish",        new[] { "ОТБИТЬ РЫБ",          "BLOCK FISH" } },
            { "quest.distance",    new[] { "ПРОПЛЫТЬ МЕТРОВ",     "SWIM METRES" } },
            { "quest.coinsRun",    new[] { "МОНЕТ ЗА ОДИН ЗАПЛЫВ", "COINS IN ONE RUN" } },
            { "quest.depthRun",    new[] { "ГЛУБИНА ЗА ОДИН ЗАПЛЫВ", "DEPTH IN ONE RUN" } },
            { "quest.element",     new[] { "РАЗБИТЬ СТИХИЕЙ",     "DESTROY WITH" } },
            { "quest.bossMet",     new[] { "ВСТРЕТИТЬ БОССОВ",    "MEET BOSSES" } },
            { "quest.bossBeat",    new[] { "ПОБЕДИТЬ БОССОВ",     "DEFEAT BOSSES" } },

            // Названия стихий — для заданий «разбить стихией».
            { "status.burn",       new[] { "ПЛАМЕНЕМ",   "FLAME" } },
            { "status.freeze",     new[] { "ЛЬДОМ",      "ICE" } },
            { "status.shock",      new[] { "РАЗРЯДОМ",   "SHOCK" } },
            { "status.corrode",    new[] { "КИСЛОТОЙ",   "ACID" } },
            { "status.gild",       new[] { "ЗОЛОТОМ",    "GOLD" } },
            { "status.shadow",     new[] { "ТЬМОЙ",      "SHADOW" } },
            { "status.stardust",   new[] { "ЗВЁЗДНОЙ ПЫЛЬЮ", "STARDUST" } },
            { "status.crystal",    new[] { "КРИСТАЛЛОМ", "CRYSTAL" } },
            { "status.soak",       new[] { "ВОДОЙ",      "WATER" } },
            { "status.haze",       new[] { "ДЫМКОЙ",     "HAZE" } },
            { "status.spectrum",   new[] { "РАДУГОЙ",    "SPECTRUM" } },
            { "status.silt",       new[] { "ВЗВЕСЬЮ",    "SILT" } },
            { "status.petrify",    new[] { "КАМНЕМ",     "STONE" } },
            { "status.glitch",     new[] { "РАСПАДОМ",   "GLITCH" } },

            { "wheel.title",       new[] { "КОЛЕСО ПРИЛИВА", "TIDE WHEEL" } },
            { "wheel.spin",        new[] { "КРУТИТЬ",    "SPIN" } },
            { "wheel.left",        new[] { "ВРАЩЕНИЙ: ", "SPINS: " } },
            { "wheel.none",        new[] { "ВРАЩЕНИЯ КОНЧИЛИСЬ", "NO SPINS LEFT" } },
            { "wheel.spinning",    new[] { "КРУТИТСЯ…",   "SPINNING…" } },
            { "wheel.nextIn",      new[] { "СЛЕДУЮЩЕЕ ЧЕРЕЗ ", "NEXT IN " } },
            { "wheel.hint",        new[] { "ОДНО БЕСПЛАТНОЕ ВРАЩЕНИЕ КАЖДЫЙ ДЕНЬ",
                                           "ONE FREE SPIN EVERY DAY" } },

            { "ach.title",         new[] { "ДОСТИЖЕНИЯ", "AWARDS" } },
            { "ach.claim",         new[] { "ЗАБРАТЬ",     "CLAIM" } },
            { "ach.complete",      new[] { "ВСЁ СОБРАНО", "ALL DONE" } },
            { "ach.tier1",         new[] { "БРОНЗА",      "BRONZE" } },
            { "ach.tier2",         new[] { "СЕРЕБРО",     "SILVER" } },
            { "ach.tier3",         new[] { "ЗОЛОТО",      "GOLD" } },
            { "ach.destroy",       new[] { "ЧИСТИЛЬЩИК ГЛУБИН", "DEEP SWEEPER" } },
            { "ach.distance",      new[] { "ПУТЬ НАВЕРХ",       "THE LONG WAY UP" } },
            { "ach.runs",          new[] { "ПОСТОЯННЫЙ ГОСТЬ",  "REGULAR" } },
            { "ach.fish",          new[] { "СТРАЖ СТАИ",        "SHOAL WARDEN" } },
            { "ach.depth",         new[] { "ПОКОРИТЕЛЬ БЕЗДНЫ", "ABYSS CLIMBER" } },
            { "ach.boss",          new[] { "ОХОТНИК НА ТЕНИ",   "SHADOW HUNTER" } },
            { "ach.streak",        new[] { "ВЕРНЫЙ ПРИЛИВУ",    "TRUE TO THE TIDE" } },
            { "ach.coins",         new[] { "СБОРЩИК МОНЕТ",     "COIN COLLECTOR" } },

            { "back.title",        new[] { "С ВОЗВРАЩЕНИЕМ!", "WELCOME BACK!" } },
            { "back.days",         new[] { "ВАС НЕ БЫЛО ДНЕЙ: ", "DAYS AWAY: " } },
            { "back.sub",          new[] { "ПОКА ВАС НЕ БЫЛО, ОТЛИВ КОЕ-ЧТО ПРИНЁС",
                                           "THE TIDE BROUGHT SOMETHING WHILE YOU WERE GONE" } },
            { "back.ok",           new[] { "ЗАБРАТЬ ВСЁ", "COLLECT ALL" } },

            { "reward.got",        new[] { "ПОЛУЧЕНО",    "RECEIVED" } },
            { "reward.coins",      new[] { "МОНЕТ",        "COINS" } },
            { "reward.spin",       new[] { "ВРАЩЕНИЕ КОЛЕСА", "WHEEL SPIN" } },
            { "reward.chest",      new[] { "СУНДУК",       "CHEST" } },

            // Про перевод часов говорим мягко и без обвинений: чаще всего это не
            // обман, а смена часового пояса или починка часов на устройстве.
            { "time.rollback",     new[] { "ЧАСЫ УСТРОЙСТВА ПОЗАДИ. ТАЙМЕРЫ ПОЙДУТ, КОГДА ВРЕМЯ СОЙДЁТСЯ.",
                                           "THE DEVICE CLOCK IS BEHIND. TIMERS RESUME ONCE IT CATCHES UP." } },

            { "notif.chestTitle",  new[] { "Сундук созрел",  "A chest is ready" } },
            { "notif.chestBody",   new[] { "Отлив принёс монеты — заберите их.",
                                           "The tide brought coins — come and take them." } },
            { "notif.dailyTitle",  new[] { "Награда за вход ждёт", "Your daily reward is waiting" } },
            { "notif.dailyBody",   new[] { "Зайдите сегодня, чтобы не прервать серию.",
                                           "Drop in today to keep your streak alive." } },
            { "notif.questTitle",  new[] { "Новые задания",  "New quests" } },
            { "notif.questBody",   new[] { "Задания на день обновились.",
                                           "Today's quests have been refreshed." } },

            { "over.title",        new[] { "Пузырёк лопнул", "The bubble burst" } },
            { "over.sub",          new[] {
                "Обломки настигли пузырёк раньше поверхности.",
                "The debris caught the bubble before the surface." } },
            { "over.meters",       new[] { "метров", "metres" } },
            { "over.record",       new[] { "рекорд", "best" } },
            { "over.retry",        new[] { "Ещё раз", "Try again" } },
            // Формулировка намеренно называет вещи своими именами: по правилам
            // площадки согласие на rewarded-ролик должно быть явным.
            { "over.rewarded",     new[] { "Посмотреть рекламу: +1 жизнь", "Watch an ad: +1 life" } },
            { "over.menu",         new[] { "В МЕНЮ", "MENU" } },

            { "banner.pathOpen",   new[] { "ПУТЬ ОТКРЫТ!",   "THE WAY IS CLEAR!" } },
            { "banner.newSkin",    new[] { "НОВЫЙ СКИН!",     "NEW SKIN!" } },
            // Финал: показывается один раз за забег, после падения Великой тени.
            { "banner.finale",     new[] { "ВЕЛИКАЯ ТЕНЬ ПОВЕРЖЕНА!\nВСЕ 10 БОССОВ ПОБЕЖДЕНЫ\nСВОБОДНОЕ ПЛАВАНИЕ",
                                           "THE GREAT SHADOW HAS FALLEN!\nALL 10 BOSSES DEFEATED\nSWIM FREE" } },
            { "banner.extraLife",  new[] { "ЕЩЁ ОДИН ВДОХ!", "ONE MORE BREATH!" } },
            { "banner.skinUnlocked", new[] { "НОВЫЙ СКИН!", "NEW SKIN!" } },

            { "boss.jelly",        new[] { "ГРОЗОВАЯ МЕДУЗА!", "STORM JELLYFISH!" } },
            { "boss.serpent",      new[] { "ЗМЕЙ БЕЗДНЫ!",     "ABYSS SERPENT!" } },
            { "boss.octopus",      new[] { "СПРУТ ГЛУБИН!",    "DEEP KRAKEN!" } },
            { "boss.swordfish",    new[] { "КЛИНОК БЕЗДНЫ!",   "ABYSS BLADE!" } },
            { "boss.crab",         new[] { "КЛЕШНЕРУК!",       "PINCER BEAST!" } },
            { "boss.ink",          new[] { "ЧЕРНИЛЬНЫЙ МОРОК!", "INK SHROUD!" } },
            { "boss.piranhas",     new[] { "СТАЯ ПИРАНИЙ!",     "PIRANHA SWARM!" } },
            { "boss.turtles",      new[] { "КАМЕННЫЙ ПАНЦИРЬ!", "STONE SHELLS!" } },
            { "boss.shark",        new[] { "БЕЛАЯ СМЕРТЬ!",     "WHITE DEATH!" } },
            { "boss.manta",        new[] { "ВЕЛИКАЯ ТЕНЬ!",     "THE GREAT SHADOW!" } },
            { "boss.manta.sub",    new[] { "ОНА ПОМНИТ ВСЕХ",   "IT REMEMBERS THEM ALL" } },
            { "boss.manta.act2",   new[] { "ОНА ПОДХОДИТ БЛИЖЕ", "IT COMES CLOSER" } },
            { "boss.manta.act3",   new[] { "ОНА ПРЯМО НАД ТОБОЙ", "IT IS RIGHT ABOVE YOU" } },

            // ------------------------------------------------------- справочник
            // Статья открывается только после встречи с боссом, поэтому тексты пишутся
            // для того, кто уже видел бой и застрял, а не для того, кто выбирает,
            // на что посмотреть. Отсюда и тон: без интриги, сразу по делу.

            { "codex.title",       new[] { "СПРАВОЧНИК", "BESTIARY" } },
            { "codex.hint",        new[] {
                "Статья открывается, когда вы дошли до этого босса.",
                "An entry opens once you have reached that boss." } },
            { "codex.locked",      new[] { "???", "???" } },
            { "codex.what",        new[] { "КТО ЭТО",       "WHAT IT IS" } },
            { "codex.weak",        new[] { "СЛАБОЕ МЕСТО",  "WEAK SPOT" } },
            { "codex.how",         new[] { "КАК ПОБЕДИТЬ",  "HOW TO WIN" } },
            { "codex.depth",       new[] { "ГЛУБИНА ",      "DEPTH " } },

            { "codex.jelly.what",  new[] {
                "Висит над пузырьком и бьёт молниями. Сама вплотную не подходит.",
                "Hangs above the bubble and throws lightning. It never closes in." } },
            { "codex.jelly.weak",  new[] {
                "Только собственный разряд. Другого урона она не получает.",
                "Only its own bolt. Nothing else can hurt it." } },
            { "codex.jelly.how",   new[] {
                "Подставьте стража под молнию — она уйдёт обратно и ударит медузу. " +
                "Просто прикрыться мало, нужно именно отразить.",
                "Put the orb in the bolt's path and it flies back into the jellyfish. " +
                "Blocking is not enough — you must reflect." } },

            { "codex.serpent.what", new[] {
                "Плавает по всему экрану, потом стягивается в круг и бьёт рывком головы.",
                "Roams the whole screen, then coils in and strikes with its head." } },
            { "codex.serpent.weak", new[] {
                "Сам рывок: он идёт по прямой и свернуть уже не может.",
                "The lunge itself: it flies straight and cannot turn." } },
            { "codex.serpent.how",  new[] {
                "Перед ударом от головы к пузырьку загорается пунктирная линия. " +
                "Встаньте на неё стражем — змей получит урон и отлетит.",
                "A dashed line lights up from its head to the bubble before it strikes. " +
                "Meet that line with the orb and the serpent takes damage." } },

            { "codex.swordfish.what", new[] {
                "Ждёт за краем экрана и таранит носом-клинком насквозь.",
                "Waits offscreen and rams straight through with its blade." } },
            { "codex.swordfish.weak", new[] {
                "Разгон. На нём он летит по прямой и не поворачивает.",
                "The charge. Once launched it cannot steer." } },
            { "codex.swordfish.how",  new[] {
                "У кромки наливается его силуэт, а к пузырьку тянется цепочка стрелок. " +
                "Встретьте клинок стражем — он воткнётся и застрянет.",
                "Its silhouette brightens at the edge and arrows point at the bubble. " +
                "Meet the blade with the orb and it gets stuck." } },

            { "codex.turtle.what", new[] {
                "Восемь черепах встают кольцом и метают камни по очереди вдоль круга.",
                "Eight turtles form a ring and throw rocks in turn around it." } },
            { "codex.turtle.weak", new[] {
                "Их собственные камни. Трогать саму черепаху бесполезно.",
                "Their own rocks. Touching a turtle does nothing." } },
            { "codex.turtle.how",  new[] {
                "Отбейте камень — он уйдёт обратно и выбьет того, кто бросил. " +
                "В веере из трёх опасен только средний. Черепаху-волчок тоже можно отбить.",
                "Block a rock and it flies back and takes out the thrower. " +
                "In a fan of three only the middle one is real. The spinning charge can be blocked too." } },

            { "codex.piranha.what", new[] {
                "Десять рыб. Проходят строем через экран, потом бросаются поодиночке.",
                "Ten fish. They sweep across in formation, then dash one by one." } },
            { "codex.piranha.weak", new[] {
                "Каждая рыба — одно попадание. Полоса здоровья считает живых.",
                "Each fish is one hit. The health bar counts the living." } },
            { "codex.piranha.how",  new[] {
                "В строю опасна ровно одна — остриё клина, идущее точно на пузырёк. " +
                "Остальные проходят насквозь, ловить их бесполезно. В одиночных рывках " +
                "стрелки показывают, кто пошёл.",
                "Exactly one fish in the sweep matters: the tip of the wedge, aimed at the bubble. " +
                "The rest pass right through you. During solo dashes the arrows show who is coming." } },

            { "codex.ink.what", new[] {
                "Заливает экран чернилами и в темноте забрасывает стража в угол.",
                "Floods the screen with ink and throws the orb into a corner while you are blind." } },
            { "codex.ink.weak", new[] {
                "Обычный блок удара. Сложность не в защите, а в том, чтобы успеть.",
                "An ordinary block. The difficulty is not the block but the time." } },
            { "codex.ink.how",  new[] {
                "Пока темно, искать нечего — переждите. Как посветлело, хватайте стража " +
                "(он в одном из четырёх углов) и ведите на линию стрелок.",
                "While it is dark there is nothing to find — wait it out. As the picture returns, " +
                "grab the orb from one of the four corners and drag it onto the arrow line." } },

            { "codex.octopus.what", new[] {
                "Голова сверху, щупальца заходят из-за всех краёв экрана. Бьёт сериями.",
                "Its head hangs above, tentacles come from every edge. It strikes in series." } },
            { "codex.octopus.weak", new[] {
                "Кончик щупальца в момент удара.",
                "The tip of a tentacle at the moment it strikes." } },
            { "codex.octopus.how",  new[] {
                "Перед каждым ударом загорается луч. Закрыть всю серию нельзя — выбирайте. " +
                "После пропущенного удара пузырёк ненадолго неуязвим, и остаток серии уже не страшен.",
                "A beam lights up before every strike. You cannot cover a whole series — choose. " +
                "After a hit the bubble is briefly invulnerable, so the rest of the series is free." } },

            { "codex.crab.what", new[] {
                "Висит сверху и бьёт двумя клешнями по очереди.",
                "Hangs above and strikes with two claws in turn." } },
            { "codex.crab.weak", new[] {
                "Сжатая клешня — обычным блоком. Раскрытая — только золотой мишенью в пасти.",
                "A closed claw takes a normal block. An open one only takes a hit on the gold target inside." } },
            { "codex.crab.how",  new[] {
                "Пока он выше половины здоровья, просто подставляйте стража. Дальше пойдут " +
                "раскрытые клешни: закрыться в них — значит попасть в захват, бить нужно точно " +
                "в мишень по центру.",
                "Above half health just block as usual. Below it he opens the claw: blocking there " +
                "means getting grabbed — you have to hit the target in the middle." } },

            { "codex.shark.what", new[] {
                "Ходит рывками и чередует две атаки строго по очереди. Всё решает пасть.",
                "Moves in bursts and alternates two attacks strictly in turn. The mouth tells you which." } },
            { "codex.shark.weak", new[] {
                "Только таран сомкнутой пастью. За охоту урона не получить.",
                "Only the ram with a closed mouth. The hunt gives you nothing." } },
            { "codex.shark.how",  new[] {
                "Пасть сомкнута — встречайте стражем, это единственный источник урона. " +
                "Пасть раскрыта — не подставляйте, уводите: защиты от неё нет. " +
                "Продержитесь десять секунд, и она отстанет.",
                "Mouth closed: meet it with the orb, that is your only damage. " +
                "Mouth open: do not block, run — nothing stops it. " +
                "Survive ten seconds and it breaks off." } },

            { "codex.manta.what", new[] {
                "Финал. Висит в фоне и повторяет приёмы всех прошлых боссов. Три акта.",
                "The finale. It looms in the background and repeats every earlier boss. Three acts." } },
            { "codex.manta.weak", new[] {
                "Только её собственные разряды. Шипы не отражаются — их можно лишь принять на стража.",
                "Only its own bolts. Spines cannot be reflected, only taken on the orb." } },
            { "codex.manta.how",  new[] {
                "Акт 1 — отражайте разряды в тушу. Акт 2 — она всплывает, приёмы те же, но плотнее. " +
                "Акт 3 — разряд засчитывается, только если попал в раскрытую жаберную щель, " +
                "а хвост в это время целится в самого стража.",
                "Act 1: reflect its bolts into the body. Act 2: it surfaces — same attacks, denser. " +
                "Act 3: a bolt only counts if it hits an open gill, and the tail now aims at the orb itself." } },

            { "zone.canyon",       new[] { "КОРАЛЛОВЫЙ КАНЬОН",  "CORAL CANYON" } },
            { "zone.kelp",         new[] { "ЛАМИНАРИЕВЫЙ ЛЕС",   "KELP FOREST" } },
            { "zone.city",         new[] { "ЗАТОНУВШИЙ ГОРОД",   "SUNKEN CITY" } },
            { "zone.trench",       new[] { "СВЕТЯЩАЯСЯ ВПАДИНА", "GLOWING TRENCH" } },
            { "zone.shoal",        new[] { "ПЕСЧАНАЯ ОТМЕЛЬ",    "SANDY SHOAL" } },

            { "skin.bubble.0",     new[] { "Пена",     "Foam" } },
            { "skin.bubble.1",     new[] { "Малахит",  "Malachite" } },
            { "skin.bubble.2",     new[] { "Багрянец", "Crimson" } },
            { "skin.bubble.3",     new[] { "Охотник",  "Hunter" } },
            { "skin.bubble.4",     new[] { "Таран",    "Breaker" } },
            { "skin.bubble.star",  new[] { "ЗВЕЗДА ГЛУБИН", "STAR OF THE DEEP" } },

            { "skin.guardian.0",   new[] { "Пена",     "Foam" } },
            { "skin.guardian.1",   new[] { "Малахит",  "Malachite" } },
            { "skin.guardian.2",   new[] { "Багрянец", "Crimson" } },
            { "skin.guardian.3",   new[] { "Охотник",  "Hunter" } },
            { "skin.guardian.4",   new[] { "Таран",    "Breaker" } },
            { "skin.guardian.star", new[] { "ЗВЕЗДА ГЛУБИН", "STAR OF THE DEEP" } },
            { "skin.bubble.jelly",       new[] { "МЕДУЗКА",           "JELLYLING" } },
            { "skin.bubble.serpent",     new[] { "ЗМЕЙКА",            "SERPENTLING" } },
            { "skin.bubble.octopus",     new[] { "СПРУТИК",           "KRAKENLING" } },
            { "skin.bubble.swordfish",   new[] { "КЛИНОК",            "BLADELING" } },
            { "skin.bubble.crab",        new[] { "КРАБИК",            "CRABLING" } },
            { "skin.bubble.ink",         new[] { "КАРАКАТИЦА",        "CUTTLELING" } },
            { "skin.bubble.piranha",     new[] { "ПИРАНЬЯ",           "PIRANHA" } },
            { "skin.bubble.turtle",      new[] { "ЧЕРЕПАШКА",         "TURTLING" } },
            { "skin.bubble.shark",       new[] { "АКУЛЁНОК",          "SHARKLING" } },
            { "skin.bubble.manta",       new[] { "СКАТИК",            "RAYLING" } },
            { "skin.guardian.jelly",     new[] { "ШАРОВАЯ МОЛНИЯ",    "BALL LIGHTNING" } },
            { "skin.guardian.serpent",   new[] { "КЛЫК ЗМЕЯ",         "SERPENT FANG" } },
            { "skin.guardian.octopus",   new[] { "ПРИСОСКА",          "SUCKER" } },
            { "skin.guardian.swordfish",  new[] { "ОСТРИЁ",            "BLADE TIP" } },
            { "skin.guardian.crab",      new[] { "КЛЕШНЯ",            "PINCER" } },
            { "skin.guardian.ink",       new[] { "ЧЕРНИЛА",           "INK BLOB" } },
            { "skin.guardian.piranha",   new[] { "ЧЕЛЮСТЬ",           "JAWS" } },
            { "skin.guardian.turtle",    new[] { "ВАЛУН",             "BOULDER" } },
            { "skin.guardian.shark",     new[] { "ПЛАВНИК",           "DORSAL FIN" } },
            { "skin.guardian.manta",     new[] { "ЖАЛО",              "STINGER" } },
        };
    }
}
