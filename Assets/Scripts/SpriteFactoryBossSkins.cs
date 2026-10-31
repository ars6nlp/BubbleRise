using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Общие слои трофейных скинов: кайма, фактура, блик, тень, обводка и
    /// начинка.
    ///
    /// **Почему это второй проход, а не правка двадцати функций.** Силуэт у
    /// каждого трофея свой и должен таким остаться — это и есть отсылка к боссу.
    /// А кайма, фактура и тень у всех устроены одинаково и отличаются только
    /// числами из <see cref="BossSkinTable"/>. Второй проход по уже готовой
    /// текстуре даёт ровно это: двадцать силуэтов остаются как были, а слои
    /// появляются у всех сразу и одинаково. Новому боссу хватит строки в таблице.
    ///
    /// **Почему проход дешёвый.** Он идёт один раз на загрузке, по той же
    /// текстуре, что уже лежит в кэше, и результат кэшируется своим ключом. В
    /// кадре от него не остаётся ничего.
    ///
    /// **Края ищутся по альфе исходника.** Кайма — там, где рядом есть пустота;
    /// обводка — снаружи от края. Поэтому они ложатся и по внешнему контуру, и
    /// между щупальцами, не зная ничего про саму форму.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>Разрешение трофейных скинов. Их разглядывают в упор в витрине.</summary>
        public const int SkinSize = 256;

        /// <summary>Сколько пикселей занимает кайма при SkinSize.</summary>
        const int RimPixels = 7;

        /// <summary>Толщина тёмной обводки снаружи силуэта, в пикселях.</summary>
        const int OutlinePixels = 3;

        /// <summary>
        /// Трофейный скин целиком: силуэт плюс общие слои.
        ///
        /// Ключ свой, поэтому исходный силуэт остаётся в кэше нетронутым — им
        /// пользуются проверки и предпросмотр.
        /// </summary>
        static void BossSkinFinish(Color[] px, int size, BossSkinDef def, bool guardian)
        {
            int w = size, h = size;
            var sp = (Color[])px.Clone();

            int rim = Mathf.Max(2, RimPixels * w / SkinSize);
            int outline = Mathf.Max(1, OutlinePixels * w / SkinSize);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Color c = sp[i];
                    var p = new Vector2((x + 0.5f) / w * 2f - 1f, (y + 0.5f) / h * 2f - 1f);

                    if (c.a < 0.04f)
                    {
                        // Снаружи — только тонкая тёмная обводка, и только если
                        // рядом есть тело. Она отделяет скин от любого фона, а на
                        // тёмной воде ещё и держит силуэт.
                        px[i] = NearBody(sp, w, h, x, y, outline)
                            ? def.Deep.WithAlpha(0.72f)
                            : Color.clear;
                        continue;
                    }

                    float edge = EdgeNear(sp, w, h, x, y, rim);
                    float gloss = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01(
                        Vector2.Distance(p, new Vector2(-0.30f, 0.36f)) / 0.44f));

                    // --- фактура.
                    //
                    // Главное про неё — МАСКА, а не сам рисунок. Без маски
                    // правильная сетка ложилась на всё тело ровным обоем:
                    // осьминог выходил мячом для гольфа, черепаха — плиткой,
                    // и читалось это как узор, а не как деталь существа. [ТЕСТ]
                    //
                    // Маска гасит фактуру у края (там работает кайма), под
                    // бликом (там блик) и в середине (там видна начинка), а
                    // низкочастотный шум делает её неровной — пятнами, как на
                    // живом теле.
                    float motif = MotifAt(p, def);
                    if (motif > 0f)
                    {
                        float blob = 0.5f + 0.5f * Mathf.Sin(p.x * 2.3f + 1.7f)
                                                 * Mathf.Cos(p.y * 2.9f - 0.6f);
                        float mask = (1f - edge)
                                     * (1f - gloss * 0.65f)
                                     * Mathf.Lerp(0.3f, 1f, blob)
                                     * Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(p.magnitude / 0.5f));

                        c = Color.Lerp(c, def.Accent, motif * def.MotifStrength * mask);
                    }

                    // --- контурная кайма в цвете босса
                    if (edge > 0f)
                        c = Color.Lerp(c, def.Rim, edge * (guardian ? 0.5f : 0.44f));

                    // --- мягкий блик сверху-слева. Не белый: осветлённый тон
                    // тела, иначе на каждом трофее появляется одинаковое белое
                    // пятно, а у нас правило против пересвета.
                    if (gloss > 0f)
                        c = Color.Lerp(c, Lighten(def.Body, 0.55f), gloss * 0.3f);

                    // --- тень снизу: тело перестаёт быть плоской наклейкой
                    float shade = Mathf.Clamp01((-p.y - 0.1f) / 0.9f);
                    if (shade > 0f)
                        c = Color.Lerp(c, def.Deep, shade * 0.3f);

                    // --- середина просвечивает, чтобы была видна начинка
                    float centre = 1f - Mathf.Clamp01(p.magnitude / 0.62f);
                    float show = Mathf.Lerp(1f, def.CoreShow,
                                            Mathf.SmoothStep(0f, 1f, centre) * (1f - edge));
                    c.a *= guardian ? Mathf.Lerp(1f, show, 0.6f) : show;

                    px[i] = c;
                }
            }
        }

        /// <summary>Есть ли тело в окрестности — для обводки снаружи силуэта.</summary>
        static bool NearBody(Color[] px, int w, int h, int x, int y, int r)
        {
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                int sx = x + Mathf.RoundToInt(Mathf.Cos(a) * r);
                int sy = y + Mathf.RoundToInt(Mathf.Sin(a) * r);
                if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
                if (px[sy * w + sx].a > 0.5f) return true;
            }
            return false;
        }

        /// <summary>
        /// Насколько пиксель близок к краю тела, 0…1. Считается по альфе
        /// исходника, поэтому работает и по внешнему контуру, и между щупальцами.
        /// </summary>
        static float EdgeNear(Color[] px, int w, int h, int x, int y, int r)
        {
            float worst = 1f;
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI * 0.25f;
                int sx = x + Mathf.RoundToInt(Mathf.Cos(a) * r);
                int sy = y + Mathf.RoundToInt(Mathf.Sin(a) * r);

                // За границей текстуры считаем, что тела нет: край картинки — это
                // тоже край скина.
                float a2 = (sx < 0 || sy < 0 || sx >= w || sy >= h) ? 0f : px[sy * w + sx].a;
                if (a2 < worst) worst = a2;
            }
            return Mathf.SmoothStep(0f, 1f, 1f - worst);
        }

        static Color Lighten(Color c, float k)
            => new Color(Mathf.Lerp(c.r, 1f, k), Mathf.Lerp(c.g, 1f, k), Mathf.Lerp(c.b, 1f, k), c.a);

        /// <summary>
        /// Сила фактуры в точке, 0…1. Десять рисунков на десять боссов — повторов
        /// нет, иначе два трофея читались бы как один перекрашенный.
        /// </summary>
        static float MotifAt(Vector2 p, BossSkinDef def)
        {
            float s = def.MotifScale;

            switch (def.Motif)
            {
                case BossMotif.Veins:
                {
                    // Ветвистые нити: две синусоиды разной частоты дают развилки.
                    float v = Mathf.Sin(p.y * s + Mathf.Sin(p.x * s * 0.7f) * 2.2f);
                    return Mathf.Clamp01(1f - Mathf.Abs(v) * 7f);
                }

                case BossMotif.Scales:
                {
                    // Ряды налегающих дуг: сетка со сдвигом через строку.
                    float row = Mathf.Floor(p.y * s);
                    float ox = (Mathf.Repeat(row, 2f) < 1f) ? 0f : 0.5f;
                    float fx = Mathf.Repeat(p.x * s + ox, 1f) - 0.5f;
                    float fy = Mathf.Repeat(p.y * s, 1f);
                    float d = Mathf.Sqrt(fx * fx + (fy - 1f) * (fy - 1f) * 0.6f);
                    return Mathf.Clamp01(1f - Mathf.Abs(d - 0.62f) * 9f);
                }

                case BossMotif.Suckers:
                {
                    // Два ряда кружков вдоль формы.
                    float fx = Mathf.Repeat(p.x * s, 1f) - 0.5f;
                    float fy = Mathf.Repeat(p.y * s * 0.8f, 1f) - 0.5f;
                    float d = Mathf.Sqrt(fx * fx + fy * fy);
                    return Mathf.Clamp01(1f - Mathf.Abs(d - 0.24f) * 11f);
                }

                case BossMotif.Blades:
                {
                    // Длинные грани вдоль тела: узкие полосы под наклоном.
                    float v = Mathf.Repeat((p.x * 0.6f + p.y) * s, 1f);
                    return Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 8f) * 0.8f;
                }

                case BossMotif.Cracks:
                {
                    // Швы хитина: ломаные, а не прямые — отсюда вложенный синус.
                    float v = Mathf.Sin(p.x * s + Mathf.Sin(p.y * s * 1.6f) * 1.4f);
                    float u = Mathf.Sin(p.y * s * 0.8f - Mathf.Sin(p.x * s) * 1.1f);
                    return Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(v) * 10f),
                                     Mathf.Clamp01(1f - Mathf.Abs(u) * 12f));
                }

                case BossMotif.Runes:
                {
                    // Знаки: короткие штрихи по сетке, половина клеток пустая.
                    var cell = new Vector2(Mathf.Floor(p.x * s), Mathf.Floor(p.y * s));
                    float seed = Mathf.Repeat(Mathf.Sin(cell.x * 12.9898f + cell.y * 78.233f) * 43758.5f, 1f);
                    if (seed < 0.45f) return 0f;

                    float fx = Mathf.Repeat(p.x * s, 1f) - 0.5f;
                    float fy = Mathf.Repeat(p.y * s, 1f) - 0.5f;
                    bool bar = seed < 0.72f ? Mathf.Abs(fy) < 0.07f : Mathf.Abs(fx) < 0.07f;
                    return bar && Mathf.Abs(fx) < 0.3f && Mathf.Abs(fy) < 0.3f ? 1f : 0f;
                }

                case BossMotif.Teeth:
                {
                    // Ряд острых треугольников вдоль верхнего края тела.
                    if (p.y < 0.1f || p.y > 0.66f) return 0f;
                    float fx = Mathf.Repeat(p.x * s, 1f) - 0.5f;
                    float t = Mathf.InverseLerp(0.66f, 0.1f, p.y);
                    return Mathf.Abs(fx) < t * 0.42f ? 1f : 0f;
                }

                case BossMotif.Plates:
                {
                    // Соты: шестиугольная сетка по двум осям.
                    float a1 = Mathf.Repeat(p.y * s, 1f) - 0.5f;
                    float a2 = Mathf.Repeat((p.x * 0.866f + p.y * 0.5f) * s, 1f) - 0.5f;
                    float a3 = Mathf.Repeat((p.x * 0.866f - p.y * 0.5f) * s, 1f) - 0.5f;
                    float m = Mathf.Min(Mathf.Abs(a1), Mathf.Min(Mathf.Abs(a2), Mathf.Abs(a3)));
                    return Mathf.Clamp01(1f - m * 14f);
                }

                case BossMotif.Chevrons:
                {
                    // Плакоидная насечка: мелкие уголки, жёсткие и частые.
                    float fy = Mathf.Repeat(p.y * s, 1f);
                    float fx = Mathf.Repeat(p.x * s + fy * 0.5f, 1f) - 0.5f;
                    return Mathf.Abs(fx) < 0.16f && fy < 0.55f ? 0.9f : 0f;
                }

                default: // Spots
                {
                    // Крупные мягкие отметины вразнобой.
                    var cell = new Vector2(Mathf.Floor(p.x * s), Mathf.Floor(p.y * s));
                    float sx = Mathf.Repeat(Mathf.Sin(cell.x * 31.1f + cell.y * 17.7f) * 4317.3f, 1f);
                    float sy = Mathf.Repeat(Mathf.Sin(cell.x * 57.3f + cell.y * 91.1f) * 2731.9f, 1f);
                    var centre = new Vector2(Mathf.Lerp(0.25f, 0.75f, sx), Mathf.Lerp(0.25f, 0.75f, sy));
                    var f = new Vector2(Mathf.Repeat(p.x * s, 1f), Mathf.Repeat(p.y * s, 1f));
                    return Mathf.Clamp01(1f - Vector2.Distance(f, centre) * 4.2f);
                }
            }
        }

        // ------------------------------------------------------------ начинка

        /// <summary>
        /// Внутреннее течение трофея: то, что медленно вращается под телом.
        ///
        /// У трофеев начинки не было вовсе — <see cref="SkinDef.Trophy"/>
        /// оставляет материал стеклом, а у стекла начинки нет по замыслу. Из-за
        /// этого все десять трофеев были ровно одним плоским слоем, тогда как
        /// любой покупаемый скин за две тысячи имел два.
        ///
        /// Рисунок один, а разными его делают число рукавов, закрутка и палитра —
        /// то есть три числа из строки таблицы.
        /// </summary>
        public static Sprite BossCore(BossKind boss)
        {
            var def = BossSkinTable.Of(boss);
            string key = "boss_core_" + (int)boss;

            return Build(key, p =>
            {
                float d = p.magnitude;
                if (d > 0.9f) return Color.clear;

                float ang = Mathf.Atan2(p.y, p.x);

                // Рукава: угол закручивается тем сильнее, чем дальше от центра.
                float swirl = ang + d * def.CoreTwist * Mathf.PI;
                float arms = Mathf.Repeat(swirl / (Mathf.PI * 2f) * def.CoreArms, 1f);
                float band = Mathf.Clamp01(1f - Mathf.Abs(arms - 0.5f) * 2.6f);

                // Ядро плотнее краёв: течение собирается к середине.
                float core = Mathf.SmoothStep(0f, 1f, 1f - d / 0.9f);

                Color c = Color.Lerp(def.Deep, def.Glow, band * core);
                c = Color.Lerp(c, def.Glow, core * core * 0.45f);

                // Край начинки гаснет, иначе под телом видна её граница.
                c.a = Mathf.SmoothStep(0f, 1f, (0.9f - d) / 0.28f) * (0.5f + band * 0.5f);
                return c;
            }, size: SkinSize);
        }
    }
}
