using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Краб, ползущий по боковому камню. Чистая декорация: геймплея не касается,
    /// столкновений нет, живёт в слое Scenery позади всего.
    ///
    /// Ориентация. Краб цепляется за стену и смотрит внутрь арены и вверх: справа —
    /// вверх-влево, слева — вверх-вправо. Из-за поворота его собственная поперечная
    /// ось (та, вдоль которой краб и переставляет ноги) идёт почти вертикально,
    /// поэтому движение по стене читается как боковая крабья походка, а не как
    /// «спрайт едет вниз».
    ///
    /// Скорость. Своя скорость ползания заметно меньше скорости прокрутки, поэтому
    /// краб всегда суммарно уезжает вниз — даже когда ползёт «вверх». Иначе он завис
    /// бы на экране и сломал бы ощущение подъёма, ради которого весь этот декор и есть.
    /// </summary>
    public class SideCrab : MonoBehaviour
    {
        public bool Pooled { get; set; }

        SpriteRenderer _sr;

        float _parallax;
        float _size;
        float _crawl;        // со знаком: + вверх по стене, − вниз
        float _baseX;
        float _tilt;
        int _side;

        // походка
        int _frame;
        float _frameTimer;
        float _stepInterval;

        // краб то ползёт, то замирает — так он читается живым, а не заводным
        bool _walking;
        float _stateTimer;

        float _swayPhase;

        public void Setup()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Scenery;
        }

        public void Activate(int side, Vector2 position, float size, float crawl,
                             float parallax, float tilt)
        {
            Pooled = false;
            _side = side;
            _size = size;
            _crawl = crawl;
            _parallax = parallax;
            _tilt = tilt;
            _baseX = position.x;

            _frame = Random.Range(0, 3);
            _stepInterval = 0.16f;
            _frameTimer = 0f;
            _swayPhase = Random.Range(0f, 10f);

            _walking = true;
            _stateTimer = Random.Range(1.6f, 4.0f);

            _sr.sprite = SpriteFactory.SideCrab(_frame);
            _sr.flipX = false;

            // Та же атмосферная перспектива, что у остальных декораций: дальний краб
            // тонет в воде, ближний ярче. Без этого он выпадал бы из фона пятном.
            float nearness = Mathf.InverseLerp(0.45f, 1f, parallax);
            _sr.color = Color.Lerp(new Color(0.34f, 0.42f, 0.46f, 0.80f),
                                   new Color(0.86f, 0.80f, 0.76f, 1f), nearness);

            transform.position = position;
            transform.localScale = Vector3.one * size;
            transform.rotation = Quaternion.Euler(0f, 0f, _side * _tilt);
            gameObject.SetActive(true);
        }

        public bool Tick(float dt, float scrollSpeed, Playfield field)
        {
            _stateTimer -= dt;
            if (_stateTimer <= 0f)
            {
                _walking = !_walking;
                _stateTimer = _walking ? Random.Range(2.0f, 4.5f) : Random.Range(0.5f, 1.4f);
            }

            if (_walking)
            {
                // темп перебора ног привязан к скорости: медленный краб не семенит
                _frameTimer -= dt;
                if (_frameTimer <= 0f)
                {
                    _frameTimer = _stepInterval / Mathf.Max(0.25f, Mathf.Abs(_crawl) / 0.45f);
                    _frame = (_frame + 1) % 3;
                    _sr.sprite = SpriteFactory.SideCrab(_frame);
                }
            }

            _swayPhase += dt;

            var p = transform.position;
            p.y -= scrollSpeed * _parallax * dt;
            if (_walking) p.y += _crawl * dt;

            // лёгкое покачивание поперёк движения — краб жмётся к камню
            p.x = _baseX + Mathf.Sin(_swayPhase * 2.4f) * 0.035f * _size;
            transform.position = p;

            return p.y > field.Bottom - _size;
        }

        public void Deactivate() => gameObject.SetActive(false);
    }
}
