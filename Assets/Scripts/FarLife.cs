using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Далёкая жизнь: косяк мелких рыбок, скат, бледные медузы, изредка кит.
    ///
    /// Должно быть ясно с первого взгляда, что это декор, а не угроза:
    /// * силуэты тонут в тумане слоя (60–75 % цвета тумана) и полупрозрачны;
    /// * формы не повторяют врагов — у рыбок нет пасти и красного брюха, как у
    ///   пираний, медузы втрое мельче и бледнее розовой медузы-угрозы;
    /// * движутся медленно (0.4–1.1 юнита/с) и всегда за средним планом;
    /// * столкновений нет — это просто рисунок в меше фона.
    /// Прежняя стайка была спрайтом вражеской пираньи, только затемнённым.
    /// </summary>
    public sealed class FarLife
    {
        enum Kind { None, School, Ray, Jellies, Whale }

        readonly Playfield _field;
        readonly QuadBatch _batch;
        readonly System.Random _rnd = new System.Random(4242);

        Kind _kind;
        float _timer = 4f, _age, _x, _y, _speed, _size;
        int _count;
        readonly Vector2[] _offsets = new Vector2[18];
        readonly float[] _phases = new float[18];

        public FarLife(Transform root, Playfield field)
        {
            _field = field;
            _batch = new QuadBatch(root, "FarLife", SpriteFactory.WorldMaterial, SortingOrders.FarLife, 20);
        }

        float R(float a, float b) => a + (b - a) * (float)_rnd.NextDouble();

        public void Tick(float dt, float scrollSpeed, DepthLook look, bool highDetail)
        {
            _batch.Clear();
            if (_kind == Kind.None)
            {
                _timer -= dt;
                if (_timer <= 0f) Start(look);
                _batch.Commit();
                return;
            }

            _age += dt;
            _x += _speed * dt;
            _y -= scrollSpeed * 0.18f * dt;          // дальний план почти не сносится
            float fadeIn = Mathf.Clamp01(_age / 2.5f);
            Color tint = Color.Lerp(Palette.Hex("9AAAB8"), look.Fog, 0.68f);
            bool right = _speed > 0f;

            switch (_kind)
            {
                case Kind.School:
                {
                    var p = SpriteFactory.World(WorldPiece.SchoolFish);
                    int n = highDetail ? _count : _count / 2;
                    for (int i = 0; i < n; i++)
                    {
                        // строй косяка дышит: рыбки чуть сходятся и расходятся
                        Vector2 o = _offsets[i] * (1f + 0.08f * Mathf.Sin(_age * 0.7f + _phases[i]));
                        Vector2 at = new Vector2(_x + o.x * (right ? -1f : 1f), _y + o.y + Mathf.Sin(_age * 1.6f + _phases[i]) * 0.05f);
                        _batch.Add(at, new Vector2(_size * 0.5f, _size * 0.5f / p.Aspect), 0f,
                                   tint.WithAlpha(0.42f * fadeIn), p.Uv, flipX: !right);
                    }
                    break;
                }
                case Kind.Ray:
                {
                    var p = SpriteFactory.World(WorldPiece.Ray);
                    // крылья — медленный взмах через высоту, раз в ~3.5 с
                    float flap = 1f + 0.12f * Mathf.Sin(_age * 1.8f);
                    _batch.Add(new Vector2(_x, _y + Mathf.Sin(_age * 0.5f) * 0.3f),
                               new Vector2(_size * 0.5f, _size * 0.5f / p.Aspect * flap),
                               (right ? -1f : 1f) * 0.12f, tint.WithAlpha(0.5f * fadeIn), p.Uv, flipX: !right);
                    break;
                }
                case Kind.Jellies:
                {
                    var p = SpriteFactory.World(WorldPiece.Jelly);
                    for (int i = 0; i < _count; i++)
                    {
                        // пульс купола — раз в 3–4 с, по размеру, а не по яркости
                        float pulse = 1f + 0.07f * Mathf.Sin(_age * 1.7f + _phases[i]);
                        Vector2 at = new Vector2(_x + _offsets[i].x, _y + _offsets[i].y + Mathf.Sin(_age * 0.4f + _phases[i]) * 0.25f);
                        _batch.Add(at, new Vector2(_size * 0.5f * pulse, _size * 0.5f / p.Aspect), 0f,
                                   Color.Lerp(Palette.Hex("B8C6D4"), look.Fog, 0.55f).WithAlpha(0.45f * fadeIn), p.Uv);
                    }
                    break;
                }
                default:
                {
                    var p = SpriteFactory.World(WorldPiece.Whale);
                    _batch.Add(new Vector2(_x, _y + Mathf.Sin(_age * 0.3f) * 0.35f),
                               new Vector2(_size * 0.5f, _size * 0.5f / p.Aspect),
                               Mathf.Sin(_age * 0.35f) * 0.04f, tint.WithAlpha(0.32f * fadeIn), p.Uv, flipX: !right);
                    break;
                }
            }
            _batch.Commit();

            float span = _field.HalfWidth + _size * 2f + 2f;
            if (Mathf.Abs(_x) > span || _y < _field.Bottom - 3f)
            {
                _kind = Kind.None;
                _timer = R(10f, 22f);
            }
        }

        void Start(DepthLook look)
        {
            float roll = (float)_rnd.NextDouble();
            // в бездне — медузы, у поверхности — косяки; кит редок везде
            float jellyChance = Mathf.Lerp(0.2f, 0.5f, look.Hum);
            _kind = roll < 0.08f ? Kind.Whale : roll < 0.08f + jellyChance ? Kind.Jellies
                  : roll < 0.08f + jellyChance + 0.18f ? Kind.Ray : Kind.School;

            bool fromLeft = _rnd.NextDouble() < 0.5;
            _age = 0f;
            switch (_kind)
            {
                case Kind.School: _size = R(0.22f, 0.3f); _speed = R(0.8f, 1.1f); _count = 14; break;
                case Kind.Ray: _size = R(1.2f, 1.7f); _speed = R(0.45f, 0.7f); _count = 1; break;
                case Kind.Jellies: _size = R(0.3f, 0.42f); _speed = R(0.18f, 0.3f); _count = 4; break;
                default: _size = R(5f, 7f); _speed = R(0.4f, 0.6f); _count = 1; break;
            }
            for (int i = 0; i < _offsets.Length; i++)
            {
                _offsets[i] = _kind == Kind.Jellies
                    ? new Vector2(R(-1.4f, 1.4f), R(-1.2f, 1.2f))
                    : new Vector2((i % 7) * 0.32f + R(-0.06f, 0.06f), (i / 7 - 0.5f) * 0.28f + R(-0.08f, 0.08f));
                _phases[i] = R(0f, 6.28f);
            }
            float span = _field.HalfWidth + _size + 1.5f;
            _x = fromLeft ? -span : span;
            _speed *= fromLeft ? 1f : -1f;
            _y = R(_field.Bottom + 2f, _field.Top - 1.5f);
        }
    }
}
