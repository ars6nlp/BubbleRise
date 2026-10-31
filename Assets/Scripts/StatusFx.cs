using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Статус на препятствии: горит, леденеет, разъедается.
    ///
    /// **Почему это отдельный компонент, а не поле внутри препятствия.** Первая
    /// версия жила прямо в <see cref="Obstacle"/> — и работала только на нём. А на
    /// экране препятствия четырёх разных классов: обломки и шестерёнки (Obstacle),
    /// красные блоки строя (CoralFormation.Block), цепочки шпилей (CoralSpire.Segment)
    /// и шары локаций (OceanZone.Blob). Три из четырёх не получали ничего, и игрок
    /// видел ровно то, на что жаловался: вспышку в точке удара и невозмутимое
    /// препятствие.
    ///
    /// Общего у всех четырёх ровно два поля — Transform и SpriteRenderer. На них
    /// компонент и опирается, больше ему ничего не нужно.
    ///
    /// **Как рисуется.** Два слоя поверх цели:
    ///
    /// * **Ореол** — мягкое пятно ПОД препятствием. Он и даёт ощущение жара или
    ///   холода вокруг объекта, потому что виден за его силуэтом.
    /// * **Накладка** — тот же спрайт, что у цели, поверх неё. Отсюда точное
    ///   совпадение с формой: шестерёнка горит шестерёнкой, а не кругом. Оба слоя —
    ///   дети цели, поэтому крутятся и движутся вместе с ней даром.
    ///
    /// Шейдера нет сознательно: в проекте нет ни одного своего шейдера, всё
    /// держится на процедурных спрайтах, и заводить ради огня зависимость от
    /// Shader Graph значило бы менять правила игры ради одного эффекта. Свечение
    /// набирается яркостью и наложением двух слоёв.
    /// </summary>
    public class StatusFx : MonoBehaviour
    {
        /// <summary>
        /// Сколько препятствий горит одновременно. Восемь — не перестраховка: на
        /// экране бывает за десяток угроз, и если светится каждая, поле перестаёт
        /// читаться, а слабый телефон проседает.
        /// </summary>
        /// <summary>
        /// Сколько статусов видно одновременно.
        ///
        /// Было восемь. Восемь горящих и бьющих током препятствий на экране — это
        /// уже не «видно, каким скином играешь», а пёстрая каша поверх того, от
        /// чего игрок уворачивается. Три хватает: больше трёх препятствий в зоне
        /// досягаемости стража одновременно и не бывает.
        /// </summary>
        const int MaxActive = 3;

        /// <summary>
        /// Потолок длительности статуса. В таблице материалов есть значения до
        /// четырёх секунд; дольше трёх статус живёт уже за пределами того отрезка,
        /// где игрок связывает его со своим ударом.
        /// </summary>
        public const float MaxDuration = 3f;

        struct Slot
        {
            public Transform Target;
            public SpriteRenderer Source;
            public SpriteRenderer Skin;
            public SpriteRenderer Glow;

            public StatusKind Kind;
            public Color Hot;
            public Color Cool;
            public float Left;
            public float Total;
            public int Stacks;
            public bool Busy;
            /// <summary>Видимая сила накладки, 0…1. Меняется не резче Rise.</summary>
            public float Shown;
        }

        /// <summary>
        /// Быстрее этого накладка не появляется и не гаснет, с. Прежде повторный
        /// удар тем же статусом сбрасывал нарастание: накладка гасла в 0 за кадр
        /// и разгоралась снова — прочное препятствие мигало на каждом ударе (E-12).
        /// </summary>
        const float Rise = 0.15f;

        Slot[] _slots;

        public void Init()
        {
            _slots = new Slot[MaxActive];

            for (int i = 0; i < MaxActive; i++)
            {
                _slots[i].Skin = NewLayer("StatusSkin");
                _slots[i].Glow = NewLayer("StatusGlow");
                _slots[i].Glow.sprite = SpriteFactory.SoftDot();
            }
        }

        SpriteRenderer NewLayer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.enabled = false;
            return sr;
        }

        // ------------------------------------------------------------ наложение

        /// <summary>
        /// Зажечь (заморозить, разъесть) цель.
        ///
        /// Повторный удар тем же статусом не начинает всё заново, а усиливает:
        /// прочную створку бьют трижды, и к третьему удару она горит сильнее.
        /// </summary>
        public void Apply(Transform target, SpriteRenderer source, StatusKind kind,
                          Color hot, Color cool, float duration)
        {
            if (_slots == null || target == null || source == null) return;

            int slot = Find(target);
            if (slot < 0) slot = FindFree();

            // Свободных нет — тише откажемся, чем отнимем эффект у соседа: мигание
            // статуса, который только что был, заметнее его отсутствия.
            if (slot < 0) return;

            bool same = _slots[slot].Busy && _slots[slot].Kind == kind;
            bool continuing = _slots[slot].Busy && _slots[slot].Target == target;
            if (!continuing) _slots[slot].Shown = 0f;

            _slots[slot].Target = target;
            _slots[slot].Source = source;
            _slots[slot].Kind = kind;
            _slots[slot].Hot = hot;
            _slots[slot].Cool = cool;
            _slots[slot].Total = Mathf.Max(0.5f, duration);
            _slots[slot].Left = _slots[slot].Total;
            _slots[slot].Stacks = same ? Mathf.Min(3, _slots[slot].Stacks + 1) : 1;
            _slots[slot].Busy = true;

            // Оба слоя становятся детьми цели: дальше они повторяют её движение и
            // поворот сами, без единой строки кода на сопровождение.
            Bind(_slots[slot].Skin, target, source, source.sortingOrder + 1);
            Bind(_slots[slot].Glow, target, source, source.sortingOrder - 1);

            _slots[slot].Skin.sprite = source.sprite;

            // Ореол крупнее силуэта — иначе его не видно за самим препятствием.
            _slots[slot].Glow.transform.localScale = Vector3.one * 1.8f;
        }

        static void Bind(SpriteRenderer layer, Transform target, SpriteRenderer source, int order)
        {
            layer.transform.SetParent(target, false);
            layer.transform.localPosition = Vector3.zero;
            layer.transform.localRotation = Quaternion.identity;
            layer.transform.localScale = Vector3.one;
            layer.sortingLayerID = source.sortingLayerID;
            layer.sortingOrder = order;
            layer.enabled = true;
        }

        int Find(Transform target)
        {
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Busy && _slots[i].Target == target) return i;
            return -1;
        }

        int FindFree()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].Busy) return i;

            // Все заняты — забираем самый выдохшийся: он и так почти погас.
            int oldest = -1;
            float least = float.MaxValue;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Left < least) { least = _slots[i].Left; oldest = i; }
            return oldest;
        }

        // ---------------------------------------------------------------- такт

        public void Tick(float dt)
        {
            if (_slots == null) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Busy) continue;

                // Цель могла уйти в пул или погибнуть — тогда слой обязан отцепиться
                // сам. Иначе он уедет вместе с переиспользованным объектом и будет
                // гореть на совершенно другом препятствии.
                var target = _slots[i].Target;
                if (target == null || !target.gameObject.activeInHierarchy)
                {
                    Release(i);
                    continue;
                }

                _slots[i].Left -= dt;
                if (_slots[i].Left <= 0f)
                {
                    Residue(i);
                    Release(i);
                    continue;
                }

                Paint(i, dt);
            }
        }

        void Paint(int i, float dt)
        {
            float t = 1f - Mathf.Clamp01(_slots[i].Left / _slots[i].Total);

            // Разгорается быстро, держится, гаснет к концу. Прежняя версия набирала
            // силу четверть длительности — для четырёхсекундного горения это целая
            // секунда, и первый удар выглядел так, будто ничего не произошло.
            // Нарастание и спад идут через Shown: повторный удар продолжает с
            // того, что видно, а не с нуля.
            float fade = Mathf.InverseLerp(1f, 0.78f, t);
            float stacks = Mathf.Lerp(0.8f, 1f, (_slots[i].Stacks - 1) / 2f);
            _slots[i].Shown = Mathf.MoveTowards(_slots[i].Shown, fade * stacks, dt / Rise);
            float strength = _slots[i].Shown * Visuals.StatusAlpha;

            // Без белого пересвета (E-08): светлый оттенок стихии, но сумма
            // каналов не выше 2.3.
            Color skin = Palette.SafeLight(SkinColor(i, t));
            skin.a *= strength;
            _slots[i].Skin.color = skin;

            Color glow = Palette.SafeLight(GlowColor(i, t));
            glow.a *= strength;
            _slots[i].Glow.color = glow;

            // Ореол дышит медленно и неглубоко: было 1.7 ± 0.18 на частоте
            // 9 рад/с (почти полтора раза в секунду) — пульсирующий свет, который
            // просили убрать. Стало 1.3 ± 0.05 примерно за три секунды на цикл.
            float breath = 1.3f + Mathf.Sin(Time.time * 2f + i) * 0.05f;
            _slots[i].Glow.transform.localScale = Vector3.one * breath * Visuals.Glow;
        }

        /// <summary>Цвет накладки по стадии — то, что видно на самой поверхности.</summary>
        Color SkinColor(int i, float t)
        {
            Color hot = _slots[i].Hot, cool = _slots[i].Cool;

            switch (_slots[i].Kind)
            {
                case StatusKind.Burn:
                    // Тление: раскалилось и обугливается. Мерцание на 34 рад/с
                    // убрано — горение должно читаться как жар, а не как лампа.
                    return Color.Lerp(hot, new Color(0.08f, 0.05f, 0.04f, 1f), Mathf.Pow(t, 1.2f))
                           .WithAlpha(0.88f);

                case StatusKind.Freeze:
                    return Color.Lerp(cool, Pale(cool), Mathf.Clamp01(t * 1.8f)).WithAlpha(0.8f);

                case StatusKind.Shock:
                    // Была ступенька «белый / цвет» почти четыре раза в секунду —
                    // самый настоящий строб. Стало плавное переливание между двумя
                    // оттенками стихии, один цикл примерно за секунду.
                    return Color.Lerp(hot, Pale(hot), 0.5f + Mathf.Sin(t * 6f) * 0.5f).WithAlpha(0.8f);

                case StatusKind.Corrode:
                    return Color.Lerp(hot, cool, t).WithAlpha(0.78f + Mathf.Sin(t * 6f) * 0.08f);

                case StatusKind.Gild:
                    // Размах до чистого белого срезан вдвое и переведён на светлый
                    // оттенок самого золота: блеск остался, пересвета нет.
                    return Color.Lerp(hot, Pale(hot), Mathf.Abs(Mathf.Sin(t * 4f)) * 0.5f).WithAlpha(0.8f);

                case StatusKind.Shadow:
                    return Color.Lerp(cool, new Color(0.02f, 0.02f, 0.05f, 1f), t).WithAlpha(0.9f);

                case StatusKind.Stardust:
                    // Перелив туда и обратно. Пила Repeat давала ступеньку
                    // «светлый → тёмный» до двух раз в секунду (E-13).
                    return Color.Lerp(hot, cool, Mathf.PingPong(t * 3.5f, 1f)).WithAlpha(0.75f);

                case StatusKind.Crystal:
                    return Color.Lerp(cool, Pale(cool), Mathf.Clamp01(t * 2f)).WithAlpha(0.74f);

                case StatusKind.Soak:
                    return hot.WithAlpha(0.5f + Mathf.Sin(t * 5f) * 0.1f);

                case StatusKind.Haze:
                    return Pale(cool).WithAlpha(0.3f + Mathf.Abs(Mathf.Sin(t * 3f)) * 0.18f);

                case StatusKind.Spectrum:
                    // Оттенок бежит по кругу — это не скачок яркости, а перелив;
                    // но яркость с единицы снижена, чтобы не спорить с фоном.
                    return Color.HSVToRGB(Mathf.Repeat(t * 1.2f, 1f), 0.6f, 0.82f).WithAlpha(0.7f);

                case StatusKind.Silt:
                    return Color.Lerp(cool, hot, t).WithAlpha(0.65f);

                case StatusKind.Petrify:
                    return Color.Lerp(hot, new Color(0.32f, 0.3f, 0.28f, 1f), t).WithAlpha(0.85f);

                default: // Glitch
                    // Была ступенька больше пяти раз в секунду. Распад остался
                    // узнаваемым за счёт резкой формы кривой, но сама кривая
                    // непрерывна и втрое медленнее.
                    return Color.Lerp(hot, cool, Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 5f)), 0.4f))
                           .WithAlpha(0.8f);
            }
        }

        /// <summary>Цвет ореола — то, что светится вокруг силуэта.</summary>
        Color GlowColor(int i, float t)
        {
            Color hot = _slots[i].Hot;

            switch (_slots[i].Kind)
            {
                case StatusKind.Burn:
                    // Жар оседает вместе с огнём. Дрожь на 26 рад/с убрана: ореол
                    // лежит под препятствием и мигал заметнее самой накладки.
                    return hot.WithAlpha((0.5f + Mathf.Sin(t * 3f) * 0.08f) * (1f - t * 0.5f));

                case StatusKind.Freeze:
                case StatusKind.Crystal:
                    return _slots[i].Cool.WithAlpha(0.5f);

                case StatusKind.Shock:
                    // Ступенька 0.8 / 0.25 почти четыре раза в секунду заменена
                    // плавным колебанием вдвое меньшего размаха.
                    return Pale(hot).WithAlpha(0.42f + Mathf.Sin(t * 6f) * 0.12f);

                case StatusKind.Shadow:
                    return new Color(0.05f, 0.03f, 0.1f, 1f).WithAlpha(0.7f);

                case StatusKind.Haze:
                    return Pale(_slots[i].Cool).WithAlpha(0.25f);

                default:
                    return hot.WithAlpha(0.35f);
            }
        }

        /// <summary>
        /// Светлая версия цвета стихии — вместо чистого белого.
        ///
        /// Белая накладка на тёмной воде и была главным источником пересвета:
        /// любое её мерцание читалось как вспышка. Светлый оттенок самой стихии
        /// даёт ту же «раскалённость» или «обледенелость», но не слепит и заодно
        /// не стирает принадлежность статуса к скину.
        /// </summary>
        static Color Pale(Color c) => Color.Lerp(c, Palette.Foam, 0.45f);

        /// <summary>
        /// След, который остаётся навсегда: копоть, иней, патина. По нему игрок
        /// узнаёт препятствие, которое уже било, — и видит, что удар не прошёл даром.
        /// </summary>
        void Residue(int i)
        {
            var source = _slots[i].Source;
            if (source == null) return;

            Color mark;
            switch (_slots[i].Kind)
            {
                case StatusKind.Burn: mark = new Color(0.42f, 0.36f, 0.33f, 1f); break;
                case StatusKind.Freeze: mark = new Color(0.78f, 0.9f, 1f, 1f); break;
                case StatusKind.Corrode: mark = new Color(0.72f, 0.86f, 0.55f, 1f); break;
                case StatusKind.Shadow: mark = new Color(0.45f, 0.42f, 0.55f, 1f); break;
                case StatusKind.Gild: mark = new Color(1f, 0.92f, 0.7f, 1f); break;
                default: mark = new Color(0.85f, 0.85f, 0.85f, 1f); break;
            }

            Color c = source.color;
            source.color = new Color(c.r * mark.r, c.g * mark.g, c.b * mark.b, c.a);
        }

        void Release(int i)
        {
            _slots[i].Busy = false;
            _slots[i].Target = null;
            _slots[i].Source = null;

            // Возвращаем слои себе: оставить их детьми чужого объекта — верный
            // способ однажды уехать вместе с ним в пул.
            _slots[i].Skin.transform.SetParent(transform, false);
            _slots[i].Glow.transform.SetParent(transform, false);
            _slots[i].Skin.enabled = false;
            _slots[i].Glow.enabled = false;
        }

        /// <summary>Снять всё: начало заплыва, бой с боссом, очистка поля.</summary>
        public void ClearAll()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Busy) Release(i);
        }

        /// <summary>Сколько статусов горит прямо сейчас — для частиц и отладки.</summary>
        public int ActiveCount
        {
            get
            {
                if (_slots == null) return 0;
                int n = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i].Busy) n++;
                return n;
            }
        }

        /// <summary>Перебор горящих целей: по ним GameManager сыплет частицы.</summary>
        public bool TryGetActive(int index, out Vector2 position, out float radius, out StatusKind kind)
        {
            position = Vector2.zero;
            radius = 0.3f;
            kind = StatusKind.Burn;

            if (_slots == null || index < 0 || index >= _slots.Length) return false;
            if (!_slots[index].Busy || _slots[index].Target == null) return false;

            position = _slots[index].Target.position;
            var source = _slots[index].Source;
            if (source != null) radius = Mathf.Max(0.12f, source.bounds.extents.magnitude * 0.7f);
            kind = _slots[index].Kind;
            return true;
        }
    }
}
