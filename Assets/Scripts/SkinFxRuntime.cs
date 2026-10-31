using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Оживляет выбранные скины: ведёт их следы, шевелит сами тела и показывает
    /// эффект при разрушении препятствия.
    ///
    /// Не MonoBehaviour: ничего своего в сцене у него нет, он только раздаёт работу
    /// уже существующим <see cref="FxManager"/>, пузырьку и стражу. Лишний объект в
    /// иерархии был бы здесь чистой декорацией.
    ///
    /// Всё под водой. Это не стилистическая оговорка, а правило, из которого растут
    /// все эффекты: лёгкое всплывает, тяжёлое тонет, резкое гаснет за доли секунды,
    /// а скорость любой частицы падает экспоненциально — вода вязкая. Поэтому даже
    /// «огненный» скин пускает не дым вверх клубами, а пар и горячие пузыри.
    /// </summary>
    public class SkinFxRuntime
    {
        FxManager _fx;
        BubbleController _bubble;
        GuardianOrb _guardian;

        /// <summary>
        /// Вид каждой половины целиком. Раньше здесь лежали россыпью оттенок,
        /// таблица эффектов и ОДИН материал на двоих — материал пузырька. Из-за
        /// него страж в бою получал чужую стихию: чужую ауру, чужой удар, чужой
        /// статус на препятствии. Витрина при этом показывала его собственную.
        /// </summary>
        SkinLook _bubbleLook;
        SkinLook _guardianLook;

        float _bubbleEmit;
        float _guardianEmit;
        float _phase;

        float _density = 1f;

        public void Init(FxManager fx, BubbleController bubble, GuardianOrb guardian, float trailDensity = 1f)
        {
            _fx = fx;
            _density = Mathf.Clamp(trailDensity, 0.3f, 1f);
            _bubble = bubble;
            _guardian = guardian;
            Refresh();
        }

        /// <summary>Перечитать выбор игрока. Зовётся после магазина и после загрузки облака.</summary>
        public void Refresh()
        {
            _bubbleLook = SkinVisuals.Bubble();
            _guardianLook = SkinVisuals.Guardian();
        }

        /// <summary>
        /// Эффект разрушения — от пузырька: это то, на что игрок смотрит весь
        /// заплыв, и его редкость решает, насколько зрелищно рассыпается цель.
        /// </summary>
        public Rarity ActiveRarity => _bubbleLook.Rarity;

        // ------------------------------------------------------------- такт

        public void Tick(float dt, bool playing)
        {
            _phase += dt;

            ApplyBodyMotion(_bubble, _bubbleLook);
            ApplyBodyMotion(_guardian, _guardianLook);
            ApplyAura();

            if (!playing) return;

            _bubbleEmit += dt * _bubbleLook.Fx.Rate * _density;
            while (_bubbleEmit >= 1f)
            {
                _bubbleEmit -= 1f;
                EmitTrail(_bubble.Center, _bubble.Radius, _bubbleLook);
            }

            // У стража след реже: он мечется по экрану, и равная плотность
            // превратила бы игровое поле в сплошную кашу из частиц.
            _guardianEmit += dt * _guardianLook.Fx.Rate * 0.45f * _density;
            while (_guardianEmit >= 1f)
            {
                _guardianEmit -= 1f;
                EmitTrail(_guardian.Center, _guardian.Radius, _guardianLook);
            }
        }

        /// <summary>
        /// Аура: свечение вокруг тела, своё у каждого материала.
        ///
        /// Появилась потому, что «уникальная анимация скина» сводилась к тому, что
        /// шарик дышал — чуть быстрее или чуть сильнее. Дыхание осталось, но теперь
        /// оно дополнение: характер скину задаёт то, что светится вокруг.
        /// </summary>
        void ApplyAura()
        {
            float t = _phase;

            // Аура своя у каждой половины: у пузырька от его материала, у стража
            // от его. Раньше обе брали материал пузырька, и купленный страж
            // светился чужой стихией — или не светился вовсе, если пузырёк
            // стеклянный.
            if (_bubble != null)
                _bubble.SetAura(_bubbleLook.Material.Aura, AuraIntensity(_bubbleLook, t));

            // У стража аура тусклее: он мечется по экрану и на полной яркости
            // засвечивал бы угрозы, которые игрок обязан видеть.
            if (_guardian != null)
                _guardian.SetAura(_guardianLook.Material.Aura,
                                  AuraIntensity(_guardianLook, t + 1.7f) * 0.65f);
        }

        /// <summary>
        /// Яркость ауры по стилю материала.
        ///
        /// Все колебания приведены к одному правилу: не быстрее одного-двух циклов
        /// в секунду и не глубже примерно четверти от среднего. «Crackle» был
        /// ступенькой — прыжок 0.3 → 0.72 три с лишним раза в секунду, то есть
        /// самый настоящий строб; «Heat» дрожал на 11 рад/с. Характер стилей от
        /// этого не пропал: он в форме кривой и в средней яркости, а не в частоте
        /// мерцания.
        /// </summary>
        public static float AuraIntensity(SkinLook look, float t)
        {
            float intensity;
            switch (look.Material.AuraStyle)
            {
                case AuraStyle.Halo:
                    intensity = 0.36f + Mathf.Sin(t * 1.4f) * 0.06f;
                    break;
                case AuraStyle.Mist:
                    intensity = 0.20f + Mathf.Sin(t * 0.9f) * 0.05f + Mathf.Sin(t * 1.7f) * 0.03f;
                    break;
                case AuraStyle.Crackle:
                    intensity = 0.30f + Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 1.6f)), 4f) * 0.14f;
                    break;
                case AuraStyle.Pulse:
                    intensity = 0.24f + Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 1.2f)), 3f) * 0.22f;
                    break;
                case AuraStyle.Shimmer:
                    intensity = 0.30f + Mathf.Sin(t * 2.4f) * 0.07f;
                    break;
                case AuraStyle.Frost:
                    intensity = 0.30f + Mathf.Sin(t * 0.8f) * 0.04f;   // почти ровное
                    break;
                case AuraStyle.Heat:
                    intensity = 0.38f + Mathf.Sin(t * 1.5f) * 0.08f;
                    break;
                default:
                    intensity = 0.25f + Mathf.Sin(t * 1.3f) * 0.05f;
                    break;
            }

            // Щадящий режим приглушает ауру, но не гасит: аура — часть базового
            // образа скина, по ней его узнают. Порог стоит на САМ множитель, а не
            // на результат: иначе он не делал бы ничего, пока множитель выше
            // порога, и перестал бы защищать ровно тогда, когда понадобится, —
            // если однажды приглушить свечение сильнее.
            return intensity * Mathf.Max(Visuals.Glow, Visuals.BaseFloor);
        }

        /// <summary>
        /// Анимация самого тела. Трогаем только визуальный масштаб — радиус
        /// столкновения живёт отдельным полем, и скин не имеет права его менять:
        /// иначе дорогой скин был бы ещё и легче в игре.
        /// </summary>
        void ApplyBodyMotion(BubbleController body, SkinLook look)
        {
            if (body == null) return;
            body.SkinScale = MotionScale(look.Fx, _phase) * look.VisualScale;
        }

        /// <summary>
        /// То же для стража. Сдвиг фазы: иначе пара дышала бы синхронно и
        /// читалась как один объект.
        /// </summary>
        void ApplyBodyMotion(GuardianOrb body, SkinLook look)
        {
            if (body == null) return;
            body.SkinScale = MotionScale(look.Fx, _phase + 1.7f) * look.VisualScale;
        }

        /// <summary>
        /// Масштаб тела для этой фазы. Публичный, потому что тем же движением живёт
        /// предпросмотр в магазине: иначе скин в витрине шевелился бы иначе, чем в
        /// игре, и выбирали бы одно, а получали другое.
        /// </summary>
        /// <summary>
        /// Потолок скорости вздрагивания Flicker: при 1.2 один толчок за 1.67 с —
        /// в пределах правила «пульс не чаще раза в 1.5–2 с».
        /// </summary>
        public const float FlickerMaxSpeed = 1.2f;

        public static Vector2 MotionScale(SkinFxDef fx, float phase)
        {
            float t = phase * fx.Speed;

            // Амплитуда срезана вдвое намеренно. Раньше вся «уникальность» скина
            // сводилась к тому, насколько сильно шарик раздувается, и читалось это
            // как один эффект на всех. Теперь характер задаёт аура и материал, а
            // движение — только живость, которая должна быть у всего под водой.
            // На «Тихо» размах вдвое меньше: Visuals.Motion раньше сюда не доходил (E-14).
            float a = fx.Amplitude * 0.5f * Visuals.Motion;

            switch (fx.Motion)
            {
                case SkinMotion.Breathe:
                    return Vector2.one * (1f + Mathf.Sin(t) * a);

                case SkinMotion.Bob:
                    // Качается по вертикали: вытягивается вверх и приседает.
                    return new Vector2(1f - Mathf.Sin(t) * a * 0.5f, 1f + Mathf.Sin(t) * a);

                case SkinMotion.Sway:
                    return new Vector2(1f + Mathf.Sin(t) * a, 1f - Mathf.Sin(t) * a * 0.5f);

                case SkinMotion.Throb:
                    // Резкое сжатие, мягкий возврат: пульс, а не синусоида.
                    float beat = Mathf.Pow(Mathf.Abs(Mathf.Sin(t)), 4f);
                    return Vector2.one * (1f + beat * a * 2f);

                case SkinMotion.Shiver:
                    return Vector2.one * (1f + Mathf.Sin(t * 6f) * a * 0.6f);

                case SkinMotion.Roll:
                    // Видимость вращения без поворота спрайта: ширина дышит,
                    // как у монеты, повёрнутой ребром.
                    return new Vector2(1f - Mathf.Abs(Mathf.Sin(t)) * a * 1.6f, 1f);

                case SkinMotion.Stretch:
                    return new Vector2(1f + Mathf.Sin(t) * a, 1f + Mathf.Cos(t) * a);

                case SkinMotion.Flicker:
                {
                    // Замирает и вздрагивает. Была ступенька размера от 2 до 10 раз
                    // в секунду — строб формы на объекте в центре взгляда (E-14).
                    // Теперь короткий плавный толчок pow(max(0, sin πt), 8) не
                    // чаще раза в 1.67 с, а на «Тихо» — медленное дыхание.
                    float slow = phase * Mathf.Min(fx.Speed, FlickerMaxSpeed);
                    float twitch = Visuals.Level == FxBrightness.Quiet
                        ? 0.5f + 0.5f * Mathf.Sin(slow * Mathf.PI)
                        : Mathf.Pow(Mathf.Max(0f, Mathf.Sin(slow * Mathf.PI)), 8f);
                    return Vector2.one * (1f + twitch * a * 1.5f);
                }

                default:
                    return Vector2.one;
            }
        }

        // ------------------------------------------------------------ следы

        /// <summary>Одна частица следа: куда летит, какая и сколько живёт.</summary>
        public struct TrailSpec
        {
            /// <summary>Смещение точки рождения от центра тела, в долях радиуса.</summary>
            public Vector2 Offset;
            /// <summary>Скорость в мировых единицах в секунду.</summary>
            public Vector2 Velocity;
            public Color Color;
            public float Life;
            /// <summary>Размер в мировых единицах.</summary>
            public float Size;
            public float Alpha;
        }

        /// <summary>
        /// Описание одной частицы следа по виду скина.
        ///
        /// **Публичная и статическая намеренно.** Этим же описанием пользуется
        /// витрина: у неё свой пул точек на холсте, но параметры частицы обязаны
        /// быть те же, иначе след в магазине снова начнёт отличаться от следа в
        /// игре — а именно такие расхождения и пришлось чинить.
        ///
        /// Возвращает параметры, а не рисует: мир и холст живут в разных единицах,
        /// и общим может быть только описание.
        /// </summary>
        public static TrailSpec SampleTrail(SkinLook look, float phase)
        {
            SkinFxDef fx = look.Fx;
            Color c = Random.value < 0.5f ? fx.PrimaryOr(look.Tint) : fx.SecondaryOr(look.Tint);

            // Исходная точка — не центр, а край: частица рождается на поверхности,
            // а не вылезает из середины тела.
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector2 edge = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.8f;

            switch (fx.Trail)
            {
                case SkinTrail.Bubbles:
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 0.75f,
                        Velocity = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.7f, 1.3f)),
                        Life = Random.Range(0.6f, 1.1f), Size = Random.Range(0.035f, 0.075f) };

                case SkinTrail.Motes:
                    // Взвесь почти стоит на месте: она и должна выглядеть тяжелее воды.
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 0.5f,
                        Velocity = new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(0.05f, 0.25f)),
                        Life = Random.Range(1.0f, 1.8f), Size = Random.Range(0.02f, 0.045f) };

                case SkinTrail.Sparks:
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 1f,
                        Velocity = new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(-0.6f, 1.2f)),
                        Life = Random.Range(0.15f, 0.35f), Size = Random.Range(0.02f, 0.04f) };

                case SkinTrail.Shards:
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 0.9f,
                        Velocity = new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(0.3f, 0.7f)),
                        Life = Random.Range(0.9f, 1.6f), Size = Random.Range(0.03f, 0.06f) };

                case SkinTrail.Ribbon:
                    // Лента тянется строго вниз и живёт долго: из неё складывается
                    // непрерывный хвост, а не пунктир.
                    return new TrailSpec { Offset = Vector2.down * 0.6f, Color = c, Alpha = 0.45f,
                        Velocity = new Vector2(Mathf.Sin(phase * 3f) * 0.25f, -0.35f),
                        Life = Random.Range(1.1f, 1.7f), Size = Random.Range(0.05f, 0.09f) };

                case SkinTrail.Rings:
                    // Кольцо = частица, рождённая на окружности и идущая наружу.
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 0.8f,
                        Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.1f,
                        Life = 0.45f, Size = Random.Range(0.025f, 0.05f) };

                case SkinTrail.Steam:
                    // Пар, а не дым: быстро всплывает и сразу тает, как и положено
                    // горячему под водой.
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 0.6f,
                        Velocity = new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(1.1f, 1.9f)),
                        Life = Random.Range(0.35f, 0.6f), Size = Random.Range(0.045f, 0.09f) };

                case SkinTrail.Dust:
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 0.45f,
                        Velocity = new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.5f, -0.1f)),
                        Life = Random.Range(0.8f, 1.4f), Size = Random.Range(0.025f, 0.05f) };

                case SkinTrail.Veil:
                    return new TrailSpec { Offset = Random.insideUnitCircle, Color = c, Alpha = 0.3f,
                        Velocity = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.1f, 0.4f)),
                        Life = Random.Range(1.2f, 2.0f), Size = Random.Range(0.07f, 0.13f) };

                default: // Stardust
                    return new TrailSpec { Offset = edge, Color = c, Alpha = 1f,
                        Velocity = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(-0.1f, 0.5f)),
                        Life = Random.Range(0.7f, 1.5f), Size = Random.Range(0.02f, 0.05f) };
            }
        }

        void EmitTrail(Vector2 center, float radius, SkinLook look)
        {
            if (_fx == null) return;

            TrailSpec t = SampleTrail(look, _phase);

            // Размер следа идёт за размером шарика. Без этого множителя игрока
            // (0.8) след остался прежним и смотрелся крупнее уменьшенного тела.
            _fx.Emit(center + t.Offset * radius, t.Velocity * PlayerSize.Scale,
                     t.Color, t.Life, t.Size * PlayerSize.Scale, t.Alpha);
        }

        // ---------------------------------------------- разрушение препятствия

        /// <summary>
        /// Что видно в месте разрушенного препятствия.
        ///
        /// Обычным, редким и эпическим — короткая вспышка в цвете скина. Легендарным —
        /// собственная анимация, у каждого своя. Разница должна быть видна сразу, иначе
        /// легендарный скин отличается от эпического только ценой.
        /// </summary>
        public void OnHazardDestroyed(Vector2 at, HazardDeath death)
        {
            if (_fx == null) return;

            // На «Тихо» разрушение и добивания реже на четверть (E-10).
            _fx.SetDensity(Visuals.Particles);

            // Легендарным положена своя постановка целиком.
            if (_bubbleLook.Rarity == Rarity.Legendary && _bubbleLook.Fx.Finisher != Finisher.None)
            {
                PlayFinisher(_bubbleLook.Fx.Finisher, at,
                             _bubbleLook.Fx.PrimaryOr(_bubbleLook.Tint),
                             _bubbleLook.Fx.SecondaryOr(_bubbleLook.Tint));
                _fx.SetDensity(1f);
                return;
            }

            // Разрушение — от пузырька: его редкость и его материал решают, как
            // рассыпается цель. Это то, на что игрок смотрит весь заплыв.
            PlayDestroy(_bubbleLook.Material, at);
            _fx.SetDensity(1f);

            // Трофей с босса заметнее купленного за монеты — но заметнее цветом
            // и формой, а не яркостью. Белое кольцо, стоявшее здесь, было самой
            // светлой точкой экрана и давало именно тот пересвет, от которого
            // болят глаза.
            if (_bubbleLook.Rarity == Rarity.Boss)
                EmitRing(at, _bubbleLook.Material.Aura,
                         Mathf.RoundToInt(10 * Visuals.Particles), 1.7f, 0.45f);
        }

        /// <summary>
        /// Что материал скина сделал с препятствием.
        ///
        /// Лава сжигает, лёд замораживает, кислота растворяет — и видно это должно
        /// быть сразу, иначе дорогой скин отличается от дешёвого только ценником.
        /// Число стадий растёт с редкостью: у обычных одна короткая вспышка, у
        /// эпических эффект разворачивается.
        ///
        /// Частиц на стадию немного и потолок жёсткий: разрушения идут очередями, и
        /// щедрый эффект складывается сам с собой до белого экрана.
        /// </summary>
        void PlayDestroy(MaterialFxDef m, Vector2 at)
        {
            int stages = Mathf.Clamp(m.Stages, 1, 3);

            switch (m.Pattern)
            {
                case DestroyPattern.Burst:
                    // Разлёт: чем больше стадий, тем дольше догорает.
                    _fx.Burst(at, m.Hot, 5 + stages * 3, 1.4f, 3.4f, 0.035f, 0.075f);
                    if (stages > 1) _fx.Burst(at, m.Cool, 5, 0.5f, 1.6f, 0.05f, 0.1f);
                    break;

                case DestroyPattern.Ring:
                    EmitRing(at, m.Hot, 8 + stages * 3, 2.4f, 0.4f);
                    if (stages > 1) EmitRing(at, m.Cool, 8, 1.3f, 0.55f);
                    if (stages > 2) EmitRing(at, Soft(m.Hot), 6, 2.6f, 0.28f);
                    break;

                case DestroyPattern.Rise:
                    // Столб вверх: пар, пепел, пузыри — всё лёгкое всплывает.
                    for (int i = 0; i < 5 + stages * 2; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.14f,
                                 new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(1.3f, 2.3f)),
                                 i % 2 == 0 ? m.Hot : m.Cool,
                                 Random.Range(0.4f, 0.8f), Random.Range(0.04f, 0.09f), 0.75f);
                    break;

                case DestroyPattern.Implode:
                    // Сначала внутрь, и только потом тёмный выброс.
                    for (int i = 0; i < 10 + stages * 2; i++)
                    {
                        float a = i / 12f * Mathf.PI * 2f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        _fx.Emit(at + dir * 0.65f, -dir * 2.6f, m.Hot, 0.3f, 0.045f, 0.95f);
                    }
                    if (stages > 1) _fx.Burst(at, m.Cool, 5, 0.3f, 1.2f, 0.06f, 0.12f);
                    break;

                case DestroyPattern.Arc:
                    // Разряд: короткие быстрые штрихи в стороны.
                    for (int i = 0; i < 4 + stages * 2; i++)
                    {
                        float a = Random.Range(0f, Mathf.PI * 2f);
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        _fx.Emit(at, dir * Random.Range(2f, 3f), Soft(m.Hot), 0.14f, 0.03f, 0.9f);
                        _fx.Emit(at + dir * 0.2f, dir * 1.6f, m.Hot, 0.22f, 0.035f, 0.9f);
                    }
                    if (stages > 2) EmitRing(at, m.Cool, 10, 2.8f, 0.3f);
                    break;

                case DestroyPattern.Spiral:
                    for (int i = 0; i < 8 + stages * 3; i++)
                    {
                        float a = i / 11f * Mathf.PI * 2f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 tangent = new Vector2(-dir.y, dir.x);
                        _fx.Emit(at + dir * 0.3f, tangent * 2.4f + dir * 0.9f,
                                 i % 3 == 0 ? m.Cool : m.Hot, 0.45f, 0.045f, 0.9f);
                    }
                    break;

                case DestroyPattern.Rain:
                    // Крошка оседает: единственный эффект, идущий вниз.
                    for (int i = 0; i < 6 + stages * 2; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.2f,
                                 new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-1.4f, -0.4f)),
                                 i % 2 == 0 ? m.Hot : m.Cool,
                                 Random.Range(0.6f, 1.1f), Random.Range(0.03f, 0.06f), 0.8f);
                    break;

                case DestroyPattern.Shards:
                    // Замерзание, потом скол: вторая стадия — крупные осколки.
                    _fx.Burst(at, m.Hot, 6, 0.3f, 1.1f, 0.05f, 0.09f);
                    if (stages > 1)
                        for (int i = 0; i < 6; i++)
                            _fx.Emit(at, Random.insideUnitCircle.normalized * Random.Range(1.6f, 2.8f),
                                     m.Cool, 0.7f, Random.Range(0.05f, 0.1f), 0.95f);
                    if (stages > 2) EmitRing(at, Soft(m.Cool), 8, 1.8f, 0.25f);
                    break;

                case DestroyPattern.Mist:
                    // Растворение: медленное, вязкое, почти без скорости.
                    for (int i = 0; i < 5 + stages * 2; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.25f,
                                 new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(0.1f, 0.5f)),
                                 i % 2 == 0 ? m.Hot : m.Cool,
                                 Random.Range(1.0f, 1.7f), Random.Range(0.07f, 0.13f), 0.4f);
                    break;

                case DestroyPattern.Grid:
                    // Распад на клетки: ровная сетка, разлетающаяся наружу.
                    for (int gx = -1; gx <= 1; gx++)
                        for (int gy = -1; gy <= 1; gy++)
                        {
                            Vector2 cell = new Vector2(gx, gy) * 0.17f;
                            _fx.Emit(at + cell, cell.normalized * 1.8f,
                                     (gx + gy) % 2 == 0 ? m.Hot : m.Cool,
                                     0.4f, 0.055f, 0.95f);
                        }
                    break;
            }
        }

        /// <summary>
        /// Легендарные анимации. Все укладываются в полсекунды и держатся в пределах
        /// полутора радиусов от точки: закрыть собой следующую угрозу эффект не
        /// должен, как бы дорого ни стоил скин.
        /// </summary>
        void PlayFinisher(Finisher finisher, Vector2 at, Color c, Color c2)
        {
            switch (finisher)
            {
                case Finisher.Shockwave:
                    // Плотное кольцо и огрызок пара в центре.
                    EmitRing(at, c, 18, 3.4f, 0.32f);
                    EmitRing(at, c2, 12, 1.8f, 0.5f);
                    for (int i = 0; i < 4; i++)
                        _fx.Emit(at, new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(1.2f, 2f)),
                                 c2, 0.45f, 0.08f, 0.7f);
                    break;

                case Finisher.Maelstrom:
                    // Воронка: частицы стартуют по кругу и закручиваются внутрь.
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i / 16f * Mathf.PI * 2f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        Vector2 tangent = new Vector2(-dir.y, dir.x);
                        _fx.Emit(at + dir * 0.45f, tangent * 2.6f - dir * 1.8f,
                                 i % 3 == 0 ? c2 : c, 0.5f, 0.05f, 0.9f);
                    }
                    break;

                case Finisher.Starfall:
                    // Была белая вспышка размером в полэкрана плюс крошка. Вспышка
                    // убрана: у легендарного скина и без неё своя узнаваемая
                    // постановка, а слепила именно она.
                    _fx.Emit(at, Vector2.zero, Soft(c), 0.25f, 0.3f, 0.55f);
                    for (int i = 0; i < 14; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.2f,
                                 new Vector2(Random.Range(-1.1f, 1.1f), Random.Range(-0.2f, 1.4f)),
                                 i % 2 == 0 ? c : c2, Random.Range(0.6f, 1.2f),
                                 Random.Range(0.025f, 0.05f), 1f);
                    break;

                case Finisher.Bloom:
                    // Цветок: три кольца разного радиуса, раскрывающиеся наружу.
                    EmitRing(at, c, 10, 1.4f, 0.55f);
                    EmitRing(at, c2, 10, 2.4f, 0.45f);
                    EmitRing(at, Soft(c), 6, 2.4f, 0.3f);
                    break;

                case Finisher.Implosion:
                    // Схлопывание: частицы летят внутрь, и только потом тёмный выброс.
                    for (int i = 0; i < 14; i++)
                    {
                        float a = i / 14f * Mathf.PI * 2f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        _fx.Emit(at + dir * 0.7f, -dir * 2.8f, c2, 0.3f, 0.05f, 0.95f);
                    }
                    _fx.Burst(at, c, 6, 0.3f, 1.1f, 0.06f, 0.12f);
                    break;
            }
        }

        void EmitRing(Vector2 at, Color c, int count, float speed, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f;
                _fx.Emit(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed,
                         c, life, 0.045f, 0.85f);
            }
        }

        // ------------------------------------------------------- удар стража

        float _lastImpact = -1f;
        int _impactsThisSecond;
        float _impactWindow;

        /// <summary>
        /// Страж ударил препятствие.
        ///
        /// Главное здесь — точка. Эффект рождается на линии между центрами, на
        /// поверхности стража, а не в его середине и не в середине препятствия:
        /// удар случился именно там, где они соприкоснулись, и показывать его в
        /// другом месте значит показывать не удар, а вспышку непонятно о чём.
        ///
        /// Частота ограничена жёстко. Страж бьёт по нескольку раз в секунду, а по
        /// строю кораллов — по десятку; без потолка экран заливает искрами, и
        /// следующая угроза теряется в собственном фейерверке.
        /// </summary>
        /// <param name="contact">Точка касания в мировых координатах.</param>
        /// <param name="normal">Направление от стража к препятствию.</param>
        public void OnImpact(Vector2 contact, Vector2 normal)
        {
            if (_fx == null) return;

            float now = Time.unscaledTime;

            // Не чаще двадцати раз в секунду и не больше восьми за секунду подряд:
            // первое спасает от наложения, второе — от очереди по строю кораллов.
            if (now - _lastImpact < 0.05f) return;

            if (now - _impactWindow > 1f) { _impactWindow = now; _impactsThisSecond = 0; }
            if (_impactsThisSecond >= 8) return;

            _lastImpact = now;
            _impactsThisSecond++;

            // Удар — от СТРАЖА: он единственный, кто по препятствиям бьёт. Раньше
            // брался материал пузырька, и витрина на вкладке «Шарик» обещала одну
            // стихию, а в бою работала другая.
            PlayImpact(_guardianLook.Material, contact,
                       normal.sqrMagnitude < 0.0001f ? Vector2.up : normal.normalized);
        }

        /// <summary>
        /// Сила удара по редкости. Обычным — короткая вспышка, боссовым — слоями.
        /// Компактность важнее щедрости: эффект живёт меньше полусекунды и не
        /// уходит дальше полурадиуса от точки касания.
        /// </summary>
        float ImpactPower()
        {
            switch (_guardianLook.Rarity)
            {
                case Rarity.Epic: return 1.35f;
                case Rarity.Legendary: return 1.7f;
                case Rarity.Boss: return 2f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Эффект удара: один мягкий всплеск частиц в цвете скина.
        ///
        /// **Что было и почему убрано.** Удар строился тремя слоями: белая вспышка
        /// размером с шарик, ударная волна кольцом и пятнадцать-сорок частиц. По
        /// отдельности каждый слой объясним, вместе — пересвет поверх игрового
        /// поля несколько раз в секунду. Убраны оба первых слоя целиком: вспышка
        /// била по глазам, кольцо закрывало препятствие, от которого игрок в этот
        /// момент уворачивается.
        ///
        /// **Что осталось.** Только частицы — и именно они всегда и отличали огонь
        /// ото льда. Различие между скинами теперь выражено цветом, формой разлёта
        /// и типом частиц, а не их количеством и яркостью: редкий скин даёт не
        /// ярче, а чуть гуще.
        ///
        /// **Три жёстких предела**, и все проверяются аудитом:
        /// бюджет частиц (<see cref="ImpactBudget"/>), время жизни (0.3 с) и
        /// дальность разлёта (примерно диаметр шарика).
        /// </summary>
        void PlayImpact(MaterialFxDef m, Vector2 at, Vector2 n)
        {
            Vector2 tangent = new Vector2(-n.y, n.x);
            PlayImpactParticles(m, at, n, tangent, ImpactBudget());
        }

        /// <summary>
        /// Сколько частиц на один удар — всего, со всеми подслоями.
        ///
        /// Именно бюджет, а не «базовое число, которое каждый узор множит на своё»:
        /// при старом устройстве узор с двумя циклами давал вдвое больше другого,
        /// и ограничить это сверху было нельзя. Теперь узор делит выданное.
        /// </summary>
        int ImpactBudget()
        {
            float power;
            switch (_guardianLook.Rarity)
            {
                case Rarity.Epic: power = 1.35f; break;
                case Rarity.Legendary: power = 1.7f; break;
                case Rarity.Boss: power = 2f; break;
                default: power = 1f; break;
            }

            // 8 частиц у обычного скина, 18 у боссового; в щадящем режиме меньше.
            int count = Mathf.RoundToInt(Mathf.Lerp(8f, 18f, power - 1f) * Visuals.Particles);
            return Mathf.Clamp(count, 5, 20);
        }

        /// <summary>
        /// Время жизни частицы удара. Дольше трети секунды нельзя: очередь ударов
        /// по прочному препятствию сложилась бы в сплошное облако.
        /// </summary>
        static float ImpactLife(float wanted) => Mathf.Min(wanted, ImpactMaxLife);

        /// <summary>Предел времени жизни частицы удара.</summary>
        public const float ImpactMaxLife = 0.3f;

        /// <summary>
        /// Предел скорости частицы удара. При жизни 0.3 с это примерно диаметр
        /// шарика — ровно столько, сколько просили: эффект не должен заслонять ни
        /// препятствие, ни сам шарик.
        /// </summary>
        public const float ImpactMaxSpeed = 2.0f;

        static float ImpactSpeed(float wanted)
            => Mathf.Min(wanted, ImpactMaxSpeed) * PlayerSize.Scale;

        /// <summary>
        /// Размер частицы удара. Тоже от размера шарика: иначе у уменьшенного
        /// шарика брызги остались бы прежними и смотрелись бы крупнее его самого.
        /// </summary>
        static float ImpactSize(float wanted) => wanted * PlayerSize.Scale;

        /// <summary>
        /// Светлая версия цвета стихии — вместо чистого белого.
        ///
        /// Белые частицы и были главным источником пересвета: на тёмной воде белая
        /// точка читается как вспышка. Светлый оттенок самой стихии даёт тот же
        /// контраст внутри эффекта, но не слепит и заодно не ломает узнаваемость
        /// скина — искры остаются оранжевыми, а не выгорают в белизну.
        /// </summary>
        static Color Soft(Color c) => Color.Lerp(c, Palette.Foam, 0.35f);

        /// <summary>
        /// Тематическая часть удара: именно она отличает огонь ото льда.
        ///
        /// Каждый узор делит выданный бюджет между своими подслоями, а не множит
        /// его. Числа внутри — пропорции, а не количества.
        /// </summary>
        void PlayImpactParticles(MaterialFxDef m, Vector2 at, Vector2 n, Vector2 tangent, int count)
        {
            int main = Mathf.Max(3, count * 2 / 3);
            int rest = Mathf.Max(0, count - main);

            switch (m.Impact)
            {
                case ImpactPattern.Splash:
                    // Вода: веер брызг и пузырьки, уходящие вверх.
                    EmitFan(at, n, m.Hot, main, 1.6f, 0.65f);
                    for (int i = 0; i < rest; i++)
                        _fx.Emit(at, new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.8f, 1.5f)),
                                 m.Cool, ImpactLife(0.3f), ImpactSize(Random.Range(0.05f, 0.1f)), 0.7f);
                    break;

                case ImpactPattern.Sparks:
                    // Огонь: искры в стороны и язычки, всплывающие вверх.
                    EmitFan(at, n, m.Hot, main, 2.0f, 0.4f);
                    for (int i = 0; i < rest; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.08f,
                                 new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(0.9f, 1.7f)),
                                 i % 2 == 0 ? m.Hot : Soft(m.Hot),
                                 ImpactLife(0.28f), ImpactSize(Random.Range(0.06f, 0.11f)), 0.8f);
                    break;

                case ImpactPattern.Flash:
                    // Неон: ровный веер в двух оттенках, без кольца и без вспышки.
                    EmitFan(at, n, m.Hot, main, 1.7f, 0.55f);
                    EmitFan(at, n, m.Cool, rest, 1.2f, 0.9f);
                    break;

                case ImpactPattern.Arc:
                    // Молния: короткая цепочка от стража к препятствию.
                    for (int i = 0; i < main; i++)
                    {
                        float along = i / (float)Mathf.Max(1, main - 1);
                        Vector2 at2 = at + n * (along * 0.26f)
                                      + tangent * (Mathf.Sin(i * 2.3f) * 0.05f);
                        _fx.Emit(at2, tangent * Random.Range(-0.9f, 0.9f),
                                 i % 2 == 0 ? Soft(m.Hot) : m.Hot,
                                 ImpactLife(0.16f), ImpactSize(0.05f), 0.85f);
                    }
                    EmitFan(at, n, m.Hot, rest, 1.8f, 0.5f);
                    break;

                case ImpactPattern.Frost:
                    // Лёд: острые осколки веером и оседающая снежная пыль.
                    for (int i = 0; i < main; i++)
                        _fx.Emit(at, Rotate(n, Random.Range(-0.9f, 0.9f)) * ImpactSpeed(Random.Range(1.3f, 1.9f)),
                                 i % 3 == 0 ? Soft(m.Cool) : m.Cool,
                                 ImpactLife(0.3f), ImpactSize(Random.Range(0.05f, 0.1f)), 0.85f);
                    for (int i = 0; i < rest; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.14f,
                                 new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(-0.15f, 0.3f)),
                                 Soft(m.Cool), ImpactLife(0.3f), ImpactSize(Random.Range(0.03f, 0.06f)), 0.55f);
                    break;

                case ImpactPattern.Shatter:
                    for (int i = 0; i < count; i++)
                        _fx.Emit(at, (n + Random.insideUnitCircle * 0.9f).normalized
                                     * ImpactSpeed(Random.Range(1.4f, 2f)),
                                 i % 2 == 0 ? m.Hot : m.Cool,
                                 ImpactLife(0.3f), ImpactSize(Random.Range(0.06f, 0.11f)), 0.9f);
                    break;

                case ImpactPattern.Spray:
                    // Кислота: струя вдоль удара и шипящие пузыри.
                    for (int i = 0; i < main; i++)
                        _fx.Emit(at, n * ImpactSpeed(Random.Range(1.5f, 2f)) + tangent * Random.Range(-0.6f, 0.6f),
                                 i % 3 == 0 ? Soft(m.Hot) : m.Hot,
                                 ImpactLife(0.28f), ImpactSize(Random.Range(0.05f, 0.09f)), 0.85f);
                    for (int i = 0; i < rest; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.12f,
                                 new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.5f, 1.1f)),
                                 m.Cool, ImpactLife(0.3f), ImpactSize(Random.Range(0.06f, 0.11f)), 0.7f);
                    break;

                case ImpactPattern.Dust:
                    for (int i = 0; i < count; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.12f,
                                 Random.insideUnitCircle * 0.9f,
                                 i % 2 == 0 ? m.Hot : m.Cool,
                                 ImpactLife(0.3f), ImpactSize(Random.Range(0.08f, 0.14f)), 0.5f);
                    break;

                case ImpactPattern.Ripple:
                    // Вода глубин: две расходящиеся дуги, медленные и мягкие.
                    EmitArc(at, m.Hot, main, 1.5f, 0.3f);
                    EmitArc(at, m.Cool, rest, 1.0f, 0.3f);
                    break;

                case ImpactPattern.Gleam:
                    // Металл: пара штрихов поперёк удара плюс пыль.
                    for (int i = 0; i < Mathf.Min(2, main); i++)
                    {
                        float side = i % 2 == 0 ? 1f : -1f;
                        _fx.Emit(at + tangent * (0.04f * side), tangent * (1.4f * side),
                                 Soft(m.Hot), ImpactLife(0.2f), ImpactSize(0.08f), 0.85f);
                    }
                    EmitFan(at, n, m.Hot, Mathf.Max(0, count - 2), 1.5f, 0.45f);
                    break;

                case ImpactPattern.Void:
                    // Тьма: частицы идут внутрь, а не наружу. Светлой сердцевины
                    // больше нет — она и была тут самой яркой точкой экрана.
                    for (int i = 0; i < count; i++)
                    {
                        float a = i / (float)count * Mathf.PI * 2f;
                        Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        _fx.Emit(at + dir * 0.34f, -dir * 1.3f, m.Cool,
                                 ImpactLife(0.3f), ImpactSize(0.08f), 0.75f);
                    }
                    break;

                case ImpactPattern.Bloom:
                    EmitFan(at, n, m.Hot, main, 1.3f, 1.1f);
                    EmitFan(at, n, m.Cool, rest, 0.9f, 0.7f);
                    break;

                case ImpactPattern.Grit:
                    for (int i = 0; i < count; i++)
                        _fx.Emit(at, new Vector2(Random.Range(-0.9f, 0.9f), Random.Range(-1.4f, -0.2f)),
                                 i % 2 == 0 ? m.Hot : m.Cool,
                                 ImpactLife(0.3f), ImpactSize(Random.Range(0.05f, 0.08f)), 0.85f);
                    break;

                case ImpactPattern.Steam:
                    for (int i = 0; i < count; i++)
                        _fx.Emit(at + Random.insideUnitCircle * 0.1f,
                                 new Vector2(Random.Range(-0.35f, 0.35f), Random.Range(1f, 1.8f)),
                                 i % 2 == 0 ? m.Hot : m.Cool,
                                 ImpactLife(0.3f), ImpactSize(Random.Range(0.07f, 0.13f)), 0.6f);
                    break;
            }
        }

        /// <summary>Веер частиц от точки касания в сторону удара.</summary>
        void EmitFan(Vector2 at, Vector2 n, Color c, int count, float speed, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(-spread, spread, count == 1 ? 0.5f : i / (float)(count - 1));
                Vector2 dir = Rotate(n, a + Random.Range(-0.12f, 0.12f));
                _fx.Emit(at, dir * ImpactSpeed(speed * Random.Range(0.8f, 1.2f)), c,
                         ImpactLife(Random.Range(0.2f, 0.3f)), ImpactSize(Random.Range(0.05f, 0.09f)), 0.85f);
            }
        }

        /// <summary>
        /// Дуга частиц вокруг точки касания — замена ударной волне.
        ///
        /// Кольцо убрано: оно расходилось на диаметр шарика и закрывало само
        /// препятствие. Дуга открывает ту же форму, но только со стороны удара и
        /// вдвое ближе.
        /// </summary>
        void EmitArc(Vector2 at, Color c, int count, float speed, float life)
        {
            for (int i = 0; i < count; i++)
            {
                float a = count == 1 ? 0f : Mathf.Lerp(-1.1f, 1.1f, i / (float)(count - 1));
                Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                _fx.Emit(at, dir * ImpactSpeed(speed), c,
                         ImpactLife(life), ImpactSize(Random.Range(0.05f, 0.08f)), 0.8f);
            }
        }

        static Vector2 Rotate(Vector2 v, float radians)
        {
            float s = Mathf.Sin(radians), c = Mathf.Cos(radians);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>Статус, который скин оставляет на препятствии.</summary>
        public StatusKind StatusKind => _guardianLook.Material.Status;

        /// <summary>
        /// Стихия активного скина: форма, вибрация, высота звука.
        ///
        /// Достаётся из статуса материала, а не хранится отдельно, — см.
        /// <see cref="ArchetypeTable"/>: два списка, обязанных совпадать, однажды
        /// разойдутся.
        /// </summary>
        public ArchetypeDef Archetype => _guardianLook.Archetype;

        /// <summary>
        /// Сколько держится статус. Берётся из таблицы, где длительность уже
        /// расписана по редкости: обычным полторы-две секунды, легендарным четыре.
        /// </summary>
        public float StatusTime => _guardianLook.Material.StatusTime;

        public Color StatusHot => _guardianLook.Material.Hot;
        public Color StatusCool => _guardianLook.Material.Cool;

        /// <summary>
        /// Частица горящего (леденеющего, разъедаемого) препятствия.
        ///
        /// Одна за вызов и редко: статусов на экране может быть до восьми, и щедрая
        /// эмиссия у каждого сложилась бы в сплошную пелену поверх игрового поля.
        /// </summary>
        public void EmitStatusMote(Vector2 at, float radius, StatusKind kind)
        {
            if (_fx == null) return;

            Vector2 edge = at + Random.insideUnitCircle * radius * 0.8f;
            Color hot = _guardianLook.Material.Hot, cool = _guardianLook.Material.Cool;

            switch (kind)
            {
                case StatusKind.Burn:
                    // уголёк вверх и тонкая струйка дыма
                    _fx.Emit(edge, new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.9f, 1.8f)),
                             hot, Random.Range(0.4f, 0.7f), Random.Range(0.035f, 0.07f), 0.9f);
                    if (Random.value < 0.4f)
                        _fx.Emit(edge, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 1f)),
                                 new Color(0.25f, 0.22f, 0.2f, 1f), 0.8f, 0.09f, 0.35f);
                    break;

                case StatusKind.Freeze:
                    _fx.Emit(edge, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.3f, 0.1f)),
                             Soft(cool), Random.Range(0.6f, 1f), Random.Range(0.025f, 0.05f), 0.65f);
                    break;

                case StatusKind.Shock:
                    _fx.Emit(edge, Random.insideUnitCircle.normalized * Random.Range(1.2f, 2f),
                             Soft(hot), 0.14f, 0.03f, 0.8f);
                    break;

                case StatusKind.Corrode:
                    // капля стекает вниз
                    _fx.Emit(edge, new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(-1.2f, -0.5f)),
                             hot, Random.Range(0.5f, 0.9f), Random.Range(0.04f, 0.08f), 0.9f);
                    break;

                case StatusKind.Gild:
                    _fx.Emit(edge, Random.insideUnitCircle * 0.5f, Soft(hot), 0.35f, 0.03f, 0.8f);
                    break;

                case StatusKind.Shadow:
                    _fx.Emit(edge, (at - edge).normalized * 0.9f, cool, 0.5f, 0.07f, 0.8f);
                    break;

                case StatusKind.Stardust:
                    _fx.Emit(edge, Random.insideUnitCircle * 0.4f,
                             Random.value < 0.5f ? Soft(hot) : hot, Random.Range(0.5f, 1f), 0.03f, 0.8f);
                    break;

                case StatusKind.Crystal:
                    _fx.Emit(edge, Random.insideUnitCircle * 0.3f, Soft(cool), 0.5f, 0.045f, 0.7f);
                    break;

                case StatusKind.Soak:
                    _fx.Emit(edge, new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.5f, 1.1f)),
                             hot, Random.Range(0.5f, 0.9f), Random.Range(0.03f, 0.055f), 0.75f);
                    break;

                case StatusKind.Haze:
                    _fx.Emit(edge, new Vector2(Random.Range(-0.12f, 0.12f), Random.Range(0.1f, 0.4f)),
                             Soft(cool), Random.Range(0.8f, 1.3f), Random.Range(0.06f, 0.11f), 0.25f);
                    break;

                case StatusKind.Spectrum:
                    _fx.Emit(edge, Random.insideUnitCircle * 0.6f,
                             Color.HSVToRGB(Random.value, 0.7f, 1f), 0.45f, 0.04f, 0.95f);
                    break;

                case StatusKind.Silt:
                    _fx.Emit(edge, new Vector2(Random.Range(-0.1f, 0.1f), Random.Range(-0.4f, -0.1f)),
                             cool, Random.Range(0.7f, 1.2f), Random.Range(0.05f, 0.09f), 0.5f);
                    break;

                case StatusKind.Petrify:
                    _fx.Emit(edge, new Vector2(Random.Range(-0.2f, 0.2f), Random.Range(-0.8f, -0.2f)),
                             cool, Random.Range(0.4f, 0.8f), Random.Range(0.035f, 0.065f), 0.8f);
                    break;

                default: // Glitch
                    _fx.Emit(edge, new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 1.2f,
                             Random.value < 0.5f ? hot : cool, 0.25f, 0.05f, 1f);
                    break;
            }
        }

        /// <summary>Цвет, в который скин красит препятствие в момент удара.</summary>
        public Color HitColor => _guardianLook.Material.Hot;

        /// <summary>Какой рисунок удара у текущего скина — по нему выбирается звук.</summary>
        public ImpactPattern ImpactKind => _guardianLook.Material.Impact;

        /// <summary>Пузырёк получил удар: короткая реакция в цвете скина.</summary>
        public void OnHurt(Vector2 at)
        {
            if (_fx == null) return;
            _fx.Burst(at, _bubbleLook.Fx.PrimaryOr(_bubbleLook.Tint), 6, 0.8f, 2.2f, 0.04f, 0.08f);
        }
    }
}
