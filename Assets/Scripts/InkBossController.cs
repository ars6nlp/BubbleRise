using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Чернильный морок» — каракатица, которая бьёт не по пузырьку, а по обзору игрока.
    ///
    /// Цикл: ЗАМАХ -> ПЛЕВОК В КАМЕРУ -> ТЕМНОТА -> ПОИСК -> УДАР СТРУЁЙ
    /// -> ПЕРЕДЫШКА -> ЗАМАХ.
    ///
    /// Ключевой такт — разделение темноты и поиска. Пока экран залит наглухо, искать
    /// нечего: не видно ни сцены, ни стража, которого в этот момент и зашвыривает
    /// в угол. Это пауза вслепую. Потом чернила сходят, картинка возвращается — и
    /// только тогда начинается собственно поиск, уже под тикающие часы.
    ///
    /// Проверяемый навык здесь свой, не повторяющий остальных пятерых: не реакция и не
    /// позиционирование, а **ориентация в потерянной картинке**. Страж оказывается не
    /// там, где игрок его оставил, экран залит, а через несколько секунд прилетит удар
    /// по линии, которую надо успеть перекрыть.
    ///
    /// Никаких меток на страже нет — на время темноты он вообще снимается с показа,
    /// чтобы не просвечивать сквозь заливку. Честным поиск делают две вещи:
    ///
    /// 1. **Углов всего четыре.** Область поиска мала и заучивается за пару боёв,
    ///    поэтому это проверка внимания и скорости, а не лотерея.
    /// 2. **Линия удара показывается вместе с картинкой.** Что перекрывать — известно
    ///    сразу; сложность в том, чтобы успеть, а не в том, чтобы угадать.
    ///
    /// Страж при этом не обезврежен — он просто не в том месте. Это важное отличие от
    /// захвата краба: там страж не работает, здесь работает, но лежит в углу.
    /// </summary>
    public class InkBossController : MonoBehaviour, IBoss
    {
        enum Phase { Enter, Charge, Spit, Blackout, Search, Strike, Recover, Dying, Done }

        const int ChevronCount = 12;

        public string DisplayName => Loc.Get("boss.ink");
        public BossKind Kind => BossKind.Ink;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        public event Action<Vector2> Damaged;
        public event Action<Vector2> Defeated;
        public event Action<Vector2> BubbleHit;
        public event Action<Vector2> GuardianBlocked;

        /// <summary>Чернила накрыли экран — момент для звука.</summary>
        public event Action InkSplashed;

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;

        InkScreen _ink;
        SpriteRenderer _body;
        SpriteRenderer _blob;      // сгусток в полёте к камере
        SpriteRenderer _jet;       // струя, которой он бьёт по линии
        SpriteRenderer[] _chevrons;

        Phase _phase = Phase.Done;
        float _timer;
        float _phaseLength;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();
        float _pulse;

        Vector2 _position;
        float _targetX;
        int _lastCorner = -1;

        Vector2 _jetPosition;
        Vector2 _jetDirection;
        bool _jetHitBubble;

        /// <summary>Насколько плотно залит экран прямо сейчас. Нужно снаружи: пока
        /// обзора нет, новых рыб выпускать нечестно.</summary>
        public float InkCoverage => _ink != null ? _ink.Coverage : 0f;

        float Rage => BossTuning.Rage(Hp, MaxHp);

        float BodySize => _field.PlayHalfWidth * 2f * _cfg.inkBodySpan;

        /// <summary>Жерло сифона — из него летят и сгусток, и струя.</summary>
        Vector2 Muzzle => _position + Vector2.down * (BodySize * 0.34f);

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;

            _body = NewRenderer("Body", SortingOrders.Boss);
            _body.sprite = SpriteFactory.InkBoss(false);
            _tint.Track(_body);

            _blob = NewRenderer("InkBlob", SortingOrders.Projectile);
            _blob.sprite = SpriteFactory.InkBlob();
            _blob.enabled = false;

            _jet = NewRenderer("InkJet", SortingOrders.Projectile);
            _jet.sprite = SpriteFactory.InkBlob();
            _jet.enabled = false;

            // Стрелки живут в слое поверх чернил — иначе их не было бы видно ровно
            // тогда, когда они и нужны. Страж в этот слой не попадает намеренно:
            // подсвеченный, он не оставил бы игроку никакого поиска.
            var chevron = SpriteFactory.AimChevron();
            _chevrons = new SpriteRenderer[ChevronCount];
            for (int i = 0; i < ChevronCount; i++)
            {
                _chevrons[i] = NewRenderer("AimChevron" + i, SortingOrders.InkMarker);
                _chevrons[i].sprite = chevron;
                _chevrons[i].enabled = false;
            }

            // Экран чернил висит на камере, а не на боссе: он не должен ездить с миром.
            var inkGo = new GameObject("InkScreen");
            _ink = inkGo.AddComponent<InkScreen>();
            _ink.Init(cfg, field, field.Cam.transform);

            gameObject.SetActive(false);
        }

        SpriteRenderer NewRenderer(string childName, int order)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = order;
            return sr;
        }

        public void Spawn()
        {
            gameObject.SetActive(true);

            MaxHp = BossTuning.Hp(Mathf.Max(1, _cfg.inkHp));
            Hp = MaxHp;
            _tint.Reset();
            _pulse = 0f;
            _lastCorner = -1;

            // Заходит сверху, из-за кромки: видно, кто пришёл.
            _position = new Vector2(0f, _field.Top + BodySize * 0.8f);
            _targetX = 0f;
            _phase = Phase.Enter;
            _timer = 0f;

            _body.sprite = SpriteFactory.InkBoss(false);
            _body.color = Color.white;
            _body.enabled = true;
            _blob.enabled = false;
            _jet.enabled = false;
            HideChevrons();
            _ink.Clear();
            _guardian.SetVisible(true);

            ApplyTransform();
        }

        /// <summary>Чернила на экране гаснут сами по таймеру, отдельного сброса не требуют.</summary>
        public void Interrupt() { }

        public void Despawn()
        {
            _phase = Phase.Done;
            _ink.Clear();
            _guardian.SetVisible(true);      // на случай смерти игрока прямо в темноте
            _guardian.ClearDisplacement();   // иначе страж остался бы лежать в углу навсегда
            HideChevrons();
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _pulse += dt;

            _tint.Tick(dt);

            // При гибели чернила смываются ускоренно — добивать босса вслепую незачем.
            _ink.Tick(_phase == Phase.Dying ? dt * 6f : dt);

            switch (_phase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Charge: TickCharge(dt); break;
                case Phase.Spit: TickSpit(dt); break;
                case Phase.Blackout: TickBlackout(dt); break;
                case Phase.Search: TickSearch(dt); break;
                case Phase.Strike: TickStrike(dt); break;
                case Phase.Recover: TickRecover(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }
        }

        Vector2 HoverPoint => new Vector2(_targetX, _field.YFromTop(_cfg.inkHoverFactor));

        void TickEnter(float dt)
        {
            _position = Vector2.MoveTowards(_position, HoverPoint, 6f * dt);
            ApplyTransform();

            if (Vector2.Distance(_position, HoverPoint) < 0.05f) StartCharge(0.6f);
        }

        // ------------------------------------------------------------- замах

        void StartCharge(float extra = 0f)
        {
            _phase = Phase.Charge;
            _phaseLength = _cfg.inkChargeTime + extra;
            _timer = _phaseLength;
            _blob.enabled = false;
            _jet.enabled = false;
            HideChevrons();
        }

        void TickCharge(float dt)
        {
            _timer -= dt;
            Drift(dt);

            // Мантия набирает воздух: сжимается и раздувается. Это единственный
            // сигнал замаха, поэтому он нарочно крупный.
            float t = 1f - Mathf.Clamp01(_timer / _phaseLength);
            // Мелкая дрожь поверх замаха — 12 рад/с, а не 22 (E-16).
            float swell = 1f + 0.16f * Mathf.Sin(t * Mathf.PI) + 0.03f * Mathf.Sin(_pulse * 12f);
            ApplyTransform(swell, 1f / swell);

            if (_timer <= 0f) StartSpit();
        }

        // ------------------------------------------------------------ плевок

        void StartSpit()
        {
            _phase = Phase.Spit;
            _phaseLength = Mathf.Max(0.1f, _cfg.inkSpitTime);
            _timer = _phaseLength;

            _blob.enabled = true;
            _blob.transform.position = Muzzle;
            _blob.transform.localScale = Vector3.one * (BodySize * 0.25f);
            _blob.color = Color.white;
        }

        /// <summary>
        /// Сгусток летит «в камеру»: разрастается и выцветает к центру экрана.
        /// Настоящей глубины у нас нет, поэтому иллюзию даёт разгон масштаба —
        /// растёт он не линейно, а с ускорением к концу.
        /// </summary>
        void TickSpit(float dt)
        {
            _timer -= dt;
            float t = 1f - Mathf.Clamp01(_timer / _phaseLength);

            Vector2 from = Muzzle;
            Vector2 to = new Vector2(0f, (_field.Top + _field.Bottom) * 0.5f);
            _blob.transform.position = Vector2.Lerp(from, to, t * t);

            float screen = Mathf.Max(_field.HalfWidth * 2f, _field.Top - _field.Bottom);
            _blob.transform.localScale = Vector3.one * Mathf.Lerp(BodySize * 0.25f, screen * 1.4f, t * t * t);
            _blob.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.25f, t));

            ApplyTransform();

            if (_timer <= 0f) Blind();
        }

        // ------------------------------------------------------------- поиск

        /// <summary>
        /// Попадание: экран закрывается наглухо, стража зашвыривает в угол.
        ///
        /// Страж на это время снимается с показа. Скрывать его приходится явно:
        /// заливка не абсолютно непрозрачна (иначе получился бы глухой чёрный
        /// прямоугольник вместо «сцена едва угадывается»), и яркий шарик просвечивал бы
        /// сквозь неё, выдавая угол раньше времени.
        /// </summary>
        void Blind()
        {
            // Клякса гаснет не здесь, а когда чернила закроют экран целиком
            // (TickBlackout): иначе за 0.2 с нарастания экран сначала светлел бы.
            _phase = Phase.Blackout;
            _phaseLength = Mathf.Lerp(_cfg.inkBlackoutTimeMin, _cfg.inkBlackoutTimeMax, Rage);
            _timer = _phaseLength;

            _ink.Splash(_phaseLength, _cfg.inkFadeTime);
            _guardian.Displace(PickCorner());
            _guardian.SetVisible(false);
            HideChevrons();
            InkSplashed?.Invoke();

            // Новая позиция по горизонтали: линия удара каждый раз другая, иначе
            // игрок заучил бы одно место и перестал смотреть на стрелки.
            _targetX = UnityEngine.Random.Range(-_field.PlayHalfWidth * 0.8f, _field.PlayHalfWidth * 0.8f);
        }

        /// <summary>Пауза вслепую. Босс дрейфует, игрок ничего не видит и не ищет.</summary>
        void TickBlackout(float dt)
        {
            if (_blob.enabled && _ink.Coverage >= 0.999f) _blob.enabled = false;
            _timer -= dt;
            Drift(dt);
            ApplyTransform();

            if (_timer > 0f) return;

            // Картинка возвращается — вот теперь ищем.
            _guardian.SetVisible(true);
            _phase = Phase.Search;
            _phaseLength = Mathf.Lerp(_cfg.inkSearchTimeMax, _cfg.inkSearchTimeMin, Rage);
            _timer = _phaseLength;
        }

        /// <summary>Угол экрана, обязательно не тот же, что в прошлый раз.</summary>
        Vector2 PickCorner()
        {
            int corner = UnityEngine.Random.Range(0, 4);
            if (corner == _lastCorner) corner = (corner + 1 + UnityEngine.Random.Range(0, 3)) % 4;
            _lastCorner = corner;

            float marginX = _field.HalfWidth * 0.22f;
            float marginY = (_field.Top - _field.Bottom) * 0.18f;

            float x = (corner == 0 || corner == 3) ? -_field.HalfWidth + marginX
                                                   : _field.HalfWidth - marginX;
            float y = (corner < 2) ? _field.Top - marginY : _field.Bottom + marginY;
            return new Vector2(x, y);
        }

        void TickSearch(float dt)
        {
            _timer -= dt;
            Drift(dt);
            ApplyTransform();

            // Стрелки от сифона к пузырьку: что перекрывать — видно с первого взгляда.
            UpdateChevrons();

            if (_timer <= 0f) StartStrike();
        }

        // -------------------------------------------------------------- удар

        void StartStrike()
        {
            _phase = Phase.Strike;
            HideChevrons();

            Vector2 delta = _bubble.Center - Muzzle;
            _jetDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.down;
            _jetPosition = Muzzle;
            _jetHitBubble = false;

            _jet.enabled = true;
            _jet.transform.localScale = Vector3.one * (_cfg.inkJetRadius * 2.4f);
            _jet.color = Color.white;
        }

        void TickStrike(float dt)
        {
            float distance = _cfg.inkStrikeSpeed * dt;

            // Дробим шаг: на полной скорости струя перепрыгнула бы стража за кадр.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.10f));
            float step = distance / steps;

            for (int i = 0; i < steps; i++)
            {
                _jetPosition += _jetDirection * step;
                if (ResolveJet()) return;
            }

            _jet.transform.position = _jetPosition;
            ApplyTransform();

            if (Mathf.Abs(_jetPosition.x) > _field.HalfWidth + 1.5f ||
                _jetPosition.y < _field.Bottom - 1.5f || _jetPosition.y > _field.Top + 1.5f)
                StartRecover();
        }

        bool ResolveJet()
        {
            if (_guardian.CanDeflect &&
                Vector2.Distance(_jetPosition, _guardian.Center) < _cfg.inkJetRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(_jetPosition);
                ApplyDamage(_jetPosition);

                _jet.enabled = false;
                if (_phase != Phase.Dying) StartRecover();
                return true;
            }

            if (!_jetHitBubble &&
                Vector2.Distance(_jetPosition, _bubble.Center) < _cfg.inkJetRadius + _bubble.Radius)
            {
                _jetHitBubble = true;   // на подшагах струя пройдёт рядом ещё не раз
                BubbleHit?.Invoke(_jetPosition);
            }
            return false;
        }

        void StartRecover()
        {
            _phase = Phase.Recover;
            _timer = _cfg.inkRestTime;
            _jet.enabled = false;
        }

        void TickRecover(float dt)
        {
            _timer -= dt;
            Drift(dt);
            ApplyTransform();
            if (_timer <= 0f) StartCharge();
        }

        void TickDying(float dt)
        {
            _timer += dt;
            _position += Vector2.up * (0.5f * dt);
            ApplyTransform(1f - _timer * 0.15f, 1f + _timer * 0.1f);

            _body.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - _timer / 1.1f));

            if (_timer >= 1.1f)
            {
                _phase = Phase.Done;
                Defeated?.Invoke(_position);
                Despawn();
            }
        }

        void ApplyDamage(Vector2 at)
        {
            Hp = Mathf.Max(0, Hp - 1);
            _tint.Hit();
            Damaged?.Invoke(at);

            if (Hp <= 0)
            {
                _phase = Phase.Dying;
                _timer = 0f;
                _jet.enabled = false;
                _blob.enabled = false;
                HideChevrons();
                _guardian.SetVisible(true);
                _guardian.ClearDisplacement();
            }
        }

        // ------------------------------------------------------- вспомогательное

        /// <summary>Неспешный дрейф к выбранной точке — босс не телепортируется.</summary>
        void Drift(float dt)
        {
            _position = Vector2.MoveTowards(_position, HoverPoint, _cfg.inkDriftSpeed * dt);
        }

        void ApplyTransform(float scaleX = 1f, float scaleY = 1f)
        {
            transform.position = _position;

            // Мантию качает на воде — без этого босс выглядит приклеенным.
            float sway = Mathf.Sin(_pulse * 1.6f) * 3.5f;
            _body.transform.localRotation = Quaternion.Euler(0f, 0f, sway);
            _body.transform.localScale = new Vector3(BodySize * scaleX, BodySize * scaleY, 1f);
        }

        void UpdateChevrons()
        {
            Vector2 from = Muzzle;
            Vector2 delta = _bubble.Center - from;
            float distance = delta.magnitude;
            if (distance < 0.3f) { HideChevrons(); return; }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float head = Mathf.Repeat(_pulse * 1.6f, 1f);

            // К концу окна стрелки разгораются — видно, что время выходит.
            float urgency = 1f - Mathf.Clamp01(_timer / Mathf.Max(0.01f, _phaseLength));

            for (int i = 0; i < _chevrons.Length; i++)
            {
                float t = (i + 0.5f) / _chevrons.Length;
                var tr = _chevrons[i].transform;
                tr.position = from + delta * t;
                tr.rotation = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = Vector3.one * 0.6f;

                float wave = 1f - Mathf.Min(Mathf.Abs(t - head), Mathf.Abs(t - head + 1f)) * 4f;
                float bright = Mathf.Clamp01(0.35f + urgency * 0.4f + Mathf.Max(0f, wave) * 0.5f);

                _chevrons[i].enabled = true;
                _chevrons[i].color = new Color(Palette.InkGlow.r, Palette.InkGlow.g, Palette.InkGlow.b, bright);
            }
        }

        void HideChevrons()
        {
            if (_chevrons == null) return;
            for (int i = 0; i < _chevrons.Length; i++) _chevrons[i].enabled = false;
        }
    }
}
