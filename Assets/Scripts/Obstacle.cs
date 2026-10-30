using UnityEngine;

namespace BubbleRise
{
    /// <summary>Чем кончился удар стража по препятствию.</summary>
    public enum HitOutcome
    {
        /// <summary>Удар не прошёл: препятствие сейчас неуязвимо (погасшая ловушка).</summary>
        Ignored,
        /// <summary>Повреждено, но держится. Монет не даёт.</summary>
        Damaged,
        /// <summary>Разрушено. Вот теперь — монеты.</summary>
        Destroyed,
    }

    /// <summary>
    /// Препятствие: движется вниз, живёт по своей повадке из <see cref="HazardTable"/>
    /// и разрушается, когда страж выбьет из него всю прочность.
    ///
    /// Раньше любой удар отправлял препятствие в отлёт, и состояний было два:
    /// целое и отбитое. Теперь между ними появилось третье — повреждённое: прочные
    /// виды держат два-три удара и показывают трещины. Отлёт остался и означает
    /// ровно то же, что и прежде, — объект больше ни с кем не сталкивается и тает.
    ///
    /// Монеты начисляет не препятствие, а <see cref="GameManager"/>, и только за
    /// <see cref="HitOutcome.Destroyed"/>. Облетел, не добил, врезался сам — платы нет.
    /// </summary>
    public class Obstacle : MonoBehaviour
    {
        /// <summary>
        /// Радиус столкновения. У створок он живой: сомкнутая раковина занимает
        /// меньше места, чем раскрытая, и хитбокс обязан это повторять — иначе
        /// игрок получает удар от того, что видит закрытым.
        /// </summary>
        public float Radius => _baseRadius * _radiusScale;

        public bool Deflected { get; private set; }
        public Vector2 Center => transform.position;
        public bool Pooled { get; set; }

        public ObstacleType Kind => _stats.Type;
        public HazardDeath Death => _stats.Death;
        /// <summary>Сколько монет даст это препятствие. Разыграно при появлении,
        /// а не при разрушении: иначе один и тот же объект стоил бы по-разному в
        /// зависимости от того, когда по нему попали.</summary>
        public int Reward => _reward;
        public int Hp => _hp;

        /// <summary>
        /// Опасно ли прямо сейчас. Ловушка в погасшей фазе пропускает сквозь себя
        /// и пузырёк, и стража: мигание должно что-то значить, иначе это украшение.
        /// </summary>
        public bool Harmful => _stats.Motion != HazardMotion.Pulse || _lit;

        /// <summary>Водоворот тянет стража, пока цел. Ноль у всех остальных.</summary>
        public float PullStrength => _stats.Motion == HazardMotion.Pull && !Deflected ? 1f : 0f;

        SpriteRenderer _sr;
        SpriteRenderer _cracks;

        HazardStats _stats;
        int _hp;
        int _reward;
        Color _tint = Color.white;
        float _baseRadius;
        float _radiusScale = 1f;

        Vector2 _velocity;
        float _spin;
        float _fade;
        float _fadeTime;
        float _flash;
        Color _flashColor = Color.white;


        // Покачивание вместо линейного сноса: объект висит в воде, а не летит вбок.
        float _baseX;
        float _swayAmplitude;
        float _swaySpeed;
        float _swayPhase;

        // Фаза собственной повадки: мигание ловушки, смыкание створок, ход маятника.
        float _behaviourPhase;
        bool _lit = true;

        public void Setup()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Obstacle;

            // Трещины — отдельный слой поверх рисунка, а не второй набор спрайтов на
            // каждый вид. Иначе каждому препятствию понадобилось бы по три картинки
            // состояния, и все они рисовались бы заново при каждой правке формы.
            var cracksObject = new GameObject("Cracks");
            cracksObject.transform.SetParent(transform, false);
            _cracks = cracksObject.AddComponent<SpriteRenderer>();
            _cracks.sharedMaterial = SpriteFactory.Unlit;
            _cracks.sortingOrder = SortingOrders.Obstacle + 1;
            _cracks.sprite = SpriteFactory.CracksStage(1);
            _cracks.enabled = false;

        }

