using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Оформление локации: цвет воды, цвет стен и коралловой корки, тон шаров.</summary>
    public struct ZoneTheme
    {
        /// <summary>Ключ локализации названия локации.</summary>
        public string NameKey;
        public Color WaterTop, WaterBottom;
        public Color WallDeep, Wall, Crust;
        public Color BlobTint;
    }

    /// <summary>
    /// Каталог локаций и их наполнение. Локация = тема + профиль стен + несколько
    /// композиций из коралловых шаров.
    ///
    /// Как и у построек, для каждой композиции подбирается смещение так, чтобы прямо
    /// над пузырьком стояло не больше zoneMaxCenterBlobs шаров — иначе пробить проход
    /// стражем было бы невозможно.
    /// </summary>
    public class ZoneSpawner : MonoBehaviour
    {
        static readonly ZoneTheme[] Themes =
        {
            new ZoneTheme
            {
                NameKey = "zone.canyon",
                WaterTop = Hex("1E5A5E"), WaterBottom = Hex("0E3238"),
                WallDeep = Hex("4A1E14"), Wall = Hex("6B2F22"), Crust = Hex("B5573F"),
                BlobTint = Hex("FFF3E6"),
            },
            new ZoneTheme
            {
                NameKey = "zone.kelp",
                WaterTop = Hex("1B4A32"), WaterBottom = Hex("08251A"),
                WallDeep = Hex("0C2013"), Wall = Hex("16321F"), Crust = Hex("2E7A4A"),
                BlobTint = Hex("EAF6E6"),
            },
            new ZoneTheme
            {
                NameKey = "zone.city",
                WaterTop = Hex("2A4258"), WaterBottom = Hex("101E2C"),
                WallDeep = Hex("1E2831"), Wall = Hex("2E3B47"), Crust = Hex("55697A"),
                BlobTint = Hex("EFF3F7"),
            },
            new ZoneTheme
            {
                NameKey = "zone.trench",
                WaterTop = Hex("2A1B45"), WaterBottom = Hex("0C0718"),
                WallDeep = Hex("140A22"), Wall = Hex("1E1230"), Crust = Hex("6B3FA0"),
                BlobTint = Hex("D9CBFF"),
            },
            new ZoneTheme
            {
                NameKey = "zone.shoal",
                WaterTop = Hex("3E6B72"), WaterBottom = Hex("1C3D46"),
                WallDeep = Hex("6B5029"), Wall = Hex("8A6A3A"), Crust = Hex("C9A45E"),
                BlobTint = Hex("FFF6E0"),
            },
        };

        // Профиль: где проходит внутренний край стены, в долях половины игровой зоны.
        // t = 0 — вход в локацию (низ), t = 1 — выход (верх). Больше 1 — стена за экраном.
        static readonly Func<float, float>[] Profiles =
        {
            t => 0.60f,                                         // прямой коридор
            t => Mathf.Lerp(0.45f, 1.20f, t),                   // воронка, расширяется вверх
            t => Mathf.Lerp(1.20f, 0.48f, t),                   // воронка, сужается вверх
            t => 0.50f + 0.78f * Mathf.Sin(t * Mathf.PI),       // зал: узкие вход и выход
            t => 1.25f - 0.75f * Mathf.Sin(t * Mathf.PI),       // перешеек: узко посередине
        };

        enum Pattern { Pyramid, Arc, Ridge, Cluster, Gate }

        GameConfig _cfg;
        Playfield _field;
        Transform _root;

        readonly Stack<OceanZone> _pool = new Stack<OceanZone>();
        int _lastTheme = -1;

        public void Init(GameConfig cfg, Playfield field, Transform root)
        {
            _cfg = cfg;
            _field = field;
            _root = root;
        }

        public OceanZone Spawn()
        {
            // тему не повторяем подряд
            int themeIndex = UnityEngine.Random.Range(0, Themes.Length);
            if (themeIndex == _lastTheme) themeIndex = (themeIndex + 1) % Themes.Length;
            _lastTheme = themeIndex;

            var theme = Themes[themeIndex];
            var profile = Profiles[UnityEngine.Random.Range(0, Profiles.Length)];
            float height = _cfg.zoneHeight;

            var zone = Rent();
            zone.Activate(theme, profile, height, _field, UnityEngine.Random.Range(0, 10000));

            FillContent(zone, theme, profile, height);
            AddWallDecor(zone, theme, profile, height);
            zone.FinishSetup();
            return zone;
        }

        // -------------------------------------------------------- наполнение

        void FillContent(OceanZone zone, ZoneTheme theme, Func<float, float> profile, float height)
        {
            // Композиции расставляем примерно через каждые 5.5 юнита, поэтому длину
            // локации можно менять в инспекторе — наполнение подстроится само.
            int slotCount = Mathf.Clamp(Mathf.RoundToInt(height / 5.5f), 3, 10);

            for (int i = 0; i < slotCount; i++)
            {
                float t = Mathf.Lerp(0.10f, 0.90f, slotCount > 1 ? i / (float)(slotCount - 1) : 0.5f)
                          + UnityEngine.Random.Range(-0.02f, 0.02f);
                float y = -height * 0.5f + t * height;
                float passage = Mathf.Min(profile(t) * _field.PlayHalfWidth, _field.PlayHalfWidth);

                // В самом узком месте каньона композицию не ставим: там и без шаров
                // тесно, а зажатые в проход шары не оставили бы шанса пробиться.
                if (passage < 1.1f) continue;

                var pattern = (Pattern)UnityEngine.Random.Range(0, 5);
                var points = BuildPattern(pattern, passage);
                if (points.Count == 0) continue;

                float offset = PickOffset(points, passage);
                for (int j = 0; j < points.Count; j++)
                {
                    var point = points[j];
                    float x = point.x + offset;
                    float radius = Mathf.Min(point.z, passage * 0.34f);

                    // Не влезает в проход — просто выбрасываем шар, а не вжимаем его
                    // к центру: иначе узкие места становились бы смертельными.
                    if (Mathf.Abs(x) > passage - radius) continue;

                    zone.AddBlob(new Vector2(x, y + point.y), radius, theme.BlobTint);
                }
            }
        }

        /// <summary>Точки композиции: x, y и радиус в компоненте z.</summary>
        List<Vector3> BuildPattern(Pattern pattern, float passage)
        {
            var points = new List<Vector3>();
            float small = _cfg.zoneBlobMinRadius;
            float big = _cfg.zoneBlobMaxRadius;
            float step = Mathf.Min(big * 2.15f, Mathf.Max(0.8f, passage * 0.62f));

            switch (pattern)
            {
                case Pattern.Pyramid:
                {
                    // 1-2-3 сверху вниз, как на референсе
                    int rows = passage > 2.2f ? 3 : 2;
                    float r = Mathf.Lerp(small, big, 0.45f);
                    for (int row = 0; row < rows; row++)
                    {
                        int count = row + 1;
                        for (int c = 0; c < count; c++)
                        {
                            float x = (c - (count - 1) * 0.5f) * step;
                            points.Add(new Vector3(x, -row * step * 0.92f, r));
                        }
                    }
                    break;
                }

                case Pattern.Arc:
                {
                    int count = passage > 2.4f ? 7 : 5;
                    float radius = Mathf.Min(passage * 0.85f, 2.6f);
                    float r = Mathf.Lerp(small, big, 0.30f);
                    for (int i = 0; i < count; i++)
                    {
                        float a = Mathf.Lerp(165f, 15f, i / (float)(count - 1)) * Mathf.Deg2Rad;
                        points.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * 0.55f, r));
                    }
                    break;
                }

                case Pattern.Ridge:
                {
                    // коралловая гряда по диагонали, шары мельчают к хвосту
                    int count = 5;
                    int dir = UnityEngine.Random.value < 0.5f ? -1 : 1;
                    for (int i = 0; i < count; i++)
                    {
                        float k = i / (float)(count - 1);
                        float r = Mathf.Lerp(big, small, k);
                        points.Add(new Vector3(dir * (k - 0.5f) * passage * 1.5f,
                                               (k - 0.5f) * 2.2f, r));
                    }
                    break;
                }

                case Pattern.Cluster:
                {
                    int count = 3;
                    for (int i = 0; i < count; i++)
                    {
                        float r = UnityEngine.Random.Range(big * 0.8f, big);
                        points.Add(new Vector3(
                            UnityEngine.Random.Range(-passage * 0.6f, passage * 0.6f),
                            UnityEngine.Random.Range(-1.3f, 1.3f), r));
                    }
                    break;
                }

                default: // Gate — две колонны с проходом посередине
                {
                    float r = Mathf.Lerp(small, big, 0.5f);
                    float side = Mathf.Max(0.9f, passage * 0.62f);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = (i - 1) * r * 2.1f;
                        points.Add(new Vector3(-side, y, r));
                        points.Add(new Vector3(side, y, r));
                    }
                    break;
                }
            }
            return points;
        }

        /// <summary>Смещаем композицию так, чтобы над пузырьком было мало шаров.</summary>
        float PickOffset(List<Vector3> points, float passage)
        {
            float limit = Mathf.Max(0f, passage * 0.55f);
            float best = 0f;
            int bestCount = int.MaxValue;

            for (int attempt = 0; attempt < 10; attempt++)
            {
                float candidate = limit > 0.01f ? UnityEngine.Random.Range(-limit, limit) : 0f;

                int blocked = 0;
                for (int i = 0; i < points.Count; i++)
                    if (Mathf.Abs(points[i].x + candidate) < points[i].z + 0.30f) blocked++;

                if (blocked <= Difficulty.MaxCenterBlocks(_cfg.zoneMaxCenterBlobs)) return candidate;
                if (blocked < bestCount) { bestCount = blocked; best = candidate; }
            }
            return best;
        }

        void AddWallDecor(OceanZone zone, ZoneTheme theme, Func<float, float> profile, float height)
        {
            var kelp = SpriteFactory.Kelp();
            var pillar = SpriteFactory.CoralPillar();
            Color tint = Color.Lerp(theme.Crust, Color.white, 0.25f);

            int decorCount = Mathf.Clamp(Mathf.RoundToInt(height / 3f), 6, 20);
            for (int i = 0; i < decorCount; i++)
            {
                float t = UnityEngine.Random.Range(0.03f, 0.97f);
                float y = -height * 0.5f + t * height;
                float inner = profile(t) * _field.PlayHalfWidth;
                if (inner > _field.HalfWidth) continue; // стена за экраном — украшать нечего

                bool right = i % 2 == 0;
                bool useKelp = UnityEngine.Random.value < 0.55f;
                float size = useKelp ? UnityEngine.Random.Range(2.4f, 3.6f)
                                     : UnityEngine.Random.Range(1.8f, 2.8f);
                float x = (right ? 1f : -1f) * (inner + UnityEngine.Random.Range(-0.15f, 0.35f));

                zone.AddDecor(useKelp ? kelp : pillar, new Vector2(x, y), size, right, tint);
            }
        }

        // ------------------------------------------------------------- пул

        OceanZone Rent()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("OceanZone");
            go.transform.SetParent(_root, false);
            return go.AddComponent<OceanZone>();
        }

        public void Recycle(OceanZone zone)
        {
            if (zone == null || zone.Pooled) return;
            zone.Pooled = true;
            zone.Deactivate();
            _pool.Push(zone);
        }

        static Color Hex(string hex) => Palette.Hex(hex);
    }
}
