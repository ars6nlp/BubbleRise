using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Спрайты «Чернильного морока». Вынесены отдельно от общей фабрики: файл боссов
    /// и без того большой, а этот набор используется только одним боем.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>
        /// Клякса на стекле. Три варианта, чтобы залитый экран не выглядел
        /// повторяющимся штампом.
        ///
        /// Форма — окружность, у которой радиус гуляет по углу: сумма нескольких
        /// синусов с разными периодами даёт мягкие языки без острых углов. Плюс
        /// несколько капель-спутников по краям — от них клякса читается как брызг,
        /// а не как круглое пятно.
        /// </summary>
        public static Sprite InkSplat(int variant)
        {
            variant = ((variant % 3) + 3) % 3;
            float seed = 1.7f + variant * 2.3f;

            return Build("ink_splat_" + variant, p =>
            {
                float angle = Mathf.Atan2(p.y, p.x);
                float distance = p.magnitude;

                // Кромка рваная, но не колючая: три гармоники, дальше — только шум формы.
                float edge = 0.74f
                           + 0.10f * Mathf.Sin(angle * 3f + seed)
                           + 0.06f * Mathf.Sin(angle * 5f - seed * 1.7f)
                           + 0.04f * Mathf.Sin(angle * 8f + seed * 0.6f);

                if (distance < edge) return SplatColor(distance / edge);

                // капли-спутники: сидят чуть дальше кромки, каждая под своим углом
                for (int i = 0; i < 5; i++)
                {
                    float a = seed * 1.3f + i * (Mathf.PI * 2f / 5f);
                    float d = 0.80f + 0.13f * Mathf.Sin(seed + i * 2.1f);
                    var drop = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                    float r = 0.10f + 0.05f * Mathf.Cos(seed * 2f + i);
                    float dd = Vector2.Distance(p, drop);
                    if (dd < r) return SplatColor(dd / r);
                }

                return Color.clear;
            }, size: 192);
        }

        /// <summary>
        /// Края кляксы чуть прозрачнее середины: жёсткая кромка выглядела бы
        /// наклейкой, а не жидкостью на стекле.
        /// </summary>
        static Color SplatColor(float t01)
        {
            var c = Palette.InkBody;
            c.a = Mathf.SmoothStep(1f, 0.55f, Mathf.InverseLerp(0.72f, 1f, t01));
            return c;
        }

        /// <summary>
        /// Сплошной блок. Подложка под кляксы: она гарантирует, что между ними не
        /// останется просветов. Восемь пикселей — больше для однотонной заливки не нужно.
        /// </summary>
        public static Sprite SolidBlock() => Build("solid_block", p => Color.white, size: 8);

        /// <summary>Сгусток чернил в полёте: ядро с рваным шлейфом.</summary>
        public static Sprite InkBlob() => Build("ink_blob", p =>
        {
            float angle = Mathf.Atan2(p.y, p.x);
            float edge = 0.66f + 0.09f * Mathf.Sin(angle * 4f + 0.8f) + 0.05f * Mathf.Sin(angle * 7f);
            float d = p.magnitude;

            if (d < edge)
            {
                var c = Palette.InkBody;
                // блик сверху-слева: без него сгусток читается дырой в экране
                if (Vector2.Distance(p, new Vector2(-0.22f, 0.24f)) < 0.17f)
                    c = Color.Lerp(c, Palette.InkGlow, 0.35f);
                c.a = 1f;
                return c;
            }

            // мягкое гало — чернила расходятся в воде
            if (d < edge + 0.22f)
            {
                var c = Palette.InkBody;
                c.a = Mathf.SmoothStep(0.55f, 0f, Mathf.InverseLerp(edge, edge + 0.22f, d));
                return c;
            }
            return Color.clear;
        });

        // ------------------------------------------------------- спрут-морок
        //
        // Основа — прежний морок (до переделки 29.10, коммит 711beb0): мантия,
        // два крупных глаза на её середине, фиолетовая палитра. Теперь это
        // классический спрут: голова-купол, снизу сужается яйцом, и восемь щупалец
        // пучком из-под неё одной линией, веером вниз, концы загнуты наружу.
        // Кромка гладкая, без бугров — не медуза и не клякса. Жуть — в деталях:
        // хмурые складки над глазами, тёмные прожилки и разводы, два рваных шрама,
        // мокрый блик по верхней кромке, глубокие глазницы и тень под глазами,
        // тонкие нити, присоски. Клюв — тёмный хитиновый крюк, без зубов и языка.
        //
        // Тело собрано из частей: мантия, два глаза со зрачками, веко, клюв и
        // восемь щупалец по два звена. Глаза разгораются и следят, зрачки
        // сужаются, щупальца идут волной с запаздыванием.
        //
        // Палитра — оттенок прежней мантии (2E1B4A), но светлее: прежняя давала к
        // фону 1.3:1 и тонула в темноте. Медиана купола над глазами L ≈ 0.145 — к
        // воде обеих локаций 2.8–3.6:1 [ТЕСТ, docs/INK_BOSS_REPORT.md].

        public static readonly Color InkMantleTop  = Palette.Hex("A772E9");   // L 0.261, сумма 2.02
        public static readonly Color InkMantleMid  = Palette.Hex("8957CE");   // L 0.166
        public static readonly Color InkMantleLow  = Palette.Hex("67429E");   // L 0.092
        static readonly Color InkWet      = Palette.Hex("B8A8E8");   // мокрый блик, сумма 2.17
        static readonly Color InkScar     = Palette.Hex("A99CCB");   // шрамы и присоски, сумма 2.09
        static readonly Color InkVein     = Palette.Hex("24123C");   // прожилки
        static readonly Color InkRim      = Palette.Hex("140A24");   // тёмная кромка силуэта
        static readonly Color InkBeakCol  = Palette.Hex("2C2238");   // хитин клюва
        static readonly Color InkDrip     = Palette.Hex("1C1030");

        /// <summary>
        /// Свет глаз: бледная холодная лаванда. Прежний InkGlow (9B6BFF) горел на
        /// тёмной мантии, а на посветлевшей сливался с ней — глаза должны быть
        /// заметно светлее тела. Правый глаз чуть темнее — живой, но не близнец левого.
        /// </summary>
        public static readonly Color[] InkEyeColors = { Palette.Hex("C8B6FF"), Palette.Hex("B9A4F2") };

        static float InkHash(int a, int b)
        {
            int h = a * 73856093 ^ b * 83492791;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        /// <summary>
        /// Голова спрута в долях спрайта: центр (0, 0.16), полуось по ширине 0.6,
        /// сверху 0.48 — высокий купол; снизу 0.36 и сужается яйцом к месту, откуда
        /// растут щупальца (не плоский срез, как у медузы). Половины сходятся
        /// гладко: на «экваторе» у обеих касательная вертикальна. |q| &lt; 1 — внутри.
        /// </summary>
        static Vector2 InkDome(Vector2 p)
        {
            float y = p.y - 0.16f;
            if (y >= 0f) return new Vector2(p.x / 0.6f, y / 0.48f);
            float s = -y / 0.36f;
            return new Vector2(p.x / (0.6f * (1f - 0.3f * Mathf.Min(1f, s * s))), -s);
        }

        /// <summary>Нижняя кромка головы над точкой x (для нитей): поиск по s делением пополам.</summary>
        static float InkDomeBottom(float x)
        {
            float lo = 0f, hi = 1f;
            for (int i = 0; i < 16; i++)
            {
                float s = (lo + hi) * 0.5f;
                float w = 0.6f * (1f - 0.3f * s * s);
                if ((x / w) * (x / w) + s * s > 1f) hi = s; else lo = s;
            }
            return 0.16f - 0.36f * lo;
        }

        /// <summary>
        /// Мантия спрута 320×320: голова-купол с градиентом снизу вверх и тенью по
        /// бокам, тёмная пятнистость кожи, ветвистые прожилки, два рваных шрама,
        /// глубокие глазницы с тенью под глазами и хмурой складкой над ними, мокрый
        /// блик по верхней кромке, тёмная кромка и тонкие нити, свисающие с низа.
        /// </summary>
        public static Sprite InkMantle(bool mask) => BuildRectTwin(MaskKey("ink_mantle", mask), 320, 320, p =>
        {
            Vector2 q = InkDome(p);
            float r = q.magnitude;

            // тонкие нити с нижней кромки: качаются вместе с телом, тянутся вниз
            if (r > 1f)
            {
                if (p.y < 0.1f && p.y > -0.62f)
                {
                    for (int i = 0; i < InkThreadX.Length; i++)
                    {
                        float x0 = InkThreadX[i];
                        float x = x0 + 0.025f * Mathf.Sin(p.y * 11f + i * 1.7f);
                        float top = InkThreadTop[i] + 0.01f;
                        float len = 0.16f + 0.1f * InkHash(i, 42);
                        if (p.y < top && p.y > top - len && Mathf.Abs(p.x - x) < 0.006f)
                            return InkMantleLow.WithAlpha(0.55f * Mathf.InverseLerp(top - len, top - len * 0.4f, p.y));
                    }
                }
                return Color.clear;
            }

            // объём: снизу вверх от тёмного к светлому, по бокам — тень купола
            float t = Mathf.InverseLerp(-1f, 1f, q.y);
            Color c = Color.Lerp(InkMantleLow, InkMantleMid, Mathf.SmoothStep(0f, 1f, t * 1.5f));
            c = Color.Lerp(c, InkMantleTop, Mathf.SmoothStep(0.55f, 1f, t) * 0.7f);
            c = Color.Lerp(c, InkMantleLow * 0.85f, Mathf.SmoothStep(0.6f, 1f, Mathf.Abs(q.x)) * 0.35f);
            c.a = 1f;

            // кожа: крупные тёмные разводы, без россыпи точек
            float m = Mathf.PerlinNoise(p.x * 3.2f + 5.3f, p.y * 3.2f + 1.7f);
            c = Blend(c, InkVein.WithAlpha(0.15f * Mathf.SmoothStep(0.5f, 0.78f, m)));

            // прожилки: тонкие ветвистые линии уровня шума, у глаз и на блике — реже
            float n1 = Mathf.Abs(Mathf.PerlinNoise(p.x * 4.6f + 3.1f, p.y * 4.6f + 7.3f) - 0.5f);
            float n2 = Mathf.Abs(Mathf.PerlinNoise(p.x * 9.5f + 11f, p.y * 9.5f + 2f) - 0.5f);
            float veinMask = Mathf.SmoothStep(0.1f, 0.5f, r) * Mathf.Lerp(1f, 0.55f, Mathf.SmoothStep(0.6f, 0.95f, q.y));
            if (n1 < 0.016f) c = Blend(c, InkVein.WithAlpha(0.55f * veinMask * (1f - n1 / 0.016f)));
            else if (n2 < 0.01f) c = Blend(c, InkVein.WithAlpha(0.35f * veinMask * (1f - n2 / 0.01f)));

            // шрамы: рваные бледные полосы с тёмными краями, к концам тоньше
            c = InkScarAt(c, p, InkScarA);
            c = InkScarAt(c, p, InkScarB);

            // глазницы, тень под глазами и хмурая складка над ними
            for (int i = 0; i < InkEyeSpots.Length; i++)
            {
                var e = InkEyeSpots[i];
                float side = Mathf.Sign(e.x);    // наружу от оси
                Vector2 de = new Vector2((p.x - e.x) / 1.3f, p.y - e.y);    // глаз шире, чем выше
                float d = de.magnitude;
                float socket = e.z * 1.45f;
                if (d < socket) c = Blend(c, InkRim.WithAlpha(0.62f * Mathf.Pow(1f - d / socket, 0.7f)));
                Vector2 under = new Vector2((p.x - e.x) / 1.7f, (p.y - (e.y - e.z * 0.95f)) / 0.55f);
                float du = under.magnitude;
                if (du < e.z * 1.1f && p.y < e.y) c = Blend(c, InkVein.WithAlpha(0.5f * (1f - du / (e.z * 1.1f))));
                // складка: от внешнего верхнего угла к внутреннему — ниже у оси
                Vector2 b0 = new Vector2(e.x + side * e.z * 1.25f, e.y + e.z * 1.45f);
                Vector2 b1 = new Vector2(e.x - side * e.z * 1.15f, e.y + e.z * 0.85f);
                float db = DistToSegment(p, b0, b1);
                if (db < 0.018f) c = Blend(c, InkVein.WithAlpha(0.55f * (1f - db / 0.018f)));
                else if (db < 0.034f && p.y > Mathf.Lerp(b0.y, b1.y, Mathf.InverseLerp(b0.x, b1.x, p.x)))
                    c = Blend(c, InkMantleTop.WithAlpha(0.3f * (1f - (db - 0.018f) / 0.016f)));
            }

            // мокрый блик: полоса вдоль верхней кромки, ярче сверху слева, и два
            // вытянутых пятна влаги
            float rim = 1f - r;
            if (q.y > 0.2f && rim < 0.12f && rim > 0.03f)
            {
                float k = Mathf.InverseLerp(0.2f, 0.9f, q.y) * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(0.5f - q.x * 0.5f));
                c = Blend(c, InkWet.WithAlpha(0.5f * k * Mathf.SmoothStep(0.12f, 0.06f, rim)));
            }
            float w1 = new Vector2((p.x + 0.24f) / 0.09f, (p.y - 0.52f) / 0.026f).magnitude;
            float w2 = new Vector2((p.x + 0.08f) / 0.035f, (p.y - 0.58f) / 0.016f).magnitude;
            if (w1 < 1f) c = Blend(c, InkWet.WithAlpha(0.45f * (1f - w1 * w1)));
            if (w2 < 1f) c = Blend(c, InkWet.WithAlpha(0.4f * (1f - w2 * w2)));
            if (rim < 0.03f) c = Blend(c, InkRim.WithAlpha(0.85f));
            return c;
        }, 2);

        /// <summary>Нити с низа головы: x и верх (по кромке купола), считаются один раз.</summary>
        static readonly float[] InkThreadX = { -0.46f, -0.3f, -0.17f, 0.15f, 0.29f, 0.45f };
        static readonly float[] InkThreadTop = System.Array.ConvertAll(InkThreadX, InkDomeBottom);

        /// <summary>Шрамы ломаными: длинный сверху справа, короткий слева.</summary>
        static readonly Vector2[] InkScarA =
            { new Vector2(0.08f, 0.56f), new Vector2(0.17f, 0.49f), new Vector2(0.24f, 0.46f), new Vector2(0.37f, 0.34f) };
        static readonly Vector2[] InkScarB =
            { new Vector2(-0.5f, 0.24f), new Vector2(-0.43f, 0.33f), new Vector2(-0.33f, 0.37f) };

        static Color InkScarAt(Color c, Vector2 p, Vector2[] line)
        {
            float best = 9f, along = 0f, total = 0f;
            for (int i = 0; i + 1 < line.Length; i++) total += Vector2.Distance(line[i], line[i + 1]);
            float acc = 0f;
            for (int i = 0; i + 1 < line.Length; i++)
            {
                Vector2 a = line[i], b = line[i + 1];
                float len = Vector2.Distance(a, b);
                float u = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (len * len));
                float d = Vector2.Distance(p, a + (b - a) * u);
                if (d < best) { best = d; along = (acc + u * len) / total; }
                acc += len;
            }
            float w = 0.011f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(along)) + 0.002f;
            best += 0.003f * Mathf.Sin(along * 47f);    // рваный край
            if (best < w) return Blend(c, InkScar.WithAlpha(0.55f));
            if (best < w + 0.009f) return Blend(c, InkVein.WithAlpha(0.45f));
            return c;
        }

        /// <summary>
        /// Глаза на мантии: x, y — центр в долях спрайта мантии, z — радиус. Места
        /// прежнего морока (±0.26 от оси, на линии середины купола). Левый глаз
        /// крупнее, правый чуть меньше и прищурен веком — лёгкая асимметрия.
        /// </summary>
        public static readonly Vector3[] InkEyeSpots =
        {
            new Vector3(-0.25f, 0.10f, 0.115f),
            new Vector3(0.25f, 0.11f, 0.10f),
        };

        /// <summary>
        /// Радужка: светлый диск с тёмным ободом и волокнами. Тинтуется цветом
        /// глаза — так глаз разгорается без подмены спрайта. Влажный блик —
        /// крупный сверху-слева и мелкий снизу-справа. Вытягивается в эллипс
        /// масштабом (глаза шире, чем выше, как у прежнего морока).
        /// </summary>
        public static Sprite InkEyeIris(int kind) => Build("ink_iris_" + kind, p =>
        {
            float d = p.magnitude;
            if (d > 0.94f) return Color.clear;
            // тонкий тёмный обод с мягким краем — не чёрное кольцо мультяшного глаза
            if (d > 0.84f) return new Color(0.22f, 0.18f, 0.3f, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.88f, 0.94f, d)));
            float k = d / 0.84f;
            Color c = Color.Lerp(new Color(1f, 1f, 1f, 1f), new Color(0.5f, 0.5f, 0.55f, 1f), k * k);
            float fiber = 0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(p.y, p.x) * 16f);
            c *= Mathf.Lerp(1f, 0.85f, fiber * k);
            c.a = 1f;
            // влажный блик: не светом, а бликом на мокрой поверхности
            float h1 = Vector2.Distance(p, new Vector2(-0.32f, 0.36f));
            float h2 = Vector2.Distance(p, new Vector2(0.3f, -0.3f));
            if (h1 < 0.16f) c = Color.Lerp(c, new Color(0.9f, 0.9f, 0.92f, 1f), 0.55f * (1f - h1 / 0.16f) + 0.15f);
            if (h2 < 0.06f) c = Color.Lerp(c, new Color(0.85f, 0.85f, 0.9f, 1f), 0.35f);
            return c;
        }, size: 96);

        /// <summary>
        /// Зрачок: горизонтальная щель, сужающаяся к концам, как у головоногих.
        /// Не прямоугольник: края скруглены, середина шире. Прищур — масштабом по Y.
        /// </summary>
        public static Sprite InkPupil() => Build("ink_pupil", p =>
        {
            float u = p.x / 0.86f;
            if (Mathf.Abs(u) >= 1f) return Color.clear;
            float half = 0.3f * Mathf.Pow(1f - u * u, 0.8f);
            float e = Mathf.Abs(p.y) / Mathf.Max(0.001f, half);
            if (e > 1f) return Color.clear;
            return new Color(0.02f, 0.01f, 0.03f, Mathf.SmoothStep(1f, 0.55f, Mathf.InverseLerp(0.7f, 1f, e)));
        }, size: 64);

        /// <summary>
        /// Тяжёлое веко правого глаза: кромка ниже у оси тела (слева в спрайте) —
        /// прищур исподлобья, а не сонный. Тёмная полоса по кромке.
        /// </summary>
        public static Sprite InkLid() => Build("ink_lid", p =>
        {
            float d = p.magnitude;
            float edge = 0.02f + 0.26f * p.x;
            if (d > 0.95f || p.y < edge) return Color.clear;
            Color c = Color.Lerp(InkMantleMid, InkMantleLow, Mathf.InverseLerp(0.95f, 0.1f, p.y));
            if (p.y < edge + 0.1f) c = InkRim;
            c.a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.9f, 0.95f, d));
            return c;
        }, size: 96);

        /// <summary>
        /// Клюв головоногого спереди: верхняя створка — загнутый вниз крюк, по бокам
        /// из-под неё видна нижняя, между ними — тёмная щель. Хитин, без зубов,
        /// без языка, без подтёков.
        /// </summary>
        public static Sprite InkBeak() => Build("ink_beak", p =>
        {
            // верхняя створка: шире сверху, сходится к острию внизу, верх скруглён
            float up = Mathf.InverseLerp(-0.78f, 0.55f, p.y);
            float upperW = 0.6f * Mathf.Pow(up, 0.75f);
            bool upper = p.y > -0.78f && p.y < 0.55f - 0.22f * (p.x / 0.6f) * (p.x / 0.6f) && Mathf.Abs(p.x) < upperW;
            // нижняя створка: короче и шире у основания, видна по бокам
            float lowW = 0.78f * Mathf.Pow(Mathf.InverseLerp(-0.5f, 0.25f, p.y), 0.6f);
            bool lower = p.y > -0.5f && p.y < 0.25f && Mathf.Abs(p.x) < lowW;
            if (!upper && !lower) return Color.clear;

            Color c;
            if (upper)
            {
                // гребень по середине светлее, к краям темнее — хитин, а не пятно
                float ridge = Mathf.Abs(p.x) / Mathf.Max(0.02f, upperW);
                c = Color.Lerp(InkBeakCol * 1.6f, InkBeakCol * 0.75f, ridge);
                if (ridge > 0.84f) c = InkRim;                 // щель вдоль кромки створки
            }
            else
            {
                c = Color.Lerp(InkBeakCol * 0.9f, InkRim, Mathf.InverseLerp(0.25f, -0.5f, p.y));
                if (Mathf.Abs(p.x) > lowW - 0.06f) c = InkRim;
            }
            c.a = 1f;
            return c;
        }, size: 128);

        /// <summary>
        /// Звено щупальца, сверху корень, снизу конец. Присоски вдоль внутренней
        /// стороны, тонкие нити по краю, к кончику — темнее.
        /// </summary>
        public static Sprite InkArm(int segment) => Build("ink_arm_" + segment, p =>
        {
            float t = Mathf.InverseLerp(0.95f, -0.95f, p.y);     // 0 у корня, 1 у конца
            float w = Mathf.Lerp(segment == 0 ? 0.46f : 0.30f, segment == 0 ? 0.30f : 0.08f, t);
            float x = p.x + 0.06f * Mathf.Sin(p.y * 3f);
            bool body = Mathf.Abs(x) < w && Mathf.Abs(p.y) < 0.95f;

            // нити: тонкая линия по краю, торчит дальше тела
            float fil = Mathf.Abs(Mathf.Abs(x) - (w + 0.08f + 0.04f * Mathf.Sin(p.y * 9f)));
            if (!body)
            {
                if (fil < 0.012f && Mathf.Abs(p.y) < 0.9f) return InkMantleLow.WithAlpha(0.5f);
                return Color.clear;
            }

            Color c = Color.Lerp(InkMantleMid, InkMantleLow, Mathf.Lerp(segment * 0.5f, segment * 0.5f + 0.5f, t));
            c = Color.Lerp(c, c * 0.65f, Mathf.InverseLerp(0f, w, Mathf.Abs(x)));   // объём
            c.a = 1f;
            // присоски вдоль внутренней стороны: светлое кольцо, тёмная середина
            float sy = Mathf.Repeat(p.y * 5f, 1f) - 0.5f;
            var sucker = new Vector2((x + w * 0.45f) / Mathf.Max(0.02f, w * 0.35f), sy * 2.2f);
            float sd = sucker.magnitude;
            if (sd < 1f) c = Color.Lerp(c, sd > 0.6f ? InkScar : InkMantleLow * 0.7f, 0.65f).WithAlpha(1f);
            // тёмная кромка
            if (Mathf.Abs(x) > w - 0.03f) c = Blend(c, InkRim.WithAlpha(0.8f));
            return c;
        }, size: 128);

        /// <summary>
        /// Мягкий светлый контур поверх чернил: пузырёк и страж видны в темноте.
        /// Кольцо с размытыми краями, без заливки. Цвет — сумма каналов 2.27.
        /// </summary>
        public static Sprite InkOutline() => Build("ink_outline", p =>
        {
            float d = p.magnitude;
            float ring = Mathf.Abs(d - 0.8f);
            if (ring > 0.16f) return Color.clear;
            return new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, ring / 0.16f));
        }, size: 128);

        /// <summary>Чернильный шар: тёмная сфера с фиолетовым ободом — видна на тёмной воде.</summary>
        public static Sprite InkBall() => Build("ink_ball", p =>
        {
            float d = p.magnitude;
            if (d > 0.95f) return Color.clear;
            if (d > 0.78f)
            {
                float a = 1f - Mathf.InverseLerp(0.78f, 0.95f, d);
                return Palette.InkGlow.WithAlpha(0.65f * a);
            }
            Color c = Color.Lerp(Palette.Hex("2A1A48"), InkDrip, d / 0.78f);
            if (Vector2.Distance(p, new Vector2(-0.28f, 0.3f)) < 0.16f) c = Color.Lerp(c, InkMantleTop, 0.5f);
            c.a = 1f;
            return c;
        });
    }
}
