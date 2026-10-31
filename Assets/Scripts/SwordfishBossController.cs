using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Клинок бездны» — рыба-меч. Четвёртый босс и самый быстрый: на экране он бывает
    /// только в момент атаки. Всё остальное время он за кадром.
    ///
    /// Цикл: ПРИЦЕЛ -> РАЗГОН -> (ЗАСТРЯЛ | ПРОБИЛ) -> ОТХОД ЗА КАДР -> ПАУЗА -> ПРИЦЕЛ.
    /// Никакого «поплавал по экрану и вдруг исчез»: вышел за край — и сразу заходит снова.
    ///
    /// Угол разгона выбирается по всей окружности — он заходит откуда угодно, всегда
    /// точно через пузырёк. Чтобы это не было лотереей, на прицеливании у кромки экрана
    /// проступает его силуэт, а от силуэта к пузырьку тянется цепочка стрелок с бегущей
    /// по ней волной: сразу видно, с какой стороны придёт удар.
    ///
    /// Если страж оказался на линии, клинок **втыкается в него и застревает**: рыба
    /// получает урон и несколько секунд бьётся, пытаясь вырваться. Это единственный
    /// момент, когда она стоит на месте — и передышка для игрока. Вырвавшись, на низком
    /// HP она заходит на повторную атаку сразу, без патруля.
    /// </summary>
    public class SwordfishBossController : MonoBehaviour, IBoss
    {
        enum Phase { Enter, Aim, Charge, Retreat, Rest, Dying, Done }

        const int ChevronCount = 14;

        public string DisplayName => Loc.Get("boss.swordfish");
        public BossKind Kind => BossKind.Swordfish;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }

        public event Action<Vector2> Damaged;
        public event Action<Vector2> Defeated;
        public event Action<Vector2> BubbleHit;
        public event Action<Vector2> GuardianBlocked;

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;

        SpriteRenderer _body;
        SpriteRenderer[] _chevrons;

        Phase _phase = Phase.Done;
        float _timer;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();

        Vector2 _position;
        Vector2 _forward = Vector2.right;
        Vector2 _chargeDirection;
        Vector2 _chargeEntry;
        float _chevronPhase;
        bool _hitBubbleThisCharge;
        bool _firstAim = true;

        float Rage => BossTuning.Rage(Hp, MaxHp);

        /// <summary>Остриё клинка — им он и колет.</summary>
        Vector2 Tip => _position + _forward * (_cfg.swordfishLength * 0.46f);

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.AddComponent<SpriteRenderer>();
            _body.sprite = SpriteFactory.Swordfish(false);
            _body.sharedMaterial = SpriteFactory.Unlit;
            _body.sortingOrder = SortingOrders.Boss + 2;
            _tint.Track(_body);

            // Указатель направления: цепочка стрелок от кромки экрана к пузырьку.
            // Одна сплошная линия не показывала, с какой стороны заходит атака.
            var chevron = SpriteFactory.AimChevron();
            _chevrons = new SpriteRenderer[ChevronCount];
            for (int i = 0; i < ChevronCount; i++)
            {
                var go = new GameObject("AimChevron" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = chevron;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.BossAura;
                sr.enabled = false;
                _chevrons[i] = sr;
            }

            gameObject.SetActive(false);
        }

        public void Spawn()
        {
            gameObject.SetActive(true);
            transform.position = Vector3.zero;

            MaxHp = BossTuning.Hp(Mathf.Max(1, _cfg.swordfishHp));
            Hp = MaxHp;
            _phase = Phase.Enter;
            _timer = 0f;
            _tint.Reset();
            _firstAim = true;

            // выход: безобидный пролёт через весь экран, чтобы игрок увидел, кто пришёл
            bool fromLeft = UnityEngine.Random.value < 0.5f;
            _chargeDirection = fromLeft ? Vector2.right : Vector2.left;
            _forward = _chargeDirection;
            _position = new Vector2((fromLeft ? -1f : 1f) * (_field.HalfWidth + 2f),
                                    _bubble.Center.y + 2.6f);

            _body.sprite = SpriteFactory.Swordfish(false);
            _body.color = Color.white;
            _body.enabled = true;
            HideChevrons();

            ApplyTransform(1f);
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _tint.Tick(dt);

            switch (_phase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Rest: TickRest(dt); break;
                case Phase.Aim: TickAim(dt); break;
                case Phase.Charge: TickCharge(dt); break;
                case Phase.Retreat: TickRetreat(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }
        }

        void TickEnter(float dt)
        {
            _position += _chargeDirection * (_cfg.swordfishChargeSpeedMin * 0.8f * dt);
            ApplyTransform(1.15f);
            if (IsOutside(_position, 2f)) StartRest(0.5f);
        }

        // ------------------------------------------------------------ прицел

        void StartAim()
        {
            _phase = Phase.Aim;
            _timer = Mathf.Lerp(_cfg.swordfishAimTimeMax, _cfg.swordfishAimTimeMin, Rage);

            // Заход строго через пузырёк, но с любого угла — «с любого ракурса».
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            _chargeDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // минимальная дистанция захода: снизу до края экрана недалеко, и без неё
            // окно реакции получалось бы вдвое короче, чем при заходе сверху
            float entryDistance = Mathf.Max(5f, DistanceToExit(_bubble.Center, -_chargeDirection) + 0.9f);
            _position = _bubble.Center - _chargeDirection * entryDistance;
            _chargeEntry = _position;
            _forward = _chargeDirection;
            _chevronPhase = 0f;

            // первое прицеливание длиннее: игрок ещё читает баннер с именем босса
            if (_firstAim) { _timer *= 1.5f; _firstAim = false; }

            _body.enabled = true;
            for (int i = 0; i < _chevrons.Length; i++) _chevrons[i].enabled = true;
            ApplyTransform(1f);
        }

        /// <summary>Сколько юнитов от точки до выхода за границы экрана в заданную сторону.</summary>
        float DistanceToExit(Vector2 from, Vector2 direction)
        {
            float best = 30f;
            float limitX = _field.HalfWidth + 0.5f;

            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                float edgeX = direction.x > 0f ? limitX : -limitX;
                best = Mathf.Min(best, (edgeX - from.x) / direction.x);
            }
            if (Mathf.Abs(direction.y) > 0.0001f)
            {
                float edgeY = direction.y > 0f ? _field.Top + 0.5f : _field.Bottom - 0.5f;
                best = Mathf.Min(best, (edgeY - from.y) / direction.y);
            }
            return Mathf.Max(0.5f, best);
        }

        bool IsOutside(Vector2 point, float margin)
        {
            return Mathf.Abs(point.x) > _field.HalfWidth + margin ||
                   point.y > _field.Top + margin || point.y < _field.Bottom - margin;
        }

        void TickAim(float dt)
        {
            _timer -= dt;

            // силуэт наливается цветом у кромки экрана — видно, откуда сейчас ударит
            float reveal = 1f - Mathf.Clamp01(_timer / 0.6f);
            _body.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.30f, 0.95f, reveal));
            _position += _chargeDirection * (1.4f * dt); // подкрадывается к кромке
            ApplyTransform(1f);

            _chevronPhase += dt * 1.9f;
            UpdateChevrons(reveal);

            if (_timer <= 0f)
            {
                _phase = Phase.Charge;
                _hitBubbleThisCharge = false;
                _body.color = Color.white;
                HideChevrons();
            }
        }

        // ------------------------------------------------------------ разгон

        void TickCharge(float dt)
        {
            float speed = Mathf.Lerp(_cfg.swordfishChargeSpeedMin, _cfg.swordfishChargeSpeedMax, Rage);
            float distance = speed * dt;

            // На такой скорости за кадр можно перепрыгнуть стража целиком,
            // поэтому движение дробится на шаги не длиннее 0.12 юнита.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.12f));
            float step = distance / steps;

            for (int i = 0; i < steps; i++)
            {
                _position += _chargeDirection * step;
                if (ResolveHit()) return;
            }

            ApplyTransform(1.3f); // растянут по ходу движения — читается как рывок

            if (IsOutside(_position, 2.2f)) StartRetreat();
        }

        bool ResolveHit()
        {
            Vector2 tip = Tip;

            if (_guardian.CanDeflect &&
                Vector2.Distance(tip, _guardian.Center) < _cfg.swordfishTipRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(tip);

                // Отбитая рыба разворачивается прямо в точке удара и уходит за кадр.
                // Раньше она застревала в страже на полторы секунды и висела посреди
                // экрана — задумывалось как передышка, а читалось как зависший объект.
                _position = _guardian.Center - _chargeDirection * (_cfg.swordfishLength * 0.46f);
                ApplyDamage(tip);

                if (_phase != Phase.Dying)
                {
                    _chargeDirection = -_chargeDirection;
                    _forward = _chargeDirection;
                    StartRetreat();
                }
                ApplyTransform(1f);
                return true;
            }

            if (!_hitBubbleThisCharge &&
                Vector2.Distance(tip, _bubble.Center) < _cfg.swordfishTipRadius + _bubble.Radius)
            {
                _hitBubbleThisCharge = true; // на подшагах остриё пройдёт рядом ещё не раз
                BubbleHit?.Invoke(tip);
                // прошивает насквозь и уходит дальше — останавливать его нечем
            }

            return false;
        }

        /// <summary>Недоигранный разгон отменяем: рыба просто уходит за кадр.</summary>
        public void Interrupt()
        {
            if (_phase == Phase.Done || _phase == Phase.Dying) return;

            _chargeDirection = -_chargeDirection;
            _forward = _chargeDirection;
            StartRetreat();
        }

        void StartRetreat()
        {
            _phase = Phase.Retreat;
            _timer = 1.4f;
            HideChevrons();
        }

        /// <summary>Уход строго за кадр — без прогулок по экрану.</summary>
        void TickRetreat(float dt)
        {
            _position += _chargeDirection * (12f * dt);
            ApplyTransform(1f);

            _timer -= dt;
            if (_timer <= 0f || IsOutside(_position, 2f))
                StartRest(Mathf.Lerp(_cfg.swordfishRestTimeMax, _cfg.swordfishRestTimeMin, Rage));
        }

        /// <summary>Короткая пауза за кадром между заходами.</summary>
        void StartRest(float duration)
        {
            _phase = Phase.Rest;
            _timer = duration;
            _body.enabled = false;
            HideChevrons();
        }

        void TickRest(float dt)
        {
            _timer -= dt;
            if (_timer <= 0f) StartAim();
        }

        void TickDying(float dt)
        {
            _timer += dt;
            _position += _forward * (1.2f * dt) + Vector2.up * (0.4f * dt);
            ApplyTransform(1f);

            float alpha = Mathf.Clamp01(1f - _timer / 1.0f);
            _body.color = new Color(1f, 1f, 1f, alpha);

            if (_timer >= 1.0f)
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
                HideChevrons();
            }
        }

        // ------------------------------------------------------- вспомогательное

        void MoveTowards(Vector2 target, float step)
        {
            Vector2 delta = target - _position;
            if (delta.sqrMagnitude > 0.0001f) _forward = delta.normalized;
            _position = Vector2.MoveTowards(_position, target, step);
        }

        void ApplyTransform(float stretch)
        {
            transform.position = _position;
            float angle = Mathf.Atan2(_forward.y, _forward.x) * Mathf.Rad2Deg;
            _body.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            _body.transform.localScale =
                new Vector3(_cfg.swordfishLength * stretch, _cfg.swordfishLength, 1f);
        }

        /// <summary>
        /// Стрелки от точки входа к пузырьку. По цепочке бежит волна яркости в сторону
        /// движения — направление атаки читается с одного взгляда, без разглядывания.
        /// </summary>
        void UpdateChevrons(float intensity)
        {
            Vector2 from = _chargeEntry;
            Vector2 to = _bubble.Center;
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.3f) { HideChevrons(); return; }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float head = Mathf.Repeat(_chevronPhase, 1f);

            for (int i = 0; i < _chevrons.Length; i++)
            {
                float t = (i + 0.5f) / _chevrons.Length;
                var tr = _chevrons[i].transform;
                tr.position = from + delta * t;
                tr.rotation = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = Vector3.one * 0.62f;

                // бегущая волна: ярче та стрелка, мимо которой сейчас «проходит» голова
                float behind = Mathf.Repeat(t - head + 1f, 1f);
                // Передний фронт волны был пилой: стрелка вспыхивала с 0.22 до
                // 0.84 за кадр, когда «голова» её проходила (E-17). Теперь
                // нарастание за 0.12 периода — около 0.06 с.
                float wave = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(behind / 0.30f))
                             * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(behind / 0.12f));
                float alpha = (0.22f + 0.62f * wave) * Mathf.Clamp01(intensity);
                _chevrons[i].color = Palette.Coral.WithAlpha(alpha);
            }
        }

        void HideChevrons()
        {
            if (_chevrons == null) return;
            for (int i = 0; i < _chevrons.Length; i++) _chevrons[i].enabled = false;
        }

        public void Despawn()
        {
            _phase = Phase.Done;
            HideChevrons();
            gameObject.SetActive(false);
        }
    }
}