        public void Activate(ObstacleType type, float radius, Vector2 position, Vector2 velocity, float spin,
                             float swayAmplitude, float swaySpeed, Color biome)
        {
            _stats = HazardTable.Of(type);
            _hp = Mathf.Max(1, _stats.Hp);
            _reward = _stats.RollReward();

            // Цвет биома: препятствие подкрашивается водой локации, в которой
            // встретилось. Подмешиваем наполовину, а не заменяем — вид должен
            // оставаться узнаваемым, иначе в тёмной впадине все угрозы станут
            // одинаковыми силуэтами.
            _tint = Color.Lerp(_stats.Tint, _stats.Tint * biome, 0.55f);

            _baseRadius = radius * _stats.Scale;
            _radiusScale = 1f;

            Deflected = false;
            Pooled = false;

            _velocity = velocity * _stats.Speed;
            _spin = _stats.Motion == HazardMotion.Spin ? Mathf.Sign(Random.value - 0.5f) * 5.5f : spin;
            _fade = 0f;
            _fadeTime = 0f;
            _flash = 0f;

            _baseX = position.x;
            _swayAmplitude = _stats.Motion == HazardMotion.Swing ? swayAmplitude * 3.2f : swayAmplitude;
            _swaySpeed = _stats.Motion == HazardMotion.Swing ? swaySpeed * 0.55f : swaySpeed;
            _swayPhase = Random.Range(0f, Mathf.PI * 2f);

            _behaviourPhase = Random.Range(0f, Mathf.PI * 2f);
            _lit = true;

            // Вариация выбирается один раз за жизнь объекта: меняться по ходу
            // падения она не должна, иначе камень на глазах превращается в другой
            // камень. Из пула приходит чистым — Activate зовётся при каждом
            // появлении, и старая вариация не переживает возврат.
            _variant = Random.Range(0, SpriteFactory.VariantsFor(type));
            _sr.sprite = SpriteFactory.Hazard(type, _variant);
            _sr.color = _tint;
            _sr.enabled = true;

            _cracks.enabled = false;
            _cracks.color = Color.white.WithAlpha(0f);
            _cracks.transform.localScale = Vector3.one;


            transform.position = position;
            transform.localScale = Vector3.one * (Radius * 2f);
            transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            gameObject.SetActive(true);
        }

        /// <summary>Какая из вариаций рисунка досталась этому препятствию.</summary>
        int _variant;

        /// <summary>Вариация — для эффекта разрушения: осколки той же фактуры.</summary>
        public int Variant => _variant;

        // ------------------------------------------------------------- удары

        /// <summary>
        /// Удар стража. Возвращает, выдержало ли препятствие.
        ///
        /// Направление нужно даже при несмертельном ударе: повреждённое препятствие
        /// подбрасывает, и без этого прочное выглядело бы приклеенным к месту.
        /// </summary>
        /// <summary>
        /// Цвет вспышки от удара. Его задаёт скин: от магмы препятствие раскаляется,
        /// ото льда синеет. Без этого удар выглядел одинаково, чем бы ни били.
        /// </summary>
        /// <summary>Цвет вспышки удара — без белого пересвета (E-08).</summary>
        public void SetHitColor(Color color) => _flashColor = Palette.SafeLight(color);

        public HitOutcome Hit(Vector2 direction, float impulse)
        {
            if (Deflected) return HitOutcome.Ignored;
            if (!Harmful) return HitOutcome.Ignored;

            _hp--;
            if (_hp > 0)
            {
                // Вспышка нарастает за FlashRise, а не встаёт за кадр (E-17).
                _flashUp = true;

                // Две стадии трещин, а не одна проступающая: по прочному
                // препятствию страж бьёт дважды или трижды, и «целое / разбито»
                // не отвечает на вопрос, сколько осталось. Первая стадия — четыре
                // лучa, вторая — восемь и шире.
                float damage = 1f - _hp / (float)Mathf.Max(1, _stats.Hp);
                int stage = damage > 0.5f ? 2 : 1;
                _cracks.sprite = SpriteFactory.CracksStage(stage);
                _cracks.enabled = true;
                _cracks.color = Color.white.WithAlpha(0.45f + damage * 0.35f);

                // Толчок, но слабый: прочное не должно улетать, иначе добить его
                // не получится — оно просто уйдёт за край экрана.
                _velocity += direction.normalized * (impulse * 0.22f);
                _spin += Random.Range(-2.5f, 2.5f);
                return HitOutcome.Damaged;
            }

            Deflect(direction, impulse);
            return HitOutcome.Destroyed;
        }

        /// <summary>
        /// Убрать препятствие без награды: игрок врезался сам, либо поле чистят
        /// перед боссом. Отдельный вход от <see cref="Hit"/> именно поэтому —
        /// чтобы монеты физически нельзя было начислить по этому пути.
        /// </summary>
        public void Deflect(Vector2 direction, float impulse, float fadeTime = 0.8f)
        {
            Deflected = true;
            _velocity = direction.normalized * impulse;
            _spin = Random.Range(-6f, 6f);
            _fadeTime = fadeTime;
            _fade = fadeTime;
            _cracks.enabled = false;
        }

        // -------------------------------------------------------------- такт

        /// <summary>
        /// Погасить вращение: так выглядит заморозка. Единственное, что статус
        /// меняет в самом препятствии, — остальное рисует StatusFx поверх.
        /// </summary>
        public void ChillSpin(float dt) => _spin = Mathf.Lerp(_spin, 0f, dt * 2.5f);

        /// <summary>Рендерер для накладки статуса: по нему берутся форма и слой.</summary>
        public SpriteRenderer Renderer => _sr;

