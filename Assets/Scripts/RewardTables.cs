using UnityEngine;

namespace BubbleRise
{
    /// <summary>Что именно выдала награда — от этого зависит, как её показать.</summary>
    public enum RewardKind
    {
        Coins,
        /// <summary>Лишнее вращение колеса.</summary>
        Spin,
        /// <summary>Лишний сундук сверх накопленных.</summary>
        Chest,
    }

    /// <summary>Одна выдача: что и сколько.</summary>
    public struct RewardGrant
    {
        public RewardKind Kind;
        public int Amount;

        public static RewardGrant Coins(int amount) => new RewardGrant { Kind = RewardKind.Coins, Amount = amount };
        public static RewardGrant Spin(int amount = 1) => new RewardGrant { Kind = RewardKind.Spin, Amount = amount };
        public static RewardGrant Chest(int amount = 1) => new RewardGrant { Kind = RewardKind.Chest, Amount = amount };
    }

    /// <summary>Что игрок делает, чтобы двигать квест.</summary>
    public enum QuestGoal
    {
        /// <summary>Разбить любые препятствия.</summary>
        Destroy,
        /// <summary>Проплыть метров суммарно.</summary>
        Distance,
        /// <summary>Сыграть заплывов.</summary>
        Runs,
        /// <summary>Заработать монет за один заплыв.</summary>
        CoinsInRun,
        /// <summary>Отбить рыб.</summary>
        Fish,
        /// <summary>Дойти до глубины за один заплыв.</summary>
        DepthInRun,
        /// <summary>Встретить босса.</summary>
        BossMet,
        /// <summary>Победить босса.</summary>
        BossBeaten,
        /// <summary>Разбить препятствия определённой стихией (по статусу скина).</summary>
        DestroyWithStatus,
    }

    /// <summary>Шаблон квеста: из него генерируется конкретное задание на день.</summary>
    public struct QuestTemplate
    {
        public QuestGoal Goal;
        /// <summary>Ключ локализации описания; число подставляется в конец.</summary>
        public string NameKey;
        public int Min;
        public int Max;
        /// <summary>Шаг округления, чтобы цель не выглядела как 37.</summary>
        public int Step;
        public int Reward;
        /// <summary>Сложность: 0 лёгкий, 1 средний, 2 трудный. По одному каждого в день.</summary>
        public int Tier;
        /// <summary>Нужен ли игроку опыт боссов, чтобы задание вообще было выполнимо.</summary>
        public bool NeedsBoss;
    }

    /// <summary>Одно достижение с тремя ступенями.</summary>
    public struct AchievementDef
    {
        public string NameKey;
        public QuestGoal Goal;
        public int Bronze;
        public int Silver;
        public int Gold;
        public int RewardBronze;
        public int RewardSilver;
        public int RewardGold;
    }

    /// <summary>
    /// Все числа системы возвращения — в одном месте.
    ///
    /// **Расчёт экономики.** Заплыв до первого босса даёт около 145 монет (см.
    /// HazardTable), играют в среднем четыре заплыва за вечер — порядка 580 монет
    /// за день обычной игры. Задача была уложить награды в 20–35 % сверху, чтобы
    /// магазин по-прежнему зарабатывался игрой, а не ожиданием.
    ///
    /// Считать сундуки «три-четыре за вечер» оказалось неверно, и это выяснилось
    /// прогоном по дням (Tools/simulate_rewards.py). Сундук зреет час и копится до
    /// трёх, поэтому за день их забирают столько, сколько раз зашли: зашёл утром и
    /// вечером — шесть, а не три. Числа ниже пересчитаны под честное поведение.
    ///
    /// В среднем за день у игрока, заходящего дважды:
    ///
    /// * сундуки — 6 штук по 8 монет, то есть около 48;
    /// * вход по календарю — 30 (среднее по семи дням);
    /// * три дневных квеста — 48, плюс 12 за все три;
    /// * колесо — 8;
    /// * недельные квесты — около 30 в пересчёте на день.
    ///
    /// Итого ≈ 176 монет, это 30 % дневного заработка игрой — середина полосы.
    /// Кто заходит трижды в день, получает ≈ 200, то есть 34 %: всё ещё внутри.
    /// Тот, кто проверяет игру четыре и более раз, выйдет за 35 % — но для этого
    /// нужно заходить каждые три часа, и ограничивать такого игрока значило бы
    /// наказывать за то самое поведение, ради которого система и сделана.
    ///
    /// Достижения в эту долю не входят: они разовые и растянуты на месяцы.
    /// </summary>
    public static class RewardTables
    {
        // ------------------------------------------------------------ сундук

