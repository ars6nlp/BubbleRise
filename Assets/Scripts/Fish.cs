using UnityEngine;

namespace BubbleRise
{
    public enum FishType { Piranha, Lurker, Jellyfish }

    /// <summary>
    /// Рыба — активная угроза, в отличие от препятствий. Приплывает сбоку или снизу
    /// и плывёт к пузырьку, но с ограниченной скоростью поворота и «блужданием»:
    /// траектория получается кривой, рыба промахивается, разворачивается и заходит снова.
    ///
    /// Именно из-за них больше нельзя просто держать стража над пузырьком —
    /// угроза приходит с любой стороны.
    /// </summary>
    public class Fish : MonoBehaviour
    {
        public float Radius { get; private set; }
        public Vector2 Center => transform.position;
        public bool Dying { get; private set; }
        public bool Pooled { get; set; }

        /// <summary>Рендерер для накладки статуса: по нему берутся форма и слой.</summary>
        public SpriteRenderer Renderer => _sr;

        SpriteRenderer _sr;
        // Хвост (у медузы — щупальца): ребёнок с опорой у корня, ходит волной вслед
        // за телом. Рисуется под телом: тот же слой, чуть дальше по глубине.
        SpriteRenderer _tail;
        Color _tint = Color.white;
        Vector2 _velocity;
        float _speed;
        float _turnRate;
        float _wanderStrength;
        float _wanderSeed;
        float _phase;
        float _life;
        bool _leaving;
        bool _fleeing;
        Vector2 _fleeFrom;

        /// <summary>Уже уплывает: в атаку больше не идёт.</summary>
        public bool Leaving => _leaving;
        public Vector2 Velocity => _velocity;

        float _fade, _fadeTime, _spin;

        public void Setup()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Fish;

            var tail = new GameObject("Tail");
            tail.transform.SetParent(transform, false);
            _tail = tail.AddComponent<SpriteRenderer>();
            _tail.sharedMaterial = SpriteFactory.Unlit;
            _tail.sortingOrder = SortingOrders.Fish;
            _tail.enabled = false;
        }

        public FishType Type { get; private set; }

        public void Activate(FishType type, float radius, Vector2 position, Vector2 direction,
                             float speed, float turnRate, float wanderStrength, float lifetime)
        {
            Type = type;
            Radius = radius;
            Dying = false;
            Pooled = false;
            _leaving = false;
            _fleeing = false;
            _speed = speed;
            _turnRate = turnRate;
            _wanderStrength = wanderStrength;
            _wanderSeed = Random.Range(0f, 100f);
            _phase = Random.Range(0f, 10f);
            _life = lifetime;
            _fade = _fadeTime = 0f;

            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            _velocity = direction.normalized * speed;

            // Тело и хвост отдельно: хвост качается сам. Оттенок — один из трёх на
            // вид, от уже выпавшего _wanderSeed: лишний вызов Random сдвинул бы
            // последовательность для всего, что появляется следом.
            var tints = type == FishType.Piranha ? SpriteFactory.PiranhaTints
                      : type == FishType.Lurker ? SpriteFactory.LurkerTints : SpriteFactory.JellyTints;
            _tint = tints[(int)(_wanderSeed * 7.31f) % tints.Length];
            _sr.sprite = SpriteFactory.FishBody(type);
            _sr.color = _tint;
            _sr.flipY = false;

            bool jelly = type == FishType.Jellyfish;
            Vector2 root = jelly ? SpriteFactory.JellyTentacleRoot : SpriteFactory.FishTailRoot;
            float span = jelly ? SpriteFactory.JellyTentacleSpan : SpriteFactory.FishTailSpan;
            _tail.sprite = SpriteFactory.FishTail(type);
            _tail.color = _tint;
            _tail.flipY = false;
            _tail.enabled = true;
            // рисунок: 1 = Radius, локальная единица = диаметр; хвост чуть дальше — под телом
            _tail.transform.localPosition = new Vector3(root.x * 0.5f, root.y * 0.5f, 0.01f);
            _tail.transform.localRotation = Quaternion.identity;
            _tail.transform.localScale = Vector3.one * (span * 0.5f);

            transform.position = position;
            // спрайт нарисован в квадрате [-1..1], где 1 == Radius: масштаб = диаметр
            transform.localScale = Vector3.one * (radius * 2f);
            gameObject.SetActive(true);

            if (type == FishType.Jellyfish) transform.rotation = Quaternion.identity;
            else FaceVelocity();
        }

        /// <summary>Отбита стражем: закручивается, улетает и растворяется.</summary>
        public void Kill(Vector2 direction, float impulse)
        {
            Dying = true;
            // Гибнет целиком одним спрайтом: накладка статуса копирует спрайт цели,
            // и у горящей или замёрзшей рыбы хвост должен быть на месте.
            _sr.sprite = SpriteFactory.FishSprite(Type);
            _tail.enabled = false;
            _velocity = direction.normalized * impulse;
            _spin = Random.Range(-500f, 500f);
            _fade = _fadeTime = 0.55f;
        }

