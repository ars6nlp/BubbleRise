using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Монеты, вылетающие из разбитого препятствия.
    ///
    /// Чистое украшение: баланс начисляется в <see cref="Wallet"/> в тот же миг,
    /// когда препятствие разрушено, и ни одна монета на экране ни на что не влияет.
    /// Сделано так нарочно — анимация не должна уметь терять деньги игрока, если
    /// кадр пропустили, игру свернули или заплыв кончился на полпути.
    ///
    /// Монеты сперва разлетаются, а потом сворачивают к счётчику: разлёт говорит
    /// «вот отсюда», сбор — «вот куда». Без первой половины награда не связана с
    /// местом, где игрок её заработал; без второй — непонятно, куда она делась.
    ///
    /// Пул и жёсткий потолок обязательны: мина при взрыве сносит соседей, и без
    /// ограничения один удар мог бы выбросить на экран сотню объектов.
    /// </summary>
    public class CoinFx : MonoBehaviour
    {
        const int MaxCoins = 40;
        const float CoinSize = 0.19f;
        const float ScatterTime = 0.35f;
        const float FlightTime = 0.75f;

        /// <summary>
        /// Как монета ведёт себя в воде. Четыре повадки вместо одной: из препятствия
        /// их вылетает пачка, и одинаковое вращение всей пачки читается как копия
        /// одного объекта, а не как горсть монет.
        /// </summary>
        enum CoinStyle
        {
            /// <summary>Вращение вокруг вертикальной оси: ширина дышит, высота нет.</summary>
            Flip,
            /// <summary>Кувыркается в плоскости экрана.</summary>
            Tumble,
            /// <summary>Качается из стороны в сторону, как лист в толще воды.</summary>
            Sway,
            /// <summary>Почти не крутится, но по ней пробегает блик.</summary>
            Shimmer,
        }

        struct Coin
        {
            public SpriteRenderer Sr;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Age;
            public float Spin;
            public float Seed;
            public CoinStyle Style;
            public bool Alive;
        }

        Coin[] _coins;
        Vector2 _target;
        Transform _root;

        public void Init(Transform root)
        {
            _root = root;
            _coins = new Coin[MaxCoins];

            for (int i = 0; i < MaxCoins; i++)
            {
                var go = new GameObject("Coin");
                go.transform.SetParent(_root, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sprite = SpriteFactory.Coin();
                sr.sortingOrder = SortingOrders.Coin;
                go.SetActive(false);
                _coins[i] = new Coin { Sr = sr };
            }
        }

        /// <summary>Куда монеты слетаются — мировая точка под счётчиком баланса.</summary>
        public void SetTarget(Vector2 worldPosition) => _target = worldPosition;

        /// <summary>
        /// Высыпать монеты из точки. Число ограничено: за створку дают десять монет,
        /// но десять летящих кругляшей закрыли бы собой следующую угрозу, поэтому
        /// показываем до шести, а точную цифру говорит надпись «+N».
        /// </summary>
        public void Spill(Vector2 position, int amount)
        {
            int visible = Mathf.Clamp(amount, 1, 6);
            for (int i = 0; i < visible; i++) Launch(position);
        }

        void Launch(Vector2 position)
        {
            for (int i = 0; i < _coins.Length; i++)
            {
                if (_coins[i].Alive) continue;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                _coins[i].Position = position;
                _coins[i].Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(1.2f, 2.6f);
                _coins[i].Age = 0f;
                _coins[i].Spin = Random.Range(-240f, 240f);
                _coins[i].Seed = Random.Range(0f, 10f);
                _coins[i].Style = (CoinStyle)Random.Range(0, 4);
                _coins[i].Alive = true;

                var sr = _coins[i].Sr;
                sr.transform.position = position;
                sr.transform.localScale = Vector3.one * CoinSize;
                sr.color = Color.white;
                sr.gameObject.SetActive(true);
                return;
            }
            // Свободных нет — молча пропускаем. Потерять нечего: деньги уже начислены.
        }

        public void Tick(float dt)
        {
            if (_coins == null) return;

            for (int i = 0; i < _coins.Length; i++)
            {
                if (!_coins[i].Alive) continue;

                _coins[i].Age += dt;
                float age = _coins[i].Age;

                if (age < ScatterTime)
                {
                    // Разлёт в воде: скорость гаснет вязко, а не по прямой.
                    _coins[i].Velocity *= Mathf.Pow(0.02f, dt);
                    _coins[i].Velocity += Vector2.up * (0.9f * dt);   // лёгкое всплытие
                    _coins[i].Position += _coins[i].Velocity * dt;
                }
                else
                {
                    // Сбор: чем ближе к счётчику, тем быстрее — иначе хвост полёта
                    // выглядит вялым и тянется дольше, чем нужно.
                    float t = Mathf.Clamp01((age - ScatterTime) / FlightTime);
                    float ease = t * t;
                    _coins[i].Position = Vector2.Lerp(_coins[i].Position, _target, 1f - Mathf.Exp(-(2f + ease * 14f) * dt));

                    if (t >= 1f || Vector2.Distance(_coins[i].Position, _target) < 0.12f)
                    {
                        Retire(i);
                        continue;
                    }
                }

                ApplyStyle(i, dt, age);
            }
        }

        /// <summary>Повадка монеты. Вся разница между четырьмя стилями — здесь.</summary>
        void ApplyStyle(int i, float dt, float age)
        {
            var tr = _coins[i].Sr.transform;
            float seed = _coins[i].Seed;
            tr.position = _coins[i].Position;

            switch (_coins[i].Style)
            {
                case CoinStyle.Flip:
                {
                    // Ребром монета почти исчезает — отсюда нижняя граница ширины.
                    float flip = Mathf.Abs(Mathf.Cos(age * 6f + seed));
                    tr.localScale = new Vector3(CoinSize * Mathf.Max(0.16f, flip), CoinSize, 1f);
                    tr.rotation = Quaternion.identity;
                    break;
                }

                case CoinStyle.Tumble:
                    tr.Rotate(0f, 0f, _coins[i].Spin * dt);
                    tr.localScale = Vector3.one * CoinSize;
                    break;

                case CoinStyle.Sway:
                {
                    // Качается, как лист: наклон вправо-влево вокруг своей оси.
                    float tilt = Mathf.Sin(age * 4.5f + seed) * 26f;
                    tr.rotation = Quaternion.Euler(0f, 0f, tilt);
                    float squeeze = 0.82f + Mathf.Abs(Mathf.Cos(age * 4.5f + seed)) * 0.18f;
                    tr.localScale = new Vector3(CoinSize * squeeze, CoinSize, 1f);
                    break;
                }

                default: // Shimmer
                {
                    // Блик: монета почти неподвижна, но вспыхивает — так в пачке
                    // появляются «спокойные» монеты, и пачка перестаёт мельтешить.
                    tr.Rotate(0f, 0f, _coins[i].Spin * 0.25f * dt);
                    float pulse = 0.94f + Mathf.Sin(age * 9f + seed) * 0.06f;
                    tr.localScale = Vector3.one * (CoinSize * pulse);

                    float glow = 0.8f + Mathf.Max(0f, Mathf.Sin(age * 9f + seed)) * 0.2f;
                    _coins[i].Sr.color = new Color(glow, glow, glow, 1f);
                    break;
                }
            }
        }

        void Retire(int i)
        {
            _coins[i].Alive = false;
            _coins[i].Sr.gameObject.SetActive(false);
        }

        public void ClearAll()
        {
            if (_coins == null) return;
            for (int i = 0; i < _coins.Length; i++)
                if (_coins[i].Alive) Retire(i);
        }
    }
}