        /// <summary>Как часто созревает сундук.</summary>
        public const int ChestHours = 1;

        /// <summary>
        /// Сколько сундуков копится, пока игрока нет. Три — чтобы пропущенный день
        /// не обнулял всё, но и чтобы отсутствие неделю не превращалось в клад.
        /// </summary>
        public const int ChestMaxStored = 3;

        /// <summary>
        /// Содержимое сундука: вес и монеты. Среднее — ровно 8.
        ///
        /// Редкая крупная находка нужна не ради денег: восемьдесят монет из шести
        /// случаев в сотне — это то, ради чего сундук вообще открывают, а не просто
        /// забирают. Без хвоста таблица превращается в ежечасную выплату.
        /// </summary>
        public static readonly (float Weight, int Coins)[] ChestLoot =
        {
            (40f, 4), (30f, 6), (18f, 10), (8f, 16), (3f, 30), (1f, 60),
        };

        // -------------------------------------------------------- календарь

        /// <summary>
        /// Семь дней входа. Третий и седьмой заметно щедрее — на них и держится
        /// привычка возвращаться. Среднее по неделе — 40 монет в день.
        /// </summary>
        public static readonly RewardGrant[] DailyCalendar =
        {
            RewardGrant.Coins(15),
            RewardGrant.Coins(25),
            RewardGrant.Spin(),          // третий день — вращение колеса
            RewardGrant.Coins(30),
            RewardGrant.Coins(35),
            RewardGrant.Chest(),         // шестой — сундук сверх накопленных
            RewardGrant.Coins(90),       // седьмой — крупный приз
        };

        /// <summary>Сколько монет считать за «Spin» и «Chest» при расчёте среднего.</summary>
        public const int SpinWorth = 8;
        public const int ChestWorth = 8;

        /// <summary>
        /// Спасательный пузырь: один пропущенный день в неделю не рвёт серию.
        /// Без него серия рвётся у любого, кто однажды лёг спать пораньше, и
        /// возвращаться становится незачем.
        /// </summary>
        public const int RescueEveryDays = 7;

        // ----------------------------------------------------------- квесты

        /// <summary>
        /// Шаблоны дневных квестов. На день берётся по одному из каждой сложности,
        /// значения разыгрываются в пределах вилки — чтобы задания не повторялись
        /// изо дня в день слово в слово.
        /// </summary>
        public static readonly QuestTemplate[] DailyQuests =
        {
            // лёгкие
            new QuestTemplate { Goal = QuestGoal.Destroy,    NameKey = "quest.destroy",   Min = 20,  Max = 40,  Step = 5,   Reward = 12, Tier = 0 },
            new QuestTemplate { Goal = QuestGoal.Runs,       NameKey = "quest.runs",      Min = 2,   Max = 4,   Step = 1,   Reward = 12, Tier = 0 },
            new QuestTemplate { Goal = QuestGoal.Fish,       NameKey = "quest.fish",      Min = 10,  Max = 25,  Step = 5,   Reward = 12, Tier = 0 },

            // средние
            new QuestTemplate { Goal = QuestGoal.Distance,   NameKey = "quest.distance",  Min = 1500, Max = 3000, Step = 250, Reward = 16, Tier = 1 },
            new QuestTemplate { Goal = QuestGoal.CoinsInRun, NameKey = "quest.coinsRun",  Min = 80,  Max = 160, Step = 10,  Reward = 16, Tier = 1 },
            new QuestTemplate { Goal = QuestGoal.DepthInRun, NameKey = "quest.depthRun",  Min = 600, Max = 1200, Step = 100, Reward = 16, Tier = 1 },

            // трудные
            new QuestTemplate { Goal = QuestGoal.DestroyWithStatus, NameKey = "quest.element", Min = 25, Max = 50, Step = 5, Reward = 20, Tier = 2 },
            new QuestTemplate { Goal = QuestGoal.BossMet,    NameKey = "quest.bossMet",   Min = 1,   Max = 2,   Step = 1,   Reward = 20, Tier = 2, NeedsBoss = true },
            new QuestTemplate { Goal = QuestGoal.BossBeaten, NameKey = "quest.bossBeat",  Min = 1,   Max = 1,   Step = 1,   Reward = 20, Tier = 2, NeedsBoss = true },
            new QuestTemplate { Goal = QuestGoal.Destroy,    NameKey = "quest.destroy",   Min = 80,  Max = 140, Step = 10,  Reward = 20, Tier = 2 },
        };

