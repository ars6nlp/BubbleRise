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
        Vector2 _velocity;
        float _speed;
        float _turnRate;
        float _wanderStrength;
        float _wanderSeed;
        float _phase;
        float _life;
        bool _leaving;

        float _fade, _fadeTime, _spin;

        public void Setup()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Fish;
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
            _speed = speed;
            _turnRate = turnRate;
            _wanderStrength = wanderStrength;
            _wanderSeed = Random.Range(0f, 100f);
            _phase = Random.Range(0f, 10f);
            _life = lifetime;
            _fade = _fadeTime = 0f;

            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            _velocity = direction.normalized * speed;

            _sr.sprite = SpriteFactory.FishSprite(type);
            _sr.color = Color.white;
            _sr.flipY = false;

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

            transform.position += (Vector3)(_velocity * dt);
            FaceVelocity();

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

        public void Deactivate() => gameObject.SetActive(false);
    }
}