        /// <param name="target">Куда плыть — центр пузырька.</param>
        public bool Tick(float dt, Playfield field, Vector2 target)
        {
            if (Dying)
            {
                transform.position += (Vector3)(_velocity * dt);
                transform.Rotate(0f, 0f, _spin * dt);
                _fade -= dt;
                var col = _sr.color;
                col.a = Mathf.Clamp01(_fade / _fadeTime);
                _sr.color = col;
                return _fade > 0f;
            }

            _phase += dt;

            // Медуза не охотится: сносится вместе с миром, покачивается вбок и пульсирует
            // куполом. Она не поворачивается по движению — купол всегда смотрит вверх.
            if (Type == FishType.Jellyfish)
            {
                var jp = (Vector2)transform.position + _velocity * dt;
                jp.x += Mathf.Sin(_phase * 0.9f + _wanderSeed) * _wanderStrength * dt;
                transform.position = jp;

                float pulse = 1f + Mathf.Sin(_phase * 2.4f) * 0.11f;
                float baseSize = Radius * 2f;
                transform.localScale = new Vector3(baseSize / pulse, baseSize * pulse, 1f);

                // Щупальца отстают от купола: тянутся, когда он сжимается, и
                // медленно качаются вбок — цикл 3.7 с.
                float lag = Mathf.Sin(_phase * 2.4f - 1.2f);
                float half = SpriteFactory.JellyTentacleSpan * 0.5f;
                _tail.transform.localScale = new Vector3(half, half * (1f - lag * 0.07f), 1f);
                _tail.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * 1.7f + _wanderSeed) * 5f);

                float jellyMargin = 2.5f;
                return jp.y > field.Bottom - jellyMargin && jp.y < field.Top + jellyMargin &&
                       Mathf.Abs(jp.x) < field.HalfWidth + jellyMargin;
            }

            if (!_leaving)
            {
                _life -= dt;
                if (_life <= 0f) _leaving = true;
            }

            if (!_leaving)
            {
                Vector2 toTarget = target - (Vector2)transform.position;
                if (toTarget.sqrMagnitude > 0.0001f) toTarget.Normalize();
                else toTarget = _velocity.normalized;

                // блуждание: шум сбивает курс вбок, поэтому рыба идёт дугой, а не по прямой
                float noise = (Mathf.PerlinNoise(_wanderSeed, _phase * 0.85f) - 0.5f) * 2f;
                Vector2 side = new Vector2(-toTarget.y, toTarget.x);
                Vector2 desired = (toTarget + side * noise * _wanderStrength).normalized;

                float delta = Vector2.SignedAngle(_velocity, desired) * Mathf.Deg2Rad;
                float maxTurn = _turnRate * dt;
                delta = Mathf.Clamp(delta, -maxTurn, maxTurn);
                _velocity = Rotate(_velocity.normalized, delta) * _speed;
            }

            // Уплывает от точки: разворачивается с той же скоростью поворота,
            // а не разом — рыба не телепортирует курс.
            if (_fleeing)
            {
                Vector2 away = (Vector2)transform.position - _fleeFrom;
                if (away.sqrMagnitude < 0.0001f) away = Vector2.down;
                float turn = Vector2.SignedAngle(_velocity, away) * Mathf.Deg2Rad;
                float limit = _turnRate * 1.5f * dt;
                _velocity = Rotate(_velocity.normalized, Mathf.Clamp(turn, -limit, limit)) * _speed;
            }

            transform.position += (Vector3)(_velocity * dt);
            FaceVelocity();

            // Хвост идёт волной вслед за телом: та же частота виляния, отставание
            // на 1.3 рад и размах вдвое больше. Жабры «дышат» — 0.7 Гц, 1.8 % по высоте.
            _tail.flipY = _sr.flipY;
            _tail.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * 13f - 1.3f) * 16f);
            float size = Radius * 2f;
            transform.localScale = new Vector3(size, size * (1f + 0.018f * Mathf.Sin(_phase * 4.4f)), 1f);

            var p = transform.position;
            float margin = 2.5f;
            return p.y > field.Bottom - margin && p.y < field.Top + margin &&
                   Mathf.Abs(p.x) < field.HalfWidth + margin;
        }

        void FaceVelocity()
        {
            float deg = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            bool facingLeft = Mathf.Abs(Mathf.DeltaAngle(deg, 0f)) > 90f;
            _sr.flipY = facingLeft;
            // лёгкое виляние телом
            transform.rotation = Quaternion.Euler(0f, 0f, deg + Mathf.Sin(_phase * 13f) * 7f);
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float c = Mathf.Cos(radians), s = Mathf.Sin(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>
        /// Уплыть от точки и не атаковать. Нужен «Чернильному мороку»: рыба,
        /// дошедшая до пузырька в темноте, бьёт из того, чего не видно (C-13).
        /// </summary>
        public void Flee(Vector2 from)
        {
            if (Dying || Type == FishType.Jellyfish) return;
            _leaving = true;
            _fleeing = true;
            _fleeFrom = from;
        }

        public void Deactivate() => gameObject.SetActive(false);
    }
}
