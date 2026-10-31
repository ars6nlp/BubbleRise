using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Графика системы наград: сундук, колесо, медали, подарок, искра.
    ///
    /// Рисуется тем же способом, что и всё остальное в игре, — функцией от точки,
    /// без единого файла с картинкой. Это не упрямство: вся игра выглядит цельно
    /// именно потому, что любая новая вещь собирается из той же палитры и той же
    /// меры шума, что и препятствия. Подарочная коробка из стороннего набора
    /// иконок выдала бы себя мгновенно.
    ///
    /// Сундук нарочно не «мультяшный ящик с золотом», а просоленный ящик с отлива:
    /// медные полосы, патина, мокрое дерево. Так он оказывается из того же мира, что
    /// и монета, у которой выломан край обода.
    /// </summary>
    public static partial class SpriteFactory
    {
        static readonly Color ChestWood = Palette.Hex("5A3A22");
        static readonly Color ChestWoodDark = Palette.Hex("32200F");
        static readonly Color ChestBand = Palette.Hex("C9922E");
        static readonly Color ChestBandDark = Palette.Hex("7A5516");
        static readonly Color ChestPatina = Palette.Hex("4FBFA8");

        /// <summary>
        /// Сундук. Закрытый — обычный ящик, открытый светится изнутри.
        ///
        /// Два спрайта, а не поворот крышки кодом: крышка на петлях в плоском 2D
        /// без скелета разъезжается с корпусом, а подмена кадра честнее и видна
        /// лучше.
        /// </summary>
        public static Sprite Chest(bool open)
        {
            return BuildRect(open ? "chest_open" : "chest", 128, 112, p =>
            {
                // p.x ∈ [-1..1], p.y сжат по пропорции (см. BuildRect).
                float x = p.x;
                float y = p.y;

                // Корпус: от -0.85 до 0.1 по высоте.
                bool body = Mathf.Abs(x) <= 0.78f && y >= -0.78f && y <= 0.1f;

                // Крышка: полукруглая. У открытого отведена назад и вверх.
                float lidCy = open ? 0.46f : 0.12f;
                float lidTilt = open ? 0.38f : 0f;
                Vector2 lp = new Vector2((x - lidTilt) * 1.0f, (y - lidCy) * 1.9f);
                bool lid = lp.magnitude <= 0.78f && (open ? lp.y >= -0.55f : lp.y >= -0.05f);

                if (!body && !lid)
                {
                    // Свет из щели у открытого: тёплое пятно над корпусом.
                    if (open)
                    {
                        float glow = Vector2.Distance(new Vector2(x, (y - 0.08f) * 1.4f), Vector2.zero);
                        // Свет из щели приглушён вдвое: сундук открывают в меню, на
                        // тёмном фоне, и прежние 0.75 читались как вспышка.
                        if (glow < 0.95f)
                            return new Color(Palette.Gold.r, Palette.Gold.g, Palette.Gold.b,
                                             Mathf.SmoothStep(0.55f, 0f, glow) * 0.38f);
                    }
                    return Color.clear;
                }

                // Дерево: вертикальные доски со швами.
                Color c = Color.Lerp(ChestWoodDark, ChestWood, Mathf.InverseLerp(-0.8f, 0.3f, y));
                float plank = Mathf.Repeat((x + 1f) * 2.6f, 1f);
                if (plank < 0.08f || plank > 0.92f) c = Color.Lerp(c, ChestWoodDark, 0.7f);

                // Мокрый блик по верхней кромке — ящик только что из воды.
                if (lid && lp.y > 0.35f) c = Color.Lerp(c, Palette.Foam, 0.18f);

                // Медные полосы: две по корпусу, одна по крышке.
                bool bandH = body && (Mathf.Abs(y + 0.62f) < 0.09f || Mathf.Abs(y + 0.18f) < 0.07f);
                bool bandV = Mathf.Abs(Mathf.Abs(x) - 0.52f) < 0.07f;
                if (bandH || bandV || (lid && Mathf.Abs(lp.y - 0.42f) < 0.12f))
                {
                    c = Color.Lerp(ChestBandDark, ChestBand, Mathf.InverseLerp(-0.6f, 0.6f, y - x * 0.3f));

                    // Патина разводами — тем же приёмом, что на монете.
                    float stain = Mathf.Sin(x * 6.1f + y * 3.7f) * Mathf.Cos(y * 5.3f - x * 2.9f);
                    if (stain > 0.3f) c = Color.Lerp(c, ChestPatina, (stain - 0.3f) * 1.2f);
                }

                // Замок по центру корпуса: по нему сундук узнаётся в размер иконки.
                if (body && Mathf.Abs(x) < 0.16f && y > -0.34f && y < 0.06f)
                {
                    c = Color.Lerp(ChestBandDark, ChestBand, 0.7f);
                    if (Vector2.Distance(new Vector2(x, y + 0.14f), Vector2.zero) < 0.07f)
                        c = ChestWoodDark;
                }

                // Тёмный контур: без него сундук сливается с фоном меню.
                float edge = Mathf.Max(Mathf.Abs(x) - 0.72f, -(y + 0.72f));
                if (body && edge > 0f) c = Color.Lerp(c, ChestWoodDark, 0.8f);

                return c;
            });
        }

        /// <summary>
        /// Лицо колеса на шесть секторов.
        ///
        /// Сектора чередуются двумя оттенками, а не раскрашены каждый в свой цвет:
        /// значение сектора игрок читает по числу внутри, и шесть разных цветов
        /// здесь только мешали бы — колесо стало бы рябым.
        /// </summary>
        public static Sprite WheelFace()
        {
            const int sectors = 6;
            Color rimOuter = Palette.Hex("1A4A58");
            Color rimInner = Palette.Hex("0D2436");
            // Разница между секторами заметная: при прежних 13596A и 0E3F4D они
            // отличались на полтона, и колесо читалось как сплошной диск.
            Color light = Palette.Hex("1C7A8E");
            Color dark = Palette.Hex("0A2B38");

            return Build("wheel_face", p =>
            {
                float r = p.magnitude;
                if (r > 1f) return Color.clear;

                // Обод с насечкой — по нему видно, что колесо вращается.
                if (r > 0.86f)
                {
                    float notch = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * sectors * 3f / Mathf.PI, 1f);
                    Color rim = Color.Lerp(rimInner, rimOuter, notch < 0.5f ? 1f : 0.45f);
                    float fade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.96f, 1f, r));
                    return new Color(rim.r, rim.g, rim.b, fade);
                }

                // Сектор: номер по углу.
                float angle = Mathf.Atan2(p.y, p.x) + Mathf.PI;          // 0…2π
                int index = (int)(angle / (Mathf.PI * 2f) * sectors) % sectors;
                Color c = (index & 1) == 0 ? light : dark;

                // Разделители между секторами.
                float within = Mathf.Repeat(angle / (Mathf.PI * 2f) * sectors, 1f);
                if (within < 0.02f || within > 0.98f) c = Color.Lerp(c, Palette.Aqua, 0.5f);

                // Ступица: центр не должен выглядеть как дырка.
                if (r < 0.2f) c = Color.Lerp(Palette.Deep2, Palette.Aqua, Mathf.InverseLerp(0.2f, 0f, r) * 0.35f);
                if (Mathf.Abs(r - 0.2f) < 0.02f) c = Color.Lerp(c, Palette.Aqua, 0.6f);

                return c;
            });
        }

        /// <summary>Указатель колеса: клин, смотрящий вниз.</summary>
        public static Sprite WheelPointer()
        {
            return BuildRect("wheel_pointer", 48, 64, p =>
            {
                // Клин: ширина убывает к низу.
                float halfWidth = Mathf.Lerp(0.62f, 0f, Mathf.InverseLerp(0.7f, -0.8f, p.y));
                if (p.y > 0.7f || p.y < -0.8f || Mathf.Abs(p.x) > halfWidth) return Color.clear;

                Color c = Color.Lerp(Palette.Gold, Palette.Coral, Mathf.InverseLerp(0.7f, -0.8f, p.y) * 0.6f);
                if (Mathf.Abs(p.x) > halfWidth - 0.12f) c = Color.Lerp(c, Palette.Deep, 0.45f);
                return c;
            });
        }

        /// <summary>
        /// Медаль достижения. Ступень — цветом и числом лучей, а не только цветом:
        /// бронза и золото на тёмном фоне различимы хуже, чем кажется.
        /// </summary>
        public static Sprite Medal(int tier)
        {
            Color face, edge;
            int rays;
            switch (tier)
            {
                case 3: face = Palette.Hex("FFD166"); edge = Palette.Hex("A8761C"); rays = 12; break;
                case 2: face = Palette.Hex("D7E3EA"); edge = Palette.Hex("7C8F9C"); rays = 8; break;
                default: face = Palette.Hex("C9922E"); edge = Palette.Hex("6E4A12"); rays = 6; break;
            }

            return Build("medal" + tier, p =>
            {
                float r = p.magnitude;
                if (r > 1f) return Color.clear;

                // Лучи вокруг диска — звезда под медалью.
                if (r > 0.68f)
                {
                    float angle = Mathf.Atan2(p.y, p.x);
                    float wave = Mathf.Cos(angle * rays) * 0.5f + 0.5f;
                    float reach = Mathf.Lerp(0.68f, 1f, wave);
                    if (r > reach) return Color.clear;
                    return new Color(edge.r, edge.g, edge.b, 0.85f);
                }

                // Диск с наклонным блеском.
                Color c = Color.Lerp(edge, face, Mathf.InverseLerp(-0.7f, 0.7f, p.y - p.x * 0.4f));
                if (Mathf.Abs(r - 0.62f) < 0.05f) c = Color.Lerp(c, edge, 0.7f);

                // Гравировка: капля — та же вода, что и везде в игре.
                Vector2 d = new Vector2(p.x, (p.y - 0.1f) * 0.8f);
                float drop = d.magnitude - Mathf.Max(0f, d.y) * 0.85f;
                if (drop < 0.24f) c = Color.Lerp(c, edge, 0.6f);
                if (drop < 0.14f) c = Color.Lerp(c, face, 0.55f);

                return c;
            });
        }

        /// <summary>Подарок для клетки календаря: коробка с лентой.</summary>
        public static Sprite GiftBox()
        {
            Color box = Palette.Hex("2A7E8C");
            Color boxDark = Palette.Hex("12414A");
            Color ribbon = Palette.Gold;

            return Build("gift_box", p =>
            {
                bool lid = p.y > 0.34f && p.y < 0.62f && Mathf.Abs(p.x) < 0.82f;
                bool body = p.y >= -0.78f && p.y <= 0.34f && Mathf.Abs(p.x) < 0.72f;

                // Бантик проверяется ДО выхода: петли висят выше крышки, то есть
                // вне коробки, и после выхода по «не крышка и не корпус» до них
                // не доходило вовсе — подарок оставался без банта.
                if (p.y > 0.55f)
                {
                    float loopL = Vector2.Distance(p, new Vector2(-0.19f, 0.73f));
                    float loopR = Vector2.Distance(p, new Vector2(0.19f, 0.73f));
                    if (loopL < 0.17f || loopR < 0.17f) return ribbon;
                    if (loopL < 0.22f || loopR < 0.22f) return Color.Lerp(ribbon, boxDark, 0.45f);
                }

                if (!lid && !body) return Color.clear;

                Color c = Color.Lerp(boxDark, box, Mathf.InverseLerp(-0.8f, 0.6f, p.y));
                if (lid) c = Color.Lerp(c, box, 0.3f);

                // Лента: вертикальная по центру и горизонтальная под крышкой.
                if (Mathf.Abs(p.x) < 0.13f) c = ribbon;
                if (lid && Mathf.Abs(p.y - 0.48f) < 0.06f) c = Color.Lerp(c, ribbon, 0.6f);

                if (Mathf.Abs(p.x) > 0.66f && body) c = Color.Lerp(c, boxDark, 0.6f);
                return c;
            });
        }

        /// <summary>
        /// Искра для всплытия награды: четырёхлучевая звезда. Одна на все награды —
        /// цвет задаётся уже компонентом Image.
        /// </summary>
        public static Sprite Sparkle()
        {
            return Build("sparkle", p =>
            {
                // Четыре луча: произведение расстояний по осям даёт крест с
                // мягкими переходами и без единой ветки.
                float arms = 1f / (1f + Mathf.Abs(p.x * p.y) * 42f);
                float falloff = Mathf.SmoothStep(1f, 0f, p.magnitude);
                float a = arms * falloff;
                if (a < 0.02f) return Color.clear;
                return new Color(1f, 1f, 1f, Mathf.Min(1f, a));
            });
        }

        /// <summary>Галочка: клетка календаря, где награда уже забрана.</summary>
        public static Sprite CheckMark()
        {
            return Build("check", p =>
            {
                // Две линии-отрезка: короткая вниз-вправо, длинная вверх-вправо.
                float shortArm = SegmentDistance(p, new Vector2(-0.55f, 0.05f), new Vector2(-0.12f, -0.45f));
                float longArm = SegmentDistance(p, new Vector2(-0.12f, -0.45f), new Vector2(0.62f, 0.52f));
                float d = Mathf.Min(shortArm, longArm);
                if (d > 0.17f) return Color.clear;
                return new Color(1f, 1f, 1f, Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.11f, 0.17f, d)));
            });
        }

        /// <summary>
        /// Пламя серии входов. Форма — капля, перевёрнутая вершиной вверх, с
        /// неровным краем: ровная капля читается как вода, а не как огонь.
        /// </summary>
        public static Sprite StreakFlame()
        {
            return Build("streak_flame", p =>
            {
                // Лёгкий изгиб языка: без него пламя выглядит симметричной фигурой,
                // а не огнём.
                float wobble = Mathf.Sin(p.y * 4.2f) * 0.07f;
                float x = p.x + wobble;

                // Шар внизу и клин, сходящийся в точку наверху. Раньше тут была
                // «капля с неровным краем» — получалась клякса без острия.
                float ball = new Vector2(x, p.y + 0.3f).magnitude - 0.5f;
                float half = Mathf.Lerp(0f, 0.5f, Mathf.InverseLerp(0.92f, -0.3f, p.y));
                float cone = Mathf.Max(Mathf.Abs(x) - half, -(p.y + 0.3f));
                float shape = Mathf.Min(ball, cone);
                if (shape > 0f) return Color.clear;

                // Жар: снаружи коралловый, внутри золотой, в сердцевине почти белый.
                float depth = Mathf.InverseLerp(0f, -0.42f, shape);
                Color c = Color.Lerp(Palette.Coral, Palette.Gold, depth);
                if (depth > 0.62f) c = Color.Lerp(c, Palette.Foam, (depth - 0.62f) * 1.6f);

                return new Color(c.r, c.g, c.b, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, -0.05f, shape)));
            });
        }

        /// <summary>Расстояние от точки до отрезка — нужно галочке и указателям.</summary>
        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>
        /// Прогреть графику наград. Зовётся из общего <see cref="Prewarm"/>:
        /// одиннадцать текстур на фоне полутора сотен уже греющихся ничего не
        /// стоят, а без прогрева первый заход в награды рисовал бы их прямо в
        /// кадре — ровно та заминка, ради которой прогрев и заведён.
        /// </summary>
        public static void PrewarmRewards()
        {
            Chest(false); Chest(true);
            WheelFace(); WheelPointer();
            Medal(1); Medal(2); Medal(3);
            GiftBox(); Sparkle(); CheckMark(); StreakFlame();
        }
    }
}
