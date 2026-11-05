using System;
using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// «Грозовая медуза» — первый босс. Всплывает сверху, зависает, ходит синусоидой
    /// и бьёт по пузырьку молниями. Отражённые стражем разряды наносят ей урон.
    ///
    /// Вплотную не подпускает: вокруг купола потрескивает поле молний из двух колец,
    /// вращающихся навстречу друг другу.
    ///
    /// Размеры считаются из долей игровой колонки на спавне — так медуза одинаково
    /// крупно читается и на портрете, и на широком экране.
    /// </summary>
    public class JellyBossController : MonoBehaviour, IBoss
    {
        public enum Phase { Enter, Fight, Dying, Done }

        public string DisplayName => Loc.Get("boss.jelly");
        public Phase CurrentPhase { get; private set; }
        public BossKind Kind => BossKind.Jelly;
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        public Vector2 Center => transform.position;

        /// <summary>Снаряд отражён стражем (позиция).</summary>
        public event Action<Vector2> GuardianBlocked;
        /// <summary>Боссу нанесён урон (позиция).</summary>
        public event Action<Vector2> Damaged;
        /// <summary>Босс уничтожен (позиция взрыва).</summary>
        public event Action<Vector2> Defeated;
        /// <summary>Снаряд попал по пузырьку (позиция).</summary>
        public event Action<Vector2> BubbleHit;

        GameConfig _cfg;
        Playfield _field;
        BubbleController _bubble;
        GuardianOrb _guardian;
        SpriteRenderer _sr;          // купол
        SpriteRenderer _glow;        // биолюминесценция внутри купола
        SpriteRenderer _haze;        // дымка вокруг
        SpriteRenderer _eye;         // один большой немигающий глаз
        SpriteRenderer _ripple;      // тёмная рябь по куполу перед атакой
        Transform _body;             // всё, что сжимается при сокращении купола
        JellyTentacles _arms;
        SpriteRenderer _aura;
        SpriteRenderer _aura2;

        // Медленные величины внешнего вида. Все с ограниченной скоростью —
        // правила для глаз записаны здесь числами, а не в комментарии.
        float _breath;        // фаза дыхания свечения, один цикл за BreathPeriod
        float _glowLevel;     // текущее свечение глаза и купола, 0…1
        float _glowWant;      // к чему оно едет
        float _squash;        // сжатие купола, 0 — покой, 1 — максимум
        float _squashWant;
        float _eyeAim;        // куда смотрит глаз, в градусах
        float _telegraph;     // сколько осталось до выстрела, пока идёт показ
        float _age;           // общее время жизни вида, для ползущих слоёв
        float _reach;         // как сильно щупальца тянутся к игроку, 0…1

        /// <summary>Период дыхания свечения. Не быстрее одного цикла за 2 с.</summary>
        const float BreathPeriod = 2.6f;
        /// <summary>За сколько глаз разгорается. Не быстрее 0.3 с.</summary>
        const float GlowRise = 0.45f;
        /// <summary>
        /// Подмес цвета к куполу при попадании: тот же, что у остальных боссов
        /// (<see cref="HitTint"/>). Прежняя подсветка вспыхивала за один кадр и
        /// не ограничивала частоту.
        /// </summary>
        readonly HitTint _tint = new HitTint();

        /// <summary>Видимая альфа колец поля: едет к цели, а не прыгает (E-15).</summary>
        float _fieldAlpha = 0.07f;
        /// <summary>За сколько кольца проходят весь путь от покоя до упора, с.</summary>
        const float FieldFade = 0.25f;
        /// <summary>За сколько до выстрела медуза начинает показывать атаку.</summary>
        const float TelegraphTime = 0.75f;
        /// <summary>Сколько длится угасание при смерти.</summary>
        const float DeathTime = 1.8f;

        // Пересчитывается из долей игровой колонки на спавне.
        float _bellRadius;
        float _fieldRadius;
        float _hitRadius;
        Transform _projectileRoot;
        int _shotCounter;

        readonly List<BossProjectile> _live = new List<BossProjectile>();
        readonly Stack<BossProjectile> _pool = new Stack<BossProjectile>();
        /// <summary>Разряды после удара: гаснут StormFadeOut и только потом в пул.</summary>
        readonly List<BossProjectile> _fading = new List<BossProjectile>();

        FxManager _fx;
        SpriteRenderer _veins;        // жилки купола: по ним бежит свет телеграфа
        SpriteRenderer _aimRing;      // мягкое кольцо в точке прицела
        SpriteRenderer[] _impactRings;
        float[] _impactAge;
        float _veinsShown;
        float _aimShown;
        float _aimScale = 1f;
        Vector2 _aimAt;
        float _trailClock;
        float _sparkClock;

        /// <summary>Кольцо прицела: прозрачность не выше этой.</summary>
        const float AimRingAlpha = 0.45f;
        /// <summary>Кольцо всплеска при попадании живёт столько, с.</summary>
        const float ImpactTime = 0.35f;

        float _timer;
        float _shootTimer;
        float _targetY;

        public void Init(GameConfig cfg, Playfield field, BubbleController bubble, GuardianOrb guardian,
                         Transform projectileRoot, FxManager fx)
        {
            _cfg = cfg;
            _field = field;
            _bubble = bubble;
            _guardian = guardian;
            _projectileRoot = projectileRoot;
            _fx = fx;

            // Тело — отдельный узел: он сжимается при сокращении купола, а
            // сам объект босса остаётся носителем позиции и хитбокса, которые
            // трогать нельзя.
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(transform, false);
            _body = bodyGo.transform;

            _haze = AddLayer("Haze", SpriteFactory.JellyHaze(), SortingOrders.JellyHaze, transform);

            _glow = AddLayer("Glow", SpriteFactory.JellyGlow(), SortingOrders.JellyGlow, _body);
            _sr = AddLayer("Bell", SpriteFactory.JellyBell(), SortingOrders.Boss, _body);
            _tint.Track(_sr);
            _ripple = AddLayer("Ripple", SpriteFactory.JellyRipple(), SortingOrders.JellyRipple, _body);
            _veins = AddLayer("Veins", SpriteFactory.JellyVeins(), SortingOrders.JellyRipple, _body);
            _veins.enabled = false;

            // Кольцо прицела и кольца всплеска живут в мире, а не на куполе.
            var aimGo = new GameObject("AimRing");
            aimGo.transform.SetParent(_projectileRoot, false);
            _aimRing = aimGo.AddComponent<SpriteRenderer>();
            _aimRing.sprite = SpriteFactory.Ring();
            _aimRing.sharedMaterial = SpriteFactory.Unlit;
            _aimRing.sortingOrder = SortingOrders.Projectile - 1;
            _aimRing.enabled = false;

            _impactRings = new SpriteRenderer[3];
            _impactAge = new float[3];
            for (int i = 0; i < _impactRings.Length; i++)
            {
                var go = new GameObject("ImpactRing" + i);
                go.transform.SetParent(_projectileRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Ring();
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.Projectile;
                sr.enabled = false;
                _impactRings[i] = sr;
                _impactAge[i] = ImpactTime;
            }
            _eye = AddLayer("Eye", SpriteFactory.JellyEye(), SortingOrders.JellyEye, _body);
            // 0.33, а не 0.42: при 0.42 глаз закрывал купол целиком, и ни
            // жилок, ни органов видно не было. [ТЕСТ]
            _eye.transform.localScale = Vector3.one * 0.33f;

            _arms = gameObject.AddComponent<JellyTentacles>();
            _arms.Build(_body);

            // Видимая граница поля молний. Кольца зубчатые и вращаются навстречу
            // друг другу — статичная текстура так читается как потрескивающий разряд.
            _aura = CreateFieldRing("StormField1");
            _aura2 = CreateFieldRing("StormField2");

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Во сколько раз ужат ВИД босса, чтобы влезть в экран. Единица — не
        /// ужат. Хитбокс (<c>_hitRadius</c>) и радиус поля считаются от
        /// <c>_bellRadius</c> и этим множителем не трогаются вовсе: уменьшать
        /// зону попадания из-за формата экрана нельзя.
        /// </summary>
        float _visualFit = 1f;

        /// <summary>
        /// Насколько вид босса торчит вверх от его центра, в долях собственного
        /// масштаба. Считается по самому высокому слою: кольца поля шире купола
        /// и дымки.
        /// </summary>
        float VisualUp => JellyFit.Up(HazeSpan, FieldSpan);

        /// <summary>Насколько вид торчит вниз: щупальца длиннее купола.</summary>
        float VisualDown => JellyFit.Down(HazeSpan, FieldSpan, JellyTentacles.Links);

        /// <summary>Размах дымки в долях купола. Нужен и проверке по форматам.</summary>
        public const float HazeSpanConst = 1.7f;
        const float HazeSpan = HazeSpanConst;
        float FieldSpan => _bellRadius > 0.01f ? _fieldRadius / _bellRadius : 1f;

        /// <summary>
        /// Подогнать высоту зависания и, если нужно, размер вида под экран.
        ///
        /// **Почему не константой.** Высота мира постоянна (worldHeight = 10), а
        /// ширина игровой колонки зависит от соотношения сторон, и размер босса
        /// считается от неё. На широком экране (4:3, альбомная ориентация)
        /// колонка упирается в designAspect, босс становится крупнее, и верх
        /// колец поля подходит к кромке вплотную: при PlayHalfWidth = 3.2 запас
        /// был 0.096 юнита — меньше процента высоты экрана. [РАСЧЁТ]
        ///
        /// Сначала двигаем высоту зависания, и только если и этого мало —
        /// ужимаем вид. Хитбокс не меняется ни в том, ни в другом случае.
        /// </summary>
        void FitToScreen()
        {
            JellyFit.Solve(_field.HalfHeight, _bellRadius, HazeSpan, FieldSpan,
                           _targetY, JellyTentacles.Links,
                           out _targetY, out _visualFit);
        }

        SpriteRenderer AddLayer(string layerName, Sprite sprite, int order, Transform parent)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = order;
            return sr;
        }

        SpriteRenderer CreateFieldRing(string ringName)
        {
            var go = new GameObject(ringName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.ElectricRing();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = SortingOrders.JellyField;
            sr.color = (SpriteFactory.JellyBio1 * 0.55f).WithAlpha(0.07f);
            return sr;
        }

        public void Spawn()
        {
            gameObject.SetActive(true);
            CurrentPhase = Phase.Enter;
            float column = _field.PlayHalfWidth * 2f;
            _bellRadius = column * _cfg.jellyBellSpan;
            _fieldRadius = column * _cfg.jellyFieldSpan;
            _hitRadius = _bellRadius * _cfg.jellyHitFraction;

            MaxHp = BossTuning.Hp(Mathf.Max(1, _cfg.jellyHp));
            Hp = MaxHp;
            _timer = 0f;
            _shootTimer = 1.4f;
            _shotCounter = 0;
            _breath = 0f;
            _glowLevel = 0.35f;
            _glowWant = 0.35f;
            _squash = 0f;
            _squashWant = 0f;
            _tint.Reset();
            _fieldAlpha = 0.07f;
            _eyeAim = 0f;
            _telegraph = 0f;
            _veinsShown = 0f;
            _aimShown = 0f;
            if (_veins != null) _veins.enabled = false;
            if (_aimRing != null) _aimRing.enabled = false;

            // КОРНЕВАЯ ПРИЧИНА «босс виден не целиком».
            //
            // TickDying уводит _bodyAlpha в ноль, Despawn его не трогает, а Spawn
            // раньше не сбрасывал. Во втором бою (и в каждом следующем) купол,
            // свечение и глаз рисовались с альфой ноль — на экране оставались
            // только щупальца и дымка, которым альфу возвращали отдельно.
            // Подтверждено кадром: _audit_scratch/rep/rep_108_seq_j_again2_f60.png
            _bodyAlpha = 1f;
            _age = 0f;
            _reach = 0f;
            if (_ripple != null) _ripple.enabled = false;

            _body.localScale = Vector3.one;
            _body.localPosition = Vector3.zero;
            _arms.ResetState();
            _arms.SetVisible(true);
            _arms.SetAlpha(1f);
            _haze.enabled = true;
            _targetY = _field.YFromTop(_cfg.jellyHoverFactor);
            FitToScreen();

            transform.position = new Vector3(0f, _field.Top + _bellRadius * 2f, 0f);
            transform.localScale = Vector3.one * (_bellRadius * 2f);
            _body.localScale = Vector3.one * _visualFit;

            // Дымка шире купола, но кольцом: середину она не закрывает, иначе
            // за ней пропали бы снаряды.
            _haze.transform.localScale = Vector3.one * (HazeSpan * _visualFit);

            ApplyBodyColour();

            // масштаб ауры задаётся относительно уже отмасштабированного родителя
            // масштаб задаётся относительно уже отмасштабированного родителя
            // Кольца показывают радиус поля отталкивания и ужимаются вместе со
            // всем видом: само поле (_fieldRadius) при этом не меняется — его
            // размер гейплейный.
            float auraLocal = FieldSpan * _visualFit;
            foreach (var ring in new[] { _aura, _aura2 })
            {
                ring.transform.localScale = Vector3.one * auraLocal;
                ring.transform.localRotation = Quaternion.identity;
                ring.color = (SpriteFactory.JellyBio1 * 0.55f).WithAlpha(0.07f);
                ring.enabled = true;
            }

            ClearProjectiles();
        }

        public void Tick(float dt)
        {
            switch (CurrentPhase)
            {
                case Phase.Enter: TickEnter(dt); break;
                case Phase.Fight: TickFight(dt); break;
                case Phase.Dying: TickDying(dt); break;
            }

            TickProjectiles(dt);
            TickTelegraphLook(dt);
            TickImpacts(dt);
        }

        void TickEnter(float dt)
        {
            var p = transform.position;
            p.y = Mathf.Lerp(p.y, _targetY, 1f - Mathf.Exp(-2.6f * dt));
            transform.position = p;
            if (Mathf.Abs(p.y - _targetY) < 0.05f) CurrentPhase = Phase.Fight;

            // Выход тоже живой: купол дышит, щупальца тянутся следом.
            TickLook(dt, 0f);
        }

        void TickFight(float dt)
        {
            _timer += dt;

            // Чем ниже HP, тем шире и быстрее мечется.
            float rage = 1f - (MaxHp > 0 ? Hp / (float)MaxHp : 0f);
            float amp = _field.PlayHalfWidth * _cfg.jellySwayAmplitude * (1f + rage * 0.25f);
            float speed = _cfg.jellySwaySpeed * (1f + rage * 0.35f);
            transform.position = new Vector3(Mathf.Sin(_timer * speed) * amp, _targetY, 0f);

            // поле отталкивания: вплотную к боссу стража не подпускаем
            _guardian.SetRepelZone(transform.position, _fieldRadius);

            // кольца крутятся навстречу и вспыхивают, когда страж упирается в поле
            // Поле отталкивания: видно, но не светит. Пульсация медленная —
            // 0.9 рад/с, то есть цикл за семь секунд, а не семь раз в секунду,
            // как было (Sin(_timer * 7f) — это уже мерцание). [КОД]
            // Флаг упора меняется покадрово: если тереть стража о поле, альфа
            // прыгала 0.07 ↔ 0.2 каждый кадр. Теперь она едет к цели не быстрее
            // FieldFade на весь размах.
            float auraWant = _guardian.RepelledThisFrame
                ? 0.2f
                : 0.07f + Mathf.Sin(_timer * 0.9f) * 0.02f;
            _fieldAlpha = Mathf.MoveTowards(_fieldAlpha, auraWant, (0.2f - 0.05f) / FieldFade * dt);
            float auraAlpha = _fieldAlpha;

            _aura.transform.Rotate(0f, 0f, 22f * dt);
            _aura2.transform.Rotate(0f, 0f, -31f * dt);
            // Цвет приглушён умножением: сам спрайт кольца почти белый, и
            // даже на малой альфе он давал светлый ореол вокруг босса. [ТЕСТ]
            _aura.color = (SpriteFactory.JellyBio1 * 0.55f).WithAlpha(auraAlpha);
            _aura2.color = (SpriteFactory.JellyBio3 * 0.5f).WithAlpha(auraAlpha * 0.6f);

            _shootTimer -= dt;

            // Телеграф. Он НЕ сдвигает выстрел: показ начинается за
            // TelegraphTime до него и укладывается в тот же интервал, поэтому
            // ни темп, ни урон не меняются. [КОД]
            _telegraph = _shootTimer <= TelegraphTime
                ? Mathf.Clamp01(1f - _shootTimer / TelegraphTime)
                : 0f;

            if (_shootTimer <= 0f)
            {
                Attack();
                float interval = Mathf.Lerp(_cfg.jellyShootIntervalMax, _cfg.jellyShootIntervalMin, rage);
                _shootTimer = interval * UnityEngine.Random.Range(0.85f, 1.15f);
                _telegraph = 0f;

                // Выстрел состоялся — щупальца тянутся вслед снаряду и
                // расслабляются за 0.6 с.
                _reach = 1f;
            }

            // Перед атакой: купол втягивается и темнеет, глаз разгорается,
            // щупальца подтягиваются. Всё три — медленно и одновременно, чтобы
            // игрок успел прочитать намерение.
            _squashWant = _telegraph;
            _glowWant = Mathf.Lerp(0.3f, 1f, _telegraph);
            _arms.SetPull(Mathf.Lerp(1f, 0.72f, _telegraph));
            _arms.SetTremble(_telegraph);
            if (_telegraph > 0f) _aimAt = _bubble.Center;

            TickLook(dt, rage);
        }

        /// <summary>
        /// Внешний вид: дыхание, сжатие купола, свет глаза, взгляд и щупальца.
        ///
        /// Все величины едут сюда через ограниченную скорость, и это главное:
        /// ни одна из них не может измениться скачком, сколько бы событий ни
        /// пришло в один кадр.
        /// </summary>
        void TickLook(float dt, float rage)
        {
            _breath += dt / BreathPeriod;
            _age += dt;

            // После выстрела щупальца ещё мгновение тянутся вслед за снарядом.
            _reach = Mathf.MoveTowards(_reach, 0f, dt / 0.6f);

            // Сжатие: к цели за 0.35 с. Купол приплющивается по вертикали и
            // чуть раздаётся вширь — так сокращается настоящая медуза.
            _squash = Mathf.MoveTowards(_squash, _squashWant, dt / 0.35f);
            float pulse = Mathf.Sin(_breath * Mathf.PI * 2f) * 0.5f + 0.5f;
            float sy = (1f - _squash * 0.16f - pulse * 0.04f) * _visualFit;
            float sx = (1f + _squash * 0.08f + pulse * 0.03f) * _visualFit;
            _body.localScale = new Vector3(sx, sy, 1f);

            // Свет: разгорается не быстрее GlowRise, гаснет так же плавно.
            _glowLevel = Mathf.MoveTowards(_glowLevel, _glowWant, dt / GlowRise);
            _tint.Tick(dt);

            ApplyBodyColour();

            // Рябь: ползёт сверху вниз по куполу и видна только на подготовке.
            // Появляется и уходит вместе с телеграфом, то есть не быстрее
            // 0.35 с — отдельного резкого включения у неё нет.
            if (_ripple != null)
            {
                float r = _squash;
                _ripple.enabled = r > 0.02f;
                if (_ripple.enabled)
                {
                    _ripple.transform.localPosition = new Vector3(
                        0f, Mathf.Repeat(_age * 0.22f, 0.5f) - 0.25f, 0f);
                    _ripple.color = SpriteFactory.JellyOrgan.WithAlpha(r * 0.55f * _bodyAlpha);
                }
            }

            // Взгляд: глаз медленно доворачивается к пузырьку и сдвигается в
            // ту же сторону — поворот без сдвига читается как косоглазие.
            Vector2 to = _bubble != null ? _bubble.Center - (Vector2)transform.position : Vector2.down;
            float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            _eyeAim = Mathf.MoveTowardsAngle(_eyeAim, want, (70f + 90f * _telegraph) * dt);

            var dir = new Vector2(Mathf.Cos(_eyeAim * Mathf.Deg2Rad), Mathf.Sin(_eyeAim * Mathf.Deg2Rad));
            _eye.transform.localPosition = new Vector3(dir.x * 0.07f, 0.1f + dir.y * 0.05f, 0f);
            _eye.transform.localRotation = Quaternion.Euler(0f, 0f, _eyeAim + 90f);

            // Зрачок сужается, глаз расширяется. Отдельного спрайта для этого
            // не нужно: по вертикали глаз растёт, по горизонтали ужимается — и
            // вертикальная щель внутри сужается вместе с ним.
            float dilate = _glowLevel;
            _eye.transform.localScale = new Vector3(
                0.33f * Mathf.Lerp(1f, 0.9f, dilate),
                0.33f * Mathf.Lerp(1f, 1.14f, dilate), 1f);

            // Взгляд фиксируется: на подготовке глаз доворачивается вдвое
            // быстрее, иначе к выстрелу он не успевает поймать игрока.
            // На подготовке тонкие нити уже тянутся к цели, после выстрела — вслед.
            _arms.SetReach(Mathf.Max(_reach, _telegraph * 0.55f), _eyeAim);

            _arms.Tick(dt);
        }

        /// <summary>
        /// Цвет всех слоёв тела в одном месте.
        ///
        /// **Подмены спрайта нет.** Раньше попадание меняло картинку на светлую
        /// копию в тот же кадр — мгновенный скачок яркости на всю площадь босса.
        /// Теперь поверх купола ложится его силуэт с подмесом не больше 25 %,
        /// нарастание 0.08 с, спад 0.28 с (<see cref="HitTint"/>). [КОД]
        /// </summary>
        void ApplyBodyColour()
        {
            // Чем ниже HP, тем темнее и холоднее тело: смена фазы читается по
            // цвету и форме, а не по вспышке.
            float rage = MaxHp > 0 ? 1f - Hp / (float)MaxHp : 0f;

            Color bell = Color.Lerp(Color.white, SpriteFactory.JellyBio3, rage * 0.25f);
            // Перед выстрелом купол темнеет: жилки на нём читаются лучше.
            bell = Color.Lerp(bell, bell * 0.8f, _veinsShown);
            bell.a = _bodyAlpha;
            _sr.color = bell;

            // Свечение: дыхание плюс разгорание. Никогда не доходит до единицы.
            float breathe = 0.78f + 0.22f * (Mathf.Sin(_breath * Mathf.PI * 2f) * 0.5f + 0.5f);
            float glow = Mathf.Clamp01(_glowLevel * breathe) * 0.72f;
            _glow.color = Color.white.WithAlpha(glow * _bodyAlpha);

            // Глаз разгорается вместе с телеграфом, но тускнеет не до нуля:
            // медуза смотрит всегда.
            float eye = Mathf.Lerp(0.55f, 1f, _glowLevel);
            _eye.color = new Color(eye, eye, eye, _bodyAlpha);

            // Дымка еле заметна: её задача — холодный ободок темноты, а не
            // облако. На прошлой сборке она вместе с кольцами поля закрывала
            // боссa и снаряды. [ТЕСТ]
            _haze.color = SpriteFactory.JellyDeep.WithAlpha(0.16f * _bodyAlpha);
        }

        float _bodyAlpha = 1f;

        /// <summary>
        /// Смерть: медленное тускнение, всплытие и обмякшие щупальца. Ни вспышки,
        /// ни белого — тело просто перестаёт светиться и уходит вверх.
        /// </summary>
        void TickDying(float dt)
        {
            _timer += dt;
            _guardian.ClearRepelZone();
            _aura.enabled = false;
            _aura2.enabled = false;

            // Всплывает медленнее прежнего: угасание стало вдвое длиннее.
            transform.position += Vector3.up * (0.4f * dt);

            float k = Mathf.Clamp01(_timer / DeathTime);
            _bodyAlpha = 1f - Mathf.SmoothStep(0f, 1f, k);

            // Свет гаснет быстрее тела: сначала потухает, потом растворяется.
            _glowWant = 0f;
            _glowLevel = Mathf.MoveTowards(_glowLevel, 0f, dt / 0.9f);

            // Купол обмякает, щупальца распускаются и отстают сильнее.
            _squash = Mathf.MoveTowards(_squash, -0.4f, dt / 1.2f);
            _body.localScale = new Vector3((1f + k * 0.1f) * _visualFit,
                                           (1f - k * 0.18f) * _visualFit, 1f);
            _arms.SetPull(Mathf.Lerp(1f, 1.15f, k));
            _arms.SetAlpha(_bodyAlpha);
            _arms.Tick(dt * 0.6f);

            _breath += dt / BreathPeriod;
            ApplyBodyColour();

            if (_timer >= DeathTime)
            {
                CurrentPhase = Phase.Done;
                Defeated?.Invoke(transform.position);
                Despawn();
            }
        }

        /// <summary>
        /// Три паттерна: одиночный прицельный, веер из трёх и круговая волна на низком HP.
        /// Одним стражем весь веер не перекрыть — приходится выбирать, что отбивать.
        /// </summary>
        void Attack()
        {
            _shotCounter++;

            Vector2 origin = (Vector2)transform.position + new Vector2(0f, -_bellRadius * 0.5f);
            Vector2 aim = (_bubble.Center - origin).normalized;
            if (aim.sqrMagnitude < 0.0001f) aim = Vector2.down;

            bool halfDead = MaxHp > 0 && Hp <= MaxHp * 0.5f;
            int ringEvery = Mathf.Max(1, _cfg.bossRingEveryNShots);
            int spreadEvery = Mathf.Max(1, _cfg.bossSpreadEveryNShots);

            if (halfDead && _shotCounter % ringEvery == 0)
            {
                int count = Mathf.Max(3, _cfg.bossRingCount);
                float step = 360f / count;
                float offset = UnityEngine.Random.Range(0f, step);
                for (int i = 0; i < count; i++)
                    Fire(origin, Quaternion.Euler(0f, 0f, offset + step * i) * Vector2.down, 0.8f);
            }
            else if (_shotCounter % spreadEvery == 0)
            {
                Fire(origin, aim, 1f);
                Fire(origin, Quaternion.Euler(0f, 0f, _cfg.bossSpreadAngle) * aim, 1f);
                Fire(origin, Quaternion.Euler(0f, 0f, -_cfg.bossSpreadAngle) * aim, 1f);
            }
            else
            {
                Fire(origin, aim, 1.12f);
            }
        }

        void Fire(Vector2 origin, Vector2 direction, float speedScale)
        {
            var p = RentProjectile();
            p.Activate(origin, direction.normalized * (_cfg.jellyBoltSpeed * speedScale), _cfg.jellyBoltRadius);
            // Свечение на острие — 0.3 диаметра стража.
            p.UseStormLook(0.3f * _guardian.Radius * 2f);
            _live.Add(p);
        }

        // ------------------------------------------------------ вид разрядов

        /// <summary>
        /// Жилки, кольцо прицела. Жилки разгораются вместе с телеграфом (0.75 с) и
        /// гаснут за 0.35 с; свет «бежит» из макушки: слой растёт от 0.7 до 1.
        /// Кольцо сужается к точке удара и после выстрела гаснет за 0.3 с.
        /// </summary>
        void TickTelegraphLook(float dt)
        {
            if (_veins == null) return;
            _veinsShown = Mathf.MoveTowards(_veinsShown, CurrentPhase == Phase.Fight ? _telegraph : 0f, dt / 0.35f);
            _veins.enabled = _veinsShown > 0.004f && _bodyAlpha > 0.01f;
            if (_veins.enabled)
            {
                _veins.color = SpriteFactory.JellyBio1.WithAlpha(0.42f * _veinsShown * _bodyAlpha);
                float spread = Mathf.Lerp(0.7f, 1f, _veinsShown);
                _veins.transform.localScale = new Vector3(spread, spread, 1f);
                _veins.transform.localPosition = new Vector3(0f, (1f - spread) * 0.25f, -0.001f);
            }

            float want = CurrentPhase == Phase.Fight ? Mathf.SmoothStep(0f, 1f, _telegraph) : 0f;
            _aimShown = Mathf.MoveTowards(_aimShown, want, dt / 0.3f);
            if (_telegraph > 0f) _aimScale = Mathf.Lerp(2.2f, 0.85f, _telegraph);
            _aimRing.enabled = _aimShown > 0.004f;
            if (_aimRing.enabled)
            {
                _aimRing.transform.position = new Vector3(_aimAt.x, _aimAt.y, 0f);
                _aimRing.transform.localScale = Vector3.one * _aimScale;
                _aimRing.color = SpriteFactory.StormBodyHostile.WithAlpha(AimRingAlpha * _aimShown);
            }
        }

        /// <summary>
        /// Всплеск попадания: 9 искр и мягкое кольцо, раскрывающееся за 0.35 с
        /// (прозрачность 0 → 0.5 за 0.06 с, потом к нулю). Белого нет.
        /// </summary>
        void Impact(Vector2 at, bool reflected)
        {
            Color core = reflected ? SpriteFactory.StormCoreReflected : SpriteFactory.StormCoreHostile;
            _fx?.Burst(at, core, 9, 0.4f, 1.6f, 0.03f, 0.06f);
            int slot = 0;
            for (int i = 1; i < _impactAge.Length; i++)
                if (_impactAge[i] > _impactAge[slot]) slot = i;
            _impactAge[slot] = 0f;
            _impactRings[slot].transform.position = new Vector3(at.x, at.y, 0f);
            _impactRings[slot].color = (reflected ? SpriteFactory.StormBodyReflected : SpriteFactory.StormBodyHostile).WithAlpha(0f);
            _impactRings[slot].enabled = true;
        }

        void TickImpacts(float dt)
        {
            if (_impactRings == null) return;
            for (int i = 0; i < _impactRings.Length; i++)
            {
                if (_impactAge[i] >= ImpactTime) { if (_impactRings[i].enabled) _impactRings[i].enabled = false; continue; }
                _impactAge[i] += dt;
                float k = Mathf.Clamp01(_impactAge[i] / ImpactTime);
                const float rise = 0.06f / ImpactTime;
                float a = k < rise ? Mathf.SmoothStep(0f, 1f, k / rise)
                                   : 1f - Mathf.SmoothStep(0f, 1f, (k - rise) / (1f - rise));
                var sr = _impactRings[i];
                sr.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 0.95f, k);
                var c = sr.color;
                c.a = 0.5f * a;
                sr.color = c;
            }
        }

        /// <summary>
        /// Шлейф и искры. Шлейф тонкий и вязкий — частицы с хвоста, живут 0.4 с;
        /// искры с острия, живут 0.25–0.4 с. Чем больше разрядов в полёте, тем
        /// реже каждый сыплет: в волне из восьми частиц не больше, чем у пары.
        /// </summary>
        void EmitTrails(float dt)
        {
            if (_fx == null || _live.Count == 0) return;
            float load = Mathf.Max(1f, _live.Count / 3f);
            _trailClock += dt;
            _sparkClock += dt;
            bool trail = _trailClock >= 0.12f * load;
            bool spark = _sparkClock >= 0.24f * load;
            if (trail) _trailClock = 0f;
            if (spark) _sparkClock = 0f;
            if (!trail && !spark) return;

            for (int i = 0; i < _live.Count; i++)
            {
                var p = _live[i];
                if (!p.Storm || p.Fading || p.Velocity.sqrMagnitude < 0.0001f) continue;
                Vector2 dir = p.Velocity.normalized;
                float half = p.Radius * 2f;
                Color body = p.Reflected ? SpriteFactory.StormBodyReflected : SpriteFactory.StormBodyHostile;
                Color core = p.Reflected ? SpriteFactory.StormCoreReflected : SpriteFactory.StormCoreHostile;
                if (trail)
                    _fx.Emit(p.Center - dir * half, -p.Velocity * 0.15f + UnityEngine.Random.insideUnitCircle * 0.1f,
                             body, 0.4f, 0.05f, 0.4f, SortingOrders.Projectile - 1);
                if (spark)
                    _fx.Emit(p.Center + dir * half, UnityEngine.Random.insideUnitCircle.normalized * 1.0f,
                             core, UnityEngine.Random.Range(0.25f, 0.4f), 0.03f, 0.7f);
            }
        }

        void TickProjectiles(float dt)
        {
            // Погасающие после удара: стоят на месте и уходят в пул по готовности.
            for (int i = _fading.Count - 1; i >= 0; i--)
            {
                var f = _fading[i];
                f.Tick(dt, _field);
                if (f.FadedOut)
                {
                    _fading.RemoveAt(i);
                    RecycleProjectile(f);
                }
            }

            EmitTrails(dt);

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                // Колбэки ниже (BubbleHit -> game over) могут почистить список прямо во время цикла.
                if (i >= _live.Count) continue;

                var p = _live[i];
                if (p.Reflected && CurrentPhase == Phase.Fight)
                    p.SteerTowards(transform.position, _cfg.jellyReflectHoming, dt);

                bool alive = p.Tick(dt, _field);

                if (alive && !p.Reflected)
                {
                    // перехват стражем — только если он заряжен
                    if (_guardian.CanDeflect &&
                        Vector2.Distance(p.Center, _guardian.Center) < p.Radius + _guardian.Radius)
                    {
                        Vector2 toBoss = (Vector2)transform.position - p.Center;
                        if (toBoss.sqrMagnitude < 0.0001f) toBoss = Vector2.up;
                        p.Reflect(toBoss, _cfg.jellyReflectSpeed);
                        _guardian.NotifyDeflect();
                        // лёгкий всплеск искр в точке отражения, без вспышки
                        _fx?.Burst(p.Center, SpriteFactory.StormCoreReflected, 8, 0.3f, 1.3f, 0.03f, 0.06f);
                        GuardianBlocked?.Invoke(p.Center);
                    }
                    // попадание по пузырьку
                    else if (Vector2.Distance(p.Center, _bubble.Center) < p.Radius + _bubble.Radius)
                    {
                        Impact(p.Center, false);
                        BubbleHit?.Invoke(p.Center);
                        // проигрыш мог уже всё убрать в пул — RetireStruck это переживёт
                        RetireStruck(p);
                        continue;
                    }
                }
                else if (alive && p.Reflected && CurrentPhase == Phase.Fight)
                {
                    // отражённый снаряд бьёт босса
                    if (Vector2.Distance(p.Center, transform.position) < p.Radius + _hitRadius)
                    {
                        Impact(p.Center, true);
                        RetireStruck(p);
                        ApplyDamage();
                        continue;
                    }
                }

                if (!alive)
                {
                    int idx = _live.IndexOf(p);
                    if (idx >= 0) _live.RemoveAt(idx);
                    RecycleProjectile(p);
                }
            }

            // Босс погиб в этом кадре — гасим оставшиеся снаряды уже вне цикла,
            // чтобы не менять список во время итерации.
            if (CurrentPhase == Phase.Dying && _live.Count > 0) ClearProjectiles();
        }

        void ApplyDamage()
        {
            Hp = Mathf.Max(0, Hp - 1);

            // Плавный подмес вместо подмены спрайта, не чаще раза в 0.45 с.
            _tint.Hit();
            Damaged?.Invoke(transform.position);

            if (Hp <= 0)
            {
                CurrentPhase = Phase.Dying;
                _timer = 0f;
                _bodyAlpha = 1f;
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Помещается ли вид босса в кадр и все ли слои включены. Строкой —
        /// дев-панели и проверке по форматам экрана.
        /// </summary>
        public string DebugFit()
        {
            if (_field == null || _sr == null) return "не создан";

            float scale = _bellRadius * 2f * _visualFit;
            float top = transform.position.y + VisualUp * scale;
            float bottom = transform.position.y - VisualDown * scale;

            bool inside = top <= _field.Top + 0.001f && bottom >= _field.Bottom - 0.001f;

            int off = 0;
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
                if (sr.name != "HitTint" && (!sr.enabled || sr.color.a < 0.02f)) off++;   // силуэт подмеса гаснет между ударами

            return string.Format(
                "вид {0:0.00}…{1:0.00} при экране {2:0.00}…{3:0.00} — {4}; ужим x{5:0.00}; тусклых слоёв {6}",
                bottom, top, _field.Bottom, _field.Top,
                inside ? "влезает" : "НЕ ВЛЕЗАЕТ", _visualFit, off);
        }

        /// <summary>Правда, если вид целиком в кадре.</summary>
        public bool DebugFits()
        {
            if (_field == null) return false;
            float scale = _bellRadius * 2f * _visualFit;
            return transform.position.y + VisualUp * scale <= _field.Top + 0.001f
                && transform.position.y - VisualDown * scale >= _field.Bottom - 0.001f;
        }

        /// <summary>Прозрачность тела. Ноль означает невидимого босса.</summary>
        public float DebugBodyAlpha => _bodyAlpha;

        /// <summary>
        /// Куда поставил бы стража игрок: на путь ближайшего враждебного разряда,
        /// который летит уже не меньше 0.3 с (успел его увидеть). Для проверки в Unity.
        /// </summary>
        public bool DevBotTarget(out Vector2 target)
        {
            target = default;
            float best = float.MaxValue;
            Vector2 b = _bubble.Center;
            for (int i = 0; i < _live.Count; i++)
            {
                var p = _live[i];
                if (p.Reflected || p.Fading || p.Age < 0.3f) continue;
                if (Vector2.Dot(p.Velocity, b - p.Center) <= 0f) continue;   // мимо, улетает
                float d = Vector2.Distance(p.Center, b);
                if (d < best) { best = d; target = b + (p.Center - b).normalized * Mathf.Min(1.1f, d); }
            }
            return best < float.MaxValue;
        }
#endif

        BossProjectile RentProjectile()
        {
            if (_pool.Count > 0)
            {
                var pooled = _pool.Pop();
                if (pooled != null) return pooled;
            }
            var go = new GameObject("BossProjectile");
            go.transform.SetParent(_projectileRoot, false);
            var proj = go.AddComponent<BossProjectile>();
            proj.Setup();
            return proj;
        }

        /// <summary>Разряд попал: снять с живых и дать ему погаснуть, а не исчезнуть за кадр.</summary>
        void RetireStruck(BossProjectile p)
        {
            int idx = _live.IndexOf(p);
            if (idx >= 0) _live.RemoveAt(idx);
            if (p == null || p.Pooled) return;
            if (!p.Storm) { RecycleProjectile(p); return; }
            p.BeginFadeOut();
            if (!_fading.Contains(p)) _fading.Add(p);
        }

        void RecycleProjectile(BossProjectile p)
        {
            if (p == null || p.Pooled) return;
            p.Pooled = true;
            p.Deactivate();
            _pool.Push(p);
        }

        /// <summary>Гасим молнии: те, что летели до смерти игрока, к нему не относятся.</summary>
        public void Interrupt() => ClearProjectiles();

        public void ClearProjectiles()
        {
            for (int i = 0; i < _live.Count; i++) RecycleProjectile(_live[i]);
            _live.Clear();
            for (int i = 0; i < _fading.Count; i++) RecycleProjectile(_fading[i]);
            _fading.Clear();
        }

        public void Despawn()
        {
            ClearProjectiles();
            if (_guardian != null) _guardian.ClearRepelZone();
            if (_aura != null) _aura.enabled = false;
            if (_aura2 != null) _aura2.enabled = false;
            if (_haze != null) _haze.enabled = false;
            if (_arms != null) _arms.SetVisible(false);
            if (_aimRing != null) _aimRing.enabled = false;
            if (_impactRings != null) foreach (var r in _impactRings) r.enabled = false;
            gameObject.SetActive(false);
            CurrentPhase = Phase.Done;
        }
    }
}
