using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Живая вода: морской снег в два слоя и пузырьки со дна.
    ///
    /// * Дальний снег мельче, медленнее и тусклее, ближний — крупнее и быстрее:
    ///   параллакс сам рассказывает о толще воды.
    /// * Хлопья не мерцают: появляются за 0.6 с и гаснут за 0.6 с, между этим
    ///   альфа постоянна. Прежний планктон «подмигивал» до 0.8 раза в секунду
    ///   цветом стража — теперь снег холодный и спокойный.
    /// * Пузырьки поднимаются струйками у краёв и по одному, в бирюзовой полосе
    ///   у поверхности гаснут, будто лопнули.
    ///
    /// Два меша — два вызова отрисовки на сотню частиц.
    /// </summary>
    public sealed class MarineSnow
    {
        struct Flake
        {
            public Vector2 Pos;
            public float Size, Parallax, Sink, Drift, Phase, Age, Life, Alpha;
        }

        struct Bubble
        {
            public Vector2 Pos;
            public float Size, Rise, Phase, Age, Life;
            public bool Alive;
        }

        readonly Playfield _field;
        readonly QuadBatch _far, _near;
        readonly Flake[] _farFlakes, _nearFlakes;
        readonly Bubble[] _bubbles;
        readonly System.Random _rnd = new System.Random(1618);
        float _streamTimer = 2f, _singleTimer = 1f;

        /// <summary>Сколько хлопьев в каждом слое при «высоких» деталях; «обычные» — половина.</summary>
        public const int FarCount = 44, NearCount = 28, BubbleCount = 40;
        /// <summary>Появление и угасание частицы, с — не быстрее правила 0.15 с.</summary>
        public const float Fade = 0.6f;

        public MarineSnow(Transform root, Playfield field)
        {
            _field = field;
            _far = new QuadBatch(root, "SnowFar", SpriteFactory.WorldMaterial, SortingOrders.SnowFar, FarCount);
            _near = new QuadBatch(root, "SnowNear", SpriteFactory.WorldMaterial, SortingOrders.SnowNear, NearCount + BubbleCount);
            _farFlakes = new Flake[FarCount];
            _nearFlakes = new Flake[NearCount];
            _bubbles = new Bubble[BubbleCount];
            for (int i = 0; i < FarCount; i++) Respawn(ref _farFlakes[i], true, false);
            for (int i = 0; i < NearCount; i++) Respawn(ref _nearFlakes[i], true, true);
        }

        float R() => (float)_rnd.NextDouble();
        float R(float a, float b) => a + (b - a) * (float)_rnd.NextDouble();

        void Respawn(ref Flake f, bool anywhere, bool near)
        {
            f.Parallax = near ? R(0.8f, 1.05f) : R(0.25f, 0.45f);
            f.Size = near ? R(0.035f, 0.06f) : R(0.018f, 0.032f);
            f.Sink = R(0.04f, 0.12f);               // своё медленное падение в воде
            f.Drift = R(0.05f, 0.14f);
            f.Phase = R(0f, 6.28f);
            f.Alpha = near ? R(0.2f, 0.3f) : R(0.14f, 0.22f);
            f.Life = R(9f, 18f);
            f.Age = anywhere ? R(0f, f.Life) : 0f;
            float x = R(-_field.HalfWidth - 0.2f, _field.HalfWidth + 0.2f);
            float y = anywhere ? R(_field.Bottom, _field.Top) : _field.Top + 0.3f;
            f.Pos = new Vector2(x, y);
        }

        public void Tick(float dt, float scrollSpeed, DepthLook look, bool highDetail, float time)
        {
            var flake = SpriteFactory.World(WorldPiece.Flake);
            var ring = SpriteFactory.World(WorldPiece.BubbleRing);
            Color tint = look.SnowTint;

            int farN = highDetail ? FarCount : FarCount / 2;
            int nearN = highDetail ? NearCount : NearCount / 2;
            float density = Mathf.Clamp01(look.Snow);

            _far.Clear();
            for (int i = 0; i < FarCount; i++) StepFlake(ref _farFlakes[i], dt, scrollSpeed, time, false);
            for (int i = 0; i < farN; i++) DrawFlake(_far, _farFlakes[i], flake, tint, density);
            _far.Commit();

            _near.Clear();
            for (int i = 0; i < NearCount; i++) StepFlake(ref _nearFlakes[i], dt, scrollSpeed, time, true);
            for (int i = 0; i < nearN; i++) DrawFlake(_near, _nearFlakes[i], flake, tint, density);
            TickBubbles(dt, scrollSpeed, look, ring, highDetail);
            _near.Commit();
        }

        void StepFlake(ref Flake f, float dt, float scrollSpeed, float time, bool near)
        {
            f.Age += dt;
            // мир уходит вниз, хлопья тонут — оба движения вниз, но хлопья ещё и колышутся
            f.Pos.y -= (scrollSpeed * f.Parallax + f.Sink) * dt;
            f.Pos.x += Mathf.Sin(time * 0.35f + f.Phase) * f.Drift * dt;
            if (f.Age > f.Life || f.Pos.y < _field.Bottom - 0.3f) Respawn(ref f, false, near);
        }

        void DrawFlake(QuadBatch batch, Flake f, AtlasPiece piece, Color tint, float density)
        {
            float fade = Mathf.Min(Mathf.Clamp01(f.Age / Fade), Mathf.Clamp01((f.Life - f.Age) / Fade));
            float a = f.Alpha * fade * (0.55f + 0.45f * density);
            if (a <= 0.004f) return;
            batch.Add(f.Pos, new Vector2(f.Size, f.Size), 0f, tint.WithAlpha(a), piece.Uv);
        }

        // ------------------------------------------------------------ пузырьки

        void TickBubbles(float dt, float scrollSpeed, DepthLook look, AtlasPiece piece, bool highDetail)
        {
            _streamTimer -= dt;
            _singleTimer -= dt;
            if (_streamTimer <= 0f)
            {
                _streamTimer = R(5f, 11f) * (highDetail ? 1f : 1.8f);
                // струйка у края: от декора на дне, не в игровой колонке
                float side = R() < 0.5f ? -1f : 1f;
                float x = side * R(_field.HalfWidth * 0.78f, _field.HalfWidth - 0.15f);
                int n = highDetail ? 9 : 5;
                for (int i = 0; i < n; i++) Emit(new Vector2(x + R(-0.08f, 0.08f), _field.Bottom - 0.2f - i * 0.45f), R(0.06f, 0.12f));
            }
            if (_singleTimer <= 0f)
            {
                _singleTimer = R(1.2f, 2.6f) * (highDetail ? 1f : 1.8f);
                Emit(new Vector2(R(-_field.HalfWidth, _field.HalfWidth), _field.Bottom - 0.2f), R(0.04f, 0.08f));
            }

            float pop = _field.Top - (_field.Top - _field.Bottom) * 0.12f * Mathf.Clamp01(look.TopGlow);
            Color c = Palette.SafeLight(Color.Lerp(look.SnowTint, Palette.Foam, 0.5f));
            for (int i = 0; i < _bubbles.Length; i++)
            {
                ref var b = ref _bubbles[i];
                if (!b.Alive) continue;
                b.Age += dt;
                // пузырёк всплывает быстрее, чем мир уходит вниз: на экране он идёт вверх
                b.Pos.y += (b.Rise - scrollSpeed * 0.55f) * dt;
                b.Pos.x += Mathf.Sin(b.Age * 2.3f + b.Phase) * 0.12f * dt;
                float fadeIn = Mathf.Clamp01(b.Age / 0.4f);
                float fadeOut = Mathf.Clamp01((pop + 0.4f - b.Pos.y) / 0.8f);       // у кромки «лопается»
                float a = 0.42f * fadeIn * fadeOut;
                if (b.Pos.y > _field.Top + 0.3f || b.Pos.y < _field.Bottom - 6f || b.Age > b.Life || fadeOut <= 0f)
                {
                    b.Alive = false;
                    continue;
                }
                _near.Add(b.Pos, new Vector2(b.Size, b.Size), 0f, c.WithAlpha(a), piece.Uv);
            }
        }

        void Emit(Vector2 at, float size)
        {
            for (int i = 0; i < _bubbles.Length; i++)
            {
                if (_bubbles[i].Alive) continue;
                _bubbles[i] = new Bubble
                {
                    Pos = at, Size = size, Rise = R(1.6f, 2.4f) + size * 6f, Phase = R(0f, 6.28f),
                    Age = 0f, Life = 14f, Alive = true,
                };
                return;
            }
        }
    }
}
