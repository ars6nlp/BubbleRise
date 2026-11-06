using UnityEngine;

namespace BubbleRise
{
    /// <summary>Что изредка отделяется от мини-босса — тихо, по одной частице.</summary>
    public enum BossAura { Arcs, Thorns, Suckers, ColdStreaks, Sand, InkPuffs, RedSparks, Shells, Bubbles, RedDots }

    /// <summary>Что тянется следом.</summary>
    public enum BossTrail { Threads, BoneDust, InkDrops, Streaks, ShellSparks, InkPlume, RedBubbles, SandShells, WaterRibbons, ShadowWaves }

    /// <summary>Удар стража — уменьшенная атака босса.</summary>
    public enum BossStrike { Arc, Coil, Grip, Slash, Pincer, Splash, Nibble, Crack, Jaws, Wave }

    /// <summary>Разрушение препятствия — то же, только целиком.</summary>
    public enum BossBreak { Storm, Spiral, Tentacles, Cross, Shards, InkCloud, TeethScatter, PlateRing, Whirl, Ripples }

    /// <summary>
    /// Паспорт боссового скина: тона босса для витрины и темы эффектов.
    ///
    /// Сам рисунок — мини-босс — живёт в <see cref="SpriteFactory.MiniBoss"/>, его
    /// живость — в <see cref="MiniBossRigs"/>. Здесь только то, что нужно вокруг
    /// рисунка: в каком тоне рамка карточки, нужна ли тёмному скину светлая
    /// обводка, чем пылит след и как выглядят удар и разрушение.
    ///
    /// Все цвета — реальные тона босса, приглушённые: без белого и без насыщенных
    /// «электрических» оттенков (проверяет BossSkinTests).
    /// </summary>
    public struct BossSkinDef
    {
        /// <summary>Главный тон босса, приглушённый: рамка и подложка карточки, подпись.</summary>
        public Color Tone;
        /// <summary>Самый тёмный тон босса.</summary>
        public Color Deep;
        /// <summary>
        /// Светлая обводка вокруг силуэта — только у тёмных скинов, которым иначе не
        /// хватает контраста с водой. Прозрачная — обводки нет.
        /// </summary>
        public Color Outline;

        public BossAura Aura;
        /// <summary>Частиц ауры в секунду у пузырька (у стража — половина).</summary>
        public float AuraRate;
        public BossTrail Trail;
        public BossStrike Strike;
        public BossBreak Break;
        /// <summary>Два цвета эффектов — из палитры босса, приглушённые.</summary>
        public Color FxA, FxB;
        /// <summary>Ключ подписи «Мини-версия …».</summary>
        public string SignatureKey;
    }

    public static class BossSkinTable
    {
        /// <summary>
        /// Обводка тёмных скинов: чуть светлее самой тёмной воды — не ярче 1.5:1 к ней,
        /// это отделение силуэта, а не кайма.
        /// </summary>
        public static readonly Color DarkOutline = Palette.Hex("2C3440", 0.85f);

        static BossSkinDef D(string tone, string deep, bool outline,
                             BossAura aura, float auraRate, BossTrail trail, BossStrike strike, BossBreak brk,
                             string fxA, string fxB, string signature)
            => new BossSkinDef
            {
                Tone = Palette.Hex(tone), Deep = Palette.Hex(deep),
                Outline = outline ? DarkOutline : Color.clear,
                Aura = aura, AuraRate = Mathf.Min(auraRate, 2f),
                Trail = trail, Strike = strike, Break = brk,
                FxA = Palette.SafeLight(Palette.Hex(fxA), 2f), FxB = Palette.SafeLight(Palette.Hex(fxB), 2f),
                SignatureKey = signature,
            };

