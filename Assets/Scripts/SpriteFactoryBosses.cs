using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Спрайты боссов и их прицельных индикаторов. Вынесены из SpriteFactory.cs,
    /// который разросся до полутора тысяч строк: там остались игрок, окружение
    /// и декорации, здесь — только боссы.
    /// </summary>
    public static partial class SpriteFactory
    {
        // ------------------------------------------------------- змей бездны
        //
        // Два этапа — два набора спрайтов. На втором тело темнее, шипы длиннее,
        // в трещинах пластин тускло тлеют раны, на хвосте — костяное лезвие.
        // Переход между наборами плавный: контроллер проявляет второй поверх
        // первого, а не подменяет за кадр.
        //
        // Разрешение 160 вместо 128 и сглаживание 2×2: деталей стало больше
        // (чешуя, трещины, точки свечения), а прогрев не должен заметно вырасти.

        /// <summary>Сторона спрайтов змея в пикселях.</summary>
        const int SerpentRes = 160;

        static float SerpentHash(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        /// <summary>
        /// Чешуя: ячейки со смещёнными рядами. Возвращает 0 в середине чешуйки и 1 у
        /// её края — по этому числу кромка темнеет, и поверхность читается рельефной.
        /// </summary>
        static float SerpentScale(Vector2 p, float density)
        {
            float v = p.y * density;
            int row = Mathf.FloorToInt(v);
            float u = p.x * density + (row & 1) * 0.5f;
            int col = Mathf.FloorToInt(u);
            float fu = u - col - 0.5f, fv = v - row - 0.5f;
            return Mathf.Clamp01(Mathf.Sqrt(fu * fu * 1.4f + fv * fv * 2.2f) * 1.45f);
        }

        /// <summary>Излом трещины: середина отрезка, сдвинутая хешем. Считается один раз на спрайт.</summary>
        static Vector2 SerpentKink(Vector2 a, Vector2 b, int seed)
            => Vector2.Lerp(a, b, 0.5f) + new Vector2(SerpentHash(seed, 1) - 0.5f, SerpentHash(seed, 2) - 0.5f) * 0.14f;

        /// <summary>Тонкая ломаная трещина: расстояние до неё, 0 — на самой линии.</summary>
        static float SerpentCrack(Vector2 p, Vector2 a, Vector2 m, Vector2 b)
            => Mathf.Min(DistToSegment(p, a, m), DistToSegment(p, m, b));

        static readonly Color SerpentGlowDot = Palette.Hex("3FD8C8");   // холодные точки по бокам
        static readonly Color SerpentWound   = Palette.Hex("A8382A");   // тусклое тление ран, сумма 1.04
        static readonly Color SerpentSlime   = Palette.Hex("9DB39A");   // слизь на зубах
        static readonly Color SerpentBone    = Palette.Hex("948A76");   // костяной шип и лезвие: тусклая кость, на тёмной воде не светит
        static readonly Color SerpentTooth   = Palette.Hex("C9C0B4");   // зубы, сумма 2.24

        /// <summary>Тело второго этапа темнее: множитель цвета чешуи.</summary>
        const float SerpentStage2Dark = 0.72f;

        static Color SerpentSkin(Vector2 p, float bellyFrom, float bellyTo, int stage)
        {
            Color body = Palette.SerpentBody, belly = Palette.SerpentBelly;
            if (stage >= 2)
            {
                body *= SerpentStage2Dark;
                belly *= SerpentStage2Dark;
            }
            Color c = Color.Lerp(belly, body, Mathf.InverseLerp(bellyFrom, bellyTo, p.y));
            // градиент чешуи: каждая чешуйка светлее к центру и темнее к кромке
            float edge = SerpentScale(p, 7f);
            c = Color.Lerp(c, c * 0.62f, Mathf.SmoothStep(0.55f, 1f, edge) * Mathf.InverseLerp(-0.55f, 0.15f, p.y));
            c.a = 1f;
            return c;
        }

        /// <summary>Голова змея, смотрит в +X (по направлению движения).</summary>
        public static Sprite SerpentHead(int stage, bool mask)
        {
            Vector2 c1a = new Vector2(-0.45f, 0.30f), c1b = new Vector2(-0.12f, 0.12f), c1m = SerpentKink(c1a, c1b, 3);
            Vector2 c2a = new Vector2(0.05f, 0.33f), c2b = new Vector2(0.22f, 0.14f), c2m = SerpentKink(c2a, c2b, 7);
            return BuildRectTwin(MaskKey("serpent_head_s" + Mathf.Clamp(stage, 1, 2), mask), SerpentRes, SerpentRes, p =>
        {
            Color spine = stage >= 2 ? Palette.SerpentSpine * 0.82f : Palette.SerpentSpine;
            spine.a = 1f;

            // гребень на затылке; на втором этапе длиннее и с костяным основанием
            for (int i = 0; i < 3; i++)
            {
                float x0 = -0.42f + i * 0.33f;
                float h = (stage >= 2 ? 0.95f : 0.78f) - i * 0.09f;
                if (InTriangle(p, new Vector2(x0 - 0.11f, 0.28f), new Vector2(x0 + 0.11f, 0.28f),
                                  new Vector2(x0 - 0.12f, h)))
                {
                    // кончик шипа темнее основания — гребень не плоский
                    return Color.Lerp(spine, spine * 0.6f, Mathf.InverseLerp(0.3f, h, p.y)).WithAlpha(1f);
                }
            }

            // череп: эллипс с вытянутой мордой
            float taper = 1f - 0.22f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 1f, p.x));
            float ex = p.x / 0.95f;
            float ey = p.y / (0.50f * taper);
            if (ex * ex + ey * ey > 1f) return Color.clear;

            Color c = SerpentSkin(p, -0.45f, 0.35f, stage);

            // пасть с зубами и слизью
            if (p.x > 0.18f && p.y < -0.02f && p.y > -0.34f)
            {
                c = Palette.BossMaw;
                for (int i = 0; i < 4; i++)
                {
                    float x0 = 0.26f + i * 0.16f;
                    if (InTriangle(p, new Vector2(x0 - 0.045f, -0.02f), new Vector2(x0 + 0.045f, -0.02f),
                                      new Vector2(x0, -0.21f)))
                        c = SerpentTooth;
                    // нить слизи с кончика зуба
                    if (Mathf.Abs(p.x - x0) < 0.012f && p.y < -0.18f && p.y > -0.28f - 0.03f * i)
                        c = Blend(c, SerpentSlime.WithAlpha(0.75f));
                }
                return c;
            }

            // панцирная пластина на темени с трещинами
            if (p.y > 0.08f && Mathf.Abs(p.x + 0.1f) < 0.42f)
            {
                c = Blend(c, new Color(0f, 0f, 0f, 0.12f));
                float crack = Mathf.Min(SerpentCrack(p, c1a, c1m, c1b), SerpentCrack(p, c2a, c2m, c2b));
                if (crack < 0.018f)
                    c = stage >= 2 ? Blend(c, SerpentWound.WithAlpha(0.9f)) : Blend(c, new Color(0f, 0f, 0f, 0.55f));
                else if (stage >= 2 && crack < 0.05f)
                    c = Blend(c, SerpentWound.WithAlpha(0.35f * (1f - crack / 0.05f)));
            }

            // холодные точки свечения вдоль боковой линии
            for (int i = 0; i < 4; i++)
            {
                var dot = new Vector2(-0.62f + i * 0.2f, -0.12f + 0.02f * i);
                float dd = Vector2.Distance(p, dot);
                if (dd < 0.045f) c = Blend(c, SerpentGlowDot.WithAlpha(0.85f * (1f - dd / 0.045f) + 0.15f));
            }

            // Глаз со щелевидным зрачком, утопленный в тёмную глазницу.
            float de = Vector2.Distance(p, new Vector2(0.16f, 0.16f));
            if (de < 0.24f) c = Blend(c, new Color(0f, 0f, 0f, 0.55f * (1f - de / 0.24f)));
            if (de < 0.17f)
            {
                Color iris = Palette.SerpentEye;
                c = Color.Lerp(iris, iris * 0.6f, de / 0.17f);
                c.a = 1f;
                float px = (p.x - 0.16f) / 0.05f;
                float py = (p.y - 0.16f) / 0.12f;
                if (px * px + py * py <= 1f) c = new Color(0.05f, 0.03f, 0.05f, 1f);
                // влажный блик
                if (Vector2.Distance(p, new Vector2(0.12f, 0.22f)) < 0.03f) c = Color.Lerp(c, iris * 1.2f, 0.6f).WithAlpha(1f);
                return c;
            }

            // ноздря
            if (Vector2.Distance(p, new Vector2(0.70f, 0.06f)) < 0.05f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.5f));

            return c;
        }, 2);
        }

        /// <summary>Звено тела змея. Ориентировано так, что +X смотрит в сторону головы.</summary>
        public static Sprite SerpentBody(int variant, int stage, bool mask)
        {
            variant = Mathf.Clamp(variant, 0, 1);
            stage = Mathf.Clamp(stage, 1, 2);
            string key = MaskKey("serpent_body_" + variant + "_s" + stage, mask);
            Vector2 ca = new Vector2(-0.40f + variant * 0.1f, 0.55f), cb = new Vector2(0.18f, 0.18f);
            Vector2 cm = SerpentKink(ca, cb, 11 + variant);

            return BuildRectTwin(key, SerpentRes, SerpentRes, p =>
            {
                Color spine = stage >= 2 ? Palette.SerpentSpine * 0.82f : Palette.SerpentSpine;
                spine.a = 1f;

                // спинной шип; на втором этапе вдвое длиннее, у основания кость
                float spikeTop = stage >= 2 ? (variant == 0 ? 0.98f : 0.90f) : (variant == 0 ? 0.80f : 0.72f);
                float spikeLean = stage >= 2 ? -0.16f : -0.06f;
                if (InTriangle(p, new Vector2(-0.20f, 0.45f), new Vector2(0.20f, 0.45f),
                                  new Vector2(spikeLean, spikeTop)))
                {
                    if (stage >= 2 && p.y < 0.58f) return SerpentBone;
                    return Color.Lerp(spine, spine * 0.62f, Mathf.InverseLerp(0.45f, spikeTop, p.y)).WithAlpha(1f);
                }

                // боковой плавник
                if (InTriangle(p, new Vector2(-0.30f, -0.20f), new Vector2(0.05f, -0.30f),
                                  new Vector2(-0.55f, -0.85f)))
                    return Color.Lerp(spine, Palette.SerpentBody, 0.5f).WithAlpha(1f);

                float d = p.magnitude;
                if (d > 0.72f) return Color.clear;

                Color c = SerpentSkin(p, -0.6f, 0.4f, stage);

                // панцирная пластина на спине с трещиной
                if (p.y > 0.12f)
                {
                    c = Blend(c, new Color(0f, 0f, 0f, 0.10f));
                    float crack = SerpentCrack(p, ca, cm, cb);
                    if (crack < 0.02f)
                        c = stage >= 2 ? Blend(c, SerpentWound.WithAlpha(0.9f)) : Blend(c, new Color(0f, 0f, 0f, 0.5f));
                    else if (stage >= 2 && crack < 0.06f)
                        c = Blend(c, SerpentWound.WithAlpha(0.32f * (1f - crack / 0.06f)));
                }

                // кольцевая насечка и тёмная кромка звена
                if (variant == 1 && Mathf.Abs(p.x) < 0.16f) c = Blend(c, new Color(0f, 0f, 0f, 0.16f));
                if (d > 0.62f) c = Blend(c, new Color(0f, 0f, 0f, 0.22f));

                // две холодные точки на боку
                for (int i = 0; i < 2; i++)
                {
                    var dot = new Vector2(-0.22f + i * 0.36f, -0.18f);
                    float dd = Vector2.Distance(p, dot);
                    if (dd < 0.05f) c = Blend(c, SerpentGlowDot.WithAlpha(0.8f * (1f - dd / 0.05f) + 0.15f));
                }

                // рана второго этапа: тёмное пятно с тлеющей серединой
                if (stage >= 2 && variant == 0)
                {
                    float dw = Vector2.Distance(p, new Vector2(0.18f, -0.02f));
                    if (dw < 0.12f) c = Blend(c, new Color(0.05f, 0.02f, 0.03f, 0.75f * (1f - dw / 0.12f)));
                    if (dw < 0.05f) c = Blend(c, SerpentWound.WithAlpha(0.6f * (1f - dw / 0.05f)));
                }
                return c;
            }, 2);
        }

        /// <summary>
        /// Хвост: +X смотрит в сторону тела. На первом этапе плавник с маленьким
        /// костяным шипом, на втором — утяжелённое звено с костяным лезвием: им
        /// змей и хлещет. Хитбокс удара — круг по самому лезвию.
        /// </summary>
        public static Sprite SerpentTail(int stage, bool mask) => BuildRectTwin(
            MaskKey("serpent_tail_s" + Mathf.Clamp(stage, 1, 2), mask), SerpentRes, SerpentRes, p =>
        {
            Color body = stage >= 2 ? Palette.SerpentBody * SerpentStage2Dark : Palette.SerpentBody;
            body.a = 1f;
            Color spine = stage >= 2 ? Palette.SerpentSpine * 0.82f : Palette.SerpentSpine;
            spine.a = 1f;

            if (stage >= 2)
            {
                // лезвие: изогнутый серп из кости, тёмная кромка по краю
                float bx = -p.x;                                     // 0 у основания, 1 у острия
                float curve = 0.18f * bx * bx;
                float half = Mathf.Lerp(0.30f, 0.0f, Mathf.InverseLerp(-0.1f, 0.98f, bx));
                float dy = p.y - curve;
                if (bx > -0.1f && bx < 0.98f && Mathf.Abs(dy) < half)
                {
                    Color bone = SerpentBone;
                    if (Mathf.Abs(dy) > half - 0.035f) bone = bone * 0.55f;   // кромка
                    bone = Color.Lerp(bone, bone * 0.78f, SerpentScale(p, 9f) * 0.5f);
                    bone.a = 1f;
                    return bone;
                }
            }
            else
            {
                // плавник уходит назад, на конце маленький костяной шип
                if (InTriangle(p, new Vector2(0.15f, 0f), new Vector2(-0.95f, 0.62f), new Vector2(-0.95f, -0.62f)))
                    return Color.Lerp(spine, body, 0.55f).WithAlpha(1f);
                if (InTriangle(p, new Vector2(-0.2f, 0.08f), new Vector2(-0.2f, -0.08f), new Vector2(-0.75f, 0f)))
                    return SerpentBone;
            }

            float d = Vector2.Distance(p, new Vector2(0.35f, 0f));
            if (d > 0.52f) return Color.clear;

            Color c = SerpentSkin(p - new Vector2(0.35f, 0f), -0.5f, 0.4f, stage);
            if (p.y < -0.15f) c = Blend(c, new Color(0f, 0f, 0f, 0.18f));
            if (stage >= 2 && Vector2.Distance(p, new Vector2(0.30f, 0.10f)) < 0.05f)
                c = Blend(c, SerpentWound.WithAlpha(0.6f));
            return c;
        }, 2);

        // -------------------------------------------- прицельные индикаторы

        /// <summary>Пунктирный луч прицеливания. Тинтуется цветом.</summary>
        public static Sprite AimBeam() => BuildRect("aim_beam", 192, 24, p =>
        {
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.75f, Mathf.Abs(p.x)));
            float thickness = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.125f, 0.07f, Mathf.Abs(p.y)));
            float dash = Mathf.Repeat(p.x * 7f, 1f) < 0.58f ? 1f : 0.12f;
            float a = fade * thickness * dash;
            return a <= 0.01f ? Color.clear : new Color(1f, 1f, 1f, a);
        }, 3);

        /// <summary>
        /// Стрелка-шеврон, смотрит в +X. Из цепочки таких стрелок с бегущей волной
        /// яркости складывается указатель направления атаки.
        /// </summary>
        public static Sprite AimChevron() => Build("aim_chevron", p =>
        {
            float d = Mathf.Min(DistToSegment(p, new Vector2(-0.45f, 0.62f), new Vector2(0.45f, 0f)),
                                DistToSegment(p, new Vector2(-0.45f, -0.62f), new Vector2(0.45f, 0f)));
            if (d < 0.13f) return Color.white;
            if (d < 0.20f) return new Color(1f, 1f, 1f, 0.45f);
            return Color.clear;
        });

        /// <summary>
        /// Штрих пунктира телеграфа: капсула с мягкими краями, тинтуется цветом.
        /// Из таких штрихов складываются дуга удара хвостом и кольцо змея.
        /// </summary>
        public static Sprite TelegraphDash() => BuildRect("tele_dash", 48, 16, p =>
        {
            float x = Mathf.Abs(p.x), y = Mathf.Abs(p.y) * 3f;
            float d = x > 0.66f ? Vector2.Distance(new Vector2(x, y), new Vector2(0.66f, 0f)) : y;
            float a = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(0.22f, 0.33f, d));
            return a <= 0.01f ? Color.clear : new Color(1f, 1f, 1f, a);
        }, 3);

        // ------------------------------------------------------ спрут глубин

        /// <summary>
        /// Голова спрута. Круглый купол мантии, тяжёлые надбровья, клюв и обрубки рук
        /// у основания: щупальца уходят за кадр, но видно, что начинаются они здесь.
        /// </summary>
        public static Sprite OctopusHead(bool mask) => Build(MaskKey("octo_head", mask), p =>
        {
            Color body = Palette.OctopusBody;
            Color dark = Palette.OctopusDark;
            Color eye = Palette.OctopusEye;
            Color glow = Palette.OctopusSucker;

            // ---- обрубки щупалец у основания: настоящие руки уходят за экран
            if (p.y < -0.55f)
            {
                for (int i = -2; i <= 2; i++)
                {
                    Vector2 root = new Vector2(i * 0.20f, -0.62f);
                    Vector2 end = new Vector2(i * 0.34f, -0.99f);
                    if (TaperedSegment(p, root, end, 0.13f, 0.075f))
                        return Color.Lerp(dark, body, 0.25f);
                }
            }

            // ---- мантия: круглый купол
            float ey = (p.y - 0.06f) / 0.90f;
            if (Mathf.Abs(ey) > 1f) return Color.clear;
            float halfWidth = 0.94f * Mathf.Sqrt(Mathf.Max(0f, 1f - ey * ey));
            if (Mathf.Abs(p.x) > halfWidth) return Color.clear;

            Color c = Color.Lerp(body, dark, Mathf.InverseLerp(0.3f, -0.9f, p.y));
            c.a = 1f;

            // продольные валики мантии
            if (Mathf.Abs(Mathf.Abs(p.x) - halfWidth * 0.55f) < 0.035f)
                c = Blend(c, new Color(0f, 0f, 0f, 0.16f));

            // бугристая кожа
            for (int i = 0; i < 9; i++)
            {
                float a = i * 2.4f;
                Vector2 wart = new Vector2(Mathf.Cos(a) * 0.62f, Mathf.Sin(a) * 0.52f + 0.28f);
                float dw = Vector2.Distance(p, wart);
                if (dw < 0.085f) c = Blend(c, new Color(1f, 1f, 1f, 0.12f));
                else if (dw < 0.115f) c = Blend(c, new Color(0f, 0f, 0f, 0.14f));
            }

            // светящиеся точки по бокам мантии
            for (int i = 0; i < 4; i++)
            {
                float y = 0.62f - i * 0.24f;
                for (int s = -1; s <= 1; s += 2)
                    if (Vector2.Distance(p, new Vector2(s * (halfWidth - 0.10f), y)) < 0.045f)
                        return glow;
            }

            // ---- клюв
            if (p.y < -0.34f && p.y > -0.72f && Mathf.Abs(p.x) < 0.26f)
            {
                float beak = Mathf.InverseLerp(-0.34f, -0.72f, p.y);
                if (Mathf.Abs(p.x) < Mathf.Lerp(0.24f, 0.03f, beak))
                {
                    if (Mathf.Abs(p.x) > Mathf.Lerp(0.20f, 0.02f, beak))
                        return Palette.BossTeeth;
                    return new Color(0.05f, 0.02f, 0.05f, 1f);
                }
            }

            // ---- глаза: крупные, с горизонтальным зрачком и тяжёлым надбровьем
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 centre = new Vector2(s * 0.44f, 0.02f);

                // надбровье — тёмный скос, придаёт злой прищур
                if (DistToSegment(p, centre + new Vector2(-s * 0.30f, 0.30f),
                                     centre + new Vector2(s * 0.30f, 0.16f)) < 0.10f)
                    return new Color(0.16f, 0.06f, 0.18f, 1f);

                float de = Vector2.Distance(p, centre);
                if (de < 0.27f)
                {
                    c = Color.Lerp(eye, eye * 0.5f, de / 0.27f);
                    c.a = 1f;
                    float px = (p.x - centre.x) / 0.21f;
                    float py = (p.y - centre.y) / 0.06f;
                    if (px * px + py * py <= 1f) c = new Color(0.05f, 0.02f, 0.05f, 1f);
                    return c;
                }
            }

            // блик на макушке и тёмный низ
            if (p.y > 0.52f && Mathf.Abs(p.x) < halfWidth * 0.6f)
                c = Blend(c, new Color(1f, 1f, 1f, 0.12f));
            if (p.y < -0.5f) c = Blend(c, new Color(0f, 0f, 0f, 0.26f));

            return c;
        });

        /// <summary>Звено щупальца. +X направлен вдоль щупальца, к кончику.</summary>
        public static Sprite TentacleSegment() => Build("octo_seg", p =>
        {
            Color body = Palette.OctopusBody;
            Color dark = Palette.OctopusDark;
            Color sucker = Palette.OctopusSucker;

            float ex = p.x / 0.96f;
            float ey = p.y / 0.82f;
            if (ex * ex + ey * ey > 1f) return Color.clear;

            Color c = Color.Lerp(body, dark, Mathf.InverseLerp(0.3f, -0.85f, p.y));
            c.a = 1f;

            // присоски по нижней стороне
            for (int i = -1; i <= 1; i++)
            {
                float dsu = Vector2.Distance(p, new Vector2(i * 0.46f, -0.34f));
                if (dsu < 0.15f) c = Blend(c, sucker.WithAlpha(0.75f));
                if (dsu < 0.07f) c = Blend(c, new Color(0f, 0f, 0f, 0.30f));
            }

            if (p.y > 0.42f) c = Blend(c, new Color(1f, 1f, 1f, 0.12f));
            return c;
        });

        /// <summary>Кончик щупальца — сужается в сторону +X.</summary>
        public static Sprite TentacleTip(bool mask) => Build(MaskKey("octo_tip", mask), p =>
        {
            Color body = Palette.OctopusBody;
            Color dark = Palette.OctopusDark;

            // капля: круглая у основания, острая к кончику
            float halfHeight = 0.80f * Mathf.Sqrt(Mathf.Max(0f,
                                   1f - Mathf.Pow(Mathf.Max(0f, p.x + 0.1f) / 1.05f, 1.6f)));
            if (p.x < -0.95f || Mathf.Abs(p.y) > halfHeight) return Color.clear;

            Color c = Color.Lerp(body, dark, Mathf.InverseLerp(0.3f, -0.8f, p.y));
            c.a = 1f;
            if (Vector2.Distance(p, new Vector2(-0.45f, -0.3f)) < 0.13f)
                c = Blend(c, Palette.OctopusSucker.WithAlpha(0.7f));
            if (p.y > 0.35f) c = Blend(c, new Color(1f, 1f, 1f, 0.12f));
            return c;
        });

        // ----------------------------------------------------- клинок бездны

        /// <summary>Рыба-меч, смотрит в +X. Длинный клинок, парусный плавник, вилка хвоста.</summary>
        public static Sprite Swordfish(bool mask) => BuildRect(MaskKey("sword", mask), 256, 104, p =>
        {
            Color back = Palette.SwordBack;
            Color belly = Palette.SwordBelly;
            Color blade = Palette.SwordBlade;
            Color fin = Palette.SwordFin;

            // Клинок. Сужается по степенной кривой, а не прямой линией: у прямого
            // сужения остриё выходит тупым обрубком, потому что толщина у самого
            // конца ещё заметная. Со степенью 2.2 он тонет в нить задолго до края.
            if (p.x > 0.24f)
            {
                float u = Mathf.InverseLerp(0.24f, 1f, p.x);
                float taper = Mathf.Lerp(0.062f, 0.002f, Mathf.Pow(u, 2.2f));
                if (Mathf.Abs(p.y) < taper)
                {
                    // грань по оси: клинок читается гранёным, а не плоской полосой
                    Color edge = Mathf.Abs(p.y) < taper * 0.30f
                        ? Color.Lerp(blade, Color.white, 0.6f) : blade;

                    // к острию светлее — блик на кромке
                    return Color.Lerp(edge, Color.white, u * 0.35f);
                }
            }

            // Спинной плавник — рваный парус с зубцами по задней кромке.
            if (InTriangle(p, new Vector2(0.12f, 0.14f), new Vector2(-0.36f, 0.10f), new Vector2(-0.08f, 0.50f)))
            {
                float rel = Mathf.InverseLerp(0.50f, 0.10f, p.y);
                if (Mathf.Repeat(rel * 7f, 1f) > 0.22f) return fin;
            }
            // грудной плавник, отведён назад
            if (InTriangle(p, new Vector2(0.10f, -0.12f), new Vector2(-0.28f, -0.46f), new Vector2(-0.02f, -0.14f)))
                return fin;
            // хвостовая вилка, длиннее прежней
            if (InTriangle(p, new Vector2(-0.66f, 0.01f), new Vector2(-1.00f, 0.44f), new Vector2(-0.84f, 0.00f)) ||
                InTriangle(p, new Vector2(-0.66f, -0.01f), new Vector2(-1.00f, -0.44f), new Vector2(-0.84f, 0.00f)))
                return fin;

            // корпус: веретено от головы к хвостовому стеблю
            float t = Mathf.InverseLerp(0.42f, -0.72f, p.x);
            if (t < 0f || t > 1f) return Color.clear;
            float half = 0.30f * Mathf.Sin(t * Mathf.PI * 0.88f + 0.22f);
            if (Mathf.Abs(p.y) > half) return Color.clear;

            // Тёмная спина и светлое брюхо с резкой границей: размытый переход
            // читается как блик, а не как окрас хищника.
            float shade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-half * 0.35f, half * 0.25f, p.y));
            Color c = Color.Lerp(belly, back, shade);
            c.a = 1f;

            // боковая полоса и жаберная линия
            if (Mathf.Abs(p.y) < half * 0.16f) c = Blend(c, new Color(1f, 1f, 1f, 0.14f));
            if (Mathf.Abs(p.x - 0.24f) < 0.02f) c = Blend(c, new Color(0f, 0f, 0f, 0.28f));

            // Рубцы поперёк бока — рыба явно не в первом бою.
            for (int i = 0; i < 3; i++)
            {
                float sx = -0.02f - i * 0.15f;
                if (Mathf.Abs(p.x - sx - p.y * 0.30f) < 0.012f && p.y > -half * 0.5f)
                    c = Blend(c, Palette.SwordScar.WithAlpha(0.5f));
            }

            // Глаз: красный, с тёмной глазницей и узким зрачком. Прежний жёлтый
            // кружок делал морду скорее удивлённой, чем злой.
            var eye = new Vector2(0.30f, 0.065f);
            float de = Vector2.Distance(p, eye);
            if (de < 0.085f) c = Blend(c, new Color(0f, 0f, 0f, 0.5f * (1f - de / 0.085f)));
            if (de < 0.050f)
            {
                c = Palette.SwordEye;

                // зрачок-щель, вытянутый вдоль тела
                if (Mathf.Abs(p.y - eye.y) < 0.012f && Mathf.Abs(p.x - eye.x) < 0.034f)
                    c = new Color(0.04f, 0.02f, 0.03f, 1f);

                c.a = 1f;
            }

            return c;
        }, 3);

        // --------------------------------------------------------- клешнерук

        /// <summary>Перевод точки в локальные координаты клыка (ось +X вдоль клыка).</summary>
        static Vector2 ToJawSpace(Vector2 p, Vector2 pivot, float angleDegrees)
        {
            Vector2 q = p - pivot;
            float a = -angleDegrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(q.x * c - q.y * s, q.x * s + q.y * c);
        }

        static Vector2 RotateVec(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>0 — мимо, 1 — тело клыка, 2 — зуб на внутренней кромке.</summary>
        static int SampleJaw(Vector2 r, float length, float baseHalf, float tipHalf, float innerSign)
        {
            if (r.x < 0f || r.x > length) return 0;

            float t = r.x / length;
            float half = Mathf.Lerp(baseHalf, tipHalf, t * t);
            float bend = -innerSign * 0.13f * t * t;   // клык загнут внутрь
            float dy = r.y - bend;

            // зубцы растут с внутренней стороны
            bool towardsInner = dy * innerSign > 0f;
            float tooth = (towardsInner && t > 0.12f && Mathf.Repeat(r.x * 11f, 1f) < 0.42f)
                ? half * 0.34f : 0f;

            if (Mathf.Abs(dy) > half + tooth) return 0;
            return Mathf.Abs(dy) > half ? 2 : 1;
        }

        /// <summary>
        /// Клешня краба. Рисуется отдельно от туши, потому что живёт своей жизнью.
        /// В открытом виде между клыками горит мишень — бить нужно точно по осевой,
        /// иначе клешня сомкнётся на страже.
        /// </summary>
        public static Sprite CrabClaw(bool open, bool mask)
        {
            string key = MaskKey("crab_claw_" + (open ? "o" : "c"), mask);
            float spread = open ? 34f : 4f;

            return Build(key, p =>
            {
                Color shell = Palette.CrabShell;
                Color dark = Palette.CrabShellDk;
                Color hilite = Palette.CrabHilite;
                Color bone = Palette.Hex("F2E3D4");

                // мишень между клыками — рисуем первой, она читается поверх фона
                if (open)
                {
                    float dm = Vector2.Distance(p, new Vector2(0.34f, 0f));
                    if (dm < 0.34f)
                    {
                        if (dm > 0.22f && dm < 0.29f) return Palette.Gold;
                        if (dm < 0.12f) return Color.white;
                        // прицельные риски
                        if (dm > 0.15f && dm < 0.21f &&
                            (Mathf.Abs(p.y) < 0.028f || Mathf.Abs(p.x - 0.34f) < 0.028f))
                            return Palette.Gold;
                        return Palette.Gold.WithAlpha(Mathf.Lerp(0.5f, 0.08f, dm / 0.34f));
                    }
                }

                Vector2 pivot = new Vector2(-0.10f, 0f);

                // внешние шипы на верхнем клыке
                for (int i = 0; i < 3; i++)
                {
                    float along = 0.16f + i * 0.26f;
                    Vector2 spikeBase = pivot + RotateVec(new Vector2(along, 0.20f - i * 0.03f), spread);
                    Vector2 spikeTip = pivot + RotateVec(new Vector2(along + 0.05f, 0.42f - i * 0.06f), spread);
                    if (TaperedSegment(p, spikeBase, spikeTip, 0.05f, 0.012f)) return dark;
                }

                int upper = SampleJaw(ToJawSpace(p, pivot, spread), 1.05f, 0.30f, 0.05f, -1f);
                int lower = SampleJaw(ToJawSpace(p, pivot, -spread), 1.05f, 0.30f, 0.05f, 1f);

                if (upper == 2 || lower == 2) return bone;
                if (upper == 1 || lower == 1)
                {
                    Color c = upper == 1 ? shell : Color.Lerp(shell, dark, 0.30f);
                    if (upper == 1 && p.y > 0.14f) c = Blend(c, hilite.WithAlpha(0.32f));
                    if (Mathf.Repeat(p.x * 5.5f, 1f) < 0.10f) c = Blend(c, new Color(0f, 0f, 0f, 0.16f));
                    c.a = 1f;
                    return c;
                }

                // сустав с кольцом сочленения
                float ex = (p.x + 0.50f) / 0.56f;
                float ey = p.y / 0.54f;
                if (ex * ex + ey * ey <= 1f)
                {
                    Color c = Color.Lerp(shell, dark, Mathf.InverseLerp(0.35f, -0.55f, p.y));
                    c.a = 1f;
                    if (Mathf.Sqrt(ex * ex + ey * ey) > 0.82f) c = Blend(c, Palette.CrabJoint.WithAlpha(0.45f));
                    if (Vector2.Distance(p, new Vector2(-0.62f, 0.22f)) < 0.19f)
                        c = Blend(c, hilite.WithAlpha(0.38f));
                    for (int i = -1; i <= 1; i++)
                        if (Vector2.Distance(p, new Vector2(-0.50f + i * 0.16f, -0.24f)) < 0.06f)
                            c = Blend(c, new Color(0f, 0f, 0f, 0.20f));
                    return c;
                }

                return Color.clear;
            });
        }

        /// <summary>Звено клешненосной руки. +X — в сторону клешни.</summary>
        public static Sprite CrabArmSegment() => Build("crab_arm", p =>
        {
            Color shell = Palette.CrabShell;
            Color dark = Palette.CrabShellDk;

            // шипы на внешней стороне звена
            for (int i = -1; i <= 1; i++)
                if (InTriangle(p, new Vector2(i * 0.34f - 0.10f, 0.30f),
                                  new Vector2(i * 0.34f + 0.10f, 0.30f),
                                  new Vector2(i * 0.34f, 0.72f)))
                    return dark;

            float ex = p.x / 0.95f;
            float ey = p.y / 0.60f;
            if (ex * ex + ey * ey > 1f) return Color.clear;

            Color c = Color.Lerp(shell, dark, Mathf.InverseLerp(0.4f, -0.7f, p.y));
            c.a = 1f;
            // кольцевые сочленения и поперечные насечки брони
            if (Mathf.Abs(p.x) > 0.70f) c = Blend(c, Palette.CrabJoint.WithAlpha(0.55f));
            if (Mathf.Repeat(p.x * 4f, 1f) < 0.12f) c = Blend(c, new Color(0f, 0f, 0f, 0.14f));
            if (p.y > 0.26f) c = Blend(c, Palette.CrabHilite.WithAlpha(0.30f));
            return c;
        });

        /// <summary>
        /// Туша краба: панцирь с зубчатой кромкой и шипами, глаза на стебельках под
        /// тяжёлыми надбровьями, четыре пары ходильных ног с когтями, боевые шрамы.
        ///
        /// Текстура широкая и плоская (v в пределах ±0.5): так и краб выглядит крабом,
        /// и туша при большой ширине не упирается в HUD сверху и в пузырёк снизу.
        /// Всё рисование держим внутри ±0.48, иначе ноги и шипы обрежутся границей.
        /// </summary>
        public static Sprite CrabBody(bool mask) => BuildRect(MaskKey("crab_body", mask), 400, 200, p =>
        {
            Color shell = Palette.CrabShell;
            Color dark = Palette.CrabShellDk;
            Color hilite = Palette.CrabHilite;
            Color joint = Palette.CrabJoint;

            // ---- ходильные ноги: четыре пары, каждая с коленом и когтем.
            // Отсекаем заранее: иначе на каждый сэмпл приходилось 32 проверки отрезков.
            if (p.y < 0.08f && Mathf.Abs(p.x) > 0.36f)
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float spread = 0.46f + i * 0.13f;
                    Vector2 hip = new Vector2(side * spread, -0.02f - i * 0.01f);
                    Vector2 knee = new Vector2(side * (spread + 0.22f), -0.14f + i * 0.035f);
                    Vector2 ankle = new Vector2(side * (spread + 0.28f), -0.33f + i * 0.03f);
                    Vector2 toe = new Vector2(side * (spread + 0.19f), -0.46f + i * 0.025f);

                    if (Vector2.Distance(p, knee) < 0.062f) return joint;
                    if (TaperedSegment(p, hip, knee, 0.070f, 0.052f)) return Color.Lerp(dark, shell, 0.45f);
                    if (TaperedSegment(p, knee, ankle, 0.052f, 0.032f)) return Color.Lerp(dark, shell, 0.30f);
                    if (TaperedSegment(p, ankle, toe, 0.030f, 0.008f)) return joint;
                }
            }

            // ---- глаза на стебельках под тяжёлым надбровьем
            if (p.y > 0.02f && Mathf.Abs(p.x) < 0.40f)
            for (int side = -1; side <= 1; side += 2)
            {
                Vector2 root = new Vector2(side * 0.20f, 0.10f);
                Vector2 tip = new Vector2(side * 0.26f, 0.33f);
                if (TaperedSegment(p, root, tip, 0.055f, 0.044f)) return dark;

                Vector2 eye = tip + new Vector2(0f, 0.06f);
                float de = Vector2.Distance(p, eye);
                if (de < 0.095f)
                {
                    if (p.y > eye.y + 0.032f) return dark;   // надбровье прищуривает глаз
                    if (Vector2.Distance(p, eye + new Vector2(side * 0.02f, -0.008f)) < 0.042f)
                        return new Color(0.05f, 0.02f, 0.03f, 1f);
                    return Palette.CrabHilite;
                }
            }

            // ---- зубчатая кромка панциря
            if (p.y < -0.11f && p.y > -0.36f)
            {
                float rimT = Mathf.InverseLerp(-1f, 1f, p.x);
                float rimY = -0.20f - 0.06f * Mathf.Cos(p.x * 2.6f);
                if (p.y < rimY + 0.015f && p.y > rimY - 0.085f && Mathf.Abs(p.x) < 0.94f &&
                    Mathf.Repeat(rimT * 17f, 1f) < 0.5f)
                    return Color.Lerp(dark, joint, 0.4f);
            }

            // ---- шипы по верху панциря
            if (p.y > 0.08f)
            for (int i = -3; i <= 3; i++)
            {
                float x0 = i * 0.25f;
                float baseY = 0.20f - Mathf.Abs(i) * 0.035f;
                float tipY = baseY + 0.17f - Mathf.Abs(i) * 0.018f;
                if (InTriangle(p, new Vector2(x0 - 0.058f, baseY), new Vector2(x0 + 0.058f, baseY),
                                  new Vector2(x0 + i * 0.015f, tipY)))
                    return dark;
            }

            // ---- панцирь: широкий и низкий
            float ex = p.x / 0.97f;
            float ey = (p.y - 0.01f) / 0.26f;
            if (ex * ex + ey * ey > 1f) return Color.clear;

            Color c = Color.Lerp(shell, dark, Mathf.InverseLerp(0.16f, -0.26f, p.y));
            c.a = 1f;

            // радиальные рёбра от центра панциря
            float ang = Mathf.Atan2((p.y - 0.01f) * 3.7f, p.x);
            if (Mathf.Repeat(ang * 3.5f, 1f) < 0.10f) c = Blend(c, new Color(0f, 0f, 0f, 0.14f));

            // бугры брони
            for (int i = -3; i <= 3; i++)
            {
                float dw = Vector2.Distance(p, new Vector2(i * 0.24f, 0.06f - Mathf.Abs(i) * 0.022f));
                if (dw < 0.062f) c = Blend(c, hilite.WithAlpha(0.30f));
                else if (dw < 0.082f) c = Blend(c, new Color(0f, 0f, 0f, 0.18f));
            }

            // боевые шрамы
            if (TaperedSegment(p, new Vector2(-0.58f, 0.14f), new Vector2(-0.30f, 0.02f), 0.017f, 0.006f) ||
                TaperedSegment(p, new Vector2(0.38f, 0.13f), new Vector2(0.62f, 0.06f), 0.014f, 0.005f))
                c = Blend(c, hilite.WithAlpha(0.45f));

            // ротовые пластины
            if (Mathf.Abs(p.x) < 0.14f && p.y < -0.09f && p.y > -0.22f)
            {
                c = Blend(c, new Color(0f, 0f, 0f, 0.35f));
                if (Mathf.Abs(Mathf.Abs(p.x) - 0.07f) < 0.02f) c = Blend(c, joint.WithAlpha(0.7f));
            }

            if (p.y < -0.16f) c = Blend(c, new Color(0f, 0f, 0f, 0.22f));
            if (p.y > 0.15f) c = Blend(c, hilite.WithAlpha(0.22f));

            return c;
        }, 3);

        // -------------------------------------------------------- белая смерть

        /// <summary>
        /// Кромка челюсти с рядом зубов. 0 — мимо, 1 — сама челюсть, 2 — зуб.
        ///
        /// Зубы растут только в одну сторону (<paramref name="toothSide"/>) — внутрь
        /// пасти. Снаружи челюсть гладкая, иначе акула выглядит колючей со спины.
        /// </summary>
        static int SampleSharkJaw(Vector2 p, Vector2 from, Vector2 to,
                                  float jawHalf, float toothLen, float toothSide, float teeth)
        {
            Vector2 axis = to - from;
            float length = axis.magnitude;
            if (length < 0.0001f) return 0;

            Vector2 dir = axis / length;
            Vector2 normal = new Vector2(-dir.y, dir.x);
            Vector2 q = p - from;

            float along = Vector2.Dot(q, dir);
            if (along < 0f || along > length) return 0;

            float side = Vector2.Dot(q, normal) * toothSide;
            if (side < -jawHalf) return 0;
            if (side <= jawHalf) return 1;

            // пилообразный профиль: каждый зуб — треугольник, к углам рта они мельче
            float t = along / length;
            float saw = 1f - Mathf.Abs(Mathf.Repeat(t * teeth, 1f) * 2f - 1f);
            float taper = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * 0.45f + 0.55f;
            return side - jawHalf <= saw * toothLen * taper ? 2 : 0;
        }

        /// <summary>
        /// Акула, смотрит в +X. Массивная спереди, с резкой границей тёмной спины и
        /// белого брюха: в профиль читается целиком даже на тёмной воде.
        ///
        /// Два состояния пасти — это две её атаки, и различать их игрок обязан
        /// мгновенно. Сомкнутая пасть значит таран рылом в пузырёк: подставляй стража.
        /// Раскрытая значит охоту за самим стражем: уводи, иначе схватит. Поэтому
        /// раскрытая пасть меняет силуэт целиком — отвисшая челюсть, красный зев и
        /// два ряда зубов видны боковым зрением, без разглядывания.
        /// </summary>
        public static Sprite Shark(bool open, bool mask) => BuildRect(
            MaskKey(open ? "shark_open" : "shark_shut", mask), 256, 128, p =>
        {
            Color back  = Palette.SharkBack;
            Color body  = Palette.SharkBody;
            Color belly = Palette.SharkBelly;
            Color fin   = Palette.SharkFin;
            Color maw   = Palette.SharkMaw;
            Color tooth = Palette.SharkTooth;

            const float NoseX = 0.88f;
            const float TailX = -0.72f;

            // ---- пасть. Рисуется первой: раскрытая челюсть выходит за обвод корпуса,
            // и корпус не должен её срезать.
            if (open)
            {
                Vector2 hinge = new Vector2(0.20f, -0.07f);
                Vector2 upper = new Vector2(0.84f, 0.00f);
                Vector2 lower = new Vector2(0.66f, -0.46f);

                int top = SampleSharkJaw(p, hinge, upper, 0.022f, 0.080f, -1f, 11f);
                int bottom = SampleSharkJaw(p, hinge, lower, 0.036f, 0.070f, 1f, 9f);

                if (top == 2 || bottom == 2) return tooth;
                if (bottom == 1) return belly;                 // отвисшая нижняя челюсть
                if (InTriangle(p, hinge, upper, lower))
                {
                    // глубина зева: у горла темнее, чем у кромки зубов
                    float depth = Mathf.InverseLerp(0.78f, 0.22f, p.x);
                    return Color.Lerp(maw, maw * 0.35f, depth).WithAlpha(1f);
                }
            }

            // ---- плавники. Рисуются ДО корпуса и основаниями заходят внутрь него:
            // приставленные снаружи, они торчали отдельными шипами, а не росли из тела.
            // спинной — главный опознавательный знак, с оттянутой назад вершиной
            if (InTriangle(p, new Vector2(0.22f, 0.10f), new Vector2(-0.28f, 0.08f), new Vector2(-0.06f, 0.50f)))
                return fin;
            // второй спинной, маленький
            if (InTriangle(p, new Vector2(-0.44f, 0.06f), new Vector2(-0.60f, 0.05f), new Vector2(-0.52f, 0.19f)))
                return fin;
            // грудной — длинный серп, отведён вниз и назад
            if (InTriangle(p, new Vector2(0.28f, -0.06f), new Vector2(-0.08f, -0.47f), new Vector2(0.06f, -0.04f)))
                return fin;
            // хвост: верхняя лопасть заметно длиннее нижней
            if (InTriangle(p, new Vector2(-0.62f, 0.05f), new Vector2(-1.00f, 0.46f), new Vector2(-0.78f, -0.02f)) ||
                InTriangle(p, new Vector2(-0.62f, -0.05f), new Vector2(-0.92f, -0.30f), new Vector2(-0.78f, 0.02f)))
                return fin;

            // ---- корпус
            float u = Mathf.InverseLerp(NoseX, TailX, p.x);
            if (u < 0f || u > 1f) return Color.clear;

            // Рыло — конус, а не полукруг. Степень больше единицы даёт остриё:
            // с прежним sin-профилем морда расширялась сразу от кончика и читалась тупой.
            float half = u < 0.34f
                ? 0.30f * Mathf.Pow(u / 0.34f, 1.15f)
                : 0.30f * Mathf.Pow(1f - (u - 0.34f) / 0.66f, 0.50f);
            if (u > 0.74f) half = Mathf.Max(half, 0.052f);      // хвостовой стебель не сходит в нить

            float halfUp = half * 0.90f;
            float halfDown = half * 1.10f;

            // Рыло нависает над пастью: снизу оно подрезано, сверху нет. Это самый
            // узнаваемый профиль акулы — у рыбы без подреза морда выглядит тупой.
            if (u < 0.30f) halfDown *= Mathf.Lerp(0.45f, 1f, u / 0.30f);

            if (p.y > halfUp || p.y < -halfDown) return Color.clear;

            // Тёмная спина занимает почти всё тело, брюхо — узкая светлая полоса понизу.
            // Раньше граница шла по средней линии, и зверь выглядел светлым и безобидным.
            float shade = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(-halfDown * 0.62f, -halfDown * 0.02f, p.y));
            Color c = shade < 0.5f
                ? Color.Lerp(belly, body, shade * 2f)
                : Color.Lerp(body, back, (shade - 0.5f) * 2f);
            c.a = 1f;

            // резкая светлая кромка на стыке спины и брюха
            if (Mathf.Abs(p.y + halfDown * 0.36f) < 0.014f)
                c = Blend(c, new Color(1f, 1f, 1f, 0.30f));

            // жаберные щели — пять косых прорезей за головой
            if (p.x > 0.26f && p.x < 0.54f && p.y < halfUp * 0.65f && p.y > -halfDown * 0.55f)
            {
                float rel = (p.x - 0.26f) + p.y * 0.20f;
                if (Mathf.Repeat(rel * 20f, 1f) < 0.32f)
                    c = Blend(c, Palette.SharkGill.WithAlpha(0.6f));
            }

            // старые шрамы поперёк спины: зверь бывалый, а не музейный
            if (p.y > -halfDown * 0.1f)
            {
                for (int i = 0; i < 3; i++)
                {
                    float sx = -0.10f - i * 0.16f;
                    if (Mathf.Abs(p.x - sx - p.y * 0.35f) < 0.011f && p.y < halfUp * 0.85f)
                        c = Blend(c, new Color(1f, 1f, 1f, 0.16f));
                }
            }

            // сомкнутая пасть: длинная щель с торчащими наружу зубами
            if (!open)
            {
                int line = SampleSharkJaw(p, new Vector2(0.22f, -0.12f), new Vector2(0.82f, -0.03f),
                                          0.012f, 0.034f, -1f, 11f);
                if (line == 2) return tooth;
                if (line == 1) c = Blend(c, new Color(0f, 0f, 0f, 0.70f));
            }

            // глаз: чёрный, в тёмной глазнице и без блика — взгляд у акулы мёртвый
            var eye = new Vector2(0.56f, 0.070f);
            float de = Vector2.Distance(p, eye);
            if (de < 0.085f) c = Blend(c, new Color(0f, 0f, 0f, 0.45f * (1f - de / 0.085f)));
            if (de < 0.048f)
            {
                c = Palette.SharkEye;
                c.a = 1f;
            }

            return c;
        }, 3);
    }
}
