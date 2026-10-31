using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Лёгкая система частиц на пуле спрайтов. Своя, а не Unity Particle System:
    /// поведение 1:1 с прототипом, ноль аллокаций в рантайме и никаких материалов-сюрпризов в URP.
    /// </summary>
    public class FxManager : MonoBehaviour
    {
        class Particle
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public Vector2 Velocity;
            public float Age;
            public float Life;
            public float Size;
            public Color Color;
            public float MaxAlpha;
            public bool Active;
        }

        Particle[] _particles;
        int _cursor;
        float _gravity = -0.79f; // юнитов/сек^2 (в прототипе 60 px/сек^2 вниз)

        public void Init(GameConfig cfg)
        {
            int count = Mathf.Max(32, cfg.maxParticles);
            _particles = new Particle[count];
            var sprite = SpriteFactory.SoftDot();

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("P");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.Particle;
                go.SetActive(false);
                _particles[i] = new Particle { Tr = go.transform, Sr = sr };
            }
        }

        float _density = 1f;
        float _thin;

        /// <summary>Проявление частицы, с. Не дольше пятой части её жизни.</summary>
        const float FadeIn = 0.08f;

        /// <summary>
        /// Плотность частиц для следующих вызовов: доля, которая действительно
        /// родится. Разрушение и добивания легендарных ставят её по
        /// <see cref="Visuals.Particles"/> (E-10): прежде «Тихо» их не трогало.
        /// Лишние частицы снимаются равномерно, а не хвостом: у кольца не
        /// появляется дыры с одной стороны.
        /// </summary>
        public void SetDensity(float density)
        {
            _density = Mathf.Clamp01(density);
            _thin = 0f;
        }

        public void Burst(Vector2 position, Color color, int count,
                          float speedMin = 0.5f, float speedMax = 2.4f,
                          float sizeMin = 0.05f, float sizeMax = 0.11f)
        {
            // Без белого пересвета (E-08): у 20 из 36 цветов удара сумма каналов
            // была выше 2.7, у восьми — чистый белый. И тише на «Тихо».
            color = Palette.SafeLight(color);
            float maxAlpha = Visuals.ParticleAlpha;
            if (_density < 1f) count = Mathf.Max(1, Mathf.RoundToInt(count * _density));

            for (int i = 0; i < count; i++)
            {
                var p = NextFree();
                if (p == null) return;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float speed = Random.Range(speedMin, speedMax);

                p.Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
                p.Age = 0f;
                p.Life = Random.Range(0.4f, 0.9f);
                p.Size = Random.Range(sizeMin, sizeMax);
                p.Color = color;
                p.MaxAlpha = maxAlpha;
                p.Active = true;

                p.Tr.position = position;
                p.Tr.localScale = Vector3.one * p.Size;
                p.Sr.color = color.WithAlpha(0f);
                p.Sr.sortingOrder = SortingOrders.Particle;
                p.Tr.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Одиночная частица с полным контролем — используется для следа пузырька:
        /// он тянется вниз со скоростью подъёма, показывая, что мы движемся вверх.
        /// </summary>
        public void Emit(Vector2 position, Vector2 velocity, Color color, float life, float size,
                         float maxAlpha = 1f, int sortingOrder = SortingOrders.Particle)
        {
            if (_density < 1f)
            {
                _thin += 1f - _density;
                if (_thin >= 1f) { _thin -= 1f; return; }
            }

            var p = NextFree();
            if (p == null) return;

            color = Palette.SafeLight(color);
            maxAlpha *= Visuals.ParticleAlpha;

            p.Velocity = velocity;
            p.Age = 0f;
            p.Life = Mathf.Max(0.05f, life);
            p.Size = size;
            p.Color = color;
            p.MaxAlpha = maxAlpha;
            p.Active = true;

            p.Tr.position = position;
            p.Tr.localScale = Vector3.one * size;
            p.Sr.color = color.WithAlpha(0f);
            p.Sr.sortingOrder = sortingOrder;
            p.Tr.gameObject.SetActive(true);
        }

        Particle NextFree()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                _cursor = (_cursor + 1) % _particles.Length;
                if (!_particles[_cursor].Active) return _particles[_cursor];
            }
            return null; // все заняты — пропускаем, вместо того чтобы «мигать» чужой частицей
        }

        public void Tick(float dt)
        {
            if (_particles == null) return;

            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];
                if (!p.Active) continue;

                p.Age += dt;
                if (p.Age >= p.Life)
                {
                    p.Active = false;
                    p.Tr.gameObject.SetActive(false);
                    continue;
                }

                p.Velocity.y += _gravity * dt;
                p.Tr.position += (Vector3)(p.Velocity * dt);

                float a = 1f - p.Age / p.Life;
                // Частица проявляется за FadeIn, а не возникает за кадр: на
                // тёмной воде одиночные кадры искр давали перепады яркости.
                float fadeIn = Mathf.Min(FadeIn, p.Life * 0.2f);
                if (p.Age < fadeIn) a *= p.Age / fadeIn;
                var c = p.Color;
                c.a = a * p.MaxAlpha;
                p.Sr.color = c;
                p.Tr.localScale = Vector3.one * (p.Size * (0.6f + 0.4f * a));
            }
        }

        public void ClearAll()
        {
            if (_particles == null) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].Active = false;
                _particles[i].Tr.gameObject.SetActive(false);
            }
        }
    }
}
