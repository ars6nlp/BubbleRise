using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Материал скина — то, что видно внутри пузырька.
    ///
    /// Отдельно от редкости и от цвета: цвет красит, материал показывает, из чего
    /// шарик сделан. Двадцать скинов, отличавшихся одним оттенком, и были той самой
    /// «копией друг друга», ради которой этот перечень и появился.
    /// </summary>
    public enum SkinMaterial
    {
        /// <summary>Обычное стекло: начинки нет, и это тоже материал.</summary>
        Glass = 0,
        Coral, Pearl, Silt, Sand, Tide,
        Ice, Toxin, Neon, Resin, Storm,
        Ember, Plasma, Wraith, Prism, Pixel,
        Nebula, Magma, Aurora, Abyss, Leviathan,

        // Для скинов, которые были в игре до магазина. Им тоже нужен свой материал:
        // «такой же шарик, но зелёный» — ровно та проблема, с которой всё началось.
        Verdigris, Cinnabar, Obsidian, Brass,

        /// <summary>Награда за полную коллекцию. Своя — чтобы звезда не делила
        /// внешность и удар с обычным пузырьком, с которого всё начиналось.</summary>
        Starlight,
    }

    /// <summary>Редкость. От неё зависят и цена, и то, что происходит при разрушении.</summary>
    public enum Rarity
    {
        Common = 0,
        Rare,
        Epic,
        Legendary,
        /// <summary>
        /// Трофеи с боссов. Отдельная ступень, а не «эпический подороже»: их нельзя
        /// купить, пока босс жив, и эффектнее они обязаны быть всего остального —
        /// иначе победа над боссом стоит меньше, чем накопленные монеты.
        /// </summary>
        Boss,
    }

    /// <summary>
    /// Как шевелится сам шарик. Под водой он не летит, а висит, поэтому все
    /// варианты — медленные: вода гасит резкое движение, и скин, который дёргается,
    /// читается как ошибка, а не как эффект.
    /// </summary>
    public enum SkinMotion
    {
        /// <summary>Ровное дыхание — базовое поведение.</summary>
        Breathe,
        /// <summary>Покачивание вверх-вниз.</summary>
        Bob,
        /// <summary>Крен из стороны в сторону.</summary>
        Sway,
        /// <summary>Пульсация с резким сжатием и мягким возвратом.</summary>
        Throb,
        /// <summary>Мелкая дрожь поверх дыхания.</summary>
        Shiver,
        /// <summary>Вращение рисунка вокруг оси.</summary>
        Roll,
        /// <summary>Растягивание по диагонали, как капля в потоке.</summary>
        Stretch,
        /// <summary>Рывками: замирает и дёргается, как живое.</summary>
        Flicker,
    }

    /// <summary>
    /// Что тянется за шариком. Все следы всплывают, а не падают: это вода, и
    /// лёгкое в ней идёт вверх. Разница между вариантами — в форме, скорости и
    /// в том, насколько след «вязнет».
    /// </summary>
    public enum SkinTrail
    {
        /// <summary>Обычные пузырьки воздуха.</summary>
        Bubbles,
        /// <summary>Мелкая взвесь, почти неподвижная.</summary>
        Motes,
        /// <summary>Искры, быстро гаснущие.</summary>
        Sparks,
        /// <summary>Осколки и льдинки, медленно всплывающие.</summary>
        Shards,
        /// <summary>Сплошная лента, колышущаяся позади.</summary>
        Ribbon,
        /// <summary>Расходящиеся кольца.</summary>
        Rings,
        /// <summary>Горячие пузыри и пар.</summary>
        Steam,
        /// <summary>Пыль, оседающая вниз вопреки всплытию.</summary>
        Dust,
        /// <summary>Полупрозрачная вуаль, тянущаяся следом.</summary>
        Veil,
        /// <summary>Звёздная крошка, мерцающая на месте.</summary>
        Stardust,
    }

    /// <summary>
    /// Особая анимация уничтожения. Есть только у легендарных скинов, и у каждого
    /// своя: в этом вся разница между «дорогим цветом» и наградой за долгую игру.
    ///
    /// Все варианты обязаны укладываться в полсекунды и не закрывать центр экрана —
    /// иначе красивая смерть препятствия стоит игроку следующего препятствия.
    /// </summary>
    public enum Finisher
    {
        None = 0,
        /// <summary>Ударная волна кольцом.</summary>
        Shockwave,
        /// <summary>Воронка, утягивающая осколки внутрь.</summary>
        Maelstrom,
        /// <summary>Вспышка и дождь звёздной пыли.</summary>
        Starfall,
        /// <summary>Распускающийся цветок света.</summary>
        Bloom,
        /// <summary>Схлопывание в точку и тёмный выброс.</summary>
        Implosion,
    }


    /// <summary>Что светится вокруг шарика. Аура — то, чего скинам не хватало больше всего.</summary>
    public enum AuraStyle
    {
        /// <summary>Ровное мягкое свечение.</summary>
        Glow,
        /// <summary>Чёткое кольцо с зазором до тела.</summary>
        Halo,
        /// <summary>Клубящаяся дымка с рваным краем.</summary>
        Mist,
        /// <summary>Потрескивает: яркость скачет рывками.</summary>
        Crackle,
        /// <summary>Дышит медленно и глубоко.</summary>
        Pulse,
        /// <summary>Переливается: яркость бежит по кругу.</summary>
        Shimmer,
        /// <summary>Холодное свечение, почти без пульсации.</summary>
        Frost,
        /// <summary>Жар: яркое, неровное, с подрагиванием.</summary>
        Heat,
    }

    /// <summary>
    /// Как выглядит разрушенное препятствие. Не «взрыв вообще», а то, что сделал с
    /// ним материал скина: лава сжигает, лёд замораживает, кислота растворяет.
    /// </summary>
    public enum DestroyPattern
    {
        /// <summary>Разлёт осколков во все стороны.</summary>
        Burst,
        /// <summary>Расходящееся кольцо.</summary>
        Ring,
        /// <summary>Всплывающий столб: пар, пепел, пузыри.</summary>
        Rise,
        /// <summary>Схлопывание внутрь.</summary>
        Implode,
        /// <summary>Дуги разряда по сторонам.</summary>
        Arc,
        /// <summary>Закрученная воронка.</summary>
        Spiral,
        /// <summary>Оседающий дождь крошки.</summary>
        Rain,
        /// <summary>Крупные осколки, медленно всплывающие.</summary>
        Shards,
        /// <summary>Расплывающееся облако.</summary>
        Mist,
        /// <summary>Распад на клетки — ровная сетка точек.</summary>
        Grid,
    }

    /// <summary>
    /// Каким бывает удар стража о препятствие.
    ///
    /// Отдельно от разрушения: удар случается всегда, а разрушение — только когда
    /// препятствие кончилось. По прочной створке страж бьёт трижды, и все три раза
    /// игрок обязан видеть, что удар засчитан.
    /// </summary>
    public enum ImpactPattern
    {
        /// <summary>Водяной: кольцо волны и несколько пузырьков.</summary>
        Splash,
        /// <summary>Искры веером от точки касания.</summary>
        Sparks,
        /// <summary>Короткая вспышка и ореол.</summary>
        Flash,
        /// <summary>Дуга разряда между стражем и препятствием.</summary>
        Arc,
        /// <summary>Холодная вспышка и снежная пыль.</summary>
        Frost,
        /// <summary>Скол: несколько крупных осколков.</summary>
        Shatter,
        /// <summary>Брызги струёй вдоль удара.</summary>
        Spray,
        /// <summary>Облачко взвеси.</summary>
        Dust,
        /// <summary>Расходящаяся рябь.</summary>
        Ripple,
        /// <summary>Блеск: короткий яркий отблеск.</summary>
        Gleam,
        /// <summary>Тёмная рябь, втягивающаяся внутрь.</summary>
        Void,
        /// <summary>Распускающийся веер лепестков.</summary>
        Bloom,
        /// <summary>Крупинки, осыпающиеся вниз.</summary>
        Grit,
        /// <summary>Пар: быстро всплывающее облачко.</summary>
        Steam,
    }

    /// <summary>
    /// Что остаётся на препятствии ПОСЛЕ удара.
    ///
    /// Отдельно и от удара, и от разрушения: удар — вспышка в момент касания,
    /// разрушение — финал, а статус живёт между ними, пока препятствие на экране.
    /// Без него выходило странное: страж бьёт по камню огнём, камень остаётся
    /// невозмутимо каменным.
    /// </summary>
    public enum StatusKind
    {
        /// <summary>Горит: раскаляется, чернеет от копоти, сыплет угольками.</summary>
        Burn,
        /// <summary>Леденеет: иней, корка, вращение затухает.</summary>
        Freeze,
        /// <summary>Искрит: мигает и дрожит под разрядами.</summary>
        Shock,
        /// <summary>Разъедается: зеленеет, пузырится, края плывут.</summary>
        Corrode,
        /// <summary>Золотится: покрывается налётом и блёстками.</summary>
        Gild,
        /// <summary>Темнеет: тьма затягивает от точки удара.</summary>
        Shadow,
        /// <summary>Светится звёздами: вспыхивает точками космической пыли.</summary>
        Stardust,
        /// <summary>Кристаллизуется: прорастает гранями, бликует.</summary>
        Crystal,
        /// <summary>Намокает: пузырьки и рябь по поверхности.</summary>
        Soak,
        /// <summary>Бледнеет: становится полупрозрачным и мерцает.</summary>
        Haze,
        /// <summary>Переливается: цвета бегут по поверхности волнами.</summary>
        Spectrum,
        /// <summary>Заносится: оседает пыль и взвесь.</summary>
        Silt,
        /// <summary>Каменеет: тускнеет и покрывается коркой.</summary>
        Petrify,
        /// <summary>Распадается клетками: мигает квадратами.</summary>
        Glitch,
    }

    /// <summary>Аура и разрушение для одного материала.</summary>
    public struct MaterialFxDef
    {
        public Color Aura;
        public AuraStyle AuraStyle;
        public DestroyPattern Pattern;
        /// <summary>Как выглядит удар стража. Своя для каждого материала.</summary>
        public ImpactPattern Impact;
        /// <summary>Что остаётся на препятствии после удара.</summary>
        public StatusKind Status;
        /// <summary>Сколько секунд держится статус. 1.5–4 по заданию редкости.</summary>
        public float StatusTime;
        /// <summary>Цвета эффекта разрушения.</summary>
        public Color Hot;
        public Color Cool;
        /// <summary>
        /// Стадий в эффекте: 1 — короткая вспышка, 2 — с продолжением (замерзание,
        /// потом осколки), 3 — зрелищно. Растёт с редкостью скина, а не сама по себе.
        /// </summary>
        public int Stages;
    }

    /// <summary>
    /// Аура и разрушение по материалу.
    ///
    /// Привязано к материалу, а не к скину, и это не лень: материал у каждого скина
    /// свой, поэтому связка «материал → эффект» автоматически даёт каждому скину
    /// собственный эффект, и его невозможно забыть прописать при добавлении нового.
    /// </summary>
    public static class MaterialFxTable
    {
        static Color C(string hex) => Palette.Hex(hex);

        static MaterialFxDef D(string aura, AuraStyle style, DestroyPattern pattern,
                               string hot, string cool, int stages, ImpactPattern impact,
                               StatusKind status, float statusTime)
            => new MaterialFxDef
            {
                // Все цвета эффекта — под потолок белого 2.3 (E-08): оттенок тот
                // же, яркость ниже. Чистого белого в таблице больше нет вовсе.
                Aura = Palette.SafeLight(C(aura)), AuraStyle = style, Pattern = pattern,
                Hot = Palette.SafeLight(C(hot)), Cool = Palette.SafeLight(C(cool)),
                Stages = stages, Impact = impact,
                Status = status, StatusTime = statusTime,
            };

        /// <summary>Порядок строго как в <see cref="SkinMaterial"/>.</summary>
        public static readonly MaterialFxDef[] All =
        {
            /* Glass     */ D("BFEFFF", AuraStyle.Glow,    DestroyPattern.Burst,  "DFF7FF", "8FD8FF", 1, ImpactPattern.Splash, StatusKind.Soak, 1.6f),
            /* Coral     */ D("FF9EB5", AuraStyle.Pulse,   DestroyPattern.Rise,   "FFD8E0", "E0566F", 1, ImpactPattern.Bloom, StatusKind.Spectrum, 1.8f),
            /* Pearl     */ D("FFF2E0", AuraStyle.Shimmer, DestroyPattern.Ring, "FFEFD6", "E8D8FF", 1, ImpactPattern.Gleam, StatusKind.Gild, 1.8f),
            /* Silt      */ D("8FA86B", AuraStyle.Mist,    DestroyPattern.Mist,   "D8E8B0", "4F6B3A", 1, ImpactPattern.Dust, StatusKind.Silt, 2.0f),
            /* Sand      */ D("E8D8A8", AuraStyle.Glow,    DestroyPattern.Rain,   "F0DFB0", "8F7340", 1, ImpactPattern.Grit, StatusKind.Petrify, 2.0f),
            /* Tide      */ D("8FD8FF", AuraStyle.Halo,    DestroyPattern.Ring,   "C2E8FF", "2F7FB8", 1, ImpactPattern.Ripple, StatusKind.Soak, 2.0f),

            /* Ice       */ D("DFF4FF", AuraStyle.Frost,   DestroyPattern.Shards, "D6F2FF", "4A8FBF", 2, ImpactPattern.Frost, StatusKind.Freeze, 2.6f),
            /* Toxin     */ D("A8FF5B", AuraStyle.Pulse,   DestroyPattern.Mist,   "E8FFC0", "2F6B1F", 2, ImpactPattern.Spray, StatusKind.Corrode, 2.6f),
            /* Neon      */ D("FF5BE8", AuraStyle.Halo,    DestroyPattern.Ring,   "5BF0FF", "FF5BE8", 2, ImpactPattern.Flash, StatusKind.Spectrum, 2.4f),
            /* Resin     */ D("FFB45B", AuraStyle.Glow,    DestroyPattern.Rain,   "FFE8B0", "8F4F10", 2, ImpactPattern.Steam, StatusKind.Petrify, 2.4f),
            /* Storm     */ D("CFE4FF", AuraStyle.Crackle, DestroyPattern.Arc,    "EAF2FF", "7A9FE8", 2, ImpactPattern.Arc, StatusKind.Shock, 2.4f),

            /* Ember     */ D("FF7A3A", AuraStyle.Heat,    DestroyPattern.Rise,   "FFE0A8", "8F2A08", 2, ImpactPattern.Sparks, StatusKind.Burn, 3.0f),
            /* Plasma    */ D("9ADFFF", AuraStyle.Crackle, DestroyPattern.Arc,    "CCD6FF", "5B8FFF", 3, ImpactPattern.Arc, StatusKind.Shock, 3.0f),
            /* Wraith    */ D("CFE8E0", AuraStyle.Mist,    DestroyPattern.Mist,   "DDF2EA", "6F8F8A", 2, ImpactPattern.Void, StatusKind.Haze, 3.0f),
            /* Prism     */ D("EAF0FF", AuraStyle.Shimmer, DestroyPattern.Shards, "F0E2FF", "8FD8FF", 3, ImpactPattern.Shatter, StatusKind.Crystal, 3.0f),
            /* Pixel     */ D("5BFF9A", AuraStyle.Pulse,   DestroyPattern.Grid,   "FF5BD8", "1F7F4F", 2, ImpactPattern.Grit, StatusKind.Glitch, 3.0f),

            /* Nebula    */ D("B88FFF", AuraStyle.Glow,    DestroyPattern.Spiral, "E4D4FF", "5BC8FF", 3, ImpactPattern.Flash, StatusKind.Stardust, 3.6f),
            /* Magma     */ D("FF5B2E", AuraStyle.Heat,    DestroyPattern.Burst,  "FFE87A", "8F1A00", 3, ImpactPattern.Sparks, StatusKind.Burn, 4.0f),
            /* Aurora    */ D("8FFFD8", AuraStyle.Halo,    DestroyPattern.Ring,   "FF8FE8", "8FB8FF", 3, ImpactPattern.Bloom, StatusKind.Spectrum, 3.6f),
            /* Abyss     */ D("6F5BD0", AuraStyle.Pulse,   DestroyPattern.Implode,"C8B8FF", "140B2E", 3, ImpactPattern.Void, StatusKind.Shadow, 3.8f),
            /* Leviathan */ D("3FD8C8", AuraStyle.Halo,    DestroyPattern.Spiral, "DFFFF8", "0F4F5F", 3, ImpactPattern.Ripple, StatusKind.Soak, 3.6f),

            /* Verdigris */ D("8CF0B4", AuraStyle.Glow,    DestroyPattern.Mist,   "DFFFE8", "1F6B4F", 1, ImpactPattern.Dust, StatusKind.Silt, 1.6f),
            /* Cinnabar  */ D("FF6B4A", AuraStyle.Heat,    DestroyPattern.Burst,  "FFC8A8", "6B1208", 1, ImpactPattern.Sparks, StatusKind.Burn, 1.8f),
            /* Obsidian  */ D("3A4150", AuraStyle.Mist,    DestroyPattern.Shards, "8FA8CF", "14171C", 2, ImpactPattern.Shatter, StatusKind.Shadow, 2.2f),
            /* Brass     */ D("FFD98A", AuraStyle.Shimmer, DestroyPattern.Rain, "FFF4D0", "7A5516", 2, ImpactPattern.Gleam, StatusKind.Gild, 2.2f),
            /* Starlight */ D("FFE9A8", AuraStyle.Shimmer, DestroyPattern.Spiral, "FFF6C0", "FFC83D", 3, ImpactPattern.Gleam, StatusKind.Stardust, 3.6f),
        };

        /// <summary>
        /// Эффекты трофейных скинов. У них нет материала — у каждого свой рисунок
        /// самого босса, — поэтому аура и разрушение берутся по боссу.
        /// Порядок строго как в <see cref="BossKind"/>.
        /// </summary>
        public static readonly MaterialFxDef[] Bosses =
        {
            /* Jelly     */ D("3A2E5C", AuraStyle.Mist,    DestroyPattern.Arc,     "7A6AAE", "3A2E5C", 3, ImpactPattern.Arc, StatusKind.Shock, 3.6f),
            /* Serpent   */ D("2B4856", AuraStyle.Mist,    DestroyPattern.Spiral,  "6E9AA4", "2B4856", 3, ImpactPattern.Ripple, StatusKind.Corrode, 3.6f),
            /* Octopus   */ D("5B2E63", AuraStyle.Mist,    DestroyPattern.Implode, "9A6A98", "5B2E63", 3, ImpactPattern.Void, StatusKind.Shadow, 3.6f),
            /* Swordfish */ D("3E5468", AuraStyle.Mist,    DestroyPattern.Shards,  "8EA0AE", "3E5468", 3, ImpactPattern.Shatter, StatusKind.Crystal, 3.6f),
            /* Crab      */ D("7A2E1E", AuraStyle.Mist,    DestroyPattern.Burst,   "B0705A", "7A2E1E", 3, ImpactPattern.Sparks, StatusKind.Burn, 3.6f),
            /* Ink       */ D("2A1A48", AuraStyle.Mist,    DestroyPattern.Mist,    "7A62A8", "2A1A48", 3, ImpactPattern.Dust, StatusKind.Haze, 3.6f),
            /* Piranha   */ D("5A3A34", AuraStyle.Mist,    DestroyPattern.Rain,    "A86A58", "5A3A34", 3, ImpactPattern.Spray, StatusKind.Corrode, 3.4f),
            /* Turtle    */ D("4E6436", AuraStyle.Mist,    DestroyPattern.Ring,    "8A9E6A", "4E6436", 3, ImpactPattern.Frost, StatusKind.Freeze, 3.6f),
            /* Shark     */ D("3E4C56", AuraStyle.Mist,    DestroyPattern.Burst,   "8A98A0", "3E4C56", 3, ImpactPattern.Splash, StatusKind.Petrify, 3.4f),
            /* Manta     */ D("1E2E38", AuraStyle.Mist,    DestroyPattern.Grid,    "4E7A84", "1E2E38", 3, ImpactPattern.Flash, StatusKind.Spectrum, 3.8f),
        };

        public static MaterialFxDef OfBoss(BossKind boss)
            => Bosses[Mathf.Clamp((int)boss, 0, Bosses.Length - 1)];

        public static MaterialFxDef Of(SkinMaterial material)
            => All[Mathf.Clamp((int)material, 0, All.Length - 1)];
    }

    /// <summary>
    /// Паспорт эффекта одного скина. Все числа здесь, а не в коде эффекта: добавить
    /// скин — значит дописать строку в таблицу, а не писать новый класс.
    /// </summary>
    public struct SkinFxDef
    {
        public SkinMotion Motion;
        public SkinTrail Trail;
        public Finisher Finisher;

        /// <summary>Основной цвет частиц. Чёрный с нулевой альфой = брать оттенок скина.</summary>
        public Color Primary;
        /// <summary>Второй цвет: часть следов перемежает два.</summary>
        public Color Secondary;

        /// <summary>Частиц в секунду.</summary>
        public float Rate;
        /// <summary>Амплитуда анимации тела, в долях размера.</summary>
        public float Amplitude;
        /// <summary>Скорость анимации тела.</summary>
        public float Speed;

        public Color PrimaryOr(Color tint) => Primary.a > 0f ? Primary : tint;
        public Color SecondaryOr(Color tint) => Secondary.a > 0f ? Secondary : tint;
    }

    /// <summary>
    /// Таблица эффектов: по одной строке на скин, в том же порядке, что каталог
    /// в <see cref="SkinStore.Bubble"/>.
    ///
    /// Повторов нет сознательно — ни одна пара «движение + след» не встречается
    /// дважды. Проверять это руками бессмысленно, поэтому за соответствием следит
    /// Tools/audit.py: он падает, если таблица разошлась с каталогом по длине или
    /// если два скина получили одинаковую пару.
    /// </summary>
    public static class SkinFxTable
    {
        static Color C(string hex) => Palette.Hex(hex);
        static readonly Color Auto = new Color(0f, 0f, 0f, 0f);   // «взять оттенок скина»

        static SkinFxDef Def(SkinMotion motion, SkinTrail trail, Color primary, Color secondary,
                             float rate, float amplitude, float speed, Finisher finisher = Finisher.None)
        {
            return new SkinFxDef
            {
                Motion = motion, Trail = trail, Finisher = finisher,
                Primary = primary, Secondary = secondary,
                Rate = rate, Amplitude = amplitude, Speed = speed,
            };
        }

        public static readonly SkinFxDef[] All =
        {
            // --- базовый и за глубину -------------------------------------------
            // У стандартного шарика тоже свой эффект: мягкое дыхание и редкие
            // пузырьки. Пустым он быть не может — именно его видят первые минуты.
            /* 0  обычный   */ Def(SkinMotion.Breathe, SkinTrail.Bubbles,  Auto,        C("DFF7FF"), 6f,  0.05f, 1.6f),
            /* 1  изумруд   */ Def(SkinMotion.Bob,     SkinTrail.Motes,    C("8CF0B4"), C("3FBF84"), 8f,  0.07f, 1.9f),
            /* 2  киноварь  */ Def(SkinMotion.Throb,   SkinTrail.Sparks,   C("FF8A6B"), C("FF4030"), 10f, 0.09f, 2.6f),

            // --- за отбитое -----------------------------------------------------
            /* 3  обсидиан  */ Def(SkinMotion.Sway,    SkinTrail.Dust,     C("6A7380"), C("2B2F36"), 9f,  0.06f, 1.3f),
            /* 4  латунь    */ Def(SkinMotion.Shiver,  SkinTrail.Rings,    C("FFD98A"), C("FFC83D"), 5f,  0.04f, 3.1f),

            // --- трофеи с боссов ------------------------------------------------
            /* 5  медуза    */ Def(SkinMotion.Throb,   SkinTrail.Veil,     C("7A6AAE"), C("3A2E5C"), 7f,  0.04f, 1.4f),
            /* 6  змей      */ Def(SkinMotion.Stretch, SkinTrail.Ribbon,   C("6E9AA4"), C("2B4856"), 10f, 0.04f, 2.2f),
            /* 7  спрут     */ Def(SkinMotion.Sway,    SkinTrail.Veil,     C("9A6A98"), C("5B2E63"), 8f,  0.04f, 1.1f),
            /* 8  клинок    */ Def(SkinMotion.Stretch, SkinTrail.Sparks,   C("8EA0AE"), C("3E5468"), 10f, 0.04f, 2.4f),
            /* 9  краб      */ Def(SkinMotion.Shiver,  SkinTrail.Shards,   C("B0705A"), C("7A2E1E"), 7f,  0.04f, 0.5f),
            /* 10 морок     */ Def(SkinMotion.Flicker, SkinTrail.Veil,     C("7A62A8"), C("2A1A48"), 9f,  0.04f, 1.0f),
            /* 11 пиранья   */ Def(SkinMotion.Flicker, SkinTrail.Sparks,   C("A86A58"), C("5A3A34"), 10f, 0.04f, 1.2f),
            /* 12 черепаха  */ Def(SkinMotion.Breathe, SkinTrail.Shards,   C("8A9E6A"), C("4E6436"), 5f,  0.04f, 1.0f),
            /* 13 акула     */ Def(SkinMotion.Roll,    SkinTrail.Dust,     C("8A98A0"), C("3E4C56"), 8f,  0.04f, 1.4f),
            /* 14 скат      */ Def(SkinMotion.Sway,    SkinTrail.Ribbon,   C("4E7A84"), C("1E2E38"), 8f,  0.04f, 0.9f),

            // --- награда за полную коллекцию ------------------------------------
            /* 15 звезда    */ Def(SkinMotion.Roll,    SkinTrail.Stardust, C("FFE9A8"), C("FFC83D"), 14f, 0.08f, 2.0f, Finisher.Starfall),

            // === покупаемые за монеты ===========================================
            // Обычные: дешёвые и спокойные. Их дело — дать первую покупку через
            // пару заплывов, а не поразить.
            /* 16 коралл    */ Def(SkinMotion.Breathe, SkinTrail.Motes,    C("FF9EB5"), C("E0566F"), 7f,  0.06f, 1.5f),
            /* 17 жемчуг    */ Def(SkinMotion.Bob,     SkinTrail.Bubbles,  C("FFF2E0"), C("D8C8B0"), 6f,  0.05f, 1.8f),
            /* 18 тина      */ Def(SkinMotion.Sway,    SkinTrail.Motes,    C("8FA86B"), C("4F6B3A"), 9f,  0.07f, 1.2f),
            /* 19 песок     */ Def(SkinMotion.Shiver,  SkinTrail.Dust,     C("E8D8A8"), C("B89A5F"), 11f, 0.04f, 2.4f),
            /* 20 прилив    */ Def(SkinMotion.Bob,     SkinTrail.Rings,    C("8FD8FF"), C("3F8FBF"), 6f,  0.08f, 1.4f),

            // Редкие: цвет начинает жить своей жизнью.
            /* 21 иней      */ Def(SkinMotion.Shiver,  SkinTrail.Bubbles,  C("DFF4FF"), C("8FC8E8"), 10f, 0.05f, 3.0f),
            /* 22 яд        */ Def(SkinMotion.Throb,   SkinTrail.Motes,    C("A8FF5B"), C("4F9F1F"), 12f, 0.10f, 2.2f),
            /* 23 неон      */ Def(SkinMotion.Throb,   SkinTrail.Rings,    C("FF5BE8"), C("5BF0FF"), 8f,  0.12f, 2.8f),
            /* 24 смола     */ Def(SkinMotion.Breathe, SkinTrail.Steam,    C("FFB45B"), C("C9752E"), 9f,  0.06f, 1.6f),
            /* 25 гроза     */ Def(SkinMotion.Flicker, SkinTrail.Rings,    C("CFE4FF"), C("7A9FE8"), 10f, 0.07f, 3.6f),

            // Эпические: эффект виден издалека.
            /* 26 уголь     */ Def(SkinMotion.Throb,   SkinTrail.Steam,    C("FF7A3A"), C("FFD08A"), 14f, 0.11f, 2.4f),
            /* 27 разряд    */ Def(SkinMotion.Roll,    SkinTrail.Sparks,   C("9ADFFF"), C("D0DAFF"), 16f, 0.08f, 4.2f),
            /* 28 призрак   */ Def(SkinMotion.Bob,     SkinTrail.Veil,     C("CFE8E0"), C("6F8F8A"), 8f,  0.13f, 1.1f),
            /* 29 призма    */ Def(SkinMotion.Roll,    SkinTrail.Shards,   C("EEF2FF"), C("8FD8FF"), 12f, 0.07f, 2.6f),
            /* 30 пиксель   */ Def(SkinMotion.Flicker, SkinTrail.Dust,     C("5BFF9A"), C("FF5BD8"), 15f, 0.05f, 5.0f),

            // Легендарные: у каждого своя анимация уничтожения, и двух одинаковых нет.
            /* 31 туманность*/ Def(SkinMotion.Stretch, SkinTrail.Stardust, C("B88FFF"), C("5BC8FF"), 16f, 0.09f, 1.8f, Finisher.Starfall),
            /* 32 магма     */ Def(SkinMotion.Stretch, SkinTrail.Steam,    C("FF5B2E"), C("FFC45B"), 18f, 0.13f, 2.2f, Finisher.Shockwave),
            /* 33 сияние    */ Def(SkinMotion.Sway,    SkinTrail.Rings,    C("8FFFD8"), C("FF8FE8"), 14f, 0.10f, 1.5f, Finisher.Bloom),
            /* 34 бездна    */ Def(SkinMotion.Breathe, SkinTrail.Veil,     C("2A2440"), C("6F5BD0"), 12f, 0.14f, 0.8f, Finisher.Implosion),
            /* 35 левиафан  */ Def(SkinMotion.Roll,    SkinTrail.Ribbon,   C("3FD8C8"), C("0F6F8F"), 17f, 0.12f, 1.3f, Finisher.Maelstrom),
        };

        public static SkinFxDef Of(int index)
            => All[Mathf.Clamp(index, 0, All.Length - 1)];
    }

}