        /// <summary>Порядок строго как в <see cref="BossKind"/>. Тона — из палитры самих боссов.</summary>
        public static readonly BossSkinDef[] All =
        {
            // Медуза: тёмный купол 463A72→120C26, кромка 8E7FC0, тусклая бирюза 3FD8C8 — только в дугах
            D("6A5A9C", "120C26", false, BossAura.Arcs, 1.2f,
              BossTrail.Threads, BossStrike.Arc, BossBreak.Storm, "2E8C86", "6A5A9C", "sig.jelly"),
            // Змей: тело 26404E, брюхо 4E7A88, костяные шипы 948A76
            D("4E7A88", "1A2E38", false, BossAura.Thorns, 1f,
              BossTrail.BoneDust, BossStrike.Coil, BossBreak.Spiral, "8E8676", "3E6A72", "sig.serpent"),
            // Спрут: 5B2E63 / 38193F, присоски E8A9C8 приглушённые
            D("7A4A84", "38193F", false, BossAura.Suckers, 1f,
              BossTrail.InkDrops, BossStrike.Grip, BossBreak.Tentacles, "9C6A88", "6A3A72", "sig.octopus"),
            // Рыба-меч: спина 172B3C, брюхо 8299AC, клинок — сталь
            D("6E8699", "172B3C", false, BossAura.ColdStreaks, 1f,
              BossTrail.Streaks, BossStrike.Slash, BossBreak.Cross, "7A8C9A", "4A6070", "sig.swordfish"),
            // Краб: панцирь B4432A приглушённый, суставы 5C1E12, песок
            D("A84A32", "5C1E12", false, BossAura.Sand, 1.2f,
              BossTrail.ShellSparks, BossStrike.Pincer, BossBreak.Shards, "A08C6A", "8C4A34", "sig.crab"),
            // Морок: мантия 8957CE→67429E приглушённая, чернила 0A0710
            D("7550AA", "24123C", false, BossAura.InkPuffs, 1f,
              BossTrail.InkPlume, BossStrike.Splash, BossBreak.InkCloud, "2A1E3C", "5E4A8A", "sig.ink"),
            // Пиранья: тело 7C8D96, спина 35434D, брюхо C4451C приглушённое
            D("8E5A48", "35434D", false, BossAura.RedSparks, 1f,
              BossTrail.RedBubbles, BossStrike.Nibble, BossBreak.TeethScatter, "8C3E30", "6E7C84", "sig.piranhas"),
            // Черепаха: панцирь 5E7F3E / 374E26, камень 7D8B93
            D("5E7F3E", "374E26", false, BossAura.Shells, 1f,
              BossTrail.SandShells, BossStrike.Crack, BossBreak.PlateRing, "8E8A6A", "5E7A44", "sig.turtles"),
            // Акула: спина 222E38, тело 4E6069, брюхо — светло-серое, не белое
            D("6A7C86", "222E38", true, BossAura.Bubbles, 1.2f,
              BossTrail.WaterRibbons, BossStrike.Jaws, BossBreak.Whirl, "6E8A9A", "8A969C", "sig.shark"),
            // Скат: почти чёрная спина 04070B, холодная кромка 22454F, тусклые красные глаза
            D("2E5664", "0E161C", true, BossAura.RedDots, 0.8f,
              BossTrail.ShadowWaves, BossStrike.Wave, BossBreak.Ripples, "2E5664", "8A3A30", "sig.manta"),
        };

        public static BossSkinDef Of(BossKind boss)
            => All[Mathf.Clamp((int)boss, 0, All.Length - 1)];

        /// <summary>Подпись скина одной строкой — для витрины.</summary>
        public static string Signature(BossKind boss) => Loc.Get(Of(boss).SignatureKey);

        /// <summary>
        /// Босс по рисунку скина. Рисунок и босс — одно и то же, но хранятся в
        /// разных перечислениях, и связь между ними обязана быть в одном месте.
        /// </summary>
        public static bool TryBoss(SkinArt art, out BossKind boss)
        {
            switch (art)
            {
                case SkinArt.Jelly: boss = BossKind.Jelly; return true;
                case SkinArt.Serpent: boss = BossKind.Serpent; return true;
                case SkinArt.Octopus: boss = BossKind.Octopus; return true;
                case SkinArt.Swordfish: boss = BossKind.Swordfish; return true;
                case SkinArt.Crab: boss = BossKind.Crab; return true;
                case SkinArt.Ink: boss = BossKind.Ink; return true;
                case SkinArt.Piranha: boss = BossKind.Piranha; return true;
                case SkinArt.Turtle: boss = BossKind.Turtle; return true;
                case SkinArt.Shark: boss = BossKind.Shark; return true;
                case SkinArt.Manta: boss = BossKind.Manta; return true;
                default: boss = BossKind.Jelly; return false;
            }
        }
    }
}
