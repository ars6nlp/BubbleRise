using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Рисунки трёх новых локаций — один атлас и один материал на все три: в кадре
    /// бывает только одна локация, а вызов отрисовки на слой остаётся один.
    /// </summary>
    public enum ZonePiece
    {
        // «Затонувший корабль»
        WPlankA, WPlankB, WFrame, WPorthole, WRigging, WWheel, WCrateA, WCrateB, WBarrel,
        WLantern, WSail, WMast, WBreach, WHatch, WFarHull,
        // «Китовые рёбра»
        RBoneSeg, RRibArc, RVertebraA, RVertebraB, RSkull, RBaleen, RBones, RWorms, RCucumber,
        RBarnacle, RFluke, RFarCage,
        // «Парящий сад»
        GIslandA, GIslandB, GIslandC, GVineA, GVineB, GMoss, GFlower, GKelp, GPebbles, GBridge,
        GArch, GFarIslands,
        // общее: мягкое пятно света (фонари, огоньки планктона)
        Glow,
        Count
    }

    public static partial class SpriteFactory
    {
        static Texture2D _zonesAtlas;
        static Material _zonesMat;
        static AtlasPiece[] _zones;

        public static Material ZonesMaterial { get { EnsureZonesAtlas(); return _zonesMat; } }
        public static Texture2D ZonesAtlasTexture { get { EnsureZonesAtlas(); return _zonesAtlas; } }
        public static AtlasPiece Zone(ZonePiece piece) { EnsureZonesAtlas(); return _zones[(int)piece]; }

        static void EnsureZonesAtlas()
        {
            if (_zonesAtlas != null) return;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            _zones = new AtlasPiece[(int)ZonePiece.Count];
            _zonesAtlas = BakedOrPaintAtlas("zones_atlas", ZonesDefs(), 1024, _zones);
            _zonesMat = new Material(Unlit) { name = "BubbleRise_Zones", mainTexture = _zonesAtlas };
            WorldAtlasMs += sw.ElapsedMilliseconds;
        }

        // ------------------------------------------------------------ цвета
        //
        // Приглушённые, без белого (сумма каналов ≤ 2.3) и мимо цветов угроз:
        // красно-коричневого коралла, оранжевого уголька, розово-фиолетового ежа.
        // Дерево — оливково-бурое, с водорослями; кость — серо-жёлтая; сад —
        // зелень и бирюза. Дальше их ещё топит туман слоя.

        static readonly Color WoodFill = Palette.Hex("6A5E46");
        static readonly Color WoodDark = Palette.Hex("3E3A2C");
        static readonly Color BrassFill = Palette.Hex("8C7C50");
        static readonly Color RopeFill = Palette.Hex("6E6A52");
        static readonly Color CanvasFill = Palette.Hex("7E8072");
        static readonly Color GlassFill = Palette.Hex("1C2826");
        static readonly Color LampFill = Palette.Hex("B09A62");
        static readonly Color BoneFill = Palette.Hex("B0AA98");
        static readonly Color BoneShade = Palette.Hex("7A766A");
        static readonly Color BaleenFill = Palette.Hex("4A463C");
        static readonly Color IslandRock = Palette.Hex("56625C");
        static readonly Color IslandMoss = Palette.Hex("4C7854");
        static readonly Color VineFill = Palette.Hex("3E6A4A");
        static readonly Color LeafFill = Palette.Hex("5A8A5C");

        static List<PieceDef> ZonesDefs()
        {
            var l = new List<PieceDef>();
            // ---- корабль
            Def(l, ZonePiece.WPlankA, 192, 48, 2, p => Plank(p, 1.3f));
            Def(l, ZonePiece.WPlankB, 192, 48, 2, p => Plank(p, 6.1f));
            Def(l, ZonePiece.WFrame, 64, 256, 2, FrameTimber);
            Def(l, ZonePiece.WPorthole, 96, 96, 2, Porthole);
            Def(l, ZonePiece.WRigging, 160, 192, 1, Rigging);
            Def(l, ZonePiece.WWheel, 128, 128, 2, ShipWheel);
            Def(l, ZonePiece.WCrateA, 96, 88, 2, p => Crate(p, 2.2f, false));
            Def(l, ZonePiece.WCrateB, 96, 88, 2, p => Crate(p, 7.4f, true));
            Def(l, ZonePiece.WBarrel, 72, 96, 2, Barrel);
            Def(l, ZonePiece.WLantern, 48, 96, 2, Lantern);
            Def(l, ZonePiece.WSail, 160, 192, 1, Sail);
            Def(l, ZonePiece.WMast, 48, 320, 2, Mast);
            Def(l, ZonePiece.WBreach, 128, 256, 1, Breach);
            Def(l, ZonePiece.WHatch, 256, 96, 1, Hatch);
            Def(l, ZonePiece.WFarHull, 256, 128, 1, FarHull);
            // ---- рёбра
            Def(l, ZonePiece.RBoneSeg, 160, 48, 2, BoneSegment);
            Def(l, ZonePiece.RRibArc, 256, 128, 1, RibArc);
            Def(l, ZonePiece.RVertebraA, 96, 96, 2, p => Vertebra(p, 1.7f));
            Def(l, ZonePiece.RVertebraB, 96, 96, 2, p => Vertebra(p, 5.3f));
            Def(l, ZonePiece.RSkull, 256, 160, 1, Skull);
            Def(l, ZonePiece.RBaleen, 256, 96, 1, Baleen);
            Def(l, ZonePiece.RBones, 128, 48, 2, BoneScatter);
            Def(l, ZonePiece.RWorms, 64, 80, 2, p => TubeWorms(p, 3.7f, Palette.Hex("8AA0A0")));
            Def(l, ZonePiece.RCucumber, 112, 48, 2, SeaCucumber);
            Def(l, ZonePiece.RBarnacle, 64, 40, 2, BarnacleCluster);
            Def(l, ZonePiece.RFluke, 256, 128, 1, Fluke);
            Def(l, ZonePiece.RFarCage, 256, 128, 1, FarCage);
            // ---- сад
            Def(l, ZonePiece.GIslandA, 256, 176, 1, p => Island(p, 1.9f, 1f));
            Def(l, ZonePiece.GIslandB, 224, 160, 1, p => Island(p, 5.7f, 0.8f));
            Def(l, ZonePiece.GIslandC, 160, 112, 2, p => Island(p, 9.2f, 0.6f));
            Def(l, ZonePiece.GVineA, 48, 224, 2, p => Vine(p, 2.4f));
            Def(l, ZonePiece.GVineB, 48, 160, 2, p => Vine(p, 6.6f));
            Def(l, ZonePiece.GMoss, 96, 48, 2, Moss);
            Def(l, ZonePiece.GFlower, 64, 80, 2, p => Anemone(p, 5.1f, Palette.Hex("4E8A7A"), Palette.Hex("B4CCA0")));
            // не веер: круглый веер крупно читается как сетка-мяч [ТЕСТ, руины]
            Def(l, ZonePiece.GKelp, 48, 160, 2, p => KelpStrand(p, 3.3f, false));
            Def(l, ZonePiece.GPebbles, 112, 48, 2, FloatingPebbles);
            Def(l, ZonePiece.GBridge, 256, 80, 1, VineBridge);
            Def(l, ZonePiece.GArch, 256, 176, 1, IslandArch);
            Def(l, ZonePiece.GFarIslands, 256, 128, 1, FarIslands);
            // ---- свет
            Def(l, ZonePiece.Glow, 32, 32, 2, SoftGlow);
            return l;
        }

        // ------------------------------------------------------------ помощники

        static float SdBox(Vector2 p, Vector2 c, Vector2 half)
        {
            Vector2 d = new Vector2(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y);
            float outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
        }

        /// <summary>Расстояние до отрезка за вычетом радиуса — капсула.</summary>
        static float SdCapsule(Vector2 p, Vector2 a, Vector2 b, float r) => SegDist(p, a, b) - r;

        /// <summary>Мягкий шум по длине волокна — годовые полосы дерева.</summary>
        static float Grain(Vector2 p, float seed) => 0.86f + 0.14f * Fbm(p.x * 2.2f + seed, p.y * 34f, 2);

        // ------------------------------------------------------------ корабль

        /// <summary>Доска обшивки: ровный пропил слева, расщеплённый конец справа, гвозди, водоросли.</summary>
        static Color Plank(Vector2 p, float seed)
        {
            // p.x −1..1, p.y −0.25..0.25
            float warp = 0.02f * Mathf.Sin(p.x * 3f + seed);
            float end = 0.9f - 0.22f * Mathf.Abs(Mathf.Sin(p.y * 38f + seed)) * Hash(Mathf.Floor(p.y * 12f), seed);
            Func<Vector2, float> sdf = q => Mathf.Max(Mathf.Abs(q.y - warp) - 0.15f, Mathf.Max(q.x - end, -0.96f - q.x));
            Color c = Lit(sdf, p, WoodFill, 0.018f);
            if (c.a <= 0f) return c;
            c = Mul(c, Grain(p, seed));
            for (int i = 0; i < 2; i++)
            {
                Vector2 nail = new Vector2(i == 0 ? -0.78f : 0.42f, warp + 0.06f * (i * 2 - 1));
                if ((p - nail).magnitude < 0.03f) c = Mul(WoodDark, 0.8f);
            }
            if (Fbm(p.x * 5f + seed, p.y * 8f, 2) * SStep(-0.05f, 0.15f, p.y - warp) > 0.5f)
                c = Color.Lerp(c, AlgaeFill, 0.5f);
            c.a = 1f;
            return c;
        }

        /// <summary>Шпангоут: изогнутое ребро корпуса, обломанное сверху.</summary>
        static Color FrameTimber(Vector2 p)
        {
            // p.x −0.25..0.25, p.y −1..1
            float top = 0.82f + 0.1f * Hash(Mathf.Floor(p.x * 30f), 3.1f);
            Func<Vector2, float> sdf = q =>
            {
                float xc = 0.1f * (1f - q.y * q.y) - 0.05f;
                float w = Mathf.Lerp(0.1f, 0.07f, (q.y + 1f) * 0.5f);
                return Mathf.Max(Mathf.Abs(q.x - xc) - w, Mathf.Max(q.y - top, -0.98f - q.y));
            };
            Color c = Lit(sdf, p, WoodFill, 0.015f);
            if (c.a <= 0f) return c;
            c = Mul(c, 0.86f + 0.14f * Fbm(p.x * 30f, p.y * 2.5f, 2));
            if (Fbm(p.x * 9f, p.y * 4f + 2f, 2) > 0.6f) c = Color.Lerp(c, AlgaeFill, 0.45f);
            return c;
        }

        /// <summary>Иллюминатор: латунное кольцо с заклёпками, тёмное стекло, ровный тусклый свет внутри.</summary>
        static Color Porthole(Vector2 p)
        {
            float r = p.magnitude;
            if (r > 0.92f) return Color.clear;
            if (r < 0.6f)
            {
                float k = 1f - r / 0.6f;
                Color g = Color.Lerp(GlassFill, Mul(LampFill, 0.55f), k * k * 0.6f);
                // блик на стекле — неподвижный, сверху слева
                if ((p - new Vector2(-0.22f, 0.24f)).magnitude < 0.1f) g = Color.Lerp(g, Mul(LampFill, 0.7f), 0.35f);
                g.a = 1f;
                return g;
            }
            Func<Vector2, float> sdf = q => Mathf.Abs(q.magnitude - 0.76f) - 0.15f;
            Color c = Lit(sdf, p, BrassFill, 0.02f);
            float ang = Mathf.Atan2(p.y, p.x);
            float rivet = Mathf.Abs(Mathf.Repeat(ang / (Mathf.PI * 2f) * 10f, 1f) - 0.5f);
            if (rivet < 0.08f && Mathf.Abs(r - 0.78f) < 0.05f) c = Mul(c, 1.2f);
            if (p.y < -0.3f && Fbm(p.x * 6f, p.y * 6f, 2) > 0.55f) c = Color.Lerp(c, AlgaeFill, 0.55f);
            c.a = 1f;
            return c;
        }

        /// <summary>Такелаж: провисшая верёвочная сеть, рваная снизу.</summary>
        static Color Rigging(Vector2 p)
        {
            // p.x −0.83..0.83, p.y −1..1
            float bottom = -0.75f + 0.35f * Fbm(p.x * 3f, 4.2f, 2);
            if (p.y < bottom || p.y > 0.95f || Mathf.Abs(p.x) > 0.8f) return Color.clear;
            float sag = 0.18f * (1f - p.x * p.x);
            float y = p.y + sag;
            float u = Mathf.Repeat((p.x + y * 0.55f) * 4.5f, 1f), v = Mathf.Repeat((p.x - y * 0.55f) * 4.5f, 1f);
            bool rope = Mathf.Abs(u - 0.5f) < 0.06f || Mathf.Abs(v - 0.5f) < 0.06f || p.y > 0.88f;
            if (!rope) return Color.clear;
            if (Fbm(p.x * 2.5f + 1f, p.y * 2.5f, 2) > 0.64f) return Color.clear;   // дыры
            Color c = Mul(RopeFill, 0.8f + 0.3f * SStep(-1f, 1f, p.y));
            c.a = 1f;
            return c;
        }

        /// <summary>Штурвал: обод, восемь спиц с рукоятями (одна обломана), латунная ступица.</summary>
        static Color ShipWheel(Vector2 p)
        {
            float r = p.magnitude;
            float ang = Mathf.Atan2(p.y, p.x);
            float spokeA = Mathf.Repeat(ang / (Mathf.PI * 2f) * 8f + 0.5f, 1f) - 0.5f;
            int spoke = Mathf.FloorToInt(Mathf.Repeat(ang / (Mathf.PI * 2f) * 8f + 0.5f, 8f));
            bool broken = spoke == 5;
            Func<Vector2, float> sdf = q =>
            {
                float rr = q.magnitude;
                float a = Mathf.Atan2(q.y, q.x);
                float s = (Mathf.Repeat(a / (Mathf.PI * 2f) * 8f + 0.5f, 1f) - 0.5f) * rr * Mathf.PI * 2f / 8f;
                int sp = Mathf.FloorToInt(Mathf.Repeat(a / (Mathf.PI * 2f) * 8f + 0.5f, 8f));
                float reach = sp == 5 ? 0.7f : 0.96f;
                float spokeD = Mathf.Max(Mathf.Abs(s) - (rr > 0.72f ? 0.06f : 0.035f), rr - reach);
                float rim = Mathf.Abs(rr - 0.6f) - 0.06f;
                float hub = rr - 0.16f;
                return Mathf.Min(Mathf.Min(spokeD, rim), hub);
            };
            Color c = Lit(sdf, p, r < 0.17f ? BrassFill : WoodFill, 0.02f);
            if (c.a <= 0f) return c;
            if (!broken && r > 0.72f) c = Mul(c, 0.92f + 0.1f * Mathf.Cos(spokeA * 12f));
            c = Mul(c, Grain(new Vector2(r * 3f, ang), 2.7f));
            return c;
        }

        /// <summary>Ящик: доски, косая распорка; у сломанного одной доски нет.</summary>
        static Color Crate(Vector2 p, float seed, bool broken)
        {
            // p.x −1..1, p.y −0.92..0.92
            Func<Vector2, float> sdf = q => SdBox(q, Vector2.zero, new Vector2(0.88f, 0.8f)) - 0.03f;
            float d = sdf(p);
            if (d > 0f) return Color.clear;
            int row = Mathf.FloorToInt((p.y + 0.83f) / 0.415f);
            if (broken && row == 2 && p.x > -0.55f + 0.2f * Hash(Mathf.Floor(p.y * 20f), seed) && d < -0.1f)
                return WithA(Mul(WoodDark, 0.6f), 1f);   // в дыре темно
            Color c = Lit(sdf, p, WoodFill, 0.02f);
            c = Mul(c, Grain(p, seed + row));
            float seam = Mathf.Abs(Mathf.Repeat((p.y + 0.83f) / 0.415f, 1f) - 0.5f);
            if (seam > 0.46f) c = Mul(c, 0.7f);
            float brace = Mathf.Abs(p.y - p.x * 0.85f);
            if (brace < 0.08f && Mathf.Abs(p.x) < 0.8f) c = Color.Lerp(c, Mul(WoodFill, 1.08f), 0.7f);
            if (Mathf.Abs(Mathf.Abs(p.x) - 0.8f) < 0.07f) c = Mul(c, 0.88f);
            c.a = 1f;
            return c;
        }

        /// <summary>Бочка: клёпки, два обруча.</summary>
        static Color Barrel(Vector2 p)
        {
            // p.x −0.75..0.75, p.y −1..1
            Func<Vector2, float> sdf = q =>
            {
                float w = 0.56f + 0.13f * (1f - q.y * q.y);
                return Mathf.Max(Mathf.Abs(q.x) - w, Mathf.Abs(q.y) - 0.92f);
            };
            Color c = Lit(sdf, p, WoodFill, 0.02f);
            if (c.a <= 0f) return c;
            float stave = Mathf.Abs(Mathf.Repeat(p.x * 5f, 1f) - 0.5f);
            if (stave > 0.45f) c = Mul(c, 0.8f);
            if (Mathf.Abs(Mathf.Abs(p.y) - 0.55f) < 0.07f) c = Mul(BrassFill, 0.7f + 0.3f * SStep(-1f, 1f, p.x * -1f));
            c.a = 1f;
            return c;
        }

        /// <summary>Фонарь: кольцо, колпак, стекло с ровным тусклым светом, донце.</summary>
        static Color Lantern(Vector2 p)
        {
            // p.x −0.5..0.5, p.y −1..1
            float ringD = Mathf.Abs((p - new Vector2(0f, 0.85f)).magnitude - 0.1f) - 0.025f;
            if (ringD < 0f) return Mul(BrassFill, 0.85f);
            if (p.y > 0.5f && p.y < 0.74f && Mathf.Abs(p.x) < 0.18f + (0.74f - p.y) * 0.7f)
                return WithA(Mul(BrassFill, 0.9f + 0.2f * SStep(0.5f, 0.74f, p.y)), 1f);
            if (p.y > -0.5f && p.y <= 0.5f && Mathf.Abs(p.x) < 0.3f)
            {
                if (Mathf.Abs(p.x) > 0.26f || Mathf.Abs(p.x) < 0.02f) return WithA(Mul(BrassFill, 0.75f), 1f);
                float k = 1f - Mathf.Abs(p.y) * 1.4f;
                return WithA(Color.Lerp(Mul(LampFill, 0.55f), LampFill, Sat(k) * 0.6f), 1f);
            }
            if (p.y > -0.7f && p.y <= -0.5f && Mathf.Abs(p.x) < 0.34f) return WithA(Mul(BrassFill, 0.7f), 1f);
            return Color.clear;
        }

        /// <summary>Парус: полотнище с реи, складки, рваный низ, дыры.</summary>
        static Color Sail(Vector2 p)
        {
            // p.x −0.83..0.83, p.y −1..1
            if (p.y > 0.86f && p.y < 0.96f && Mathf.Abs(p.x) < 0.82f) return WithA(Mul(WoodFill, 0.9f), 1f);   // рея
            float edge = 0.7f + 0.06f * Mathf.Sin(p.y * 6f);
            float bottom = -0.55f + 0.3f * Fbm(p.x * 3.5f, 7.7f, 2) + 0.15f * Mathf.Abs(Mathf.Sin(p.x * 9f));
            if (Mathf.Abs(p.x) > edge || p.y > 0.86f || p.y < bottom) return Color.clear;
            if (Fbm(p.x * 3f + 2f, p.y * 3f, 2) > 0.67f) return Color.clear;
            float fold = 0.85f + 0.15f * Mathf.Sin(p.x * 10f + p.y * 1.5f);
            Color c = Mul(CanvasFill, fold * (0.85f + 0.2f * SStep(-1f, 1f, p.y)));
            if (Fbm(p.x * 6f, p.y * 6f + 1f, 2) > 0.6f) c = Color.Lerp(c, AlgaeFill, 0.35f);
            c.a = 1f;
            return c;
        }

        /// <summary>Мачта: обломанный сверху столб с железными бугелями.</summary>
        static Color Mast(Vector2 p)
        {
            // p.x −0.15..0.15, p.y −1..1
            float top = 0.9f - 0.08f * Hash(Mathf.Floor(p.x * 60f), 5.5f);
            Func<Vector2, float> sdf = q => Mathf.Max(Mathf.Abs(q.x) - Mathf.Lerp(0.1f, 0.07f, (q.y + 1f) * 0.5f),
                                                      Mathf.Max(q.y - top, -0.99f - q.y));
            Color c = Lit(sdf, p, WoodFill, 0.012f);
            if (c.a <= 0f) return c;
            c = Mul(c, 0.86f + 0.14f * Fbm(p.x * 40f, p.y * 3f, 2));
            float band = Mathf.Min(Mathf.Abs(p.y + 0.6f), Mathf.Min(Mathf.Abs(p.y - 0.1f), Mathf.Abs(p.y - 0.62f)));
            if (band < 0.025f) c = Mul(BrassFill, 0.65f);
            c.a = 1f;
            return c;
        }

        /// <summary>Пробоина: край борта — доски торчат из стены разной длины, за ними шпангоут.</summary>
        static Color Breach(Vector2 p)
        {
            // p.x −0.5..0.5, p.y −1..1; стена слева, дыра справа
            float row = Mathf.Floor((p.y + 1f) / 0.15f);
            float fy = Mathf.Repeat((p.y + 1f) / 0.15f, 1f);
            float end = -0.15f + 0.55f * Hash(row, 3.3f) - 0.12f * Mathf.Abs(Mathf.Sin(fy * 9f + row));
            if (Mathf.Abs(p.x + 0.38f) < 0.05f) return WithA(Mul(WoodDark, 1.1f), 1f);   // шпангоут
            if (p.x > end || fy < 0.08f) return Color.clear;
            Color c = Mul(WoodFill, (0.75f + 0.3f * fy) * Grain(p, row));
            if (Fbm(p.x * 6f, p.y * 3f, 2) > 0.58f) c = Color.Lerp(c, AlgaeFill, 0.4f);
            c.a = 1f;
            return c;
        }

        /// <summary>Люк палубы: рама и решётка — в просветах видна вода.</summary>
        static Color Hatch(Vector2 p)
        {
            // p.x −1..1, p.y −0.375..0.375
            float outer = SdBox(p, Vector2.zero, new Vector2(0.96f, 0.33f));
            if (outer > 0f) return Color.clear;
            float inner = SdBox(p, Vector2.zero, new Vector2(0.76f, 0.2f));
            bool bar = Mathf.Abs(Mathf.Repeat(p.x * 6f, 1f) - 0.5f) > 0.32f || Mathf.Abs(p.y) < 0.03f;
            if (inner < 0f && !bar) return Color.clear;
            Func<Vector2, float> sdf = q => SdBox(q, Vector2.zero, new Vector2(0.96f, 0.33f));
            Color c = Lit(sdf, p, inner < 0f ? Mul(WoodFill, 0.9f) : WoodFill, 0.02f);
            c = Mul(c, Grain(p, 4.4f));
            c.a = 1f;
            return c;
        }

        /// <summary>Дальний корпус: силуэт лежащего наискось судна с двумя мачтами, одна сломана.</summary>
        static Color FarHull(Vector2 p)
        {
            // p.x −1..1, p.y −0.5..0.5
            Vector2 q = new Vector2(p.x * 0.97f + p.y * 0.12f, p.y - p.x * 0.14f);
            float deck = 0.02f;
            float keel = -0.3f + 0.25f * q.x * q.x;
            bool hull = q.y < deck && q.y > keel && Mathf.Abs(q.x) < 0.88f + 0.08f * q.y;
            bool mast1 = Mathf.Abs(q.x + 0.3f) < 0.015f && q.y > 0f && q.y < 0.46f;
            bool mast2 = Mathf.Abs(q.x - 0.25f + (q.y - deck) * 0.6f) < 0.015f && q.y > 0f && q.y < 0.22f;
            bool yard = Mathf.Abs(q.y - 0.32f) < 0.012f && Mathf.Abs(q.x + 0.3f) < 0.2f;
            if (!hull && !mast1 && !mast2 && !yard) return Color.clear;
            Color c = Mul(SilhouetteFill, 0.8f + 0.25f * SStep(-0.3f, 0.4f, q.y));
            if (hull && Mathf.Abs(Mathf.Repeat(q.x * 9f, 1f) - 0.5f) > 0.46f && q.y < -0.05f) c = Mul(c, 0.85f);
            c.a = 1f;
            return c;
        }

        // ------------------------------------------------------------ рёбра

        /// <summary>Отрезок кости с утолщениями на концах — из них складываются рёбра.</summary>
        static Color BoneSegment(Vector2 p)
        {
            // p.x −1..1, p.y −0.3..0.3
            Func<Vector2, float> sdf = q =>
            {
                float shaft = SdCapsule(q, new Vector2(-0.78f, 0f), new Vector2(0.78f, 0f), 0.1f + 0.02f * Mathf.Abs(q.x));
                float k1 = (q - new Vector2(-0.82f, 0.03f)).magnitude - 0.16f;
                float k2 = (q - new Vector2(0.82f, -0.02f)).magnitude - 0.15f;
                return Mathf.Min(shaft, Mathf.Min(k1, k2));
            };
            Color c = Lit(sdf, p, BoneFill, 0.018f);
            if (c.a <= 0f) return c;
            if (Hash(Mathf.Floor(p.x * 50f), Mathf.Floor(p.y * 50f)) > 0.94f) c = Mul(c, 0.82f);    // поры
            c = Color.Lerp(c, Mul(BoneShade, 1.05f), 0.25f * SStep(0.5f, 0.95f, Mathf.Abs(p.x)));
            return c;
        }

        /// <summary>Большое ребро: изогнутая кость, сужается к концу.</summary>
        static Color RibArc(Vector2 p)
        {
            // p.x −1..1, p.y −0.5..0.5: от основания слева-внизу дугой вправо-вверх
            Func<Vector2, float> sdf = q =>
            {
                Vector2 c0 = new Vector2(0.95f, -0.75f);
                float rr = (q - c0).magnitude;
                float ang = Mathf.Atan2(q.y - c0.y, q.x - c0.x);
                float t = Mathf.InverseLerp(Mathf.PI * 0.95f, Mathf.PI * 0.52f, ang);
                float w = Mathf.Lerp(0.08f, 0.035f, Sat(t));
                float band = Mathf.Abs(rr - 1.12f) - w;
                return Mathf.Max(band, Mathf.Max(-t, t - 1f) * 0.6f);
            };
            Color c = Lit(sdf, p, BoneFill, 0.015f);
            if (c.a <= 0f) return c;
            if (Fbm(p.x * 8f, p.y * 8f, 2) > 0.62f) c = Mul(c, 0.88f);
            return c;
        }

        /// <summary>
        /// Позвонок спереди: округлое тело, над ним дуга с тёмным каналом, остистый
        /// отросток клином вверх, поперечные — крыльями в стороны и чуть вниз.
        /// </summary>
        static Color Vertebra(Vector2 p, float seed)
        {
            float lean = 0.08f * (Hash(1f, seed) - 0.5f);
            Func<Vector2, float> sdf = q =>
            {
                float body = ((new Vector2(q.x / 0.38f, (q.y + 0.4f) / 0.28f)).magnitude - 1f) * 0.28f;
                float arch = Mathf.Abs((q - new Vector2(0f, -0.04f)).magnitude - 0.2f) - 0.07f;
                float t = Sat((q.y - 0.08f) / 0.82f);
                float spine = Mathf.Max(Mathf.Abs(q.x - lean * q.y) - Mathf.Lerp(0.13f, 0.025f, t),
                                        Mathf.Max(0.08f - q.y, q.y - 0.9f));
                float ax = Mathf.Abs(q.x);
                float wingY = -0.14f - 0.16f * ax - 0.06f * Hash(2f, seed) * ax;
                float wing = Mathf.Max(Mathf.Abs(q.y - wingY) - Mathf.Lerp(0.09f, 0.035f, Sat(ax / 0.86f)), ax - 0.86f);
                return Mathf.Min(Mathf.Min(body, arch), Mathf.Min(spine, wing));
            };
            if ((p - new Vector2(0f, -0.04f)).magnitude < 0.13f) return WithA(Mul(BoneShade, 0.4f), 1f);   // канал
            Color c = Lit(sdf, p, BoneFill, 0.02f);
            if (c.a <= 0f) return c;
            if (Fbm(p.x * 7f + seed, p.y * 7f, 2) > 0.62f) c = Mul(c, 0.88f);
            return c;
        }

        /// <summary>
        /// Череп кита сверху: широкий свод слева, длинное клиновидное рыло вправо,
        /// глазницы по бокам у основания рыла, шов по оси (щели ноздрей в игре
        /// читались знаком «=» [ТЕСТ] — убраны).
        /// </summary>
        static Color Skull(Vector2 p)
        {
            // p.x −1..1, p.y −0.625..0.625
            Func<Vector2, float> sdf = q =>
            {
                float cran = ((new Vector2((q.x + 0.52f) / 0.44f, q.y / 0.54f)).magnitude - 1f) * 0.36f;
                float k = Sat((q.x + 0.25f) / 1.22f);
                float halfW = Mathf.Lerp(0.36f, 0.05f, Mathf.Pow(k, 0.9f));
                float rostrum = Mathf.Max(Mathf.Abs(q.y) - halfW, Mathf.Max(q.x - 0.97f, -0.3f - q.x));
                return Mathf.Min(cran, rostrum);
            };
            Color c = Lit(sdf, p, BoneFill, 0.015f);
            if (c.a <= 0f) return c;
            Color hole = WithA(Mul(BoneShade, 0.35f), 1f);
            if ((new Vector2((p.x + 0.3f) / 0.1f, (Mathf.Abs(p.y) - 0.37f) / 0.075f)).magnitude < 1f) return hole;   // глазницы
            if (Mathf.Abs(p.y) < 0.012f && p.x > -0.2f) c = Mul(c, 0.82f);                                           // шов
            if (Fbm(p.x * 6f, p.y * 6f, 2) > 0.6f) c = Mul(c, 0.88f);
            return c;
        }

        /// <summary>Китовый ус: тёмная полоса дёсен и бахрома пластин вниз.</summary>
        static Color Baleen(Vector2 p)
        {
            // p.x −1..1, p.y −0.375..0.375
            if (p.y > 0.22f && p.y < 0.34f && Mathf.Abs(p.x) < 0.98f) return WithA(Mul(BoneShade, 0.75f), 1f);
            float col = Mathf.Floor((p.x + 1f) * 16f);
            float len = 0.32f + 0.28f * Hash(col, 2.9f) * (1f - 0.4f * Mathf.Abs(p.x));
            float fx = Mathf.Repeat((p.x + 1f) * 16f, 1f);
            if (p.y > 0.22f || p.y < 0.22f - len || fx < 0.18f) return Color.clear;
            float fringe = SStep(0.22f - len, 0.22f - len + 0.12f, p.y);
            Color c = Mul(BaleenFill, 0.75f + 0.35f * fringe);
            c.a = 0.5f + 0.5f * fringe;
            return c;
        }

        /// <summary>Мелкие кости на дне: три-четыре вразброс.</summary>
        static Color BoneScatter(Vector2 p)
        {
            // p.x −1..1, p.y −0.375..0.375
            Func<Vector2, float> sdf = q =>
            {
                float d = 9f;
                for (int i = 0; i < 4; i++)
                {
                    Vector2 c = new Vector2(-0.7f + i * 0.46f, -0.15f + 0.2f * Hash(i, 8.1f));
                    float a = (Hash(i + 3f, 8.1f) - 0.5f) * 1.6f;
                    Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (0.16f + 0.08f * Hash(i, 2.2f));
                    d = Mathf.Min(d, SdCapsule(q, c - dir, c + dir, 0.045f));
                    d = Mathf.Min(d, (q - (c + dir)).magnitude - 0.065f);
                }
                return d;
            };
            return Lit(sdf, p, BoneFill, 0.02f);
        }

        /// <summary>Голотурия: бугристая «колбаса» с бледными щупальцами на конце.</summary>
        static Color SeaCucumber(Vector2 p)
        {
            // p.x −1..1, p.y −0.43..0.43
            Func<Vector2, float> sdf = q =>
                SdCapsule(q, new Vector2(-0.7f, -0.05f), new Vector2(0.62f, 0.02f), 0.2f + 0.03f * Mathf.Sin(q.x * 18f));
            Color c = Lit(sdf, p, Palette.Hex("6E6A56"), 0.02f);
            if (c.a > 0f)
            {
                if (Hash(Mathf.Floor(p.x * 20f), Mathf.Floor(p.y * 20f)) > 0.86f) c = Mul(c, 0.78f);
                return c;
            }
            // щупальца справа
            for (int i = 0; i < 5; i++)
            {
                float a = -0.6f + i * 0.3f;
                Vector2 tip = new Vector2(0.82f + 0.12f * Mathf.Cos(a), 0.02f + 0.2f * Mathf.Sin(a));
                if (SegDist(p, new Vector2(0.78f, 0.02f), tip) < 0.025f) return WithA(Palette.Hex("A8A890"), 1f);
            }
            return Color.clear;
        }

        /// <summary>Хвостовой плавник: два широких лопастя с выемкой, бледный, как выцветший.</summary>
        static Color Fluke(Vector2 p)
        {
            // p.x −1..1, p.y −0.5..0.5; стебель снизу
            Func<Vector2, float> sdf = q =>
            {
                float stem = SdCapsule(q, new Vector2(0f, -0.48f), new Vector2(0f, -0.05f), 0.08f);
                float lobe = 9f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 c = new Vector2(s * 0.45f, 0.08f);
                    Vector2 d = new Vector2((q.x - c.x) / 0.52f, (q.y - c.y + 0.15f * (q.x * s)) / 0.2f);
                    lobe = Mathf.Min(lobe, (d.magnitude - 1f) * 0.2f);
                }
                float notch = 0.06f - (new Vector2(q.x / 0.6f, (q.y - 0.32f) / 0.2f)).magnitude * 0.06f;
                return Mathf.Max(Mathf.Min(stem, lobe), notch);
            };
            Color c = Lit(sdf, p, Mul(BoneFill, 0.85f), 0.015f);
            if (c.a <= 0f) return c;
            if (Mathf.Abs(Mathf.Repeat(p.x * 8f, 1f) - 0.5f) > 0.46f && p.y > -0.05f) c = Mul(c, 0.9f);   // лучи плавника
            return c;
        }

        /// <summary>Дальняя грудная клетка: хребет дугой и рёбра вниз — силуэт.</summary>
        static Color FarCage(Vector2 p)
        {
            // p.x −1..1, p.y −0.5..0.5
            float spineY = 0.3f - 0.12f * p.x * p.x;
            bool spine = Mathf.Abs(p.y - spineY) < 0.035f && Mathf.Abs(p.x) < 0.95f;
            bool rib = false;
            for (int i = 0; i < 9; i++)
            {
                float x0 = -0.82f + i * 0.2f;
                float t = (spineY - p.y) / 0.62f;
                if (t < 0f || t > 1f) continue;
                float x = x0 + 0.18f * Mathf.Sin(t * 2.2f) * (x0 < 0f ? -1f : 1f);
                if (Mathf.Abs(p.x - x) < 0.022f * (1f - 0.5f * t)) rib = true;
            }
            if (!spine && !rib) return Color.clear;
            Color c = Mul(SilhouetteFill, 0.85f + 0.2f * SStep(-0.4f, 0.3f, p.y));
            c.a = 1f;
            return c;
        }

        // ------------------------------------------------------------ сад

        /// <summary>Плавучий остров: мшистый купол сверху, конус камня вниз, корни.</summary>
        static Color Island(Vector2 p, float seed, float rough)
        {
            // p.x −1..1, p.y −0.69..0.69 (у B и C — чуть ниже)
            Func<Vector2, float> sdf = q =>
            {
                float top = 0.3f - 0.22f * q.x * q.x + 0.06f * (Fbm(q.x * 4f + seed, 1.1f, 2) - 0.5f) * rough;
                float spread = 0.92f - 0.1f * Hash(2f, seed);
                float bot = 0.05f - 0.62f * Mathf.Pow(Sat(1f - Mathf.Abs(q.x) / spread), 1.2f)
                          - 0.08f * (Fbm(q.x * 5f, seed, 2) - 0.5f) * rough;
                float dTop = q.y - top, dBot = bot - q.y;
                return Mathf.Max(Mathf.Max(dTop, dBot), Mathf.Abs(q.x) - spread);
            };
            float d = sdf(p);
            if (d > 0f)
            {
                // корни под островом
                float root = Mathf.Abs(Mathf.Repeat((p.x + 1f) * 5f, 1f) - 0.5f);
                if (root < 0.03f && p.y < 0f && p.y > -0.68f && sdf(new Vector2(p.x, p.y + 0.14f)) < 0f)
                    return WithA(Mul(VineFill, 0.8f), 1f);
                return Color.clear;
            }
            Color c = Lit(sdf, p, IslandRock, 0.02f);
            c = Mul(c, 0.9f + 0.2f * Fbm(p.x * 6f + seed, p.y * 6f, 3));
            float topY = 0.3f - 0.22f * p.x * p.x;
            if (p.y > topY - 0.12f - 0.05f * Fbm(p.x * 9f, seed, 2)) c = Color.Lerp(c, IslandMoss, 0.8f);
            c.a = 1f;
            return c;
        }

        /// <summary>Свисающая лиана: волнистая плеть, листья по очереди.</summary>
        static Color Vine(Vector2 p, float seed)
        {
            // p.x −0.21..0.21, p.y −1..1; крепится сверху
            float x = 0.06f * Mathf.Sin(p.y * 5f + seed);
            float t = (1f - p.y) * 0.5f;          // 0 — верх
            if (p.y < -0.92f + 0.1f * Hash(1f, seed)) return Color.clear;
            if (Mathf.Abs(p.x - x) < 0.022f) return WithA(Mul(VineFill, 0.9f + 0.2f * (1f - t)), 1f);
            float k = Mathf.Repeat(p.y * 6f + seed, 1f);
            float side = Mathf.Repeat(Mathf.Floor(p.y * 6f + seed), 2f) < 1f ? -1f : 1f;
            Vector2 leaf = new Vector2(x + side * 0.09f, 0f);
            Vector2 d = new Vector2((p.x - leaf.x) / 0.1f, (k - 0.5f) / 0.32f);
            if (d.magnitude < 1f) return WithA(Mul(LeafFill, 0.8f + 0.25f * (1f - d.magnitude)), 1f);
            return Color.clear;
        }

        /// <summary>Мох: бугорки-кочки.</summary>
        static Color Moss(Vector2 p)
        {
            // p.x −1..1, p.y −0.5..0.5
            float top = -0.15f + 0.35f * Mathf.Abs(Mathf.Sin(p.x * 4.5f)) * (1f - p.x * p.x) + 0.08f * Fbm(p.x * 9f, 3.3f, 2);
            if (p.y > top || p.y < -0.45f) return Color.clear;
            Color c = Mul(IslandMoss, 0.75f + 0.35f * SStep(-0.45f, top, p.y));
            if (Hash(Mathf.Floor(p.x * 40f), Mathf.Floor(p.y * 40f)) > 0.9f) c = Mul(c, 1.15f);
            c.a = 1f;
            return c;
        }

        /// <summary>Камушки в толще: три-пять мелких, без опоры.</summary>
        static Color FloatingPebbles(Vector2 p)
        {
            // p.x −1..1, p.y −0.43..0.43
            Func<Vector2, float> sdf = q =>
            {
                float d = 9f;
                for (int i = 0; i < 5; i++)
                {
                    Vector2 c = new Vector2(-0.78f + i * 0.39f, 0.18f * (Hash(i, 4.4f) - 0.5f) * 2f);
                    float r = 0.1f + 0.08f * Hash(i + 1f, 4.4f);
                    d = Mathf.Min(d, (new Vector2(q.x - c.x, (q.y - c.y) * 1.3f)).magnitude - r);
                }
                return d;
            };
            Color c = Lit(sdf, p, Palette.Hex("6A7268"), 0.02f);
            if (c.a > 0f && p.y > 0.05f && Fbm(p.x * 10f, p.y * 10f, 2) > 0.5f) c = Color.Lerp(c, IslandMoss, 0.5f);
            return c;
        }

        /// <summary>Висячий мост: две провисшие лианы, перекладины, листья.</summary>
        static Color VineBridge(Vector2 p)
        {
            // p.x −1..1, p.y −0.31..0.31
            float sag = 0.2f - 0.34f * (1f - p.x * p.x);
            bool rope = Mathf.Abs(p.y - sag) < 0.022f || Mathf.Abs(p.y - sag - 0.1f) < 0.018f;
            bool slat = Mathf.Abs(Mathf.Repeat(p.x * 7f, 1f) - 0.5f) < 0.1f && p.y > sag - 0.03f && p.y < sag + 0.1f
                        && Mathf.Abs(p.x) < 0.92f;
            if (slat) return WithA(Mul(WoodFill, 0.85f), 1f);
            if (rope) return WithA(VineFill, 1f);
            float k = Mathf.Repeat(p.x * 11f, 1f);
            if (p.y < sag && p.y > sag - 0.14f * Hash(Mathf.Floor(p.x * 11f), 7.1f) && Mathf.Abs(k - 0.5f) < 0.12f)
                return WithA(LeafFill, 1f);
            return Color.clear;
        }

        /// <summary>Арка-остров: природный свод с мхом поверху и лианами из проёма.</summary>
        static Color IslandArch(Vector2 p)
        {
            Color c = NaturalArch(new Vector2(p.x, p.y), IslandRock, 0f);
            if (c.a <= 0f)
            {
                float hole = (p.x * p.x) / 0.30f + ((p.y + 0.67f) * (p.y + 0.67f)) / 0.62f;
                if (hole < 1f && Mathf.Abs(Mathf.Repeat((p.x + 1f) * 6f, 1f) - 0.5f) < 0.06f
                    && hole > 0.7f - 0.4f * Hash(Mathf.Floor((p.x + 1f) * 6f), 1.9f))
                    return WithA(VineFill, 1f);
                return c;
            }
            float outerTop = 0.55f - 0.25f * p.x * p.x;
            if (p.y > outerTop - 0.1f) c = Color.Lerp(c, IslandMoss, 0.75f);
            c.a = 1f;
            return c;
        }

        /// <summary>Дальние острова: три силуэта разного размера.</summary>
        static Color FarIslands(Vector2 p)
        {
            // p.x −1..1, p.y −0.5..0.5
            for (int i = 0; i < 3; i++)
            {
                Vector2 c = new Vector2(-0.62f + i * 0.6f, 0.12f - 0.22f * Hash(i, 3.7f));
                float s = 0.2f + 0.14f * Hash(i + 2f, 3.7f);
                Vector2 q = (p - c) / s;
                float top = 0.3f - 0.22f * q.x * q.x;
                float bot = 0.05f - 0.62f * Mathf.Pow(Sat(1f - Mathf.Abs(q.x) / 0.9f), 1.2f);
                if (q.y < top && q.y > bot && Mathf.Abs(q.x) < 0.9f)
                    return WithA(Mul(SilhouetteFill, 0.85f + 0.2f * SStep(bot, top, q.y)), 1f);
            }
            return Color.clear;
        }

        /// <summary>Мягкое пятно света: тон даёт цвет рисунка.</summary>
        static Color SoftGlow(Vector2 p)
        {
            float a = Sat(1f - p.magnitude);
            return new Color(0.9f, 0.9f, 0.9f, a * a);
        }
    }
}
