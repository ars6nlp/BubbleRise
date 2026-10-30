using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Пузырёк-игрок. Зафиксирован по X в центре, по Y — на заданной доле высоты экрана.
    /// Игрок им напрямую не управляет: только покачивается, теряет жизни и мигает.
    /// </summary>
    public class BubbleController : MonoBehaviour
    {
        public float Radius { get; private set; }
        public int Lives { get; private set; }
        public bool IsInvulnerable => _invuln > 0f;
        /// <summary>Режим бессмертия из дев-панели. В релизной сборке включить неоткуда.</summary>
        public bool Invincible { get; set; }

        /// <summary>Оттенок выбранного скина. Умножается на цвет спрайта.</summary>
        public Color Tint { get; set; } = Color.white;

        /// <summary>
        /// Надеть скин. Трофейные скины меняют не только оттенок, но и рисунок,
        /// поэтому цвета одного мало.
        /// </summary>
        /// <summary>
        /// Множитель масштаба от скина. Отдельно от радиуса и от сплющивания при
        /// ударе: скин меняет только вид, и права сделать пузырёк меньше хитбокса
        /// у него нет — иначе дорогой скин был бы ещё и легче в игре.
        /// </summary>
        public Vector2 SkinScale { get; set; } = Vector2.one;

        /// <summary>
        /// Надеть скин: оболочка и начинка.
        ///
        /// Два слоя, а не один. Начинка (материал) лежит под стеклом и медленно
        /// крутится — отсюда ощущение, что внутри шарика что-то течёт. Оболочка со
        /// своим бликом остаётся неподвижной: блик — это отражение, и вращать его
        /// вместе с содержимым значит выдавать, что перед нами плоская картинка.
        ///
        /// Оттенок достаётся начинке, если она есть. Красить ещё и стекло поверх
        /// цветного материала — значит класть цвет дважды и гасить сам материал.
        /// </summary>
        public void ApplySkin(Sprite sprite, Sprite core, Color tint)
        {
            Tint = tint;
            if (_sr == null) return;

            _sr.sprite = sprite != null ? sprite : SpriteFactory.Bubble();

            if (_core != null)
            {
                _core.sprite = core;
                _core.enabled = core != null;
                _core.color = tint;
            }
            _coreColor = tint;

            // Со стеклом поверх материала цвет уже положен начинкой. Этот же цвет
            // берёт неуязвимость: раньше она красила стекло оттенком поверх
            // цветной начинки, то есть клала цвет дважды.
            _glassColor = core != null ? Color.white : tint;
            _sr.color = _glassColor;
        }
        /// <summary>Центр пузырька без визуального покачивания — по нему считаются столкновения.</summary>
        public Vector2 Center => _anchor;

        SpriteRenderer _sr;
        SpriteRenderer _core;
        SpriteRenderer _aura;

        // Отклик на урон без вспышек (A-08): кольцо Coral и короткое сжатие.
        // Неуязвимость видна у любого скина (A-09): пунктирное кольцо и
        // полупрозрачная начинка, а не только стекло.
        SpriteRenderer _hurtRing;
        SpriteRenderer _guardRing;
        Color _glassColor = Color.white;
        Color _coreColor = Color.white;
        float _hurtAge = HurtTime;
        float _invulnShown;

        /// <summary>Кольцо урона живёт столько, с: α 0 → 0.6 за 0.06 с, дальше к нулю.</summary>
        const float HurtTime = 0.35f;
        /// <summary>Пик сжатия при уроне — 8 % через 0.12 с, возврат к 0.3 с.</summary>
        const float HurtSquash = 0.08f;
        const float HurtSquashPeak = 0.12f;
        const float HurtSquashTime = 0.3f;
        /// <summary>Неуязвимость проявляется и гаснет за это время, а не за кадр.</summary>
        const float InvulnFade = 0.15f;
        float _coreAngle;
        GameConfig _cfg;
        Playfield _field;
        Vector2 _anchor;
        float _wobble;
        float _invuln;

        public void Init(GameConfig cfg, Playfield field)
        {
            _cfg = cfg;
            _field = field;
            Radius = PlayerSize.Bubble(cfg);

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sprite = SpriteFactory.Bubble();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Bubble;

            // Аура — самый нижний слой: светится из-под тела, а не поверх него.
            var auraObject = new GameObject("Aura");
            auraObject.transform.SetParent(transform, false);
            _aura = auraObject.AddComponent<SpriteRenderer>();
            _aura.sharedMaterial = SpriteFactory.Unlit;
            _aura.sprite = SpriteFactory.SoftDot();
            _aura.sortingOrder = SortingOrders.Bubble - 2;
            _aura.enabled = false;

            // Начинка — отдельный объект, потому что крутится она, а стекло нет.
            var coreObject = new GameObject("Core");
            coreObject.transform.SetParent(transform, false);
            _core = coreObject.AddComponent<SpriteRenderer>();
            _core.sharedMaterial = SpriteFactory.Unlit;
            _core.sortingOrder = SortingOrders.Bubble - 1;
            _core.enabled = false;

            _hurtRing = NewRing("HurtRing", SpriteFactory.Ring());
            _guardRing = NewRing("InvulnRing", SpriteFactory.DashedRing());

            transform.localScale = Vector3.one * (Radius * 2f);

            ResetState();
        }


        /// <summary>
        /// Аура скина: мягкое свечение под телом. Отдельный слой, потому что ею
        /// управляет SkinFxRuntime — она дышит по своему закону, а не вместе с
        /// пузырьком, и гасится, когда скин без ауры.
        /// </summary>
        /// <summary>
        /// Спрятать на время модального экрана.
        ///
        /// Гасим рендереры, а не сам объект: объект продолжает получать Tick от
        /// GameManager, и выключение сбило бы его положение и фазу анимации. Под
        /// плотной подложкой меню этого всё равно не видно, но полагаться только
        /// на прозрачность подложки не стоит — один полупрозрачный элемент
        /// интерфейса, и свечение снова проступит.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_sr != null) _sr.enabled = visible;
            if (_core != null) _core.enabled = visible && _core.sprite != null;
            if (_aura != null) _aura.enabled = visible && _auraOn;
            if (!visible && _hurtRing != null) _hurtRing.enabled = false;
            if (!visible && _guardRing != null) _guardRing.enabled = false;
        }

        bool _auraOn;

        public void SetAura(Color color, float intensity)
        {
            if (_aura == null) return;

            bool on = intensity > 0.01f;
            _auraOn = on;
            _aura.enabled = on;
            if (!on) return;

            _aura.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(intensity));

            // Радиус свечения урезан примерно на треть: при прежних 1.45 + 0.5·I
            // гало доходило до 1.8 диаметра тела и закрывало препятствия рядом с
            // шариком — ровно то, от чего игрок уворачивается.
            _aura.transform.localScale = Vector3.one * (1.1f + intensity * 0.25f);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Состояние слоёв для дев-оверлея: что включено и каким оно сейчас стало.
        ///
        /// Только для отладки, поэтому за препроцессором: в релизной сборке
        /// отражать внутренности наружу незачем, да и строки эти на русском и
        /// мимо словаря — игрок их видеть не должен ни при каких условиях.
        /// </summary>
        public string DebugLayers()
        {
            if (_sr == null) return "нет";
            return string.Format(
                "оболочка {0}  начинка {1}  аура {2}  масштаб {3:0.000}x{4:0.000}  угол {5:0}",
                _sr.enabled ? "вкл" : "ВЫКЛ",
                _core != null && _core.enabled ? "вкл" : "ВЫКЛ",
                _aura != null && _aura.enabled ? "вкл" : "ВЫКЛ",
                SkinScale.x, SkinScale.y, _coreAngle % 360f);
        }
