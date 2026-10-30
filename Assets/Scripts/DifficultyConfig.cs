using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Три режима. Выбирается игроком в настройках.</summary>
    public enum DifficultyLevel { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>
    /// Один набор чисел сложности. Правится в инспекторе, как и весь остальной
    /// баланс игры.
    /// </summary>
    [Serializable]
    public class DifficultyPreset
    {
        [Header("Мягкий старт")]
        [Tooltip("Первые сколько метров идут только простые препятствия и вдвое реже.")]
        public float warmupMeters = 150f;

        [Tooltip("Во сколько раз реже появляются препятствия в начале заплыва.")]
        public float warmupSpawnFactor = 2.0f;

        [Tooltip("Насколько реже обычного в начале летят кораллы, шпили и локации.")]
        public float warmupFeatureFactor = 2.2f;

        [Header("Ввод новых видов")]
        [Tooltip("Множитель глубины открытия видов из HazardTable. Больше — позже.")]
        public float unlockDepthScale = 1.6f;

        [Tooltip("Минимум метров между появлением двух новых видов подряд.")]
        public float newTypeEveryMeters = 200f;

        [Tooltip("Дополнительный сдвиг для опасных видов: мины, воронки, створки, ловушки, якоря.")]
        public float dangerousExtraDepth = 800f;

        [Header("Кривая сложности")]
        [Tooltip("Множитель разгона прокрутки. Меньше — пологее.")]
        public float accelerationScale = 0.62f;

        [Tooltip("Множитель потолка скорости.")]
        public float speedCapScale = 0.84f;

        [Tooltip("Множитель интервала между препятствиями. Больше — реже.")]
        public float spawnIntervalScale = 1.3f;

        [Tooltip("Множитель скорости, с которой интервал сокращается. Меньше — пологее.")]
        public float spawnDecayScale = 0.6f;

        [Header("Проходы")]
        [Tooltip("Минимальная ширина чистого прохода в диаметрах пузырька.")]
        public float minGapInBubbles = 1.8f;

        [Tooltip("Сколько блоков постройки, шпиля или локации может стоять над пузырьком. 0 — взять из GameConfig.")]
        public int maxCenterBlocks = 1;

        [Header("Прощение")]
        [Tooltip("Во сколько раз хитбокс пузырька меньше рисунка.")]
        [Range(0.7f, 1f)] public float bubbleHitboxScale = 0.86f;

        [Tooltip("Во сколько раз хитбокс препятствия меньше рисунка.")]
        [Range(0.7f, 1f)] public float hazardHitboxScale = 0.88f;

        // Поднят после второго уменьшения пары: без этого перехват падает сильнее,
        // чем облегчается уворот, и игра становится не легче, а сложнее.
        [Tooltip("Во сколько раз страж достаёт дальше своего рисунка. 1 — ровно по кругу.")]
        [Range(1f, 1.35f)] public float guardianReachScale = 1.22f;

        [Tooltip("Окно прощения на скользящее касание, секунд.")]
        public float coyoteTime = 0.1f;

        [Tooltip("Неуязвимость после удара, секунд.")]
        public float invulnerabilityTime = 1.8f;

        [Tooltip("Каждые столько метров возвращается одна жизнь. 0 — не возвращать.")]
        public float lifeEveryMeters = 300f;

        [Header("Управление")]
        [Tooltip("Насколько резво страж идёт за пальцем. Больше — отзывчивее.")]
        public float guardianFollowLerp = 38f;

        [Header("Подстройка под игрока")]
        [Tooltip("После скольких коротких проигрышей подряд включается помощь.")]
        public int adaptiveAfterLosses = 3;

        [Tooltip("Короткий проигрыш — это сколько метров.")]
        public float adaptiveShortRun = 100f;

        [Tooltip("Насколько легче становится следующий заплыв. 0.18 = на 18 %.")]
        [Range(0f, 0.35f)] public float adaptiveRelief = 0.18f;
    }

    /// <summary>
    /// Числа сложности для трёх режимов.
    ///
    /// **Почему MonoBehaviour, а не ScriptableObject.** Просили ScriptableObject,
    /// чтобы править числа без кода. MonoBehaviour на том же объекте, где уже висит
    /// GameConfig, даёт ровно это: три раскрывающихся блока в инспекторе, ползунки,
    /// подсказки. Разница только в том, что не нужен файл .asset — а в этом проекте
    /// нет ни одного ассета вообще: ни префабов, ни материалов, ни спрайтов, всё
    /// рисуется и собирается кодом. Заводить ради одной настройки отдельный тип
    /// ассета и пункт меню для его создания значило бы завести вторую систему
    /// хранения настроек рядом с уже работающей.
    ///
    /// **Почему три пресета, а не множители поверх одного.** Множитель «сложнее на
    /// 20 %» звучит удобно, но на деле режимы отличаются не равномерно: на лёгком
    /// важнее длинный мягкий старт и возврат жизней, на сложном — ранние опасные
    /// виды. Это разные наборы чисел, и прятать их за одним ползунком значит
    /// лишить себя возможности настроить каждый.
    /// </summary>
    public class DifficultyConfig : MonoBehaviour
    {
        [Header("Лёгкая — по умолчанию для новых игроков")]
        public DifficultyPreset easy = new DifficultyPreset();

        [Header("Обычная")]
        public DifficultyPreset normal = new DifficultyPreset
        {
            warmupMeters = 100f,
            warmupSpawnFactor = 1.6f,
            warmupFeatureFactor = 1.7f,
            unlockDepthScale = 1.25f,
            newTypeEveryMeters = 150f,
            dangerousExtraDepth = 400f,
            accelerationScale = 0.8f,
            speedCapScale = 0.93f,
            spawnIntervalScale = 1.15f,
            spawnDecayScale = 0.8f,
            minGapInBubbles = 1.5f,
            maxCenterBlocks = 2,
            bubbleHitboxScale = 0.945f,
            hazardHitboxScale = 0.9f,
            guardianReachScale = 1.18f,
            coyoteTime = 0.1f,
            invulnerabilityTime = 1.5f,
            lifeEveryMeters = 450f,
            guardianFollowLerp = 36f,
            adaptiveAfterLosses = 3,
            adaptiveShortRun = 100f,
            adaptiveRelief = 0.15f,
        };

        [Header("Сложная — как игра была раньше")]
        public DifficultyPreset hard = new DifficultyPreset
        {
            warmupMeters = 0f,
            warmupSpawnFactor = 1f,
            warmupFeatureFactor = 1f,
            unlockDepthScale = 1f,
            newTypeEveryMeters = 0f,
            dangerousExtraDepth = 0f,
            accelerationScale = 1f,
            speedCapScale = 1f,
            spawnIntervalScale = 1f,
            spawnDecayScale = 1f,
            minGapInBubbles = 1.2f,
            maxCenterBlocks = 0,          // 0 = как в GameConfig
            bubbleHitboxScale = 1f,
            hazardHitboxScale = 1f,
            guardianReachScale = 1.08f,
            coyoteTime = 0f,
            invulnerabilityTime = 1.2f,
            lifeEveryMeters = 0f,
            guardianFollowLerp = 30f,
            adaptiveAfterLosses = 0,      // 0 = помощи нет
            adaptiveShortRun = 0f,
            adaptiveRelief = 0f,
        };

        public DifficultyPreset Of(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Hard: return hard;
                case DifficultyLevel.Normal: return normal;
                default: return easy;
            }
        }
    }

    /// <summary>
    /// Текущая сложность — одной точкой на всю игру.
    ///
    /// Статический класс, как Wallet и SkinStore рядом: числа нужны спавнерам,
    /// столкновениям, стражу и магазину, и передавать пресет через полдюжины Init
    /// значило бы протянуть его там, где он больше ничего не делает.
    ///
    /// Выбор игрока живёт в PlayerPrefs и уезжает в облако вместе с остальным
    /// профилем: сложность — это настройка, а настройки площадка требует хранить
    /// между сессиями наравне с прогрессом.
    /// </summary>
    public static class Difficulty
    {
        /// <summary>Сработал ли подбор сложности после серии коротких проигрышей.</summary>
        public static bool ReliefActive { get; private set; }

        static DifficultyConfig _source;
        static DifficultyPreset _preset = new DifficultyPreset();
        static DifficultyLevel _level = DifficultyLevel.Easy;
        static bool _loaded;

        static int _shortLosses;

        public static DifficultyPreset Current => _preset;

        public static DifficultyLevel Level
        {
            get { EnsureLoaded(); return _level; }
        }

        /// <summary>Привязать таблицу и прочитать выбор игрока. Зовётся на старте.</summary>
        public static void Bind(DifficultyConfig source)
        {
            _source = source;
            _loaded = false;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            // По умолчанию новичок получает лёгкую, а тот, кто уже играл, — обычную.
            // Переключить сложность у игры, в которую человек уже втянулся, значит
            // обесценить его рекорд; а встречать новичка прежней сложностью значит
            // не решить задачу вовсе.
            int fallback = PlayerPrefs.GetInt(SaveData.BestKey, 0) > 0
                ? (int)DifficultyLevel.Normal
                : (int)DifficultyLevel.Easy;

            _level = (DifficultyLevel)Mathf.Clamp(
                PlayerPrefs.GetInt(SaveData.DifficultyKey, fallback), 0, 2);

            Apply();
        }

        public static void Set(DifficultyLevel level)
        {
            EnsureLoaded();
            _level = level;
            PlayerPrefs.SetInt(SaveData.DifficultyKey, (int)level);
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Переключить по кругу: лёгкая → обычная → сложная → лёгкая.</summary>
        public static void Cycle() => Set((DifficultyLevel)(((int)Level + 1) % 3));

        static void Apply()
        {
            _preset = _source != null ? _source.Of(_level) : new DifficultyPreset();
        }

        /// <summary>Название режима для настроек.</summary>
        public static string LevelKey(DifficultyLevel level)
        {
            switch (level)
            {
                case DifficultyLevel.Hard: return "diff.hard";
                case DifficultyLevel.Normal: return "diff.normal";
                default: return "diff.easy";
            }
        }

        // ------------------------------------------------- подстройка под игрока

        /// <summary>
        /// Заплыв кончился. Если игрок трижды подряд погиб, не пройдя и сотни
        /// метров, следующий заплыв пойдёт мягче.
        ///
        /// Игроку об этом не сообщается — сообщение превратило бы помощь в диагноз.
        /// Но и обманом награды это не становится: монеты начисляются как обычно,
        /// меняется только плотность и ширина проходов.
        /// </summary>
        public static void ReportRunEnd(int depth)
        {
            var p = Current;
            if (p.adaptiveAfterLosses <= 0) { ReliefActive = false; return; }

            if (depth < p.adaptiveShortRun) _shortLosses++;
            else _shortLosses = 0;

            ReliefActive = _shortLosses >= p.adaptiveAfterLosses;
        }

        /// <summary>Игрок уплыл дальше порога — помощь больше не нужна.</summary>
        public static void ClearRelief()
        {
            _shortLosses = 0;
            ReliefActive = false;
        }

        /// <summary>Множитель «реже и шире», с учётом включённой помощи.</summary>
        public static float Relief => ReliefActive ? 1f + Current.adaptiveRelief : 1f;

        // ------------------------------------------------------- готовые числа

        /// <summary>Интервал между препятствиями с учётом мягкого старта и помощи.</summary>
        public static float SpawnIntervalFactor(float depth)
        {
            var p = Current;
            float factor = p.spawnIntervalScale * Relief;

            if (p.warmupMeters > 0f && depth < p.warmupMeters)
            {
                // Плавный выход из мягкого старта: ступенька на сто пятидесятом
                // метре читалась бы как «игра вдруг включилась».
                float t = Mathf.Clamp01(depth / p.warmupMeters);
                factor *= Mathf.Lerp(p.warmupSpawnFactor, 1f, t * t);
            }
            return factor;
        }

        /// <summary>То же для кораллов, шпилей и локаций.</summary>
        public static float FeatureIntervalFactor(float depth)
        {
            var p = Current;
            float factor = Relief;

            if (p.warmupMeters > 0f && depth < p.warmupMeters)
            {
                float t = Mathf.Clamp01(depth / p.warmupMeters);
                factor *= Mathf.Lerp(p.warmupFeatureFactor, 1f, t * t);
            }
            return factor;
        }

        /// <summary>Сколько блоков может стоять над пузырьком.</summary>
        public static int MaxCenterBlocks(int fromConfig)
        {
            int limit = Current.maxCenterBlocks;
            return limit > 0 ? Mathf.Min(limit, fromConfig) : fromConfig;
        }

        /// <summary>Минимальная ширина чистого прохода в юнитах мира.</summary>
        public static float MinGap(float bubbleRadius)
            => bubbleRadius * 2f * Current.minGapInBubbles * Relief;
    }
}
