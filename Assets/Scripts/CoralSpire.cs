using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Длинный коралл — ветвь из перекрывающихся звеньев с боковыми отростками.
    /// Перекрывает проход почти целиком, так что объехать её нельзя (пузырёк
    /// зафиксирован по центру) — надо отломить стражем звено на своём пути.
    ///
    /// Звенья ставятся с шагом меньше своей длины: цепочка выглядит сплошной,
    /// но после одного удара соседи расходятся достаточно, чтобы пузырёк прошёл.
    /// </summary>
    public class CoralSpire : MonoBehaviour
    {
        public class Segment
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public bool Alive;
            public float DyingTimer;
            public float Radius;
            public float Size;
            public Vector2 Center => Tr.position;
        }

        public bool Pooled { get; set; }
        public IReadOnlyList<Segment> Segments => _segments;

        readonly List<Segment> _segments = new List<Segment>();
        int _used;
        float _spin;
        float _reach;

        public void Activate(Vector2 position, float angleDegrees, float spin, float reach)
        {
            Pooled = false;
            _spin = spin;
            _reach = reach;

            transform.position = position;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);

            _used = 0;
            gameObject.SetActive(true);
        }

        public void AddSegment(Vector2 localPosition, float localAngle, float size, float radius)
        {
            var segment = Rent();
            segment.Alive = true;
            segment.DyingTimer = 0f;
            segment.Radius = radius;
            segment.Size = size;

            segment.Sr.sprite = SpriteFactory.CoralLimb(Random.value < 0.5f ? 0 : 1);
            segment.Sr.color = Color.white;
            segment.Sr.enabled = true;

            segment.Tr.localPosition = localPosition;
            segment.Tr.localRotation = Quaternion.Euler(0f, 0f, localAngle);
            segment.Tr.localScale = Vector3.one * size;
            segment.Tr.gameObject.SetActive(true);
        }

        /// <summary>Спрятать звенья, оставшиеся от прошлой активации.</summary>
        public void FinishSetup()
        {
            for (int i = _used; i < _segments.Count; i++)
            {
                _segments[i].Alive = false;
                _segments[i].DyingTimer = 0f;
                _segments[i].Tr.gameObject.SetActive(false);
            }
        }

        Segment Rent()
        {
            if (_used < _segments.Count) return _segments[_used++];

            var go = new GameObject("Limb");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.sortingOrder = SortingOrders.Formation;

            var segment = new Segment { Tr = go.transform, Sr = sr };
            _segments.Add(segment);
            _used++;
            return segment;
        }

        public void Shatter(Segment segment)
        {
            if (!segment.Alive) return;
            segment.Alive = false;
            segment.DyingTimer = 0.26f;
        }

        public void ShatterAll()
        {
            for (int i = 0; i < _used; i++)
                if (_segments[i].Alive) Shatter(_segments[i]);
        }

        public bool Tick(float dt, float scrollSpeed, Playfield field)
        {
            var p = transform.position;
            p.y -= scrollSpeed * dt;
            transform.position = p;

            if (Mathf.Abs(_spin) > 0.01f)
                transform.Rotate(0f, 0f, _spin * dt);

            bool anyVisible = false;
            for (int i = 0; i < _used; i++)
            {
                var s = _segments[i];
                if (s.Alive) { anyVisible = true; continue; }
                if (s.DyingTimer <= 0f) continue;

                s.DyingTimer -= dt;
                float k = Mathf.Clamp01(s.DyingTimer / 0.26f);
                s.Tr.localScale = Vector3.one * (s.Size * (0.35f + 0.65f * k));
                var col = s.Sr.color;
                col.a = k;
                s.Sr.color = col;

                if (s.DyingTimer <= 0f) s.Tr.gameObject.SetActive(false);
                else anyVisible = true;
            }

            if (!anyVisible) return false;
            return p.y > field.Bottom - _reach - 1f;
        }

        public void Deactivate()
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                _segments[i].Alive = false;
                _segments[i].DyingTimer = 0f;
                _segments[i].Tr.gameObject.SetActive(false);
            }
            _used = 0;
            gameObject.SetActive(false);
        }
    }
}
