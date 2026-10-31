using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Насколько злее боссы на текущем круге.
    ///
    /// Боссы идут строго по глубине: 1000 м — первый, 2000 м — второй и так далее до
    /// десятого на 10000 м. Дальше ряд начинается заново, но уже тяжелее: на 11000 м
    /// снова выходит медуза, только вдвое крепче и сразу разъярённая.
    ///
    /// Держать это в одном месте, а не в каждом боссе, пришлось по простой причине:
    /// условие «вдвое сложнее» иначе означало бы десять одинаковых правок в десяти
    /// файлах, и любой новый босс про него бы забыл. Здесь же круг выставляется один
    /// раз перед боем, и его подхватывают все.
    /// </summary>
    public static class BossTuning
    {
        /// <summary>Множитель запаса здоровья: первый круг 1, второй 2, третий 3.</summary>
        public static float HpScale { get; private set; } = 1f;

        /// <summary>
        /// Нижняя граница «ярости». Боссы ускоряются по мере потери HP, и на втором
        /// круге они стартуют уже разогретыми — иначе удвоенное здоровье делало бы бой
        /// просто длиннее, а не тяжелее.
        /// </summary>
        public static float Fury { get; private set; }

        /// <summary>Круг отсчитывается с нуля: 0 — боссы с 1000 до 10000 м.</summary>
        public static void SetLap(int lap)
        {
            lap = Mathf.Max(0, lap);
            HpScale = 1f + lap;
            Fury = Mathf.Clamp01(lap * 0.5f);
        }

        public static void Reset() => SetLap(0);

        /// <summary>Здоровье босса с поправкой на круг.</summary>
        public static int Hp(int baseHp) => Mathf.Max(1, Mathf.RoundToInt(baseHp * HpScale));

        /// <summary>
        /// Ярость: 0 при полном здоровье, 1 на последнем HP. Боссы пересчитывают через
        /// неё темп атак, поэтому нижняя граница круга поднимает его сразу.
        /// </summary>
        public static float Rage(int hp, int maxHp)
        {
            float natural = maxHp > 0 ? 1f - hp / (float)maxHp : 0f;
            return Mathf.Clamp01(Mathf.Max(natural, Fury));
        }
    }
}
