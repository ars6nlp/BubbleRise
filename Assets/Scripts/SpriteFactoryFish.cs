using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Рыбы-угрозы: пиранья, зелёный луркер, медуза.
    ///
    /// **Что было.** Эллипс, три треугольника-плавника, полоски у пираньи — на
    /// размере игры читалось, но вблизи выглядело вырезанным из бумаги, и хвост
    /// не двигался: рыба виляла телом целиком, как дощечка.
    ///
    /// **Что стало — в меру.** Силуэт тот же по размеру (вписан в прежний квадрат,
    /// хитбокс — круг Radius, о рисунке он ничего не знает), но у каждой рыбы свой:
    /// пиранья — высокая, горбатая, с выдвинутой нижней челюстью и простыми
    /// чёткими зубцами; луркер — вытянутый, с длинным спинным плавником и
    /// раздвоенным хвостом; медуза — купол с каналами и бахромой. Деталей немного и
    /// крупных: плавники с лучами, чешуя мягкими пятнами (не по одной чешуйке),
    /// жаберная крышка, блик по спине и тень по брюху, глаз с радужкой и бликом.
    ///
    /// **Хвост — отдельный спрайт.** Рыба = тело + хвост (ребёнок, точка опоры —
    /// корень хвоста), хвост ходит волной вслед за телом (Fish.Tick). Отдельный
    /// «целый» спрайт — для гибели и статуса: накладка статуса копирует спрайт
    /// цели, и у погибшей рыбы хвост на месте. Медуза так же: купол + щупальца.
    ///
    /// **Оттенки.** Три на вид, выбираются при появлении (Fish.Activate) цветом
    /// SpriteRenderer — без лишних текстур. Тёмные места подняты так, чтобы любой
    /// оттенок держал ≥ 3:1 к воде (Tools/check_play.py).
    /// </summary>
    public static partial class SpriteFactory
    {
        // Палитра. Середина тела — по ней считается контраст с водой.
        public static readonly Color FishPiranhaBack  = Palette.Hex("D2362A");
        public static readonly Color FishPiranhaMid   = Palette.Hex("FF5A3C");
        public static readonly Color FishPiranhaBelly = Palette.Hex("FF9A72");
        public static readonly Color FishPiranhaFin   = Palette.Hex("D23A26");
        public static readonly Color FishLurkerBack   = Palette.Hex("1F8A44");
        public static readonly Color FishLurkerMid    = Palette.Hex("3FCF66");
        public static readonly Color FishLurkerBelly  = Palette.Hex("B8F0A8");
        public static readonly Color FishLurkerFin    = Palette.Hex("2A9E50");
        static readonly Color FishEyeDark  = Palette.Hex("120A0C");
        static readonly Color FishTeeth    = Palette.Hex("EDE4D6");
        static readonly Color FishMouth    = Palette.Hex("2A0E10");

        /// <summary>
        /// Оттенки на вид: множитель цвета SpriteRenderer. Сдвиги мягкие — рыба
        /// остаётся своей (пиранья красная, луркер зелёный), но стая не из копий.
        /// </summary>
        public static readonly Color[] PiranhaTints =
            { Color.white, new Color(1f, 0.92f, 0.86f), new Color(0.96f, 0.95f, 1f) };
        public static readonly Color[] LurkerTints =
            { Color.white, new Color(0.86f, 1f, 0.88f), new Color(1f, 0.97f, 0.80f) };
        public static readonly Color[] JellyTints =
            { Color.white, new Color(0.88f, 0.92f, 1f), new Color(1f, 0.90f, 0.96f) };

        /// <summary>Корень хвоста в координатах рисунка рыбы (1 = Radius).</summary>
        public static readonly Vector2 FishTailRoot = new Vector2(-0.56f, 0f);
        /// <summary>Сторона квадрата хвостового спрайта в координатах рисунка рыбы.</summary>
        public const float FishTailSpan = 0.92f;
        /// <summary>Где у медузы крепятся щупальца и какой высоты их спрайт.</summary>
        public static readonly Vector2 JellyTentacleRoot = new Vector2(0f, -0.14f);
        public const float JellyTentacleSpan = 1.2f;

        const int FishSize = 128;

        // ---------------------------------------------------------- спрайты

        /// <summary>Рыба целиком, смотрит вправо: для гибели и накладки статуса.</summary>
        public static Sprite FishSprite(FishType type)
        {
            if (type == FishType.Jellyfish)
                return BuildPivot("fishx_jelly", p => JellyBell(p).a > 0.002f ? OverTentacles(p) : JellyTentaclesAt(p - JellyTentacleRoot, 0f),
                                  new Vector2(0.5f, 0.5f), FishSize, 0f);
            bool piranha = type == FishType.Piranha;
            return BuildPivot(piranha ? "fishx_piranha" : "fishx_lurker", p =>
            {
                Color body = piranha ? PiranhaBody(p) : LurkerBody(p);
                if (body.a > 0.99f) return body;
                Color tail = piranha ? PiranhaTail(p - FishTailRoot) : LurkerTail(p - FishTailRoot);
                return body.a > 0.002f ? Blend(tail, body) : tail;
            }, new Vector2(0.5f, 0.5f), FishSize, 0.82f);

            Color OverTentacles(Vector2 p)
            {
                Color bell = JellyBell(p);
                Color t = JellyTentaclesAt(p - JellyTentacleRoot, 0f);
                return t.a > 0.002f ? Blend(t, bell) : bell;
            }
        }

        /// <summary>Тело без хвоста (у медузы — купол без щупалец).</summary>
        public static Sprite FishBody(FishType type)
        {
            if (type == FishType.Jellyfish) return BuildPivot("fishb_jelly", JellyBell, new Vector2(0.5f, 0.5f), FishSize, 0f);
            bool piranha = type == FishType.Piranha;
            return BuildPivot(piranha ? "fishb_piranha" : "fishb_lurker",
                              piranha ? (Func<Vector2, Color>)PiranhaBody : LurkerBody,
                              new Vector2(0.5f, 0.5f), FishSize, 0.82f);
        }

        /// <summary>
        /// Хвост (у медузы — щупальца). Опора спрайта — корень: у рыбы правый край
        /// посередине, у медузы — верх посередине; хвост качается вокруг неё.
        /// </summary>
        public static Sprite FishTail(FishType type)
        {
            if (type == FishType.Jellyfish)
                return BuildPivot("fisht_jelly", p => JellyTentaclesAt(new Vector2(p.x, p.y - 1f) * (JellyTentacleSpan * 0.5f), 0f),
                                  new Vector2(0.5f, 1f), FishSize, 0f);
            bool piranha = type == FishType.Piranha;
            float half = FishTailSpan * 0.5f;
            return BuildPivot(piranha ? "fisht_piranha" : "fisht_lurker", p =>
            {
                var d = new Vector2((p.x - 1f) * half, p.y * half);
                return piranha ? PiranhaTail(d) : LurkerTail(d);
            }, new Vector2(1f, 0.5f), FishSize, 0.82f);
        }

        /// <summary>Спрайт с опорой не в центре: хвосту нужна точка вращения у корня.</summary>
        static Sprite BuildPivot(string key, Func<Vector2, Color> shade, Vector2 pivot, int size, float outline)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            var px = Render(shade, size);
            if (outline > 0f) Outline(px, size, size, Mathf.Max(1, size / 64), outline);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "tex_" + key
            };
            tex.SetPixels(px);
            tex.Apply(false, true);
            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), pivot, size, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        // ---------------------------------------------------------- общее

        /// <summary>
        /// Чешуя мягкими пятнами: дуги рядами, но видны только там, где их
        /// пропускает крупный шум, и еле-еле — пятна, а не сетка.
        /// </summary>
        static Color Scales(Color c, Vector2 p, float density, float amount, int seed)
        {
            float u = p.x * density;
            float row = Mathf.Floor(p.y * density * 1.4f);
            float v = p.y * density * 1.4f - row;
            float cu = u + (Mathf.Repeat(row, 2f) < 1f ? 0.5f : 0f);
            float fu = cu - Mathf.Floor(cu) - 0.5f;
            float arc = Mathf.Abs(Mathf.Sqrt(fu * fu + (v - 0.15f) * (v - 0.15f)) - 0.42f);
            float mask = Mathf.SmoothStep(0.45f, 0.7f, Mathf.PerlinNoise(p.x * 2.2f + seed * 3.1f, p.y * 2.2f + seed));
            if (arc < 0.07f) c = Blend(c, new Color(0f, 0f, 0f, amount * mask * (1f - arc / 0.07f)));
            return c;
        }

        /// <summary>Плавник: заливка с лучами от основания и прозрачнеющей кромкой.</summary>
        static Color Fin(Color fin, Vector2 p, Vector2 origin, float edge01, int rays)
        {
            float ang = Mathf.Atan2(p.y - origin.y, p.x - origin.x);
            float ray = Mathf.Abs(Mathf.Sin(ang * rays));
            Color c = Color.Lerp(fin * 0.82f, fin * 1.12f, Mathf.SmoothStep(0f, 1f, edge01));
            if (ray < 0.16f) c = Color.Lerp(c, fin * 0.62f, 0.55f * (1f - ray / 0.16f));
            c.a = Mathf.Lerp(0.97f, 0.78f, Mathf.SmoothStep(0.55f, 1f, edge01));
            return c;
        }

        /// <summary>Глаз: радужка, зрачок, влажный блик.</summary>
        static Color Eye(Vector2 p, Vector2 at, float r, Color iris, Color c)
        {
            float d = Vector2.Distance(p, at);
            if (d > r) return c;
            if (d > r * 0.86f) return FishEyeDark;
            Color e = Color.Lerp(iris * 1.15f, iris * 0.75f, d / r);
            if (Vector2.Distance(p, at + new Vector2(r * 0.18f, 0f)) < r * 0.5f) e = FishEyeDark;
            if (Vector2.Distance(p, at + new Vector2(-r * 0.2f, r * 0.32f)) < r * 0.2f) e = Color.Lerp(e, Color.white, 0.85f);
            e.a = 1f;
            return e;
        }

        // ---------------------------------------------------------- пиранья

        /// <summary>Пиранья: высокая, горбатая; полуоси тела 0.62 × 0.44 сверху / 0.38 снизу.</summary>
        static float PiranhaHalfHeight(float x, bool top)
        {
            float t = (x - 0.02f) / 0.62f;
            if (Mathf.Abs(t) >= 1f) return 0f;
            float e = Mathf.Sqrt(1f - t * t);
            // горб над головой и чуть приподнятая спина
            float hump = top ? 1f + 0.12f * Mathf.Exp(-((x - 0.12f) * (x - 0.12f)) / 0.05f) : 1f;
            return (top ? 0.44f : 0.38f) * e * hump;
        }

        static Color PiranhaBody(Vector2 p)
        {
            bool top = p.y >= 0f;
            float h = PiranhaHalfHeight(p.x, top);

            // вне тела — плавники
            if (Mathf.Abs(p.y) > h)
            {
                if (top && p.x > -0.3f && p.x < 0.16f)
                {
                    // спинной: поднимается от спины к пику 0.62 и спадает к хвосту
                    float k = Mathf.InverseLerp(0.16f, -0.3f, p.x);
                    float finTop = Mathf.Lerp(h, 0.62f, Mathf.Sin(Mathf.PI * Mathf.Pow(k, 0.7f)));
                    if (p.y < finTop)
                        return Fin(FishPiranhaFin, p, new Vector2(-0.07f, 0.3f), Mathf.InverseLerp(h, finTop, p.y), 9);
                }
                if (!top && InTriangle(p, new Vector2(0.02f, -0.34f), new Vector2(0.2f, -0.33f), new Vector2(0.04f, -0.52f)))
                    return Fin(FishPiranhaFin, p, new Vector2(0.1f, -0.34f), Mathf.InverseLerp(-0.34f, -0.52f, p.y), 7);
                if (!top && InTriangle(p, new Vector2(-0.42f, -0.25f), new Vector2(-0.16f, -0.34f), new Vector2(-0.38f, -0.48f)))
                    return Fin(FishPiranhaFin, p, new Vector2(-0.28f, -0.3f), Mathf.InverseLerp(-0.3f, -0.48f, p.y), 7);
                return Color.clear;
            }

            float yn = Mathf.Abs(p.y) / Mathf.Max(0.01f, h);   // 0 у оси, 1 у кромки
            // спина темнее, бок — свой цвет, брюхо светлее, самый низ в тени
            Color c = top ? Color.Lerp(FishPiranhaMid, FishPiranhaBack, Mathf.SmoothStep(0.25f, 1f, yn))
                          : Color.Lerp(FishPiranhaMid, FishPiranhaBelly, Mathf.SmoothStep(0.1f, 0.6f, yn));
            if (!top) c = Color.Lerp(c, FishPiranhaBack * 0.85f, Mathf.SmoothStep(0.75f, 1f, yn) * 0.6f);
            c.a = 1f;
            // блик по спине: мягкая полоса у верхней кромки в передней половине
            if (top && yn > 0.5f && yn < 0.88f && p.x > -0.3f)
                c = Color.Lerp(c, Palette.Hex("FFB09A"), 0.3f * Mathf.InverseLerp(-0.3f, 0.3f, p.x) * (1f - Mathf.Abs(yn - 0.69f) / 0.19f));

            c = Scales(c, p, 9f, 0.12f, 1);

            // жаберная крышка: дуга за глазом и светлая кромка за ней
            float gill = Mathf.Abs(Vector2.Distance(p, new Vector2(0.5f, 0.02f)) - 0.3f);
            if (p.x < 0.32f && gill < 0.022f && yn < 0.85f) c = Color.Lerp(c, FishPiranhaBack * 0.7f, 0.7f);
            else if (p.x < 0.3f && gill < 0.045f && yn < 0.8f) c = Color.Lerp(c, FishPiranhaBelly, 0.25f);

            // грудной плавник на боку — полупрозрачный лист назад-вниз
            Vector2 pf = p - new Vector2(0.14f, -0.1f);
            float pfx = pf.x * 0.82f + pf.y * 0.57f, pfy = -pf.x * 0.57f + pf.y * 0.82f;
            if (pfx < 0f && pfx > -0.22f && Mathf.Abs(pfy) < 0.07f * (1f + pfx / 0.22f) + 0.012f)
                c = Color.Lerp(c, FishPiranhaFin * 0.9f, 0.3f);

            // пасть: нижняя челюсть выдвинута, зубы — простые треугольники сверху и снизу
            if (p.x > 0.4f && p.y < 0.02f && p.y > -0.2f)
            {
                float JawY(float x) => -0.07f - (x - 0.4f) * 0.25f;
                for (int i = 0; i < 4; i++)
                {
                    float x0 = 0.45f + i * 0.055f, yj = JawY(x0);
                    // верхние зубы смотрят вниз, нижние — вверх, вперемежку
                    if (InTriangle(p, new Vector2(x0 - 0.022f, yj + 0.028f), new Vector2(x0 + 0.022f, yj + 0.028f), new Vector2(x0, yj - 0.045f)))
                        return FishTeeth;
                    if (i < 3 && InTriangle(p, new Vector2(x0 + 0.006f, yj - 0.028f), new Vector2(x0 + 0.046f, yj - 0.028f), new Vector2(x0 + 0.026f, yj + 0.03f)))
                        return FishTeeth;
                }
                if (Mathf.Abs(p.y - JawY(p.x)) < 0.028f) return FishMouth;
            }
            return Eye(p, new Vector2(0.36f, 0.13f), 0.105f, Palette.Hex("FFB440"), c);
        }

        /// <summary>Хвост пираньи, d — от корня: веер с неглубокой выемкой и лучами.</summary>
        static Color PiranhaTail(Vector2 d)
        {
            if (d.x > 0.02f) return Color.clear;
            float len = -d.x;
            float spread = 0.1f + len * 0.85f;           // расходится от корня
            if (Mathf.Abs(d.y) > spread || len > 0.46f) return Color.clear;
            float notch = 0.38f + 0.06f * (Mathf.Abs(d.y) / Mathf.Max(0.01f, spread));   // выемка посередине
            if (len > notch) return Color.clear;
            return Fin(FishPiranhaFin, new Vector2(d.x, d.y), Vector2.zero, len / notch, 7);
        }

        // ---------------------------------------------------------- луркер

        /// <summary>Луркер: вытянутый, полуоси 0.66 × 0.3, морда острее.</summary>
        static float LurkerHalfHeight(float x)
        {
            float t = (x + 0.02f) / 0.66f;
            if (Mathf.Abs(t) >= 1f) return 0f;
            float e = Mathf.Sqrt(1f - t * t);
            float snout = x > 0.3f ? Mathf.Lerp(1f, 0.8f, (x - 0.3f) / 0.34f) : 1f;
            return 0.32f * e * snout;
        }

        static Color LurkerBody(Vector2 p)
        {
            bool top = p.y >= 0f;
            float h = LurkerHalfHeight(p.x);

            if (Mathf.Abs(p.y) > h)
            {
                // длинный спинной плавник вдоль всей спины, кромка волной
                if (top && p.x > -0.5f && p.x < 0.22f)
                {
                    float k = Mathf.InverseLerp(-0.5f, 0.22f, p.x);
                    float finTop = h + 0.05f + 0.21f * Mathf.Sin(k * Mathf.PI) + 0.015f * Mathf.Sin(k * 9f);
                    if (p.y < finTop) return Fin(FishLurkerFin, p, new Vector2(p.x - 0.12f, h - 0.12f), Mathf.InverseLerp(h, finTop, p.y), 3);
                }
                // брюшной плавник
                if (!top && InTriangle(p, new Vector2(-0.06f, -0.29f), new Vector2(0.14f, -0.28f), new Vector2(-0.02f, -0.53f)))
                    return Fin(FishLurkerFin, p, new Vector2(0.04f, -0.29f), Mathf.InverseLerp(-0.29f, -0.53f, p.y), 6);
                return Color.clear;
            }

            float yn = Mathf.Abs(p.y) / Mathf.Max(0.01f, h);
            Color c = top ? Color.Lerp(FishLurkerMid, FishLurkerBack, Mathf.SmoothStep(0.15f, 1f, yn))
                          : Color.Lerp(FishLurkerMid, FishLurkerBelly, Mathf.SmoothStep(0.2f, 0.7f, yn) * 0.7f);
            if (!top) c = Color.Lerp(c, FishLurkerBack * 0.8f, Mathf.SmoothStep(0.8f, 1f, yn) * 0.55f);
            c.a = 1f;
            // крупные тёмные пятна по спине — мягко
            float spots = Mathf.PerlinNoise(p.x * 5.2f + 7f, p.y * 5.2f + 3f);
            if (top && spots > 0.62f) c = Color.Lerp(c, FishLurkerBack * 0.75f, Mathf.SmoothStep(0.62f, 0.78f, spots) * 0.5f);
            // боковая линия
            if (Mathf.Abs(p.y - (0.02f + 0.04f * Mathf.Sin(p.x * 3f))) < 0.012f && p.x < 0.25f && p.x > -0.5f)
                c = Color.Lerp(c, FishLurkerBelly, 0.35f);
            // блик по спине
            if (top && yn > 0.5f && yn < 0.82f) c = Color.Lerp(c, Palette.Hex("D8FFC8"), 0.22f * (1f - Mathf.Abs(yn - 0.66f) / 0.16f));

            c = Scales(c, p, 11f, 0.12f, 4);

            // жабры: две дуги
            for (int g = 0; g < 2; g++)
            {
                float gill = Mathf.Abs(Vector2.Distance(p, new Vector2(0.56f - g * 0.05f, 0f)) - 0.28f);
                if (p.x < 0.32f - g * 0.05f && gill < 0.016f && yn < 0.8f) c = Color.Lerp(c, FishLurkerBack * 0.7f, 0.6f);
            }
            // пасть — длинная щель с мелкими зубцами
            float mouth = p.y + 0.06f + (p.x - 0.42f) * 0.12f;
            if (p.x > 0.42f && Mathf.Abs(mouth) < 0.022f)
                return Mathf.Repeat(p.x * 26f, 1f) < 0.42f && mouth > -0.004f ? FishTeeth : FishMouth;
            // светящийся глаз — ровный, не мигает
            return Eye(p, new Vector2(0.38f, 0.1f), 0.09f, Palette.Gold, c);
        }

        /// <summary>Хвост луркера: раздвоенный, лопасти острые.</summary>
        static Color LurkerTail(Vector2 d)
        {
            if (d.x > 0.02f) return Color.clear;
            float len = -d.x;
            float ay = Mathf.Abs(d.y);
            float spread = 0.07f + len * 1.0f;
            if (ay > spread || len > 0.46f) return Color.clear;
            // вилка: середина короче, лопасти длиннее
            float reach = Mathf.Lerp(0.2f, 0.46f, Mathf.SmoothStep(0f, 1f, ay / Mathf.Max(0.01f, spread)));
            if (len > reach) return Color.clear;
            return Fin(FishLurkerFin, d, Vector2.zero, len / reach, 6);
        }

        // ---------------------------------------------------------- медуза

        static readonly Color JellyRim = Palette.Hex("E8D8FF");

        /// <summary>
        /// Купол медузы: каналы от середины, бахрома по краю, ядро. Плотнее прежнего
        /// (α середины 0.62, кромки 0.82): полупрозрачный купол на тёмной воде
        /// иначе давал меньше 3:1.
        /// </summary>
        static Color JellyBell(Vector2 p)
        {
            float scallop = Mathf.Abs(Mathf.Sin(p.x * 7.5f)) * 0.06f;
            float bottom = -0.16f + scallop;
            if (p.y < bottom) return Color.clear;
            float ex = p.x / 0.92f, ey = (p.y + 0.16f) / 0.92f;
            float d = Mathf.Sqrt(ex * ex + ey * ey);
            if (d > 1f) return Color.clear;

            Color c = Color.Lerp(Palette.JellyBell * 1.08f, Palette.JellyBell * 0.82f, d);
            c.a = Mathf.Lerp(0.62f, 0.82f, Mathf.SmoothStep(0.6f, 1f, d));
            // радиальные каналы
            float ang = Mathf.Atan2(ey, ex);
            if (Mathf.Abs(Mathf.Sin(ang * 8f)) < 0.09f && d > 0.25f) c = Color.Lerp(c, JellyRim, 0.35f);
            // ядро-гонады: четыре мягких лепестка
            Vector2 q = p - new Vector2(0f, 0.08f);
            float petals = q.magnitude - 0.2f - 0.07f * Mathf.Cos(Mathf.Atan2(q.y, q.x) * 4f);
            if (petals < 0f) c = Color.Lerp(c, Palette.JellyCore, 0.55f * Mathf.SmoothStep(0f, -0.08f, petals) + 0.2f);
            // светлая кромка и бахрома по нижнему краю
            if (d > 0.88f) c = Color.Lerp(c, JellyRim, 0.45f);
            if (p.y < bottom + 0.05f) c = Color.Lerp(c, JellyRim, 0.4f);
            return c;
        }

        /// <summary>
        /// Щупальца медузы, d — от точки крепления вниз (d.y &lt; 0): пять тонких и
        /// два ротовых — пошире, с бахромой. phase — для рисунка, не для анимации.
        /// </summary>
        static Color JellyTentaclesAt(Vector2 d, float phase)
        {
            if (d.y > 0.02f || d.y < -0.84f) return Color.clear;
            float depth = Mathf.InverseLerp(0f, -0.84f, d.y);
            Color core = Palette.JellyCore;
            // ротовые лопасти — две, у середины
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * 0.07f + Mathf.Sin(d.y * 6f + s) * 0.04f;
                float w = Mathf.Lerp(0.065f, 0.028f, depth) * (1f + 0.12f * Mathf.Sin(d.y * 22f));
                if (Mathf.Abs(d.x - x) < w && depth < 0.55f)
                    return new Color(core.r, core.g, core.b, Mathf.Lerp(0.85f, 0.45f, depth / 0.55f));
            }
            for (int i = -2; i <= 2; i++)
            {
                float baseX = i * 0.24f;
                float wave = Mathf.Sin(d.y * 7f + i * 1.3f + phase) * 0.08f;
                float width = Mathf.Lerp(0.04f, 0.014f, depth);
                if (Mathf.Abs(d.x - (baseX + wave)) < width)
                    return new Color(core.r, core.g, core.b, Mathf.Lerp(0.8f, 0.2f, depth));
            }
            return Color.clear;
        }
    }
}
