using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Мини-боссы — скины, которые падают с боссов: уменьшенная живая копия самого
    /// босса, а не значок в круге.
    ///
    /// **Откуда рисунок.** Формы, пропорции и цвета взяты из кода боссов
    /// (SpriteFactoryJelly, SpriteFactoryBosses, SpriteFactoryInk, SpriteFactorySwarm,
    /// SpriteFactoryManta) и упрощены под размер игрока: сначала два-три крупных
    /// признака (купол и глаз медузы, клешни краба, крылья ската), затем средние
    /// детали, мелочь — только та, что читается на 90 точках. Цвета — тона самого
    /// босса, приглушённые; светится только то, что светится у босса, и тускло.
    ///
    /// **Пара.** Пузырёк — голова или тело босса (то, что бережём), страж — его
    /// оружие или характерная часть (клешня, клык, жало, плавник). Палитра у пары
    /// общая, формы разные.
    ///
    /// **Ориентация.** Ни пузырёк, ни страж при движении не поворачиваются
    /// (BubbleController, GuardianOrb), поэтому все головы, носы и острия смотрят
    /// вверх — по ходу всплытия; рыбы боком стоят по общей позе <see cref="MiniBossPose"/>.
    /// Вверх ногами ничего не бывает.
    ///
    /// **Размер.** Рисунок целиком внутри круга игрока (радиус 1 в координатах
    /// холста) с запасом под изгиб сетки — хитбокс прежний, круг.
    ///
    /// Текстура 256², рисуется один раз и кэшируется, билинейная, без mip-уровней.
    /// </summary>
    public static partial class SpriteFactory
    {
        public const int MiniSize = 256;

        /// <summary>Размер рисунка звезды коллекции (SpriteFactorySkins).</summary>
        public const int SkinSize = 256;

        /// <summary>Неподвижный кадр мини-босса. Живой рисует сетка <see cref="MiniBossMotion"/> по его текстуре.</summary>
        public static Sprite MiniBoss(MiniBossKey key)
        {
            string k = "mini_" + key;
            if (Cache.TryGetValue(k, out var cached) && cached != null) return cached;
            var px = MiniBossPixels(key);
            if (!key.Guardian) UploadBadge(key.Boss, px);
            return Upload(k, px, MiniSize);
        }

        /// <summary>
        /// Значок босса для карточки: силуэт его мини-пузырька одним цветом (цвет даёт
        /// Image). Берётся из тех же пикселей, что и сам мини-босс, — отдельно не рисуется.
        /// </summary>
        public static Sprite MiniBossBadge(BossKind boss)
        {
            string k = "mini_badge_" + boss;
            if (Cache.TryGetValue(k, out var cached) && cached != null) return cached;
            MiniBoss(new MiniBossKey(boss, false));
            return Cache.TryGetValue(k, out cached) ? cached : null;
        }

        const int BadgeSize = 64;

        static void UploadBadge(BossKind boss, Color[] px)
        {
            int f = MiniSize / BadgeSize;
            var badge = new Color[BadgeSize * BadgeSize];
            for (int y = 0; y < BadgeSize; y++)
                for (int x = 0; x < BadgeSize; x++)
                {
                    float a = 0f;
                    for (int dy = 0; dy < f; dy++)
                        for (int dx = 0; dx < f; dx++)
                            a += px[(y * f + dy) * MiniSize + x * f + dx].a;
                    badge[y * BadgeSize + x] = new Color(1f, 1f, 1f, a / (f * f));
                }
            Upload("mini_badge_" + boss, badge, BadgeSize);
        }

        /// <summary>
        /// Начинки у трофеев больше нет — мини-босс рисуется целиком. Функция осталась,
        /// потому что её зовёт прогрев в SpriteFactory.cs, а тот — исходник запечённых
        /// атласов мира: правка в нём сбросила бы запечённое.
        /// </summary>
        public static Sprite BossCore(BossKind boss) => null;

        /// <summary>
        /// Рамка боссовой карточки: тонкий скруглённый контур без заливки. Белая — цвет
        /// (приглушённый тон босса) даёт Image. Режется девятью частями, как фон карточки.
        /// </summary>
        public static Sprite BossCardFrame()
        {
            const int s = 64;
            const float r = 0.28f, thickness = 0.045f;
            int borderPx = Mathf.RoundToInt(r * s * 0.5f) + 2;
            return Build("boss_card_frame2", p =>
            {
                float qx = Mathf.Abs(p.x) - (1f - r);
                float qy = Mathf.Abs(p.y) - (1f - r);
                float d = qx > 0f && qy > 0f ? Mathf.Sqrt(qx * qx + qy * qy) : Mathf.Max(qx, qy);
                float sd = d - r;
                return sd > 0f || sd < -thickness ? Color.clear : Color.white;
            }, new Vector4(borderPx, borderPx, borderPx, borderPx), s);
        }

        /// <summary>Пиксели мини-босса — для текстуры, листа-снимка и проверок.</summary>
        public static Color[] MiniBossPixels(MiniBossKey key)
        {
            var c = new MiniBossCanvas(MiniSize);
            switch (key.Boss)
            {
                case BossKind.Jelly: if (key.Guardian) JellyBall(c); else JellyBell(c); break;
                case BossKind.Serpent: if (key.Guardian) SerpentFang(c); else SerpentCoil(c); break;
                case BossKind.Octopus: if (key.Guardian) OctopusArm(c); else OctopusHead(c); break;
                case BossKind.Swordfish: if (key.Guardian) SwordTip(c); else SwordFish(c); break;
                case BossKind.Crab: if (key.Guardian) CrabPincer(c); else CrabBody(c); break;
                case BossKind.Ink: if (key.Guardian) InkDrop(c); else InkMantle(c); break;
                case BossKind.Piranha: if (key.Guardian) PiranhaJaws(c); else PiranhaFish(c); break;
                case BossKind.Turtle: if (key.Guardian) TurtleBoulder(c); else TurtleTop(c); break;
                case BossKind.Shark: if (key.Guardian) SharkFin(c); else SharkFish(c); break;
                case BossKind.Manta: if (key.Guardian) MantaSting(c); else MantaRay(c); break;
            }

            if (MiniOutlined(key)) c.OutlineUnder(BossSkinTable.DarkOutline, 0.024f);
            c.Limit(MiniLight);
            c.Bleed(2);
            return c.Pixels;
        }

        /// <summary>Потолок суммы каналов у мини-боссов: без белого, с запасом к общему 2.3.</summary>
        public const float MiniLight = 2.2f;

        /// <summary>
        /// Кому нужна светлая обводка: тёмным скинам, у которых край сливается с
        /// водой (акула, скат, чернила, тёмная спина рыбы-меч).
        /// </summary>
        public static bool MiniOutlined(MiniBossKey key)
        {
            switch (key.Boss)
            {
                case BossKind.Shark:
                case BossKind.Manta:
                case BossKind.Swordfish:
                    return true;
                case BossKind.Ink:
                    return key.Guardian;
                default:
                    return false;
            }
        }

        // ------------------------------------------------------------ общее

        static Color Hx(string hex) => Palette.Hex(hex);
        static Rect R(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(x0, y0, x1, y1);
        static float Sat01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        static float Sm(float a, float b, float x) => Sdf.Smooth(a, b, x);

        /// <summary>Свет сверху-слева, как у боссов.</summary>
        static readonly Vector2 MiniLightDir = new Vector2(-0.45f, 0.89f);

        /// <summary>
        /// Объём тела: тёмный → основной → светлый по нормали <paramref name="n"/>
        /// (смещение от центра в долях радиуса), и затенённый край у контура.
        /// </summary>
        static Color Vol(Color dark, Color mid, Color lit, Vector2 n, float d, float edge = 0.05f, float litK = 0.55f)
        {
            float l = Sat01(0.5f + 0.5f * Vector2.Dot(n.sqrMagnitude > 1f ? n.normalized : n, MiniLightDir));
            Color c = l > 0.5f ? Color.Lerp(mid, lit, (l - 0.5f) * 2f * litK) : Color.Lerp(dark, mid, 0.35f + l * 1.3f);
            return Color.Lerp(c, dark, Sm(-edge, 0f, d) * 0.4f);
        }

        static Vector2 Q(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return a * (u * u) + b * (2f * u * t) + c * (t * t);
        }

        /// <summary>Квадратичная кривая Безье, <paramref name="n"/> отрезков.</summary>
        static Vector2[] Bez(Vector2 a, Vector2 b, Vector2 c, int n)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++) pts[i] = Q(a, b, c, (float)i / n);
            return pts;
        }

        static Vector2[] Bez3(Vector2 a, Vector2 b, Vector2 c, Vector2 d, int n)
        {
            var pts = new Vector2[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, u = 1f - t;
                pts[i] = a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t);
            }
            return pts;
        }

        /// <summary>
        /// Ближайшая точка ломаной: расстояние, доля длины 0…1 и сторона (+1 — слева
        /// по ходу, −1 — справа). Нужна, чтобы красить щупальце с изнанки иначе.
        /// </summary>
        static float Along(Vector2 p, Vector2[] pts, out float t, out float side)
        {
            float best = float.MaxValue; t = 0f; side = 1f;
            float total = 0f;
            for (int i = 0; i < pts.Length - 1; i++) total += (pts[i + 1] - pts[i]).magnitude;
            float acc = 0f;
            for (int i = 0; i < pts.Length - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1], ba = b - a;
                float len = ba.magnitude;
                float h = Mathf.Clamp01(Vector2.Dot(p - a, ba) / Mathf.Max(1e-8f, len * len));
                Vector2 q = a + ba * h;
                float d = (p - q).magnitude;
                if (d < best)
                {
                    best = d;
                    t = (acc + len * h) / Mathf.Max(1e-6f, total);
                    float cross = ba.x * (p.y - a.y) - ba.y * (p.x - a.x);
                    side = cross >= 0f ? 1f : -1f;
                }
                acc += len;
            }
            return best;
        }

        /// <summary>Тонкая ломаная линия — трещина, жилка, шрам.</summary>
        static void Line(MiniBossCanvas c, Vector2[] pts, float width, Color color, float soft = 0f)
        {
            c.Fill(Sdf.Around(pts, width + soft + 0.02f), p =>
            {
                float d = float.MaxValue;
                for (int i = 0; i < pts.Length - 1; i++) d = Mathf.Min(d, Sdf.Segment(p, pts[i], pts[i + 1]));
                return d - width;
            }, color, soft);
        }

        static void Dot(MiniBossCanvas c, Vector2 at, float r, Color color, float soft = 0f)
            => c.Fill(Sdf.Box(at.x, at.y, r + soft + 0.01f, r + soft + 0.01f), p => Sdf.Circle(p, at, r), color, soft);

        /// <summary>Глаз со щелью зрачка: глазница, радужка с волокнами, щель, тусклый блик.</summary>
        static void SlitEye(MiniBossCanvas c, Vector2 at, float rx, float ry, Color socket, Color iris, Color irisDark,
                            Color pupil, bool vertical, float lid = 0f, Color lidColor = default, float tilt = 0f)
        {
            c.Fill(Sdf.Box(at.x, at.y, rx * 1.3f + 0.02f, ry * 1.3f + 0.02f),
                   p => Sdf.Ellipse(p, at, rx * 1.25f, ry * 1.25f, tilt), socket);
            c.Fill(Sdf.Box(at.x, at.y, rx + 0.02f, ry + 0.02f), p => Sdf.Ellipse(p, at, rx, ry, tilt), (p, d) =>
            {
                var q = Sdf.Rot(p - at, -tilt);
                float rad = new Vector2(q.x / rx, q.y / ry).magnitude;
                float fib = 0.5f + 0.5f * Mathf.Sin(Mathf.Atan2(q.y, q.x) * 18f);
                Color col = Color.Lerp(iris, irisDark, Sm(0.2f, 1f, rad) * 0.7f);
                return Color.Lerp(col, irisDark, fib * 0.18f);
            });
            float px = vertical ? rx * 0.24f : rx * 0.75f, py = vertical ? ry * 0.8f : ry * 0.24f;
            c.Fill(Sdf.Box(at.x, at.y, rx, ry), p => Sdf.Ellipse(p, at, px, py, tilt), pupil);
            if (lid > 0f)
            {
                // веко: верхняя часть глаза закрыта — прищур, как у босса
                c.Fill(Sdf.Box(at.x, at.y + ry * 0.5f, rx * 1.35f, ry * 0.9f), p =>
                {
                    var q = Sdf.Rot(p - at, -tilt);
                    float cut = ry * (1f - 2f * lid) + q.x * q.x * 0.6f;
                    return Mathf.Max(Sdf.Ellipse(p, at, rx * 1.28f, ry * 1.28f, tilt), cut - q.y);
                }, lidColor);
            }
        }

        // ============================================================ МЕДУЗА
        // Палитра живой медузы (SpriteFactoryJelly): купол 463A72→120C26, кромка 8E7FC0,
        // жилки 5B4A94, органы 0C0718, руки 2A1F46, радужка C8E86A, бирюза 3FD8C8.

        static readonly Color JBody = Palette.Hex("463A72"), JDeep = Palette.Hex("120C26"),
                              JEdge = Palette.Hex("8E7FC0"), JVein = Palette.Hex("5B4A94"),
                              JOrgan = Palette.Hex("0C0718"), JArm = Palette.Hex("4A3D78"),
                              JArmDark = Palette.Hex("2A1F46"), JBio = Palette.Hex("2E9C94"),
                              JIris = Palette.Hex("A8BC60"), JIris2 = Palette.Hex("5E8A42");

        static void JellyArmStrip(MiniBossCanvas c, Vector2[] pts, float r0, float r1)
        {
            c.Fill(Sdf.Around(pts, r0 + 0.02f), p => Sdf.Chain(p, pts, r0, r1), (p, d) =>
            {
                float w = Sm(-0.03f, 0f, d);
                return Color.Lerp(Color.Lerp(JArm, JEdge, 0.62f), JArmDark, w * 0.3f);
            });
            // присоски вдоль руки — светлые точки кромки
            for (int i = 1; i < pts.Length - 1; i++)
                Dot(c, pts[i], Mathf.Lerp(r0, r1, (float)i / (pts.Length - 1)) * 0.38f, Color.Lerp(JEdge, JVein, 0.5f).WithAlpha(0.7f));
        }

        static void JellyThread(MiniBossCanvas c, Vector2[] pts)
        {
            c.Fill(Sdf.Around(pts, 0.03f), p => Sdf.Chain(p, pts, 0.014f, 0.007f), (p, d) =>
            {
                Along(p, pts, out float t, out _);
                return Color.Lerp(JVein, JBio, Sm(0.7f, 1f, t) * 0.8f).WithAlpha(0.75f);
            });
        }

        /// <summary>Купол медузы: жилки, светлая кромка, один большой глаз-щель, руки и бахрома.</summary>
        static void JellyBell(MiniBossCanvas c)
        {
            var bc = new Vector2(0f, 0.24f);
            const float rx = 0.68f, ry = 0.54f;

            // бахрома и руки — под куполом
            float[] threadX = { -0.5f, -0.27f, -0.09f, 0.09f, 0.27f, 0.5f };
            for (int i = 0; i < threadX.Length; i++)
            {
                float x = threadX[i];
                JellyThread(c, Bez3(new Vector2(x, 0.02f), new Vector2(x + 0.06f, -0.25f),
                                    new Vector2(x - 0.07f, -0.45f), new Vector2(x * 1.05f + 0.03f, -0.66f), 8));
            }
            float[] armX = { -0.36f, -0.18f, 0f, 0.18f, 0.36f };
            for (int i = 0; i < armX.Length; i++)
            {
                float x = armX[i];
                float len = Mathf.Abs(x) > 0.3f ? 0.72f : 0.82f;
                var pts = Bez3(new Vector2(x, 0.06f), new Vector2(x + 0.07f, -0.2f),
                               new Vector2(x - 0.06f, -0.5f), new Vector2(x * 1.12f + 0.02f, 0.06f - len), 6);
                JellyArmStrip(c, pts, 0.075f, 0.026f);
            }

            // купол
            Func<Vector2, float> bell = p =>
            {
                float skirt = 0.04f - 0.3f * p.x * p.x + Mathf.Abs(Mathf.Sin(p.x * 10f)) * 0.035f;
                return Mathf.Max(Sdf.Ellipse(p, bc, rx, ry), skirt - p.y);
            };
            c.Fill(R(-0.72f, -0.08f, 0.72f, 0.8f), bell, (p, d) =>
            {
                var q = new Vector2((p.x - bc.x) / rx, (p.y - bc.y) / ry);
                float rad = q.magnitude;
                Color col = Color.Lerp(JBody, JDeep, Sm(0.35f, 1.05f, rad) * 0.7f);
                float v1 = Mathf.Sin(6.5f * p.y + Mathf.Sin(5f * p.x) * 2.2f);
                float v2 = Mathf.Sin(6f * p.x - Mathf.Sin(4.2f * p.y) * 1.8f);
                float vein = Mathf.Max(1f - Mathf.Abs(v1) * 7f, 1f - Mathf.Abs(v2) * 8f);
                col = Color.Lerp(col, JVein, Sat01(vein) * 0.5f);
                float sheen = Sat01(Vector2.Dot(q, MiniLightDir));
                col = Color.Lerp(col, JEdge, sheen * sheen * 0.22f);
                // светлая кромка купола — как у босса (кромка d > 0.9 к цвету Edge)
                col = Color.Lerp(col, JEdge, Sm(-0.085f, -0.012f, d) * 0.72f);
                return col.WithAlpha(0.97f);
            });

            // органы и пузырьки газа
            c.Fill(R(-0.5f, 0.14f, -0.18f, 0.42f), p => Sdf.Ellipse(p, new Vector2(-0.33f, 0.27f), 0.11f, 0.075f, 20f), JOrgan.WithAlpha(0.5f), 0.01f);
            c.Fill(R(0.18f, 0.12f, 0.5f, 0.4f), p => Sdf.Ellipse(p, new Vector2(0.34f, 0.25f), 0.1f, 0.07f, -25f), JOrgan.WithAlpha(0.5f), 0.01f);
            Vector2[] gas = { new Vector2(-0.43f, 0.45f), new Vector2(0.44f, 0.42f), new Vector2(-0.18f, 0.66f), new Vector2(0.24f, 0.63f) };
            foreach (var g in gas) Dot(c, g, 0.024f, JEdge.WithAlpha(0.55f));

            // тусклые бирюзовые разряды — тонкие дуги только внутри купола
            Line(c, new[] { new Vector2(-0.53f, 0.18f), new Vector2(-0.45f, 0.31f), new Vector2(-0.52f, 0.41f), new Vector2(-0.4f, 0.53f) },
                 0.008f, JBio.WithAlpha(0.5f), 0.004f);
            Line(c, new[] { new Vector2(0.5f, 0.14f), new Vector2(0.43f, 0.26f), new Vector2(0.52f, 0.36f), new Vector2(0.43f, 0.49f) },
                 0.008f, JBio.WithAlpha(0.45f), 0.004f);

            // глаз — главный признак босса: вертикальная щель на жёлто-зелёной радужке
            SlitEye(c, new Vector2(0f, 0.32f), 0.17f, 0.155f, JOrgan, JIris, JIris2, JOrgan, vertical: true);
            Dot(c, new Vector2(-0.06f, 0.39f), 0.032f, JEdge.WithAlpha(0.5f), 0.01f);
        }

        /// <summary>«Шаровая молния»: тёмный шар-орган с тусклыми дугами внутри, обвитый руками медузы.</summary>
        static void JellyBall(MiniBossCanvas c)
        {
            var o = new Vector2(0f, 0.06f);

            // бахрома снизу
            float[] tx = { -0.14f, 0f, 0.14f };
            for (int i = 0; i < tx.Length; i++)
                JellyThread(c, Bez3(new Vector2(tx[i], -0.2f), new Vector2(tx[i] + 0.06f, -0.42f),
                                    new Vector2(tx[i] - 0.06f, -0.6f), new Vector2(tx[i] * 1.4f, -0.8f), 8));

            // четыре руки закручены вокруг шара
            for (int k = 0; k < 4; k++)
            {
                float a0 = Mathf.PI * 0.25f + k * Mathf.PI * 0.5f;
                var pts = new Vector2[7];
                for (int j = 0; j < pts.Length; j++)
                {
                    float r = 0.3f + 0.083f * j;
                    float a = a0 + j * 0.3f;
                    pts[j] = o + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                }
                JellyArmStrip(c, pts, 0.09f, 0.03f);
            }

            // шар: мембрана купола, светлая кромка
            const float R0 = 0.36f;
            c.Fill(Sdf.Box(o.x, o.y, R0 + 0.02f, R0 + 0.02f), p => Sdf.Circle(p, o, R0), (p, d) =>
            {
                var q = (p - o) / R0;
                Color col = Color.Lerp(JBody, JDeep, Sm(0.2f, 1.05f, q.magnitude) * 0.75f);
                float v1 = Mathf.Sin(9f * p.y + Mathf.Sin(7f * p.x) * 2f);
                col = Color.Lerp(col, JVein, Sat01(1f - Mathf.Abs(v1) * 7f) * 0.45f);
                col = Color.Lerp(col, JEdge, Sat01(Vector2.Dot(q, MiniLightDir)) * 0.2f);
                return Color.Lerp(col, JEdge, Sm(-0.07f, -0.01f, d) * 0.7f);
            });

            // тусклое ядро и три тонкие дуги — молния, запертая в шаре
            c.Fill(Sdf.Box(o.x, o.y, 0.26f, 0.26f), p => Sdf.Circle(p, o, 0.13f), JBio.WithAlpha(0.22f), 0.08f);
            Line(c, new[] { o, o + new Vector2(0.08f, 0.07f), o + new Vector2(0.06f, 0.16f), o + new Vector2(0.15f, 0.24f) }, 0.009f, JBio.WithAlpha(0.55f), 0.004f);
            Line(c, new[] { o, o + new Vector2(-0.1f, 0.02f), o + new Vector2(-0.16f, -0.08f), o + new Vector2(-0.26f, -0.1f) }, 0.009f, JBio.WithAlpha(0.5f), 0.004f);
            Line(c, new[] { o, o + new Vector2(0.03f, -0.1f), o + new Vector2(0.13f, -0.14f), o + new Vector2(0.17f, -0.25f) }, 0.008f, JBio.WithAlpha(0.45f), 0.004f);
            Dot(c, o + new Vector2(-0.14f, 0.16f), 0.028f, JEdge.WithAlpha(0.5f));
        }

        // ============================================================ ЗМЕЙ
        // SpriteFactoryBosses: тело 26404E, брюхо 4E7A88, гребень FF6B5B, глаз FF4A2E,
        // раны A8382A, кость 948A76, зубы C9C0B4, пасть 140A12, слизь 9DB39A.

        static readonly Color SBody = Palette.Hex("4C7686"), SBelly = Palette.Hex("6E9AA6"),
                              SSpine = Palette.Hex("B85A4C"), SEye = Palette.Hex("B0442E"),
                              SWound = Palette.Hex("8E3428"), SBone = Palette.Hex("948A76"),
                              SBoneLight = Palette.Hex("BDB3A0"), STooth = Palette.Hex("B8B0A4"),
                              SMaw = Palette.Hex("140A12"), SSlime = Palette.Hex("9DB39A"),
                              SDark = Palette.Hex("162630");

        /// <summary>Чешуя: смещённые ряды ячеек, 0 в центре ячейки и 1 на шве.</summary>
        static float Scale(Vector2 p, float density)
        {
            float y = p.y * density;
            float row = Mathf.Floor(y);
            float x = p.x * density + (((int)row & 1) == 0 ? 0f : 0.5f);
            float fu = x - Mathf.Floor(x) - 0.5f, fv = y - row - 0.5f;
            return Sat01(Mathf.Sqrt(fu * fu * 1.4f + fv * fv * 2.2f) * 1.45f);
        }

        /// <summary>Тёмная трещина с тусклым красным свечением внутри.</summary>
        static void Crack(MiniBossCanvas c, Vector2[] pts)
        {
            Line(c, pts, 0.016f, SWound.WithAlpha(0.35f), 0.02f);
            Line(c, pts, 0.009f, Palette.Hex("2A0E0C"));
            Line(c, pts, 0.004f, SWound.WithAlpha(0.8f));
        }

        static void SerpentSegment(MiniBossCanvas c, Vector2 at, float r, Vector2 back)
        {
            // костяной шип на спине и плавник на брюхе
            Vector2 side = new Vector2(-back.y, back.x);
            Vector2 a = at + back * r * 0.55f + side * r * 0.42f, b = at + back * r * 0.55f - side * r * 0.42f;
            Vector2 tip = at + back * (r + 0.13f) + side * 0.03f;
            c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), (p, d) =>
                Color.Lerp(SSpine, Palette.Hex("6E2A22"), Sm(-0.03f, 0f, d) * 0.5f));
            Vector2 f0 = at - back * r * 0.5f + side * r * 0.4f, f1 = at - back * r * 0.4f - side * r * 0.5f;
            Vector2 ftip = at - back * (r + 0.07f) - side * 0.08f;
            c.Fill(Sdf.Around(new[] { f0, f1, ftip }, 0.02f), p => Sdf.Triangle(p, f0, f1, ftip), SDark);

            c.Fill(Sdf.Box(at.x, at.y, r + 0.02f, r + 0.02f), p => Sdf.Circle(p, at, r), (p, d) =>
            {
                var n = (p - at) / r;
                float belly = Sm(-0.3f, 0.9f, -Vector2.Dot(n, back));
                Color col = Color.Lerp(SBody, SBelly, belly);
                col = Vol(SDark, col, Color.Lerp(col, SBelly, 0.6f), n, d, 0.05f, 0.45f);
                float s = Scale(p, 15f);
                col = Color.Lerp(col, SDark, s * s * 0.22f);
                // кромка чешуи ловит свет — тон брюха, не свечение
                return Color.Lerp(col, Color.Lerp(SBelly, Palette.Hex("6E9AA4"), 0.5f), Sm(-0.045f, -0.005f, d) * 0.6f);
            });
        }

        /// <summary>Змейка: голова с гребнем и зубами, тело изгибом, шипы, трещины с тусклым красным.</summary>
        static void SerpentCoil(MiniBossCanvas c)
        {
            // хвост: плавник и костяное жало
            Vector2 t0 = new Vector2(0.22f, -0.58f), t1 = new Vector2(0.38f, -0.8f);
            c.Fill(Sdf.Around(t0, t1, 0.14f), p => Sdf.Taper(p, t0, t1, 0.11f, 0.025f), SBody);
            Vector2 fa = new Vector2(0.27f, -0.66f), fb = new Vector2(0.18f, -0.7f), ftip = new Vector2(0.12f, -0.88f);
            c.Fill(Sdf.Around(new[] { fa, fb, ftip }, 0.02f), p => Sdf.Triangle(p, fa, fb, ftip), SSpine);

            Vector2[] seg = { new Vector2(0.22f, -0.56f), new Vector2(0.0f, -0.42f), new Vector2(-0.22f, -0.2f),
                              new Vector2(-0.2f, 0.06f), new Vector2(0.02f, 0.25f) };
            float[] rad = { 0.145f, 0.175f, 0.195f, 0.205f, 0.21f };
            for (int i = 0; i < seg.Length; i++)
            {
                Vector2 prev = i + 1 < seg.Length ? seg[i + 1] : MiniBossPose.SerpentHead.O;
                Vector2 next = i > 0 ? seg[i - 1] : t1;
                Vector2 dir = (prev - next).normalized;
                // «спина» — наружная сторона изгиба
                Vector2 back = new Vector2(-dir.y, dir.x);
                if (Vector2.Dot(back, seg[i] - new Vector2(0f, seg[i].y * 0.4f)) < 0f && i != 2) back = -back;
                SerpentSegment(c, seg[i], rad[i], back);
            }
            Crack(c, new[] { new Vector2(-0.28f, -0.13f), new Vector2(-0.21f, -0.21f), new Vector2(-0.16f, -0.17f), new Vector2(-0.1f, -0.26f) });
            Crack(c, new[] { new Vector2(-0.06f, 0.28f), new Vector2(0.02f, 0.21f), new Vector2(0.08f, 0.27f) });

            // голова смотрит вверх (MiniBossPose.SerpentHead), крупнее тела — главный признак
            var head = MiniBossPose.SerpentHead;
            Vector2 hc = head.O;
            float ang = head.Deg, hs = head.S;
            Func<Vector2, Vector2> L = p => Sdf.Rot(p - hc, -ang) / hs;
            // гребень: три кораллово-костяных шипа
            for (int i = 0; i < 3; i++)
            {
                float x0 = -0.14f + 0.1f * i;
                Vector2 a = hc + Sdf.Rot(new Vector2(x0 - 0.045f, 0.1f) * hs, ang), b = hc + Sdf.Rot(new Vector2(x0 + 0.045f, 0.1f) * hs, ang);
                Vector2 tip = hc + Sdf.Rot(new Vector2(x0 - 0.07f, 0.27f - 0.04f * i) * hs, ang);
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), (p, d) =>
                    Color.Lerp(SSpine, SBone, 0.15f));
            }
            Func<Vector2, float> skull = p =>
            {
                var q = L(p);
                return hs * Sdf.SMin(Sdf.Ellipse(q, new Vector2(-0.05f, 0f), 0.2f, 0.155f), Sdf.Taper(q, Vector2.zero, new Vector2(0.24f, -0.01f), 0.13f, 0.065f), 0.05f);
            };
            c.Fill(Sdf.Box(hc.x, hc.y, 0.42f, 0.42f), skull, (p, d) =>
            {
                var q = L(p);
                Color col = Color.Lerp(SBody, SBelly, Sm(0.02f, -0.12f, q.y));
                col = Vol(SDark, col, Color.Lerp(col, SBelly, 0.5f), q / 0.22f, d, 0.04f, 0.4f);
                float s = Scale(q, 17f);
                col = Color.Lerp(col, SDark, s * s * 0.25f);
                return Color.Lerp(col, Color.Lerp(SBelly, Palette.Hex("6E9AA4"), 0.5f), Sm(-0.04f, -0.005f, d) * 0.5f);
            });
            // приоткрытая пасть с зубами
            Vector2 m0 = hc + Sdf.Rot(new Vector2(0.0f, -0.045f) * hs, ang), m1 = hc + Sdf.Rot(new Vector2(0.28f, -0.02f) * hs, ang),
                    m2 = hc + Sdf.Rot(new Vector2(0.26f, -0.1f) * hs, ang);
            c.Fill(Sdf.Around(new[] { m0, m1, m2 }, 0.02f), p => Sdf.Triangle(p, m0, m1, m2), SMaw);
            for (int i = 0; i < 3; i++)
            {
                float x = 0.08f + 0.06f * i;
                Vector2 a = hc + Sdf.Rot(new Vector2(x - 0.02f, -0.035f) * hs, ang), b = hc + Sdf.Rot(new Vector2(x + 0.02f, -0.03f) * hs, ang);
                Vector2 tip = hc + Sdf.Rot(new Vector2(x, -0.085f) * hs, ang);
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.01f), p => Sdf.Triangle(p, a, b, tip), STooth);
            }
            Line(c, new[] { hc + Sdf.Rot(new Vector2(0.14f, -0.05f) * hs, ang), hc + Sdf.Rot(new Vector2(0.15f, -0.1f) * hs, ang) }, 0.005f, SSlime.WithAlpha(0.5f));
            // глаз: красная радужка со щелью — тусклая
            Vector2 eye = hc + Sdf.Rot(new Vector2(0.03f, 0.055f) * hs, ang);
            SlitEye(c, eye, 0.055f, 0.055f, SMaw, SEye, Palette.Hex("6E2418"), SMaw, vertical: true);
            Dot(c, hc + Sdf.Rot(new Vector2(0.2f, 0.035f) * hs, ang), 0.014f, SDark);
        }

        /// <summary>«Клык змея»: изогнутый костяной клык в куске челюсти, с тусклой трещиной.</summary>
        static void SerpentFang(MiniBossCanvas c)
        {
            // кусок челюсти: чешуйчатая кожа, дёсна, мелкие зубы, шипы по бокам
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 a = new Vector2(s * 0.4f, -0.56f), b = new Vector2(s * 0.42f, -0.7f), tip = new Vector2(s * 0.62f, -0.6f);
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), SSpine);
            }
            var jaw = new Vector2(0f, -0.62f);
            c.Fill(R(-0.52f, -0.86f, 0.52f, -0.38f), p => Sdf.Ellipse(p, jaw, 0.48f, 0.22f), (p, d) =>
            {
                var n = (p - jaw) / 0.48f;
                Color col = Color.Lerp(SBelly, SBody, Sm(-0.7f, -0.45f, p.y));
                col = Vol(SDark, col, Color.Lerp(col, SBelly, 0.5f), n, d, 0.05f, 0.45f);
                float s = Scale(p, 14f);
                return Color.Lerp(col, SDark, s * s * 0.35f);
            });
            c.Fill(R(-0.34f, -0.54f, 0.34f, -0.38f), p => Sdf.Ellipse(p, new Vector2(0f, -0.46f), 0.3f, 0.06f), SMaw);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 a = new Vector2(s * 0.2f, -0.45f), b = new Vector2(s * 0.3f, -0.46f), tip = new Vector2(s * 0.25f, -0.33f);
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), STooth);
            }

            // сам клык — по кривой, от толстого основания к острию; остриё смотрит вверх
            var pts = Bez(new Vector2(-0.06f, -0.48f), new Vector2(0.16f, 0.12f), new Vector2(0f, 0.84f), 10);
            c.Fill(R(-0.32f, -0.6f, 0.3f, 0.86f), p => Sdf.Chain(p, pts, 0.2f, 0.012f), (p, d) =>
            {
                Along(p, pts, out float t, out float side);
                Color col = Color.Lerp(SBone, SBoneLight, side > 0f ? 0.75f : 0.25f);
                col = Color.Lerp(col, Palette.Hex("6A6252"), Sm(-0.035f, 0f, d) * 0.45f);
                return Color.Lerp(col, SBoneLight, Sm(0.5f, 1f, t) * 0.2f);
            });
            // жёлоб для яда вдоль клыка и трещина у основания
            var groove = Bez(new Vector2(-0.03f, -0.36f), new Vector2(0.12f, 0.12f), new Vector2(0.01f, 0.66f), 8);
            Line(c, groove, 0.008f, Palette.Hex("6A6252").WithAlpha(0.7f));
            Crack(c, new[] { new Vector2(-0.18f, -0.28f), new Vector2(-0.1f, -0.22f), new Vector2(-0.13f, -0.14f), new Vector2(-0.05f, -0.08f) });
            Line(c, new[] { new Vector2(0.06f, -0.44f), new Vector2(0.09f, -0.52f) }, 0.005f, SSlime.WithAlpha(0.5f));
        }

        // ============================================================ СПРУТ
        // SpriteFactoryBosses: тело 5B2E63, низ 38193F, присоски E8A9C8, глаза FFD166,
        // клыки E8DCD0, бровь (0.16, 0.06, 0.18).

        static readonly Color OBody = Palette.Hex("844E8E"), ODark = Palette.Hex("54305E"),
                              OLight = Palette.Hex("B086B8"), OSucker = Palette.Hex("C88AA8"),
                              OUnder = Palette.Hex("B48AA4"), OEye = Palette.Hex("D2AE58"),
                              OEyeDark = Palette.Hex("8A6A2A"), OFang = Palette.Hex("C8BCB0"),
                              OBrow = new Color(0.16f, 0.06f, 0.18f), OPupil = Palette.Hex("1A0A14");

        static void OctopusTentacle(MiniBossCanvas c, Vector2[] pts, float r0, float r1, bool suckers)
        {
            c.Fill(Sdf.Around(pts, r0 + 0.02f), p => Sdf.Chain(p, pts, r0, r1), (p, d) =>
            {
                Along(p, pts, out float t, out float side);
                Color col = Color.Lerp(OBody, ODark, Sm(0f, 1f, t) * 0.2f);
                col = side < 0f ? Color.Lerp(col, OUnder, 0.45f) : Color.Lerp(col, OLight, 0.3f);
                return Color.Lerp(col, ODark, Sm(-0.03f, 0f, d) * 0.25f);
            });
            if (!suckers) return;
            for (int i = 1; i < pts.Length - 1; i++)
            {
                Vector2 dir = (pts[i + 1] - pts[i - 1]).normalized;
                Vector2 right = new Vector2(dir.y, -dir.x);
                float w = Mathf.Lerp(r0, r1, (float)i / (pts.Length - 1));
                Vector2 at = pts[i] + right * w * 0.45f;
                Dot(c, at, w * 0.36f, OSucker.WithAlpha(0.85f));
                Dot(c, at, w * 0.16f, ODark.WithAlpha(0.8f));
            }
        }

        /// <summary>Спрутик: круглая голова с бородавками, жёлтые сонные глаза под тяжёлыми бровями, клыки, короткие руки.</summary>
        static void OctopusHead(MiniBossCanvas c)
        {
            float[] ax = { -0.32f, -0.19f, -0.065f, 0.065f, 0.19f, 0.32f };
            for (int i = 0; i < ax.Length; i++)
            {
                float x = ax[i], s = Mathf.Sign(x);
                var pts = Bez3(new Vector2(x, -0.16f), new Vector2(x * 1.2f, -0.46f),
                               new Vector2(x * 1.5f + s * 0.02f, -0.74f), new Vector2(x * 1.5f + s * 0.2f, -0.6f), 7);
                OctopusTentacle(c, pts, 0.1f, 0.035f, true);
            }

            var hc = new Vector2(0f, 0.22f);
            Func<Vector2, float> dome = p => Sdf.SMin(Sdf.Ellipse(p, hc, 0.56f, 0.54f), Sdf.Ellipse(p, new Vector2(0f, -0.1f), 0.38f, 0.17f), 0.12f);
            c.Fill(R(-0.6f, -0.3f, 0.6f, 0.8f), dome, (p, d) =>
            {
                var n = (p - hc) / 0.56f;
                Color col = Color.Lerp(OBody, ODark, Sm(0.25f, -0.3f, p.y));
                col = Vol(ODark, col, OLight, n, d, 0.05f, 0.6f);
                // продольные гребни
                float ridge = Mathf.Abs(Mathf.Abs(p.x) - 0.3f * Mathf.Sqrt(Mathf.Max(0f, 1f - n.y * n.y)));
                return Color.Lerp(col, ODark, Sat01(1f - ridge / 0.02f) * 0.35f);
            });
            // бородавки на макушке: светлая сердцевина в тёмном кольце
            for (int i = 0; i < 6; i++)
            {
                float a = i * 2.4f;
                var at = new Vector2(Mathf.Cos(a) * 0.36f, 0.5f + Mathf.Sin(a) * 0.17f);
                Dot(c, at, 0.043f, ODark.WithAlpha(0.8f));
                Dot(c, at, 0.026f, OLight);
            }
            // огоньки по бокам — тусклые точки цвета присосок, как у босса
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 3; i++)
                {
                    float y = 0.48f - 0.15f * i;
                    float e = (y - hc.y) / 0.54f;
                    float w = 0.56f * Mathf.Sqrt(Mathf.Max(0f, 1f - e * e));
                    Dot(c, new Vector2(s * (w - 0.075f), y), 0.024f, OSucker.WithAlpha(0.75f));
                }

            // глаза: жёлтые, зрачок-полоска, тяжёлое веко сверху — сонная злость босса
            for (int s = -1; s <= 1; s += 2)
            {
                var at = new Vector2(s * 0.22f, 0.18f);
                SlitEye(c, at, 0.135f, 0.125f, ODark, OEye, OEyeDark, OPupil, vertical: false, lid: 0.36f, lidColor: Color.Lerp(ODark, OBody, 0.4f));
                Vector2 b0 = new Vector2(s * 0.07f, 0.35f), b1 = new Vector2(s * 0.37f, 0.29f);
                c.Fill(Sdf.Around(b0, b1, 0.06f), p => Sdf.Capsule(p, b0, b1, 0.035f), OBrow);
            }
            // рот с двумя клыками
            c.Fill(R(-0.12f, -0.12f, 0.12f, 0.02f), p => Sdf.Ellipse(p, new Vector2(0f, -0.04f), 0.09f, 0.045f), OPupil);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 a = new Vector2(s * 0.03f, -0.02f), b = new Vector2(s * 0.08f, -0.03f), tip = new Vector2(s * 0.06f, -0.12f);
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), OFang);
            }
        }

        /// <summary>«Присоска»: одна рука спрута, закрученная спиралью вверху, присоски по изнанке.</summary>
        static void OctopusArm(MiniBossCanvas c)
        {
            var sc = new Vector2(0.1f, 0.4f);
            var pts = new System.Collections.Generic.List<Vector2>
            {
                new Vector2(0.06f, -0.74f), new Vector2(-0.04f, -0.52f), new Vector2(-0.14f, -0.26f), new Vector2(-0.2f, 0.02f),
            };
            for (int i = 0; i <= 12; i++)
            {
                float k = i / 12f;
                float a = Mathf.Lerp(200f, -170f, k) * Mathf.Deg2Rad;
                float r = Mathf.Lerp(0.32f, 0.07f, k);
                pts.Add(sc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            var arr = pts.ToArray();
            OctopusTentacle(c, arr, 0.155f, 0.035f, true);
            // бородавки по наружной стороне основания
            Dot(c, new Vector2(-0.16f, -0.42f), 0.03f, OLight.WithAlpha(0.8f));
            Dot(c, new Vector2(-0.26f, -0.1f), 0.026f, OLight.WithAlpha(0.8f));
        }
    }
}
