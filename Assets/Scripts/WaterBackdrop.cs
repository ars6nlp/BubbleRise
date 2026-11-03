using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Толща воды: градиент по высоте экрана, чуть светлее у краёв и у верхней
    /// кромки, и мягкая виньетка в углах.
    ///
    /// Всё — цветами вершин в двух мешах, без текстуры и без перезаливки
    /// текстуры каждый кадр (прежде градиент жил в текстуре 1×64 и заливался
    /// заново при каждом изменении глубины).
    ///
    /// Колонка, где летят угрозы, держится тёмной: свет у краёв только на
    /// внешних 14 % ширины, а на телефоне, где игровая зона во всю ширину,
    /// ещё и вдвое слабее.
    /// </summary>
    public sealed class WaterBackdrop
    {
        readonly Playfield _field;
        readonly QuadBatch _water;
        readonly QuadBatch _vignette;

        static readonly Rect White = new Rect(0f, 0f, 1f, 1f);
        static readonly float[] EdgeLift = { 0.35f, 0.55f, 0.8f, 1f };
        readonly float[] _ys = new float[4];
        readonly Color[] _centre = new Color[4];

        public WaterBackdrop(Transform root, Playfield field)
        {
            _field = field;
            _water = new QuadBatch(root, "Water", SpriteFactory.VertexColorUnlit, SortingOrders.Background, 16);
            _vignette = new QuadBatch(root, "Vignette", SpriteFactory.VertexColorUnlit, SortingOrders.WorldVignette, 16);
        }

        /// <param name="themeBlend">Насколько подмешан цвет воды локации, 0…1.</param>
        public void Tick(DepthLook look, Color themeTop, Color themeBottom, float themeBlend)
        {
            float w = _field.HalfWidth + 0.3f;
            float b = _field.Bottom - 0.3f, t = _field.Top + 0.3f;
            float h = t - b;

            Color top = look.WaterTop, mid = look.WaterMid, bottom = look.WaterBottom;
            if (themeBlend > 0f)
            {
                top = Color.Lerp(top, themeTop, themeBlend);
                mid = Color.Lerp(mid, Color.Lerp(themeTop, themeBottom, 0.5f), themeBlend);
                bottom = Color.Lerp(bottom, themeBottom, themeBlend);
            }

            // На широком экране боковые полосы вне игры — им можно больше света.
            bool sideBand = _field.HalfWidth > _field.PlayHalfWidth + 0.6f;
            float edgeK = sideBand ? 0.32f : 0.14f;
            float edgeX = sideBand ? _field.PlayHalfWidth + 0.2f : _field.HalfWidth * 0.72f;

            _ys[0] = b; _ys[1] = b + h * 0.45f; _ys[2] = t - h * 0.16f; _ys[3] = t;
            _centre[0] = bottom; _centre[1] = mid; _centre[2] = top;
            _centre[3] = Color.Lerp(top, look.EdgeLight, 0.14f * look.TopGlow);   // верхняя кромка — к свету

            _water.Clear();
            for (int row = 0; row < 3; row++)
            {
                Color c0 = _centre[row], c1 = _centre[row + 1];
                Color e0 = Color.Lerp(c0, look.EdgeLight, edgeK * EdgeLift[row] * (0.6f + 0.4f * look.TopGlow));
                Color e1 = Color.Lerp(c1, look.EdgeLight, edgeK * EdgeLift[row + 1] * (0.6f + 0.4f * look.TopGlow));
                float y0 = _ys[row], y1 = _ys[row + 1];
                // левый край → центр → правый край
                _water.AddCorners(new Vector2(-w, y0), new Vector2(-w, y1), new Vector2(-edgeX, y1), new Vector2(-edgeX, y0),
                                  e0, e1, c1, c0, White);
                _water.AddCorners(new Vector2(-edgeX, y0), new Vector2(-edgeX, y1), new Vector2(edgeX, y1), new Vector2(edgeX, y0),
                                  c0, c1, c1, c0, White);
                _water.AddCorners(new Vector2(edgeX, y0), new Vector2(edgeX, y1), new Vector2(w, y1), new Vector2(w, y0),
                                  c0, c1, e1, e0, White);
            }
            _water.Commit();

            // Виньетка: тёмные углы, середина и игровая колонка нетронуты.
            Color dark = Color.Lerp(bottom, Color.black, 0.5f);
            Color clear = dark.WithAlpha(0f);
            Color corner = dark.WithAlpha(0.34f);
            Color side = dark.WithAlpha(0.12f);
            float ix = w * 0.62f, iyT = t - h * 0.2f, iyB = b + h * 0.2f;
            _vignette.Clear();
            // четыре угла и четыре края по рамке
            _vignette.AddCorners(new Vector2(-w, b), new Vector2(-w, iyB), new Vector2(-ix, iyB), new Vector2(-ix, b), corner, side, clear, side, White);
            _vignette.AddCorners(new Vector2(ix, b), new Vector2(ix, iyB), new Vector2(w, iyB), new Vector2(w, b), side, clear, side, corner, White);
            _vignette.AddCorners(new Vector2(-w, iyT), new Vector2(-w, t), new Vector2(-ix, t), new Vector2(-ix, iyT), side, corner, side, clear, White);
            _vignette.AddCorners(new Vector2(ix, iyT), new Vector2(ix, t), new Vector2(w, t), new Vector2(w, iyT), clear, side, corner, side, White);
            _vignette.AddCorners(new Vector2(-w, iyB), new Vector2(-w, iyT), new Vector2(-ix, iyT), new Vector2(-ix, iyB), side, side, clear, clear, White);
            _vignette.AddCorners(new Vector2(ix, iyB), new Vector2(ix, iyT), new Vector2(w, iyT), new Vector2(w, iyB), clear, clear, side, side, White);
            _vignette.Commit();
        }
    }
}
