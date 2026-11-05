using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Разряд молнии. Летит в пузырёк; если его перехватил страж — меняет цвет и
    /// уходит обратно в босса.
    ///
    /// Обликов два. Обычный — коралловый зигзаг, им стреляет «Великая тень»:
    /// опасное в игре коралловое, так что цвет читается без раздумий. Облик
    /// «грозовой разряд» включает медуза (<see cref="UseStormLook"/>): ветвистая
    /// сине-фиолетовая молния с бирюзовой сердцевиной и тёмной обводкой.
    ///
    /// **Правила для глаз в грозовом облике** записаны числами:
    /// * форма — один из запечённых вариантов и перетекает в следующий плавно
    ///   (SmoothStep), один вариант живёт <see cref="StormStep"/> — то есть не
    ///   чаще 4 перестроек в секунду и не меньше 0.25 с на форму;
    /// * появление <see cref="StormFadeIn"/>, угасание <see cref="StormFadeOut"/>:
    ///   разряд не возникает и не пропадает за кадр;
    /// * покачивание — один цикл за <see cref="SwayPeriod"/>;
    /// * цвета без белого: сумма каналов не выше 2.3 (SpriteFactory.StormBolt).
    /// Размер рисунка тот же, что был: хитбокс не менялся.
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

        /// <summary>Грозовой облик включён.</summary>
        public bool Storm => _storm;
        /// <summary>Гаснет после удара: уже ни во что не попадает.</summary>
        public bool Fading => _fading;
        /// <summary>Угасание закончилось — можно в пул.</summary>
        public bool FadedOut => _fading && _fadeOutLeft <= 0f;

        /// <summary>Сколько живёт одна форма разряда, с. Не меньше 0.25.</summary>
        public const float StormStep = 0.3f;
        /// <summary>Появление разряда, с. Не быстрее 0.15.</summary>
        public const float StormFadeIn = 0.15f;
        /// <summary>Угасание после удара, с.</summary>
        public const float StormFadeOut = 0.3f;
        /// <summary>Цвет при отражении меняется за это время, с.</summary>
        public const float StormRecolor = 0.2f;
        /// <summary>Один цикл покачивания в полёте, с.</summary>
        public const float SwayPeriod = 1.6f;
        /// <summary>Размах покачивания, градусы.</summary>
        const float SwayDegrees = 4f;

        SpriteRenderer _sr;
        Vector2 _velocity;

        // Молния рисуется вытянутой, длиннее своего хитбокса, — иначе на таком радиусе
        // её было бы не разглядеть. Множитель к радиусу подобран так, чтобы видимое
        // острие не убегало далеко от центра столкновения.
        const float VisualScale = 4f;

        /// <summary>Во сколько раз рисунок крупнее хитбокса у текущего облика.</summary>
        float _look = VisualScale;

        // грозовой облик
        bool _storm;
        SpriteRenderer _sr2;     // следующая форма: проявляется поверх текущей
        SpriteRenderer _tip;     // свечение на острие
        int _variant, _next;
        float _step;             // ход перетекания текущей формы в следующую, 0…1
        float _fadeIn;
        bool _fading;
        float _fadeOutLeft;
        float _recolor;          // 0 — враждебный, 1 — отражённый; едет плавно
        float _swayPhase;
        float _tipSize;

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
            _storm = false;
            _fading = false;
            if (_sr2 != null) _sr2.enabled = false;
            if (_tip != null) _tip.enabled = false;

            transform.position = position;
            transform.localScale = Vector3.one * (radius * _look);
            transform.rotation = Quaternion.identity;
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

        /// <summary>
        /// Грозовой облик медузы. Зовётся сразу после Activate.
        /// </summary>
        /// <param name="tipSize">Свечение на острие в мире, юнитов (0.3 диаметра стража).</param>
        public void UseStormLook(float tipSize)
        {
            if (_sr2 == null)
            {
                var go = new GameObject("StormNext");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 0f, -0.001f);
                _sr2 = go.AddComponent<SpriteRenderer>();
                _sr2.sharedMaterial = SpriteFactory.Unlit;
                _sr2.sortingOrder = SortingOrders.Projectile;

                var tipGo = new GameObject("StormTip");
                tipGo.transform.SetParent(transform, false);
                tipGo.transform.localPosition = new Vector3(0.46f, 0f, -0.002f);
                _tip = tipGo.AddComponent<SpriteRenderer>();
                _tip.sharedMaterial = SpriteFactory.Unlit;
                _tip.sprite = SpriteFactory.SoftDot();
                _tip.sortingOrder = SortingOrders.Projectile;
            }

            _storm = true;
            _variant = Random.Range(0, SpriteFactory.StormBoltVariants);
            _next = NextVariant(_variant);
            _step = Random.value * 0.5f;
            _fadeIn = 0f;
            _fading = false;
            _fadeOutLeft = StormFadeOut;
            _recolor = 0f;
            _swayPhase = Random.value;
            _tipSize = tipSize;
            _sr.color = Color.white;
            _sr2.enabled = true;
            _tip.enabled = true;
            ApplyStorm();
        }

        static int NextVariant(int current)
            => (current + 1 + Random.Range(0, SpriteFactory.StormBoltVariants - 1)) % SpriteFactory.StormBoltVariants;

        public void Reflect(Vector2 direction, float speed)
        {
            Reflected = true;
            _velocity = direction.normalized * speed;
            // Грозовой облик меняет цвет плавно, за StormRecolor; обычный — сразу,
            // как и раньше у «Великой тени».
            if (!_storm) _sr.color = Palette.StormSpark;
        }

        /// <summary>
        /// Удар: снаряд больше ни во что не попадает, стоит на месте и гаснет за
        /// StormFadeOut. Только для грозового облика — обычный убирается сразу.
        /// </summary>
        public void BeginFadeOut()
        {
            if (!_storm) return;
            _fading = true;
            _fadeOutLeft = StormFadeOut;
            _velocity = Vector2.zero;
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
            if (_storm) TickStorm(dt);
            FaceVelocity();

            var p = transform.position;
            return p.y > field.Bottom - 1f && p.y < field.Top + 2f &&
                   Mathf.Abs(p.x) < field.HalfWidth + 1.5f;
        }

        void TickStorm(float dt)
        {
            _fadeIn = Mathf.Min(1f, _fadeIn + dt / StormFadeIn);
            if (_fading) _fadeOutLeft = Mathf.Max(0f, _fadeOutLeft - dt);
            _recolor = Mathf.MoveTowards(_recolor, Reflected ? 1f : 0f, dt / StormRecolor);
            _swayPhase += dt / SwayPeriod;

            // Пока меняется цвет, форма стоит: перетекают палитры, а не формы.
            bool recoloring = _recolor > 0f && _recolor < 1f;
            if (!recoloring)
            {
                _step += dt / StormStep;
                if (_step >= 1f)
                {
                    _step -= 1f;
                    _variant = _next;
                    _next = NextVariant(_variant);
                }
            }
            ApplyStorm();
        }

        void ApplyStorm()
        {
            float alpha = Mathf.SmoothStep(0f, 1f, _fadeIn);
            if (_fading) alpha *= Mathf.SmoothStep(0f, 1f, _fadeOutLeft / StormFadeOut);

            bool recoloring = _recolor > 0f && _recolor < 1f;
            if (recoloring)
            {
                // одна форма в двух палитрах: враждебная уходит, отражённая проступает
                // новая палитра проступает за первую половину, старая уходит за
                // вторую: прозрачность целого не проседает посередине
                float k = _recolor;
                _sr.sprite = SpriteFactory.StormBolt(_variant, false);
                _sr2.sprite = SpriteFactory.StormBolt(_variant, true);
                _sr.color = Color.white.WithAlpha(alpha * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, k))));
                _sr2.color = Color.white.WithAlpha(alpha * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.5f, k)));
            }
            else
            {
                // Следующая форма проступает за первую половину шага, текущая
                // уходит за вторую. При простом перекрёстном переходе посередине
                // оба слоя были бы полупрозрачны, и разряд тускнел бы 3 раза в
                // секунду — пульсация, которой правила не допускают.
                bool reflected = _recolor >= 1f;
                float k = _step;
                _sr.sprite = SpriteFactory.StormBolt(_variant, reflected);
                _sr2.sprite = SpriteFactory.StormBolt(_next, reflected);
                _sr.color = Color.white.WithAlpha(alpha * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, k))));
                _sr2.color = Color.white.WithAlpha(alpha * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.5f, k)));
            }

            Color tip = Color.Lerp(SpriteFactory.StormCoreHostile, SpriteFactory.StormCoreReflected, _recolor);
            _tip.color = tip.WithAlpha(0.5f * alpha);
            float local = _tipSize / Mathf.Max(0.01f, Radius * _look);
            _tip.transform.localScale = Vector3.one * local;
        }

        /// <summary>
        /// Молния смотрит по направлению полёта; грозовая ещё и чуть покачивается —
        /// один цикл за SwayPeriod, ±4°.
        /// </summary>
        void FaceVelocity()
        {
            if (_velocity.sqrMagnitude < 0.0001f) return;
            float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg;
            if (_storm) angle += Mathf.Sin(_swayPhase * Mathf.PI * 2f) * SwayDegrees;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void Deactivate()
        {
            _storm = false;
            _fading = false;
            gameObject.SetActive(false);
        }
    }
}
