using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Оболочка столкновения: то, что вспыхивает вокруг стража, когда он бьёт
    /// препятствие. Только у легендарных скинов и трофеев с боссов.
    ///
    /// **Четыре слоя, и каждый отвечает за своё.**
    ///
    /// 1. **Тень** — тёмная подложка под ядром. Без неё яркое тело на тёмной воде
    ///    расплывается и теряет край.
    /// 2. **Ядро** — плотное кольцо вплотную к стражу. Тело эффекта.
    /// 3. **Форма** — то, по чему скин узнают: столб пламени, купол молний,
    ///    щупальца. Силуэт.
    /// 4. **Искры** — мелкое и быстрое по краю, чтобы край был неровным.
    ///
    /// Плюс **связь**: пряди от стража к точке касания, растекающиеся по самому
    /// препятствию. И **всплеск** в точке касания, который рисует
    /// <see cref="SkinFxRuntime"/> — здесь он не дублируется.
    ///
    /// **Единица измерения — диаметр стража.** Корень масштабируется ровно на
    /// него, и это единственное место во всём эффекте, где D превращается в
    /// мировые юниты. Прошлая версия масштабировала на РАДИУС, а числа в таблице
    /// называла диаметрами: оболочка выходила вчетверо мельче задуманного и была
    /// меньше самого шарика. Отсюда и бралась «крошечная искра».
    ///
    /// **Почему свой пул, а не FxManager.** Частицы общего пула летят по прямой в
    /// мировых координатах: они не умеют ходить по орбите и не следуют за
    /// стражем. Оболочка обязана делать и то, и другое.
    ///
    /// **Почему не буквально дочерний объект стража.** У стража свой масштаб —
    /// он пульсирует на перезарядке и дышит по закону скина. Оболочка, повешенная
    /// на него напрямую, пульсировала бы вместе с перезарядкой, к которой она
    /// отношения не имеет. Поэтому свой корень, который каждый кадр берёт у
    /// стража позицию и диаметр: двигается и масштабируется вместе с ним, но не
    /// наследует чужую анимацию. Множитель размера игрока при этом применяется
    /// ровно один раз — он уже внутри <c>guardian.Radius</c>.
    ///
    /// **Безопасность для глаз** — не пожелание, а предел, записанный числами:
    /// появление не быстрее <see cref="FadeIn"/>, затухание <see cref="FadeOut"/>,
    /// пиковая прозрачность по слоям, ни одного белого цвета в таблице, форма дуг
    /// обновляется не чаще <see cref="ArcHz"/> раз в секунду, дыхание ядра — один
    /// цикл за 1.75 с, тряски камеры нет вовсе.
    /// </summary>
    public class CollisionEnvelope
    {
        /// <summary>Появление. 0.2 с: медленнее — удар читается с опозданием.</summary>
        public const float FadeIn = 0.2f;
        /// <summary>Затухание. 0.5–0.8 по заданию.</summary>
        public const float FadeOut = 0.6f;

        /// <summary>
        /// Потолки прозрачности по слоям.
        ///
        /// Ядру позволено быть почти непрозрачным: «не слепит» — это про белизну
        /// и про резкие перепады, а не про плотность. А вот край обязан
        /// просвечивать, иначе за эффектом не видно, куда лететь.
        /// </summary>
        public const float CoreAlpha = 0.95f;
        public const float ShapeAlpha = 0.8f;
        public const float SparkAlpha = 0.62f;
        public const float ShadowAlpha = 0.6f;
        public const float LinkAlpha = 0.85f;

        /// <summary>
        /// Как часто перестраивается форма ломаных дуг, раз в секунду. Между
        /// соседними формами — плавный переход (<see cref="EnvelopeShape"/>),
        /// а не скачок: при 4 Гц со скачком кончик ветви прыгал на 94–235 px до
        /// восьми раз в секунду (E-05). На «Тихо» вдвое реже.
        /// </summary>
        public const float ArcHz = 2f;

        /// <summary>Потолок усиления от повторных ударов. +30 % по заданию.</summary>
        public const float MaxIntensity = 1.3f;

        /// <summary>
        /// Сколько частиц в пуле: половина самой плотной оболочки на форму,
        /// десятая на искры, плюс самая длинная связь. 75 + 15 + 40 = 130,
        /// округлено вверх.
        ///
        /// Было 216. Разницу дали не урезанные числа, а два наблюдения: тень и
        /// ядро — это кольца, и рисовать их полусотней точек незачем; связь
        /// может быть сплошной при вчетверо меньшем числе точек, если не давать
        /// точке стать меньше шага между соседями.
        /// </summary>
        public const int PoolSize = 136;

        /// <summary>Сколько оболочек может жить одновременно. Потолок нагрузки.</summary>
        public const int MaxEnvelopes = 2;

        class Mote
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public float Seed;       // своя фаза, чтобы частицы не шли строем
            public int Order;        // текущий слой сортировки, чтобы не писать каждый кадр
            public bool Active;
        }

        Mote[] _motes;
        Transform _root;
        Transform _follow;

        // Кольца: тень, тело и акцент. Три объекта на всю оболочку вместо
        // полусотни точек, и Transform им переписывается по одному разу в кадр.
        SpriteRenderer _shadowRing;
        SpriteRenderer _coreRing;
        SpriteRenderer _accentRing;

        // Границы видимого мира: частицы за ними не трогаем вовсе.
        float _halfWidth = 100f;
        float _bottom = -100f;
        float _top = 100f;

        EnvelopeDef _def;
        bool _hasDef;

        float _life;          // сколько осталось жить оболочке
        float _age;           // сколько живёт, для нарастания
        float _intensity = 1f;
        float _linkLife;
        float _linkFull = 1f;
        float _arcStep;              // номер формы дуг с дробной частью: между формами — переход

        Vector2 _linkAtWorld;        // точка касания
        Vector2 _targetWorld;        // центр препятствия — к нему и тянется связь
        float _diameter = 0.6f;     // мировых юнитов в одном D
        float _targetRadius = 0.3f;   // в мире; в D пересчитывается в Tick
        float _weight = 1f;         // вторая оболочка тише первой

        /// <summary>Оболочка сейчас показывается.</summary>
        public bool Active => _life > 0f;

        /// <summary>Вид, который она играет. Нужен, чтобы продлевать, а не ставить вторую.</summary>
        public EnvelopeKind Kind => _def.Kind;

        /// <summary>Куда тянется связь. По ней решается, та же это цель или новая.</summary>
        public Vector2 LinkAt => _linkAtWorld;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Корни всех оболочек в игре — счётчику «образов стража» (GuardianImageWatch).</summary>
        public static readonly System.Collections.Generic.List<Transform> DebugRoots = new System.Collections.Generic.List<Transform>();
