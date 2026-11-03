using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Свет с поверхности: три слоя лучей разной ширины и каустики у верхней кромки.
    ///
    /// Правила для глаз здесь главные:
    /// * лучи не мерцают — у альфы нет шума, только «дыхание» с периодом 9–13 с
    ///   и размахом ±8 %;
    /// * лучи ползут вбок за 50–80 с на цикл, каустики — ещё медленнее;
    /// * в игровой колонке свет вдвое слабее, чем у края: вода за угрозами
    ///   обязана оставаться тёмной (см. <see cref="DepthLook"/>).
    ///
    /// Свет привязан к камере, а не к миру: он идёт сверху всегда, сколько бы
    /// пузырёк ни поднялся.
    /// </summary>
    public sealed class OceanLight
    {
        struct Shaft
        {
            public float Slot;        // −1…1, место по ширине
            public float Width;       // юнитов
            public float Alpha;       // пиковая альфа слоя
            public float DriftPeriod, BreathPeriod, Phase, Angle;
        }

        readonly Playfield _field;
        readonly QuadBatch _far, _near, _caustics;
        readonly Shaft[] _farShafts, _midShafts, _nearShafts;
        float _time;

        /// <summary>Размах «дыхания» лучей. Правило — не больше 10 %.</summary>
        public const float BreathAmplitude = 0.08f;
        /// <summary>Самый быстрый цикл дыхания, с. Правило — не быстрее 6–8 с.</summary>
        public const float BreathPeriodMin = 9f;
        /// <summary>Скорость каустик, доля плитки в секунду.</summary>
        public const float CausticSpeed = 0.012f;

        public OceanLight(Transform root, Playfield field)
        {
            _field = field;
            _far = new QuadBatch(root, "ShaftsFar", SpriteFactory.WorldMaterial, SortingOrders.ShaftsFar, 8);
            _near = new QuadBatch(root, "ShaftsNear", SpriteFactory.WorldMaterial, SortingOrders.LightShaft, 8);
            _caustics = new QuadBatch(root, "Caustics", SpriteFactory.CausticMaterial, SortingOrders.Caustics, 8);

            var rnd = new System.Random(2718);
            _farShafts = MakeShafts(rnd, 2, 2.6f, 3.6f, 0.030f);
            _midShafts = MakeShafts(rnd, 2, 1.3f, 2.0f, 0.040f);
            _nearShafts = MakeShafts(rnd, 3, 0.55f, 1.0f, 0.046f);
        }

        static Shaft[] MakeShafts(System.Random rnd, int count, float wMin, float wMax, float alpha)
        {
            var s = new Shaft[count];
            for (int i = 0; i < count; i++)
            {
                float slot = count > 1 ? Mathf.Lerp(-0.85f, 0.85f, i / (count - 1f)) : 0f;
                s[i] = new Shaft
                {
                    Slot = slot + (float)(rnd.NextDouble() - 0.5) * 0.25f,
                    Width = Mathf.Lerp(wMin, wMax, (float)rnd.NextDouble()),
                    Alpha = alpha,
                    DriftPeriod = Mathf.Lerp(50f, 80f, (float)rnd.NextDouble()),
                    BreathPeriod = Mathf.Lerp(BreathPeriodMin, 13f, (float)rnd.NextDouble()),
                    Phase = (float)rnd.NextDouble() * 10f,
                    Angle = Mathf.Lerp(-13f, -7f, (float)rnd.NextDouble()) * Mathf.Deg2Rad,
                };
            }
            return s;
        }

        public void Tick(float dt, DepthLook look, bool highDetail)
        {
            _time += dt;
            var piece = SpriteFactory.World(WorldPiece.Shaft);
            // Цвет луча — свет края, поднятый к белому, но под потолком «сумма ≤ 2.3».
            Color light = Palette.SafeLight(Color.Lerp(look.EdgeLight, Palette.Foam, 0.45f));

            _far.Clear();
            AddShafts(_far, _farShafts, piece, light, look.Shafts, 1.5f);
            AddShafts(_far, _midShafts, piece, light, look.Shafts, 1.35f);
            _far.Commit();

            _near.Clear();
            AddShafts(_near, _nearShafts, piece, light, look.Shafts, 1.25f);
            _near.Commit();

            _caustics.Visible = highDetail && look.Caustics > 0.01f;
            if (_caustics.Visible) TickCaustics(look);
        }

        void AddShafts(QuadBatch batch, Shaft[] shafts, AtlasPiece piece, Color light, float strength, float lengthK)
        {
            if (strength <= 0.001f) return;
            float hw = _field.HalfWidth;
            float height = (_field.Top - _field.Bottom) * lengthK;
            for (int i = 0; i < shafts.Length; i++)
            {
                var s = shafts[i];
                float drift = Mathf.Sin((_time / s.DriftPeriod + s.Phase) * Mathf.PI * 2f) * 0.12f;
                float x = (s.Slot + drift) * hw;
                float breathe = 1f + BreathAmplitude * Mathf.Sin((_time / s.BreathPeriod + s.Phase * 0.37f) * Mathf.PI * 2f);
                // в игровой колонке слабее: средние 55 % ширины — 30 % силы края,
                // к краю плавно до полной (контраст угроз к фону ≥ 3:1)
                float side = Mathf.Lerp(0.3f, 1f, Mathf.SmoothStep(0.55f, 1f, Mathf.Abs(x) / Mathf.Max(0.1f, hw)));
                float a = s.Alpha * strength * breathe * side;
                Vector2 centre = new Vector2(x, _field.Top - height * 0.5f + 0.6f);
                batch.Add(centre, new Vector2(s.Width * 0.5f, height * 0.5f), s.Angle, light.WithAlpha(a), piece.Uv);
            }
        }

        /// <summary>
        /// Каустики: две плитки световой сетки, ползущие навстречу друг другу,
        /// у верхней кромки. Вниз гаснут, в колонке вдвое тише края.
        /// </summary>
        void TickCaustics(DepthLook look)
        {
            float w = _field.HalfWidth + 0.3f;
            float t = _field.Top + 0.2f;
            float band = (_field.Top - _field.Bottom) * 0.32f;
            float b = t - band;
            float tile = 2.4f; // юнитов на плитку
            Color c = Palette.SafeLight(Color.Lerp(look.EdgeLight, Palette.Foam, 0.5f));
            float top = 0.07f * look.Caustics;

            _caustics.Clear();
            for (int layer = 0; layer < 2; layer++)
            {
                float dir = layer == 0 ? 1f : -0.8f;
                float ox = _time * CausticSpeed * dir, oy = _time * CausticSpeed * 0.6f;
                float scale = layer == 0 ? 1f : 1.37f;
                float u0 = -w / (tile * scale) + ox, u1 = w / (tile * scale) + ox;
                float v0 = b / (tile * scale) + oy, v1 = t / (tile * scale) + oy;
                float mid = w * 0.5f;
                // три полосы по ширине: у краёв ярче, в центре — 30 % края
                const float inner = 0.3f;
                for (int k = 0; k < 3; k++)
                {
                    float x0 = k == 0 ? -w : k == 1 ? -mid : mid;
                    float x1 = k == 0 ? -mid : k == 1 ? mid : w;
                    float k0 = k == 1 ? inner : 1f, k1 = k == 1 ? inner : 1f;
                    if (k == 0) k1 = inner;
                    if (k == 2) k0 = inner;
                    float uu0 = Mathf.Lerp(u0, u1, (x0 + w) / (2f * w)), uu1 = Mathf.Lerp(u0, u1, (x1 + w) / (2f * w));
                    _caustics.AddCorners(new Vector2(x0, b), new Vector2(x0, t), new Vector2(x1, t), new Vector2(x1, b),
                                         c.WithAlpha(0f), c.WithAlpha(top * k0), c.WithAlpha(top * k1), c.WithAlpha(0f),
                                         new Rect(uu0, v0, uu1 - uu0, v1 - v0));
                }
            }
            _caustics.Commit();
        }
    }
}
