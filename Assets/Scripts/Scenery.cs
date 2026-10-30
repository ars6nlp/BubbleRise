using UnityEngine;

namespace BubbleRise
{
    public enum SceneryKind { CoralPillar, RockLedge, RuinBlocks, RuinArch, Kelp }

    /// <summary>
    /// Декорация: коралловая колонна, каменный уступ, затонувшая постройка, арка, водоросль.
    /// Геймплея не касается — её задача чисто визуальная: это неподвижные объекты мира,
    /// мимо которых пузырёк проплывает. Именно они дают ощущение подъёма, а не падения предметов.
    ///
    /// Ключевой момент: движутся строго со скоростью прокрутки (с поправкой на параллакс),
    /// без случайного разброса — разнобой скоростей и читается как «предметы падают».
    /// </summary>
    public class Scenery : MonoBehaviour
    {
        public bool Pooled { get; set; }

        SpriteRenderer _sr;
        float _parallax;
        float _size;
        float _swayAmplitude;
        float _swaySpeed;
        float _phase;
        float _baseX;

        public void Setup()
        {
            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sharedMaterial = SpriteFactory.Unlit;
            _sr.sortingOrder = SortingOrders.Scenery;
        }

        public void Activate(SceneryKind kind, Vector2 position, float size, bool flip,
                             float parallax, float swayAmplitude)
        {
            Pooled = false;
            _parallax = parallax;
            _size = size;
            _baseX = position.x;
            _swayAmplitude = swayAmplitude;
            _swaySpeed = Random.Range(0.5f, 0.9f);
            _phase = Random.Range(0f, 10f);

            _sr.sprite = SpriteFor(kind);
            _sr.flipX = flip;

            // Дальние объекты тонут в толще воды — атмосферная перспектива подчёркивает глубину.
            float nearness = Mathf.InverseLerp(0.45f, 1f, parallax);
            _sr.color = Color.Lerp(new Color(0.30f, 0.44f, 0.50f, 0.75f),
                                   new Color(0.72f, 0.82f, 0.85f, 1f), nearness);

            transform.position = position;
            transform.localScale = Vector3.one * size;
            transform.rotation = Quaternion.identity;
            gameObject.SetActive(true);
        }

        static Sprite SpriteFor(SceneryKind kind)
        {
            switch (kind)
            {
                case SceneryKind.RockLedge: return SpriteFactory.RockLedge();
                case SceneryKind.RuinBlocks: return SpriteFactory.RuinBlocks();
                case SceneryKind.RuinArch: return SpriteFactory.RuinArch();
                case SceneryKind.Kelp: return SpriteFactory.Kelp();
                default: return SpriteFactory.CoralPillar();
            }
        }

        public bool Tick(float dt, float scrollSpeed, Playfield field)
        {
            _phase += dt;

            var p = transform.position;
            p.y -= scrollSpeed * _parallax * dt;
            p.x = _baseX + Mathf.Sin(_phase * _swaySpeed) * _swayAmplitude;
            transform.position = p;

            if (_swayAmplitude > 0.001f)
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * _swaySpeed) * 3.5f);

            return p.y > field.Bottom - _size;
        }

        public void Deactivate() => gameObject.SetActive(false);
    }
}
