using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Из каких слоёв состоит оболочка. Их по-прежнему четыре, но рисуются они
    /// по-разному: тень и ядро — кольца, то есть по одному спрайту на слой;
    /// форма и искры — россыпь, то есть отдельные частицы.
    /// </summary>
    public enum EnvelopeLayer
    {
        /// <summary>Тёмная подложка под ядром: без неё яркое тело расплывается.</summary>
        Shadow,
        /// <summary>Ядро: плотное кольцо вплотную к стражу. Тело эффекта.</summary>
        Core,
        /// <summary>Внешняя форма: то самое, по чему скин узнают. Силуэт.</summary>
        Shape,
        /// <summary>Искры: мелкое и быстрое по краю.</summary>
        Spark,
    }

    /// <summary>
    /// Где и какой должна быть частица оболочки прямо сейчас.
    ///
    /// **Единица измерения — ДИАМЕТР стража (D), везде и без исключений.** Радиус
    /// стража равен 0.5 D, то есть частица на расстоянии 1.0 лежит вдвое дальше
    /// от центра, чем поверхность шарика. Прошлая версия считала в радиусах, а
    /// подписывала диаметрами, и оболочка выходила вчетверо мельче задуманного —
    /// меньше самого стража. Пересчёт D → мир живёт ровно в одном месте:
    /// <see cref="CollisionEnvelope.Tick"/> масштабирует корень на диаметр.
    /// Множитель размера игрока (0.68) уже внутри этого диаметра, и второй раз
    /// его не применяет никто.
    ///
    /// **Почему описание, а не отрисовка.** Ровно по той же причине, по которой
    /// так устроен след скина: в бою оболочка рисуется спрайтами в мировых
    /// координатах, в витрине — точками на холсте. Общим может быть только
    /// описание. Если бы каждая сторона считала траектории сама, они разошлись бы
    /// при первой же правке — это в проекте уже случалось и с оболочкой скина, и
    /// со следом.
    ///
    /// **Четыре слоя вместо одного.** Одним потоком частиц силуэт не строится:
    /// получается облако точек, одинаковое у всех. Теперь у каждой оболочки есть
    /// тёмная подложка, плотное ядро, своя внешняя форма и искры — и узнаётся она
    /// по форме, а не по цвету.
    ///
    /// **Размер не зависит от настроек.** В этом файле нет ни одного обращения к
    /// <see cref="Visuals"/>, кроме числа частиц. Урезать размер настройке просто
    /// нечем, и это проверяется аудитом.
    /// </summary>
    public static class EnvelopeShape
    {
        /// <summary>Одна частица: где, какого размера и цвета. Всё в D.</summary>
        public struct Sampled
        {
            public Vector2 Position;
            public float Size;
            public Color Color;
            public float Alpha;
        }

        /// <summary>
        /// Сколько ОТДЕЛЬНЫХ частиц уходит в каждый россыпной слой.
        ///
        /// Тени и ядра здесь нет: они стали кольцами и стоят по одному спрайту
        /// каждое независимо от плотности (см. <see cref="Rings"/>). Раньше на
        /// них уходило 32 % всех частиц — восемнадцать и тридцать объектов у
        /// самой плотной оболочки, — и каждому из них каждый кадр переписывали
        /// Transform ради фигуры, которая целиком помещается в одну текстуру.
        /// </summary>
        public struct Split
        {
            public int Shape, Spark;
            public int Total => Shape + Spark;
        }

        /// <summary>Во что превращается кольцевой слой: масштаб и прозрачность.</summary>
        public struct RingState
        {
            public float ShadowScale, ShadowAlpha;
            public float CoreScale, CoreAlpha;
            public float AccentAlpha;
        }

        /// <summary>Потолок частиц в одной оболочке. Трофеям положено до 150.</summary>
        public const int MaxCount = 150;

        /// <summary>
        /// Общий множитель размера частиц и толщины связи.
        ///
        /// Радиус оболочки урезан на четверть, и детали урезаны вместе с ним —
        /// иначе тот же рисунок в меньшем круге слипается в плотное пятно. Но
        /// не на те же 25 %, а на 15: при полном пересчёте эффект рассыпался бы
        /// на отдельные точки, чего задание прямо запрещает. Числа частиц при
        /// этом не менялись вовсе. [РАСЧЁТ]
        /// </summary>
        public const float DetailScale = 0.85f;

        /// <summary>
        /// Насколько прозрачнее внешний край оболочки. Сквозь него обязаны
        /// просвечивать препятствия и снаряды — это требование читаемости, а не
        /// вкусовое.
        /// </summary>
        public const float EdgeFade = 0.6f;

        /// <summary>Общий бюджет точек на ход прядей к цели.</summary>
        public const int LinkTravelBudget = 32;

        /// <summary>Точек в пятне по препятствию. Общее на все ветви.</summary>
        public const int LinkSpread = 8;

        /// <summary>
        /// Сколько частиц в оболочке.
        ///
        /// Ступень яркости убавляет плотность максимум на четверть (см.
        /// <see cref="Visuals.Particles"/>) — и только плотность. Размер,
        /// радиус и число прядей от неё не зависят вовсе.
        /// </summary>
        public static int Count(EnvelopeDef def, float intensity)
        {
            float n = def.Count * Visuals.Particles * intensity;
            return Mathf.Clamp(Mathf.RoundToInt(n), 24, MaxCount);
        }

        /// <summary>
        /// Разложить плотность по россыпным слоям.
        ///
        /// Доли от <see cref="EnvelopeDef.Count"/>: половина — внешняя форма,
        /// десятая часть — искры. Остальное раньше уходило на тень и ядро, а
        /// теперь на них хватает трёх спрайтов на всю оболочку.
        ///
        /// Искр стало меньше (0.1 вместо 0.18), но каждая крупнее — покрытие
        /// края то же, а объектов вдвое меньше. Это единственное место, где
        /// плотность изменилась намеренно.
        /// </summary>
        public static Split Layers(EnvelopeDef def, float intensity)
        {
            int n = Count(def, intensity);

            return new Split
            {
                Shape = Mathf.Max(8, Mathf.RoundToInt(n * 0.5f)),
                Spark = Mathf.Max(4, Mathf.RoundToInt(n * 0.1f)),
            };
        }

        /// <summary>
        /// Состояние колец: тень и ядро. Масштаб — в диаметрах стража, ровно
        /// такой, чтобы полоса спрайта легла туда, где раньше стояли точки.
        ///
        /// Полоса спрайта стоит на 0.72 его половины, поэтому масштаб равен
        /// радиусу кольца, делённому на 0.36.
        /// </summary>
        public static RingState Rings(EnvelopeDef def, float age)
        {
            float breath = Breath(age);

            // Ядро: там же, где были точки — радиус 0.72·дыхание + 0.05.
            float coreR = 0.72f * breath + 0.05f;

            // Тень: чуть шире ядра и заметно тусклее. Её задача — тёмный контур
            // под светлым телом, а не второй светящийся слой.
            float shadowR = 0.8f;

            return new RingState
            {
                ShadowScale = shadowR / 0.36f * 1.12f,
                ShadowAlpha = 0.55f,
                CoreScale = coreR / 0.36f,
                CoreAlpha = 0.92f * breath,
                AccentAlpha = 0.85f * breath,
            };
        }

        /// <summary>
        /// Сколько точек уходит на ход прядей к цели.
        ///
        /// Бюджет общий на все ветви, а не по девять на каждую: иначе у скина с
        /// семью ветвями одна только связь стоила шестидесяти трёх спрайтов —
        /// больше, чем весь силуэт у легендарного скина.
        ///
        /// Разреженность компенсируется толщиной: <see cref="LinkSize"/> не даёт
        /// точке стать меньше шага, так что линия остаётся сплошной и на далёкой
        /// цели. Это дешевле, чем добавлять точки.
        /// </summary>
        public static int LinkTravelSteps(EnvelopeDef def)
            => Mathf.Clamp(LinkTravelBudget / Strands(def), 3, 8);

        /// <summary>Сколько точек уходит на пятно по препятствию.</summary>
        public static int LinkSpreadCount(EnvelopeDef def) => LinkSpread;

        /// <summary>Всего точек у связи.</summary>
        public static int LinkCount(EnvelopeDef def)
            => Strands(def) * LinkTravelSteps(def) + LinkSpreadCount(def);

        /// <summary>
        /// Сколько отдельных прядей у этой формы: лент, щупалец, лезвий, рядов.
        /// Берётся из паспорта скина, а не из вида: число ветвей — это то, что
        /// настраивают, когда эффект кажется редким.
        /// </summary>
        public static int Strands(EnvelopeDef def) => Mathf.Clamp(def.Branches, 1, 8);

        /// <summary>
        /// Прозрачность по доле пройденного пути: появляется быстро, гаснет под
        /// конец. Колокол здесь запрещён — он стирает саму траекторию, и именно
        /// из-за него спираль, водоворот, кольца и россыпь когда-то выглядели
        /// одним и тем же кольцом из точек.
        /// </summary>
        static float Fade(float life) => Mathf.Min(1f, life * 6f) * (1f - life * 0.8f);

        /// <summary>
        /// Медленное дыхание ядра: один цикл за 1.75 с. Быстрее нельзя — это уже
        /// мерцание, а оно в правилах безопасности отдельным пунктом.
        /// </summary>
        static float Breath(float age) => 0.82f + 0.18f * Mathf.Sin(age * 3.59f);

        // ------------------------------------------------------- общие слои

        /// <summary>
        /// Искры: мелкое, быстрое и редкое по краю оболочки. Им позволено уходить
        /// чуть дальше внешней формы — так у эффекта появляется неровный край, а
        /// не обрез по циркулю.
        /// </summary>
        static Sampled SampleSpark(EnvelopeDef def, int index, int count, float age, float seed)
        {
            float life = Mathf.Repeat(index / (float)Mathf.Max(1, count) + age * 0.55f, 1f);
            float a = seed * 6.2832f + life * 1.4f;
            float rr = Mathf.Lerp(0.85f, def.RadiusD * 1.08f, life);
            float twinkle = 0.75f + 0.25f * Mathf.Sin(age * 3f + seed * 9f);

            return new Sampled
            {
                Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr,
                // Крупнее прежнего ровно настолько, насколько их стало меньше:
                // площадь на искру выросла вдвое, значит поперечник в 1.4 раза.
                Size = 0.2f + 0.12f * seed,
                Color = index % 3 == 0 ? def.Accent : def.Cool,
                Alpha = Fade(life) * twinkle,
            };
        }

        // -------------------------------------------------------------- вход

        /// <summary>
        /// Частица оболочки.
        /// </summary>
        /// <param name="age">Сколько живёт оболочка.</param>
        /// <param name="seed">Своя фаза частицы, 0…1 — чтобы не шли строем.</param>
        /// <param name="arcStep">
        /// Номер перестройки формы с дробной частью. Между соседними номерами
        /// форма переходит плавно (<see cref="StepNoise"/>).
        /// </param>
        public static Sampled Sample(EnvelopeDef def, EnvelopeLayer layer,
                                     int index, int count, float age, float seed, float arcStep)
        {
            // Тень и ядро сюда не попадают: они кольца, и их описывает Rings.
            var s = layer == EnvelopeLayer.Spark
                ? SampleSpark(def, index, count, age, seed)
                : SampleShape(def, index, count, age, seed, arcStep);

            s.Size *= DetailScale;

            // Чем дальше от стража, тем прозрачнее: ближние препятствия должны
            // читаться сквозь край оболочки.
            float out01 = def.RadiusD > 0.01f
                ? Mathf.Clamp01((s.Position.magnitude - 0.9f) / Mathf.Max(0.3f, def.RadiusD - 0.9f))
                : 0f;
            s.Alpha *= Mathf.Lerp(1f, EdgeFade, Mathf.SmoothStep(0f, 1f, out01));

            return s;
        }

        // ------------------------------------------------- внешняя форма, 16 видов

        static Sampled SampleShape(EnvelopeDef def, int index, int count,
                                   float age, float seed, float arcStep)
        {
            count = Mathf.Max(1, count);

            float slot = index / (float)count;
            float t = age * def.Speed;
            float angle = (slot + t) * Mathf.PI * 2f;
            float r = def.RadiusD;

            // Пряди: у каждой свой непрерывный кусок номеров, иначе лента,
            // собранная из каждой третьей частицы, лентой не выглядит.
            int strands = Strands(def);
            int strand = index % strands;
            int perStrand = Mathf.Max(1, Mathf.CeilToInt(count / (float)strands));
            float along = Mathf.Clamp01((index / strands) / (float)perStrand);

            switch (def.Kind)
            {
                case EnvelopeKind.Flame:
                {
                    // Шесть языков пламени поднимаются из кольца вверх, сужаясь.
                    // Высота до 2.2 D — это и есть «столб», а не искра.
                    float rise = Mathf.Repeat(along + age * 0.75f + strand * 0.13f, 1f);
                    float lean = Mathf.Sin(strand * 2.3f + age * 1.1f) * 0.55f;
                    float spread = (strand / (float)strands - 0.5f) * 1.7f;
                    bool ember = index % 5 == 0;

                    return new Sampled
                    {
                        Position = new Vector2(
                            (spread + lean * rise) * (1f - rise * 0.35f),
                            -0.45f + rise * (r * 0.85f + 1.0f)),
                        Size = ember ? 0.16f : Mathf.Lerp(0.56f, 0.18f, rise),
                        Color = ember ? def.Accent : Color.Lerp(def.Hot, def.Cool, rise * 0.85f),
                        Alpha = ember ? 0.9f : Fade(rise),
                    };
                }

                case EnvelopeKind.Spiral:
                {
                    // Спираль затягивается внутрь: радиус падает по ходу жизни, а
                    // угол обгоняет радиус — оттого и виток.
                    // Рукава, а не кольцо. Радиус обязан расти ВМЕСТЕ с углом по
                    // длине пряди — иначе на экране оказываются все радиусы
                    // сразу, и спираль не отличить от водоворота и от колец.
                    float a = along * 5f + strand / (float)strands * Mathf.PI * 2f
                              + age * 1.2f;
                    float rr = Mathf.Lerp(0.75f, r, along);
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr,
                        Size = Mathf.Lerp(0.42f, 0.18f, along),
                        Color = index % 5 == 0 ? def.Accent : Color.Lerp(def.Hot, def.Cool, along),
                        Alpha = 0.95f - along * 0.25f,
                    };
                }

                case EnvelopeKind.Ribbon:
                {
                    // Четыре широкие ленты. Каждая — сплошная дуга в три четверти
                    // круга, колышущаяся по длине.
                    float a = (along * 0.42f + t) * Mathf.PI * 2f + strand * 1.9f;
                    float wave = Mathf.Sin(along * Mathf.PI * 2f + age * 1.6f + strand);
                    float rr = r * (0.78f + 0.22f * wave);
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr,
                        Size = 0.44f + 0.12f * wave,
                        Color = strand == 0 ? def.Hot
                              : strand == 1 ? def.Cool
                              : strand == 2 ? def.Accent : Color.Lerp(def.Hot, def.Accent, 0.5f),
                        Alpha = 0.62f + 0.28f * Mathf.Abs(wave),
                    };
                }

                case EnvelopeKind.Vortex:
                {
                    // Водоворот: частица кружит, тонет и гаснет. Угол развязан с
                    // глубиной через seed — иначе все собирались в одну дугу.
                    // Воронка: частица кружит, сужается и УХОДИТ ВНИЗ. Снос по
                    // вертикали сильный и приплюснутость заметная — иначе
                    // водоворот читался бы как ещё одно кольцо.
                    float sink = Mathf.Repeat(slot + age * 0.5f, 1f);
                    float a = seed * 6.2832f + angle - sink * 4f;
                    float rr = Mathf.Lerp(r, 0.55f, sink);
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a) * rr,
                                               Mathf.Sin(a) * rr * 0.55f - sink * r * 0.85f),
                        Size = Mathf.Lerp(0.44f, 0.16f, sink),
                        Color = index % 6 == 0 ? def.Accent : Color.Lerp(def.Hot, def.Cool, sink),
                        Alpha = Fade(sink) * 0.95f,
                    };
                }

                case EnvelopeKind.Coils:
                {
                    // Четыре водяных кольца расходятся по очереди: пока одно
                    // уходит, следующее только рождается. Это и есть волна.
                    float phase = Mathf.Repeat(age * 0.7f + strand / (float)strands, 1f);
                    float a = (along + t) * Mathf.PI * 2f;
                    float rr = Mathf.Lerp(0.8f, r, phase);
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr,
                        Size = Mathf.Lerp(0.42f, 0.2f, phase),
                        Color = index % 7 == 0 ? def.Accent : Color.Lerp(def.Hot, def.Cool, phase),
                        Alpha = Fade(phase) * 0.95f,
                    };
                }

                case EnvelopeKind.Starfall:
                {
                    // Россыпь оседает наружу и мерцает — медленно, по правилам.
                    // Россыпь не просто расходится, а оседает: у каждой искры
                    // есть снос вниз. Без него она ничем не отличалась бы от
                    // спирали, рассыпавшейся по кругу.
                    float out01 = Mathf.Repeat(slot + age * 0.4f, 1f);
                    float a = seed * 6.2832f + angle * 0.6f;
                    float rr = Mathf.Lerp(0.75f, r, out01);
                    float twinkle = 0.86f + 0.14f * Mathf.Sin(age * 3f + seed * 9f);
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a) * rr,
                                               Mathf.Sin(a) * rr - out01 * out01 * r * 0.55f),
                        Size = 0.3f + 0.24f * seed,
                        Color = index % 3 == 0 ? def.Accent : def.Hot,
                        Alpha = Fade(out01) * twinkle,
                    };
                }

                case EnvelopeKind.Arc:
                {
                    // Купол разряда: шесть толстых ветвей наружу. Форма
                    // перестраивается не чаще ArcHz раз в секунду — мелькание
                    // кадр за кадром запрещено, а дуга живёт не меньше 0.25 с.
                    // Ветвь — ЛИНИЯ, а не облако. Наклон у неё один на всю длину,
                    // а излом меняется лишь пять раз по длине: если дёргать
                    // каждую точку отдельно, из молнии получается туман, что и
                    // было в прошлой версии.
                    float tilt = (StepNoise(strand * 31, arcStep, 7) - 0.5f) * 0.5f;
                    float kink = (StepNoise(strand * 131 + (int)(along * 5f), arcStep, 17) - 0.5f) * 0.3f;
                    float a = strand / (float)strands * Mathf.PI * 2f + arcStep * 0.21f
                              + tilt + kink * along;
                    float rr = Mathf.Lerp(0.6f, r, along);

                    // Ветвление: каждая третья точка отходит вбок своей веточкой.
                    float fork = (index % 3 == 2) ? kink * 2.6f * along : 0f;

                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a + fork), Mathf.Sin(a + fork)) * rr,
                        Size = Mathf.Lerp(0.34f, 0.22f, along),
                        Color = along > 0.8f ? def.Accent : Color.Lerp(def.Hot, def.Cool, along),
                        Alpha = 0.92f - along * 0.15f,
                    };
                }

                case EnvelopeKind.Serpent:
                {
                    // Пять змеек скользят по орбите, за ними ядовитые капли.
                    float a = (along * 0.5f + t) * Mathf.PI * 2f + strand * 1.3f;
                    float wave = Mathf.Sin(along * 9f + age * 3f) * 0.42f;
                    bool drop = index % 6 == 0;
                    Vector2 on = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * 0.86f + wave);
                    return new Sampled
                    {
                        Position = drop ? on + Vector2.down * 0.5f : on,
                        Size = drop ? 0.2f : Mathf.Lerp(0.46f, 0.2f, along),
                        Color = drop ? def.Accent : Color.Lerp(def.Hot, def.Cool, along),
                        Alpha = drop ? 0.8f : 0.92f - along * 0.3f,
                    };
                }

                case EnvelopeKind.Tentacle:
                {
                    // Шесть щупалец тянутся наружу и втягиваются обратно, на
                    // каждом присоски.
                    float reach = 0.6f + 0.4f * Mathf.Sin(age * 1.5f + strand * 1.25f);
                    float a = strand / (float)strands * Mathf.PI * 2f
                              + Mathf.Sin(along * 3f + age) * 0.35f;
                    float rr = Mathf.Lerp(0.7f, r * reach, along);
                    bool cup = (index / strands) % 3 == 0;
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr,
                        Size = cup ? 0.3f : Mathf.Lerp(0.5f, 0.18f, along),
                        Color = cup ? def.Accent : Color.Lerp(def.Hot, def.Cool, along),
                        Alpha = 0.9f - along * 0.25f,
                    };
                }

                case EnvelopeKind.Blade:
                {
                    // Пять росчерков по касательной: прямые штрихи мимо стража,
                    // а не вокруг него.
                    float a = strand / (float)strands * Mathf.PI * 2f + age * 0.9f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 tan = new Vector2(-dir.y, dir.x);
                    float swing = Mathf.Repeat(age * 1.4f + strand * 0.2f, 1f);
                    return new Sampled
                    {
                        // Вылет вбок укорочен вдвое: при полутора радиусах
                        // соседние росчерки смыкались концами, и пять штрихов
                        // читались как ровный пятиугольник.
                        Position = dir * Mathf.Lerp(1.1f, r, swing)
                                   + tan * ((along - 0.5f) * r * 0.72f),
                        Size = Mathf.Sin(along * Mathf.PI) * 0.4f + 0.12f,
                        Color = along > 0.6f ? def.Cool : def.Hot,
                        Alpha = Mathf.Clamp01((0.4f + Mathf.Sin(along * Mathf.PI) * 0.6f)
                                              * (0.45f + Fade(swing) * 0.55f)),
                    };
                }

                case EnvelopeKind.Pincer:
                {
                    // Шесть клешней: рывок наружу и обратно, резкий, но не
                    // мгновенный — иначе это мерцание.
                    float beat = Mathf.Repeat(age * 1.3f + strand * 0.17f, 1f);
                    float punch = Mathf.Pow(Mathf.Sin(beat * Mathf.PI), 0.6f);
                    float a = strand / (float)strands * Mathf.PI * 2f + (along - 0.5f) * 0.45f;
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a))
                                   * Mathf.Lerp(0.85f, r, punch),
                        Size = 0.44f - punch * 0.12f,
                        Color = index % 5 == 0 ? def.Accent : Color.Lerp(def.Cool, def.Hot, punch),
                        Alpha = 0.35f + punch * 0.55f,
                    };
                }

                case EnvelopeKind.Ink:
                {
                    // Клякса расплывается наружу неровными долями, внутри редкие
                    // тёплые искры. Тёмное на тёмном — поэтому крупнее и плотнее.
                    float spread = Mathf.Repeat(slot + age * 0.35f, 1f);
                    float a = seed * 6.2832f;
                    float lobe = 0.7f + 0.4f * Mathf.Sin(a * 3f + age);
                    bool spark = index % 9 == 0;
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a))
                                   * Mathf.Lerp(0.7f, r * lobe, spread),
                        Size = spark ? 0.18f : Mathf.Lerp(0.34f, 0.78f, spread),
                        Color = spark ? def.Accent : Color.Lerp(def.Hot, def.Cool, spread),
                        Alpha = spark ? 0.95f : Fade(spread) * 0.85f,
                    };
                }

                case EnvelopeKind.Swarm:
                {
                    // Рой: каждая рыбка идёт по своей орбите рывками.
                    float own = seed * 6.2832f;
                    float a = angle * 1.3f + own;
                    float dart = Mathf.Pow(Mathf.Abs(Mathf.Sin(age * 2.2f + own)), 3f);
                    float rr = r * (0.62f + 0.38f * Mathf.Sin(own + age * 0.7f))
                               * (0.9f + dart * 0.22f);
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr,
                        Size = 0.22f + 0.12f * seed,
                        Color = index % 4 == 0 ? def.Accent : Color.Lerp(def.Hot, def.Cool, seed),
                        Alpha = 0.75f + 0.2f * dart,
                    };
                }

                case EnvelopeKind.Shell:
                {
                    // Каменные пластины: крупные, редкие, медленно вращаются
                    // замкнутым кольцом.
                    float a = (slot + t) * Mathf.PI * 2f;
                    float bob = Mathf.Sin(age * 1.1f + slot * 6.2832f) * 0.22f;
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * 0.9f + bob),
                        Size = 0.52f + 0.18f * Mathf.Sin(slot * 13f),
                        Color = index % 4 == 0 ? def.Accent : Color.Lerp(def.Hot, def.Cool, slot),
                        Alpha = 0.8f,
                    };
                }

                case EnvelopeKind.Fin:
                {
                    // Пять плавников: широкие серпы, обходящие стража по очереди.
                    float sweep = Mathf.Repeat(age * 1.2f + strand / (float)strands, 1f);
                    float a = (strand / (float)strands + sweep * 0.45f) * Mathf.PI * 2f
                              + along * 0.55f;
                    // Радиус почти постоянный: когда он гулял от 0.62 до 1.0,
                    // концы серпов сходились и получался цветок, а не плавники.
                    float rr = r * (0.82f + 0.18f * Mathf.Sin(along * Mathf.PI));
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr,
                        Size = Mathf.Sin(along * Mathf.PI) * 0.46f + 0.14f,
                        Color = along > 0.7f ? def.Accent : Color.Lerp(def.Cool, def.Hot, along),
                        Alpha = Fade(sweep) * 0.95f,
                    };
                }

                default: // Wing
                {
                    // Семь широких тёмных волн расходятся сеткой — приплюснуто,
                    // как бьёт крыло ската.
                    float wave = Mathf.Repeat(age * 0.5f + strand / (float)strands, 1f);
                    float a = (along * 0.85f + strand * 0.08f) * Mathf.PI * 2f;
                    float rr = Mathf.Lerp(0.75f, r, wave);
                    return new Sampled
                    {
                        Position = new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.6f),
                        Size = 0.46f - wave * 0.16f,
                        Color = strand % 3 == 0 ? def.Accent : Color.Lerp(def.Hot, def.Cool, wave),
                        Alpha = Fade(wave) * 0.9f,
                    };
                }
            }
        }

        // -------------------------------------------- связь с препятствием

        /// <summary>Одна точка связи: где, какого размера и на цели ли она.</summary>
        public struct LinkDot
        {
            public Vector2 Position;
            public float Size;
            /// <summary>Точка лежит на препятствии, а не на пути к нему.</summary>
            public bool OnTarget;
        }

        /// <summary>
        /// Точка связи по сквозному номеру.
        ///
        /// Сначала идут пряди к цели — по <see cref="LinkTravelSteps"/> точек на
        /// каждую ветвь, — затем общее пятно по препятствию из
        /// <see cref="LinkSpread"/> точек.
        ///
        /// **Почему пятно общее, а не по ветви.** Пламя на камне — это пятно, а
        /// не семь отдельных кисточек. Раньше каждая ветвь растекалась своей
        /// долей, и на крупном скине пятно стоило двадцати одной точки вместо
        /// восьми, выглядя при этом ровно так же.
        ///
        /// **Почему точки не мельчают на далёкой цели.** Шаг между ними растёт
        /// вместе с расстоянием, и размер точки не даёт ей стать меньше шага:
        /// линия остаётся сплошной без единой лишней точки.
        ///
        /// Физики в проекте нет, Collider2D тоже: размер цели приходит из той же
        /// проверки окружностей, которой считается само попадание.
        /// </summary>
        /// <param name="fromD">Центр стража, в D.</param>
        /// <param name="toD">Центр препятствия, в D относительно стража.</param>
        /// <param name="targetRadiusD">Радиус препятствия в D.</param>
        public static LinkDot LinkAt(EnvelopeDef def, int index, Vector2 fromD, Vector2 toD,
                                     float arcStep, float targetRadiusD)
        {
            int strands = Strands(def);
            int steps = LinkTravelSteps(def);
            int travel = strands * steps;

            if (index < travel)
            {
                int strand = index / steps;
                int step = index % steps;
                float k = steps <= 1 ? 1f : step / (float)(steps - 1);

                Vector2 pos = Travel(def, fromD, toD, strand, strands, k, arcStep);

                // Шаг между соседними точками пряди. Точка не имеет права быть
                // мельче его — иначе на далёкой цели связь рассыпается в пунктир.
                float len = Mathf.Max(0.001f, (toD - fromD).magnitude - 0.5f);
                float gap = steps <= 1 ? 0f : len / (steps - 1);

                // Размер не меньше шага — иначе линия рассыпается в пунктир, —
                // но и не больше двух с половиной толщин: на далёкой цели шаг
                // разрастался, и связь превращалась в цепь огромных клякс
                // поперёк экрана. [ТЕСТ]
                float w = LinkWidth(def, k);
                return new LinkDot
                {
                    Position = pos,
                    Size = Mathf.Clamp(gap * 1.15f, w, def.LinkWidthD * 2.5f),
                    OnTarget = false,
                };
            }

            // Пятно по препятствию.
            //
            // Раньше ветви расходились строго по углам strand/strands, и на
            // каждом камне рисовалась одна и та же правильная снежинка — чем
            // дальше от огня и молний, тем лучше. Теперь точки ложатся по диску
            // вразнобой: угол и радиус берутся из устойчивого шума, а корень из
            // доли даёт равномерность по площади, а не сгусток в центре.
            //
            // Для огня и молний пятно живёт: шум идёт по arcStep, то есть точки
            // плавно переползают, а не перескакивают, и не чаще ArcHz форм в секунду.
            int spot = index - travel;
            bool shimmer = def.Link == LinkKind.Bolt || def.Link == LinkKind.Tongue;
            int stride = shimmer ? 7 : 0;

            float spin = StepNoise(spot * 31, arcStep, stride) * 6.2832f;
            float rr = targetRadiusD * Mathf.Sqrt(StepNoise(spot * 31 + 17, arcStep, stride));

            return new LinkDot
            {
                Position = toD + new Vector2(Mathf.Cos(spin), Mathf.Sin(spin)) * rr,
                // Пятно кроет цель целиком, поэтому точки крупные: восемь штук
                // должны закрыть диск, а не рассыпаться по нему крошкой.
                Size = Mathf.Max(def.LinkWidthD, targetRadiusD * 0.75f),
                OnTarget = true,
            };
        }

        /// <summary>Путь от стража до точки касания, свой у каждого вида связи.</summary>
        static Vector2 Travel(EnvelopeDef def, Vector2 from, Vector2 to,
                              int strand, int strands, float k, float arcStep)
        {
            // Прядь выходит не из центра стража, а с его поверхности: иначе
            // половина связи тонет в самом шарике и наружу торчит огрызок.
            Vector2 full = to - from;
            Vector2 dir = full.sqrMagnitude > 1e-6f ? full.normalized : Vector2.right;
            Vector2 start = from + dir * 0.5f;

            Vector2 d = to - start;
            Vector2 n = new Vector2(-d.y, d.x).normalized;
            Vector2 straight = Vector2.Lerp(start, to, k);

            // Каждая ветвь отклоняется от прямой по-своему: без этого все пряди
            // лежали бы одна на другой и читались как одна линия. Веер шире для
            // далёкой цели — на коротком пути разводить нечего.
            float side = strands <= 1 ? 0f : (strand / (float)(strands - 1) - 0.5f) * 2f;
            side *= 0.4f + 0.6f * Mathf.Clamp01(d.magnitude);
            float bow = Mathf.Sin(k * Mathf.PI);

            switch (def.Link)
            {
                case LinkKind.Bolt:
                {
                    // Ломаная: излом идёт вместе с arcStep и между формами
                    // переходит плавно — ни одного скачка за кадр.
                    float j = StepNoise(strand * 53 + (int)(k * 11f), arcStep, 13) - 0.5f;
                    return straight + n * ((side * 0.55f + j * 0.5f) * bow);
                }

                case LinkKind.Reach:
                    return straight + n * (side * 0.7f * bow);

                case LinkKind.Jet:
                    return straight + n * ((side * 0.35f + Mathf.Sin(k * Mathf.PI * 3f) * 0.18f) * bow);

                case LinkKind.Slash:
                    return Vector2.Lerp(start, to, 0.35f + k * 0.65f) + n * (side * 0.3f * bow);

                case LinkKind.Tongue:
                    // Язык: широко у стража, виляет к концу.
                    return straight + n * ((side * 0.45f + Mathf.Sin(k * Mathf.PI * 2f) * 0.22f) * bow * k);

                default: // Thread
                    return straight + n * (side * 0.28f * bow);
            }
        }

        /// <summary>
        /// Толщина пряди на пути к цели, в диаметрах стража. У основания — как
        /// записано в паспорте (не меньше 0.25 D), к концу чуть тоньше.
        /// </summary>
        public static float LinkWidth(EnvelopeDef def, float k)
            => def.LinkWidthD * DetailScale * Mathf.Lerp(1f, 0.6f, k);

        /// <summary>
        /// Шум по номеру формы без скачков (E-05): между Hash соседних номеров —
        /// SmoothStep по дробной части. stride — шаг соли на один номер; при 0
        /// шум от номера не зависит вовсе.
        /// </summary>
        static float StepNoise(int salt, float step, int stride)
        {
            int i = Mathf.FloorToInt(step);
            float f = step - i;
            float a = Hash(salt + i * stride);
            float b = Hash(salt + (i + 1) * stride);
            return Mathf.Lerp(a, b, Mathf.SmoothStep(0f, 1f, f));
        }

        /// <summary>Устойчивый шум 0…1. Тот же приём, что у материалов скинов.</summary>
        static float Hash(int v)
        {
            unchecked
            {
                int h = v * 374761393;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }
    }
}
