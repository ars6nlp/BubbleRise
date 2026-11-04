using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BubbleRise
{
    /// <summary>
    /// Прогон честности локаций без Play: настоящий ZoneSpawner на поле заданного
    /// аспекта, каждый вид × пресет × N зёрен. На каждую локацию — нагрузка на
    /// линии пузырька за 2 с (ZoneLoad), безупречный игрок (ZoneLoad.Simulate),
    /// самый узкий свободный проход между шарами внутри стен.
    ///
    /// Tools → BubbleRise → 6. Прогон локаций, или из командной строки:
    ///   Unity -batchmode -projectPath … -executeMethod BubbleRise.ZoneSim.RunBatch
    ///         -zoneSimOut путь -zoneSimSeeds 40 -quit
    /// </summary>
    public static class ZoneSim
    {
        /// <summary>Аспекты (ширина / высота) из задачи: 20:9, 16:9, 4:3 — и их кадры.</summary>
        public static readonly (string Name, int W, int H)[] Aspects =
        {
            ("0.45", 1080, 2400), ("0.5625", 1080, 1920), ("0.75", 1536, 2048),
        };

        /// <summary>Скорость пальца безупречного игрока, юн./с — экран поперёк за ~0.8 с. [ГИПОТЕЗА]</summary>
        public const float MoveSpeed = 6f;
        public const float Reaction = 0.5f;

        public struct Sample
        {
            public ZoneKind Kind;
            public int Seed;
            public int Blobs, OnLine, PeakCap, PeakTop;
            public float MeanCap, Height;
            /// <summary>Скорость пальца, при которой безупречный игрок успевает всё, что можно (ZoneLoad.RequiredSpeed).</summary>
            public float Required;
            public ZoneLoad.Perfect AtCap, AtTop;
            /// <summary>Самый узкий из самых широких просветов поперёк прохода, юн.</summary>
            public float NarrowestGap;
        }

        /// <summary>Сцена для прогона: камера с аспектом, поле, конфиг, сложность, спавнер.</summary>
        public sealed class Rig : IDisposable
        {
            public readonly GameObject Root;
            public readonly GameConfig Cfg;
            public readonly Playfield Field;
            public readonly ZoneSpawner Spawner;
            readonly RenderTexture _rt;
            readonly DifficultyLevel _was;

            public Rig(int w, int h)
            {
                Root = new GameObject("ZoneSimRig");
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

            public float BubbleY => Field.YFromBottom(Cfg.bubbleYFactor);
            public float BubbleHit => PlayerSize.Bubble(Cfg) * Difficulty.Current.bubbleHitboxScale;
            public float Reach => Cfg.guardianRadius * PlayerSize.Scale * Difficulty.Current.guardianReachScale;
            public float MinDistance => Cfg.guardianMinDistanceFromBubble * PlayerSize.Scale;
            public float SpeedCap => Cfg.maxScrollSpeed * Difficulty.Current.speedCapScale;
            public float SpeedTop => SpeedCap * Cfg.maxScrollSpeedGrowth;

            /// <summary>Локация вида kind с зерном seed: шары (x, y от середины, r) и сама локация.</summary>
            public Sample Run(ZoneKind kind, int seed, List<Vector3> blobs = null)
            {
                blobs = blobs ?? new List<Vector3>();
                blobs.Clear();
                UnityEngine.Random.InitState(seed);
                var zone = Spawner.Spawn(kind);
                foreach (var b in zone.Blobs)
                    if (b.Alive) blobs.Add(new Vector3(b.Tr.localPosition.x, b.Tr.localPosition.y, b.Radius));

                var s = new Sample { Kind = kind, Seed = seed, Blobs = blobs.Count, Height = zone.Height };
                float y0 = zone.transform.position.y;
                s.OnLine = ZoneLoad.LineBlobs(blobs, BubbleHit).Count;
                s.PeakCap = ZoneLoad.Peak(blobs, SpeedCap, BubbleHit);
                s.PeakTop = ZoneLoad.Peak(blobs, SpeedTop, BubbleHit);
                s.MeanCap = ZoneLoad.Mean(blobs, zone.Height, SpeedCap, BubbleHit);
                var home = new Vector2(0f, BubbleY) + Cfg.guardianHomeOffset * PlayerSize.Scale;
                s.AtCap = ZoneLoad.Simulate(blobs, y0, SpeedCap, Field.Top, BubbleY, BubbleHit, Reach, MinDistance,
                                            Cfg.guardianDeflectCooldown, Reaction, MoveSpeed, home);
                s.AtTop = ZoneLoad.Simulate(blobs, y0, SpeedTop, Field.Top, BubbleY, BubbleHit, Reach, MinDistance,
                                            Cfg.guardianDeflectCooldown, Reaction, MoveSpeed, home);
                s.Required = ZoneLoad.RequiredSpeed(blobs, y0, SpeedCap, Field.Top, BubbleY, BubbleHit, Reach, MinDistance,
                                                    Cfg.guardianDeflectCooldown, Reaction, home);
                s.NarrowestGap = NarrowestGap(zone, blobs);
                Spawner.Recycle(zone);
                return s;
            }

            /// <summary>
            /// Поперёк прохода (между стенами по профилю) через каждые 0.1 юн.: самый
            /// широкий просвет между шарами; из всех полос — самый узкий такой просвет.
            /// </summary>
            float NarrowestGap(OceanZone zone, List<Vector3> blobs)
            {
                float worst = float.MaxValue;
                var cover = new List<Vector2>();
                float h = zone.Height;
                for (float y = -h * 0.5f + 1f; y < h * 0.5f - 1f; y += 0.1f)
                {
                    float t = (y + h * 0.5f) / h;
                    float c = zone.Centre(t), w = Mathf.Min(zone.Passage(t), Field.HalfWidth);
                    float lo = Mathf.Max(c - w, -Field.HalfWidth), hi = Mathf.Min(c + w, Field.HalfWidth);
                    cover.Clear();
                    foreach (var b in blobs)
                    {
                        float dy = Mathf.Abs(b.y - y);
                        if (dy >= b.z) continue;
                        float half = Mathf.Sqrt(b.z * b.z - dy * dy);
                        cover.Add(new Vector2(b.x - half, b.x + half));
                    }
                    if (cover.Count == 0) continue;
                    cover.Sort((a, b) => a.x.CompareTo(b.x));
                    float best = 0f, at = lo;
                    foreach (var iv in cover)
                    {
                        if (iv.x > at) best = Mathf.Max(best, Mathf.Min(iv.x, hi) - at);
                        at = Mathf.Max(at, iv.y);
                    }
                    best = Mathf.Max(best, hi - at);
                    worst = Mathf.Min(worst, best);
                }
                return worst == float.MaxValue ? 99f : worst;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(Root);
                _rt.Release();
                Object.DestroyImmediate(_rt);
                Difficulty.Set(_was);
            }
        }

        [MenuItem("Tools/BubbleRise/6. Прогон локаций (честность)")]
        public static void RunMenu() => Write(Path.Combine(Application.dataPath, "..", "ZoneSim"), 40);

        public static void RunBatch()
        {
            string outDir = Arg("-zoneSimOut") ?? Path.Combine(Application.dataPath, "..", "ZoneSim");
            int seeds = int.TryParse(Arg("-zoneSimSeeds"), out var n) ? n : 40;
            try
            {
                Write(outDir, seeds);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// По шаблонам: локация из одного шаблона, сколько шаров на слот и сколько из
        /// них на линии пузырька — где перекос нагрузки.
        /// </summary>
        static void PatternTable(Rig rig, string aspect, DifficultyLevel level, int seeds, StringBuilder csv)
        {
            var ci = CultureInfo.InvariantCulture;
            var list = new List<Vector3>();
            foreach (var def in ZoneCatalog.All)
            {
                if (def.Legacy) continue;
                foreach (var p in def.Patterns)
                {
                    var one = new ZoneDef
                    {
                        Kind = def.Kind, Key = def.Key, Length = def.Length, Theme = def.Theme, HalfWidth = def.HalfWidth,
                        Shift = def.Shift, Walls = def.Walls, SlotSpacing = def.SlotSpacing, OpenChance = def.OpenChance,
                        LineBias = def.LineBias, Patterns = new[] { p },
                    };
                    float height = def.Length * rig.Cfg.zoneHeight / 38f;
                    int slots = 0, blobs = 0, hits = 0;
                    float gap = 99f;
                    for (int seed = 1; seed <= seeds; seed++)
                    {
                        UnityEngine.Random.InitState(seed * 104729);
                        ZoneSpawner.Layout(one, height, rig.Field.PlayHalfWidth, rig.Cfg, list);
                        int n = Mathf.Clamp(Mathf.RoundToInt(height * 0.8f / def.SlotSpacing) + 1, 3, 12);
                        slots += n;
                        blobs += list.Count;
                        int h = ZoneLoad.LineBlobs(list, rig.BubbleHit).Count;
                        hits += h;
                        gap = Mathf.Min(gap, LayoutGap(one, list, height, rig.Field));
                    }
                    csv.AppendLine(string.Format(ci, "{0},{1},{2},{3},{4},{5:F2},{6:F2},{7:F2}", aspect, level, def.Kind, p.Name,
                                                 slots, blobs / (float)slots, hits / (float)slots, gap));
                }
            }
        }

        /// <summary>Самый узкий просвет раскладки (как Rig.NarrowestGap, проход — по записи каталога).</summary>
        static float LayoutGap(ZoneDef def, List<Vector3> blobs, float h, Playfield field)
        {
            float worst = 99f;
            var cover = new List<Vector2>();
            for (float y = -h * 0.5f + 1f; y < h * 0.5f - 1f; y += 0.1f)
            {
                float t = (y + h * 0.5f) / h;
                float c = def.Shift(t) * field.PlayHalfWidth, w = Mathf.Min(def.HalfWidth(t) * field.PlayHalfWidth, field.HalfWidth);
                float lo = Mathf.Max(c - w, -field.HalfWidth), hi = Mathf.Min(c + w, field.HalfWidth);
                cover.Clear();
                foreach (var b in blobs)
                {
                    float dy = Mathf.Abs(b.y - y);
                    if (dy >= b.z) continue;
                    float half = Mathf.Sqrt(b.z * b.z - dy * dy);
                    cover.Add(new Vector2(b.x - half, b.x + half));
                }
                if (cover.Count == 0) continue;
                cover.Sort((a, b) => a.x.CompareTo(b.x));
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

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }

        static void Write(string outDir, int seeds)
        {
            Directory.CreateDirectory(outDir);
            var ci = CultureInfo.InvariantCulture;
            var csv = new StringBuilder("aspect,preset,kind,seed,blobs,online,peak_cap,peak_top,mean_cap,miss_cap,miss_top,slack_cap,move_cap,gap,height,req_cap\n");
            var blobs = new List<Vector3>();
            var dump = new StringBuilder();
            var patterns = new StringBuilder();
            patterns.AppendLine("aspect,preset,kind,pattern,slots,blobs_per_slot,hits_per_slot,gap");
            foreach (var a in Aspects)
            {
                using (var rig = new Rig(a.W, a.H))
                {
                    foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    {
                        Difficulty.Set(level);
                        foreach (var def in ZoneCatalog.All)
                        {
                            for (int seed = 1; seed <= seeds; seed++)
                            {
                                var s = rig.Run(def.Kind, seed * 7919, blobs);
                                if (s.AtCap.Misses > 0 && dump.Length < 200000)
                                {
                                    dump.AppendLine(string.Format(ci, "{0} {1} {2} seed {3} misses {4} y0 {5:F2}", a.Name, level, def.Kind,
                                                                  s.Seed, s.AtCap.Misses, rig.Field.Top + s.Height * 0.5f + 0.5f));
                                    foreach (var b in ZoneLoad.LineBlobs(blobs, rig.BubbleHit))
                                        dump.AppendLine(string.Format(ci, "  {0:F2} {1:F2} {2:F2}", b.x, b.y, b.z));
                                }
                                csv.Append(string.Format(ci,
                                    "{0},{1},{2},{3},{4},{5},{6},{7},{8:F3},{9},{10},{11:F3},{12:F2},{13:F3},{14:F1},{15:F2}\n",
                                    a.Name, level, def.Kind, s.Seed, s.Blobs, s.OnLine, s.PeakCap, s.PeakTop, s.MeanCap,
                                    s.AtCap.Misses, s.AtTop.Misses, s.AtCap.MinSlack, s.AtCap.PeakMove, s.NarrowestGap, s.Height, s.Required));
                            }
                        }
                    }
                    foreach (DifficultyLevel level in Enum.GetValues(typeof(DifficultyLevel)))
                    {
                        Difficulty.Set(level);
                        PatternTable(rig, a.Name, level, seeds, patterns);
                    }
                    File.AppendAllText(Path.Combine(outDir, "rig.txt"), string.Format(ci,
                        "aspect {0}: PlayHalfWidth {1:F3} HalfWidth {2:F3} bubbleY {3:F3}\n",
                        a.Name, rig.Field.PlayHalfWidth, rig.Field.HalfWidth, rig.BubbleY));
                }
            }
            File.WriteAllText(Path.Combine(outDir, "zonesim.csv"), csv.ToString());
            File.WriteAllText(Path.Combine(outDir, "misses.txt"), dump.ToString());
            File.WriteAllText(Path.Combine(outDir, "patterns.csv"), patterns.ToString());
            Debug.Log("[ZoneSim] " + Path.Combine(outDir, "zonesim.csv"));
        }
    }
}
