using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Разряд молнии грозовой медузы. Летит в пузырёк; если его перехватил страж —
    /// меняет цвет на электрический и уходит обратно в босса.
    ///
    /// Враждебная молния коралловая, отражённая — цвета самой медузы: опасное
    /// в игре всегда коралловое, так что цвет читается без раздумий.
    /// </summary>
    public class BossProjectile : MonoBehaviour
    {
        public float Radius { get; private set; }
        public bool Reflected { get; private set; }
        /// <summary>Текущая скорость — нужна для зеркального отскока от стража.</summary>
        public Vector2 Velocity => _velocity;
        /// <summary>Сколько снаряд уже летит. Доворачивающий снаряд может кружить, и
        /// возраст — единственный надёжный способ это оборвать.</summary>
        public float Age { get; private set; }
        public Vector2 Center => transform.position;
        /// <summary>Уже лежит в пуле — защита от двойного возврата.</summary>
        public bool Pooled { get; set; }

        SpriteRenderer _sr;
        Vector2 _velocity;

        // Молния рисуется вытянутой, длиннее своего хитбокса, — иначе на таком радиусе
        // её было бы не разглядеть. Множитель к радиусу подобран так, чтобы видимое
        // острие не убегало далеко от центра столкновения.
        const float VisualScale = 4f;

        /// <summary>Во сколько раз рисунок крупнее хитбокса у текущего облика.</summary>
        float _look = VisualScale;

        public void Setup()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = SpriteFactory.LightningBolt();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Projectile;
        }

        public void Activate(Vector2 position, Vector2 velocity, float radius)
        {
            Radius = radius;
            Reflected = false;
            Pooled = false;
            _velocity = velocity;

            // Снаряды берутся из пула, поэтому облик сбрасывается к молнии явно:
            // иначе шип «Великой тени» возвращался бы в игру под видом разряда.
            Age = 0f;
            _look = VisualScale;
            _sr.sprite = SpriteFactory.LightningBolt();
            _sr.color = Palette.Coral;

            transform.position = position;
            transform.localScale = Vector3.one * (radius * _look);
            gameObject.SetActive(true);
            FaceVelocity();
        }

        /// <summary>
        /// Подменить облик снаряда. Шипы «Великой тени» летят тем же механизмом, что и
        /// молнии медузы, но выглядеть обязаны иначе: разряд можно отразить, шип —
        /// только принять на стража, и спутать их игрок не должен.
        /// </summary>
        public void SetLook(Sprite sprite, Color color, float visualScale)
        {
            _look = visualScale;
            _sr.sprite = sprite;
            _sr.color = color;
            transform.localScale = Vector3.one * (Radius * _look);
        }

        public void Reflect(Vector2 direction, float speed)
        {
            Reflected = true;
            _velocity = direction.normalized * speed;
            _sr.color = Palette.StormSpark;
        }

        /// <summary>
        /// Мягкое самонаведение отражённого снаряда. Босс стал крупнее и мечется быстрее,
        /// без подруливания попасть по нему с дистанции почти нереально.
        /// </summary>
        public void SteerTowards(Vector2 target, float turnRateDegPerSec, float dt)
        {
            Vector2 desired = target - (Vector2)transform.position;
            if (desired.sqrMagnitude < 0.0001f || _velocity.sqrMagnitude < 0.0001f) return;

            float speed = _velocity.magnitude;
            float delta = Vector2.SignedAngle(_velocity, desired);
            float maxTurn = turnRateDegPerSec * dt;
            delta = Mathf.Clamp(delta, -maxTurn, maxTurn);
            _velocity = (Vector2)(Quaternion.Euler(0f, 0f, delta) * _velocity.normalized) * speed;
        }

        public bool Tick(float dt, Playfield field)
        {
            Age += dt;
            transform.position += (Vector3)(_velocity * dt);
            FaceVelocity();

            var p = transform.position;
            return p.y > field.Bottom - 1f && p.y < field.Top + 2f &&
                   Mathf.Abs(p.x) < field.HalfWidth + 1.5f;
        }

        /// <summary>Молния всегда смотрит по направлению полёта.</summary>
        void FaceVelocity()
        {
            if (_velocity.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void Deactivate() => gameObject.SetActive(false);
    }
}
