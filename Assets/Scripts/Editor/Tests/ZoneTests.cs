using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BubbleRise.Tests
{
    /// <summary>
    /// Локации: настоящий ZoneSpawner на поле заданного аспекта, настоящие пресеты
    /// сложности. Проверяется поведение — какие виды выходят и в каком порядке, где
    /// встают шары, успевает ли их разбить безупречный игрок, — а не текст кода.
    ///
    /// Вид локации, вызов «по очереди» и вызов конкретного вида берутся отражением,
    /// поэтому тесты собираются и на прежнем коде (два вида через раз) и там падают:
    /// видов меньше пяти, новых нет. Счёт нагрузки и безупречный игрок написаны здесь
    /// заново, независимо от ZoneLoad, — это и перекрёстная проверка инструмента.
    /// </summary>
    public class ZoneTests
    {
        static readonly (int W, int H)[] Aspects = { (1080, 2400), (1080, 1920), (1536, 2048) };
        const int Seeds = 40;
        static readonly string[] Legacy = { "Ruins", "Ice" };

        static readonly Type KindType = typeof(ZoneSpawner).Assembly.GetType("BubbleRise.ZoneKind");
        static MethodInfo SpawnMethod => typeof(ZoneSpawner).GetMethod("Spawn");

        static string[] AllKinds => KindType != null ? Enum.GetNames(KindType) : Legacy;
        static string[] NewKinds => AllKinds.Where(k => !Legacy.Contains(k)).ToArray();

        // ------------------------------------------------------------------ сцена

        sealed class Rig : IDisposable
        {
            public readonly GameObject Root;
            public readonly GameConfig Cfg;
            public readonly Playfield Field;
            public readonly ZoneSpawner Spawner;
            readonly RenderTexture _rt;
            readonly DifficultyLevel _was;

            public Rig(int w, int h)
            {
                Root = new GameObject("ZoneTest");
                var camGo = new GameObject("Cam");
                camGo.transform.SetParent(Root.transform, false);
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                var cam = camGo.AddComponent<Camera>();
                _rt = new RenderTexture(w, h, 0);
                cam.targetTexture = _rt;
                Cfg = Root.AddComponent<GameConfig>();
                PlayerSize.Bind(Cfg);
                _was = Difficulty.Level;
                Difficulty.Bind(Root.AddComponent<DifficultyConfig>());
                Field = Root.AddComponent<Playfield>();
                Field.Init(cam, Cfg.worldHeight, Cfg.designAspect);
                Spawner = Root.AddComponent<ZoneSpawner>();
                Spawner.Init(Cfg, Field, Root.transform);
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                _rt.Release();
                Object.DestroyImmediate(_rt);
                Difficulty.Set(_was);
            }

            public void ResetRun(int seed)
            {
                var withSeed = typeof(ZoneSpawner).GetMethod("ResetRun", new[] { typeof(int) });
                if (withSeed != null) withSeed.Invoke(Spawner, new object[] { seed });
                else typeof(ZoneSpawner).GetMethod("ResetRun", Type.EmptyTypes).Invoke(Spawner, null);
            }

            public OceanZone SpawnNext()
            {
                var p = SpawnMethod.GetParameters()[0];
                object arg = p.DefaultValue is DBNull ? Type.Missing : p.DefaultValue;
                return (OceanZone)SpawnMethod.Invoke(Spawner, new[] { arg });
            }

            public OceanZone Spawn(string kind)
            {
                object arg;
                if (KindType != null) arg = Enum.Parse(KindType, kind);
                else if (kind == "Ice") arg = 1;
                else if (kind == "Ruins") arg = 0;
                else { Assert.Fail("вида «" + kind + "» нет"); return null; }
                return (OceanZone)SpawnMethod.Invoke(Spawner, new[] { arg });
            }

            public float BubbleY => Field.YFromBottom(Cfg.bubbleYFactor);
            public float BubbleHit => PlayerSize.Bubble(Cfg) * Difficulty.Current.bubbleHitboxScale;
            public float Speed => Cfg.maxScrollSpeed * Difficulty.Current.speedCapScale;
            public float GuardDiameter => 2f * Cfg.guardianRadius * PlayerSize.Scale;
        }

        static string KindOf(OceanZone zone)
        {
            var p = typeof(OceanZone).GetProperty("Kind");
            return p != null ? p.GetValue(zone).ToString() : zone.IsIce ? "Ice" : "Ruins";
        }

        static List<Vector3> BlobsOf(OceanZone zone)
        {
            var list = new List<Vector3>();
            foreach (var b in zone.Blobs)
                if (b.Alive) list.Add(new Vector3(b.Tr.localPosition.x, b.Tr.localPosition.y, b.Radius));
            return list;
        }

        // ---------------------------------------------------------- счёт нагрузки

        static List<Vector3> OnLine(List<Vector3> blobs, float bubbleHit)
            => blobs.Where(b => Mathf.Abs(b.x) < b.z * Difficulty.Current.hazardHitboxScale + bubbleHit)
                    .OrderBy(b => b.y).ToList();

        /// <summary>Больше всего шаров на линии пузырька за любые 2 с.</summary>
        static int Peak2s(List<Vector3> line, float speed)
        {
            int best = 0;
            for (int i = 0; i < line.Count; i++)
            {
                int n = 0;
                for (int j = i; j < line.Count && line[j].y - line[i].y <= 2f * speed; j++) n++;
                best = Math.Max(best, n);
            }
            return best;
        }

        struct Load { public float Peak, Mean; }

        static Dictionary<string, Load> Loads(Rig rig, IEnumerable<string> kinds)
        {
            var result = new Dictionary<string, Load>();
            foreach (var kind in kinds)
            {
                float peak = 0f, mean = 0f;
                for (int seed = 1; seed <= Seeds; seed++)
                {
                    UnityEngine.Random.InitState(seed * 7919);
                    var zone = rig.Spawn(kind);
                    var line = OnLine(BlobsOf(zone), rig.BubbleHit);
                    peak += Peak2s(line, rig.Speed);
                    mean += line.Count * 2f * rig.Speed / zone.Height;
                    rig.Spawner.Recycle(zone);
                }
                result[kind] = new Load { Peak = peak / Seeds, Mean = mean / Seeds };
            }
            return result;
        }

        // ----------------------------------------------------- безупречный игрок

        /// <summary>
        /// Сколько шаров на линии не разбить никак. Шар заметен через 0.5 с после того,
        /// как край въехал в экран; разбить — пока страж (не ближе своей границы к
        /// пузырьку) ещё дотягивается; между ударами — перезарядка; палец — до 6 юн./с.
        /// </summary>
        static int Unavoidable(Rig rig, OceanZone zone, List<Vector3> blobs)
        {
            var cfg = rig.Cfg;
            float v = rig.Speed, top = rig.Field.Top, y0 = zone.transform.position.y;
            float reach = cfg.guardianRadius * PlayerSize.Scale * Difficulty.Current.guardianReachScale;
            float minDist = cfg.guardianMinDistanceFromBubble * PlayerSize.Scale;
            var line = OnLine(blobs, rig.BubbleHit).OrderBy(b => b.y - b.z).ToList();
            Vector2 pos = new Vector2(0f, rig.BubbleY + 0.65f);
            float t = float.NegativeInfinity;
            int misses = 0;
            foreach (var b in line)
            {
                float yw = y0 + b.y;
                float seen = (yw - b.z - top) / v + 0.5f;
                float last = (yw - (rig.BubbleY + minDist + reach + b.z)) / v;
                float ready = float.IsNegativeInfinity(t) ? seen : Mathf.Max(t, seen);
                float from = float.IsNegativeInfinity(t) ? seen : Mathf.Max(seen, t + cfg.guardianDeflectCooldown);
                bool hit = false;
                for (float tau = from; tau <= last; tau += 0.005f)
                {
                    var at = new Vector2(b.x, yw - v * tau);
                    float need = Mathf.Max(0f, Vector2.Distance(pos, at) - (b.z + reach) * 0.9f);
                    if (need > 6f * (tau - ready)) continue;
                    pos = at; t = tau; hit = true;
                    break;
                }
                if (!hit) misses++;
            }
            return misses;
        }

        /// <summary>Самый узкий из самых широких просветов поперёк прохода.</summary>
        static float NarrowestGap(Rig rig, OceanZone zone, List<Vector3> blobs)
        {
            float worst = float.MaxValue, h = zone.Height;
            var centre = typeof(OceanZone).GetMethod("Centre");
            for (float y = -h * 0.5f + 1f; y < h * 0.5f - 1f; y += 0.1f)
            {
                float t = (y + h * 0.5f) / h;
                float c = centre != null ? (float)centre.Invoke(zone, new object[] { t }) : 0f;
                float w = Mathf.Min(zone.Passage(t), rig.Field.HalfWidth);
                float lo = Mathf.Max(c - w, -rig.Field.HalfWidth), hi = Mathf.Min(c + w, rig.Field.HalfWidth);
                var cover = blobs.Where(b => Mathf.Abs(b.y - y) < b.z)
                                 .Select(b => { float s = Mathf.Sqrt(b.z * b.z - (b.y - y) * (b.y - y)); return new Vector2(b.x - s, b.x + s); })
                                 .OrderBy(iv => iv.x).ToList();
                if (cover.Count == 0) continue;
                float best = 0f, at = lo;
                foreach (var iv in cover)
                {
                    if (iv.x > at) best = Mathf.Max(best, Mathf.Min(iv.x, hi) - at);
                    at = Mathf.Max(at, iv.y);
                }
                worst = Mathf.Min(worst, Mathf.Max(best, hi - at));
            }
            return worst;
        }

        // ------------------------------------------------------------------ тесты

        [Test]
        public void AtLeastFiveKinds_EveryCircleHasAll()
        {
            using (var rig = new Rig(1080, 2340))
            {
                var seen = new HashSet<string>();
                for (int seed = 1; seed <= 30; seed++)
                {
                    rig.ResetRun(seed);
                    var run = new List<string>();
                    for (int i = 0; i < 25; i++)
                    {
                        var zone = rig.SpawnNext();
                        run.Add(KindOf(zone));
                        rig.Spawner.Recycle(zone);
                    }
                    seen.UnionWith(run);
                    int k = AllKinds.Length;
                    for (int start = 0; start + k <= run.Count; start += k)
                        Assert.That(run.Skip(start).Take(k).Distinct().Count(), Is.EqualTo(k),
                                    $"зерно {seed}: в круге {start / k + 1} не все виды: {string.Join(",", run.Skip(start).Take(k))}");
                }
                Assert.That(seen.Count, Is.GreaterThanOrEqualTo(5), "видов локаций: " + string.Join(", ", seen));
            }
        }

        [Test]
        public void NeverSameKindTwiceInARow_EvenAfterDevSpawn()
        {
            Assert.That(AllKinds.Length, Is.GreaterThanOrEqualTo(5), "видов локаций меньше пяти");
            using (var rig = new Rig(1080, 2340))
            {
                for (int seed = 1; seed <= 40; seed++)
                {
                    rig.ResetRun(seed);
                    string last = null;
                    for (int i = 0; i < 30; i++)
                    {
                        // время от времени — вид из панели разработчика в обход очереди
                        var zone = i % 7 == 3 ? rig.Spawn(AllKinds[(seed + i) % AllKinds.Length]) : rig.SpawnNext();
                        string kind = KindOf(zone);
                        rig.Spawner.Recycle(zone);
                        if (i % 7 != 3)
                            Assert.That(kind, Is.Not.EqualTo(last), $"зерно {seed}, локация {i}: {kind} дважды подряд");
                        last = kind;
                    }
                }
            }
        }

        [Test]
        public void NewKinds_LineLoad_60To100PercentOfHeaviestOld()
        {
            Assert.That(NewKinds.Length, Is.GreaterThanOrEqualTo(3), "новых локаций нет");
            var problems = new List<string>();
            foreach (var a in Aspects)
            {
                using (var rig = new Rig(a.W, a.H))
                {
                    foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    {
                        Difficulty.Set(level);
                        var loads = Loads(rig, AllKinds);
                        float peak = Legacy.Max(k => loads[k].Peak), mean = Legacy.Max(k => loads[k].Mean);
                        foreach (var k in NewKinds)
                        {
                            float rp = loads[k].Peak / peak, rm = loads[k].Mean / mean;
                            if (rp < 0.6f || rp > 1f || rm < 0.6f || rm > 1f)
                                problems.Add($"{a.W}x{a.H} {level} {k}: пик {rp:P0}, среднее {rm:P0}");
                        }
                    }
                }
            }
            Assert.That(problems, Is.Empty, string.Join("; ", problems));
        }

        [Test]
        public void NewKinds_PerfectPlayerTakesNoHit_AndPassageIsWide()
        {
            Assert.That(NewKinds.Length, Is.GreaterThanOrEqualTo(3), "новых локаций нет");
            var problems = new List<string>();
            using (var rig = new Rig(1080, 2400))   // самый узкий аспект
            {
                foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    Difficulty.Set(level);
                    foreach (var k in NewKinds)
                    {
                        for (int seed = 1; seed <= Seeds; seed++)
                        {
                            UnityEngine.Random.InitState(seed * 7919);
                            var zone = rig.Spawn(k);
                            var blobs = BlobsOf(zone);
                            int miss = Unavoidable(rig, zone, blobs);
                            float gap = NarrowestGap(rig, zone, blobs);
                            rig.Spawner.Recycle(zone);
                            if (miss > 0) problems.Add($"{level} {k} зерно {seed}: не разбить {miss}");
                            if (gap < 1.5f * rig.GuardDiameter) problems.Add($"{level} {k} зерно {seed}: просвет {gap:F2}");
                        }
                    }
                }
            }
            Assert.That(problems, Is.Empty, string.Join("; ", problems.Take(12)));
        }

        [Test]
        public void EveryKind_ThreatsStandOutFromWater()
        {
            Assert.That(AllKinds.Length, Is.GreaterThanOrEqualTo(5), "видов локаций меньше пяти");
            var metalField = typeof(SpriteFactory).GetField("BlobMetal", BindingFlags.Public | BindingFlags.Static);
            Color metal = metalField != null ? (Color)metalField.GetValue(null) : Color.white;
            Color urchin = Palette.Hex("7A5A8A");   // самая тёмная угроза — ёж
            var problems = new List<string>();
            using (var rig = new Rig(1080, 2340))
            {
                foreach (var k in AllKinds)
                {
                    UnityEngine.Random.InitState(11);
                    var zone = rig.Spawn(k);
                    var blob = zone.Blobs.First(b => b.Alive);
                    // к краю шар темнее: 0.88 — как в Tools/check_world.py
                    float lb = Lum(blob.Sr.color * metal) * 0.88f;
                    foreach (var water in new[] { zone.WaterTop, zone.WaterBottom })
                    {
                        float lw = Lum(water);
                        if (Ratio(lb, lw) < 3f) problems.Add($"{k}: шар к воде {Ratio(lb, lw):F1}:1");
                        if (Ratio(Lum(urchin), lw) < 2.2f) problems.Add($"{k}: ёж к воде {Ratio(Lum(urchin), lw):F1}:1");
                    }
                    rig.Spawner.Recycle(zone);
                }
            }
            Assert.That(problems, Is.Empty, string.Join("; ", problems));
        }

        [Test]
        public void EveryKind_NoLight2DOrPostProcessing()
        {
            Assert.That(AllKinds.Length, Is.GreaterThanOrEqualTo(5), "видов локаций меньше пяти");
            using (var rig = new Rig(1080, 2340))
            {
                foreach (var k in AllKinds)
                {
                    var zone = rig.Spawn(k);
                    var bad = zone.GetComponentsInChildren<Component>(true)
                                  .Where(c => c != null && (c.GetType().Name.Contains("Light2D") || c.GetType().Name == "Volume"))
                                  .Select(c => c.GetType().Name).ToList();
                    Assert.That(bad, Is.Empty, k + ": " + string.Join(", ", bad));
                    rig.Spawner.Recycle(zone);
                }
            }
        }

        static float Lum(Color c)
        {
            float L(float u) => u <= 0.04045f ? u / 12.92f : Mathf.Pow((u + 0.055f) / 1.055f, 2.4f);
            return 0.2126f * L(c.r) + 0.7152f * L(c.g) + 0.0722f * L(c.b);
        }

        static float Ratio(float a, float b) => (Mathf.Max(a, b) + 0.05f) / (Mathf.Min(a, b) + 0.05f);
    }
}
