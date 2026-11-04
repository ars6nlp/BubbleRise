using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Коралловые блоки строя, звенья шпилей и шары локаций (шестерёнки и луны).
    ///
    /// **Что было.** Блок — скруглённый квадрат с плоским градиентом и тремя-четырьмя
    /// точками пор, окошко — чёрный квадрат. Звено — овал с пятью порами. Шары —
    /// почти белые (сумма каналов 2.86) с бороздами и бугорками, свет сверху-слева
    /// запечён в рисунок и при случайном повороте шара приходил с любой стороны.
    ///
    /// **Что стало — в меру.** Силуэты и размеры прежние (стыки кладки и шпилей не
    /// тронуты, хитбоксы живут в GameConfig и рисунка не знают). Блок: тёплый
    /// градиент от светлой вершины к тёмному основанию, мягкие рёбра, крупные поры
    /// пятнами, ракушки, налёт водорослей снизу; окошко — с внутренней тенью.
    /// Четыре вида блока и звена: тон, раскладка пор, трещина. Шары — холодный
    /// лавандово-серый металл и камень (сумма каналов ≤ 2.3), обводка чуть темнее
    /// заливки, свет без направления (D-17): шестерёнка — зубцы, заклёпки, ступица с
    /// отверстием, лёгкие царапины; луна — два-три крупных кратера и мягкие «моря».
    /// </summary>
    public static partial class SpriteFactory
    {
        // Палитра. Середина — по ней считается контраст с водой (Tools/check_play.py).
        public static readonly Color CoralBlockTop  = Palette.Hex("DE9068");
        public static readonly Color CoralBlockMid  = Palette.Hex("CB6A42");
        public static readonly Color CoralBlockBase = Palette.Hex("8C432C");
        public static readonly Color CoralLimbMid   = Palette.Hex("C86642");
        /// <summary>Холодный лавандово-серый шаров: сумма каналов 2.29 — без белого, но к светлой корке льда ещё ≥ 2:1.</summary>
        public static readonly Color BlobMetal      = Palette.Hex("C0BFCA");
        static readonly Color CoralShell = Palette.Hex("D8C3A6");
        static readonly Color CoralAlgae = Palette.Hex("7FA878");

        /// <summary>Сколько видов блока и звена.</summary>
        public const int CoralTileVariants = 4;
        public const int CoralLimbVariants = 4;

        const int CoralSize = 160;
        const int LimbSize = 256;
        const int BlobSize = 256;

        // ------------------------------------------------------------ общее

        /// <summary>
        /// Ракушка-балянус сверху: конус из пластин и узкая щель-устье. Круглое
        /// тёмное устье в светлом кольце на размере игры читалось глазом.
        /// </summary>
        static Color Shell(Color c, Vector2 p, Vector2 at, float r)
        {
            Vector2 q = p - at;
            float d = q.magnitude;
            if (d > r) return c;
            float k = d / r;
            Color s = Color.Lerp(CoralShell * 1.05f, CoralShell * 0.72f, k * k);
            // пластины: три тёмных шва от устья к краю
            float ang = Mathf.Atan2(q.y, q.x);
            if (Mathf.Abs(Mathf.Sin(ang * 1.5f)) < 0.12f && k > 0.3f) s = s * 0.82f;
            // устье — щель поперёк, а не дырка
            if (Mathf.Abs(q.y) < r * 0.1f && Mathf.Abs(q.x) < r * 0.38f) s = Color.Lerp(s, CoralBlockBase * 0.6f, 0.75f);
            return Color.Lerp(c, s, 0.78f).WithAlpha(c.a);
        }

        /// <summary>Пора: тёмная середина и светлая нижняя губа — крупно, без точечного шума.</summary>
        static Color Pore(Color c, Vector2 p, Vector2 at, float r)
        {
            float d = Vector2.Distance(p, at);
            if (d > r * 1.35f) return c;
            if (d < r) return Color.Lerp(c, c * 0.52f, 0.75f * (1f - d / r * 0.4f));
            // губа вокруг поры — светлее
            return Color.Lerp(c, CoralBlockTop, 0.22f * (1f - (d - r) / (r * 0.35f)));
        }

        /// <summary>Налёт водорослей по нижней части: пятнами, у самого низа гуще.</summary>
        static Color Algae(Color c, Vector2 p, float from, float to, int seed)
        {
            float k = Mathf.InverseLerp(from, to, p.y);
            if (k <= 0f) return c;
            float n = Mathf.PerlinNoise(p.x * 4.5f + seed * 5.3f, p.y * 4.5f + seed * 1.7f);
            float a = Mathf.SmoothStep(0.5f - 0.25f * k, 0.75f - 0.2f * k, n) * 0.7f * k;
            return Color.Lerp(c, CoralAlgae, a).WithAlpha(c.a);
        }

        /// <summary>Крупные пятна фактуры: ±8 % яркости, не рябь.</summary>
        static Color Mottle(Color c, Vector2 p, float scale, int seed)
        {
            float n = Mathf.PerlinNoise(p.x * scale + seed * 2.3f, p.y * scale + seed * 4.1f) - 0.5f;
            return new Color(c.r * (1f + n * 0.16f), c.g * (1f + n * 0.16f), c.b * (1f + n * 0.16f), c.a);
        }

        // ------------------------------------------------------- блоки строя

        /// <summary>
        /// Блок строя: 0 — пористый, 1 — с отростками сверху, 2 — с окошком, 3 — с
        /// трещиной и ракушками. Форма прежняя: скруглённый квадрат 0.9, радиус
        /// угла 0.2 — стыки кладки остаются теми же.
        /// </summary>
        public static Sprite CoralTile(int variant)
        {
            int v = Mathf.Clamp(variant, 0, CoralTileVariants - 1);
            Color top = Shift(CoralBlockTop, v * 0.006f - 0.006f, -0.02f * v, 0f);
            Color mid = Shift(CoralBlockMid, v * 0.006f - 0.006f, -0.03f * (v % 2), 0.01f * (v % 3));
            Color bottom = Shift(CoralBlockBase, 0f, 0f, 0.01f * v);
            Vector2[] pores = TilePores(v);

            return Build("coral_tile_" + v, p =>
            {
                // отростки сверху — у варианта 1, торчат над блоком
                if (v == 1)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float x0 = i * 0.42f + 0.05f;
                        Vector2 tip = new Vector2(x0 + i * 0.09f, 0.97f - Mathf.Abs(i) * 0.09f);
                        float d = DistToSegment(p, new Vector2(x0, 0.7f), tip);
                        float w = Mathf.Lerp(0.09f, 0.055f, Mathf.InverseLerp(0.7f, tip.y, p.y));
                        if (d < w || Vector2.Distance(p, tip) < 0.075f)
                        {
                            Color polyp = Color.Lerp(mid, Palette.CoralTip, Mathf.InverseLerp(0.7f, tip.y, p.y));
                            if (Vector2.Distance(p, tip) < 0.04f) polyp = Color.Lerp(polyp, CoralShell, 0.5f);
                            return polyp;
                        }
                    }
                }

                float wobble = Mathf.Sin(p.x * 7.3f + v) * 0.018f + Mathf.Cos(p.y * 6.1f) * 0.018f;
                float half = 0.90f + wobble, r = 0.20f;
                float qx = Mathf.Abs(p.x) - (half - r), qy = Mathf.Abs(p.y) - (half - r);
                float sd = (qx > 0f && qy > 0f) ? Mathf.Sqrt(qx * qx + qy * qy) : Mathf.Max(qx, qy);
                if (sd > r) return Color.clear;

                // тёплый градиент: светлая вершина, тёмное основание
                float t = Mathf.InverseLerp(-0.9f, 0.9f, p.y);
                Color c = t < 0.55f ? Color.Lerp(bottom, mid, Mathf.SmoothStep(0f, 1f, t / 0.55f))
                                    : Color.Lerp(mid, top, Mathf.SmoothStep(0f, 1f, (t - 0.55f) / 0.45f));
                c.a = 1f;
                c = Mottle(c, p, 3.2f, v);

                // мягкие рёбра: верхняя кромка светлее, нижняя темнее, обвод чуть темнее заливки
                float edge = Mathf.InverseLerp(r - 0.18f, r, sd);
                if (edge > 0f)
                {
                    if (p.y > Mathf.Abs(p.x) * 0.4f) c = Color.Lerp(c, top * 1.08f, 0.35f * edge);
                    else if (p.y < -Mathf.Abs(p.x) * 0.4f) c = Color.Lerp(c, bottom * 0.8f, 0.4f * edge);
                }
                if (sd > r - 0.035f) c = c * 0.82f;
                c.a = 1f;

                // окошко с внутренней тенью
                if (v == 2)
                {
                    float wx = Mathf.Abs(p.x) - 0.3f, wy = Mathf.Abs(p.y + 0.02f) - 0.3f;
                    float wsd = (wx > 0f && wy > 0f) ? Mathf.Sqrt(wx * wx + wy * wy) : Mathf.Max(wx, wy);
                    if (wsd < 0.1f)
                    {
                        // глубже у верхнего края (козырёк тенит), у нижнего — отсвет
                        float depth = Mathf.InverseLerp(-0.38f, 0.38f, p.y + 0.02f);
                        Color hole = Color.Lerp(bottom * 0.5f, bottom * 0.22f, depth);
                        if (wsd > 0.06f && p.y < -0.02f) hole = Color.Lerp(hole, top, 0.3f);   // светлая нижняя губа
                        return hole.WithAlpha(1f);
                    }
                    if (wsd < 0.16f) c = Color.Lerp(c, c * 0.7f, (0.16f - wsd) / 0.06f * 0.6f);   // обвод окна
                }

                for (int i = 0; i < pores.Length; i++) c = Pore(c, p, pores[i], 0.075f + 0.012f * (i % 3));

                // трещина у варианта 3: ломаная от верхнего края вниз, тёмная с светлой кромкой
                if (v == 3)
                {
                    float best = 9f;
                    Vector2 a0 = new Vector2(-0.55f, 0.88f), a1 = new Vector2(-0.3f, 0.45f), a2 = new Vector2(-0.38f, 0.1f), a3 = new Vector2(-0.05f, -0.3f);
                    best = Mathf.Min(DistToSegment(p, a0, a1), Mathf.Min(DistToSegment(p, a1, a2), DistToSegment(p, a2, a3)));
                    if (best < 0.022f) c = Color.Lerp(c, bottom * 0.45f, 0.85f);
                    else if (best < 0.045f && p.x > -0.4f) c = Color.Lerp(c, top, 0.25f);
                    c = Shell(c, p, new Vector2(0.5f, -0.52f), 0.12f);
                    c = Shell(c, p, new Vector2(0.66f, -0.36f), 0.08f);
                }
                else if (v == 0) c = Shell(c, p, new Vector2(0.58f, -0.6f), 0.1f);

                c = Algae(c, p, -0.35f, -0.9f, v);
                return c.WithAlpha(1f);
            }, size: CoralSize);
        }

        static Vector2[] TilePores(int v)
        {
            switch (v)
            {
                case 1: return new[] { new Vector2(-0.45f, -0.15f), new Vector2(0.3f, 0.1f), new Vector2(0.05f, -0.55f) };
                case 2: return new[] { new Vector2(-0.66f, 0.6f), new Vector2(0.64f, -0.6f), new Vector2(0.66f, 0.55f) };
                case 3: return new[] { new Vector2(0.35f, 0.42f), new Vector2(0.48f, 0.0f), new Vector2(-0.6f, -0.45f) };
                default: return new[] { new Vector2(-0.38f, 0.36f), new Vector2(0.42f, -0.2f),
                                        new Vector2(-0.1f, -0.56f), new Vector2(0.52f, 0.5f) };
            }
        }

        // ------------------------------------------------------- звенья шпиля

        /// <summary>
        /// Звено длинного коралла: овал вдоль ветви, звенья внахлёст. Светлый гребень,
        /// тёмный низ, кольца-рёбра поперёк, поры в шахматку, ракушка, налёт снизу.
        /// </summary>
        public static Sprite CoralLimb(int variant)
        {
            int v = Mathf.Clamp(variant, 0, CoralLimbVariants - 1);
            Color body = Shift(CoralLimbMid, (v - 1.5f) * 0.006f, -0.03f * (v % 2), 0.012f * (v - 1.5f));

            return Build("limb_" + v, p =>
            {
                float bulge = 0.6f + 0.05f * Mathf.Sin(p.x * 6.5f + (v % 2) * 2f + (v / 2) * 0.25f);
                float ex = p.x / 0.97f, ey = p.y / bulge;
                float d2 = ex * ex + ey * ey;
                if (d2 > 1f) return Color.clear;

                float t = Mathf.InverseLerp(-bulge, bulge * 0.8f, p.y);
                Color c = Color.Lerp(body * 0.62f, Color.Lerp(body, CoralBlockTop, 0.35f), Mathf.SmoothStep(0f, 1f, t));
                c.a = 1f;
                c = Mottle(c, p, 3f, v + 7);

                // рёбра поперёк ветви: мягкие тёмные кольца
                float ring = Mathf.Abs(Mathf.Sin((p.x + v * 0.13f) * 9.2f));
                if (ring < 0.12f) c = Color.Lerp(c, c * 0.8f, 0.5f * (1f - ring / 0.12f));
                // светлый гребень и тёмный низ — без резкой ступени
                if (ey > 0.35f) c = Color.Lerp(c, CoralBlockTop * 1.05f, 0.25f * Mathf.InverseLerp(0.35f, 0.8f, ey));
                if (ey < -0.55f) c = Color.Lerp(c, body * 0.5f, 0.3f * Mathf.InverseLerp(-0.55f, -1f, ey));

                // поры в шахматку
                for (int i = -2; i <= 2; i++)
                {
                    float px = i * 0.34f + (v % 2 == 0 ? 0.08f : -0.07f) + v * 0.02f;
                    float py = (i + v) % 2 == 0 ? 0.15f : -0.17f;
                    c = Pore(c, p, new Vector2(px, py), 0.06f);
                }
                c = Shell(c, p, new Vector2(-0.55f + v * 0.33f, -0.28f + 0.06f * (v % 2)), 0.1f);
                c = Algae(c, p, -0.2f, -0.6f, v + 3);
                if (d2 > 0.9f) c = c * 0.84f;   // обвод чуть темнее заливки
                return c.WithAlpha(1f);
            }, size: LimbSize);
        }

        // ------------------------------------------------------- шары локаций

        /// <summary>
        /// Шар локации, тонируется цветом локации (SpriteRenderer.color):
        /// 0 — луна (кратеры и «моря»), 1 — шестерёнка (зубцы, заклёпки, ступица).
        /// Свет — от середины к краю, без направления: шар при любом повороте
        /// выглядит одинаково освещённым (D-17), бликов, бегающих при вращении, нет.
        /// </summary>
        public static Sprite CoralBlob(int variant)
        {
            int v = Mathf.Clamp(variant, 0, 1);
            return Build("blob_" + v, v == 0 ? (System.Func<Vector2, Color>)Moon : Gear, size: BlobSize);
        }

        static Color Moon(Vector2 p)
        {
            float d = p.magnitude;
            if (d > 0.93f) return Color.clear;
            // шар: середина чуть светлее, к краю темнее — свет без направления
            Color c = Color.Lerp(BlobMetal * 1.06f, BlobMetal * 0.8f, Mathf.SmoothStep(0.2f, 1f, d / 0.93f));
            c.a = 1f;
            // моря: два мягких тёмных пятна с неровным краем
            float wob = (Mathf.PerlinNoise(p.x * 3f + 4f, p.y * 3f + 1f) - 0.5f) * 0.12f;
            float m1 = Vector2.Distance(p, new Vector2(-0.22f, 0.18f)) - 0.36f + wob;
            float m2 = Vector2.Distance(p, new Vector2(0.28f, -0.28f)) - 0.26f + wob;
            float sea = Mathf.Max(Mathf.SmoothStep(0.08f, -0.06f, m1), Mathf.SmoothStep(0.08f, -0.06f, m2));
            c = Color.Lerp(c, c * 0.74f, sea * 0.7f);
            // кратеры: тёмное дно, светлый вал вокруг — со всех сторон, без направления
            c = Crater(c, p, new Vector2(0.38f, 0.36f), 0.17f);
            c = Crater(c, p, new Vector2(-0.46f, -0.32f), 0.13f);
            c = Crater(c, p, new Vector2(0.02f, -0.6f), 0.1f);
            if (d > 0.88f) c = c * 0.82f;   // обвод чуть темнее заливки
            return c.WithAlpha(1f);
        }

        static Color Crater(Color c, Vector2 p, Vector2 at, float r)
        {
            float d = Vector2.Distance(p, at);
            if (d < r * 0.82f) return Color.Lerp(c, c * 0.72f, 0.75f);
            if (d < r * 1.18f) return Color.Lerp(c, c * 1.12f, 0.6f * (1f - Mathf.Abs(d - r) / (r * 0.18f)));
            return c;
        }

        static Color Gear(Vector2 p)
        {
            float d = p.magnitude;
            const int teeth = 10;
            float ang = Mathf.Atan2(p.y, p.x);
            // зуб: трапеция, вершина 45 % шага, скосы мягкие
            float phase = Mathf.Repeat(ang / (Mathf.PI * 2f) * teeth, 1f);
            float tooth = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.06f, 0.2f, phase)) *
                          Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.66f, 0.52f, phase));
            float outer = 0.78f + 0.17f * tooth;
            if (d > outer) return Color.clear;
            if (d < 0.11f) return Color.clear;   // отверстие в ступице

            Color c = Color.Lerp(BlobMetal * 1.04f, BlobMetal * 0.82f, Mathf.SmoothStep(0.3f, 1f, d));
            c.a = 1f;
            // металл: еле заметные концентрические кольца обработки
            c = c * (1f + Mathf.Sin(d * 70f) * 0.018f);
            // утопленный диск между ступицей и венцом
            if (d > 0.36f && d < 0.68f) c = c * 0.9f;
            if (Mathf.Abs(d - 0.36f) < 0.018f || Mathf.Abs(d - 0.68f) < 0.018f) c = c * 0.82f;
            // ступица: выпуклое кольцо и тёмный край отверстия
            if (d < 0.3f) c = Color.Lerp(c, BlobMetal * 1.1f, Mathf.InverseLerp(0.3f, 0.18f, d) * 0.5f);
            if (d < 0.16f) c = c * 0.62f;
            // три заклёпки
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + 0.4f;
                Vector2 at = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.52f;
                float rd = Vector2.Distance(p, at);
                if (rd < 0.055f) c = Color.Lerp(BlobMetal * 1.12f, BlobMetal * 0.7f, rd / 0.055f);
                else if (rd < 0.07f) c = c * 0.75f;
            }
            // царапины: несколько тонких светлых штрихов по венцу
            for (int i = 0; i < 3; i++)
            {
                float a = 1.1f + i * 2.2f;
                Vector2 s0 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.45f;
                Vector2 s1 = new Vector2(Mathf.Cos(a + 0.35f), Mathf.Sin(a + 0.35f)) * 0.62f;
                if (DistToSegment(p, s0, s1) < 0.008f) c = Color.Lerp(c, BlobMetal * 1.15f, 0.35f);
            }
            if (d > outer - 0.04f) c = c * 0.8f;   // обвод зубцов чуть темнее заливки
            return c.WithAlpha(1f);
        }
    }
}
