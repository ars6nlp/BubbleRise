using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Части грозовой медузы.
    ///
    /// **Почему частей много, а была одна.** Раньше весь босс был единственным
    /// спрайтом: купол, глаза и щупальца запечены в одну картинку, а «анимация»
    /// сводилась к подмене её на светлую копию при уроне. Поэтому щупальца не
    /// шевелились вовсе, глаза не могли следить за игроком, а телеграфировать
    /// атаку было нечем. [КОД]
    ///
    /// Теперь купол, свечение, глаз, рукава, бахрома и дымка — разные объекты, и
    /// каждый умеет своё: купол сжимается, свечение дышит, глаз разгорается и
    /// поворачивается, щупальца идут волной с запаздыванием.
    ///
    /// **Палитра холодная и тёмная.** Тёмно-фиолетовый и сине-чёрный корпус,
    /// биолюминесценция бирюзовая, болотно-зелёная и бледно-фиолетовая. Белого
    /// нет нигде — ни в одном слое, ни в одном состоянии.
    /// </summary>
    public static partial class SpriteFactory
    {
        // Цвета медузы держим здесь: они нужны и спрайтам, и контроллеру,
        // который красит ими слои в рантайме.
        public static readonly Color JellySkin = Palette.Hex("2A1F46");
        /// <summary>Тон купола. Светлее JellySkin, иначе тело тонет в воде.</summary>
        public static readonly Color JellyBody = Palette.Hex("463A72");
        public static readonly Color JellyDeep = Palette.Hex("120C26");
        public static readonly Color JellyEdge = Palette.Hex("8E7FC0");
        public static readonly Color JellyVein = Palette.Hex("5B4A94");
        public static readonly Color JellyOrgan = Palette.Hex("0C0718");
        public static readonly Color JellyBio1 = Palette.Hex("3FD8C8");   // бирюзовый
        public static readonly Color JellyBio2 = Palette.Hex("6E9B4A");   // болотно-зелёный
        public static readonly Color JellyBio3 = Palette.Hex("9B7FD0");   // бледно-фиолетовый
        public static readonly Color JellyIris = Palette.Hex("C8E86A");   // радужка: ядовитая, не жёлтая

        const int JellySize = 256;

        /// <summary>
        /// Купол: полупрозрачный, с жилками, тёмными органами, пузырьками газа,
        /// шрамами и наростами. Щупалец в нём больше нет — они живые объекты.
        ///
        /// Посадка купола в квадрате спрайта оставлена прежней (эллипс 0.95 на
        /// 0.98 со смещением вниз на 0.20): хитбокс считается от transform, и
        /// сдвиг рисунка развёл бы видимое тело и зону попадания. [КОД]
        /// </summary>
        public static Sprite JellyBell(bool mask = false) => Build(MaskKey("jelly_bell", mask), p =>
        {
            float ex = p.x / 0.95f;
            float ey = (p.y + 0.20f) / 0.98f;
            float d2 = ex * ex + ey * ey;
            if (d2 > 1f) return Color.clear;

            // Юбка купола.
            //
            // Раньше низ обрезался ПРЯМОЙ на y = −0.20, и купол читался как
            // срезанный: бока обрывались по горизонтали, а не загибались вниз,
            // как у настоящего колокола. Теперь граница идёт по дуге — к бокам
            // она опускается, — и по ней же пущены фестоны. [ТЕСТ]
            float skirt = -0.2f - 0.5f * (p.x * p.x);
            float scallop = Mathf.Abs(Mathf.Sin(p.x * 7.5f)) * 0.06f;
            if (p.y < skirt + scallop) return Color.clear;

            float d = Mathf.Sqrt(d2);

            // Тело: к краю темнее и плотнее, в середине прозрачнее — так виден
            // внутренний свет и органы.
            // Светлее и плотнее прежнего: на тёмной воде купол при альфе 0.72
            // пропадал целиком, от него оставался один ободок. Полупрозрачность
            // осталась, но теперь её хватает ровно на то, чтобы сквозь тело
            // просвечивали свет и органы. [ТЕСТ]
            Color c = Color.Lerp(JellyBody, JellyDeep, Mathf.SmoothStep(0f, 1f, d) * 0.85f);
            float alpha = Mathf.Lerp(0.9f, 1f, Mathf.SmoothStep(0f, 1f, d));

            // --- жилки: ветвистая сетка, видимая изнутри
            float vein = Mathf.Sin(p.y * 7f + Mathf.Sin(p.x * 5.5f) * 2.4f);
            float veinB = Mathf.Sin(p.x * 6.5f - Mathf.Sin(p.y * 4.5f) * 1.8f);
            float v = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(vein) * 9f),
                                Mathf.Clamp01(1f - Mathf.Abs(veinB) * 11f));
            if (v > 0f) c = Color.Lerp(c, JellyVein, v * 0.5f * (1f - d * 0.5f));

            // --- органы: несколько тёмных сгустков в верхней части купола
            for (int i = 0; i < 4; i++)
            {
                float a = 0.7f + i * 1.63f;
                var o = new Vector2(Mathf.Cos(a) * 0.34f, 0.3f + Mathf.Sin(a) * 0.22f);

                // Органы вытянуты по вертикали и неодинаковы: ровные круги
                // читаются как дырки, а не как внутренности.
                var q = new Vector2((p.x - o.x) / (0.3f - i * 0.03f),
                                    (p.y - o.y) / (0.21f - i * 0.018f));
                float od = q.magnitude;
                if (od < 1f)
                {
                    c = Color.Lerp(JellyOrgan, c, Mathf.SmoothStep(0.15f, 1f, od));

                    // Тусклая кромка органа: он не плоское пятно, а объём.
                    if (od > 0.78f) c = Color.Lerp(c, JellyVein, (od - 0.78f) / 0.22f * 0.45f);
                }
            }

            // --- пузырьки газа: мелкие светлые кольца внутри
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.399f;
                float r = 0.2f + (i % 4) * 0.16f;
                var b = new Vector2(Mathf.Cos(a) * r, 0.1f + Mathf.Sin(a) * r * 0.72f);
                float bd = Vector2.Distance(p, b);
                float rr = 0.028f + (i % 3) * 0.012f;
                if (bd < rr) c = Color.Lerp(c, JellyEdge, 0.3f + 0.3f * (1f - bd / rr));
            }

            // --- шрамы: два ломаных следа по куполу
            if (DistToSegment(p, new Vector2(-0.52f, 0.46f), new Vector2(-0.22f, 0.2f)) < 0.018f ||
                DistToSegment(p, new Vector2(-0.22f, 0.2f), new Vector2(-0.34f, -0.05f)) < 0.014f ||
                DistToSegment(p, new Vector2(0.46f, 0.1f), new Vector2(0.62f, -0.12f)) < 0.016f)
                c = Color.Lerp(c, JellyOrgan, 0.8f);

            // --- наросты по краю: паразиты, из-за которых край неровный
            for (int i = 0; i < 5; i++)
            {
                float a = -0.25f + i * 0.78f;
                var g = new Vector2(Mathf.Cos(a) * 0.9f, -0.2f + Mathf.Sin(a) * 0.92f);
                float gd = Vector2.Distance(p, g);
                if (gd < 0.075f) c = Color.Lerp(c, JellyOrgan, 0.6f * (1f - gd / 0.075f));
            }

            // --- бледная кайма по краю купола, тонкая
            if (d > 0.9f)
            {
                float k = Mathf.InverseLerp(0.9f, 1f, d);
                c = Color.Lerp(c, JellyEdge, Mathf.Sin(k * Mathf.PI) * 0.75f);
                alpha = Mathf.Lerp(alpha, 1f, 0.5f);
            }

            c.a = alpha;
            return c;
        }, size: JellySize);

        /// <summary>
        /// Внутреннее биолюминесцентное свечение. Отдельный слой, потому что его
        /// прозрачностью медуза дышит и разгорается перед атакой — без подмены
        /// спрайта и без вспышки.
        /// </summary>
        public static Sprite JellyGlow() => Build("jelly_glow", p =>
        {
            float ex = p.x / 0.9f;
            float ey = (p.y + 0.18f) / 0.92f;
            if (ex * ex + ey * ey > 1f) return Color.clear;

            Color c = Color.clear;
            float sum = 0f;

            // Три пятна разных холодных оттенков, чтобы свет не был однотонным.
            for (int i = 0; i < 6; i++)
            {
                float a = i * 1.047f + 0.4f;
                float r = 0.22f + (i % 3) * 0.17f;
                var q = new Vector2(Mathf.Cos(a) * r, 0.16f + Mathf.Sin(a) * r * 0.8f);
                float k = 1f - Mathf.Clamp01(Vector2.Distance(p, q) / 0.34f);
                if (k <= 0f) continue;

                k = Mathf.SmoothStep(0f, 1f, k);
                Color tone = i % 3 == 0 ? JellyBio1 : i % 3 == 1 ? JellyBio2 : JellyBio3;
                c += tone * k;
                sum += k;
            }

            if (sum <= 0.001f) return Color.clear;
            c /= sum;
            c.a = Mathf.Clamp01(sum * 0.5f);
            return c;
        }, size: JellySize);

        /// <summary>
        /// Глаз: один огромный, немигающий, с узким зрачком и влажным бликом.
        /// Лёгкая асимметрия заложена в сам рисунок — идеальный круг выглядит
        /// нарисованным, а не живым.
        /// </summary>
        public static Sprite JellyEye() => Build("jelly_eye", p =>
        {
            // Склера слегка вытянута и смещена: отсюда асимметрия.
            var e = new Vector2(p.x / 0.95f, (p.y - 0.03f) / 0.88f);
            float d = e.magnitude;
            if (d > 1f) return Color.clear;

            // Радужка с волокнами, к краю темнее.
            float ang = Mathf.Atan2(e.y, e.x);
            float fib = 0.5f + 0.5f * Mathf.Sin(ang * 22f);
            Color iris = Color.Lerp(JellyIris, JellyBio2, Mathf.Clamp01(d * 1.15f) * (0.55f + fib * 0.45f));
            Color c = Color.Lerp(iris, JellyOrgan, Mathf.SmoothStep(0.55f, 1f, d));

            // Узкий вертикальный зрачок — от него взгляд и становится нечеловеческим.
            float px = Mathf.Abs(p.x) / 0.13f;
            float py = Mathf.Abs(p.y - 0.02f) / 0.72f;
            if (px * px + py * py < 1f) c = JellyOrgan;

            // Влажный блик: смещён от центра, мягкий, не белый.
            float g = 1f - Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.3f, 0.36f)) / 0.26f);
            if (g > 0f) c = Color.Lerp(c, JellyEdge, Mathf.SmoothStep(0f, 1f, g) * 0.8f);

            c.a = d > 0.93f ? Mathf.InverseLerp(1f, 0.93f, d) : 1f;
            return c;
        }, size: 128);

        /// <summary>
        /// Звено толстого ротового рукава: сегмент с перехватом и присосками.
        /// Рукав собирается из нескольких таких звеньев, поэтому сам спрайт
        /// короткий — так он гнётся, а не ломается.
        /// </summary>
        public static Sprite JellyArm() => BuildRect("jelly_arm", 64, 128, p =>
        {
            // Звено — капсула, а не прямоугольник: у прямоугольных звеньев на
            // изгибе видны стыки, и рукав читается лесенкой. [ТЕСТ]
            float t = Mathf.InverseLerp(1f, -1f, p.y);
            float halfW = Mathf.Lerp(0.78f, 0.5f, t);

            // Скругление концов: к самым краям ширина сходит на нет.
            float cap = Mathf.Min(Mathf.InverseLerp(1f, 0.86f, p.y),
                                  Mathf.InverseLerp(-1f, -0.86f, p.y));
            halfW *= Mathf.Sqrt(Mathf.Clamp01(cap));

            float ax = Mathf.Abs(p.x);
            if (halfW <= 0.001f || ax > halfW) return Color.clear;

            float across = ax / halfW;

            // Тело: светлее по центру, темнее к краям — объём без блика.
            Color c = Color.Lerp(JellySkin, JellyDeep, across * across * 0.9f);

            // Перехваты: мягкие, не поперечные полосы во всю ширину.
            float seg = Mathf.Abs(Mathf.Sin(p.y * 3.1f));
            c = Color.Lerp(c, JellyDeep, (1f - seg) * 0.22f);

            // Присоски: два ряда круглых чашек в шахматном порядке.
            for (int row = 0; row < 5; row++)
            {
                float sy = Mathf.Lerp(0.78f, -0.78f, row / 4f);
                float side = (row % 2 == 0) ? 0.42f : -0.42f;
                var q = new Vector2(side * halfW, sy);
                float dd = Vector2.Distance(new Vector2(p.x, p.y * 0.55f),
                                            new Vector2(q.x, q.y * 0.55f));
                if (dd < 0.13f)
                {
                    float k = 1f - dd / 0.13f;
                    c = Color.Lerp(c, JellyVein, 0.5f + 0.35f * k);
                    if (dd < 0.06f) c = Color.Lerp(c, JellyOrgan, 0.5f);
                }
            }

            // Светлая кромка по одной стороне — рукав становится объёмным.
            if (p.x < -halfW * 0.6f)
                c = Color.Lerp(c, JellyEdge, Mathf.InverseLerp(-halfW * 0.6f, -halfW, p.x) * 0.45f);

            c.a = Mathf.Lerp(0.96f, 0.6f, Mathf.Clamp01((across - 0.75f) / 0.25f));
            return c;
        });

        /// <summary>
        /// Полоса тонких нитей. Десятки отдельных нитей объектами обошлись бы в
        /// сотни спрайтов; здесь их восемь в одной текстуре, а живость даёт
        /// покачивание самой полосы с запаздыванием по ярусам. [РАСЧЁТ]
        /// </summary>
        public static Sprite JellyFringe(int variant) => BuildRect("jelly_fringe" + variant, 128, 256, p =>
        {
            int n = 8;
            float best = 1f;
            float tipGlow = 0f;

            for (int i = 0; i < n; i++)
                {
                float seed = i * 1.7f + variant * 3.1f;
                float baseX = (i + 0.5f) / n * 2f - 1f;

                // Нити разной длины и местами рваные.
                float len = 0.55f + Mathf.Repeat(Mathf.Sin(seed * 12.9f) * 43.7f, 1f) * 0.45f;
                float yTop = 1f;
                float yBot = 1f - len * 2f;
                if (p.y < yBot || p.y > yTop) continue;

                float t = Mathf.InverseLerp(yTop, yBot, p.y);
                float wob = Mathf.Sin(p.y * 5.5f + seed) * 0.08f + Mathf.Sin(p.y * 13f + seed * 2f) * 0.02f;
                float w = Mathf.Lerp(0.035f, 0.012f, t);

                float dx = Mathf.Abs(p.x - (baseX + wob));
                if (dx < w)
                {
                    best = Mathf.Min(best, dx / w);
                    if (t > 0.86f) tipGlow = Mathf.Max(tipGlow, (t - 0.86f) / 0.14f);
                }
            }

            if (best >= 1f) return Color.clear;

            Color c = Color.Lerp(JellyVein, JellyDeep, best);
            if (tipGlow > 0f) c = Color.Lerp(c, JellyBio1, tipGlow * 0.8f);

            c.a = Mathf.Lerp(0.85f, 0.25f, best);
            return c;
        });

        /// <summary>
        /// Тёмная рябь, ползущая по куполу перед атакой.
        ///
        /// Отдельный слой, а не перекраска купола: перекрасить купол целиком —
        /// это изменить яркость всей его площади разом, то есть ровно то, чего
        /// правила для глаз не разрешают. Полоса же проходит по телу и меняет
        /// за кадр лишь малую его часть.
        /// </summary>
        public static Sprite JellyRipple() => Build("jelly_ripple", p =>
        {
            float ex = p.x / 0.95f;
            float ey = (p.y + 0.20f) / 0.98f;
            if (ex * ex + ey * ey > 1f) return Color.clear;

            // Три волны поперёк купола, мягкие по краям.
            float band = Mathf.Sin(p.y * 6.5f + Mathf.Sin(p.x * 3.4f) * 0.9f);
            float k = Mathf.Clamp01(1f - Mathf.Abs(band) * 2.2f);
            if (k <= 0f) return Color.clear;

            Color c = JellyOrgan;
            c.a = Mathf.SmoothStep(0f, 1f, k) * 0.7f;
            return c;
        }, size: JellySize);

        /// <summary>
        /// Дымка вокруг босса: тёмная, с лёгким холодным подсветом по краю.
        /// Плотной середины у неё нет — сквозь дымку обязано быть видно снаряды.
        /// </summary>
        public static Sprite JellyHaze() => Build("jelly_haze", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            // Кольцо, а не диск: середину закрывать нельзя, там сам босс и
            // вылетающие снаряды.
            float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.66f) / 0.34f);
            ring = Mathf.SmoothStep(0f, 1f, ring);

            Color c = Color.Lerp(JellyDeep, JellyBio1, 0.18f);
            c.a = ring * 0.3f;
            return c;
        }, size: JellySize);
    }
}
