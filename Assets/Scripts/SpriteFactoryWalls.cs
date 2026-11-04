using UnityEngine;

namespace BubbleRise
{
    /// <summary>Фактура стен локации. Новые — в конец: номер вида — номер материала.</summary>
    public enum WallStyle { Rock, Masonry, Sand, Ice, Wood, Bone }

    public static partial class SpriteFactory
    {
        // ------------------------------------------------------------ стены локаций
        //
        // Фактура тянется по стене повтором (128×256, без шва по обеим осям),
        // оттенок даёт цвет вершин меша — одна фактура на несколько тем.
        // Рисуется один раз, при первой локации этого вида или в Prewarm.

        const int WallW = 128, WallH = 256;
        static Material[] _wallMats;

        /// <summary>Материал стены локации: фактура вида × цвет вершин.</summary>
        public static Material ZoneWallMaterial(WallStyle style)
        {
            if (_wallMats == null) _wallMats = new Material[System.Enum.GetValues(typeof(WallStyle)).Length];
            int i = (int)style;
            if (_wallMats[i] != null) return _wallMats[i];

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var tex = LoadBaked("wall_" + style, TextureWrapMode.Repeat, WallW, WallH)
                      ?? MakeTexture("wall_" + style, WallW, WallH, PaintWall(style), TextureWrapMode.Repeat);
            _wallMats[i] = new Material(Unlit) { name = "BubbleRise_Wall_" + style, mainTexture = tex };
            WorldAtlasMs += sw.ElapsedMilliseconds;
            return _wallMats[i];
        }

        /// <summary>Пиксели фактуры стены 128×256, бесшовные по обеим осям.</summary>
        static Color32[] PaintWall(WallStyle style)
        {
            var px = new Color32[WallW * WallH];
            for (int y = 0; y < WallH; y++)
            {
                for (int x = 0; x < WallW; x++)
                {
                    Vector2 uv = new Vector2((x + 0.5f) / WallW, (y + 0.5f) / WallH);
                    Color c;
                    switch (style)
                    {
                        case WallStyle.Masonry: c = MasonryTex(uv); break;
                        case WallStyle.Sand: c = SandTex(uv); break;
                        case WallStyle.Ice: c = IceTex(uv); break;
                        case WallStyle.Wood: c = WoodTex(uv); break;
                        case WallStyle.Bone: c = BoneTex(uv); break;
                        default: c = RockTex(uv); break;
                    }
                    c.a = 1f;
                    px[y * WallW + x] = c;
                }
            }
            return px;
        }

