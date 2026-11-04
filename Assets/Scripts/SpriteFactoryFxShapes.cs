using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Формы частиц фирменных эффектов боссовых скинов: разряд, зуб, клякса,
    /// осколок… Обычные эффекты остаются мягкими точками.
    /// </summary>
    public enum FxShape
    {
        Dot, Bolt, Streak, Tooth, Blob, Shard, Scale, Ring,
        Shell, Grain, Hex, Curl, Thorn, Spot, Fang, Sucker,
    }

    public static partial class SpriteFactory
    {
        /// <summary>Клетка одной формы в атласе, точек. Вся форма — в круге 0.9 клетки.</summary>
        const int FxCell = 64;

        static Sprite[] _fxShapes;

        /// <summary>
        /// Форма частицы. Все шестнадцать — в одной текстуре 256×256 и с одним
        /// материалом, поэтому частицы разных форм рисуются вместе. Белые: цвет даёт
        /// SpriteRenderer.color, как у мягкой точки. Ширина спрайта — один юнит, как у
        /// <see cref="SoftDot"/>: масштаб частицы и есть её размер.
        /// </summary>
        public static Sprite FxShapeSprite(FxShape shape)
        {
            if (shape == FxShape.Dot) return SoftDot();
            if (_fxShapes == null) BuildFxShapes();
            return _fxShapes[(int)shape];
        }

        static void BuildFxShapes()
        {
            const int grid = 4, size = FxCell * grid;
            var px = new Color[size * size];
            int count = System.Enum.GetValues(typeof(FxShape)).Length;
            for (int s = 0; s < count; s++)
            {
                int cx = (s % grid) * FxCell, cy = (s / grid) * FxCell;
                for (int y = 0; y < FxCell; y++)
                    for (int x = 0; x < FxCell; x++)
                    {
                        // 2×2 подвыборки: край мягкий без размытия формы
                        float a = 0f;
                        for (int sy = 0; sy < 2; sy++)
                            for (int sx = 0; sx < 2; sx++)
                            {
                                var p = new Vector2((x + 0.25f + sx * 0.5f) / FxCell * 2f - 1f,
                                                    (y + 0.25f + sy * 0.5f) / FxCell * 2f - 1f);
                                a += FxShapeAlpha((FxShape)s, p);
                            }
                        a *= 0.25f;
                        px[(cy + y) * size + cx + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                    }
            }

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "tex_fx_shapes",
            };
            tex.SetPixels(px);
            tex.Apply(false, true);

            _fxShapes = new Sprite[count];
            for (int s = 0; s < count; s++)
            {
                var rect = new Rect((s % grid) * FxCell, (s / grid) * FxCell, FxCell, FxCell);
                _fxShapes[s] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), FxCell, 0, SpriteMeshType.FullRect);
                _fxShapes[s].name = "fx_" + (FxShape)s;
            }
        }

        /// <summary>Непрозрачность формы в точке p ∈ [−1, 1]². Ось формы — вдоль +x.</summary>
        static float FxShapeAlpha(FxShape shape, Vector2 p)
        {
            float d = p.magnitude;
            switch (shape)
            {
                case FxShape.Bolt:
                {
                    // ломаная из трёх звеньев вдоль x, концы тоньше
                    var a = new Vector2(-0.85f, -0.10f);
                    var b = new Vector2(-0.25f, 0.22f);
                    var c = new Vector2(0.20f, -0.22f);
                    var e = new Vector2(0.85f, 0.12f);
                    float dist = Mathf.Min(DistToSegment(p, a, b), Mathf.Min(DistToSegment(p, b, c), DistToSegment(p, c, e)));
                    // толще к середине: в размере пальца тонкая ломаная пропадала [ТЕСТ]
                    float w = Mathf.Lerp(0.08f, 0.17f, 1f - Mathf.Abs(p.x) / 0.85f);
                    return Edge(w - dist, 0.05f) * Edge(0.92f - Mathf.Abs(p.x), 0.06f);
                }
                case FxShape.Streak:
                {
                    // вытянутое веретено вдоль x
                    float half = 0.16f * Mathf.Sqrt(Mathf.Max(0f, 1f - p.x * p.x / 0.8f));
                    return Edge(half - Mathf.Abs(p.y), 0.06f) * Edge(0.9f - Mathf.Abs(p.x), 0.1f);
                }
                case FxShape.Tooth:
                {
                    // треугольный зуб остриём вверх, слегка изогнутый
                    // диапазон — по координате: InverseLerp зажимает в 0…1
                    if (p.y < -0.7f || p.y > 0.85f) return 0f;
                    float t = Mathf.InverseLerp(-0.7f, 0.85f, p.y);
                    float half = Mathf.Lerp(0.45f, 0.02f, t);
                    float bend = 0.12f * t * t;
                    return Edge(half - Mathf.Abs(p.x - bend), 0.05f);
                }
                case FxShape.Blob:
                {
                    // клякса: рваный край
                    float ang = Mathf.Atan2(p.y, p.x);
                    float r = 0.62f + 0.12f * Mathf.Sin(ang * 3f + 0.7f) + 0.08f * Mathf.Sin(ang * 5f + 2.1f);
                    float body = Edge(r - d, 0.12f);
                    float drop = Edge(0.16f - (p - new Vector2(0.72f, 0.38f)).magnitude, 0.06f);
                    return Mathf.Max(body, drop);
                }
                case FxShape.Shard:
                {
                    // неправильный многоугольник-осколок
                    float a1 = Vector2.Dot(p, new Vector2(0.94f, 0.34f)) - 0.55f;
                    float a2 = Vector2.Dot(p, new Vector2(-0.6f, 0.8f)) - 0.6f;
                    float a3 = Vector2.Dot(p, new Vector2(-0.5f, -0.86f)) - 0.5f;
                    float a4 = Vector2.Dot(p, new Vector2(0.45f, -0.9f)) - 0.42f;
                    float m = Mathf.Max(Mathf.Max(a1, a2), Mathf.Max(a3, a4));
                    return Edge(-m, 0.05f);
                }
                case FxShape.Scale:
                {
                    // чешуйка: полукруг с острым концом вниз
                    var q = new Vector2(p.x, p.y + 0.15f);
                    float r = q.y > 0f ? q.magnitude : Mathf.Abs(q.x) + Mathf.Abs(q.y) * 0.7f;
                    float inner = Edge(0.66f - r, 0.06f);
                    float gloss = p.y > 0.1f ? 0.25f : 0f;
                    return Mathf.Clamp01(inner * (0.75f + gloss));
                }
                case FxShape.Ring:
                    return Edge(0.1f - Mathf.Abs(d - 0.66f), 0.05f) + Edge(0.18f - (p - new Vector2(-0.28f, 0.3f)).magnitude, 0.08f) * 0.5f;
                case FxShape.Shell:
                {
                    // гребешок: веер с рёбрами и ушками
                    if (p.y < -0.7f || d > 0.86f) return 0f;
                    float ang = Mathf.Atan2(p.y + 0.6f, p.x);
                    if (ang < 0.25f || ang > Mathf.PI - 0.25f) return 0f;
                    float rib = 0.75f + 0.25f * Mathf.Cos(ang * 18f);
                    return Edge(0.86f - d, 0.06f) * rib;
                }
                case FxShape.Grain:
                {
                    var q = new Vector2(p.x * 0.8f + p.y * 0.3f, p.y * 1.1f - p.x * 0.2f);
                    return Edge(0.48f - Mathf.Max(Mathf.Abs(q.x), Mathf.Abs(q.y)), 0.12f);
                }
                case FxShape.Hex:
                {
                    var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
                    float h = Mathf.Max(q.x * 0.866f + q.y * 0.5f, q.y);
                    float body = Edge(0.72f - h, 0.05f);
                    float rim = Edge(0.07f - Mathf.Abs(h - 0.6f), 0.04f);
                    return Mathf.Clamp01(body * 0.7f + rim * 0.3f);
                }
                case FxShape.Curl:
                {
                    // завиток: дуга спирали
                    float ang = Mathf.Atan2(p.y, p.x);
                    float r = 0.25f + 0.55f * Mathf.Repeat((ang + Mathf.PI) / (Mathf.PI * 2f), 1f);
                    float w = 0.09f * (0.5f + r);
                    return Edge(w - Mathf.Abs(d - r), 0.05f) * Edge(0.9f - d, 0.05f);
                }
                case FxShape.Thorn:
                {
                    // шип вдоль +x, основание слева
                    if (p.x < -0.8f || p.x > 0.9f) return 0f;
                    float t = Mathf.InverseLerp(-0.8f, 0.9f, p.x);
                    float half = Mathf.Lerp(0.30f, 0.0f, t);
                    return Edge(half - Mathf.Abs(p.y + 0.06f * t), 0.04f);
                }
                case FxShape.Spot:
                {
                    // «глаз»: светлое кольцо и тёмная середина
                    float ring = Edge(0.22f - Mathf.Abs(d - 0.5f), 0.08f);
                    float core = Edge(0.22f - d, 0.08f) * 0.55f;
                    return Mathf.Max(ring, core);
                }
                case FxShape.Fang:
                {
                    // изогнутый клык: дуга, сходящая на нет
                    var c = new Vector2(-0.55f, -0.3f);
                    float r = (p - c).magnitude;
                    float ang = Mathf.Atan2(p.y - c.y, p.x - c.x);
                    if (ang > 1.6f || ang < 0.05f) return 0f;
                    float t = Mathf.InverseLerp(1.6f, 0.05f, ang);
                    float w = Mathf.Lerp(0.26f, 0.02f, t);
                    return Edge(w - Mathf.Abs(r - 0.95f), 0.05f);
                }
                case FxShape.Sucker:
                {
                    float ring = Edge(0.16f - Mathf.Abs(d - 0.56f), 0.06f);
                    float inner = Edge(0.2f - d, 0.06f) * 0.8f;
                    return Mathf.Max(ring, inner);
                }
                default:
                    return Edge(0.8f - d, 0.5f);
            }
        }

        /// <summary>Плавный край: 0 снаружи, 1 внутри, ширина перехода w.</summary>
        static float Edge(float inside, float w) => Mathf.Clamp01(inside / Mathf.Max(0.0001f, w));
    }
}
