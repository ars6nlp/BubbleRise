using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Стая пираний» — босс не из одного существа, а из десяти. Полоса HP здесь
    /// буквально считает живых: **один отбитый пиранья — минус один из стаи**.
    ///
    /// Цикл: СБОР -> ПРОХОД СТРОЕМ -> РАЗЛЁТ -> ОДИНОЧНЫЕ РЫВКИ, и так по кругу.
    ///
    /// 1. **Сбор.** Пираньи уходят за кромку и строятся уже там. Именно уходят,
    ///    а не исчезают: без этого такта построение выглядело бы телепортом.
    ///    Первый сбор — он же и выход босса: стая заводится сразу за кадром и
    ///    появляется только вместе с первым строем. Проявляться посреди экрана она
    ///    не должна — это выглядит как «объекты возникли из воздуха вокруг игрока».
    /// 2. **Проход строем.** Стая проходит экран насквозь — сверху вниз, снизу вверх,
    ///    слева направо, справа налево, по кругу. Опасен ровно **один** пиранья —
    ///    тот, что идёт остриём клина точно по линии пузырька. Только он и считается:
    ///    остальные проходят сквозь стража, их нельзя ни отбить, ни получить от них
    ///    урон. Такт целиком про то, чтобы разглядеть нужного среди десяти.
    /// 3. **Разлёт и одиночные рывки.** Стая расходится по всей арене и по очереди
    ///    бросается на пузырёк с разных сторон, каждый раз с замахом.
    ///
    /// Между тактами стая **живёт по всей карте**, а не висит у пузырька: точки
    /// блуждания раскиданы по всему экрану, ближе `piranhaRoamMinDistance` к пузырьку
    /// их не бывает, и у каждого пираньи свой темп и своя фаза покачивания. Десять
    /// одинаково движущихся тел читались бы одним объектом.
    /// </summary>
    public class PiranhaBossController : MonoBehaviour, IBoss
    {
        enum Phase { Regroup, Formation, Scatter, Aim, Dash, Dying, Done }

        const int ChevronCount = 10;

        class Piranha
        {
            public SpriteRenderer Sr;
            public Vector2 Position;
            public Vector2 Forward = Vector2.right;
            public Vector2 Target;
            public Vector2 Slot;        // место в строю на ближайший проход
            public bool Alive;
            public float DyingTimer;
            public float Wave;          // своя фаза покачивания
            public float SpeedScale;    // свой темп: стая не должна плыть как одно тело
            public bool Mirror;         // отражена ли сейчас; меняется с запасом по курсу
        }

        public string DisplayName => Loc.Get("boss.piranhas");
        public BossKind Kind => BossKind.Piranha;
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

        Piranha[] _pod;
        SpriteRenderer[] _chevrons;

        Phase _phase = Phase.Done;
        float _timer;
        float _phaseLength;
        float _pulse;

        // проход строем
        Vector2 _sweepDirection = Vector2.down;
        int _sweepIndex;            // 0 вниз, 1 вверх, 2 влево, 3 вправо
        int _sweepTarget = -1;      // единственный опасный пиранья в строю
        bool _sweepHitBubble;
        bool _sweepEntered;
        float _sweepTimeout;

        // одиночные рывки
        int _attacker = -1;
        int _soloLeft;
        Vector2 _dashDirection;
        bool _dashHitBubble;

        float Rage => BossTuning.Rage(Hp, MaxHp);
        float Size => _field.PlayHalfWidth * 2f * _cfg.piranhaSizeSpan;

        // ---------------------------------------------------------- построение

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;

            _pod = new Piranha[Mathf.Max(1, cfg.piranhaCount)];
            for (int i = 0; i < _pod.Length; i++)
            {
                var go = new GameObject("Piranha" + i);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.PiranhaSwarm();
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.Boss;
                _pod[i] = new Piranha { Sr = sr };
            }

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

            MaxHp = _pod.Length;
            Hp = MaxHp;
            _pulse = 0f;
            _sweepIndex = 0;
            _attacker = -1;

            for (int i = 0; i < _pod.Length; i++)
            {
                var d = _pod[i];
                d.Alive = true;
                d.DyingTimer = 0f;
                d.Wave = UnityEngine.Random.Range(0f, 10f);
                d.SpeedScale = UnityEngine.Random.Range(0.78f, 1.26f);
                d.Sr.sprite = SpriteFactory.PiranhaSwarm();
                d.Sr.color = Color.white;
                d.Sr.enabled = true;

                // Заводим стаю сразу за кадром: до первого строя её не должно быть
                // видно вовсе. Появление посреди экрана читается как «возникли из
                // воздуха», а не как «приплыли».
                d.Position = PickOffscreenPoint();
                d.Forward = (-d.Position).normalized;
            }

            HideChevrons();
            StartRegroup();
        }

        /// <summary>Случайная точка за кромкой экрана — оттуда стая и заходит.</summary>
        Vector2 PickOffscreenPoint()
        {
            float margin = Size * 1.5f;
            float halfW = _field.HalfWidth + margin;
            float halfH = (_field.Top - _field.Bottom) * 0.5f + margin;
            float midY = (_field.Top + _field.Bottom) * 0.5f;

            switch (UnityEngine.Random.Range(0, 4))
            {
                case 0: return new Vector2(UnityEngine.Random.Range(-halfW, halfW), midY + halfH);
                case 1: return new Vector2(UnityEngine.Random.Range(-halfW, halfW), midY - halfH);
                case 2: return new Vector2(-halfW, UnityEngine.Random.Range(midY - halfH, midY + halfH));
                default: return new Vector2(halfW, UnityEngine.Random.Range(midY - halfH, midY + halfH));
            }
        }

        /// <summary>Стая не оставляет после себя ничего, кроме собственного строя.</summary>
        public void Interrupt() { }

        public void Despawn()
        {
            _phase = Phase.Done;
            HideChevrons();
            gameObject.SetActive(false);
        }

        // --------------------------------------------------------------- цикл

        public void Tick(float dt)
        {
            if (_phase == Phase.Done) return;
            _pulse += dt;

            TickDeaths(dt);

            switch (_phase)
            {
                case Phase.Regroup: TickRegroup(dt); break;
                case Phase.Formation: TickFormation(dt); break;
                case Phase.Scatter: TickScatter(dt); break;
                case Phase.Aim: TickAim(dt); break;
                case Phase.Dash: TickDash(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }

            ApplySprites();
        }

        /// <summary>Отбитый пиранья не исчезает мгновенно — он гаснет, кувыркаясь.</summary>
        void TickDeaths(float dt)
        {
            for (int i = 0; i < _pod.Length; i++)
            {
                var d = _pod[i];
                if (d.Alive || d.DyingTimer <= 0f) continue;

                d.DyingTimer -= dt;
                d.Position += d.Forward * (1.2f * dt) + Vector2.down * (0.6f * dt);

                float k = Mathf.Clamp01(d.DyingTimer / 0.45f);
                d.Sr.color = new Color(1f, 1f, 1f, k);
                if (d.DyingTimer <= 0f) d.Sr.enabled = false;
            }
        }

        // ------------------------------------------------------------- сбор

        /// <summary>
        /// Пираньи уплывают за кромку и строятся там. Без этого такта построение
        /// выглядело бы телепортом: только что кружили по экрану — и вдруг стоят
        /// ровной линией за кадром.
        /// </summary>
        void StartRegroup()
        {
            _phase = Phase.Regroup;
            LayoutFormation();
            _timer = _cfg.piranhaRegroupTimeout;
        }

        void TickRegroup(float dt)
        {
            _timer -= dt;

            bool allInPlace = true;
            for (int i = 0; i < _pod.Length; i++)
            {
                var d = _pod[i];
                if (!d.Alive) continue;

                d.Position = Vector2.MoveTowards(d.Position, d.Slot,
                                                 _cfg.piranhaRegroupSpeed * d.SpeedScale * dt);

                Vector2 delta = d.Slot - d.Position;
                if (delta.sqrMagnitude > 0.01f)
                {
                    d.Forward = Vector2.Lerp(d.Forward, delta.normalized, 1f - Mathf.Exp(-8f * dt)).normalized;
                    allInPlace = false;
                }
                else d.Forward = _sweepDirection;
            }

            if (!allInPlace && _timer > 0f) return;

            // Опоздавших доводим силой: один застрявший не должен держать весь бой.
            for (int i = 0; i < _pod.Length; i++)
            {
                if (!_pod[i].Alive) continue;
                _pod[i].Position = _pod[i].Slot;
                _pod[i].Forward = _sweepDirection;
            }

            _phase = Phase.Formation;
            _sweepEntered = false;
            _sweepTimeout = 12f;
        }

        // ---------------------------------------------------------- строй

        /// <summary>
        /// Разметить места строя для очередного направления. Направления идут по кругу:
        /// вниз, вверх, влево, вправо — стая приходит каждый раз с новой стороны.
        /// </summary>
        void LayoutFormation()
        {
            _sweepHitBubble = false;
            _sweepTarget = -1;

            switch (_sweepIndex % 4)
            {
                case 0: _sweepDirection = Vector2.down; break;
                case 1: _sweepDirection = Vector2.up; break;
                case 2: _sweepDirection = Vector2.left; break;
                default: _sweepDirection = Vector2.right; break;
            }
            _sweepIndex++;

            Vector2 lateral = new Vector2(-_sweepDirection.y, _sweepDirection.x);

            // Полосы режем по ширине ЭКРАНА, а не игровой колонки: десять тел длиной
            // в полтора юнита в колонку просто не помещаются и слипаются в кучу.
            int alive = CountAlive();
            float span = _field.HalfWidth * 2f;
            float step = Mathf.Min(_cfg.piranhaLaneStep, span / Mathf.Max(1, alive));

            // Строй ставим симметрично относительно центра экрана. Раньше он строился
            // от опасной полосы, и при крайнем номере весь клин уезжал к одному краю —
            // половина экрана пустая, стая приходит сбоку, хотя должна идти по центру.
            var screenCentre = new Vector2(0f, (_field.Top + _field.Bottom) * 0.5f);
            float centreLateral = Vector2.Dot(screenCentre, lateral);
            float bubbleLateral = Vector2.Dot(_bubble.Center, lateral);
            float half = (alive - 1) * 0.5f;

            // Одна полоса обязана пройти через пузырёк — иначе такт стал бы декорацией.
            // Берём ту, что и так ближе всех к нему, и подтягиваем ровно на его линию:
            // сдвиг меньше половины шага, симметрия строя от этого не страдает.
            int chosen = Mathf.Clamp(
                Mathf.RoundToInt(half + (bubbleLateral - centreLateral) / Mathf.Max(0.01f, step)),
                0, Mathf.Max(0, alive - 1));

            float depth = _field.Top - _field.Bottom + Size * 2f;
            Vector2 start = -_sweepDirection * (depth * 0.5f + Size * 1.2f);

            int index = 0;
            for (int i = 0; i < _pod.Length; i++)
            {
                var d = _pod[i];
                if (!d.Alive) continue;

                bool dangerous = index == chosen;
                if (dangerous) _sweepTarget = i;

                float lane = dangerous
                    ? bubbleLateral
                    : centreLateral + (index - half) * step + UnityEngine.Random.Range(-0.14f, 0.14f);

                // Клин: чем дальше от опасной полосы, тем сильнее отставание. Оно же
                // разводит тела по глубине — иначе на портретном экране они наезжают
                // друг на друга. Плюс разброс, чтобы строй не выглядел линейкой.
                // Опасному разброс не достаётся: он всегда точно остриё клина, и это
                // единственная подсказка, кого из десяти надо встретить.
                float lag = dangerous
                    ? 0f
                    : Mathf.Abs(index - chosen) * _cfg.piranhaWedgeLag
                      + UnityEngine.Random.Range(-0.35f, 0.35f);

                d.Slot = start + lateral * lane - _sweepDirection * lag;
                index++;
            }

            HideChevrons();
        }

        void TickFormation(float dt)
        {
            float speed = Mathf.Lerp(_cfg.piranhaSweepSpeedMin, _cfg.piranhaSweepSpeedMax, Rage);
            float distance = speed * dt;

            // Дробим шаг: на скорости прохода пиранья перепрыгнул бы стража за кадр.
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.12f));
            float step = distance / steps;

            for (int s = 0; s < steps; s++)
            {
                for (int i = 0; i < _pod.Length; i++)
                {
                    var d = _pod[i];
                    if (!d.Alive) continue;

                    d.Position += _sweepDirection * step;
                }

                // Столкновения считаются ТОЛЬКО для опасной полосы. Остальные идут
                // мимо пузырька и сквозь стража: их нельзя ни отбить, ни получить от
                // них урон. Иначе строй превращался в бесплатную раздачу — страж
                // просто стоял поперёк и снимал всю стаю подряд, а такт должен быть
                // про то, чтобы разглядеть одного нужного среди десяти.
                if (_sweepTarget >= 0 && _pod[_sweepTarget].Alive)
                    ResolveContact(_pod[_sweepTarget], ref _sweepHitBubble);
            }

            if (CountAlive() == 0) { StartDying(); return; }

            bool anyOnScreen = false;
            for (int i = 0; i < _pod.Length; i++)
            {
                var d = _pod[i];
                if (d.Alive && !IsOutside(d.Position, Size)) { anyOnScreen = true; break; }
            }

            // Строй начинается ЗА кадром, поэтому «никого не видно» в первый момент
            // значит «ещё не вошли», а не «уже прошли». Разделяем флагом, иначе такт
            // заканчивался бы на первом же кадре.
            if (anyOnScreen) _sweepEntered = true;

            _sweepTimeout -= dt;
            if ((_sweepEntered && !anyOnScreen) || _sweepTimeout <= 0f) StartScatter();
        }

        // -------------------------------------------------------- разлёт

        void StartScatter()
        {
            _phase = Phase.Scatter;
            _phaseLength = _cfg.piranhaScatterTime;
            _timer = _phaseLength;
            _soloLeft = Mathf.Max(1, _cfg.piranhaSoloPerRound);

            for (int i = 0; i < _pod.Length; i++)
            {
                var d = _pod[i];
                if (!d.Alive) continue;

                d.Target = PickRoamPoint();
                // После прохода они уже далеко за кадром: подтягиваем к кромке, иначе
                // возвращаться в кадр они будут дольше, чем длится сам разлёт.
                d.Position = _field.ClampToScreen(d.Position, -Size);
            }
        }

        /// <summary>
        /// Точка блуждания — по всему экрану, но не вплотную к пузырьку: доставать
        /// должен рывок, а не случайный проплыв мимо.
        /// </summary>
        Vector2 PickRoamPoint()
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var point = new Vector2(
                    UnityEngine.Random.Range(-_field.HalfWidth * 0.92f, _field.HalfWidth * 0.92f),
                    UnityEngine.Random.Range(_field.Bottom + 0.9f, _field.Top - 0.9f));

                if (Vector2.Distance(point, _bubble.Center) > _cfg.piranhaRoamMinDistance) return point;
            }

            // Запасной вариант: верхний угол — заведомо далеко от пузырька.
            return new Vector2(_field.HalfWidth * 0.85f * (UnityEngine.Random.value < 0.5f ? -1f : 1f),
                               _field.Top - 1.2f);
        }

        void TickScatter(float dt)
        {
            _timer -= dt;
            Roam(dt, _cfg.piranhaRoamSpeed);

            if (_timer <= 0f) StartAim();
        }

        /// <summary>
        /// Блуждание по арене. У каждого свой темп и своя фаза, а дойдя до точки,
        /// пиранья сразу берёт следующую — стая всё время в движении по всей карте.
        /// </summary>
        void Roam(float dt, float speed, int skip = -1)
        {
            for (int i = 0; i < _pod.Length; i++)
            {
                if (i == skip) continue;

                var d = _pod[i];
                if (!d.Alive) continue;

                Vector2 delta = d.Target - d.Position;
                if (delta.magnitude < 0.4f) { d.Target = PickRoamPoint(); continue; }

                Vector2 dir = delta.normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);
                dir = (dir + side * (0.3f * Mathf.Sin(_pulse * 2.2f + d.Wave))).normalized;

                d.Forward = Vector2.Lerp(d.Forward, dir, 1f - Mathf.Exp(-6f * dt)).normalized;
                d.Position += d.Forward * (speed * d.SpeedScale * dt);
            }
        }

        // ------------------------------------------------------ одиночный рывок

        void StartAim()
        {
            _attacker = PickAttacker();
            if (_attacker < 0) { StartRegroup(); return; }

            _phase = Phase.Aim;
            _phaseLength = Mathf.Lerp(_cfg.piranhaAimTimeMax, _cfg.piranhaAimTimeMin, Rage);
            _timer = _phaseLength;

            var d = _pod[_attacker];
            Vector2 delta = _bubble.Center - d.Position;
            _dashDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
            d.Forward = _dashDirection;
        }

        /// <summary>Атакует тот, кто дальше от пузырька: рывок должен быть виден целиком.</summary>
        int PickAttacker()
        {
            int best = -1;
            float bestDistance = -1f;
            for (int i = 0; i < _pod.Length; i++)
            {
                if (!_pod[i].Alive) continue;
                float distance = Vector2.Distance(_pod[i].Position, _bubble.Center);
                if (distance > bestDistance) { bestDistance = distance; best = i; }
            }
            return best;
        }

        void TickAim(float dt)
        {
            _timer -= dt;

            // остальные продолжают кружить — стая не замирает ради одного
            Roam(dt, _cfg.piranhaRoamSpeed * 0.7f, skip: _attacker);

            var d = _pod[_attacker];
            if (!d.Alive) { NextSolo(); return; }

            // Атакующий пятится, набирая замах: это и есть сигнал, кто сейчас пойдёт.
            Vector2 delta = _bubble.Center - d.Position;
            _dashDirection = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
            d.Forward = _dashDirection;
            d.Position -= _dashDirection * (_cfg.piranhaRecoilSpeed * dt);

            UpdateChevrons(d.Position, _bubble.Center, 1f - Mathf.Clamp01(_timer / _phaseLength));

            if (_timer <= 0f)
            {
                _phase = Phase.Dash;
                _dashHitBubble = false;
                HideChevrons();
            }
        }

        void TickDash(float dt)
        {
            Roam(dt, _cfg.piranhaRoamSpeed * 0.7f, skip: _attacker);

            var d = _pod[_attacker];
            if (!d.Alive) { NextSolo(); return; }

            float speed = Mathf.Lerp(_cfg.piranhaDashSpeedMin, _cfg.piranhaDashSpeedMax, Rage);
            float distance = speed * dt;
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.10f));
            float step = distance / steps;

            for (int s = 0; s < steps; s++)
            {
                d.Position += _dashDirection * step;
                if (ResolveContact(d, ref _dashHitBubble)) { NextSolo(); return; }
            }

            if (IsOutside(d.Position, Size * 1.5f)) NextSolo();
        }

        /// <summary>Рывок закончился: либо следующая пиранья, либо снова строй.</summary>
        void NextSolo()
        {
            HideChevrons();

            // Вернувшемуся из рывка нужна новая точка, иначе он уплывёт за кадр.
            if (_attacker >= 0 && _pod[_attacker].Alive)
            {
                _pod[_attacker].Position = _field.ClampToScreen(_pod[_attacker].Position, -Size);
                _pod[_attacker].Target = PickRoamPoint();
            }

            if (CountAlive() == 0) { StartDying(); return; }

            _soloLeft--;
            if (_soloLeft <= 0) { StartRegroup(); return; }

            _phase = Phase.Scatter;
            _timer = _cfg.piranhaSoloGap;
        }

        // ------------------------------------------------------- столкновения

        /// <summary>Возвращает true, если пиранья выбыл.</summary>
        bool ResolveContact(Piranha d, ref bool hitBubbleFlag)
        {
            float radius = Size * _cfg.piranhaHitFraction;

            if (_guardian.CanDeflect &&
                Vector2.Distance(d.Position, _guardian.Center) < radius + _guardian.Radius)
            {
                _guardian.NotifyDeflect();
                GuardianBlocked?.Invoke(d.Position);
                Kill(d);
                return true;
            }

            if (!hitBubbleFlag &&
                Vector2.Distance(d.Position, _bubble.Center) < radius + _bubble.Radius)
            {
                hitBubbleFlag = true;   // на подшагах пиранья пройдёт рядом ещё не раз
                BubbleHit?.Invoke(d.Position);
            }
            return false;
        }

        void Kill(Piranha d)
        {
            d.Alive = false;
            d.DyingTimer = 0.45f;

            Hp = Mathf.Max(0, Hp - 1);
            Damaged?.Invoke(d.Position);

            if (Hp <= 0) StartDying();
        }

        void StartDying()
        {
            if (_phase == Phase.Dying || _phase == Phase.Done) return;
            _phase = Phase.Dying;
            _timer = 0f;
            HideChevrons();
        }

        void TickDying(float dt)
        {
            _timer += dt;
            if (_timer < 0.6f) return;

            _phase = Phase.Done;
            Defeated?.Invoke(_bubble.Center + Vector2.up * 2f);
            Despawn();
        }

        // ------------------------------------------------------- вспомогательное

        int CountAlive()
        {
            int n = 0;
            for (int i = 0; i < _pod.Length; i++) if (_pod[i].Alive) n++;
            return n;
        }

        bool IsOutside(Vector2 point, float margin)
        {
            return Mathf.Abs(point.x) > _field.HalfWidth + margin ||
                   point.y > _field.Top + margin || point.y < _field.Bottom - margin;
        }

        void ApplySprites()
        {
            float size = Size;
            for (int i = 0; i < _pod.Length; i++)
            {
                var d = _pod[i];
                if (!d.Sr.enabled) continue;

                d.Sr.transform.position = d.Position;

                float angle = Mathf.Atan2(d.Forward.y, d.Forward.x) * Mathf.Rad2Deg;

                // Спрайт нарисован носом вправо. Простой поворот на 180° положил бы
                // пираньи брюхом вверх, поэтому влево он идёт отражением по X.
                // flipX зеркалит ДО поворота: нос уходит в локальный -x, и чтобы он
                // смотрел куда надо, угол сдвигается на 180°, а не отражается.
                // С запасом, как у акулы: рыба, плывущая почти вертикально,
                // иначе отражалась бы туда-обратно на каждом колебании курса.
                d.Mirror = d.Mirror ? d.Forward.x < 0.25f : d.Forward.x < -0.25f;
                bool mirror = d.Mirror;
                if (mirror) angle -= 180f;

                d.Sr.flipX = mirror;
                d.Sr.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                d.Sr.transform.localScale = Vector3.one * size;
            }
        }

        void UpdateChevrons(Vector2 from, Vector2 to, float urgency)
        {
            Vector2 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.3f) { HideChevrons(); return; }

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            float head = Mathf.Repeat(_pulse * 1.8f, 1f);

            for (int i = 0; i < _chevrons.Length; i++)
            {
                float t = (i + 0.5f) / _chevrons.Length;
                var tr = _chevrons[i].transform;
                tr.position = from + delta * t;
                tr.rotation = Quaternion.Euler(0f, 0f, angle);
                tr.localScale = Vector3.one * 0.6f;

                float wave = 1f - Mathf.Min(Mathf.Abs(t - head), Mathf.Abs(t - head + 1f)) * 4f;
                float bright = Mathf.Clamp01(0.3f + urgency * 0.45f + Mathf.Max(0f, wave) * 0.5f);

                _chevrons[i].enabled = true;
                _chevrons[i].color = new Color(Palette.Coral.r, Palette.Coral.g, Palette.Coral.b, bright);
            }
        }

        void HideChevrons()
        {
            if (_chevrons == null) return;
            for (int i = 0; i < _chevrons.Length; i++) _chevrons[i].enabled = false;
        }
    }
}
