using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Вид препятствия. Первые три были в игре с самого начала и остались как есть:
    /// ломаются с одного удара, ведут себя одинаково, отличаются только рисунком.
    /// Остальные добавлены позже и у каждого своя механика.
    ///
    /// Порядок менять нельзя: по индексу открываются виды по мере погружения
    /// (<see cref="HazardTable.UnlockDepth"/>), и перестановка сбила бы прогрессию.
    /// </summary>
    public enum ObstacleType
    {
        Rock = 0,
        Urchin,
        Coral,

        /// <summary>Коралловая вертушка: крутится и сносит стража в сторону вращения.</summary>
        Spinner,
        /// <summary>Шипастый нарост: медленный, но прочный.</summary>
        Barnacle,
        /// <summary>Раскалённый камень из источника: падает быстро, ломается с одного удара.</summary>
        Cinder,
        /// <summary>Якорь на цепи: ходит маятником поперёк экрана.</summary>
        Anchor,
        /// <summary>Глубинная мина: при уничтожении взрывается и забирает соседей.</summary>
        Mine,
        /// <summary>Биолюминесцентная ловушка: светится рывками, опасна только зажжённой.</summary>
        Glowtrap,
        /// <summary>Водоворот: тянет пузырёк к себе, пока не разбит.</summary>
        Vortex,
        /// <summary>Створки раковины: смыкаются и размыкаются, самое прочное из всего.</summary>
        Jaw,
    }

    /// <summary>
    /// Как препятствие себя ведёт. Отделено от вида, потому что поведений меньше,
    /// чем видов: камень, ёж и коралл отличаются рисунком, а не повадкой.
    /// </summary>
    public enum HazardMotion
    {
        /// <summary>Висит и покачивается — поведение трёх исходных препятствий.</summary>
        Drift,
        /// <summary>Крутится вокруг себя заметно быстрее обычного.</summary>
        Spin,
        /// <summary>Падает быстрее прокрутки.</summary>
        Fast,
        /// <summary>Широкий размах поперёк экрана, как маятник.</summary>
        Swing,
        /// <summary>Мигает: опасно только в зажжённой фазе.</summary>
        Pulse,
        /// <summary>Притягивает пузырёк к себе.</summary>
        Pull,
        /// <summary>Периодически смыкается, меняя свой радиус.</summary>
        Clamp,
    }

    /// <summary>Что происходит в момент разрушения — от этого зависит эффект и звук.</summary>
    public enum HazardDeath
    {
        /// <summary>Осколки в стороны.</summary>
        Shards,
        /// <summary>Рассыпается крошкой.</summary>
        Crumble,
        /// <summary>Взрыв с ударной волной, задевает соседние препятствия.</summary>
        Blast,
        /// <summary>Гаснет и растворяется.</summary>
        Dissolve,
        /// <summary>Схлопывается внутрь себя.</summary>
        Implode,
    }

    /// <summary>Паспорт одного вида препятствия: всё, что о нём нужно знать игре.</summary>
    public struct HazardStats
    {
        public ObstacleType Type;
        public HazardMotion Motion;
        public HazardDeath Death;

        /// <summary>Сколько ударов стража держит. Единица — ломается сразу, как раньше.</summary>
        public int Hp;
        /// <summary>
        /// Монет за уничтожение — вилка, а не число. Одинаковая награда за каждый
        /// камень читается как счётчик, а не как добыча; разброс делает каждое
        /// разрушение чуть-чуть разным. Платим по-прежнему за труд: прочнее и
        /// опаснее — выше обе границы.
        /// </summary>
        public int RewardMin;
        public int RewardMax;

        /// <summary>Сколько выпадет в этот раз. Границы включительно.</summary>
        public int RollReward() => Random.Range(RewardMin, RewardMax + 1);
        /// <summary>Множитель радиуса поверх общего разброса из конфигурации.</summary>
        public float Scale;
        /// <summary>Множитель скорости падения.</summary>
        public float Speed;
        /// <summary>Глубина в метрах, с которой вид начинает попадаться.</summary>
        public int UnlockDepth;
        /// <summary>
        /// Опасный вид: мина взрывает соседей, воронка тянет, створка требует трёх
        /// ударов, ловушка и якорь ходят непредсказуемо. Новичку такие достаются
        /// позже остальных — на лёгкой сложности с заметным запасом.
        /// </summary>
        public bool Dangerous;
        /// <summary>Относительная частота среди уже открытых видов.</summary>
        public float Weight;
        public Color Tint;
    }

    /// <summary>
    /// Каталог препятствий.
    ///
    /// Награда посчитана, а не выдумана. Модель заплыва по этим числам и числам из
    /// GameConfig даёт такую картину: до первого босса (1000 м) игрок плывёт около
    /// 80 секунд, успевает разбить порядка тридцати препятствий и набирает вместе с
    /// кораллами примерно 145 монет. Заплыв до 4000 м приносит около 425.
    ///
    /// Отсюда цены в магазине: первый скин 360 — это два-три заплыва, как и задумано;
    /// самый дорогой легендарный 9400 — около шестидесяти пяти заплывов; трофеи с
    /// боссов от 10000 и выше, и за ними стоит ещё и победа над самим боссом.
    /// Менять награды здесь, не пересчитав цены в SkinStore, нельзя: экономика
    /// держится ровно на этой пропорции.
    ///
    /// Прочность и награда идут вместе. Препятствие, которое надо ударить трижды,
    /// занимает стража на три перезарядки, а значит стоит игроку трёх пропущенных
    /// других угроз — за это и платим втрое.
    /// </summary>
    public static class HazardTable
    {
        public static readonly HazardStats[] All =
        {
            new HazardStats { Type = ObstacleType.Rock,     Motion = HazardMotion.Drift, Death = HazardDeath.Shards,
                              Hp = 1, RewardMin = 1,  RewardMax = 3,  Scale = 1f,    Speed = 1f,    UnlockDepth = 0,    Weight = 1.00f,
                              Tint = Color.white },

            new HazardStats { Type = ObstacleType.Urchin,   Motion = HazardMotion.Drift, Death = HazardDeath.Shards,
                              Hp = 1, RewardMin = 1,  RewardMax = 4,  Scale = 1f,    Speed = 1f,    UnlockDepth = 0,    Weight = 1.00f,
                              Tint = Color.white },

            new HazardStats { Type = ObstacleType.Coral,    Motion = HazardMotion.Drift, Death = HazardDeath.Crumble,
                              Hp = 1, RewardMin = 2,  RewardMax = 5,  Scale = 1f,    Speed = 1f,    UnlockDepth = 0,    Weight = 1.00f,
                              Tint = Color.white },

            // Дальше — новые виды. Открываются не сразу: на первых метрах игрок
            // учится подставлять стража, и подсовывать ему прочное в этот момент
            // значит учить его тому, что страж не работает.
            new HazardStats { Type = ObstacleType.Spinner,  Motion = HazardMotion.Spin,  Death = HazardDeath.Shards,
                              Hp = 2, RewardMin = 3,  RewardMax = 6,  Scale = 1.15f, Speed = 0.95f, UnlockDepth = 400,  Weight = 0.95f,
                              Tint = Palette.Hex("8FE3C8") },

            new HazardStats { Type = ObstacleType.Barnacle, Motion = HazardMotion.Drift, Death = HazardDeath.Crumble,
                              Hp = 2, RewardMin = 3,  RewardMax = 6,  Scale = 1.1f,  Speed = 0.85f, UnlockDepth = 700,  Weight = 0.95f,
                              Tint = Palette.Hex("C9B89A") },

            new HazardStats { Type = ObstacleType.Cinder,   Motion = HazardMotion.Fast,  Death = HazardDeath.Dissolve,
                              Hp = 1, RewardMin = 2,  RewardMax = 5,  Scale = 0.9f,  Speed = 1.55f, UnlockDepth = 1200, Weight = 0.90f,
                              Tint = Palette.Hex("FF8A4A") },

            new HazardStats { Type = ObstacleType.Anchor,   Motion = HazardMotion.Swing, Death = HazardDeath.Shards,
                              Hp = 2, RewardMin = 4,  RewardMax = 8,  Scale = 1.2f,  Speed = 0.9f,  UnlockDepth = 1800, Weight = 0.85f, Dangerous = true,
                              Tint = Palette.Hex("9AA7B4") },

            new HazardStats { Type = ObstacleType.Mine,     Motion = HazardMotion.Drift, Death = HazardDeath.Blast,
                              Hp = 1, RewardMin = 5,  RewardMax = 9,  Scale = 1.05f, Speed = 0.95f, UnlockDepth = 2500, Weight = 0.80f, Dangerous = true,
                              Tint = Palette.Hex("E05A4A") },

            new HazardStats { Type = ObstacleType.Glowtrap, Motion = HazardMotion.Pulse, Death = HazardDeath.Dissolve,
                              Hp = 2, RewardMin = 5,  RewardMax = 9,  Scale = 1.1f,  Speed = 0.9f,  UnlockDepth = 3200, Weight = 0.80f, Dangerous = true,
                              Tint = Palette.Hex("7BE8FF") },

            new HazardStats { Type = ObstacleType.Vortex,   Motion = HazardMotion.Pull,  Death = HazardDeath.Implode,
                              Hp = 3, RewardMin = 7,  RewardMax = 12,  Scale = 1.3f,  Speed = 0.8f,  UnlockDepth = 4200, Weight = 0.70f, Dangerous = true,
                              Tint = Palette.Hex("6E7BE8") },

            new HazardStats { Type = ObstacleType.Jaw,      Motion = HazardMotion.Clamp, Death = HazardDeath.Shards,
                              Hp = 3, RewardMin = 8,  RewardMax = 14, Scale = 1.25f, Speed = 0.85f, UnlockDepth = 5500, Weight = 0.65f, Dangerous = true,
                              Tint = Palette.Hex("D8C7A8") },
        };

        public static HazardStats Of(ObstacleType type) => All[(int)type];

        /// <summary>
        /// Выбрать вид для новой угрозы: только из открытых на этой глубине, с учётом
        /// весов. Прочные виды реже простых намеренно — иначе к пяти тысячам метров
        /// экран состоял бы из одних трёхударных, и стражу некогда было бы дышать.
        /// </summary>
        public static ObstacleType Roll(int depth)
        {
            float total = 0f;
            for (int i = 0; i < All.Length; i++)
                if (Unlocked(i, depth)) total += All[i].Weight;

            if (total <= 0f) return ObstacleType.Rock;

            float pick = Random.value * total;
            for (int i = 0; i < All.Length; i++)
            {
                if (!Unlocked(i, depth)) continue;
                pick -= All[i].Weight;
                if (pick <= 0f) return All[i].Type;
            }
            return ObstacleType.Rock;
        }

        /// <summary>
        /// Открыт ли вид на этой глубине с учётом выбранной сложности.
        ///
        /// Три правила поверх таблицы, и каждое отвечает за свою жалобу новичка:
        ///
        /// 1. **Мягкий старт.** Первые метры — только три исходных вида. Они
        ///    ломаются с одного удара и ведут себя одинаково; именно на них и
        ///    учатся подставлять стража.
        /// 2. **По одному новому.** Виды вводятся не чаще, чем раз в заданное
        ///    число метров. Без этого на лёгкой сложности, где все глубины
        ///    растянуты множителем, два-три вида открывались бы почти разом.
        /// 3. **Опасные позже.** Мина, воронка, створка, ловушка и якорь получают
        ///    отдельную отсрочку сверх общей.
        ///
        /// Порядок в таблице при этом не меняется: вид под номером N открывается
        /// после вида N−1 при любых настройках.
        /// </summary>
        public static bool Unlocked(int index, int depth)
        {
            if (index < 0 || index >= All.Length) return false;

            // Исходная тройка доступна всегда — иначе спавнить было бы нечего.
            if (All[index].UnlockDepth <= 0) return true;

            var p = Difficulty.Current;
            if (depth < p.warmupMeters) return false;

            float at = All[index].UnlockDepth * Mathf.Max(0.1f, p.unlockDepthScale);
            if (All[index].Dangerous) at += p.dangerousExtraDepth;

            // Разводим виды по глубине: N-й новый вид не раньше, чем через
            // newTypeEveryMeters после предыдущего. Номер считаем от первого вида
            // с ненулевой глубиной.
            int order = 0;
            for (int i = 0; i < index; i++) if (All[i].UnlockDepth > 0) order++;
            at = Mathf.Max(at, p.warmupMeters + order * p.newTypeEveryMeters);

            return depth >= at;
        }

        /// <summary>Сколько видов открыто на этой глубине — для проверок и дев-панели.</summary>
        public static int UnlockedCount(int depth)
        {
            int n = 0;
            for (int i = 0; i < All.Length; i++) if (Unlocked(i, depth)) n++;
            return n;
        }
    }
}
