using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Какой фактурой покрыт трофейный скин. По одной на босса, повторов нет —
    /// иначе два трофея читались бы как перекрашенная копия.
    /// </summary>
    public enum BossMotif
    {
        /// <summary>Жилки: ветвистые нити, просвечивающие изнутри. Медуза.</summary>
        Veins,
        /// <summary>Чешуя: ряды налегающих друг на друга дуг. Змей.</summary>
        Scales,
        /// <summary>Присоски: два ряда кружков вдоль рук. Осьминог.</summary>
        Suckers,
        /// <summary>Грани: длинные стальные блики вдоль формы. Рыба-меч.</summary>
        Blades,
        /// <summary>Трещины хитина: ломаные линии между пластинами. Краб.</summary>
        Cracks,
        /// <summary>Знаки: мелкие руны, проступающие в черноте. Чернильник.</summary>
        Runes,
        /// <summary>Зубы: ряд острых треугольников. Пиранья.</summary>
        Teeth,
        /// <summary>Пластины: шестиугольные соты панциря. Черепаха.</summary>
        Plates,
        /// <summary>Плакоидные шевроны: мелкая жёсткая насечка. Акула.</summary>
        Chevrons,
        /// <summary>Пятна: крупные тёмные отметины по спине. Скат.</summary>
        Spots,
    }

    /// <summary>
    /// Паспорт трофейного скина: палитра, фактура и характер внутреннего течения.
    ///
    /// **Зачем он появился.** Раньше трофеи рисовались двадцатью отдельными
    /// функциями, и всё, что в них было общего — силуэт босса, — повторялось
    /// руками. Ни каймы, ни тени, ни обводки, ни начинки у них не было вовсе:
    /// <see cref="SkinDef.Trophy"/> оставляет <c>Material = Glass</c>, а
    /// <c>MaterialCore(Glass)</c> возвращает null, поэтому все десять трофеев
    /// имели ровно один слой — плоский силуэт. [КОД]
    ///
    /// Теперь общие слои — кайма, фактура, блик, тень, обводка и начинка —
    /// накладываются одной функцией по данным отсюда, а силуэт остаётся своим.
    /// Новому боссу достаточно одной строки в <see cref="All"/>.
    /// </summary>
    public struct BossSkinDef
    {
        /// <summary>Основной тон тела. Из палитры самого босса, не придуманный.</summary>
        public Color Body;
        /// <summary>Глубокая тень тела и цвет обводки.</summary>
        public Color Deep;
        /// <summary>Контурная кайма: цвет, которым босс читается издалека.</summary>
        public Color Rim;
        /// <summary>Акцент фактуры: чешуи, присосок, зубов, рун.</summary>
        public Color Accent;
        /// <summary>Свечение начинки — то, что медленно течёт внутри.</summary>
        public Color Glow;

        public BossMotif Motif;

        /// <summary>Как часто ложится фактура. Больше — мельче рисунок.</summary>
        public float MotifScale;
        /// <summary>Сила фактуры, 0…1. Выше 0.3 начинается шум, а не деталь.</summary>
        public float MotifStrength;

        /// <summary>
        /// Насколько просвечивает середина тела, чтобы была видна начинка.
        /// 1 — полностью непрозрачная, начинки не видно вовсе.
        /// </summary>
        public float CoreShow;

        /// <summary>Сколько рукавов у внутреннего течения.</summary>
        public int CoreArms;
        /// <summary>Закрутка течения. Больше — круче спираль.</summary>
        public float CoreTwist;
    }

    /// <summary>
    /// Десять трофеев — по одному на босса.
    ///
    /// **Палитра берётся у самого босса**, а не подбирается заново: трофей обязан
    /// опознаваться как его уменьшенная копия. Цвета — те же константы
    /// <see cref="Palette"/>, которыми нарисован сам босс.
    ///
    /// **Пара «пузырёк + страж» делит палитру и фактуру**, а различается формой и
    /// ролью: пузырёк — сам зверь, страж — его оружие или часть.
    /// </summary>
    public static class BossSkinTable
    {
        static BossSkinDef D(Color body, Color deep, Color rim, Color accent, Color glow,
                             BossMotif motif, float motifScale, float motifStrength,
                             float coreShow, int coreArms, float coreTwist)
            => new BossSkinDef
            {
                Body = body, Deep = deep, Rim = rim, Accent = accent, Glow = glow,
                Motif = motif, MotifScale = motifScale, MotifStrength = motifStrength,
                CoreShow = coreShow, CoreArms = coreArms, CoreTwist = coreTwist,
            };

        /// <summary>Порядок строго как в <see cref="BossKind"/>.</summary>
        public static readonly BossSkinDef[] All =
        {
            // Jelly — грозовая медуза: фиолетовый купол, холодные разряды.
            D(Palette.StormBell, Palette.StormDeep, Palette.StormSpark,
              Palette.Hex("9FD8FF"), Palette.Hex("5FC8E8"),
              BossMotif.Veins, 6.5f, 0.2f, 0.68f, 3, 1.6f),

            // Serpent — глубинный змей: сине-зелёное тело, рыжий гребень.
            D(Palette.SerpentBody, Palette.Hex("12222B"), Palette.SerpentSpine,
              Palette.SerpentBelly, Palette.Hex("3E6E7C"),
              BossMotif.Scales, 9f, 0.16f, 0.74f, 2, 1.1f),

            // Octopus — осьминог: пурпур и розовые присоски.
            D(Palette.OctopusBody, Palette.OctopusDark, Palette.OctopusSucker,
              Palette.OctopusEye, Palette.Hex("8A3F96"),
              BossMotif.Suckers, 4f, 0.2f, 0.70f, 4, 1.9f),

            // Swordfish — рыба-меч: сталь и холодный блик.
            D(Palette.SwordBack, Palette.Hex("0B1824"), Palette.SwordBlade,
              Palette.SwordBelly, Palette.Hex("5F7E96"),
              BossMotif.Blades, 3f, 0.16f, 0.76f, 2, 0.7f),

            // Crab — краб: рыжий хитин, тёмные швы.
            D(Palette.CrabShell, Palette.CrabJoint, Palette.CrabHilite,
              Palette.CrabShellDk, Palette.Hex("D2603A"),
              BossMotif.Cracks, 3.5f, 0.18f, 0.74f, 3, 0.9f),

            // Ink — чернильник: почти чёрное тело и лиловые знаки.
            D(Palette.InkMantle, Palette.InkBody, Palette.InkGlow,
              Palette.InkSucker, Palette.Hex("6A3FB0"),
              BossMotif.Runes, 3.2f, 0.22f, 0.66f, 5, 2.3f),

            // Piranha — стая: серое тело, рыжее брюхо, белые зубы.
            D(Palette.PiranhaSwarmBody, Palette.PiranhaSwarmFin, Palette.PiranhaSwarmBelly,
              Palette.PiranhaSwarmTooth, Palette.Hex("A8503A"),
              BossMotif.Teeth, 7f, 0.18f, 0.74f, 3, 1.3f),

            // Turtle — черепаха: оливковый панцирь, соты пластин.
            D(Palette.TurtleShell, Palette.TurtleShellDark, Palette.TurtleEye,
              Palette.TurtleSkin, Palette.Hex("6E8F48"),
              BossMotif.Plates, 2.8f, 0.2f, 0.76f, 2, 0.6f),

            // Shark — акула: свинцовая спина, светлое брюхо.
            D(Palette.SharkBody, Palette.SharkBack, Palette.SharkBelly,
              Palette.SharkGill, Palette.Hex("5A727E"),
              BossMotif.Chevrons, 11f, 0.14f, 0.76f, 2, 0.8f),

            // Manta — великая тень: почти чёрное крыло, бирюзовая жабра.
            D(Palette.MantaSpot, Palette.MantaBack, Palette.MantaGlow,
              Palette.MantaEdge, Palette.Hex("2E7A74"),
              BossMotif.Spots, 2.2f, 0.22f, 0.70f, 3, 1.4f),
        };

        public static BossSkinDef Of(BossKind boss)
            => All[Mathf.Clamp((int)boss, 0, All.Length - 1)];

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
