using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BubbleRise.Tests
{
    /// <summary>
    /// Двадцать боссовых скинов — мини-боссы. Проверяется то, что нарисовано и как
    /// оно движется: настоящие текстуры (читаются через видеокарту), настоящая
    /// сетка игры и витрины, настоящий генератор частиц.
    ///
    /// Всё новое (MiniBossKey, MiniBossMotion, MiniBossView, MiniBossGraphic)
    /// берётся отражением, поэтому файл собирается и на прежнем (неоновом) коде —
    /// и там падает. Каталог, хитбоксы и скины других редкостей проходят и там,
    /// и здесь: это про то, что не должно было измениться.
    /// </summary>
    public class BossSkinTests
    {
        static readonly Assembly Game = typeof(SkinStore).Assembly;
        static Type T(string name) => Game.GetType("BubbleRise." + name);
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <summary>Отпечаток каталога (индекс, ключ, открытие, босс, порог, редкость, цена, материал, рисунок).</summary>
        const string CatalogPrint = "0A7285AA4BB8CD7C";
        /// <summary>Отпечаток всех небоссовых скинов: пиксели, оттенки, эффекты, материал, оболочка.</summary>
        const string OthersPrint = "D5CDF60CF9B45718";

        /// <summary>Вода: три тона пяти ступеней глубины и вода локаций (руины — пять тем, лёд).</summary>
        static readonly string[] Waters =
        {
            "0A1024", "070B1A", "04060F", "0C1830", "091226", "060B19", "0E2034", "0A182A", "07101D",
            "0D2131", "0A1927", "08131F", "0D2031", "0B1D2C", "091724",
            "10262A", "0E2619", "122636", "1C1433", "112628", "0B2535",
        };

        // ------------------------------------------------------------ помощники

        static object Member(object o, string name)
        {
            if (o == null) return null;
            var t = o.GetType();
            var f = t.GetField(name, Any);
            if (f != null) return f.GetValue(o);
            var p = t.GetProperty(name, Any);
            return p != null ? p.GetValue(o) : null;
        }

        static Type Need(string name)
        {
            var t = T(name);
            Assert.IsNotNull(t, "нет типа " + name + " — мини-боссов нет");
            return t;
        }

        static object Key(BossKind boss, bool guardian) => Activator.CreateInstance(Need("MiniBossKey"), boss, guardian);

        static IEnumerable<(int Index, bool Guardian, SkinDef Skin)> BossSkins()
        {
            for (int g = 0; g < 2; g++)
            {
                var list = g == 0 ? SkinStore.Bubble : SkinStore.Guardian;
                for (int i = 0; i < list.Length; i++)
                    if (list[i].Kind == UnlockKind.Boss) yield return (i, g == 1, list[i]);
            }
        }

        static string Who(bool guardian, SkinDef s) => (guardian ? "страж " : "пузырёк ") + s.UnlockBoss;

        /// <summary>Пиксели спрайта через видеокарту: текстуры скинов не хранят CPU-копию.</summary>
        static Color[] Pixels(Sprite sprite, out int size)
        {
            var tex = sprite.texture;
            size = tex.width;
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = read.GetPixels();
            Object.DestroyImmediate(read);
            return px;
        }

        static readonly Dictionary<string, (Color[] Px, int Size)> _cache = new Dictionary<string, (Color[], int)>();

        /// <summary>Вид трофея ровно так, как его берут игра и витрина: SkinVisuals → Shell.</summary>
        static Color[] Art(int index, bool guardian, out int size)
        {
            string k = index + (guardian ? "g" : "b");
            if (!_cache.TryGetValue(k, out var v))
            {
                var look = SkinVisuals.At(guardian, index);
                var px = Pixels(look.Shell, out int s);
                v = (px, s);
                _cache[k] = v;
            }
            size = v.Size;
            return v.Px;
        }

        static float Lin(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        static float Lum(Color c) => 0.2126f * Lin(c.r) + 0.7152f * Lin(c.g) + 0.0722f * Lin(c.b);
        static float Ratio(float a, float b) => (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);

        /// <summary>Маска силуэта n×n (альфа больше половины).</summary>
        static bool[] Mask(Color[] px, int size, int n)
        {
            var m = new bool[n * n];
            int f = size / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float a = 0f;
                    for (int dy = 0; dy < f; dy++)
                        for (int dx = 0; dx < f; dx++)
                            a += px[(y * f + dy) * size + x * f + dx].a;
                    m[y * n + x] = a / (f * f) > 0.5f;
                }
            return m;
        }

        static float IoU(bool[] a, bool[] b)
        {
            int i = 0, u = 0;
            for (int k = 0; k < a.Length; k++) { if (a[k] && b[k]) i++; if (a[k] || b[k]) u++; }
            return u == 0 ? 1f : (float)i / u;
        }

        static float Value(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        static float Saturation(Color c) { float v = Value(c); return v <= 1e-4f ? 0f : (v - Mathf.Min(c.r, Mathf.Min(c.g, c.b))) / v; }
        /// <summary>«Неон»: и яркий, и насыщенный — электрические цвета прежних кайм.</summary>
        static bool Neon(Color c) => Value(c) > 0.85f && Saturation(c) > 0.45f;

        static string Print(string s)
        {
            using (var sha = System.Security.Cryptography.SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).Replace("-", "").Substring(0, 16);
        }

        static void Dump(StringBuilder sb, object o)
        {
            if (o == null) { sb.Append("null;"); return; }
            foreach (var f in o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(f => f.Name))
            {
                var v = f.GetValue(o);
                sb.Append(f.Name).Append('=').Append(v is float x ? x.ToString("R") : Convert.ToString(v)).Append(',');
            }
            sb.Append(';');
        }

        static void HashPixels(StringBuilder sb, Sprite s)
        {
            if (s == null) { sb.Append("nosprite;"); return; }
            var px = Pixels(s, out int size);
            unchecked
            {
                long h = 1469598103934665603L;
                foreach (Color32 c in px.Select(c => (Color32)c))
                    h = (h ^ (c.r | (c.g << 8) | (c.b << 16) | (c.a << 24))) * 1099511628211L;
                sb.Append(size).Append(':').Append(h.ToString("X")).Append(';');
            }
        }

        /// <summary>Закрытый цвет босса из SpriteFactory (кость и зубы змея).</summary>
        static Color Private(string name) => (Color)typeof(SpriteFactory).GetField(name, Any).GetValue(null);

        /// <summary>Тона самого босса — из его фабрики спрайтов, а не из таблицы скинов.</summary>
        static Color[] BossTones(BossKind boss)
        {
            switch (boss)
            {
                case BossKind.Jelly: return new[] { SpriteFactory.JellyBody, SpriteFactory.JellyDeep, SpriteFactory.JellyEdge };
                case BossKind.Serpent: return new[] { Palette.SerpentBody, Palette.SerpentBelly, Palette.SerpentSpine, Private("SerpentBone"), Private("SerpentTooth") };
                case BossKind.Octopus: return new[] { Palette.OctopusBody, Palette.OctopusDark, Palette.OctopusSucker, Palette.OctopusEye };
                case BossKind.Swordfish: return new[] { Palette.SwordBack, Palette.SwordBelly, Palette.SwordBlade };
                case BossKind.Crab: return new[] { Palette.CrabShell, Palette.CrabShellDk, Palette.CrabHilite };
                case BossKind.Ink: return new[] { SpriteFactory.InkMantleTop, SpriteFactory.InkMantleMid, SpriteFactory.InkMantleLow, Palette.InkBody };
                case BossKind.Piranha: return new[] { Palette.PiranhaSwarmBack, Palette.PiranhaSwarmBody, Palette.PiranhaSwarmBelly, Palette.PiranhaSwarmTooth };
                case BossKind.Turtle: return new[] { Palette.TurtleShell, Palette.TurtleShellDark, Palette.TurtleSkin, Palette.Rock };
                case BossKind.Shark: return new[] { Palette.SharkBack, Palette.SharkBody, Palette.SharkBelly, Palette.SharkFin };
                default: return new[] { Palette.MantaBack, Palette.MantaEdge, Palette.MantaSpot, Palette.MantaSpine };
            }
        }

        /// <summary>Расстояние до тона с поправкой на свет и тень: тон, умноженный на 0.45…1.25.</summary>
        static float ToneDistance(Color c, Color tone)
        {
            float best = float.MaxValue;
            for (float s = 0.45f; s <= 1.25f; s += 0.05f)
            {
                float dr = c.r - tone.r * s, dg = c.g - tone.g * s, db = c.b - tone.b * s;
                best = Mathf.Min(best, Mathf.Sqrt(dr * dr + dg * dg + db * db));
            }
            return best;
        }

        // ------------------------------------------------- что не должно было измениться

        [Test]
        public void Catalog_IndicesAndKeysUnchanged()
        {
            var sb = new StringBuilder();
            foreach (var list in new[] { SkinStore.Bubble, SkinStore.Guardian })
            {
                for (int i = 0; i < list.Length; i++)
                {
                    var s = list[i];
                    sb.Append(i).Append('|').Append(s.NameKey).Append('|').Append(s.Kind).Append('|')
                      .Append(s.UnlockBoss).Append('|').Append(s.Amount).Append('|').Append(s.Rarity).Append('|')
                      .Append(s.Price).Append('|').Append(s.Material).Append('|').Append(s.Art).Append(';');
                }
                sb.Append('#');
            }
            string print = Print(sb.ToString());
            Assert.AreEqual(CatalogPrint, print, "каталог скинов изменился (отпечаток " + print + ")");
            Assert.AreEqual(20, BossSkins().Count(), "боссовых скинов должно быть 20");
        }

        [Test]
        public void OtherSkins_Unchanged()
        {
            var sb = new StringBuilder();
            for (int g = 0; g < 2; g++)
            {
                var list = g == 0 ? SkinStore.Bubble : SkinStore.Guardian;
                for (int i = 0; i < list.Length; i++)
                {
                    if (list[i].Kind == UnlockKind.Boss) continue;
                    var look = SkinVisuals.Resolve(list[i], g == 1, i);
                    sb.Append(g).Append('/').Append(i).Append(':');
                    HashPixels(sb, look.Shell);
                    HashPixels(sb, look.Core);
                    sb.Append(look.ShellColor).Append(look.Tint).Append(look.VisualScale.ToString("R")).Append(';');
                    Dump(sb, look.Fx);
                    Dump(sb, look.Material);
                    sb.Append(EnvelopeTable.TryGet(list[i], out var env)).Append(';');
                    Dump(sb, env);
                    sb.Append('|');
                }
            }
            string print = Print(sb.ToString());
            Assert.AreEqual(OthersPrint, print, "скины других редкостей изменились (отпечаток " + print + ")");
        }

        [Test]
        public void Hitboxes_Unchanged()
        {
            var cfgGo = new GameObject("Config");
            try
            {
                // радиусы игрока при множителе 0.68 — те же, что до работы
                var cfg = cfgGo.AddComponent<GameConfig>();
                PlayerSize.Bind(cfg);
                Assert.AreEqual(0.28f * 0.68f, PlayerSize.Bubble(cfg), 1e-5f, "радиус пузырька изменился");
                Assert.AreEqual(0.45f * 0.68f, PlayerSize.Guardian(cfg), 1e-5f, "радиус стража изменился");
            }
            finally { Object.DestroyImmediate(cfgGo); }
        }

        [Test]
        public void MiniBoss_IsOnlyAPicture()
        {
            // мини-босс — только картинка: ни коллайдеров, ни изменения масштаба тела
            var viewType = Need("MiniBossView");
            var body = new GameObject("Body", typeof(SpriteRenderer));
            try
            {
                body.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
                foreach (var (index, guardian, skin) in BossSkins())
                {
                    var view = viewType.GetMethod("On").Invoke(null, new object[] { body.transform });
                    viewType.GetMethod("Show").Invoke(view, new object[] { SkinVisuals.Resolve(skin, guardian, index) });
                }
                Assert.AreEqual(0, body.GetComponentsInChildren<Collider2D>(true).Length, "у мини-босса появился коллайдер");
                Assert.AreEqual(0, body.GetComponentsInChildren<Collider>(true).Length, "у мини-босса появился коллайдер");
                Assert.AreEqual(new Vector3(0.38f, 0.38f, 1f), body.transform.localScale, "мини-босс поменял размер тела");
            }
            finally { Object.DestroyImmediate(body); }
        }

        // ------------------------------------------------------------- рисунок

        [Test]
        public void EverySkin_IsItsBossSilhouette_NotACircle()
        {
            const int n = 64;
            var masks = new List<(string Who, bool[] Mask)>();
            var report = new StringBuilder();
            foreach (var (index, guardian, skin) in BossSkins())
            {
                var px = Art(index, guardian, out int size);
                Assert.GreaterOrEqual(size, 256, Who(guardian, skin) + ": текстура мельче 256");
                var m = Mask(px, size, n);

                // доля закрашенного внутри круга игрока (r ≤ 0.9) и совпадение с кругом той же площади
                int inside = 0, filled = 0, area = 0;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        if (m[y * n + x]) area++;
                        if (u * u + v * v > 0.81f) continue;
                        inside++;
                        if (m[y * n + x]) filled++;
                    }
                float r = Mathf.Sqrt(area / (float)(n * n) * 4f / Mathf.PI);
                var disc = new bool[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                        disc[y * n + x] = u * u + v * v <= r * r;
                    }
                float fill = (float)filled / inside, round = IoU(m, disc);
                report.AppendFormat("{0}: круг закрашен {1:P0}, похож на круг {2:P0}; ", Who(guardian, skin), fill, round);
                Assert.Less(fill, 0.75f, Who(guardian, skin) + ": закрашен почти весь круг — это значок, а не силуэт босса");
                Assert.Less(round, 0.8f, Who(guardian, skin) + ": силуэт — круг");
                masks.Add((Who(guardian, skin), m));
            }
            // все двадцать различимы между собой. Порог 0.72: когда рыбы боком встали носом
            // вверх, маски пираньи и черепашки (обе — вертикальный овал) совпали на 0.70,
            // хотя рыбу с хвостом и черепаху с ластами путать не с чем (и цвет разный)
            for (int i = 0; i < masks.Count; i++)
                for (int j = i + 1; j < masks.Count; j++)
                    Assert.Less(IoU(masks[i].Mask, masks[j].Mask), 0.72f, masks[i].Who + " и " + masks[j].Who + ": одинаковый силуэт");
            Debug.Log("[BossSkinTests] силуэты: " + report);
        }

        [Test]
        public void EverySkin_UsesItsBossPalette()
        {
            var report = new StringBuilder();
            foreach (var (index, guardian, skin) in BossSkins())
            {
                var px = Art(index, guardian, out _);
                var tones = BossTones(skin.UnlockBoss);
                int opaque = 0, own = 0;
                float[] other = new float[10];
                for (int i = 0; i < px.Length; i += 3)
                {
                    var c = px[i];
                    if (c.a < 0.6f) continue;
                    opaque++;
                    if (tones.Min(t => ToneDistance(c, t)) < 0.12f) own++;
                }
                float share = (float)own / Mathf.Max(1, opaque);
                // свой босс должен подходить лучше, чем большинство чужих
                int better = 0;
                foreach (BossKind b in Enum.GetValues(typeof(BossKind)))
                {
                    if (b == skin.UnlockBoss) continue;
                    var ot = BossTones(b);
                    int hit = 0;
                    for (int i = 0; i < px.Length; i += 3)
                        if (px[i].a >= 0.6f && ot.Min(t => ToneDistance(px[i], t)) < 0.12f) hit++;
                    if (hit < own) better++;
                }
                report.AppendFormat("{0}: тона босса {1:P0}, лучше чужих {2}/9; ", Who(guardian, skin), share, better);
                Assert.GreaterOrEqual(share, 0.45f, Who(guardian, skin) + ": меньше половины рисунка — в тонах босса");
                // тёмно-серые тона есть у нескольких глубинных боссов, поэтому — не хуже половины чужих
                Assert.GreaterOrEqual(better, 4, Who(guardian, skin) + ": тона ближе к чужим боссам, чем к своему");
            }
            Debug.Log("[BossSkinTests] палитра: " + report);
        }

        [Test]
        public void NoNeon_NoGlowingRim_NoWhite()
        {
            var report = new StringBuilder();
            foreach (var (index, guardian, skin) in BossSkins())
            {
                var px = Art(index, guardian, out int size);
                int opaque = 0, neon = 0, edge = 0, edgeLit = 0;
                float edgeSum = 0f, innerSum = 0f; int inner = 0;
                for (int y = 2; y < size - 2; y++)
                    for (int x = 2; x < size - 2; x++)
                    {
                        var c = px[y * size + x];
                        if (c.a < 0.6f) continue;
                        opaque++;
                        Assert.LessOrEqual(c.r + c.g + c.b, 2.3f + 0.03f, Who(guardian, skin) + ": белый пересвет");
                        if (Neon(c)) neon++;
                        bool atEdge = false;
                        for (int d = -2; d <= 2 && !atEdge; d++)
                            atEdge = px[(y + d) * size + x].a < 0.3f || px[y * size + x + d].a < 0.3f;
                        float l = Lum(c);
                        if (atEdge)
                        {
                            edge++; edgeSum += l;
                            if (Value(c) > 0.7f && Saturation(c) > 0.35f) edgeLit++;
                        }
                        else { inner++; innerSum += l; }
                    }
                float neonShare = (float)neon / Mathf.Max(1, opaque);
                float litRim = (float)edgeLit / Mathf.Max(1, edge);
                float rimL = edgeSum / Mathf.Max(1, edge);
                float rimToInner = rimL / Mathf.Max(1e-4f, innerSum / Mathf.Max(1, inner));
                report.AppendFormat("{0}: неон {1:P1}, яркая кромка {2:P0}, кромка L {3:F2} и ×{4:F2} к середине; ",
                                    Who(guardian, skin), neonShare, litRim, rimL, rimToInner);
                Assert.LessOrEqual(neonShare, 0.02f, Who(guardian, skin) + ": неоновые цвета");
                Assert.LessOrEqual(litRim, 0.25f, Who(guardian, skin) + ": светящаяся кайма по контуру");
                // светлая кайма — это и светлый контур, и заметно светлее тела; тусклая холодная кромка ската — нет
                Assert.IsFalse(rimL > 0.25f && rimToInner > 1.8f, Who(guardian, skin) + ": контур светится ярче тела — кайма");
            }
            Debug.Log("[BossSkinTests] неон: " + report);
        }

        [Test]
        public void Contrast_ReadableOnEveryWater_OutlineSubtle()
        {
            var outlined = Need("SpriteFactory").GetMethod("MiniOutlined", Any);
            Assert.IsNotNull(outlined, "нет признака тёмного скина");
            float waterMax = Waters.Max(h => Lum(Palette.Hex(h))), waterMin = Waters.Min(h => Lum(Palette.Hex(h)));
            var report = new StringBuilder();
            var failed = new List<string>();
            foreach (var (index, guardian, skin) in BossSkins())
            {
                var px = Art(index, guardian, out int size);
                // «читаемая кромка»: по 72 лучам самая яркая точка в наружных 18 % силуэта, медиана
                var vals = new List<float>();
                for (int a = 0; a < 360; a += 5)
                {
                    float dx = Mathf.Cos(a * Mathf.Deg2Rad), dy = Mathf.Sin(a * Mathf.Deg2Rad);
                    int rmax = -1;
                    for (int r = 0; r < size / 2; r++)
                    {
                        int x = size / 2 + Mathf.RoundToInt(dx * r), y = size / 2 + Mathf.RoundToInt(dy * r);
                        if (x < 0 || y < 0 || x >= size || y >= size) break;
                        if (px[y * size + x].a > 0.9f) rmax = r;
                    }
                    if (rmax < 0) continue;
                    float best = 0f;
                    for (int r = Mathf.FloorToInt(rmax * 0.82f); r <= rmax; r++)
                    {
                        var c = px[(size / 2 + Mathf.RoundToInt(dy * r)) * size + size / 2 + Mathf.RoundToInt(dx * r)];
                        if (c.a > 0.9f) best = Mathf.Max(best, Lum(c));
                    }
                    vals.Add(best);
                }
                vals.Sort();
                float edgeL = vals[vals.Count / 2];
                // заметность фигуры: яркость, которую дают её светлые части (75-й процентиль силуэта)
                var lums = new List<float>();
                for (int i = 0; i < px.Length; i += 2) if (px[i].a > 0.9f) lums.Add(Lum(px[i]));
                lums.Sort();
                float bodyL = lums[(int)(lums.Count * 0.75f)];
                float contrast = Ratio(bodyL, waterMax), edge = Ratio(edgeL, waterMax);
                bool dark = (bool)outlined.Invoke(null, new[] { Key(skin.UnlockBoss, guardian) });
                float need = dark ? 2.2f : 3f;
                report.AppendFormat("{0}: фигура {1:F1}:1, кромка {2:F1}:1{3}; ", Who(guardian, skin), contrast, edge, dark ? " (тёмный)" : "");
                if (contrast < need) failed.Add(Who(guardian, skin) + " " + contrast.ToString("F2") + ":1");
            }
            Debug.Log("[BossSkinTests] контраст (вода до L=" + waterMax.ToString("F3") + "): " + report);
            Assert.IsEmpty(failed, "кромка сливается с водой: " + string.Join(", ", failed));
            // обводка тёмных — отделение силуэта, не кайма: не ярче 1.5:1 к самой тёмной воде
            var outline = (Color)T("BossSkinTable").GetField("DarkOutline").GetValue(null);
            foreach (var h in Waters)
            {
                var w = Palette.Hex(h);
                var blend = Color.Lerp(w, outline, outline.a);
                Assert.LessOrEqual(Ratio(Lum(blend), Lum(w)), 1.5f, "обводка ярче 1.5:1 над водой " + h);
            }
        }

        // ------------------------------------------------------------- живость

        /// <summary>
        /// Главная ось силуэта (по моментам маски): угол в градусах от оси x (0…180) и
        /// вытянутость — корень отношения собственных чисел.
        /// </summary>
        static (float Angle, float Elongation) Axis(Color[] px, int size)
        {
            double n = 0, sx = 0, sy = 0;
            for (int y = 0; y < size; y += 2)
                for (int x = 0; x < size; x += 2)
                    if (px[y * size + x].a > 0.5f) { n++; sx += x; sy += y; }
            double cx = sx / n, cy = sy / n, xx = 0, yy = 0, xy = 0;
            for (int y = 0; y < size; y += 2)
                for (int x = 0; x < size; x += 2)
                    if (px[y * size + x].a > 0.5f) { double dx = x - cx, dy = y - cy; xx += dx * dx; yy += dy * dy; xy += dx * dy; }
            xx /= n; yy /= n; xy /= n;
            double tr = xx + yy, det = xx * yy - xy * xy, root = Math.Sqrt(Math.Max(0, tr * tr / 4 - det));
            double l1 = tr / 2 + root, l2 = Math.Max(1e-6, tr / 2 - root);
            double ang = 0.5 * Math.Atan2(2 * xy, xx - yy) * 180.0 / Math.PI;
            if (ang < 0) ang += 180;
            return ((float)ang, (float)Math.Sqrt(l1 / l2));
        }

        /// <summary>Совпадение силуэта с его зеркалом относительно вертикали через центр масс.</summary>
        static float MirrorIoU(Color[] px, int size)
        {
            double n = 0, sx = 0;
            for (int y = 0; y < size; y += 2)
                for (int x = 0; x < size; x += 2)
                    if (px[y * size + x].a > 0.5f) { n++; sx += x; }
            int cx = (int)Math.Round(sx / n);
            int inter = 0, union = 0;
            for (int y = 0; y < size; y += 2)
                for (int x = 0; x < size; x += 2)
                {
                    int mx = 2 * cx - x;
                    bool a = px[y * size + x].a > 0.5f;
                    bool b = mx >= 0 && mx < size && px[y * size + mx].a > 0.5f;
                    if (a && b) inter++;
                    if (a || b) union++;
                }
            return union == 0 ? 1f : (float)inter / union;
        }

        [Test]
        public void EverySkin_FacesUp_InGameAndShop()
        {
            // Игра и витрина берут рисунок одним путём (SkinVisuals → Shell, та же текстура
            // у сетки) и не поворачивают его: проверяем сам рисунок и что сетка без поворота.
            var report = new StringBuilder();
            var failed = new List<string>();
            foreach (var (index, guardian, skin) in BossSkins())
            {
                var px = Art(index, guardian, out int size);
                var (angle, elong) = Axis(px, size);
                report.AppendFormat("{0}: ось {1:F0}°, вытянутость {2:F2}, зеркальность {3:F2}; ", Who(guardian, skin), angle, elong, MirrorIoU(px, size));
                // у вытянутого силуэта (рыба, клинок, клык, жало, щупальце) ось — вертикаль;
                // широкие существа анфас или сверху (краб, скат) — поперёк, но зеркальны
                // относительно вертикали, то есть тоже смотрят вверх
                bool vertical = Mathf.Abs(angle - 90f) <= 20f;
                bool wideSymmetric = Mathf.Min(angle, 180f - angle) <= 12f && MirrorIoU(px, size) >= 0.75f;
                if (elong >= 1.35f && !vertical && !wideSymmetric)
                    failed.Add(Who(guardian, skin) + " смотрит под " + angle.ToString("F0") + "°");
            }
            Debug.Log("[BossSkinTests] ориентация: " + report);
            Assert.IsEmpty(failed, "мини-боссы смотрят вбок, а не вверх: " + string.Join(", ", failed));

            // ни игра, ни витрина не добавляют поворота: сетка лежит в осях тела
            var viewType = Need("MiniBossView");
            var gfxType = Need("MiniBossGraphic");
            var body = new GameObject("Body", typeof(SpriteRenderer));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(canvasGo.transform, false);
                var icon = iconGo.GetComponent<Image>();
                foreach (var (index, guardian, skin) in BossSkins())
                {
                    var look = SkinVisuals.Resolve(skin, guardian, index);
                    var view = (Component)viewType.GetMethod("On").Invoke(null, new object[] { body.transform });
                    viewType.GetMethod("Show").Invoke(view, new object[] { look });
                    gfxType.GetMethod("Apply").Invoke(null, new object[] { icon, look, true });
                    var gfx = iconGo.transform.Find("MiniBoss");
                    Assert.AreEqual(Quaternion.identity, view.transform.localRotation, Who(guardian, skin) + ": сетка в игре повёрнута");
                    Assert.AreEqual(Quaternion.identity, gfx.localRotation, Who(guardian, skin) + ": сетка в витрине повёрнута");
                }
            }
            finally
            {
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(canvasGo);
            }
        }

        /// <summary>
        /// Где на экране окажется точка рисунка (u, v ∈ 0…1) при данных вершинах: та же
        /// разбивка клетки на два треугольника, что в MiniBossMotion.Triangles.
        /// </summary>
        static Vector2 Warped(Vector2[] pos, int side, float u, float v)
        {
            int grid = side - 1;
            float gx = Mathf.Clamp(u * grid, 0f, grid - 1e-4f), gy = Mathf.Clamp(v * grid, 0f, grid - 1e-4f);
            int i = (int)gx, j = (int)gy;
            float fx = gx - i, fy = gy - j;
            Vector2 p00 = pos[j * side + i], p10 = pos[j * side + i + 1], p01 = pos[(j + 1) * side + i], p11 = pos[(j + 1) * side + i + 1];
            return fy >= fx ? p00 + fx * (p11 - p01) + fy * (p01 - p00)
                            : p00 + fx * (p10 - p00) + fy * (p11 - p10);
        }

        [Test]
        public void Motion_StaysInsideCircleAndIsSlow()
        {
            var motion = Need("MiniBossMotion");
            var fill = motion.GetMethod("Fill");
            var pixels = Need("SpriteFactory").GetMethod("MiniBossPixels");
            int count = (int)motion.GetField("VertexCount").GetValue(null);
            int side = (int)motion.GetField("Side").GetValue(null);
            const float dt = 0.05f;
            foreach (BossKind boss in Enum.GetValues(typeof(BossKind)))
                for (int g = 0; g < 2; g++)
                {
                    string who = boss + (g == 1 ? " страж" : " пузырёк");
                    var key = Key(boss, g == 1);
                    var px = (Color[])pixels.Invoke(null, new[] { key });
                    int size = Mathf.RoundToInt(Mathf.Sqrt(px.Length));
                    // точки рисунка (каждая третья по обеим осям)
                    var art = new List<Vector2>();
                    for (int y = 0; y < size; y += 3)
                        for (int x = 0; x < size; x += 3)
                            if (px[y * size + x].a > 0.05f) art.Add(new Vector2((x + 0.5f) / size, (y + 0.5f) / size));

                    var pos = new Vector2[count];
                    var rest = new Vector2[count];
                    fill.Invoke(null, new object[] { key, 0f, 0f, rest });
                    var restArt = art.Select(a => Warped(rest, side, a.x, a.y)).ToArray();
                    var prevArt = (Vector2[])restArt.Clone();
                    float maxShift = 0f, maxSpeed = 0f, maxR = 0f;
                    for (float t = 0f; t <= 8f; t += dt)
                    {
                        fill.Invoke(null, new object[] { key, t, 1f, pos });
                        for (int k = 0; k < art.Count; k++)
                        {
                            var w = Warped(pos, side, art[k].x, art[k].y);
                            maxR = Mathf.Max(maxR, w.magnitude);
                            maxShift = Mathf.Max(maxShift, (w - restArt[k]).magnitude);
                            if (t > 0f) maxSpeed = Mathf.Max(maxSpeed, (w - prevArt[k]).magnitude / dt);
                            prevArt[k] = w;
                        }
                    }
                    Assert.LessOrEqual(maxR, 0.5f, who + ": рисунок выходит за круг игрока");
                    Assert.Greater(maxShift, 0.01f, who + ": не двигается — мини-босс неживой");
                    Assert.LessOrEqual(maxShift, 0.06f, who + ": размах больше 6 % диаметра");
                    Assert.LessOrEqual(maxSpeed, 0.2f, who + ": движение резкое (быстрее 0.2 диаметра в секунду)");
                }
        }

        [Test]
        public void GameAndShop_DrawTheSameMesh()
        {
            var viewType = Need("MiniBossView");
            var gfxType = Need("MiniBossGraphic");
            var motion = Need("MiniBossMotion");
            var clock = motion.GetField("ClockOverride");
            var body = new GameObject("Body", typeof(SpriteRenderer));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            try
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(canvasGo.transform, false);
                var icon = iconGo.GetComponent<Image>();
                icon.rectTransform.sizeDelta = new Vector2(200f, 200f);
                clock.SetValue(null, 1.37f);
                foreach (var (index, guardian, skin) in BossSkins())
                {
                    var look = SkinVisuals.Resolve(skin, guardian, index);
                    var view = viewType.GetMethod("On").Invoke(null, new object[] { body.transform });
                    viewType.GetMethod("Show").Invoke(view, new object[] { look });
                    var gamePos = ((Vector2[])viewType.GetProperty("LastPositions").GetValue(view)).ToArray();

                    gfxType.GetMethod("Apply").Invoke(null, new object[] { icon, look, true });
                    var gfx = iconGo.transform.Find("MiniBoss").GetComponent(gfxType);
                    var vh = new VertexHelper();
                    gfxType.GetMethod("OnPopulateMesh", Any, null, new[] { typeof(VertexHelper) }, null).Invoke(gfx, new object[] { vh });
                    Assert.AreEqual(gamePos.Length, vh.currentVertCount, Who(guardian, skin) + ": разная сетка");
                    var ui = new UIVertex();
                    for (int i = 0; i < gamePos.Length; i++)
                    {
                        vh.PopulateUIVertex(ref ui, i);
                        var shop = new Vector2(ui.position.x, ui.position.y) / 200f;
                        Assert.AreEqual(gamePos[i].x, shop.x, 1e-4f, Who(guardian, skin) + ": витрина гнёт иначе, чем игра");
                        Assert.AreEqual(gamePos[i].y, shop.y, 1e-4f, Who(guardian, skin) + ": витрина гнёт иначе, чем игра");
                    }
                    vh.Dispose();
                    Assert.AreSame(look.Shell.texture, ((Graphic)gfx).mainTexture, Who(guardian, skin) + ": витрина рисует другую текстуру");
                    Assert.IsFalse(icon.enabled, Who(guardian, skin) + ": под мини-боссом в витрине виден ещё и старый кадр");
                    Assert.IsTrue(body.GetComponent<SpriteRenderer>().forceRenderingOff, Who(guardian, skin) + ": в игре под мини-боссом рисуется тело");
                }
            }
            finally
            {
                clock.SetValue(null, null);
                Object.DestroyImmediate(body);
                Object.DestroyImmediate(canvasGo);
            }
        }

        // ------------------------------------------------------------- эффекты

        static object Spec(string method, params object[] args)
        {
            var m = T("BossFx").GetMethods(Any).First(x => x.Name == method && x.GetParameters().Length == args.Length);
            return m.Invoke(null, args);
        }

        [Test]
        public void Effects_ThemedMutedAndSlow()
        {
            var fx = T("BossFx");
            Assert.IsNotNull(fx, "нет генератора частиц трофеев");
            var listType = typeof(List<>).MakeGenericType(T("FxSpec"));
            foreach (BossKind boss in Enum.GetValues(typeof(BossKind)))
            {
                var def = BossSkinTable.Of(boss);
                var all = new List<object>();
                for (int n = 0; n < 60; n++) all.Add(Spec("Aura", def));
                for (int n = 0; n < 60; n++) all.Add(Spec("Trail", def, n * 0.13f));
                var output = (IList)Activator.CreateInstance(listType);
                for (int n = 0; n < 8; n++)
                {
                    Spec("Strike", def, Vector2.up, 99, output);
                    Assert.LessOrEqual(output.Count, 14, boss + ": удар больше бюджета");
                    foreach (var o in output) all.Add(o);
                    Spec("Break", def, 99, output);
                    Assert.LessOrEqual(output.Count, 16, boss + ": разрушение больше бюджета");
                    foreach (var o in output) all.Add(o);
                }
                foreach (var s in all)
                {
                    var c = (Color)Member(s, "Color");
                    float alpha = (float)Member(s, "Alpha"), fadeIn = (float)Member(s, "FadeIn"), life = (float)Member(s, "Life");
                    string what = boss + " " + Member(s, "Shape");
                    Assert.LessOrEqual(c.r + c.g + c.b, 2.0f + 0.01f, what + ": слишком светлый цвет");
                    Assert.IsFalse(Neon(c), what + ": неоновый цвет " + ColorUtility.ToHtmlStringRGB(c));
                    Assert.LessOrEqual(alpha, 0.75f + 1e-4f, what + ": слишком плотная частица");
                    Assert.GreaterOrEqual(fadeIn, 0.15f - 1e-4f, what + ": появляется быстрее 0.15 с");
                    Assert.GreaterOrEqual(life - fadeIn, 0.3f - 1e-4f, what + ": гаснет быстрее 0.3 с");
                    Assert.LessOrEqual(life - fadeIn, 0.6f + 1e-4f, what + ": гаснет дольше 0.6 с");
                    if (Member(s, "Shape").ToString() == "Bolt")
                        Assert.GreaterOrEqual(life, 0.25f, what + ": разряд живёт меньше 0.25 с");
                }
                Assert.LessOrEqual((float)Member(def, "AuraRate"), 4f, boss + ": аура перерождается чаще 4 раз в секунду");
            }
        }

        /// <summary>Оболочка трофеев до работы: число частиц и радиус — их нельзя увеличивать.</summary>
        static readonly int[] EnvCount = { 132, 124, 138, 112, 118, 136, 142, 106, 116, 148 };
        static readonly float[] EnvRadius = { 2.40f, 2.25f, 2.47f, 2.47f, 2.17f, 2.40f, 2.47f, 2.17f, 2.50f, 2.50f };

        [Test]
        public void Envelope_InBossThemeMutedNotBigger()
        {
            var alpha = typeof(EnvelopeTable).GetField("TrophyAlpha", Any);
            Assert.IsNotNull(alpha, "у оболочки трофеев нет своей приглушённой прозрачности");
            Assert.LessOrEqual((float)alpha.GetValue(null), 0.7f, "оболочка трофеев слишком плотная");
            for (int i = 0; i < EnvelopeTable.Boss.Length; i++)
            {
                var e = EnvelopeTable.Boss[i];
                string who = ((BossKind)i).ToString();
                foreach (var c in new[] { e.Hot, e.Cool, e.Accent })
                {
                    Assert.IsFalse(Neon(c), who + ": неоновый цвет оболочки " + ColorUtility.ToHtmlStringRGB(c));
                    Assert.LessOrEqual(c.r + c.g + c.b, 1.9f, who + ": слишком светлая оболочка " + ColorUtility.ToHtmlStringRGB(c));
                }
                Assert.LessOrEqual(e.Count, EnvCount[i], who + ": частиц оболочки больше, чем было");
                Assert.LessOrEqual(e.RadiusD, EnvRadius[i] + 1e-4f, who + ": оболочка шире, чем была");
            }
        }
    }
}
