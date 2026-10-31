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

        /// <summary>
        /// Сам босс. Силуэт нарочно не такой, как у «Спрута глубин»: там голова с
        /// длинными щупальцами по всему экрану, здесь — приземистая мантия каракатицы
        /// с большим сифоном внизу и короткими поджатыми руками. Два босса-осьминога
        /// в одной ротации обязаны различаться с одного взгляда.
        /// </summary>
        public static Sprite InkBoss(bool mask) => BuildRect(MaskKey("inkboss", mask), 320, 256, p =>
        {
            Color mantle = Palette.InkMantle;
            Color dark = Palette.InkMantleDk;
            Color glow = Palette.InkGlow;

            // ---- короткие поджатые руки веером снизу
            if (p.y < 0.10f)
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                float side = Mathf.Sign(i);
                float k = Mathf.Abs(i) / 3f;

                var root = new Vector2(i * 0.11f, -0.16f);
                var mid = new Vector2(i * 0.20f - side * 0.02f, -0.42f + k * 0.06f);
                var tip = new Vector2(i * 0.16f + side * 0.14f, -0.60f + k * 0.12f);

                if (TaperedSegment(p, root, mid, 0.070f, 0.048f) ||
                    TaperedSegment(p, mid, tip, 0.048f, 0.016f))
                    return Color.Lerp(dark, mantle, 0.35f + k * 0.2f);
            }

            // ---- сифон: труба, из которой и летят чернила
            if (TaperedSegment(p, new Vector2(0f, -0.10f), new Vector2(0f, -0.40f), 0.13f, 0.10f))
            {
                // светящееся жерло — по нему читается замах
                if (p.y < -0.30f) return glow;
                return dark;
            }

            // ---- мантия: широкий низкий купол
            float ex = p.x / 0.62f;
            float ey = (p.y - 0.10f) / 0.42f;
            if (ex * ex + ey * ey > 1f) return Color.clear;

            Color c = Color.Lerp(mantle, dark, Mathf.InverseLerp(0.34f, -0.18f, p.y));
            c.a = 1f;

            // боковые плавники каракатицы — тёмные полосы вдоль кромки
            if (Mathf.Abs(Mathf.Abs(ex) - 0.86f) < 0.10f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.30f));

            // крапчатый узор: пятна разного размера, как у настоящей каракатицы
            for (int i = 0; i < 14; i++)
            {
                float a = i * 2.399f;                       // золотой угол — равномерно без сетки
                float r = 0.14f + 0.42f * Mathf.Sqrt((i + 0.5f) / 14f);
                var spot = new Vector2(Mathf.Cos(a) * r * 1.35f, 0.10f + Mathf.Sin(a) * r * 0.9f);
                if (Vector2.Distance(p, spot) < 0.045f + 0.02f * Mathf.Sin(i * 3.7f))
                    c = Blend(c, new Color(0f, 0f, 0f, 0.28f));
            }

            // ---- глаза: горизонтальный зрачок, как у головоногих
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new Vector2(side * 0.26f, 0.10f);
                float de = Vector2.Distance(new Vector2((p.x - eye.x), (p.y - eye.y) * 1.5f), Vector2.zero);
                if (de < 0.115f)
                {
                    if (Mathf.Abs(p.y - eye.y) < 0.022f && Mathf.Abs(p.x - eye.x) < 0.075f)
                        return new Color(0.03f, 0.02f, 0.05f, 1f);
                    return glow;
                }
            }

            return c;
        });
    }
}
