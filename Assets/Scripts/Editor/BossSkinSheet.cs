using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BubbleRise.EditorTools
{
    /// <summary>
    /// Листы двадцати мини-боссов.
    ///
    /// * <c>boss_skins_TAG.png</c> — крупно (256) и в размере игры (89 и 143 точки при
    ///   1080×2340) на воде бездны и «Ледяного разлома»;
    /// * <c>boss_anim_TAG.png</c> — по пять кадров живости каждого за один цикл.
    ///
    /// Сетка с изгибом растеризуется здесь на CPU той же функцией
    /// (<see cref="MiniBossMotion.Fill"/>), что и в игре и витрине.
    ///
    /// Tools → BubbleRise → 7. Лист боссовых скинов, или из командной строки:
    ///   Unity -batchmode … -executeMethod BubbleRise.EditorTools.BossSkinSheet.RunBatch
    ///         -sheetOut путь -sheetTag стало
    /// </summary>
    public static class BossSkinSheet
    {
        const int GameBubble = 89, GameGuardian = 143, Big = 256, Pad = 12, Frame = 120;

        [MenuItem("Tools/BubbleRise/7. Лист боссовых скинов")]
        public static void RunMenu() => Write(Path.Combine(Application.dataPath, "..", "BossSkinSheet"), "sheet");

        public static void RunBatch()
        {
            try
            {
                Write(Arg("-sheetOut") ?? Path.Combine(Application.dataPath, "..", "BossSkinSheet"), Arg("-sheetTag") ?? "sheet");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
            return null;
        }

        public static readonly Color WaterDeep = Palette.Hex("0A1024"), WaterIce = Palette.Hex("0B2535");

        static void Write(string dir, string tag)
        {
            Directory.CreateDirectory(dir);
            var bosses = (BossKind[])Enum.GetValues(typeof(BossKind));
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var art = new Color[bosses.Length * 2][];
            for (int i = 0; i < bosses.Length; i++)
            {
                art[i * 2] = SpriteFactory.MiniBossPixels(new MiniBossKey(bosses[i], false));
                art[i * 2 + 1] = SpriteFactory.MiniBossPixels(new MiniBossKey(bosses[i], true));
            }
            Debug.Log("[BossSkinSheet] 20 рисунков за " + watch.ElapsedMilliseconds + " мс");

            // сами рисунки с прозрачностью — для расчёта контраста по глубинам
            string raw = Path.Combine(dir, "raw");
            Directory.CreateDirectory(raw);
            for (int i = 0; i < bosses.Length; i++)
                for (int g = 0; g < 2; g++)
                    Save(Path.Combine(raw, (g == 1 ? "g_" : "b_") + bosses[i] + ".png"), art[i * 2 + g],
                         SpriteFactory.MiniSize, SpriteFactory.MiniSize);

            // лист 1: крупно и в размере игры
            int w = Pad + (Big + Pad) * 2 + (GameGuardian + Pad) * 4;
            int rowH = Big + Pad;
            int h = Pad + rowH * bosses.Length;
            var sheet = Fill(w, h, Palette.Hex("05070C"));
            for (int r = 0; r < bosses.Length; r++)
            {
                int y0 = h - (r + 1) * rowH;
                var kb = new MiniBossKey(bosses[r], false);
                var kg = new MiniBossKey(bosses[r], true);
                int x = Pad;
                Paste(sheet, w, h, Render(art[r * 2], kb, 0f, Big, WaterDeep), Big, x, y0); x += Big + Pad;
                Paste(sheet, w, h, Render(art[r * 2 + 1], kg, 0f, Big, WaterDeep), Big, x, y0); x += Big + Pad;
                Paste(sheet, w, h, Render(art[r * 2], kb, 0f, GameBubble, WaterDeep), GameBubble, x, y0 + Big - GameBubble); x += GameGuardian + Pad;
                Paste(sheet, w, h, Render(art[r * 2 + 1], kg, 0f, GameGuardian, WaterDeep), GameGuardian, x, y0 + Big - GameGuardian); x += GameGuardian + Pad;
                Paste(sheet, w, h, Render(art[r * 2], kb, 0f, GameBubble, WaterIce), GameBubble, x, y0 + Big - GameBubble); x += GameGuardian + Pad;
                Paste(sheet, w, h, Render(art[r * 2 + 1], kg, 0f, GameGuardian, WaterIce), GameGuardian, x, y0 + Big - GameGuardian);
            }
            Save(Path.Combine(dir, "boss_skins_" + tag + ".png"), sheet, w, h);

            // лист 2: пять кадров живости за 2.4 с у пузырька и стража
            const int frames = 5;
            int aw = Pad + (Frame + Pad) * frames * 2 + Pad;
            int ah = Pad + (Frame + Pad) * bosses.Length;
            var anim = Fill(aw, ah, Palette.Hex("05070C"));
            for (int r = 0; r < bosses.Length; r++)
            {
                int y0 = ah - (r + 1) * (Frame + Pad);
                for (int half = 0; half < 2; half++)
                    for (int f = 0; f < frames; f++)
                    {
                        var key = new MiniBossKey(bosses[r], half == 1);
                        float t = f * 0.6f;
                        int x0 = Pad + half * (frames * (Frame + Pad) + Pad) + f * (Frame + Pad);
                        Paste(anim, aw, ah, Render(art[r * 2 + half], key, t, Frame, WaterDeep), Frame, x0, y0);
                    }
            }
            Save(Path.Combine(dir, "boss_anim_" + tag + ".png"), anim, aw, ah);
            Debug.Log("[BossSkinSheet] " + dir + " — " + bosses.Length + " боссов");
        }

        static Color[] Fill(int w, int h, Color c)
        {
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            return px;
        }

        static void Save(string path, Color[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>
        /// Мини-босс в момент <paramref name="t"/> на квадрате воды size×size: сетка
        /// с изгибом, билинейная выборка без mip — как в игре.
        /// </summary>
        public static Color[] Render(Color[] tex, MiniBossKey key, float t, int size, Color water)
        {
            var px = Fill(size, size, water);
            var pos = new Vector2[MiniBossMotion.VertexCount];
            MiniBossMotion.Fill(key, t, 1f, pos);
            var uv = MiniBossMotion.Uvs;
            var tri = MiniBossMotion.Triangles;
            int n = SpriteFactory.MiniSize;
            for (int k = 0; k < tri.Length; k += 3)
            {
                Vector2 a = (pos[tri[k]] + Vector2.one * 0.5f) * size, b = (pos[tri[k + 1]] + Vector2.one * 0.5f) * size,
                        c = (pos[tri[k + 2]] + Vector2.one * 0.5f) * size;
                Vector2 ua = uv[tri[k]], ub = uv[tri[k + 1]], uc = uv[tri[k + 2]];
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                int y1 = Mathf.Min(size - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));
                float den = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(den) < 1e-9f) continue;
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float l1 = ((b.y - c.y) * (p.x - c.x) + (c.x - b.x) * (p.y - c.y)) / den;
                        float l2 = ((c.y - a.y) * (p.x - c.x) + (a.x - c.x) * (p.y - c.y)) / den;
                        float l3 = 1f - l1 - l2;
                        if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                        Vector2 u = ua * l1 + ub * l2 + uc * l3;
                        Color s = Bilinear(tex, n, u.x * n - 0.5f, u.y * n - 0.5f);
                        int i = y * size + x;
                        px[i] = Color.Lerp(px[i], new Color(s.r, s.g, s.b, 1f), s.a);
                    }
            }
            return px;
        }

        static Color Bilinear(Color[] tex, int n, float x, float y)
        {
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, n - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, n - 1);
            int x1 = Mathf.Min(n - 1, x0 + 1), y1 = Mathf.Min(n - 1, y0 + 1);
            float fx = Mathf.Clamp01(x - x0), fy = Mathf.Clamp01(y - y0);
            // как GPU: смешиваем с весом альфы, иначе прозрачный чёрный темнит край
            Color a = tex[y0 * n + x0], b = tex[y0 * n + x1], c = tex[y1 * n + x0], d = tex[y1 * n + x1];
            return Color.Lerp(Color.Lerp(a, b, fx), Color.Lerp(c, d, fx), fy);
        }

        static void Paste(Color[] sheet, int w, int h, Color[] px, int size, int x0, int y0)
        {
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int sx = x0 + x, sy = y0 + y;
                    if (sx < 0 || sy < 0 || sx >= w || sy >= h) continue;
                    sheet[sy * w + sx] = px[y * size + x];
                }
        }
    }
}
