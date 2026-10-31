using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Процедурная генерация всех спрайтов игры — арт-ассетов в проекте нет.
    /// Каждый спрайт рисуется в нормализованных координатах p ∈ [-1..1]^2 и
    /// создаётся с pixelsPerUnit == размеру текстуры, поэтому в мире занимает
    /// ровно 1x1 юнит: масштаб объекта = его диаметр.
    /// </summary>
    public static partial class SpriteFactory
    {
        const int Size = 128;   // разрешение текстуры
        const int SS = 3;       // суперсэмплинг (сглаживание краёв)

        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static Material _unlit;

        /// <summary>
        /// Общий unlit-материал. Важно для URP 2D: материал по умолчанию у SpriteRenderer —
        /// Sprite-Lit-Default, и без Light2D спрайты выглядят чёрными.
        /// </summary>
        public static Material Unlit
        {
            get
            {
                if (_unlit == null)
                {
                    // В URP правильный шейдер — 2D/Sprite-Unlit-Default; "Sprites/Default"
                    // оставлен запасным вариантом (он всегда есть в Always Included Shaders).
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    _unlit = new Material(sh) { name = "BubbleRise_Unlit" };
                }
                return _unlit;
            }
        }

        static Material _vertexColor;

        /// <summary>
        /// Материал для процедурных мешей (стены локаций). Текстура не задаётся —
        /// Sprites/Default умножает белую текстуру по умолчанию на цвет вершин.
        /// </summary>
        public static Material VertexColorUnlit
        {
            get
            {
                if (_vertexColor == null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    _vertexColor = new Material(sh) { name = "BubbleRise_VertexColor" };
                }
                return _vertexColor;
            }
        }

        public static void ClearCache()
        {
            Cache.Clear();
        }

        /// <summary>
        /// Отрисовать все спрайты заранее, на загрузке. Иначе первое препятствие и первый босс
        /// рисуют свои текстуры прямо в кадре — заметный подтормаживание.
        /// </summary>
        public static void Prewarm()
        {
            Disc(); Orb(); SoftDot();

            // Кольца оболочки столкновения: рисуются один раз на загрузке, как
            // и всё остальное. Иначе первый же удар легендарным скином стоил бы
            // кадра — ровно в тот момент, когда на экране и без того удар.
            EnvelopeRing(); EnvelopeSpeck();
            Bubble(); Guardian();
            GuardianGlass(); BubbleGlass();
            Rock(); Urchin(); CoralChunk();   // старые — для декораций и подсказок

            // Препятствия с прочностью и монета. Рисуются один раз на загрузке по той
            // же причине, что и краб: первая мина посреди заплыва иначе стоит кадра,
            // а монета рисуется в момент, когда на экране и без того взрыв.
            // Препятствия теперь с вариациями — греем их отдельной функцией.
            PrewarmHazards();
            Coin();

            // Начинки скинов. Их двадцать четыре, и рисуются они не дешевле боссовых:
            // без прогрева первый заход в магазин подвисал бы на листании страниц.
            for (int i = 0; i <= (int)SkinMaterial.Starlight; i++) MaterialCore((SkinMaterial)i);
            FishSprite(FishType.Piranha); FishSprite(FishType.Lurker); FishSprite(FishType.Jellyfish);
            LightShaft(); PlanktonMote(); WhaleSilhouette(); MantaSilhouette();
            // Медуза собрана из слоёв: купол, свечение, глаз, звено рукава,
            // две полосы бахромы и дымка. Один общий спрайт с запечёнными
            // щупальцами ей больше не нужен.
            JellyBell(); JellyBell(true); JellyGlow(); JellyEye(); JellyArm();
            JellyFringe(0); JellyFringe(1); JellyHaze(); JellyRipple();
            Ring(); DashedRing(); ElectricRing(); LightningBolt();
            SerpentHead(false); SerpentHead(true);
            SerpentBody(0, false); SerpentBody(1, false);
            SerpentBody(0, true); SerpentBody(1, true);
            SerpentTail(false); SerpentTail(true);
            OctopusHead(false); OctopusHead(true);
            TentacleSegment();
            TentacleTip(false); TentacleTip(true);
            Swordfish(false); Swordfish(true);
            // Краб рисуется дороже всех: без прогрева игра спотыкалась при его
            // появлении, при первом ударе и при первом уроне. Маски — для подмеса
            // при попадании (HitTint); суставам клешней она не нужна.
            CrabBody(false); CrabBody(true);
            CrabArmSegment();
            CrabClaw(false, false); CrabClaw(true, false);
            CrabClaw(false, true); CrabClaw(true, true);
            InkBoss(false); InkBoss(true); InkBlob(); SolidBlock();
            PiranhaSwarm(); Turtle();
            Shark(false, false); Shark(true, false); Shark(false, true); Shark(true, true);
            MantaBack(false); MantaBack(true); MantaBelly(false); MantaBelly(true);
            MantaGill(false); MantaGill(true); MantaPup();
            MantaSpike(); MantaEyeGlow();
            MantaLash(false); MantaLash(true); MantaOrb(false); MantaOrb(true);
            MantaSpark(); MantaSplitter();
            InkSplat(0); InkSplat(1); InkSplat(2);
            AimBeam(); AimChevron();
            CoralTile(0); CoralTile(1); CoralTile(2);
            CoralBlob(0); CoralBlob(1);
            CoralLimb(0); CoralLimb(1);
            CoralPillar(); RockLedge(); RuinBlocks(); RuinArch(); Kelp();
            SideCrab(0); SideCrab(1); SideCrab(2);
            // Трофейные скины: их видно в магазине сразу, поэтому греем все.
            // Границу берём из самого перечисления: с прежним «до Turtle» каждый
            // новый босс молча оставлял свою пару скинов холодной.
            foreach (SkinArt art in Enum.GetValues(typeof(SkinArt)))
            {
                BubbleSkin(art);
                GuardianSkin(art);
            }

            // Начинка трофеев: десять текстур, которые иначе рисовались бы в
            // момент открытия витрины.
            foreach (BossKind boss in Enum.GetValues(typeof(BossKind))) BossCore(boss);
            Vignette();
            SpeakerIcon(false); SpeakerIcon(true);
            RoundedRect("btn", 0.5f);
            RoundedRect("bar", 0.5f);
            RoundedOutline("hud", 0.35f, 0.06f);
            VignetteSolid();
            PrewarmRewards();
            PrewarmShapes();
        }

        // ---------------------------------------------------------------- ядро

        static Sprite Build(string key, Func<Vector2, Color> shade, Vector4 border = default, int size = Size)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            return Upload(key, Render(Masked(key, shade), size), size, border);
        }

        /// <summary>Окончание ключа маски: та же форма, белая, с той же прозрачностью.</summary>
        public const string MaskSuffix = "_mask";

        /// <summary>Ключ спрайта или его маски.</summary>
        static string MaskKey(string key, bool mask) => mask ? key + MaskSuffix : key;

        /// <summary>
        /// Маска вместо светлой копии. Боссы при попадании больше не подменяют
        /// спрайт: поверх тела ложится его силуэт, окрашенный в один цвет с
        /// ограниченной яркостью (<see cref="HitTint"/>). Силуэт рисуется той же
        /// функцией формы, поэтому совпадает с телом до пикселя, а цвета в нём нет
        /// вовсе — его задаёт только подмес.
        /// </summary>
        static Func<Vector2, Color> Masked(string key, Func<Vector2, Color> shade)
        {
            if (!key.EndsWith(MaskSuffix, StringComparison.Ordinal)) return shade;
            return p =>
            {
                Color c = shade(p);
                return new Color(1f, 1f, 1f, c.a);
            };
        }

        /// <summary>Маска к готовому спрайту, если её нарисовали. Иначе null.</summary>
        public static Sprite MaskOf(Sprite sprite)
        {
            if (sprite == null) return null;
            return Cache.TryGetValue(sprite.name + MaskSuffix, out var mask) ? mask : null;
        }

        /// <summary>
        /// Посчитать пиксели по функции цвета. Вынесено из <see cref="Build"/>,
        /// потому что трофейным скинам нужен ещё один проход ПО БУФЕРУ: готовая
        /// текстура уже не читается — <c>Apply(false, true)</c> освобождает
        /// CPU-копию, и <c>GetPixels</c> по ней падает.
        /// </summary>
        static Color[] Render(Func<Vector2, Color> shade, int size)
        {
            var px = new Color[size * size];
            float inv = 1f / size;
            const int n = SS * SS;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < SS; sy++)
                    {
                        for (int sx = 0; sx < SS; sx++)
                        {
                            float u = (x + (sx + 0.5f) / SS) * inv * 2f - 1f;
                            float v = (y + (sy + 0.5f) / SS) * inv * 2f - 1f;
                            Color c = shade(new Vector2(u, v));
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }
                    px[y * size + x] = a > 0.0001f
                        ? new Color(r / a, g / a, b / a, a / n)
                        : new Color(0f, 0f, 0f, 0f);
                }
            }

            return px;
        }

        /// <summary>Залить готовый буфер в текстуру и завести спрайт.</summary>
        static Sprite Upload(string key, Color[] px, int size, Vector4 border = default)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "tex_" + key
            };

            tex.SetPixels(px);
            tex.Apply(false, true); // больше не читаем — освобождаем CPU-копию

            var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                       size, 0, SpriteMeshType.FullRect, border);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Прямоугольная версия Build для декораций. Больший размер текстуры соответствует
        /// диапазону [-1..1], меньший сжимается пропорционально — форма не искажается.
        /// </summary>
        static Sprite BuildRect(string key, int w, int h, Func<Vector2, Color> shade, int ss = 2)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;
            shade = Masked(key, shade);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "tex_" + key
            };

            var px = new Color[w * h];
            int n = ss * ss;
            float aspectU = w >= h ? 1f : (float)w / h;
            float aspectV = w >= h ? (float)h / w : 1f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < ss; sy++)
                    {
                        for (int sx = 0; sx < ss; sx++)
                        {
                            float u = ((x + (sx + 0.5f) / ss) / w * 2f - 1f) * aspectU;
                            float v = ((y + (sy + 0.5f) / ss) / h * 2f - 1f) * aspectV;
                            Color c = shade(new Vector2(u, v));
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                        }
                    }
                    px[y * w + x] = a > 0.0001f
                        ? new Color(r / a, g / a, b / a, a / n)
                        : new Color(0f, 0f, 0f, 0f);
                }
            }

            tex.SetPixels(px);
            tex.Apply(false, true);

            // pixelsPerUnit по большей стороне: при scale = 1 объект занимает 1 юнит по ней
            float ppu = Mathf.Max(w, h);
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                                       ppu, 0, SpriteMeshType.FullRect);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }

        // ------------------------------------------------------------ примитивы

        static Color Blend(Color under, Color over)
        {
            float a = over.a + under.a * (1f - over.a);
            if (a <= 0.0001f) return new Color(0, 0, 0, 0);
            Color rgb = (over * over.a + under * under.a * (1f - over.a)) / a;
            rgb.a = a;
            return rgb;
        }

        static bool InPolygon(Vector2 p, Vector2[] poly)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            }
            return inside;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t);
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
            float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
            float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0;
            bool pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        // -------------------------------------------------------------- спрайты

        /// <summary>Белый круг под тинт (частицы, снаряды, иконки жизней).</summary>
        public static Sprite Disc() => Build("disc", p => p.magnitude <= 1f ? Color.white : Color.clear);

        /// <summary>Белая сфера с радиальным градиентом под тинт (снаряды).</summary>
        public static Sprite Orb() => Build("orb", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;
            float shade = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.3f, 0.3f)) / 1.4f));
            return new Color(shade, shade, shade, 1f);
        });

        /// <summary>Мягкая точка с затуханием к краям (частицы, фоновые пузырьки).</summary>
        public static Sprite SoftDot() => Build("softdot", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;
            float a = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - 0.35f) / 0.65f));
            return new Color(1f, 1f, 1f, a);
        });

        /// <summary>Пузырёк-игрок: стеклянная сфера с бликом.</summary>
        public static Sprite Bubble() => Build("bubble", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            // радиальный градиент со смещённым центром (как в прототипе)
            float gd = Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.3f, 0.4f)) / 1.35f);
            Color c;
            if (gd < 0.4f)
                c = Color.Lerp(new Color(1f, 1f, 1f, 0.90f), new Color(0.706f, 0.941f, 0.902f, 0.55f), gd / 0.4f);
            else
                c = Color.Lerp(new Color(0.706f, 0.941f, 0.902f, 0.55f), Palette.Aqua.WithAlpha(0.28f), (gd - 0.4f) / 0.6f);

            // ободок
            if (d > 0.93f) c = Blend(c, new Color(1f, 1f, 1f, 0.70f * Mathf.InverseLerp(0.93f, 0.97f, Mathf.Min(d, 0.97f))));

            // блик
            Vector2 h = new Vector2((p.x + 0.32f) / 0.22f, (p.y - 0.36f) / 0.13f);
            if (h.sqrMagnitude < 1f) c = Blend(c, new Color(1f, 1f, 1f, 0.85f));

            return c;
        });

        /// <summary>Страж: яркая аква-сфера с ободком и внешним свечением.</summary>
        public static Sprite Guardian() => Build("guardian", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            Color c;
            if (d < 0.70f)
            {
                float t = d / 0.70f;
                c = Color.Lerp(new Color(0.92f, 1f, 0.98f, 1f), Palette.Aqua, t);
                c.a = 1f;
            }
            else if (d < 0.85f)
            {
                c = new Color(0.61f, 1f, 0.94f, 1f); // яркий ободок
            }
            else
            {
                float t = (d - 0.85f) / 0.15f;
                c = Palette.Aqua.WithAlpha(0.55f * (1f - t));
            }

            Vector2 h = new Vector2((p.x + 0.26f) / 0.20f, (p.y - 0.30f) / 0.14f);
            if (h.sqrMagnitude < 1f) c = Blend(c, new Color(1f, 1f, 1f, 0.75f));

            return c;
        });

        /// <summary>
        /// Стеклянная оболочка пузырька: почти прозрачная, с ободком и бликом.
        ///
        /// Обычный <see cref="Bubble"/> залит бирюзой с непрозрачностью от 0.55 в
        /// середине до 0.9 под бликом. Поверх материала это работает как молочное
        /// стекло: красная киноварь, зелёная патина и радужное сияние выходили
        /// одинаковыми бледно-бирюзовыми шариками — ровно то, что было видно в
        /// магазине. Проверено складыванием слоёв вне Unity, а не на глаз.
        ///
        /// Здесь заливка снижена до одной десятой: материал виден целиком, а
        /// стеклом шарик остаётся за счёт ободка и блика — именно они и читаются
        /// как стекло, а не мутная заливка.
        /// </summary>
        public static Sprite BubbleGlass() => Build("bubble_glass", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            // Лёгкая дымка, чуть гуще к краю: объём без потери цвета начинки.
            float haze = Mathf.Lerp(0.07f, 0.16f, d);
            Color c = new Color(0.82f, 0.98f, 0.96f, haze);

            // Ободок — главное, что делает шарик шариком.
            if (d > 0.9f)
            {
                float t = Mathf.InverseLerp(0.9f, 0.97f, Mathf.Min(d, 0.97f));
                c = new Color(1f, 1f, 1f, 0.12f + t * 0.5f);
            }

            // Блик: второе, по чему читается стекло.
            Vector2 h = new Vector2((p.x + 0.32f) / 0.22f, (p.y - 0.36f) / 0.13f);
            if (h.sqrMagnitude < 1f) c = Blend(c, new Color(1f, 1f, 1f, 0.8f));

            return c;
        });

        /// <summary>
        /// Стеклянная оболочка стража: кольцо и блик, середина прозрачная.
        ///
        /// Обычный <see cref="Guardian"/> залит аквой до непрозрачности, и под ним
        /// не видно ничего. Именно поэтому все скины с материалом выглядели одним и
        /// тем же бирюзовым шариком: начинка лежала снизу, оболочка закрывала её
        /// целиком — и в магазине, и в игре.
        ///
        /// Эта версия показывает материал: от середины к краю почти прозрачная,
        /// ободок яркий (по нему читается радиус защиты), блик на месте.
        /// </summary>
        public static Sprite GuardianGlass() => Build("guardian_glass", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            Color c;
            if (d < 0.74f)
            {
                // лёгкая дымка поверх начинки — стекло, а не пустота
                float t = d / 0.74f;
                c = new Color(0.86f, 1f, 0.98f, 0.1f + t * 0.08f);
            }
            else if (d < 0.9f)
            {
                // ободок: он и показывает, где работает защита
                float t = Mathf.InverseLerp(0.74f, 0.82f, d);
                c = new Color(0.61f, 1f, 0.94f, 0.35f + t * 0.5f);
            }
            else
            {
                float t = (d - 0.9f) / 0.1f;
                c = Palette.Aqua.WithAlpha(0.5f * (1f - t));
            }

            Vector2 h = new Vector2((p.x + 0.26f) / 0.20f, (p.y - 0.30f) / 0.14f);
            if (h.sqrMagnitude < 1f) c = Blend(c, new Color(1f, 1f, 1f, 0.7f));

            return c;
        });

        /// <summary>Камень — неровный семиугольник.</summary>
        public static Sprite Rock()
        {
            const int pts = 7;
            var poly = new Vector2[pts];
            for (int i = 0; i < pts; i++)
            {
                float ang = (i / (float)pts) * Mathf.PI * 2f;
                float rr = 0.78f + 0.2f * Mathf.Sin(i * 2.1f);
                poly[i] = new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr);
            }
            return Build("rock", p =>
            {
                if (!InPolygon(p, poly)) return Color.clear;
                Color c = Palette.Rock;
                // тень снизу
                c = Color.Lerp(c * 0.72f, c, Mathf.InverseLerp(-0.9f, 0.5f, p.y));
                c.a = 1f;
                // блик
                if (Vector2.Distance(p, new Vector2(-0.28f, 0.28f)) < 0.35f)
                    c = Blend(c, new Color(1f, 1f, 1f, 0.14f));
                return c;
            });
        }

        /// <summary>Морской ёж — тёмный шарик с иглами.</summary>
        public static Sprite Urchin()
        {
            const int spikes = 10;
            var a = new Vector2[spikes];
            var b = new Vector2[spikes];
            for (int i = 0; i < spikes; i++)
            {
                float ang = (i / (float)spikes) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                a[i] = dir * 0.42f;
                b[i] = dir * 0.97f;
            }
            return Build("urchin", p =>
            {
                float d = p.magnitude;
                if (d < 0.50f)
                {
                    Color c = Palette.UrchinBody;
                    if (Vector2.Distance(p, new Vector2(-0.17f, 0.17f)) < 0.16f)
                        c = Blend(c, new Color(1f, 1f, 1f, 0.18f));
                    c.a = 1f;
                    return c;
                }
                for (int i = 0; i < spikes; i++)
                {
                    if (DistToSegment(p, a[i], b[i]) < 0.045f) return Palette.UrchinBody;
                }
                return Color.clear;
            });
        }

        /// <summary>Кусок коралла — брусок с шипами сверху.</summary>
        public static Sprite CoralChunk()
        {
            return Build("coral", p =>
            {
                // шипы сверху
                for (int i = -1; i <= 1; i++)
                {
                    float x0 = i * 0.6f;
                    if (InTriangle(p, new Vector2(x0 - 0.18f, 0.6f), new Vector2(x0 + 0.18f, 0.6f), new Vector2(x0, 1.08f)))
                        return Palette.CoralTip;
                }
                if (Mathf.Abs(p.x) <= 0.98f && Mathf.Abs(p.y) <= 0.6f)
                {
                    Color c = Palette.CoralBody;
                    if (p.y < -0.2f && p.y > -0.6f) c = Blend(c, new Color(0f, 0f, 0f, 0.18f));
                    if (p.y > 0.42f) c = Blend(c, new Color(1f, 1f, 1f, 0.10f));
                    c.a = 1f;
                    return c;
                }
                return Color.clear;
            });
        }

        // ------------------------------------------------- препятствия с прочностью

        /// <summary>
        /// Монета — древняя морская находка, а не золотой кружок из платформера.
        ///
        /// Что делает её «морской», а не просто жёлтой: восьмигранный обод со
        /// щербиной (её били и роняли), патина — зелёные разводы поверх бронзы,
        /// гравировка волны и перламутровая вставка в середине. Ровный блестящий
        /// диск читался бы как иконка из другой игры: в этом мире всё либо живое,
        /// либо пролежало на дне не одну сотню лет.
        ///
        /// Рисунок один на всю игру: он же в счётчике баланса, он же вылетает из
        /// разбитого препятствия, он же стоит в ценнике магазина. Разные монеты в
        /// разных местах игрок прочитал бы как разные валюты.
        /// </summary>
        public static Sprite Coin()
        {
            // Восьмигранник: монету такого возраста чеканили не идеально круглой.
            const int sides = 8;
            var poly = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float ang = (i / (float)sides) * Mathf.PI * 2f + 0.22f;
                // лёгкий разнобой радиуса — та самая «битость» обода
                float rr = 0.9f + 0.045f * Mathf.Sin(i * 2.7f);
                poly[i] = new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr);
            }

            Color bronze = Palette.Hex("C9922E");
            Color bronzeDark = Palette.Hex("7A5516");
            Color patina = Palette.Hex("4FBFA8");
            Color pearl = Palette.Hex("EAF7F2");

            return Build("coin", p =>
            {
                float r = p.magnitude;

                // Мягкое свечение вокруг: монета светится в тёмной воде, иначе на
                // глубине её не видно вовсе.
                if (!InPolygon(p, poly))
                {
                    if (r > 1f) return Color.clear;
                    float halo = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.9f, 1f, r));
                    return new Color(patina.r, patina.g, patina.b, halo * 0.3f);
                }

                // Щербина: кусок обода выломан. Одна, в фиксированном месте —
                // случайная заставила бы перерисовывать текстуру ради каждой монеты.
                if (Vector2.Distance(p, new Vector2(0.74f, 0.52f)) < 0.26f)
                    return new Color(patina.r, patina.g, patina.b, 0.22f);

                Color c = Color.Lerp(bronzeDark, bronze, Mathf.InverseLerp(-0.9f, 0.9f, p.y - p.x * 0.35f));

                // Патина разводами, а не ровным слоем: шум из двух синусов даёт
                // пятна, похожие на налёт, и стоит дешевле любой текстуры.
                float stain = Mathf.Sin(p.x * 5.3f + p.y * 3.1f) * Mathf.Cos(p.y * 4.7f - p.x * 2.3f);
                if (stain > 0.35f) c = Color.Lerp(c, patina, (stain - 0.35f) * 1.1f);

                // Обод: двойное кольцо, между ними насечка.
                float rim = Mathf.Abs(r - 0.8f);
                if (rim < 0.06f) c = Color.Lerp(c, bronzeDark, 0.55f);
                else if (rim < 0.12f && Mathf.Repeat(Mathf.Atan2(p.y, p.x) * 9f, 1f) < 0.45f)
                    c = Color.Lerp(c, bronzeDark, 0.3f);

                // Гравировка — волна: две дуги, набегающие друг на друга.
                if (r < 0.62f)
                {
                    float wave = p.y - Mathf.Sin(p.x * 4.2f) * 0.17f;
                    if (Mathf.Abs(wave + 0.1f) < 0.055f) c = Color.Lerp(c, bronzeDark, 0.75f);
                    if (Mathf.Abs(wave - 0.22f) < 0.045f) c = Color.Lerp(c, bronzeDark, 0.55f);
                }

                // Перламутровая вставка: единственное светлое пятно, по нему монета
                // узнаётся даже размером в десяток точек.
                float inlay = Vector2.Distance(p, new Vector2(-0.02f, -0.3f));
                if (inlay < 0.16f)
                {
                    float t = 1f - inlay / 0.16f;
                    c = Color.Lerp(c, pearl, 0.35f + t * 0.55f);
                }

                // Косой блик по всей поверхности.
                if (Mathf.Abs(p.y - p.x * 0.85f - 0.34f) < 0.08f)
                    c = Blend(c, new Color(1f, 1f, 1f, 0.3f));

                c.a = 1f;
                return c;
            });
        }


        /// <summary>Разряд молнии. Смотрит в +X, поворачивается по вектору скорости.</summary>
        public static Sprite LightningBolt() => BuildRect("bolt", 160, 72, p =>
        {
            // ломаная от хвоста к острию
            Vector2 a = new Vector2(-1.00f, 0.02f);
            Vector2 b = new Vector2(-0.36f, 0.20f);
            Vector2 c = new Vector2(0.08f, -0.13f);
            Vector2 d = new Vector2(0.56f, 0.16f);
            Vector2 e = new Vector2(1.00f, -0.02f);

            float dist = Mathf.Min(
                Mathf.Min(DistToSegment(p, a, b), DistToSegment(p, b, c)),
                Mathf.Min(DistToSegment(p, c, d), DistToSegment(p, d, e)));

            if (dist < 0.055f) return Color.white;                       // ядро
            if (dist < 0.145f) return new Color(1f, 1f, 1f, 0.55f);      // свечение
            if (dist < 0.215f) return new Color(1f, 1f, 1f, 0.18f);      // ореол
            return Color.clear;
        }, 3);

        /// <summary>
        /// Кольцо-корона с зубцами: поле молний вокруг медузы. Два таких кольца,
        /// вращаясь навстречу друг другу, дают ощущение потрескивающего разряда.
        /// </summary>
        public static Sprite ElectricRing() => Build("electric_ring", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            float ang = Mathf.Atan2(p.y, p.x);
            // зубцы по внешнему краю
            float spikes = 0.055f * (0.5f + 0.5f * Mathf.Cos(ang * 16f));
            float outer = 0.90f + spikes;
            float inner = 0.70f;

            if (d > outer) return Color.clear;
            if (d < inner) return new Color(1f, 1f, 1f, 0.05f);

            float band = Mathf.InverseLerp(inner, outer, d);
            float a = Mathf.SmoothStep(0f, 1f, band) * Mathf.SmoothStep(1f, 0f,
                        Mathf.InverseLerp(0.88f, 1f, band));
            return new Color(1f, 1f, 1f, Mathf.Clamp01(0.15f + a * 0.85f));
        });

        /// <summary>
        /// Пунктирное кольцо неуязвимости вокруг пузырька (A-09): двенадцать
        /// штрихов с мягкими краями. Пунктир, а не сплошное, — чтобы не путать с
        /// аурой скина и с кольцом урона.
        /// </summary>
        public static Sprite DashedRing() => Build("ring_dashed", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;
            float band = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.80f, 0.88f, d))
                         * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.92f, 0.99f, d));
            float seg = Mathf.Repeat((Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2f) + 1f) * 12f, 1f);
            float dash = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.15f, seg))
                         * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.55f, 0.65f, seg));
            return new Color(1f, 1f, 1f, band * dash);
        });

        /// <summary>Кольцо-аура: поле отталкивания вокруг босса. Тинтуется через SpriteRenderer.</summary>
        public static Sprite Ring() => Build("ring", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 0.97f, d));
            float fade = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.97f, 1f, d));
            return new Color(1f, 1f, 1f, Mathf.Max(0.05f, edge * fade));
        });

        /// <summary>
        /// Мягкий ободок для оболочки столкновения.
        ///
        /// **Зачем он есть.** Тень и ядро оболочки — это кольца, и собирались они
        /// из полусотни отдельных точек: восемнадцать на тень, тридцать на ядро.
        /// Полсотни объектов, которым каждый кадр переписывали Transform и цвет,
        /// ради фигуры, которая целиком помещается в одну текстуру. Теперь это
        /// один спрайт на слой — вид тот же, а объектов в пятьдесят раз меньше.
        ///
        /// Полоса стоит на d = 0.72 и гаснет к 0.52 и 0.92, то есть при масштабе S
        /// лежит на радиусе 0.36·S и имеет ширину 0.2·S.
        /// </summary>
        public static Sprite EnvelopeRing() => Build("envring", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.72f) / 0.2f);
            return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, a));
        });

        /// <summary>
        /// Тот же ободок, но разбитый на дольки.
        ///
        /// Ядро раньше красило каждую четвёртую точку в акцентный цвет. Цвет у
        /// спрайта один, поэтому акцент вынесен во второй слой: та же полоса с
        /// прорезями, своим тоном поверх основной.
        /// </summary>
        public static Sprite EnvelopeSpeck() => Build("envspeck", p =>
        {
            float d = p.magnitude;
            if (d > 1f) return Color.clear;

            float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.72f) / 0.2f);
            if (a <= 0f) return Color.clear;

            // Девять долек по кругу — примерно столько акцентных точек и было.
            float ang = Mathf.Atan2(p.y, p.x) / (Mathf.PI * 2f) + 0.5f;
            float slot = Mathf.Repeat(ang * 9f, 1f);
            float cut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((0.45f - Mathf.Abs(slot - 0.5f)) / 0.2f));

            return new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, a) * cut);
        });

        // -------------------------------------------------------------- рыбы

        /// <summary>
        /// Рыба, смотрит вправо (+X): при движении влево спрайт переворачивается через flipY.
        /// Piranha — быстрая зубастая мелочь, Lurker — крупная тёмная тварь со светящимся глазом.
        /// </summary>
        public static Sprite FishSprite(FishType type)
        {
            if (type == FishType.Jellyfish) return JellyfishSprite();

            bool piranha = type == FishType.Piranha;
            Color body = piranha ? Palette.PiranhaBody : Palette.LurkerBody;
            Color fin = piranha ? Palette.PiranhaFin : Palette.LurkerFin;
            Color eyeGlow = piranha ? Color.white : Palette.Gold;

            return Build(piranha ? "fish_piranha" : "fish_lurker", p =>
            {
                // хвост
                if (InTriangle(p, new Vector2(-0.52f, 0f), new Vector2(-1.0f, 0.46f), new Vector2(-1.0f, -0.46f)))
                    return fin;

                // спинной плавник
                if (InTriangle(p, new Vector2(-0.22f, 0.24f), new Vector2(0.16f, 0.26f), new Vector2(-0.06f, 0.62f)))
                    return fin;
                // брюшной плавник
                if (InTriangle(p, new Vector2(-0.18f, -0.24f), new Vector2(0.10f, -0.26f), new Vector2(-0.10f, -0.52f)))
                    return fin;

                // тело — эллипс
                float ex = (p.x + 0.04f) / 0.66f;
                float ey = p.y / 0.36f;
                if (ex * ex + ey * ey > 1f) return Color.clear;

                Color c = Color.Lerp(body * 0.7f, body, Mathf.InverseLerp(-0.34f, 0.3f, p.y));
                c.a = 1f;

                // пасть с зубами у носа
                if (p.x > 0.30f && p.y < 0.02f && p.y > -0.20f)
                {
                    c = Palette.BossMaw;
                    for (int i = 0; i < 3; i++)
                    {
                        float x0 = 0.36f + i * 0.10f;
                        if (InTriangle(p, new Vector2(x0 - 0.032f, 0.02f), new Vector2(x0 + 0.032f, 0.02f),
                                          new Vector2(x0, -0.12f)))
                            c = Palette.BossTeeth;
                    }
                    return c;
                }

                // глаз
                float de = Vector2.Distance(p, new Vector2(0.26f, 0.12f));
                if (de < 0.11f)
                {
                    c = eyeGlow;
                    if (Vector2.Distance(p, new Vector2(0.29f, 0.12f)) < 0.05f)
                        c = new Color(0.06f, 0.02f, 0.04f, 1f);
                    c.a = 1f;
                    return c;
                }

                // полоски на боку
                if (piranha && Mathf.Repeat(p.x * 6.5f, 1f) < 0.28f)
                    c = Blend(c, new Color(0f, 0f, 0f, 0.16f));

                return c;
            });
        }

        // ------------------------------------------------- коралловые блоки

        /// <summary>
        /// Блок коралловой постройки. Из таких блоков собираются фигуры (квадрат, ромб,
        /// крест, арка...). Три варианта, чтобы кладка не выглядела штампованной:
        /// 0 — обычный, 1 — с отростками сверху, 2 — с полостью внутри.
        /// </summary>
        public static Sprite CoralTile(int variant)
        {
            variant = Mathf.Clamp(variant, 0, 2);
            var pores = PorePattern(variant);

            return Build("coral_tile_" + variant, p =>
            {
                // отростки сверху — только у варианта 1, торчат над блоком
                if (variant == 1)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float x0 = i * 0.42f + 0.05f;
                        float top = 0.99f - Mathf.Abs(i) * 0.09f;
                        if (TaperedSegment(p, new Vector2(x0, 0.72f), new Vector2(x0 + i * 0.09f, top), 0.09f, 0.05f) ||
                            Vector2.Distance(p, new Vector2(x0 + i * 0.09f, top)) < 0.07f)
                            return Palette.CoralTip;
                    }
                }

                // тело блока: скруглённый квадрат с неровным краем
                float wobble = Mathf.Sin(p.x * 7.3f + variant) * 0.018f + Mathf.Cos(p.y * 6.1f) * 0.018f;
                float half = 0.90f + wobble;
                float r = 0.20f;
                float qx = Mathf.Abs(p.x) - (half - r);
                float qy = Mathf.Abs(p.y) - (half - r);
                float sd = (qx > 0f && qy > 0f) ? Mathf.Sqrt(qx * qx + qy * qy) : Mathf.Max(qx, qy);
                if (sd > r) return Color.clear;

                Color c = Color.Lerp(Palette.Hex("8E4232"), Palette.CoralBody,
                                     Mathf.InverseLerp(-0.9f, 0.9f, p.y));
                c.a = 1f;

                // светлая кромка сверху и тень снизу
                if (sd > r - 0.14f && p.y > 0.1f) c = Blend(c, new Color(1f, 1f, 1f, 0.20f));
                if (sd > r - 0.12f && p.y < -0.3f) c = Blend(c, new Color(0f, 0f, 0f, 0.22f));

                // полость у варианта 2
                if (variant == 2)
                {
                    float inner = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    if (inner < 0.42f) return Blend(c, new Color(0f, 0f, 0f, 0.45f));
                    if (inner < 0.52f) return Blend(c, new Color(1f, 1f, 1f, 0.12f));
                }

                // поры
                for (int i = 0; i < pores.Length; i++)
                {
                    float d = Vector2.Distance(p, pores[i]);
                    if (d < 0.085f) return Blend(c, new Color(0f, 0f, 0f, 0.30f));
                    if (d < 0.115f) return Blend(c, new Color(1f, 1f, 1f, 0.10f));
                }

                return c;
            });
        }

        static Vector2[] PorePattern(int variant)
        {
            switch (variant)
            {
                case 1:
                    return new[] { new Vector2(-0.45f, -0.15f), new Vector2(0.30f, 0.10f), new Vector2(0.05f, -0.55f) };
                case 2:
                    return new[] { new Vector2(-0.68f, 0.55f), new Vector2(0.66f, -0.52f) };
                default:
                    return new[] { new Vector2(-0.38f, 0.36f), new Vector2(0.42f, -0.22f),
                                   new Vector2(-0.10f, -0.58f), new Vector2(0.55f, 0.52f) };
            }
        }

        // --------------------------------------------- сегмент длинного коралла

        /// <summary>
        /// Звено длинного коралла: овал, вытянутый вдоль ветви. Звенья ставятся
        /// внахлёст, поэтому цепочка читается как сплошная ветка, а не как бусы.
        /// </summary>
        public static Sprite CoralLimb(int variant)
        {
            variant = Mathf.Clamp(variant, 0, 1);
            Color body = variant == 0 ? Palette.CoralBody : Palette.Hex("A85138");

            return Build("limb_" + variant, p =>
            {
                // неровная толщина — ветка живая, а не труба
                float bulge = 0.60f + 0.05f * Mathf.Sin(p.x * 6.5f + variant * 2f);
                float ex = p.x / 0.97f;
                float ey = p.y / bulge;
                if (ex * ex + ey * ey > 1f) return Color.clear;

                Color c = Color.Lerp(body * 0.68f, body, Mathf.InverseLerp(-bulge, bulge * 0.8f, p.y));
                c.a = 1f;

                // блик сверху и тень снизу
                if (p.y > bulge * 0.45f) c = Blend(c, new Color(1f, 1f, 1f, 0.16f));
                if (p.y < -bulge * 0.55f) c = Blend(c, new Color(0f, 0f, 0f, 0.18f));

                // поры вдоль ветви
                for (int i = -2; i <= 2; i++)
                {
                    float px = i * 0.34f + (variant == 0 ? 0.08f : -0.06f);
                    float py = (i % 2 == 0 ? 0.14f : -0.16f);
                    float d = Vector2.Distance(p, new Vector2(px, py));
                    if (d < 0.07f) return Blend(c, new Color(0f, 0f, 0f, 0.28f));
                    if (d < 0.10f) return Blend(c, new Color(1f, 1f, 1f, 0.12f));
                }

                return c;
            });
        }

        // ------------------------------------------------ шары локаций

        /// <summary>
        /// Коралловый шар — основная фигура внутри локаций. Рисуется почти белым,
        /// чтобы тонироваться под цвет локации через SpriteRenderer.color.
        /// 0 — мозговой коралл с бороздами, 1 — актиния с бугорками по краю.
        /// </summary>
        public static Sprite CoralBlob(int variant)
        {
            variant = Mathf.Clamp(variant, 0, 1);

            return Build("blob_" + variant, p =>
            {
                float d = p.magnitude;

                if (variant == 1)
                {
                    // бугорки по краю выходят чуть за окружность
                    float angle = Mathf.Atan2(p.y, p.x);
                    float nubs = 0.90f + 0.09f * Mathf.Cos(angle * 11f);
                    if (d > nubs) return Color.clear;
                }
                else if (d > 0.95f) return Color.clear;

                // объём: светлее сверху-слева, тень снизу
                float shade = Mathf.Lerp(1f, 0.74f,
                    Mathf.Clamp01(Vector2.Distance(p, new Vector2(-0.32f, 0.34f)) / 1.5f));
                Color c = new Color(0.949f, 0.969f, 0.953f, 1f) * shade;
                c.a = 1f;

                if (variant == 0)
                {
                    // борозды мозгового коралла
                    float wave = Mathf.Sin(p.x * 5.5f + Mathf.Sin(p.y * 4.2f) * 1.6f);
                    if (wave > 0.55f) c = Blend(c, new Color(0.62f, 0.71f, 0.66f, 0.55f));
                }
                else
                {
                    // тёмная сердцевина актинии
                    if (d < 0.34f) c = Blend(c, new Color(0.55f, 0.63f, 0.60f, 0.45f));
                    if (d < 0.16f) c = Blend(c, new Color(0.42f, 0.50f, 0.48f, 0.55f));
                }

                // светлый ободок
                if (d > 0.82f && d < 0.93f) c = Blend(c, new Color(1f, 1f, 1f, 0.30f));

                return c;
            });
        }

        /// <summary>
        /// Медуза: полупрозрачный купол и волнистые щупальца. Рисуется «куполом вверх»
        /// и, в отличие от рыб, по направлению движения не поворачивается.
        /// </summary>
        static Sprite JellyfishSprite() => Build("jellyfish", p =>
        {
            Color bell = Palette.JellyBell;
            Color core = Palette.JellyCore;

            // щупальца
            if (p.y < -0.10f)
            {
                for (int i = -2; i <= 2; i++)
                {
                    float baseX = i * 0.26f;
                    float wave = Mathf.Sin(p.y * 7f + i * 1.3f) * 0.09f;
                    float width = Mathf.Lerp(0.055f, 0.018f, Mathf.InverseLerp(-0.10f, -0.95f, p.y));
                    if (Mathf.Abs(p.x - (baseX + wave)) < width && p.y > -0.96f)
                    {
                        float fade = Mathf.InverseLerp(-0.96f, -0.10f, p.y);
                        return new Color(core.r, core.g, core.b, 0.30f + 0.45f * fade);
                    }
                }
            }

            // купол: половина эллипса с фестончатым низом
            float scallop = Mathf.Abs(Mathf.Sin(p.x * 7.5f)) * 0.06f;
            float bottom = -0.16f + scallop;
            if (p.y < bottom) return Color.clear;

            float ex = p.x / 0.92f;
            float ey = (p.y + 0.16f) / 0.92f;
            if (ex * ex + ey * ey > 1f) return Color.clear;

            float d = Mathf.Sqrt(ex * ex + ey * ey);
            Color c = Color.Lerp(new Color(1f, 1f, 1f, 0.55f), bell.WithAlpha(0.42f), d);

            // светящееся ядро и внутренние прожилки
            if (Vector2.Distance(p, new Vector2(0f, 0.02f)) < 0.30f)
                c = Blend(c, core.WithAlpha(0.40f));
            if (d > 0.82f) c = Blend(c, new Color(1f, 1f, 1f, 0.35f));
            if (Mathf.Abs(Mathf.Sin(p.x * 9f)) > 0.93f && p.y > bottom)
                c = Blend(c, new Color(1f, 1f, 1f, 0.18f));

            return c;
        });

        // ------------------------------------------------- дальний план океана

        /// <summary>Силуэт кита, смотрит в +X. Рисуется плоским — цвет и прозрачность задаёт тинт.</summary>
        public static Sprite WhaleSilhouette() => BuildRect("whale", 256, 112, p =>
        {
            // хвостовые лопасти
            if (InTriangle(p, new Vector2(-0.70f, 0.02f), new Vector2(-1.00f, 0.40f), new Vector2(-0.84f, 0.02f)) ||
                InTriangle(p, new Vector2(-0.70f, -0.02f), new Vector2(-1.00f, -0.40f), new Vector2(-0.84f, -0.02f)))
                return Color.white;

            // корпус: плавное веретено от носа к хвостовому стеблю
            float t = Mathf.InverseLerp(1.00f, -0.66f, p.x);
            if (t < 0f || t > 1f) return Color.clear;
            float half = 0.33f * Mathf.Sin(t * Mathf.PI * 0.85f + 0.25f);

            // грудной плавник
            if (InTriangle(p, new Vector2(0.28f, -0.08f), new Vector2(-0.02f, -0.40f), new Vector2(0.32f, -0.24f)))
                return Color.white;
            // спинной плавник
            if (InTriangle(p, new Vector2(-0.34f, half * 0.8f), new Vector2(-0.08f, half * 0.8f),
                              new Vector2(-0.26f, half + 0.11f)))
                return Color.white;

            // брюхо чуть более плоское, чем спина
            if (p.y < half && p.y > -half * 0.88f) return Color.white;
            return Color.clear;
        }, 3);

        /// <summary>Силуэт манты сверху: +X — направление движения, крылья уходят по вертикали.</summary>
        public static Sprite MantaSilhouette() => BuildRect("manta", 176, 224, p =>
        {
            // головные лопасти
            for (int s = -1; s <= 1; s += 2)
            {
                if (InTriangle(p, new Vector2(0.46f, s * 0.09f), new Vector2(0.74f, s * 0.15f),
                                  new Vector2(0.46f, s * 0.21f)))
                    return Color.white;
            }

            // хвост
            if (p.x < -0.28f && p.x > -0.76f && Mathf.Abs(p.y) < 0.035f) return Color.white;

            if (Mathf.Abs(p.y) > 0.97f) return Color.clear;

            // крыло: передняя кромка выпуклая, задняя скошена назад к концам
            float leading = 0.60f * Mathf.Cos(p.y * 1.35f);
            float trailing = -0.30f - 0.42f * Mathf.Abs(p.y);
            if (p.x < leading && p.x > trailing) return Color.white;

            return Color.clear;
        }, 3);

        /// <summary>Светящаяся частица планктона: ядро плюс мягкий крест лучей.</summary>
        public static Sprite PlanktonMote() => Build("plankton", p =>
        {
            float d = p.magnitude;
            float core = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.10f, 0.44f, d));

            float rayH = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.02f, 0.11f, Mathf.Abs(p.y)))
                       * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.12f, 1f, Mathf.Abs(p.x)));
            float rayV = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.02f, 0.11f, Mathf.Abs(p.x)))
                       * Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.12f, 1f, Mathf.Abs(p.y)));

            float a = Mathf.Clamp01(core + (rayH + rayV) * 0.5f);
            return a <= 0.01f ? Color.clear : new Color(1f, 1f, 1f, a);
        });

        /// <summary>Луч света с поверхности: узкий сверху, расходится вниз и гаснет.</summary>
        public static Sprite LightShaft() => BuildRect("light_shaft", 96, 256, p =>
        {
            // p.y: 1 сверху, -1 снизу; p.x в пределах ±0.375
            float t = Mathf.InverseLerp(1f, -1f, p.y);
            float halfWidth = Mathf.Lerp(0.11f, 0.34f, t);
            float edge = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(halfWidth * 0.35f, halfWidth, Mathf.Abs(p.x)));
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1f, -0.15f, p.y));
            float a = edge * fade * 0.9f;
            return a <= 0.01f ? Color.clear : new Color(1f, 1f, 1f, a);
        }, 3);

        // ----------------------------------------------------------- декорации

        static bool TaperedSegment(Vector2 p, Vector2 a, Vector2 b, float widthA, float widthB)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return Vector2.Distance(p, a) < widthA;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + ab * t) < Mathf.Lerp(widthA, widthB, t);
        }

        static bool InRect(Vector2 p, float x0, float y0, float x1, float y1)
            => p.x >= x0 && p.x <= x1 && p.y >= y0 && p.y <= y1;

        /// <summary>Коралловая колонна — тянется снизу вверх, ветвится.</summary>
        public static Sprite CoralPillar() => BuildRect("sc_pillar", 48, 168, p =>
        {
            Color body = Palette.CoralBody;
            Color tip = Palette.CoralTip;

            // ствол
            if (TaperedSegment(p, new Vector2(0f, -1f), new Vector2(0.02f, 0.78f), 0.20f, 0.065f))
                return Color.Lerp(body * 0.75f, body, Mathf.InverseLerp(-1f, 0.8f, p.y));

            // ветви
            if (TaperedSegment(p, new Vector2(0f, -0.30f), new Vector2(0.20f, 0.10f), 0.085f, 0.045f) ||
                TaperedSegment(p, new Vector2(0f, 0.15f), new Vector2(-0.19f, 0.50f), 0.075f, 0.040f) ||
                TaperedSegment(p, new Vector2(0f, 0.45f), new Vector2(0.17f, 0.80f), 0.065f, 0.038f))
                return body * 0.88f;

            // утолщения на концах
            if (Vector2.Distance(p, new Vector2(0.20f, 0.10f)) < 0.06f ||
                Vector2.Distance(p, new Vector2(-0.19f, 0.50f)) < 0.055f ||
                Vector2.Distance(p, new Vector2(0.17f, 0.80f)) < 0.05f ||
                Vector2.Distance(p, new Vector2(0.02f, 0.80f)) < 0.055f)
                return tip;

            return Color.clear;
        });

        /// <summary>Каменный уступ — торчит от края экрана внутрь.</summary>
        public static Sprite RockLedge()
        {
            var poly = new[]
            {
                new Vector2(-1.00f, -0.44f), new Vector2(-1.00f, 0.40f), new Vector2(-0.58f, 0.30f),
                new Vector2(-0.12f, 0.38f), new Vector2(0.32f, 0.18f), new Vector2(0.70f, 0.02f),
                new Vector2(0.94f, -0.18f), new Vector2(0.58f, -0.40f)
            };
            return BuildRect("sc_ledge", 168, 84, p =>
            {
                if (!InPolygon(p, poly)) return Color.clear;
                Color c = Palette.Hex("46525E");
                c = Color.Lerp(c * 0.6f, c, Mathf.InverseLerp(-0.42f, 0.34f, p.y));
                c.a = 1f;
                // светлая кромка сверху
                if (!InPolygon(new Vector2(p.x, p.y + 0.07f), poly)) c = Blend(c, new Color(1f, 1f, 1f, 0.14f));
                return c;
            });
        }

        /// <summary>Затонувшая постройка — блоки-прямоугольники, явная рукотворная геометрия.</summary>
        public static Sprite RuinBlocks() => BuildRect("sc_ruin", 128, 128, p =>
        {
            Color stone = Palette.Hex("3D4A55");

            bool a = InRect(p, -0.92f, -1.00f, 0.58f, -0.12f);
            bool b = InRect(p, -0.72f, -0.10f, 0.16f, 0.44f);
            bool c2 = InRect(p, -0.56f, 0.46f, -0.14f, 0.86f);
            bool d = InRect(p, 0.20f, -0.10f, 0.78f, 0.22f);
            if (!(a || b || c2 || d)) return Color.clear;

            Color c = Color.Lerp(stone * 0.62f, stone, Mathf.InverseLerp(-1f, 0.9f, p.y));
            c.a = 1f;

            // швы кладки
            if (Mathf.Repeat(p.y * 5.6f + 0.5f, 1f) < 0.09f) c = Blend(c, new Color(0f, 0f, 0f, 0.35f));
            if (Mathf.Repeat(p.x * 4.3f, 1f) < 0.07f) c = Blend(c, new Color(0f, 0f, 0f, 0.22f));

            // подсветка верхних граней
            bool aboveA = InRect(new Vector2(p.x, p.y + 0.06f), -0.92f, -1.00f, 0.58f, -0.12f);
            bool aboveB = InRect(new Vector2(p.x, p.y + 0.06f), -0.72f, -0.10f, 0.16f, 0.44f);
            bool aboveC = InRect(new Vector2(p.x, p.y + 0.06f), -0.56f, 0.46f, -0.14f, 0.86f);
            bool aboveD = InRect(new Vector2(p.x, p.y + 0.06f), 0.20f, -0.10f, 0.78f, 0.22f);
            if (!(aboveA || aboveB || aboveC || aboveD))
                c = Blend(c, new Color(1f, 1f, 1f, 0.16f));

            return c;
        });

        /// <summary>Каменная арка — крупный ориентир, хорошо читается при прокрутке.</summary>
        public static Sprite RuinArch() => BuildRect("sc_arch", 168, 120, p =>
        {
            Color stone = Palette.Hex("36424D");

            bool left = InRect(p, -0.94f, -0.71f, -0.54f, 0.24f);
            bool right = InRect(p, 0.54f, -0.71f, 0.94f, 0.24f);
            bool beam = InRect(p, -0.94f, 0.24f, 0.94f, 0.50f);

            // свод: кольцо между пилонами
            float d = Vector2.Distance(p, new Vector2(0f, 0.24f));
            bool vault = p.y > 0.24f && d > 0.54f && d < 0.94f;

            if (!(left || right || beam || vault)) return Color.clear;
            // вырезаем проём под сводом
            if (beam && d < 0.54f && p.y > 0.24f) return Color.clear;

            Color c = Color.Lerp(stone * 0.6f, stone, Mathf.InverseLerp(-0.71f, 0.5f, p.y));
            c.a = 1f;
            if (Mathf.Repeat(p.y * 6.5f, 1f) < 0.08f) c = Blend(c, new Color(0f, 0f, 0f, 0.30f));
            return c;
        });

        /// <summary>Водоросль — длинная волнистая лента.</summary>
        public static Sprite Kelp() => BuildRect("sc_kelp", 40, 180, p =>
        {
            float centre = Mathf.Sin(p.y * 3.1f + 0.7f) * 0.11f;
            float width = Mathf.Lerp(0.055f, 0.022f, Mathf.InverseLerp(-1f, 1f, p.y));
            if (p.y > 0.94f) return Color.clear;

            Color body = Palette.Hex("1F4F47");

            if (Mathf.Abs(p.x - centre) < width)
                return Color.Lerp(body * 0.7f, body, Mathf.InverseLerp(-1f, 1f, p.y));

            // листья
            for (int i = 0; i < 6; i++)
            {
                float ly = -0.85f + i * 0.34f;
                float lc = Mathf.Sin(ly * 3.1f + 0.7f) * 0.11f;
                int side = i % 2 == 0 ? 1 : -1;
                if (TaperedSegment(p, new Vector2(lc, ly), new Vector2(lc + side * 0.14f, ly + 0.16f), 0.035f, 0.008f))
                    return body * 0.85f;
            }
            return Color.clear;
        });

        /// <summary>
        /// Рифовый краб для боковых камней. Три кадра — это фазы шага: ноги
        /// переставляются вдоль собственной поперечной оси краба, поэтому при
        /// повороте спрайта к стене движение читается как боковая походка.
        ///
        /// Рисуется «мордой вверх»: повернуть его к нужной стене — задача SideCrab.
        /// </summary>
        public static Sprite SideCrab(int frame)
        {
            frame = ((frame % 3) + 3) % 3;
            float phase = frame * (Mathf.PI * 2f / 3f);

            return BuildRect("sidecrab_" + frame, 224, 168, p =>
            {
                Color shell = Palette.CrabShell;
                Color dark = Palette.CrabShellDk;
                Color hilite = Palette.CrabHilite;
                Color joint = Palette.CrabJoint;

                // ---- ходильные ноги: три пары, шагают в противофазе
                if (p.y < 0.16f && Mathf.Abs(p.x) > 0.28f)
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        // Дальняя нога в крайней фазе шага должна остаться внутри
                        // текстуры: край её обрезает, и краб выглядит бы куцым.
                        float spread = 0.34f + i * 0.11f;
                        float swing = Mathf.Sin(phase + i * 2.1f) * 0.10f;

                        Vector2 hip = new Vector2(side * spread, -0.06f - i * 0.02f);
                        Vector2 knee = new Vector2(side * (spread + 0.22f) + swing * 0.5f, 0.02f - i * 0.03f);
                        Vector2 toe = new Vector2(side * (spread + 0.26f) + swing, -0.38f - i * 0.02f);

                        if (TaperedSegment(p, hip, knee, 0.052f, 0.040f)) return Color.Lerp(dark, shell, 0.4f);
                        if (TaperedSegment(p, knee, toe, 0.040f, 0.010f)) return joint;
                        if (Vector2.Distance(p, knee) < 0.046f) return joint;
                    }
                }

                // ---- клешни подняты вверх-в стороны
                if (p.y > -0.10f && Mathf.Abs(p.x) > 0.24f)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 shoulder = new Vector2(side * 0.34f, 0.06f);
                    Vector2 elbow = new Vector2(side * 0.58f, 0.16f);
                    Vector2 hand = new Vector2(side * 0.56f, 0.34f);

                    if (TaperedSegment(p, shoulder, elbow, 0.075f, 0.062f)) return Color.Lerp(dark, shell, 0.5f);
                    if (TaperedSegment(p, elbow, hand, 0.062f, 0.070f)) return Color.Lerp(dark, shell, 0.6f);

                    // кисть с раскрытой щелью между клыками
                    float dh = Vector2.Distance(p, hand);
                    if (dh < 0.145f)
                    {
                        Vector2 local = p - hand;
                        bool gap = local.y > 0.02f && Mathf.Abs(local.x - side * 0.01f) < 0.030f + local.y * 0.22f;
                        if (gap) return Color.clear;
                        return Color.Lerp(shell, hilite, Mathf.InverseLerp(-0.1f, 0.16f, local.y));
                    }
                }

                // ---- глаза на коротких стебельках
                if (p.y > 0.06f && Mathf.Abs(p.x) < 0.34f)
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector2 root = new Vector2(side * 0.13f, 0.10f);
                    Vector2 tip = new Vector2(side * 0.17f, 0.28f);
                    if (TaperedSegment(p, root, tip, 0.040f, 0.034f)) return dark;
                    if (Vector2.Distance(p, tip + new Vector2(0f, 0.03f)) < 0.062f)
                        return Vector2.Distance(p, tip + new Vector2(side * 0.012f, 0.022f)) < 0.030f
                            ? new Color(0.05f, 0.02f, 0.03f, 1f) : hilite;
                }

                // ---- панцирь
                float ex = p.x / 0.54f;
                float ey = (p.y + 0.02f) / 0.30f;
                if (ex * ex + ey * ey > 1f) return Color.clear;

                Color c = Color.Lerp(shell, dark, Mathf.InverseLerp(0.16f, -0.30f, p.y));
                c.a = 1f;

                // бугры брони и тёмная кромка — на таком размере этого достаточно
                for (int i = -2; i <= 2; i++)
                {
                    float dw = Vector2.Distance(p, new Vector2(i * 0.17f, 0.04f - Mathf.Abs(i) * 0.02f));
                    if (dw < 0.045f) c = Blend(c, hilite.WithAlpha(0.28f));
                }
                if (ex * ex + ey * ey > 0.78f) c = Blend(c, new Color(0f, 0f, 0f, 0.22f));

                return c;
            });
        }

        // ---------------------------------------------------------------- UI

        /// <summary>
        /// Иконка звука для кнопки-глушилки. Рисуем спрайтом, а не текстовым символом:
        /// во встроенном шрифте Unity нужных пиктограмм нет, вместо них будут «квадраты».
        /// </summary>
        public static Sprite SpeakerIcon(bool muted) => Build(muted ? "icon_mute" : "icon_sound", p =>
        {
            // корпус динамика
            if (InRect(p, -0.62f, -0.26f, -0.24f, 0.26f)) return Color.white;
            // раструб
            if (InTriangle(p, new Vector2(-0.24f, -0.62f), new Vector2(-0.24f, 0.62f), new Vector2(0.10f, 0f)) ||
                InRect(p, -0.24f, -0.30f, 0.10f, 0.30f))
                return Color.white;

            if (muted)
            {
                // перечёркивание
                if (DistToSegment(p, new Vector2(0.22f, -0.36f), new Vector2(0.74f, 0.36f)) < 0.075f ||
                    DistToSegment(p, new Vector2(0.22f, 0.36f), new Vector2(0.74f, -0.36f)) < 0.075f)
                    return Color.white;
                return Color.clear;
            }

            // звуковые волны
            for (int i = 0; i < 2; i++)
            {
                float r = 0.34f + i * 0.26f;
                float d = Vector2.Distance(p, new Vector2(0.06f, 0f));
                if (p.x > 0.16f && d > r && d < r + 0.10f) return Color.white;
            }
            return Color.clear;
        });

        /// <summary>Радиальное затемнение для стартового экрана и экрана поражения.</summary>
        public static Sprite Vignette() => Build("vignette", p =>
        {
            float d = Mathf.Clamp01(p.magnitude / 1.35f);
            Color inner = Palette.Deep2.WithAlpha(0.55f);
            Color outer = Hex06();
            return Color.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, d));
        });

        static Color Hex06() => Palette.Hex("060F18", 0.94f);

        /// <summary>
        /// Плотная подложка для экранов меню.
        ///
        /// Обычная виньетка прозрачна в центре на 45 % — так задумано для
        /// стартового экрана, где сквозь неё красиво виден плывущий мир. Но в
        /// магазине и наградах сквозь неё просвечивали пузырёк со стражем: их
        /// свечение проступало прямо сквозь кнопки, и на приглушённой кнопке
        /// «Крутить» это выглядело как красное пятно поверх интерфейса.
        ///
        /// Здесь тот же градиент, но центр почти непрозрачен: фон остаётся
        /// различимым как текстура, а отдельные объекты сквозь него не читаются.
        /// </summary>
        public static Sprite VignetteSolid() => Build("vignette_solid", p =>
        {
            float d = Mathf.Clamp01(p.magnitude / 1.35f);
            Color inner = Palette.Deep.WithAlpha(0.955f);
            Color outer = Palette.Hex("060F18", 0.99f);
            return Color.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, d));
        });

        /// <summary>Скруглённый прямоугольник с 9-slice границей — для кнопок и панелей.</summary>
        public static Sprite RoundedRect(string key, float cornerFraction = 0.45f)
        {
            const int s = 64;
            float r = cornerFraction; // радиус в нормализованных единицах (0..1)
            int borderPx = Mathf.RoundToInt(r * s * 0.5f) + 2;
            return Build("rr_" + key, p =>
            {
                float qx = Mathf.Abs(p.x) - (1f - r);
                float qy = Mathf.Abs(p.y) - (1f - r);
                float d;
                if (qx > 0f && qy > 0f) d = Mathf.Sqrt(qx * qx + qy * qy);
                else d = Mathf.Max(qx, qy);
                return d <= r ? Color.white : Color.clear;
            }, new Vector4(borderPx, borderPx, borderPx, borderPx), s);
        }

        /// <summary>Скруглённая «таблетка» с обводкой — рамка HUD.</summary>
        public static Sprite RoundedOutline(string key, float cornerFraction = 0.45f, float thickness = 0.08f)
        {
            const int s = 64;
            float r = cornerFraction;
            int borderPx = Mathf.RoundToInt(r * s * 0.5f) + 2;
            return Build("ro_" + key, p =>
            {
                float qx = Mathf.Abs(p.x) - (1f - r);
                float qy = Mathf.Abs(p.y) - (1f - r);
                float d;
                if (qx > 0f && qy > 0f) d = Mathf.Sqrt(qx * qx + qy * qy);
                else d = Mathf.Max(qx, qy);
                float sd = d - r; // <0 внутри
                if (sd > 0f) return Color.clear;
                if (sd > -thickness) return Color.white;
                return new Color(1f, 1f, 1f, 0.30f);
            }, new Vector4(borderPx, borderPx, borderPx, borderPx), s);
        }

        /// <summary>Вертикальный градиент 1xN — фон. Цвета обновляются в рантайме.</summary>
        public static Texture2D CreateGradientTexture(int height)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "tex_gradient"
            };
            return tex;
        }
    }
}
