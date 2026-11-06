using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Холст мини-босса: фигуры кладутся слоями, каждая — только в своей рамке.
    ///
    /// Почему не <c>SpriteFactory.Render</c>: тот на каждую точку зовёт всю функцию
    /// рисунка девять раз (суперсэмплинг 3×3). У мини-босса полсотни фигур, и так
    /// одна текстура 256² рисовалась бы около секунды. Здесь фигура считается
    /// только внутри своей рамки и один раз на точку, а край сглаживается по
    /// расстоянию до контура (поле расстояний), — те же края в десятки раз дешевле.
    ///
    /// Координаты — как везде в SpriteFactory: [-1, 1] по обеим осям, y вверх,
    /// (0, 0) — центр круга игрока, радиус 1 — его габарит.
    /// </summary>
    public sealed class MiniBossCanvas
    {
        public readonly int Size;
        readonly Color[] _px;
        /// <summary>Размер точки текстуры в единицах холста.</summary>
        public readonly float Pixel;

        public MiniBossCanvas(int size)
        {
            Size = size;
            _px = new Color[size * size];
            Pixel = 2f / size;
        }

        public Color[] Pixels => _px;

        /// <summary>Залить фигуру <paramref name="sdf"/> (меньше нуля — внутри) цветом <paramref name="paint"/>(точка, расстояние).</summary>
        public void Fill(Rect box, Func<Vector2, float> sdf, Func<Vector2, float, Color> paint, float soft = 0f)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt((box.xMin + 1f) / Pixel) - 1);
            int x1 = Mathf.Min(Size - 1, Mathf.CeilToInt((box.xMax + 1f) / Pixel) + 1);
            int y0 = Mathf.Max(0, Mathf.FloorToInt((box.yMin + 1f) / Pixel) - 1);
            int y1 = Mathf.Min(Size - 1, Mathf.CeilToInt((box.yMax + 1f) / Pixel) + 1);
            float width = Pixel + 2f * soft;
            for (int y = y0; y <= y1; y++)
            {
                float v = (y + 0.5f) * Pixel - 1f;
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2((x + 0.5f) * Pixel - 1f, v);
                    float d = sdf(p);
                    float cover = 0.5f - d / width;
                    if (cover <= 0f) continue;
                    if (cover > 1f) cover = 1f;
                    Color c = paint(p, d);
                    Over(y * Size + x, c, c.a * cover);
                }
            }
        }

        public void Fill(Rect box, Func<Vector2, float> sdf, Color color, float soft = 0f)
            => Fill(box, sdf, (p, d) => color, soft);

        /// <summary>Положить цвет поверх точки: обычное «поверх» в прямой альфе.</summary>
        void Over(int i, Color c, float a)
        {
            if (a <= 0f) return;
            var dst = _px[i];
            float outA = a + dst.a * (1f - a);
            if (outA <= 1e-5f) return;
            float kd = dst.a * (1f - a) / outA, ks = a / outA;
            _px[i] = new Color(c.r * ks + dst.r * kd, c.g * ks + dst.g * kd, c.b * ks + dst.b * kd, outA);
        }

        /// <summary>
        /// Обводка снаружи силуэта толщиной <paramref name="width"/> (в единицах холста):
        /// ложится ПОД рисунок, поэтому видна только за его краем.
        /// </summary>
        public void OutlineUnder(Color color, float width)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(width / Pixel));
            var alpha = new float[_px.Length];
            for (int i = 0; i < _px.Length; i++) alpha[i] = _px[i].a;

            // максимум альфы в окне — сначала по строкам, потом по столбцам
            var tmp = new float[_px.Length];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float m = 0f;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = x + k;
                        if (xx >= 0 && xx < Size && alpha[y * Size + xx] > m) m = alpha[y * Size + xx];
                    }
                    tmp[y * Size + x] = m;
                }
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    float m = 0f;
                    for (int k = -r; k <= r; k++)
                    {
                        int yy = y + k;
                        if (yy >= 0 && yy < Size && tmp[yy * Size + x] > m) m = tmp[yy * Size + x];
                    }
                    int i = y * Size + x;
                    float under = m * color.a;
                    if (under <= 0f) continue;
                    // рисунок поверх обводки
                    var top = _px[i];
                    float outA = top.a + under * (1f - top.a);
                    if (outA <= 1e-5f) continue;
                    float kt = top.a / outA, ku = under * (1f - top.a) / outA;
                    _px[i] = new Color(top.r * kt + color.r * ku, top.g * kt + color.g * ku, top.b * kt + color.b * ku, outA);
                }
        }

        /// <summary>Без белого пересвета: сумма каналов не выше <paramref name="sum"/>, оттенок тот же.</summary>
        public void Limit(float sum)
        {
            for (int i = 0; i < _px.Length; i++)
            {
                var c = _px[i];
                float s = c.r + c.g + c.b;
                if (s <= sum) continue;
                float k = sum / s;
                _px[i] = new Color(c.r * k, c.g * k, c.b * k, c.a);
            }
        }

        /// <summary>
        /// Растянуть цвет края на прозрачные точки рядом. Альфа не меняется: это
        /// только против тёмной каймы, которую даёт билинейная выборка на границе
        /// с прозрачным чёрным.
        /// </summary>
        public void Bleed(int passes = 2)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var src = (Color[])_px.Clone();
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        int i = y * Size + x;
                        if (src[i].a > 0.02f) continue;
                        float r = 0f, g = 0f, b = 0f, w = 0f;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                int xx = x + dx, yy = y + dy;
                                if (xx < 0 || yy < 0 || xx >= Size || yy >= Size) continue;
                                var c = src[yy * Size + xx];
                                float k = c.a > 0.02f ? c.a : (c.r + c.g + c.b > 0f ? 0.05f : 0f);
                                r += c.r * k; g += c.g * k; b += c.b * k; w += k;
                            }
                        if (w > 0f) _px[i] = new Color(r / w, g / w, b / w, src[i].a);
                    }
            }
        }
    }

    /// <summary>Поля расстояний для мини-боссов (меньше нуля — внутри фигуры).</summary>
    public static class Sdf
    {
        public static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        /// <summary>Эллипс, повёрнутый на <paramref name="deg"/>. Приближение Квилеса: на краю точное.</summary>
        public static float Ellipse(Vector2 p, Vector2 c, float rx, float ry, float deg = 0f)
        {
            var q = Rot(p - c, -deg);
            float k0 = new Vector2(q.x / rx, q.y / ry).magnitude;
            float k1 = new Vector2(q.x / (rx * rx), q.y / (ry * ry)).magnitude;
            return k1 < 1e-6f ? -Mathf.Min(rx, ry) : k0 * (k0 - 1f) / k1;
        }

        public static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Mathf.Max(1e-8f, ba.sqrMagnitude));
            return (pa - ba * h).magnitude;
        }

        public static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r) => Segment(p, a, b) - r;

        /// <summary>Капсула с разными радиусами концов (конус со скруглёнными торцами).</summary>
        public static float Taper(Vector2 p, Vector2 a, Vector2 b, float ra, float rb)
        {
            Vector2 ba = b - a;
            float l2 = Vector2.Dot(ba, ba);
            if (l2 < 1e-8f) return (p - a).magnitude - Mathf.Max(ra, rb);
            float rr = ra - rb;
            float a2 = l2 - rr * rr;
            float il2 = 1f / l2;
            Vector2 pa = p - a;
            float y = Vector2.Dot(pa, ba);
            float z = y - l2;
            Vector2 xv = pa * l2 - ba * y;
            float x2 = Vector2.Dot(xv, xv);
            float y2 = y * y * l2;
            float z2 = z * z * l2;
            float k = Mathf.Sign(rr) * rr * rr * x2;
            if (Mathf.Sign(z) * a2 * z2 > k) return Mathf.Sqrt(x2 + z2) * il2 - rb;
            if (Mathf.Sign(y) * a2 * y2 < k) return Mathf.Sqrt(x2 + y2) * il2 - ra;
            return (Mathf.Sqrt(Mathf.Max(0f, x2 * a2 * il2)) + y * rr) * il2 - ra;
        }

        /// <summary>Цепочка сужающихся звеньев: щупальце, хвост, шея змея.</summary>
        public static float Chain(Vector2 p, Vector2[] pts, float r0, float r1)
        {
            float d = float.MaxValue;
            int n = pts.Length - 1;
            for (int i = 0; i < n; i++)
            {
                float ra = Mathf.Lerp(r0, r1, (float)i / n), rb = Mathf.Lerp(r0, r1, (float)(i + 1) / n);
                d = Mathf.Min(d, Taper(p, pts[i], pts[i + 1], ra, rb));
            }
            return d;
        }

        public static float Box(Vector2 p, Vector2 c, Vector2 half, float round = 0f, float deg = 0f)
        {
            var q = Rot(p - c, -deg);
            var d = new Vector2(Mathf.Abs(q.x) - half.x + round, Mathf.Abs(q.y) - half.y + round);
            return new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - round;
        }

        /// <summary>Треугольник (Квилес).</summary>
        public static float Triangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            Vector2 e0 = b - a, e1 = c - b, e2 = a - c;
            Vector2 v0 = p - a, v1 = p - b, v2 = p - c;
            Vector2 pq0 = v0 - e0 * Mathf.Clamp01(Vector2.Dot(v0, e0) / Vector2.Dot(e0, e0));
            Vector2 pq1 = v1 - e1 * Mathf.Clamp01(Vector2.Dot(v1, e1) / Vector2.Dot(e1, e1));
            Vector2 pq2 = v2 - e2 * Mathf.Clamp01(Vector2.Dot(v2, e2) / Vector2.Dot(e2, e2));
            float s = Mathf.Sign(e0.x * e2.y - e0.y * e2.x);
            float dx = Mathf.Min(Mathf.Min(pq0.sqrMagnitude, pq1.sqrMagnitude), pq2.sqrMagnitude);
            float dy = Mathf.Min(Mathf.Min(s * (v0.x * e0.y - v0.y * e0.x), s * (v1.x * e1.y - v1.y * e1.x)),
                                 s * (v2.x * e2.y - v2.y * e2.x));
            return -Mathf.Sqrt(dx) * Mathf.Sign(dy);
        }

        /// <summary>Мягкое объединение: шов между фигурами скруглён на <paramref name="k"/>.</summary>
        public static float SMin(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        public static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Рамка вокруг точки.</summary>
        public static Rect Box(float cx, float cy, float hw, float hh) => new Rect(cx - hw, cy - hh, hw * 2f, hh * 2f);
        public static Rect Around(Vector2 a, Vector2 b, float pad)
            => Rect.MinMaxRect(Mathf.Min(a.x, b.x) - pad, Mathf.Min(a.y, b.y) - pad, Mathf.Max(a.x, b.x) + pad, Mathf.Max(a.y, b.y) + pad);
        public static Rect Around(Vector2[] pts, float pad)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var q in pts) { x0 = Mathf.Min(x0, q.x); y0 = Mathf.Min(y0, q.y); x1 = Mathf.Max(x1, q.x); y1 = Mathf.Max(y1, q.y); }
            return Rect.MinMaxRect(x0 - pad, y0 - pad, x1 + pad, y1 + pad);
        }
        public static readonly Rect All = new Rect(-1f, -1f, 2f, 2f);

        public static float Sat(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
        public static float Smooth(float a, float b, float x) { float t = Sat((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        /// <summary>Целочисленный хеш → 0…1.</summary>
        public static float Hash(float x, float y)
        {
            unchecked
            {
                uint h = (uint)(int)(x * 1597f) * 0x27d4eb2dU ^ (uint)(int)(y * 2531f) * 0x165667b1U;
                h ^= h >> 15; h *= 0x85ebca6bU; h ^= h >> 13; h *= 0xc2b2ae35U; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        public static float Noise(float x, float y)
        {
            float ix = Mathf.Floor(x), iy = Mathf.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float a = Hash(ix, iy), b = Hash(ix + 1f, iy), c = Hash(ix, iy + 1f), d = Hash(ix + 1f, iy + 1f);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float x, float y, int octaves = 3)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, y) * amp;
                norm += amp;
                x = x * 2.03f + 17.1f; y = y * 2.03f + 9.7f;
                amp *= 0.5f;
            }
            return sum / norm;
        }
    }
}
