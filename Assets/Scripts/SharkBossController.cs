using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Белая смерть» — девятый босс. Крупная акула, которая двигается рывками:
    /// короткий бросок, короткая пауза, снова бросок. Ровная скорость ей не идёт —
    /// от акулы должно быть страшно именно потому, что она непредсказуемо быстрая.
    ///
    /// У неё две атаки, и различает их игрок по одному признаку — пасть.
    ///
    /// **Пасть сомкнута — таран из засады.** Акула уходит за край экрана целиком,
    /// разворачивается уже там, вне поля зрения, и влетает обратно по прямой в
    /// пузырёк. Работа для стража: встать на линию и принять удар. За это акула
    /// получает урон и уходит по инерции — другого способа ранить её нет.
    ///
    /// **Пасть раскрыта — охота за самим стражем.** Теперь она гонится не за
    /// пузырьком, а за защитой, и подставлять стража больше нельзя — его нужно
    /// уводить. Догнала — зажимает стража в пасти и **несёт к пузырьку, пока не
    /// ударит**; отпускает только после удара. Продержался десять секунд — акула
    /// просто отстаёт, смыкает пасть и возвращается к тарану.
    ///
    /// Охота намеренно не даёт очков: это испытание на выживание, а награда за него —
    /// следующий таран, в котором игрок и отыгрывает HP. Поэтому у акулы их всего
    /// шесть: цикл длинный, и с обычным для боссов запасом бой тянулся бы вдвое дольше.
    ///
    /// Честность охоты держится на одном правиле: **направление броска фиксируется
    /// в момент старта и дальше не доводится.** Акула наводится только в паузах.
    /// С доводкой по ходу броска уклонение было бы физически невозможным — она
    /// быстрее стража.
    /// </summary>
    public class SharkBossController : MonoBehaviour, IBoss
    {
        enum Phase { Enter, Cruise, RamAim, RamCharge, RamRecover, Open, Hunt, Bite, Withdraw, Dying, Done }

        public string DisplayName => Loc.Get("boss.shark");
        public BossKind Kind => BossKind.Shark;
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

        Phase _phase = Phase.Done;
        float _timer;
        // Подмес цвета при попадании вместо подмены спрайта (правила для глаз).
        readonly HitTint _tint = new HitTint();

        /// <summary>
        /// Переворот спрайта с запасом по курсу. Когда акула смотрит почти
        /// вертикально (укус сверху), курс колеблется около нуля по X, и спрайт
        /// переворачивался туда-обратно несколько раз в секунду: белое брюхо
        /// скакало с бока на бок — до 8 перепадов яркости в секунду. [ТЕСТ]
        /// </summary>
        const float FlipHysteresis = 0.25f;
        bool _flipped;

        bool Flip()
        {
            _flipped = _flipped ? _forward.x < FlipHysteresis : _forward.x < -FlipHysteresis;
            return _flipped;
        }
        bool _mouthOpen;

        Vector2 _position;
        Vector2 _forward = Vector2.right;

        float _orbitAngle;
        float _orbitDir = 1f;
        float _breath;

        Vector2 _ramDirection;
        Vector2 _ramAnchor;
        bool _ramLurking;
        bool _hitBubbleThisCharge;

        // Охота: бросок либо пауза между бросками.
        bool _darting;
        Vector2 _dartDirection;
        float _huntLeft;

        bool _holdingGuardian;
        bool _bitBubbleThisHold;

        /// <summary>Следующая атака. Таран и охота идут строго по очереди: игрок должен
        /// успевать переключаться между «подставь» и «уводи», а не угадывать.</summary>
        bool _nextIsHunt;

        float Rage => BossTuning.Rage(Hp, MaxHp);

        float Length => _field.PlayHalfWidth * 2f * _cfg.sharkLengthSpan;
        float OrbitRadius => _field.PlayHalfWidth * 2f * _cfg.sharkOrbitSpan;

        /// <summary>Кончик рыла — им она таранит и им же хватает.</summary>
        Vector2 Snout => _position + _forward * (Length * 0.42f);

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
            _body.sprite = SpriteFactory.Shark(false, false);
            _body.sharedMaterial = SpriteFactory.Unlit;
            _body.sortingOrder = SortingOrders.Boss + 2;
            _tint.Track(_body);

            gameObject.SetActive(false);
        }

        public void Spawn()
        {
            gameObject.SetActive(true);
            transform.position = Vector3.zero;

            MaxHp = BossTuning.Hp(Mathf.Max(1, _cfg.sharkHp));
            Hp = MaxHp;

            _phase = Phase.Enter;
            _timer = 0f;
            _ramLurking = false;
            _tint.Reset();
            _mouthOpen = false;
            _holdingGuardian = false;
            _darting = false;
            _nextIsHunt = false;          // первый заход всегда таран: он проще и учит читать пасть

            // Заходит сбоку, наискось — сразу видно, что зверь крупный и быстрый.
            bool fromLeft = UnityEngine.Random.value < 0.5f;
            _orbitDir = fromLeft ? 1f : -1f;
            _position = new Vector2((fromLeft ? -1f : 1f) * (_field.HalfWidth + Length * 0.6f),
                                    _bubble.Center.y + OrbitRadius * 0.55f);
            _forward = (_bubble.Center - _position).normalized;
            _orbitAngle = Mathf.Atan2(_position.y - _bubble.Center.y, _position.x - _bubble.Center.x);

            _body.color = Color.white;
            _body.enabled = true;
            RefreshSprite();
            ApplyTransform(1f);
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;

            _breath += dt;

            _tint.Tick(dt);

            switch (_phase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Cruise: TickCruise(dt); break;
                case Phase.RamAim: TickRamAim(dt); break;
                case Phase.RamCharge: TickRamCharge(dt); break;
                case Phase.RamRecover: TickRamRecover(dt); break;
                case Phase.Open: TickOpen(dt); break;
                case Phase.Hunt: TickHunt(dt); break;
                case Phase.Bite: TickBite(dt); break;
                case Phase.Withdraw: TickWithdraw(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }
        }

        // -------------------------------------------------------------- выход

        void TickEnter(float dt)
        {
            _timer += dt;
            MoveTowards(OrbitPoint(), _cfg.sharkCruiseSpeed * 1.8f * dt);
            ApplyTransform(1.1f);

            if (_timer > 0.35f && Vector2.Distance(_position, OrbitPoint()) < 1.2f)
                StartCruise();
        }

        // ------------------------------------------------------------ патруль

        /// <summary>Точка на орбите вокруг пузырька — сюда акула возвращается между атаками.</summary>
        Vector2 OrbitPoint()
        {
            Vector2 raw = _bubble.Center + new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * OrbitRadius;
            return _field.ClampToScreen(raw, Length * 0.22f);
        }

        void StartCruise()
        {
            _phase = Phase.Cruise;
            _timer = Mathf.Lerp(_cfg.sharkCruiseTimeMax, _cfg.sharkCruiseTimeMin, Rage);
            SetMouth(false);
        }

        void TickCruise(float dt)
        {
            _timer -= dt;

            // Обходит пузырёк по кругу; на низком HP заметно быстрее.
            _orbitAngle += _orbitDir * dt * Mathf.Lerp(0.9f, 1.5f, Rage);

            // После тарана она оказывается далеко за кадром. Возвращаться оттуда
            // прогулочным ходом значит пропасть с экрана на несколько секунд, поэтому
            // издалека акула подгребает заметно быстрее.
            Vector2 orbit = OrbitPoint();
            float speed = _cfg.sharkCruiseSpeed * Mathf.Lerp(1f, 1.35f, Rage);
            if (Vector2.Distance(_position, orbit) > 2f) speed *= 2.6f;

            MoveTowards(orbit, speed * dt);
            ApplyTransform(1f + Mathf.Sin(_breath * 6f) * 0.03f);

            if (_timer <= 0f)
            {
                if (_nextIsHunt) StartOpen();
                else StartRamAim();
                _nextIsHunt = !_nextIsHunt;
            }
        }

        // -------------------------------------------------------------- таран

        /// <summary>
        /// Замах — уход в засаду. Акула покидает экран целиком и бьёт уже оттуда:
        /// пока она разворачивалась на виду, разворот почти на 180° читался как
        /// «плывёт хвостом вперёд», а сам таран — как медленный наплыв.
        ///
        /// Подсказки при этом нет намеренно. Удар идёт строго по прямой в пузырёк,
        /// зверь крупный, и момент его появления у кромки — единственное и достаточное
        /// предупреждение.
        /// </summary>
        void StartRamAim()
        {
            _phase = Phase.RamAim;
            _timer = _cfg.sharkRamRetreatLimit;
            _ramLurking = false;
            SetMouth(false);

            // Уходит в ту сторону, где она сейчас: так путь до засады короче всего.
            Vector2 away = _position - _bubble.Center;
            if (away.sqrMagnitude < 0.0001f) away = Vector2.up;
            away.Normalize();

            // За край экрана целиком, с запасом на всю длину туши.
            _ramAnchor = _bubble.Center + away * (DistanceToExit(_bubble.Center, away) + Length * 0.70f);
        }

        void TickRamAim(float dt)
        {
            _timer -= dt;

            if (!_ramLurking)
            {
                MoveTowards(_ramAnchor, _cfg.sharkRamRetreatSpeed * dt);
                ApplyTransform(1.08f);

                if (_timer <= 0f || Vector2.Distance(_position, _ramAnchor) < 0.35f)
                {
                    // Разворот делаем здесь, за кадром: на экране его видеть незачем.
                    _ramLurking = true;
                    _timer = _cfg.sharkRamLurkTime;
                    _ramDirection = (_bubble.Center - _position).normalized;
                    _forward = _ramDirection;
                    ApplyTransform(1f);
                }
                return;
            }

            // Притаилась. Стоит на месте, чтобы бросок начался именно из засады.
            if (_timer <= 0f)
            {
                _phase = Phase.RamCharge;
                _ramDirection = (_bubble.Center - _position).normalized;
                _forward = _ramDirection;
                _hitBubbleThisCharge = false;
            }
        }

        /// <summary>Сколько юнитов от точки до края экрана в заданную сторону.</summary>
        float DistanceToExit(Vector2 from, Vector2 direction)
        {
            float best = 30f;
            float limitX = _field.HalfWidth;

            if (Mathf.Abs(direction.x) > 0.0001f)
            {
                float edgeX = direction.x > 0f ? limitX : -limitX;
                best = Mathf.Min(best, (edgeX - from.x) / direction.x);
            }
            if (Mathf.Abs(direction.y) > 0.0001f)
            {
                float edgeY = direction.y > 0f ? _field.Top : _field.Bottom;
                best = Mathf.Min(best, (edgeY - from.y) / direction.y);
            }
            return Mathf.Max(0.5f, best);
        }

        void TickRamCharge(float dt)
        {
            float speed = Mathf.Lerp(_cfg.sharkRamSpeedMin, _cfg.sharkRamSpeedMax, Rage);
            float distance = speed * dt;

            // На такой скорости за кадр можно перепрыгнуть стража целиком.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.12f));
            float step = distance / steps;

            for (int i = 0; i < steps; i++)
            {
                _position += _ramDirection * step;
                if (ResolveRamHit()) return;
            }

            ApplyTransform(1.18f);   // растянута по ходу движения

            if (IsOutside(_position, Length * 0.75f)) StartRamRecover();
        }

        bool ResolveRamHit()
        {
            Vector2 snout = Snout;

            if (_guardian.CanDeflect &&
                Vector2.Distance(snout, _guardian.Center) < _cfg.sharkNoseRadius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(snout);
                ApplyDamage(snout);

                if (_phase != Phase.Dying)
                {
                    // Отбитая акула отваливает назад — удар засчитан, добавки не будет.
                    _ramDirection = -_ramDirection;
                    _forward = _ramDirection;
                    StartRamRecover();
                }
                ApplyTransform(1f);
                return true;
            }

            if (!_hitBubbleThisCharge &&
                Vector2.Distance(snout, _bubble.Center) < _cfg.sharkNoseRadius + _bubble.Radius)
            {
                _hitBubbleThisCharge = true;   // на подшагах рыло пройдёт рядом ещё не раз
                BubbleHit?.Invoke(snout);
            }

            return false;
        }

        void StartRamRecover()
        {
            _phase = Phase.RamRecover;
            _timer = _cfg.sharkRamRecoverTime;
            SetMouth(false);
            ReleaseGuardian();
        }

        void TickRamRecover(float dt)
        {
            _timer -= dt;

            // Уходит по инерции, постепенно замедляясь.
            float speed = Mathf.Lerp(1.5f, 7f, Mathf.Clamp01(_timer / Mathf.Max(0.01f, _cfg.sharkRamRecoverTime)));
            _position += _forward * (speed * dt);
            ApplyTransform(1f);

            if (_timer <= 0f)
            {
                // Возвращается на ту сторону орбиты, где сейчас находится: иначе
                // пересекала бы пузырёк по пути назад и била бы его «бесплатно».
                _orbitAngle = Mathf.Atan2(_position.y - _bubble.Center.y, _position.x - _bubble.Center.x);
                StartCruise();
            }
        }

        // -------------------------------------------------------------- охота

        void StartOpen()
        {
            _phase = Phase.Open;
            _timer = _cfg.sharkOpenTelegraph;
            SetMouth(true);
        }

        /// <summary>Раскрыла пасть и подобралась — сигнал сменить тактику с «подставь» на «уводи».</summary>
        void TickOpen(float dt)
        {
            _timer -= dt;

            TurnTowards(_guardian.Center - _position, 300f, dt);
            _position -= _forward * (1.2f * dt);   // отшатывается перед броском
            _position = _field.ClampToScreen(_position, Length * 0.12f);
            // Дрожь не быстрее 14 рад/с и не больше 3 % (E-16): было 22 рад/с и 5 %.
            ApplyTransform(1f + Mathf.Sin(_breath * 12f) * 0.03f);

            if (_timer <= 0f)
            {
                _phase = Phase.Hunt;
                _huntLeft = _cfg.sharkHuntTime;
                _darting = false;
                _timer = Mathf.Lerp(_cfg.sharkDartGapMax, _cfg.sharkDartGapMin, Rage);
            }
        }

        void TickHunt(float dt)
        {
            _huntLeft -= dt;
            _timer -= dt;

            if (_darting)
            {
                float speed = Mathf.Lerp(_cfg.sharkDartSpeedMin, _cfg.sharkDartSpeedMax, Rage);
                float distance = speed * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.12f));
                float step = distance / steps;

                for (int i = 0; i < steps; i++)
                {
                    _position += _dartDirection * step;
                    if (TryBite()) return;
                }

                _position = _field.ClampToScreen(_position, Length * 0.10f);
                ApplyTransform(1.14f);

                if (_timer <= 0f)
                {
                    _darting = false;
                    _timer = Mathf.Lerp(_cfg.sharkDartGapMax, _cfg.sharkDartGapMin, Rage);
                }
            }
            else
            {
                // Пауза: доворачивается на стража и ползёт к нему. Только здесь она и
                // наводится — в броске направление уже не меняется.
                TurnTowards(_guardian.Center - _position, 420f, dt);
                _position += _forward * (_cfg.sharkCruiseSpeed * 0.75f * dt);
                _position = _field.ClampToScreen(_position, Length * 0.10f);
                ApplyTransform(1f + Mathf.Sin(_breath * 11f) * 0.03f);

                if (TryBite()) return;

                if (_timer <= 0f)
                {
                    _darting = true;
                    _dartDirection = _forward;
                    _timer = _cfg.sharkDartTime;
                }
            }

            if (_huntLeft <= 0f) StartWithdraw();
        }

        /// <summary>
        /// Пасть дотянулась до стража. Перезарядка стража тут ничего не решает:
        /// от раскрытой пасти защищаться нечем, от неё можно только уйти.
        /// </summary>
        bool TryBite()
        {
            if (_guardian.IsPinned) return false;
            if (Vector2.Distance(Snout, _guardian.Center) > _cfg.sharkMawRadius + _guardian.Radius) return false;

            _phase = Phase.Bite;
            _timer = _cfg.sharkBiteFuse;
            _holdingGuardian = true;
            _bitBubbleThisHold = false;
            _darting = false;
            _guardian.Pin(Snout);
            ApplyTransform(1f);
            return true;
        }

        /// <summary>
        /// Держит стража в пасти и несёт его к пузырьку, которому теперь нечем закрыться.
        ///
        /// Отсчёт на отпускание начинается **только после удара**. Раньше он шёл с
        /// момента захвата, и если пузырёк был далеко, акула успевала лишь подержать
        /// стража и отпустить — захват проходил без всякого наказания. Предохранитель
        /// на весь эпизод остаётся, но срабатывает только в патовой ситуации.
        /// </summary>
        void TickBite(float dt)
        {
            _timer -= dt;
            TurnTowards(_bubble.Center - _position, 360f, dt);

            if (!_bitBubbleThisHold)
            {
                _position += _forward * (_cfg.sharkBiteRushSpeed * dt);

                if (Vector2.Distance(Snout, _bubble.Center) < _cfg.sharkNoseRadius + _bubble.Radius)
                {
                    _bitBubbleThisHold = true;
                    BubbleHit?.Invoke(_bubble.Center);
                    _timer = _cfg.sharkBiteHoldTime;   // ударила — теперь можно и отпустить
                }
            }
            else
            {
                // Удар прошёл — уносит добычу в сторону, мотая головой.
                _position += _forward * (2.2f * dt);
            }

            _position = _field.ClampToScreen(_position, Length * 0.10f);
            _guardian.Pin(Snout);   // страж физически внутри пасти, позиция нужна каждый кадр

            float shake = Mathf.Sin(_breath * 14f) * 0.03f;
            ApplyTransform(1f + shake);

            if (_timer <= 0f)
            {
                ReleaseGuardian();
                StartRamRecover();
            }
        }

        // ------------------------------------------------------------ отвалила

        void StartWithdraw()
        {
            _phase = Phase.Withdraw;
            _timer = _cfg.sharkWithdrawTime;
            SetMouth(false);      // пасть смыкается — знак, что охота кончилась
            _darting = false;
        }

        /// <summary>
        /// Продержался — акула отстаёт. Отходит от стража, смыкает пасть и берётся за
        /// пузырёк заново. Передышка короткая, но её видно: закрытая пасть и уход в
        /// сторону — понятный сигнал, что можно перестать убегать.
        /// </summary>
        void TickWithdraw(float dt)
        {
            _timer -= dt;

            TurnTowards(_position - _guardian.Center, 240f, dt);
            _position += _forward * (_cfg.sharkCruiseSpeed * 1.3f * dt);
            _position = _field.ClampToScreen(_position, Length * 0.10f);
            ApplyTransform(1f + Mathf.Sin(_breath * 5f) * 0.04f);

            if (_timer <= 0f)
            {
                _orbitAngle = Mathf.Atan2(_position.y - _bubble.Center.y, _position.x - _bubble.Center.x);
                StartRamAim();     // отплыла — и снова за пузырёк
            }
        }

        // ------------------------------------------------------------- урон

        void ApplyDamage(Vector2 at)
        {
            Hp = Mathf.Max(0, Hp - 1);
            _tint.Hit();
            Damaged?.Invoke(at);

            if (Hp <= 0)
            {
                _phase = Phase.Dying;
                _timer = 0f;
                ReleaseGuardian();
            }
        }

        void TickDying(float dt)
        {
            _timer += dt;

            // Заваливается набок и уходит вниз — крупный зверь тонет, а не тает.
            _position += _forward * (0.9f * dt) + Vector2.down * (1.1f * dt);
            transform.position = _position;

            float roll = Mathf.Clamp01(_timer / 1.1f);
            float angle = Mathf.Atan2(_forward.y, _forward.x) * Mathf.Rad2Deg;
            bool flip = Flip();
            _body.flipX = flip;
            _body.transform.localRotation =
                Quaternion.Euler(0f, 0f, (flip ? angle - 180f : angle) + roll * (flip ? -55f : 55f));

            float s = Length;
            _body.transform.localScale = new Vector3(s, s, 1f);

            _body.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - _timer / 1.2f));

            if (_timer >= 1.2f)
            {
                _phase = Phase.Done;
                Defeated?.Invoke(_position);
                Despawn();
            }
        }

        // ------------------------------------------------------- вспомогательное

        /// <summary>Выпускаем стража из пасти и отваливаем: заход начался до смерти игрока.</summary>
        public void Interrupt()
        {
            if (_phase == Phase.Done || _phase == Phase.Dying) return;

            ReleaseGuardian();
            _guardian.Release();
            StartRamRecover();
        }

        void ReleaseGuardian()
        {
            if (!_holdingGuardian) return;
            _holdingGuardian = false;
            _guardian.Release();
        }

        void SetMouth(bool open)
        {
            if (_mouthOpen == open) return;
            _mouthOpen = open;
            RefreshSprite();
        }

        void RefreshSprite() => _body.sprite = SpriteFactory.Shark(_mouthOpen, false);

        /// <summary>
        /// Доворот к цели через угол, а не интерполяцией самих векторов.
        /// Vector2.Lerp двух почти противоположных направлений проходит через ноль,
        /// а normalized нулевого вектора — это нулевой вектор: угол скакал на
        /// произвольный, и акула на развороте внезапно плыла хвостом вперёд.
        /// </summary>
        void TurnTowards(Vector2 direction, float degreesPerSecond, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            float target = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float current = Mathf.Atan2(_forward.y, _forward.x) * Mathf.Rad2Deg;
            float next = Mathf.MoveTowardsAngle(current, target, degreesPerSecond * dt) * Mathf.Deg2Rad;

            _forward = new Vector2(Mathf.Cos(next), Mathf.Sin(next));
        }

        void MoveTowards(Vector2 target, float step)
        {
            Vector2 delta = target - _position;
            if (delta.sqrMagnitude > 0.0001f) _forward = delta.normalized;
            _position = Vector2.MoveTowards(_position, target, step);
        }

        bool IsOutside(Vector2 point, float margin)
        {
            return Mathf.Abs(point.x) > _field.HalfWidth + margin ||
                   point.y > _field.Top + margin || point.y < _field.Bottom - margin;
        }

        /// <summary>
        /// Спрайт нарисован смотрящим вправо. При движении влево его нельзя просто
        /// развернуть на 180° — акула поплывёт брюхом вверх. Поэтому зеркалим по X и
        /// доворачиваем на угол минус 180: flipX применяется до поворота, и низ
        /// остаётся низом.
        /// </summary>
        void ApplyTransform(float stretch)
        {
            transform.position = _position;

            float angle = Mathf.Atan2(_forward.y, _forward.x) * Mathf.Rad2Deg;
            bool flip = Flip();

            _body.flipX = flip;
            _body.transform.localRotation = Quaternion.Euler(0f, 0f, flip ? angle - 180f : angle);
            _body.transform.localScale = new Vector3(Length * stretch, Length, 1f);
        }

        public void Despawn()
        {
            _phase = Phase.Done;
            ReleaseGuardian();
            if (_body != null) _body.flipX = false;
            _flipped = false;
            gameObject.SetActive(false);
        }
    }
}