        /// <summary>Шум значений на решётке с периодом px×py — без шва при повторе.</summary>
        static float PNoise(float x, float y, int px, int py)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % px) + px) % px, xb = (xa + 1) % px;
            int ya = ((y0 % py) + py) % py, yb = (ya + 1) % py;
            float a = Hash(xa, ya), b = Hash(xb, ya), c = Hash(xa, yb), d = Hash(xb, yb);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Фрактальный шум по фактуре 1:2 (u, v ∈ [0, 1)), клетки квадратные.</summary>
        static float PFbm(Vector2 uv, int cells, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * PNoise(uv.x * cells, uv.y * cells * 2, cells, cells * 2);
                norm += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return sum / norm;
        }

        /// <summary>Расстояние до границы ячеек Вороного на торе — трещины и стыки.</summary>
        static float PCrack(Vector2 uv, int cx, int cy, float salt)
        {
            Vector2 p = new Vector2(uv.x * cx, uv.y * cy);
            int gx = Mathf.FloorToInt(p.x), gy = Mathf.FloorToInt(p.y);
            float d1 = 9f, d2 = 9f;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = gx + ox, y = gy + oy;
                    int wx = ((x % cx) + cx) % cx, wy = ((y % cy) + cy) % cy;
                    Vector2 pt = new Vector2(x + Hash(wx + salt, wy), y + Hash(wx, wy + salt + 3f));
                    float d = (p - pt).sqrMagnitude;
                    if (d < d1) { d2 = d1; d1 = d; }
                    else if (d < d2) d2 = d;
                }
            }
            return Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
        }

        static Color Grey(float v, float warm = 0f) => new Color(v * (1f + warm), v, v * (1f - warm));

        static Color RockTex(Vector2 uv)
        {
            // слоистая порода: пласты с изгибом, трещины, мелкие поры
            float n = PFbm(uv, 4, 4);
            float warp = PFbm(uv + new Vector2(0.37f, 0.11f), 2, 2);
            float strata = Mathf.Sin((uv.y * 7f + warp * 0.9f) * Mathf.PI * 2f);
            float band = SStep(0.7f, 0.95f, strata);
            float crack = 1f - SStep(0.02f, 0.09f, PCrack(uv, 4, 8, 1.7f));
            float pore = PFbm(uv + new Vector2(0.5f, 0.5f), 16, 1) > 0.78f ? 1f : 0f;
            float v = 0.8f + 0.24f * (n - 0.5f) * 2f - 0.1f * band - 0.22f * crack - 0.12f * pore;
            return Grey(Mathf.Clamp(v, 0.45f, 1f), 0.02f);
        }

        static Color MasonryTex(Vector2 uv)
        {
            // кладка: 4 блока в ряд, 8 рядов, ряды вразбежку; фаски, сколы, пятна водорослей
            float rowF = uv.y * 8f;
            int row = Mathf.FloorToInt(rowF);
            float shift = (row & 1) == 0 ? 0f : 0.125f;
            float colF = Mathf.Repeat(uv.x + shift, 1f) * 4f;
            int col = Mathf.FloorToInt(colF);
            float fx = colF - col, fy = rowF - row;
            float mortarX = Mathf.Min(fx, 1f - fx), mortarY = Mathf.Min(fy, 1f - fy);
            float n = PFbm(uv, 8, 3);
            bool chip = Hash(col + 11f, row) < 0.3f && mortarX < 0.16f && mortarY < 0.2f && n > 0.45f;
            if (mortarX < 0.045f || mortarY < 0.06f || chip) return Grey(0.5f + 0.08f * n);
            float face = 0.78f + 0.12f * (Hash(col, row + 5f) - 0.5f) * 2f + 0.12f * (n - 0.5f);
            face += 0.06f * SStep(0.75f, 0.94f, fy) - 0.07f * SStep(0.25f, 0.06f, fy);   // свет сверху, тень снизу
            float moss = PFbm(uv + new Vector2(0.21f, 0.63f), 4, 3);
            Color c = Grey(Mathf.Clamp(face, 0.5f, 1f), 0.03f);
            if (moss > 0.58f) c = Color.Lerp(c, new Color(c.r * 0.82f, c.g * 0.95f, c.b * 0.8f), SStep(0.58f, 0.7f, moss));
            return c;
        }

        static Color SandTex(Vector2 uv)
        {
            // плотный песок: рябь, зерно, редкая галька
            float warp = PFbm(uv, 2, 2);
            float ripple = Mathf.Sin((uv.y * 12f + warp * 1.2f) * Mathf.PI * 2f);
            float grain = PNoise(uv.x * 64f, uv.y * 128f, 64, 128);
            float n = PFbm(uv, 4, 3);
            float v = 0.84f + 0.05f * ripple + 0.08f * (grain - 0.5f) + 0.1f * (n - 0.5f);
            float pebble = PCrack(uv, 8, 16, 4.1f);
            if (Hash(Mathf.Floor(uv.x * 8f), Mathf.Floor(uv.y * 16f)) < 0.18f && pebble > 0.32f) v -= 0.12f;
            return Grey(Mathf.Clamp(v, 0.55f, 1f), 0.03f);
        }

        static Color WoodTex(Vector2 uv)
        {
            // обшивка корпуса: длинные доски поперёк (8 по высоте фактуры — по полюнита),
            // стык один на доску, вразбежку; волокно вдоль, гниль пятнами. Короткие доски
            // в два ряда читались кирпичной кладкой [ТЕСТ, кадр nz_Wreck]
            float rowF = uv.y * 8f;
            int row = Mathf.FloorToInt(rowF);
            float fy = rowF - row;
            float joint = Mathf.Repeat(uv.x + Hash(row, 3.7f), 1f);
            float grain = PNoise(uv.x * 4f, uv.y * 128f, 4, 128);
            float n = PFbm(uv, 4, 3);
            float v = 0.78f + 0.1f * (Hash(row, 1.3f) - 0.5f) * 2f + 0.08f * (grain - 0.5f) + 0.1f * (n - 0.5f);
            v += 0.05f * SStep(0.7f, 0.95f, fy) - 0.08f * SStep(0.25f, 0.05f, fy);   // свет сверху, тень снизу
            if (fy < 0.05f || joint < 0.012f) v = 0.5f + 0.06f * n;                   // щели
            Color c = Grey(Mathf.Clamp(v, 0.45f, 1f), 0.06f);
            float rot = PFbm(uv + new Vector2(0.41f, 0.17f), 4, 3);
            if (rot > 0.6f) c = Color.Lerp(c, new Color(c.r * 0.8f, c.g * 0.92f, c.b * 0.78f), SStep(0.6f, 0.72f, rot));
            return c;
        }

        static Color BoneTex(Vector2 uv)
        {
            // кость: гладкая, волокна вдоль, мелкие поры — без «плитки» и сетки трещин
            float n = PFbm(uv, 3, 4);
            float fibre = PNoise(uv.x * 48f, uv.y * 6f, 48, 6);
            float pores = PNoise(uv.x * 32f, uv.y * 64f, 32, 64);
            float v = 0.82f + 0.14f * (n - 0.5f) * 2f + 0.06f * (fibre - 0.5f);
            if (pores > 0.72f) v -= 0.12f * SStep(0.72f, 0.85f, pores);
            return Grey(Mathf.Clamp(v, 0.55f, 0.97f), 0.04f);
        }

        static Color IceTex(Vector2 uv)
        {
            // лёд: глубина пятнами, светлые трещины, вмёрзшие пузырьки
            float n = PFbm(uv, 4, 4);
            float crack = 1f - SStep(0.015f, 0.06f, PCrack(uv, 3, 6, 2.9f));
            float fine = 1f - SStep(0.01f, 0.035f, PCrack(uv, 7, 14, 6.3f));
            float v = 0.78f + 0.2f * (n - 0.5f) * 2f + 0.1f * crack + 0.04f * fine;
            Vector2 cell = new Vector2(Mathf.Floor(uv.x * 16f), Mathf.Floor(uv.y * 32f));
            Vector2 f = new Vector2(uv.x * 16f - cell.x - 0.5f, uv.y * 32f - cell.y - 0.5f);
            if (Hash(cell.x + 2f, cell.y) < 0.12f && f.magnitude < 0.18f) v += 0.08f;
            v = Mathf.Clamp(v, 0.5f, 0.96f);
            return new Color(v * 0.94f, v * 0.98f, v);
        }
    }
}
