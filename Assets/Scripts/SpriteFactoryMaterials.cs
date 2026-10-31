using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Материалы скинов — то, что видно ВНУТРИ пузырька.
    ///
    /// До этого все покупаемые скины были одним и тем же стеклянным шариком, которому
    /// меняли оттенок. Двадцать «разных» скинов отличались друг от друга ровно одним
    /// числом в палитре, и издалека их было не различить вовсе.
    ///
    /// Теперь каждый скин — свой материал со своей внутренней структурой: у лавы
    /// тёмная корка и светящиеся трещины, у льда — пузырьки воздуха и сколы, у
    /// туманности — звёзды. Рисуется всё той же процедурой, что и остальное в игре,
    /// так что ни одного байта графики в сборку не добавляется.
    ///
    /// Устроено слоями: этот спрайт — начинка, а поверх него лежит стеклянная
    /// оболочка (<see cref="Bubble"/>) с ободком и бликом. Начинка крутится и дышит,
    /// оболочка стоит на месте — отсюда ощущение, что внутри шарика что-то живёт.
    /// Нарисовать всё одним спрайтом было нельзя: вращать его целиком значило бы
    /// вращать и блик, а блик — это отражение, оно на месте.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>
        /// Начинка по материалу. Glass отдаёт null: у обычного пузырька начинки нет,
        /// он весь — стекло, и лишний прозрачный слой под ним ничего не добавит.
        /// </summary>
        public static Sprite MaterialCore(SkinMaterial material)
        {
            switch (material)
            {
                case SkinMaterial.Coral: return CoreCoral();
                case SkinMaterial.Pearl: return CorePearl();
                case SkinMaterial.Silt: return CoreSilt();
                case SkinMaterial.Sand: return CoreSand();
                case SkinMaterial.Tide: return CoreTide();
                case SkinMaterial.Ice: return CoreIce();
                case SkinMaterial.Toxin: return CoreToxin();
                case SkinMaterial.Neon: return CoreNeon();
                case SkinMaterial.Resin: return CoreResin();
                case SkinMaterial.Storm: return CoreStorm();
                case SkinMaterial.Ember: return CoreEmber();
                case SkinMaterial.Plasma: return CorePlasma();
                case SkinMaterial.Wraith: return CoreWraith();
                case SkinMaterial.Prism: return CorePrism();
                case SkinMaterial.Pixel: return CorePixel();
                case SkinMaterial.Nebula: return CoreNebula();
                case SkinMaterial.Magma: return CoreMagma();
                case SkinMaterial.Aurora: return CoreAurora();
                case SkinMaterial.Abyss: return CoreAbyss();
                case SkinMaterial.Leviathan: return CoreLeviathan();
                case SkinMaterial.Verdigris: return CoreVerdigris();
                case SkinMaterial.Cinnabar: return CoreCinnabar();
                case SkinMaterial.Obsidian: return CoreObsidian();
                case SkinMaterial.Brass: return CoreBrass();
                case SkinMaterial.Starlight: return CoreStarlight();
                default: return null;
            }
        }

        /// <summary>
        /// Оболочка начинки: круг с мягким краем. Все материалы рисуются через неё,
        /// чтобы ни один не вылез за пределы пузырька и не выдал квадрат текстуры.
        /// </summary>
        static Sprite Core(string key, Func<Vector2, Color> inner)
        {
            return Build("mat_" + key, p =>
            {
                float d = p.magnitude;
                if (d > 0.92f) return Color.clear;

                Color c = inner(p);
                // край начинки растворяется: под стеклом не бывает резкой границы
                c.a *= Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.74f, 0.92f, d));
                return c;
            });
        }

        /// <summary>
        /// Шум для материалов.
        ///
        /// Раньше здесь стояло произведение синуса на косинус — дёшево и совершенно
        /// неправильно: две волны под прямым углом дают регулярную решётку, и лава
        /// выглядела как оранжево-чёрная шахматка, а не как камень. Ровная сетка
        /// видна глазом мгновенно, в этом её беда.
        ///
        /// Теперь честный ценностный шум: решётка случайных чисел со сглаженной
        /// интерполяцией, три октавы. Считается один раз при запекании спрайта, так
        /// что цена не важна, а пятна получаются неповторяющимися.
        /// </summary>
        static float Hash(int x, int y)
        {
            // Детерминированно и без состояния: один и тот же узел решётки обязан
            // давать одно и то же число при каждом запуске, иначе материал будет
            // меняться от сессии к сессии.
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        static float ValueNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;

            // Сглаживание Эрмита: без него между узлами видны прямые грани.
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);

            float a = Hash(xi, yi), b = Hash(xi + 1, yi);
            float c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);

            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        /// <summary>Три октавы: крупные пятна плюс мелкая рябь. Возвращает −1…1.</summary>
        static float Wisp(Vector2 p, float scale, float seedX = 0f, float seedY = 0f)
        {
            float n = ValueNoise(p.x * scale + seedX, p.y * scale + seedY) * 0.55f
                    + ValueNoise(p.x * scale * 2.3f + 31.7f + seedX, p.y * scale * 2.3f + 11.3f + seedY) * 0.3f
                    + ValueNoise(p.x * scale * 4.7f + 71.1f + seedX, p.y * scale * 4.7f + 53.9f + seedY) * 0.15f;
            return n * 2f - 1f;
        }

        // ------------------------------------------------------------ обычные

        /// <summary>Коралл: живые ветви, расходящиеся от середины.</summary>
        static Sprite CoreCoral() => Core("coral", p =>
        {
            float ang = Mathf.Atan2(p.y, p.x);
            float r = p.magnitude;

            // пять ветвей, утолщающихся к краю
            float branch = Mathf.Abs(Mathf.Sin(ang * 2.5f + r * 2.2f));
            Color flesh = Palette.Hex("FF9EB5");
            Color deep = Palette.Hex("B83F5E");

            Color c = Color.Lerp(deep, flesh, branch);
            // поры
            if (Wisp(p, 9f) > 0.75f) c = Color.Lerp(c, Palette.Hex("FFD8E0"), 0.6f);
            c.a = 0.92f;
            return c;
        });

        /// <summary>Жемчуг: перламутр — цвет плывёт по кругу.</summary>
        static Sprite CorePearl() => Core("pearl", p =>
        {
            float ang = Mathf.Atan2(p.y, p.x);
            float r = p.magnitude;

            // радужный перелив: три пастельных тона, сменяющих друг друга по углу
            float t = Mathf.Repeat(ang / (Mathf.PI * 2f) + r * 0.35f, 1f);
            Color a = Palette.Hex("FFF2E0"), b = Palette.Hex("E8D8FF"), c3 = Palette.Hex("D8F0E8");
            Color c = t < 0.33f ? Color.Lerp(a, b, t / 0.33f)
                    : t < 0.66f ? Color.Lerp(b, c3, (t - 0.33f) / 0.33f)
                                : Color.Lerp(c3, a, (t - 0.66f) / 0.34f);

            c = Color.Lerp(Color.white, c, Mathf.Clamp01(r * 1.4f));
            c.a = 0.95f;
            return c;
        });

        /// <summary>Тина: мутная взвесь, в которой почти ничего не видно.</summary>
        static Sprite CoreSilt() => Core("silt", p =>
        {
            float n = Wisp(p, 3.2f, 5f) * 0.5f + 0.5f;
            Color c = Color.Lerp(Palette.Hex("3F5A2F"), Palette.Hex("9FB86B"), n);
            // редкие светлые хлопья
            if (Wisp(p, 11f, 41f) > 0.82f) c = Color.Lerp(c, Palette.Hex("D8E8B0"), 0.5f);
            c.a = 0.9f;
            return c;
        });

        /// <summary>Песок: слои осадка и крупинки.</summary>
        static Sprite CoreSand() => Core("sand", p =>
        {
            // горизонтальные слои разной плотности
            float layer = Mathf.Repeat(p.y * 3.4f + Mathf.Sin(p.x * 2f) * 0.2f, 1f);
            Color c = Color.Lerp(Palette.Hex("C9A86B"), Palette.Hex("F0DFB0"), layer);
            // крупинки
            if (Wisp(p, 17f, 11f) > 0.6f) c = Color.Lerp(c, Palette.Hex("8F7340"), 0.45f);
            c.a = 0.93f;
            return c;
        });

        /// <summary>Прилив: вода с поверхностью внутри шарика.</summary>
        static Sprite CoreTide() => Core("tide", p =>
        {
            // «уровень воды» с волной — выше него светлее, ниже темнее
            float surface = Mathf.Sin(p.x * 3.2f) * 0.08f + 0.12f;
            Color above = Palette.Hex("BFEFFF").WithAlpha(0.55f);
            Color below = Palette.Hex("2F7FB8");

            if (p.y > surface) return above;

            float depth = Mathf.InverseLerp(surface, -0.95f, p.y);
            Color c = Color.Lerp(Palette.Hex("6FC8E8"), below, depth);
            // блики ряби у самой поверхности
            if (p.y > surface - 0.08f) c = Color.Lerp(c, Color.white, 0.4f);
            c.a = 0.92f;
            return c;
        });

        // -------------------------------------------------------------- редкие

        /// <summary>Лёд: прозрачная толща с трещинами и вмёрзшими пузырьками.</summary>
        static Sprite CoreIce() => Core("ice", p =>
        {
            Color c = Color.Lerp(Palette.Hex("DFF4FF"), Palette.Hex("4A8FBF"),
                                 Mathf.InverseLerp(-0.9f, 0.9f, p.y - p.x * 0.4f));

            // сколы: прямые линии под разными углами
            var cracks = new[]
            {
                new[] { new Vector2(-0.6f, 0.5f), new Vector2(0.1f, -0.2f) },
                new[] { new Vector2(0.1f, -0.2f), new Vector2(0.55f, 0.35f) },
                new[] { new Vector2(0.1f, -0.2f), new Vector2(-0.2f, -0.75f) },
            };
            for (int i = 0; i < cracks.Length; i++)
                if (DistToSegment(p, cracks[i][0], cracks[i][1]) < 0.035f)
                    c = Color.Lerp(c, Color.white, 0.75f);

            // вмёрзшие пузырьки воздуха
            if (Vector2.Distance(p, new Vector2(0.32f, 0.28f)) < 0.09f) c = Color.Lerp(c, Color.white, 0.8f);
            if (Vector2.Distance(p, new Vector2(-0.38f, -0.3f)) < 0.06f) c = Color.Lerp(c, Color.white, 0.7f);

            c.a = 0.88f;
            return c;
        });

        /// <summary>Яд: бурлящая кислота с крупными пузырями.</summary>
        static Sprite CoreToxin() => Core("toxin", p =>
        {
            float n = Wisp(p, 4.5f, 17f) * 0.5f + 0.5f;
            Color c = Color.Lerp(Palette.Hex("2F6B1F"), Palette.Hex("A8FF5B"), n);

            // пузыри: кольца разного размера
            var bubbles = new[] { new Vector3(0.25f, 0.3f, 0.2f), new Vector3(-0.35f, -0.1f, 0.14f),
                                  new Vector3(0.05f, -0.45f, 0.11f) };
            for (int i = 0; i < bubbles.Length; i++)
            {
                float d = Vector2.Distance(p, new Vector2(bubbles[i].x, bubbles[i].y));
                if (Mathf.Abs(d - bubbles[i].z) < 0.03f) c = Color.Lerp(c, Palette.Hex("E8FFC0"), 0.8f);
                else if (d < bubbles[i].z) c = Color.Lerp(c, Palette.Hex("7FDF3F"), 0.35f);
            }
            c.a = 0.93f;
            return c;
        });

        /// <summary>Неон: тёмное нутро и светящиеся кольца.</summary>
        static Sprite CoreNeon() => Core("neon", p =>
        {
            float r = p.magnitude;
            Color c = Palette.Hex("1A0A28");

            // три кольца, каждое своего цвета
            float[] radii = { 0.28f, 0.48f, 0.68f };
            Color[] colors = { Palette.Hex("FF5BE8"), Palette.Hex("5BF0FF"), Palette.Hex("FFE05B") };
            for (int i = 0; i < radii.Length; i++)
            {
                float d = Mathf.Abs(r - radii[i]);
                if (d < 0.045f) c = Color.Lerp(c, colors[i], 1f - d / 0.045f);
                else if (d < 0.1f) c = Color.Lerp(c, colors[i], (1f - d / 0.1f) * 0.3f);   // ореол
            }
            c.a = 0.95f;
            return c;
        });

        /// <summary>Смола: янтарь с вмёрзшими соринками.</summary>
        static Sprite CoreResin() => Core("resin", p =>
        {
            Color c = Color.Lerp(Palette.Hex("8F4F10"), Palette.Hex("FFC878"),
                                 Mathf.InverseLerp(-0.9f, 0.7f, p.y + p.x * 0.3f));
            // потёки
            float flow = Mathf.Sin(p.x * 6f + p.y * 2f);
            if (flow > 0.55f) c = Color.Lerp(c, Palette.Hex("FFE8B0"), 0.35f);

            // включения: мелкие тёмные пятнышки
            if (Vector2.Distance(p, new Vector2(-0.2f, 0.22f)) < 0.07f) c = Palette.Hex("4A2A08");
            if (Vector2.Distance(p, new Vector2(0.3f, -0.26f)) < 0.05f) c = Palette.Hex("4A2A08");
            c.a = 0.94f;
            return c;
        });

        /// <summary>Гроза: тучи и молния внутри шара.</summary>
        static Sprite CoreStorm() => Core("storm", p =>
        {
            float n = Wisp(p, 3.8f, 23f) * 0.5f + 0.5f;
            Color c = Color.Lerp(Palette.Hex("2A3340"), Palette.Hex("9FB4CF"), n);

            // зигзаг молнии
            var bolt = new[]
            {
                new[] { new Vector2(-0.1f, 0.72f), new Vector2(0.14f, 0.16f) },
                new[] { new Vector2(0.14f, 0.16f), new Vector2(-0.14f, 0.08f) },
                new[] { new Vector2(-0.14f, 0.08f), new Vector2(0.1f, -0.68f) },
            };
            for (int i = 0; i < bolt.Length; i++)
            {
                float d = DistToSegment(p, bolt[i][0], bolt[i][1]);
                if (d < 0.03f) c = Color.white;
                else if (d < 0.08f) c = Color.Lerp(c, Palette.Hex("CFE4FF"), 1f - d / 0.08f);
            }
            c.a = 0.93f;
            return c;
        });

        // ----------------------------------------------------------- эпические

        /// <summary>Уголь: остывшая корка, сквозь которую пробивается жар.</summary>
        static Sprite CoreEmber() => Core("ember", p =>
        {
            float n = Wisp(p, 5f, 29f);
            Color crust = Palette.Hex("241A16");
            Color heat = Color.Lerp(Palette.Hex("FF7A3A"), Palette.Hex("FFE0A8"),
                                    Mathf.Clamp01(1f - p.magnitude));

            // трещины — там, где шум близок к нулю: получается сетка, а не пятна
            float seam = 1f - Mathf.Clamp01(Mathf.Abs(n) * 7f);
            Color c = Color.Lerp(crust, heat, seam);
            c.a = 0.95f;
            return c;
        });

        /// <summary>Плазма: дуги разряда, пляшущие внутри.</summary>
        static Sprite CorePlasma() => Core("plasma", p =>
        {
            Color c = Palette.Hex("1A1030");

            // дуги: линии постоянной фазы у искажённого поля
            for (int i = 0; i < 3; i++)
            {
                float phase = i * 2.1f;
                float field = Mathf.Sin(p.x * 5f + phase) + Mathf.Sin(p.y * 6.5f - phase) * 0.8f;
                float d = Mathf.Abs(field);
                if (d < 0.08f) c = Color.Lerp(c, Color.white, 1f - d / 0.08f);
                else if (d < 0.3f) c = Color.Lerp(c, Palette.Hex("9ADFFF"), (1f - d / 0.3f) * 0.45f);
            }
            c.a = 0.95f;
            return c;
        });

        /// <summary>Призрак: туман, сквозь который видно воду.</summary>
        static Sprite CoreWraith() => Core("wraith", p =>
        {
            float n = Wisp(p, 2.6f, 37f) * 0.5f + 0.5f;
            Color c = Color.Lerp(Palette.Hex("6F8F8A"), Palette.Hex("E8FFF8"), n);
            // полупрозрачен весь, и неравномерно: сквозь него должна читаться вода
            c.a = 0.22f + n * 0.4f;
            return c;
        });

        /// <summary>Призма: грани, каждая со своим преломлением.</summary>
        static Sprite CorePrism() => Core("prism", p =>
        {
            float ang = Mathf.Atan2(p.y, p.x);
            // шесть секторов — граней
            float facet = Mathf.Floor(Mathf.Repeat(ang / (Mathf.PI * 2f) * 6f, 6f));

            Color[] tints =
            {
                Palette.Hex("FFFFFF"), Palette.Hex("BFE8FF"), Palette.Hex("E0D0FF"),
                Palette.Hex("D0FFF0"), Palette.Hex("FFF0D0"), Palette.Hex("FFD8F0"),
            };
            Color c = tints[(int)facet % tints.Length];
            // рёбра между гранями
            float edge = Mathf.Repeat(ang / (Mathf.PI * 2f) * 6f, 1f);
            if (edge < 0.04f || edge > 0.96f) c = Color.Lerp(c, Palette.Hex("8FB8CF"), 0.6f);
            c = Color.Lerp(Color.white, c, Mathf.Clamp01(p.magnitude * 1.3f));
            c.a = 0.9f;
            return c;
        });

        /// <summary>Пиксель: крупная сетка клеток.</summary>
        static Sprite CorePixel() => Core("pixel", p =>
        {
            const float cell = 0.22f;
            float gx = Mathf.Floor((p.x + 1f) / cell);
            float gy = Mathf.Floor((p.y + 1f) / cell);

            // детерминированный «случайный» выбор цвета клетки
            float h = Mathf.Repeat(Mathf.Sin(gx * 12.9898f + gy * 78.233f) * 43758.5453f, 1f);
            Color c = h < 0.45f ? Palette.Hex("1F7F4F")
                    : h < 0.75f ? Palette.Hex("5BFF9A")
                    : h < 0.92f ? Palette.Hex("FF5BD8")
                                : Palette.Hex("0F2A1F");

            // шов между клетками
            float fx = Mathf.Repeat((p.x + 1f) / cell, 1f);
            float fy = Mathf.Repeat((p.y + 1f) / cell, 1f);
            if (fx < 0.08f || fy < 0.08f) c = Color.Lerp(c, Color.black, 0.35f);
            c.a = 0.96f;
            return c;
        });

        // --------------------------------------------------------- легендарные

        /// <summary>Туманность: рукава, звёздная пыль и редкие звёзды.</summary>
        static Sprite CoreNebula() => Core("nebula", p =>
        {
            float r = p.magnitude;
            float ang = Mathf.Atan2(p.y, p.x);

            // два рукава, закрученных к центру
            float arm = Mathf.Sin(ang * 2f - r * 5.5f) * 0.5f + 0.5f;
            Color c = Color.Lerp(Palette.Hex("140B2E"), Palette.Hex("B88FFF"), arm * (1f - r * 0.5f));
            c = Color.Lerp(c, Palette.Hex("5BC8FF"), Mathf.Clamp01(arm - 0.6f) * 1.2f);

            // ядро
            c = Color.Lerp(c, Palette.Hex("FFF0FF"), Mathf.Clamp01(1f - r * 4f));

            // звёзды: редкие яркие точки
            if (Mathf.Repeat(Mathf.Sin(p.x * 91.7f + p.y * 47.3f) * 43758.5453f, 1f) > 0.993f)
                c = Color.white;

            c.a = 0.96f;
            return c;
        });

        /// <summary>
        /// Магма: тёмная остывшая корка, сквозь трещины которой светится расплав.
        ///
        /// Соотношение здесь важнее цвета. Первая версия заливала шар расплавом и
        /// читалась как расплавленное стекло, а не как камень: лава узнаётся именно
        /// по контрасту — чёрная корка и тонкие раскалённые швы между плитами.
        /// Поэтому расплав живёт в узкой полосе около нуля шума, а всё остальное —
        /// базальт.
        /// </summary>
        static Sprite CoreMagma() => Core("magma", p =>
        {
            float n = Wisp(p, 3.2f, 47f);

            // Швы между плитами корки: узкая полоса около нуля. Чем меньше
            // множитель, тем шире шов; восемь даёт трещину, а не разлив.
            float seam = Mathf.Clamp01(1f - Mathf.Abs(n) * 8f);

            // Редкие озёра расплава: только там, где шум ушёл совсем низко.
            float pool = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.55f, -0.85f, n));

            float molten = Mathf.Clamp01(Mathf.Max(seam, pool));

            // Базальт: тёмный, с лёгким разбросом, чтобы не был плоской заливкой.
            float grain = Wisp(p, 9f, 83f) * 0.5f + 0.5f;
            Color crust = Color.Lerp(Palette.Hex("120C0A"), Palette.Hex("2E211C"), grain);

            // Расплав: от тёмно-красного по краю шва к белому в середине.
            Color lava = Color.Lerp(Palette.Hex("C42A06"), Palette.Hex("FFD24A"), molten);
            if (molten > 0.75f) lava = Color.Lerp(lava, Palette.Hex("FFF4C0"), (molten - 0.75f) * 4f);

            Color c = Color.Lerp(crust, lava, molten);

            // Жар изнутри: ближе к середине корка подсвечена, будто просвечивает.
            float glow = Mathf.Clamp01(1f - p.magnitude * 1.5f);
            c = Color.Lerp(c, Palette.Hex("FF6A20"), glow * 0.22f * (1f - molten));

            c.a = 0.97f;
            return c;
        });

        /// <summary>Сияние: ленты полярного света.</summary>
        static Sprite CoreAurora() => Core("aurora", p =>
        {
            // вертикальные ленты, изогнутые по синусу
            float band = p.x + Mathf.Sin(p.y * 2.6f) * 0.35f;
            float t = Mathf.Repeat(band * 1.2f + 0.5f, 1f);

            Color a = Palette.Hex("8FFFD8"), b = Palette.Hex("FF8FE8"), c3 = Palette.Hex("8FB8FF");
            Color c = t < 0.33f ? Color.Lerp(a, b, t / 0.33f)
                    : t < 0.66f ? Color.Lerp(b, c3, (t - 0.33f) / 0.33f)
                                : Color.Lerp(c3, a, (t - 0.66f) / 0.34f);

            // ленты ярче сверху и растворяются книзу, как настоящее сияние
            float fade = Mathf.InverseLerp(-0.9f, 0.6f, p.y);
            c = Color.Lerp(Palette.Hex("0A1A2A"), c, 0.25f + fade * 0.75f);
            c.a = 0.94f;
            return c;
        });

        /// <summary>Бездна: почти ничего, и это намеренно.</summary>
        static Sprite CoreAbyss() => Core("abyss", p =>
        {
            float r = p.magnitude;

            // провал в середине — чернее самой чёрной воды вокруг
            Color c = Color.Lerp(Palette.Hex("000006"), Palette.Hex("2A2440"), Mathf.Clamp01(r * 1.3f));

            // кольцо горизонта событий
            if (Mathf.Abs(r - 0.52f) < 0.04f) c = Color.Lerp(c, Palette.Hex("6F5BD0"), 0.8f);

            // редкие искры, затягиваемые внутрь
            if (Mathf.Repeat(Mathf.Sin(p.x * 61.3f + p.y * 37.9f) * 43758.5453f, 1f) > 0.988f)
                c = Color.Lerp(c, Palette.Hex("C8B8FF"), 0.9f);

            c.a = 0.97f;
            return c;
        });

        // ------------------------------------------- материалы прежних скинов

        /// <summary>Патина: окисленная медь разводами.</summary>
        static Sprite CoreVerdigris() => Core("verdigris", p =>
        {
            float n = Wisp(p, 3.4f, 53f) * 0.5f + 0.5f;
            Color c = Color.Lerp(Palette.Hex("1F6B4F"), Palette.Hex("8CF0B4"), n);
            // проступающая сквозь окисел медь
            if (n < 0.22f) c = Color.Lerp(c, Palette.Hex("B87333"), (0.22f - n) * 3.5f);
            c.a = 0.94f;
            return c;
        });

        /// <summary>Киноварь: красный минерал с прожилками.</summary>
        static Sprite CoreCinnabar() => Core("cinnabar", p =>
        {
            Color c = Color.Lerp(Palette.Hex("6B1208"), Palette.Hex("FF6B4A"),
                                 Mathf.InverseLerp(-0.9f, 0.9f, p.y + p.x * 0.4f));
            // кристаллические прожилки
            float vein = Mathf.Abs(Mathf.Sin(p.x * 7f + p.y * 4f));
            if (vein > 0.86f) c = Color.Lerp(c, Palette.Hex("FFC8A8"), (vein - 0.86f) * 7f);
            c.a = 0.95f;
            return c;
        });

        /// <summary>Обсидиан: чёрное стекло с раковистым изломом.</summary>
        static Sprite CoreObsidian() => Core("obsidian", p =>
        {
            Color c = Palette.Hex("14171C");
            // изломы: концентрические дуги от точки удара
            float d = Vector2.Distance(p, new Vector2(-0.25f, 0.3f));
            float shell = Mathf.Repeat(d * 5.5f, 1f);
            c = Color.Lerp(c, Palette.Hex("3A4150"), shell * 0.55f);
            // холодный блеск по краю излома
            if (shell > 0.92f) c = Color.Lerp(c, Palette.Hex("8FA8CF"), 0.5f);
            c.a = 0.96f;
            return c;
        });

        /// <summary>Латунь: полированный металл с потёртостями.</summary>
        static Sprite CoreBrass() => Core("brass", p =>
        {
            // анизотропный блеск: металл светится полосой поперёк
            float sheen = Mathf.Abs(Mathf.Sin(p.y * 2.2f - p.x * 0.6f));
            Color c = Color.Lerp(Palette.Hex("7A5516"), Palette.Hex("FFD98A"), sheen);
            // царапины
            if (Mathf.Repeat(p.y * 23f + p.x * 3f, 1f) > 0.93f) c = Color.Lerp(c, Palette.Hex("FFF4D0"), 0.4f);
            c.a = 0.96f;
            return c;
        });

        /// <summary>Звёздный свет: награда за полную коллекцию.</summary>
        static Sprite CoreStarlight() => Core("starlight", p =>
        {
            float r = p.magnitude;

            // лучи, расходящиеся от середины
            float ang = Mathf.Atan2(p.y, p.x);
            float rays = Mathf.Abs(Mathf.Sin(ang * 5f));
            Color c = Color.Lerp(Palette.Hex("FFC83D"), Palette.Hex("FFF6C0"), rays * (1f - r * 0.6f));

            // раскалённое ядро
            c = Color.Lerp(c, Color.white, Mathf.Clamp01(1f - r * 3.2f));

            // искры по полю
            if (Mathf.Repeat(Mathf.Sin(p.x * 73.1f + p.y * 29.7f) * 43758.5453f, 1f) > 0.99f)
                c = Color.white;

            c.a = 0.96f;
            return c;
        });

        /// <summary>Левиафан: чешуя и зрачок, который смотрит в ответ.</summary>
        static Sprite CoreLeviathan() => Core("leviathan", p =>
        {
            // чешуя: ряды полукружий со смещением через ряд
            const float step = 0.3f;
            float row = Mathf.Floor((p.y + 1f) / step);
            float offset = Mathf.Repeat(row, 2f) < 1f ? 0f : step * 0.5f;
            Vector2 cell = new Vector2(Mathf.Repeat(p.x + 1f + offset, step) - step * 0.5f,
                                       Mathf.Repeat(p.y + 1f, step) - step * 0.5f);

            float d = cell.magnitude;
            Color deep = Palette.Hex("0F4F5F");
            Color scale = Palette.Hex("3FD8C8");
            Color c = Color.Lerp(scale, deep, Mathf.Clamp01(d / (step * 0.6f)));
            if (d > step * 0.46f) c = Color.Lerp(c, Palette.Hex("073038"), 0.7f);

            // глаз: вертикальный зрачок в середине
            float eye = Vector2.Distance(p, new Vector2(0f, 0.05f));
            if (eye < 0.26f)
            {
                c = Color.Lerp(Palette.Hex("FFE08A"), Palette.Hex("C98A2E"), eye / 0.26f);
                if (Mathf.Abs(p.x) < 0.055f && Mathf.Abs(p.y - 0.05f) < 0.2f) c = Palette.Hex("08141A");
            }
            c.a = 0.97f;
            return c;
        });
    }
}
