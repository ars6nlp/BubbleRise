using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Как устроена оболочка столкновения — форма и характер движения.
    ///
    /// Шестнадцать видов на шестнадцать скинов: пять легендарных, награда за
    /// коллекцию и десять трофеев с боссов. Повторов нет ни одного — в этом весь
    /// смысл: игрок, купивший легендарный скин, должен узнавать его по тому, что
    /// происходит вокруг стража, а не по цене в магазине.
    /// </summary>
    public enum EnvelopeKind
    {
        /// <summary>Кольцо огня и столб пламени вверх, по краю угли.</summary>
        Flame,
        /// <summary>Спираль пыли и звёзд, закручивающаяся внутрь.</summary>
        Spiral,
        /// <summary>Широкие ленты света, колышущиеся вокруг.</summary>
        Ribbon,
        /// <summary>Тёмный водоворот: частицы кружат и тонут.</summary>
        Vortex,
        /// <summary>Водяные кольца, идущие волной по кругу.</summary>
        Coils,
        /// <summary>Звёздная россыпь, медленно оседающая наружу.</summary>
        Starfall,

        /// <summary>Купол ветвистых разрядов.</summary>
        Arc,
        /// <summary>Змейки, скользящие по орбите с ядовитыми каплями.</summary>
        Serpent,
        /// <summary>Щупальца с присосками, втягивающие частицы внутрь.</summary>
        Tentacle,
        /// <summary>Росчерки лезвий, рассекающие по касательной.</summary>
        Blade,
        /// <summary>Клешни: короткие рывки наружу и обратно.</summary>
        Pincer,
        /// <summary>Чернильное облако, расплывающееся кляксами.</summary>
        Ink,
        /// <summary>Рой мелких силуэтов, кружащих рывками.</summary>
        Swarm,
        /// <summary>Каменные пластины, медленно вращающиеся кольцом.</summary>
        Shell,
        /// <summary>Плавники: быстрые дуги, проходящие мимо.</summary>
        Fin,
        /// <summary>Широкие тёмные волны, расходящиеся сеткой.</summary>
        Wing,
    }

    /// <summary>Чем оболочка дотягивается до препятствия.</summary>
    public enum LinkKind
    {
        /// <summary>Язык: плотная струя к точке касания и огонь по контуру.</summary>
        Tongue,
        /// <summary>Дуга: ломаная из нескольких звеньев.</summary>
        Bolt,
        /// <summary>Нить: ровная тонкая линия.</summary>
        Thread,
        /// <summary>Щупальце: изогнутая линия с утолщением на конце.</summary>
        Reach,
        /// <summary>Росчерк: короткий прямой штрих поперёк.</summary>
        Slash,
        /// <summary>Струя: цепочка капель.</summary>
        Jet,
    }

    /// <summary>
    /// Паспорт одной оболочки.
    ///
    /// **Все размеры — в ДИАМЕТРАХ стража (D), и это важно.** Прошлая версия
    /// хранила их в радиусах, но подписывала диаметрами, и оболочка выходила
    /// вчетверо мельче задуманного: при радиусе стража 0.306 юнита «радиус 1.0»
    /// означал 0.5 D, то есть оболочка была меньше самого шарика. Именно отсюда
    /// бралась «крошечная искра» на скриншоте. Теперь единица одна, названа явно,
    /// и пересчёт из D в мир живёт ровно в одном месте —
    /// <see cref="CollisionEnvelope.Tick"/>.
    /// </summary>
    public struct EnvelopeDef
    {
        public EnvelopeKind Kind;
        public LinkKind Link;

        /// <summary>Основной цвет. Насыщенный, не белый.</summary>
        public Color Hot;
        /// <summary>Вторичный: в него уходит частица к концу жизни.</summary>
        public Color Cool;
        /// <summary>Акцент: искры, угли, присоски — то, что видно точками.</summary>
        public Color Accent;
        /// <summary>
        /// Тёмный край. Рисуется под ярким телом слоем крупнее — без него
        /// светлые линии на тёмной воде расплываются и теряют силуэт.
        /// </summary>
        public Color Edge;

        /// <summary>Сколько держится оболочка: 1.5–2.2 легендарным, 2.0–3.0 трофеям.</summary>
        public float Duration;
        /// <summary>Частиц во всей оболочке: 60–90 легендарным, 100–150 трофеям.</summary>
        public int Count;
        /// <summary>
        /// Радиус оболочки в диаметрах стража. 1.9–2.6.
        ///
        /// Было 2.6–3.4: оболочка занимала до 21 % площади экрана и закрывала
        /// ближние препятствия и снаряды босса. Урезано на четверть; самая
        /// крупная теперь занимает 11.5 % вместо 21.3 %. [РАСЧЁТ]
        /// </summary>
        public float RadiusD;
        /// <summary>Скорость движения по своей траектории, оборотов в секунду.</summary>
        public float Speed;
        /// <summary>Сколько прядей: ветвей молнии, языков пламени, щупалец. 4–7.</summary>
        public int Branches;
        /// <summary>Толщина связи у основания, в диаметрах стража. Не меньше 0.25.</summary>
        public float LinkWidthD;
        /// <summary>Есть ли у скина второй слой. У трофеев есть, у легендарных нет.</summary>
        public bool Rich;
    }

    /// <summary>
    /// Оболочки столкновения для легендарных скинов и трофеев с боссов.
    ///
    /// **Почему только им.** У обычных, редких и эпических уже есть свой тихий
    /// эффект удара, и добавлять им оболочку значило бы стереть разницу между
    /// «скин за две тысячи» и «скин за девять». Легендарный и трофейный — то, ради
    /// чего копят; у них должно быть видно, за что.
    ///
    /// **Почему таблицей, а не кодом у каждого скина.** Логика у всех одна:
    /// частицы ходят по своей траектории вокруг стража, плавно появляются и
    /// гаснут. Различаются числа и форма траектории. Новому скину хватит одной
    /// строки здесь — как и всему остальному в этом проекте.
    ///
    /// **Ключ — материал и босс, а не номер скина.** Номера меняются при любой
    /// перестановке каталога, а материал у скина свой навсегда.
    /// </summary>
    public static class EnvelopeTable
    {
        static Color C(string hex) => Palette.Hex(hex);

        static EnvelopeDef D(EnvelopeKind kind, LinkKind link,
                             string hot, string cool, string accent, string edge,
                             float duration, int count, float radiusD, float speed,
                             int branches, float linkWidthD, bool rich)
            => new EnvelopeDef
            {
                Kind = kind, Link = link,
                // Потолок белого 2.3 (E-08) — у восемнадцати цветов из 64 он был выше.
                Hot = Palette.SafeLight(C(hot)), Cool = Palette.SafeLight(C(cool)),
                Accent = Palette.SafeLight(C(accent)), Edge = Palette.SafeLight(C(edge)),
                Duration = duration, Count = count, RadiusD = radiusD, Speed = speed,
                Branches = branches, LinkWidthD = linkWidthD, Rich = rich,
            };

        // ------------------------------------------------------- легендарные

        /// <summary>
        /// Шесть легендарных: пять покупаемых и награда за полную коллекцию.
        /// 60–90 частиц, радиус 2.5–2.9 D, 1.5–2.2 с.
        /// </summary>
        public static readonly (SkinMaterial Material, EnvelopeDef Def)[] Legendary =
        {
            // Магма: кольцо огня вокруг стража и столб пламени вверх, по краю
            // тлеющие угли. Под водой это пар и горячие пузыри, а не дым.
            (SkinMaterial.Magma,
             D(EnvelopeKind.Flame, LinkKind.Tongue, "FF6B2E", "FFC45B", "FFE08A", "5E1400",
               1.9f, 84, 2.10f, 0.5f, 6, 0.34f, false)),

            // Туманность: спираль пыли и мелких звёзд, затягивающаяся внутрь.
            (SkinMaterial.Nebula,
             D(EnvelopeKind.Spiral, LinkKind.Thread, "B88FFF", "5BC8FF", "E8DFFF", "1E1038",
               2.0f, 78, 2.17f, 0.35f, 5, 0.26f, false)),

            // Сияние: широкие переливающиеся ленты, колышутся медленно.
            (SkinMaterial.Aurora,
             D(EnvelopeKind.Ribbon, LinkKind.Jet, "7CFFD0", "FF8FE8", "8FB8FF", "0C3A33",
               2.0f, 72, 2.03f, 0.3f, 4, 0.3f, false)),

            // Бездна: тёмный водоворот, частицы тонут и гаснут.
            (SkinMaterial.Abyss,
             D(EnvelopeKind.Vortex, LinkKind.Reach, "8A6BFF", "2A1358", "3FD8C8", "0B0620",
               1.9f, 80, 2.03f, 0.45f, 5, 0.32f, false)),

            // Левиафан: водяные кольца идут волной, следом пена.
            (SkinMaterial.Leviathan,
             D(EnvelopeKind.Coils, LinkKind.Jet, "3FD8C8", "1A6A7A", "BFF7EE", "062A33",
               1.8f, 76, 1.95f, 0.4f, 4, 0.3f, false)),

            // Звезда глубин: награда за коллекцию. Россыпь, оседающая наружу.
            (SkinMaterial.Starlight,
             D(EnvelopeKind.Starfall, LinkKind.Thread, "FFE08A", "FFB82E", "FFF0C0", "4A3200",
               1.8f, 70, 1.95f, 0.25f, 5, 0.27f, false)),
        };

        // ------------------------------------------------------------ боссы

        /// <summary>
        /// Десять трофеев. 100–150 частиц, радиус 2.9–3.4 D, 2.0–3.0 с: трофей
        /// нельзя купить, пока босс жив, и выглядеть он обязан заметнее
        /// купленного — формой и числом, а не яркостью: цвета — приглушённые тона
        /// самого босса, как у мини-босса. Порядок строго как в <see cref="BossKind"/>.
        /// </summary>
        public static readonly EnvelopeDef[] Boss =
        {
            // Jelly — «Шаровая молния»: купол из толстых ветвистых разрядов.
            D(EnvelopeKind.Arc, LinkKind.Bolt, "3E8A86", "4A3D78", "6A5A9C", "0E0A1E",
              2.4f, 132, 2.40f, 0.6f, 6, 0.33f, true),

            // Serpent — «Клык змея»: змейки по орбите и ядовитые капли.
            D(EnvelopeKind.Serpent, LinkKind.Reach, "6E9AA4", "2B4856", "948A76", "0E1A20",
              2.4f, 124, 2.25f, 0.5f, 5, 0.3f, true),

            // Octopus — «Присоска»: щупальца втягивают частицы внутрь.
            D(EnvelopeKind.Tentacle, LinkKind.Reach, "8A5A94", "4A2652", "9C6A88", "1E0A22",
              2.6f, 138, 2.47f, 0.35f, 6, 0.34f, true),

            // Swordfish — «Остриё клинка»: стальные росчерки по касательной.
            D(EnvelopeKind.Blade, LinkKind.Slash, "8EA0AE", "3E5468", "6E8699", "0E1A26",
              2.0f, 112, 2.47f, 0.9f, 5, 0.3f, true),

            // Crab — «Клешня»: короткие рывки наружу и обратно.
            D(EnvelopeKind.Pincer, LinkKind.Slash, "A8604A", "6E2A1C", "A08C6A", "2A0E06",
              2.1f, 118, 2.17f, 0.7f, 6, 0.31f, true),

            // Ink — «Чернильник»: клякса расплывается, внутри искры. Светлее, чем
            // просится по смыслу: на тёмно-синей воде настоящих чернил не видно.
            D(EnvelopeKind.Ink, LinkKind.Tongue, "5E4A8A", "2A1A48", "7A62A8", "0E0A16",
              2.6f, 136, 2.40f, 0.25f, 5, 0.34f, true),

            // Piranha — «Стая»: мелкие силуэты кружат рывками.
            D(EnvelopeKind.Swarm, LinkKind.Slash, "8C5048", "4A2A26", "7C8D96", "1E0A08",
              2.1f, 142, 2.47f, 0.8f, 6, 0.28f, true),

            // Turtle — «Панцирь»: каменные пластины вращаются кольцом.
            D(EnvelopeKind.Shell, LinkKind.Thread, "7A8E5A", "3E5430", "8E8A6A", "101C0C",
              2.6f, 106, 2.17f, 0.22f, 4, 0.29f, true),

            // Shark — «Плавник»: быстрые серые дуги проходят мимо.
            D(EnvelopeKind.Fin, LinkKind.Slash, "7A8A94", "3E4C56", "8A969C", "101820",
              2.0f, 116, 2.50f, 0.85f, 5, 0.3f, true),

            // Manta — «Крыло»: широкие тёмные волны расходятся сеткой.
            D(EnvelopeKind.Wing, LinkKind.Bolt, "3E6A74", "1E3440", "8A3A30", "06101A",
              2.6f, 148, 2.50f, 0.3f, 7, 0.32f, true),
        };

        /// <summary>
        /// Есть ли у скина оболочка, и какая.
        ///
        /// Возвращает ложь для всех прочих редкостей — у них свой тихий эффект, и
        /// это намеренно.
        /// </summary>
        /// <summary>
        /// Общая прозрачность оболочки трофеев. Цвета у них — приглушённые тона босса,
        /// но частиц много и они ложатся друг на друга: плотное кольцо без этого
        /// множителя всё равно читалось бы светящимся. Размер и число — прежние.
        /// </summary>
        public const float TrophyAlpha = 0.6f;

        /// <summary>Множитель прозрачности оболочки: у трофеев — <see cref="TrophyAlpha"/>, у легендарных — 1.</summary>
        public static float Alpha(EnvelopeDef def) => def.Rich ? TrophyAlpha : 1f;

        public static bool TryGet(SkinDef skin, out EnvelopeDef def)
        {
            if (skin.Kind == UnlockKind.Boss)
            {
                int i = Mathf.Clamp((int)skin.UnlockBoss, 0, Boss.Length - 1);
                def = Boss[i];
                return true;
            }

            if (skin.Rarity == Rarity.Legendary)
            {
                for (int i = 0; i < Legendary.Length; i++)
                {
                    if (Legendary[i].Material != skin.Material) continue;
                    def = Legendary[i].Def;
                    return true;
                }
            }

            def = default;
            return false;
        }
    }
}