        /// <summary>Недельные: те же цели, но крупнее и дороже.</summary>
        public static readonly QuestTemplate[] WeeklyQuests =
        {
            new QuestTemplate { Goal = QuestGoal.Destroy,  NameKey = "quest.destroy",  Min = 300,   Max = 600,   Step = 50,   Reward = 70, Tier = 0 },
            new QuestTemplate { Goal = QuestGoal.Distance, NameKey = "quest.distance", Min = 12000, Max = 20000, Step = 1000, Reward = 70, Tier = 1 },
            new QuestTemplate { Goal = QuestGoal.Runs,     NameKey = "quest.runs",     Min = 15,    Max = 25,    Step = 5,    Reward = 70, Tier = 2 },
        };

        /// <summary>Сколько даёт выполнение всех трёх дневных квестов.</summary>
        public const int DailySweepReward = 12;

        /// <summary>Одна бесплатная замена квеста в сутки.</summary>
        public const int QuestRerollsPerDay = 1;

        // ----------------------------------------------------------- колесо

        /// <summary>
        /// Сектора колеса: вес и монеты. Среднее — ровно 8, как у сундука.
        ///
        /// Таблица та же не по лени: колесо и сундук — два лица одной награды, и
        /// разная щедрость заставила бы игрока выбирать, что выгоднее открыть,
        /// вместо того чтобы просто забрать оба.
        /// </summary>
        public static readonly (float Weight, int Coins)[] WheelSectors =
        {
            (40f, 4), (30f, 6), (18f, 10), (8f, 16), (3f, 30), (1f, 60),
        };

        /// <summary>Бесплатное вращение раз в сутки.</summary>
        public const int FreeSpinsPerDay = 1;

        // ------------------------------------------------------ достижения

        /// <summary>
        /// Восемь достижений по три ступени. Цифры рассчитаны на месяцы игры:
        /// золото — то, до чего доходят единицы, и в дневную долю наград оно не
        /// входит вовсе.
        /// </summary>
        public static readonly AchievementDef[] Achievements =
        {
            new AchievementDef { NameKey = "ach.destroy",  Goal = QuestGoal.Destroy,
                                 Bronze = 250,   Silver = 2500,  Gold = 15000,
                                 RewardBronze = 40, RewardSilver = 150, RewardGold = 600 },

            new AchievementDef { NameKey = "ach.distance", Goal = QuestGoal.Distance,
                                 Bronze = 10000, Silver = 75000, Gold = 400000,
                                 RewardBronze = 40, RewardSilver = 150, RewardGold = 600 },

            new AchievementDef { NameKey = "ach.runs",     Goal = QuestGoal.Runs,
                                 Bronze = 25,    Silver = 150,   Gold = 600,
                                 RewardBronze = 30, RewardSilver = 120, RewardGold = 500 },

            new AchievementDef { NameKey = "ach.fish",     Goal = QuestGoal.Fish,
                                 Bronze = 150,   Silver = 1200,  Gold = 6000,
                                 RewardBronze = 30, RewardSilver = 120, RewardGold = 500 },

            new AchievementDef { NameKey = "ach.depth",    Goal = QuestGoal.DepthInRun,
                                 Bronze = 1000,  Silver = 4000,  Gold = 10000,
                                 RewardBronze = 50, RewardSilver = 200, RewardGold = 800 },

            new AchievementDef { NameKey = "ach.boss",     Goal = QuestGoal.BossBeaten,
                                 Bronze = 1,     Silver = 5,     Gold = 10,
                                 RewardBronze = 60, RewardSilver = 250, RewardGold = 900 },

            new AchievementDef { NameKey = "ach.streak",   Goal = QuestGoal.Runs,   // считается отдельно
                                 Bronze = 3,     Silver = 14,    Gold = 60,
                                 RewardBronze = 50, RewardSilver = 200, RewardGold = 800 },

            new AchievementDef { NameKey = "ach.coins",    Goal = QuestGoal.CoinsInRun,
                                 Bronze = 1000,  Silver = 10000, Gold = 60000,
                                 RewardBronze = 40, RewardSilver = 150, RewardGold = 600 },
        };

        /// <summary>Достижение «серия входов» считается по дням, а не по игре.</summary>
        public const int StreakAchievementIndex = 6;

        // ------------------------------------------------------ вспомогательное

        /// <summary>Разыграть по весам. Возвращает индекс.</summary>
        public static int Roll((float Weight, int Coins)[] table)
        {
            float total = 0f;
            for (int i = 0; i < table.Length; i++) total += table[i].Weight;

            float pick = Random.value * total;
            for (int i = 0; i < table.Length; i++)
            {
                pick -= table[i].Weight;
                if (pick <= 0f) return i;
            }
            return 0;
        }
    }
}