        /// <summary>Возвращает false, когда препятствие пора убрать.</summary>
        public bool Tick(float dt, Playfield field)
        {
            transform.position += (Vector3)(_velocity * dt);
            transform.Rotate(0f, 0f, _spin * Mathf.Rad2Deg * dt);

            if (!Deflected)
            {
                // Обломок висит в воде и качается вокруг своей позиции — по вертикали он
                // движется ровно со скоростью прокрутки, поэтому читается как объект,
                // мимо которого мы проплываем, а не как падающий предмет.
                _swayPhase += dt * _swaySpeed;
                float limit = field.PlayHalfWidth - Radius;
                var p = transform.position;
                p.x = Mathf.Clamp(_baseX + Mathf.Sin(_swayPhase) * _swayAmplitude, -limit, limit);
                transform.position = p;

                TickBehaviour(dt);
            }
            else
            {
                _fade -= dt;
                float a = _fadeTime > 0f ? Mathf.Clamp01(_fade / _fadeTime) : 0f;
                var c = _sr.color;
                c.a = a;
                _sr.color = c;
                if (_fade <= 0f) return false;
            }

            TickFlash(dt);

            var pos = transform.position;
            if (pos.y < field.Bottom - 1.5f || pos.y > field.Top + 4f ||
                Mathf.Abs(pos.x) > field.HalfWidth + 2f)
                return false;

            return true;
        }

        /// <summary>Собственная повадка вида: мигание, смыкание, дыхание водоворота.</summary>
        void TickBehaviour(float dt)
        {
            _behaviourPhase += dt;

            switch (_stats.Motion)
            {
                case HazardMotion.Pulse:
                {
                    // Полторы секунды горит, полсекунды гаснет: окно для удара
                    // короткое, но предсказуемое — по свету видно, когда бить.
                    const float cycle = 2f;
                    float t = Mathf.Repeat(_behaviourPhase, cycle);
                    _lit = t < 1.5f;

                    // Внутри «горит» был размах 33 % на 1.3 Гц — мерцание, которое
                    // просили убрать. Само различие фаз остаётся резким и это
                    // намеренно: по нему игрок понимает, когда бить. Но тусклая
                    // фаза поднята с 0.18 до 0.3, чтобы переход не был провалом в
                    // темноту и обратно.
                    //
                    // Само окно (_lit) не тронуто. Сглажен только свет на стыке
                    // фаз: ступенька 0.3 ↔ 0.72 за кадр стала переходом за 0.08 с
                    // (E-17) — по нему всё так же видно, когда бить.
                    float on = t < 1.5f
                        ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.08f))
                        : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.5f) / 0.08f));
                    float glow = Mathf.Lerp(0.3f, 0.72f + Mathf.Sin(t * 3f) * 0.1f, on);
                    _sr.color = _tint * new Color(1f, 1f, 1f, glow);
                    _radiusScale = _lit ? 1f : 0.82f;
                    break;
                }

                case HazardMotion.Clamp:
                {
                    // Створки дышат: раскрытые занимают больше места и опаснее.
                    float t = (Mathf.Sin(_behaviourPhase * 1.8f) + 1f) * 0.5f;
                    _radiusScale = Mathf.Lerp(0.72f, 1.12f, t);
                    break;
                }

                case HazardMotion.Pull:
                {
                    // Воронка медленно крутится и слегка пульсирует — чтобы было
                    // видно, что она живая и работает, даже когда стража рядом нет.
                    _radiusScale = 1f + Mathf.Sin(_behaviourPhase * 2.2f) * 0.06f;
                    break;
                }
            }

            transform.localScale = Vector3.one * (Radius * 2f);
        }

        /// <summary>Нарастание вспышки удара, с. Было 0 — фронт за кадр.</summary>
        const float FlashRise = 0.06f;
        bool _flashUp;

        /// <summary>Вспышка от несмертельного удара: подсветка цветом стихии, которая гаснет.</summary>
        void TickFlash(float dt)
        {
            if (_flashUp)
            {
                _flash = Mathf.Min(1f, _flash + dt / FlashRise);
                if (_flash >= 1f) _flashUp = false;
            }
            else
            {
                if (_flash <= 0f) return;

                // Затухание вдвое медленнее (было 4): резкий скачок цвета за пару
                // кадров читается как вспышка, плавный — как удар.
                _flash = Mathf.Max(0f, _flash - dt * 2.2f);
            }
            if (Deflected) return;

            Color baseColor = _stats.Motion == HazardMotion.Pulse ? _sr.color : _tint;
            // Глубина подмешивания срезана с 0.9 до 0.45: препятствие теперь
            // подсвечивается цветом стихии, а не перекрашивается в него целиком.
            _sr.color = Color.Lerp(baseColor, _flashColor, _flash * 0.45f);
        }

        public void Deactivate()
        {
            gameObject.SetActive(false);
        }
    }
}