#endif

        public void Init(Transform parent, float weight = 1f)
        {
            _weight = Mathf.Clamp01(weight);

            var go = new GameObject("CollisionEnvelope");
            go.transform.SetParent(parent, false);
            _root = go.transform;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DebugRoots.Add(_root);
#endif

            var sprite = SpriteFactory.SoftDot();

            // Второй оболочке столько частиц не нужно: она играет вместе с первой
            // и в основном добавляет связь ко второй цели.
            int size = Mathf.RoundToInt(PoolSize * Mathf.Lerp(0.6f, 1f, _weight));
            _motes = new Mote[size];

            for (int i = 0; i < size; i++)
            {
                var moteGo = new GameObject("E");
                moteGo.transform.SetParent(_root, false);

                var sr = moteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.Guardian + 5;

                moteGo.SetActive(false);
                _motes[i] = new Mote { Tr = moteGo.transform, Sr = sr, Order = sr.sortingOrder };
            }

            _shadowRing = MakeRing("Shadow", SpriteFactory.EnvelopeRing(),
                                   SortingOrders.Guardian + 3);
            _coreRing = MakeRing("Core", SpriteFactory.EnvelopeRing(),
                                 SortingOrders.Guardian + 4);
            _accentRing = MakeRing("CoreAccent", SpriteFactory.EnvelopeSpeck(),
                                   SortingOrders.Guardian + 4);

            _root.gameObject.SetActive(false);
        }

        SpriteRenderer MakeRing(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        /// <summary>
        /// Границы видимого мира. Частицы за ними не получают ни Transform, ни
        /// цвета: у оболочки радиусом 3.4 D это заметная доля кадра, когда страж
        /// прижат к краю экрана.
        /// </summary>
        public void SetBounds(float halfWidth, float bottom, float top)
        {
            _halfWidth = halfWidth;
            _bottom = bottom;
            _top = top;
        }

        /// <summary>За кем следовать. Зовётся один раз, после создания стража.</summary>
        public void Follow(Transform target) => _follow = target;

        /// <summary>
        /// Начать или продлить оболочку.
        ///
        /// Повторный удар не создаёт вторую оболочку: таймер обновляется, а
        /// насыщенность растёт до потолка и дальше держится. Иначе при серии
        /// ударов по створке раковины вокруг стража копилось бы десять оболочек
        /// подряд.
        /// </summary>
        /// <param name="guardianRadius">Радиус стража в мире. Диаметр вдвое больше.</param>
        /// <param name="targetRadius">Радиус препятствия в мире — по нему растекается связь.</param>
        /// <param name="link">Тянуть ли связь. Разрушенной цели она ни к чему.</param>
        /// <param name="targetCenter">
        /// Центр препятствия. Связь тянется именно к нему, а не к точке касания:
        /// точка касания лежит посередине перекрытия, то есть в полурадиусе от
        /// центра стража, и связь, нацеленная туда, оказывалась короче самого
        /// шарика и смотрела внутрь него.
        /// </param>
        public void Play(EnvelopeDef def, Vector2 contact, Vector2 targetCenter,
                         float guardianRadius, float targetRadius, bool link = true)
        {
            bool extending = _life > 0f && _hasDef && _def.Kind == def.Kind;

            // Продление не имеет права поднять яркость за кадр (E-07). Раньше
            // жизнь сбрасывалась на полную, а возраст оставался: удар в последние
            // 0.6 с, когда оболочка уже гасла, возвращал её к пику мгновенно.
            // Теперь возраст откатывается так, чтобы нарастание продолжилось с
            // текущей видимости со своей обычной скоростью FadeIn.
            if (extending)
            {
                float current = Mathf.Clamp01(Mathf.Min(_age / FadeIn,
                                                        _life > FadeOut ? 1f : _life / FadeOut));
                _age = Mathf.Min(_age, current * FadeIn);
            }

            _def = def;
            _hasDef = true;
            _life = def.Duration;

            // Связь держится больше половины оболочки, но не дольше секунды: это
            // огонь, оставшийся на камне, а не вторая оболочка.
            _linkFull = link ? Mathf.Min(1f, def.Duration * 0.55f) : 0f;
            _linkLife = _linkFull;

            _linkAtWorld = contact;
            _targetWorld = targetCenter;
            _diameter = Mathf.Max(0.02f, guardianRadius * 2f);
            _targetRadius = Mathf.Max(0.02f, targetRadius);

            if (extending) _intensity = Mathf.Min(MaxIntensity, _intensity + 0.1f);
            else { _intensity = 1f; _age = 0f; }

            _root.gameObject.SetActive(true);
        }

        /// <summary>
        /// Такт. На покое оболочка выключена целиком — ни одного активного
        /// объекта в иерархии, ни одного вызова в кадре.
        /// </summary>
        public void Tick(float dt)
        {
            if (_life <= 0f)
            {
                if (_root != null && _root.gameObject.activeSelf) Hide();
                return;
            }

            _life -= dt;
            _age += dt;
            _linkLife = Mathf.Max(0f, _linkLife - dt);
            _arcStep += dt * ArcHz * Visuals.ArcRate;

            if (_follow != null) _root.position = _follow.position;

            // Диаметр стража — единственный источник размера. Множитель игрока уже
            // внутри него, поэтому второй раз его применять нельзя.
            float scale = _diameter;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            scale *= DebugSizeScale;
#endif
            _root.localScale = Vector3.one * scale;

            // Нарастание и затухание. Между ними оболочка держится ровно.
            float fade = Mathf.Clamp01(Mathf.Min(_age / FadeIn,
                                                 _life > FadeOut ? 1f : _life / FadeOut));
            fade *= _weight;

            var split = EnvelopeShape.Layers(_def, _intensity);
            int link = _linkLife > 0f ? EnvelopeShape.LinkCount(_def) : 0;

            // Кольца: три спрайта на всю оболочку, по одному Transform в кадр.
            PlaceRings(fade);

            // Пул второй оболочки меньше, и самая плотная в него не влезает.
            // Ужимать нужно именно россыпь: связь лежит в конце диапазона, и
            // простой обрез по длине пула срезал бы её целиком — то есть ровно
            // то, ради чего вторая оболочка и заводилась.
            int room = _motes.Length - link;
            int want = split.Shape + split.Spark;

            if (want > room && want > 0)
            {
                float k = room / (float)want;
                split.Spark = Mathf.Max(2, Mathf.FloorToInt(split.Spark * k));
                split.Shape = Mathf.Max(4, room - split.Spark);
            }

            int endShape = split.Shape;
            int endSpark = endShape + split.Spark;
            int endLink = endSpark + link;

            Vector2 rootPos = _root.position;
            float d = Mathf.Max(0.0001f, _diameter);
            Vector2 toD = (_targetWorld - rootPos) / d;
            float targetRD = Mathf.Clamp(_targetRadius / d, 0.15f, 3f);

            // Отсечка по экрану. Считаем в долях D вокруг стража один раз, а не
            // сравниваем мировые координаты у каждой частицы.
            float leftD = (-_halfWidth - rootPos.x) / d;
            float rightD = (_halfWidth - rootPos.x) / d;
            float downD = (_bottom - rootPos.y) / d;
            float upD = (_top - rootPos.y) / d;

            for (int i = 0; i < _motes.Length; i++)
            {
                var m = _motes[i];

                if (i >= endLink)
                {
                    if (m.Active) { m.Active = false; m.Tr.gameObject.SetActive(false); }
                    continue;
                }

                if (m.Seed <= 0f) m.Seed = (i * 0.6180339f) % 1f;

                EnvelopeLayer layer;
                EnvelopeShape.Sampled s;
                EnvelopeShape.LinkDot dot = default;
                bool isLink = i >= endSpark;

                if (isLink)
                {
                    dot = EnvelopeShape.LinkAt(_def, i - endSpark, Vector2.zero, toD,
                                               _arcStep, targetRD);
                    s = default;
                    layer = EnvelopeLayer.Shape;
                }
                else
                {
                    layer = i < endShape ? EnvelopeLayer.Shape : EnvelopeLayer.Spark;
                    int idx = i < endShape ? i : i - endShape;
                    int cnt = i < endShape ? split.Shape : split.Spark;
                    s = EnvelopeShape.Sample(_def, layer, idx, cnt, _age, m.Seed, _arcStep);
                }

                Vector2 at = isLink ? dot.Position : s.Position;
                float size = isLink ? dot.Size : s.Size;

                // За краем экрана частицу не только не рисуем, но и не трогаем:
                // ни Transform, ни цвета. Это и есть главная экономия, когда
                // страж прижат к борту, а оболочка в семь его диаметров.
                if (at.x + size < leftD || at.x - size > rightD
                    || at.y + size < downD || at.y - size > upD)
                {
                    if (m.Active) { m.Active = false; m.Tr.gameObject.SetActive(false); }
                    continue;
                }

                if (!m.Active)
                {
                    m.Active = true;
                    m.Tr.gameObject.SetActive(true);
                }

                m.Tr.localPosition = at;
                m.Tr.localScale = Vector3.one * size;

                int order = isLink ? SortingOrders.Guardian + 6 : OrderOf(layer);
                if (m.Order != order) { m.Order = order; m.Sr.sortingOrder = order; }

                if (isLink)
                {
                    float linkFade = _linkFull > 0f ? Mathf.Clamp01(_linkLife / _linkFull) : 0f;
                    linkFade *= Mathf.Clamp01(_age / 0.1f);

                    Color c = dot.OnTarget ? _def.Accent : _def.Hot;
                    m.Sr.color = c.WithAlpha(Mathf.Clamp01(
                        linkFade * fade * LinkAlpha * Visuals.EnvelopeAlpha * EnvelopeTable.Alpha(_def)));
                }
                else
                {
                    // Clamp01 здесь не перестраховка: без него любая траектория
                    // с альфой больше единицы тихо пробила бы потолок слоя, то
                    // есть главное правило безопасности для глаз.
                    m.Sr.color = s.Color.WithAlpha(Mathf.Clamp01(
                        Mathf.Clamp01(s.Alpha) * fade * CeilingOf(layer) * Visuals.EnvelopeAlpha * EnvelopeTable.Alpha(_def)));
                }
            }
        }

        /// <summary>
        /// Тень, тело и акцент — три спрайта. Раньше это были сорок восемь точек,
        /// которым каждый кадр переписывали Transform и цвет ради фигуры,
        /// целиком помещающейся в одну текстуру.
        /// </summary>
        void PlaceRings(float fade)
        {
            var r = EnvelopeShape.Rings(_def, _age);
            float a = fade * Visuals.EnvelopeAlpha * EnvelopeTable.Alpha(_def);

            SetRing(_shadowRing, r.ShadowScale, _def.Edge,
                    r.ShadowAlpha * a * ShadowAlpha, _age * 0.35f);
            SetRing(_coreRing, r.CoreScale, _def.Hot,
                    r.CoreAlpha * a * CoreAlpha, _age * 0.5f);
            SetRing(_accentRing, r.CoreScale * 1.02f, _def.Accent,
                    r.AccentAlpha * a * CoreAlpha, -_age * 0.8f);
        }

        static void SetRing(SpriteRenderer sr, float scale, Color tint, float alpha, float spin)
        {
            if (sr == null) return;

            float a = Mathf.Clamp01(alpha);
            if (a <= 0.01f) { if (sr.enabled) sr.enabled = false; return; }

            if (!sr.enabled) sr.enabled = true;
            sr.transform.localScale = Vector3.one * scale;
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, spin * Mathf.Rad2Deg * 0.25f);
            sr.color = tint.WithAlpha(a);
        }

        static float CeilingOf(EnvelopeLayer layer)
        {
            switch (layer)
            {
                case EnvelopeLayer.Shadow: return ShadowAlpha;
                case EnvelopeLayer.Core: return CoreAlpha;
                case EnvelopeLayer.Spark: return SparkAlpha;
                default: return ShapeAlpha;
            }
        }

        /// <summary>
        /// Порядок отрисовки по слоям. Всё выше стража (20) и ниже частиц удара
        /// (30): всплеск в точке касания должен читаться поверх оболочки.
        /// </summary>
        static int OrderOf(EnvelopeLayer layer)
        {
            switch (layer)
            {
                case EnvelopeLayer.Shadow: return SortingOrders.Guardian + 3;
                case EnvelopeLayer.Core: return SortingOrders.Guardian + 4;
                case EnvelopeLayer.Spark: return SortingOrders.Guardian + 7;
                default: return SortingOrders.Guardian + 5;
            }
        }

        void Hide()
        {
            for (int i = 0; i < _motes.Length; i++)
            {
                if (!_motes[i].Active) continue;
                _motes[i].Active = false;
                _motes[i].Tr.gameObject.SetActive(false);
            }
            if (_shadowRing != null) _shadowRing.enabled = false;
            if (_coreRing != null) _coreRing.enabled = false;
            if (_accentRing != null) _accentRing.enabled = false;

            _root.gameObject.SetActive(false);
            _intensity = 1f;
            _age = 0f;
        }

        /// <summary>Сбросить при начале заплыва: пул обязан вернуться чистым.</summary>
        public void Clear()
        {
            _life = 0f;
            _linkLife = 0f;
            if (_root != null) Hide();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>
        /// Множитель размера для дев-панели. В релизной сборке поля нет вовсе —
        /// препроцессор вырезает и его, и умножение в Tick.
        /// </summary>
        public static float DebugSizeScale = 1f;

        /// <summary>Сколько частиц сейчас на экране — дев-панели.</summary>
        public int DebugActive
        {
            get
            {
                int n = 0;
                if (_motes == null) return 0;
                for (int i = 0; i < _motes.Length; i++) if (_motes[i].Active) n++;
                return n;
            }
        }

        public float DebugLife => _life;
        public float DebugIntensity => _intensity;
        public int DebugPool => _motes != null ? _motes.Length : 0;
#endif
    }

    /// <summary>
    /// Две оболочки и правило, какую из них взять.
    ///
    /// **Зачем две.** Цепной взрыв мины бьёт несколько препятствий в одном кадре,
    /// и связь, перепрыгивающая с одного на другое, читается как сбой. Две
    /// оболочки позволяют показать оба попадания честно.
    ///
    /// **Почему ровно две, а не сколько придётся.** Это потолок нагрузки: третья
    /// оболочка прибавила бы ещё две сотни спрайтов и при этом легла бы поверх
    /// первых двух, ничего не добавив глазу. Повторный удар по той же цели
    /// продлевает уже идущую, а не занимает слот.
    ///
    /// **Вторая тише первой.** Её вес 0.6: две полные оболочки, наложенные друг
    /// на друга, дали бы суммарную яркость выше потолка — то самое, чего правила
    /// безопасности не разрешают.
    /// </summary>
    public class EnvelopeBank
    {
        readonly CollisionEnvelope[] _slots = new CollisionEnvelope[CollisionEnvelope.MaxEnvelopes];

        public void Init(Transform parent, Transform follow)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new CollisionEnvelope();
                _slots[i].Init(parent, i == 0 ? 1f : 0.6f);
                _slots[i].Follow(follow);
            }
        }

        /// <summary>
        /// Сыграть удар. Та же цель — продлеваем, новая — занимаем свободный слот,
        /// слотов нет — продлеваем первый.
        /// </summary>
        public void Play(EnvelopeDef def, Vector2 contact, Vector2 targetCenter,
                         float guardianRadius, float targetRadius, bool link = true)
        {
            // «Та же цель» — в пределах радиуса стража: по соседней створке той же
            // раковины бьют почти в ту же точку, и это один удар, а не два.
            float near = Mathf.Max(0.01f, guardianRadius);

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Active) continue;
                if (Vector2.Distance(_slots[i].LinkAt, contact) > near) continue;

                _slots[i].Play(def, contact, targetCenter, guardianRadius, targetRadius, link);
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i].Active) continue;
                _slots[i].Play(def, contact, targetCenter, guardianRadius, targetRadius, link);
                return;
            }

            _slots[0].Play(def, contact, targetCenter, guardianRadius, targetRadius, link);
        }

        public void Tick(float dt)
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i].Tick(dt);
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i].Clear();
        }

        /// <summary>Границы видимого мира — обеим оболочкам сразу.</summary>
        public void SetBounds(float halfWidth, float bottom, float top)
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i].SetBounds(halfWidth, bottom, top);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public int DebugActive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++) n += _slots[i].DebugActive;
                return n;
            }
        }

        public int DebugLive
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i].Active) n++;
                return n;
            }
        }

        public float DebugLife => _slots[0].DebugLife;
        public float DebugIntensity => _slots[0].DebugIntensity;
#endif
    }
}