#endif

        SpriteRenderer NewRing(string ringName, Sprite sprite)
        {
            var go = new GameObject(ringName);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sprite = sprite;
            sr.sortingOrder = SortingOrders.Bubble + 1;
            sr.enabled = false;
            return sr;
        }

        public void ResetState()
        {
            Lives = _cfg.startLives;
            _invuln = 0f;
            _invulnShown = 0f;
            _hurtAge = HurtTime;
            _wobble = 0f;
            SnapToAnchor();
            SetAlpha(1f);
            if (_core != null) _core.color = _coreColor;
            if (_hurtRing != null) _hurtRing.enabled = false;
            if (_guardRing != null) _guardRing.enabled = false;
        }

        public void SnapToAnchor()
        {
            _anchor = new Vector2(0f, _field.YFromBottom(_cfg.bubbleYFactor));
            transform.position = _anchor;
        }

        public void Tick(float dt)
        {
            _anchor = new Vector2(0f, _field.YFromBottom(_cfg.bubbleYFactor));
            _wobble += dt * _cfg.wobbleSpeed;

            float y = _anchor.y + Mathf.Sin(_wobble) * _cfg.wobbleAmplitude;
            transform.position = new Vector3(_anchor.x, y, 0f);

            // Лёгкое сжатие-растяжение: пузырёк «выгребает» вверх, а не висит на месте.
            float squash = Mathf.Sin(_wobble * 2f) * 0.045f;

            // Удар: сплющивается на 8 % и возвращается — вес удара без вспышки.
            float hurt = 0f;
            if (_hurtAge < HurtSquashTime)
            {
                hurt = _hurtAge < HurtSquashPeak
                    ? Mathf.SmoothStep(0f, 1f, _hurtAge / HurtSquashPeak)
                    : 1f - Mathf.SmoothStep(0f, 1f, (_hurtAge - HurtSquashPeak) / (HurtSquashTime - HurtSquashPeak));
            }
            float d = Radius * 2f;
            transform.localScale = new Vector3(d * (1f - squash) * (1f + hurt * HurtSquash * 0.5f) * SkinScale.x,
                                               d * (1f + squash) * (1f - hurt * HurtSquash) * SkinScale.y, 1f);
            TickHurtRing(dt);

            // Внутреннее движение материала: лава течёт, туманность вращается.
            // Медленно — под водой и под стеклом ничто не вертится быстро.
            if (_core != null && _core.enabled)
            {
                // Скорость и время — общие с витриной (SkinVisuals.CoreSpin).
                // Было 14 у пузырька, 20 у стража и 14 в витрине: начинка одного
                // скина вращалась с тремя разными скоростями. Время нескалированное,
                // иначе на паузе и на замедлении из дев-панели витрина и игра
                // расходятся ещё и по темпу.
                _coreAngle += Time.unscaledDeltaTime * SkinVisuals.CoreSpin;
                _core.transform.localRotation = Quaternion.Euler(0f, 0f, _coreAngle);

                // Компенсируем только НЕРАВНОМЕРНОСТЬ сжатия, а не сам размер.
                //
                // Здесь была причина, по которой материал в игре стоял, пока в
                // витрине дышал: деление на SkinScale целиком гасило и общий
                // масштаб тоже. Оболочка дышала, начинка под ней — нет, и скин в
                // бою выглядел мёртвым. В витрине такого слоя нет вовсе: там
                // масштабируется держатель, и оба слоя дышат вместе.
                //
                // Среднее геометрическое — ровно та часть масштаба, которая
                // одинакова по осям. Поделив на неё, оставляем дыхание и убираем
                // только перекос.
                float mean = Mathf.Sqrt(Mathf.Max(0.0001f, SkinScale.x * SkinScale.y));
                float sx = SkinScale.x > 0.01f ? mean / SkinScale.x : 1f;
                float sy = SkinScale.y > 0.01f ? mean / SkinScale.y : 1f;
                _core.transform.localScale = new Vector3(sx, sy, 1f);
            }

            if (_invuln > 0f) _invuln -= dt;

            // Было мигание семь раз в секунду между 1.0 и 0.15 — самый сильный
            // строб во всей игре, и держался он почти две секунды после каждого
            // удара. Теперь неуязвимость читается полупрозрачностью с очень
            // медленным дыханием: один цикл примерно за полторы секунды,
            // размах 0.1. Состояние так же очевидно, а глаза целы.
            //
            // Вход и выход — за InvulnFade, а не за кадр. И видно её у любого
            // скина (A-09): прежде прозрачнело только стекло, а у покупных скинов
            // его почти не видно за начинкой — поэтому полупрозрачна и начинка,
            // а вокруг лежит пунктирное кольцо.
            // Блок выполняется и в тот кадр, когда показ дошёл до нуля: иначе
            // стекло застревало бы на 0.98, а пунктир — на почти нулевой альфе.
            float shownBefore = _invulnShown;
            _invulnShown = Mathf.MoveTowards(_invulnShown, _invuln > 0f ? 1f : 0f, dt / InvulnFade);
            if (_invulnShown > 0f || shownBefore > 0f)
            {
                float breathe = 0.62f + Mathf.Sin(_invuln * 4f) * 0.1f;
                SetAlpha(Mathf.Lerp(1f, breathe, _invulnShown));
                if (_core != null && _core.enabled)
                    _core.color = _coreColor.WithAlpha(_coreColor.a * Mathf.Lerp(1f, 0.75f, _invulnShown));
                if (_guardRing != null)
                {
                    bool on = _invulnShown > 0.01f && _sr.enabled;
                    _guardRing.enabled = on;
                    if (on)
                    {
                        _guardRing.color = Palette.Foam.WithAlpha(0.35f * _invulnShown);
                        _guardRing.transform.localScale = Vector3.one * 1.22f;
                    }
                }
            }
        }

        /// <summary>Кольцо урона: Coral, раскрывается наружу и гаснет за HurtTime.</summary>
        void TickHurtRing(float dt)
        {
            if (_hurtRing == null) return;
            if (_hurtAge >= HurtTime)
            {
                if (_hurtRing.enabled) _hurtRing.enabled = false;
                _hurtAge = HurtTime;
                return;
            }

            _hurtAge += dt;
            float k = Mathf.Clamp01(_hurtAge / HurtTime);
            const float rise = 0.06f / HurtTime;
            float a = k < rise
                ? Mathf.SmoothStep(0f, 1f, k / rise)
                : 1f - Mathf.SmoothStep(0f, 1f, (k - rise) / (1f - rise));
            _hurtRing.enabled = _sr.enabled;
            _hurtRing.color = Palette.Coral.WithAlpha(0.6f * a);
            _hurtRing.transform.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.4f, k);
        }

        /// <summary>Возвращает true, если урон реально прошёл.</summary>
        public bool TakeDamage()
        {
            if (Invincible || _invuln > 0f) return false;
            Lives--;
            _invuln = InvulnerabilityTime;
            _hurtAge = 0f;
            return true;
        }

        /// <summary>
        /// Сколько длится неуязвимость. Берём большее из двух: сложность вправе
        /// добавить игроку времени, но не отнять то, что уже заложено в баланс.
        /// </summary>
        float InvulnerabilityTime
            => Mathf.Max(_cfg.invulnerabilityTime, Difficulty.Current.invulnerabilityTime);

        /// <summary>
        /// Хитбокс меньше рисунка: касание по самому краю не считается ударом.
        ///
        /// Отдельное свойство, а не уменьшенный <see cref="Radius"/>, потому что по
        /// радиусу считается ещё и размер спрайта и положение следа. Уменьшить его
        /// значило бы уменьшить сам пузырёк на экране — а просили обратного:
        /// чтобы видимое и засчитанное расходились в пользу игрока.
        /// </summary>
        public float HitRadius => Radius * Difficulty.Current.bubbleHitboxScale;

        public void AddLife(int amount = 1)
        {
            Lives = Mathf.Min(_cfg.startLives, Lives + amount);
            _invuln = Mathf.Max(_invuln, InvulnerabilityTime);
        }

        /// <summary>Есть ли куда возвращать жизнь. Нужно начислению за метры.</summary>
        public bool CanHeal => Lives > 0 && Lives < _cfg.startLives;

        void SetAlpha(float a)
        {
            var c = _glassColor;
            c.a = a;
            _sr.color = c;
        }
    }
}
