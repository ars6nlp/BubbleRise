using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Мини-босс в интерфейсе: карточка, превью, «Сравнить», «Показать эффекты».
    /// Та же текстура и та же сетка с изгибом, что в игре (<see cref="MiniBossMotion.Fill"/>).
    ///
    /// Кладётся дочерним объектом на картинку шарика и растягивается по ней; пока
    /// показан трофей, сама картинка выключена.
    /// </summary>
    public sealed class MiniBossGraphic : MaskableGraphic
    {
        bool _on;
        MiniBossKey _key;
        Texture _tex;
        Vector2[] _pos;

        public override Texture mainTexture => _tex != null ? _tex : s_WhiteTexture;

        /// <summary>Сетка мини-босса поверх картинки шарика; создаётся один раз.</summary>
        public static MiniBossGraphic On(Image icon)
        {
            var t = icon.transform.Find("MiniBoss");
            var g = t != null ? t.GetComponent<MiniBossGraphic>() : null;
            if (g != null) return g;
            var go = new GameObject("MiniBoss", typeof(RectTransform), typeof(CanvasRenderer));
            var rt = (RectTransform)go.transform;
            rt.SetParent(icon.transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            g = go.AddComponent<MiniBossGraphic>();
            g.raycastTarget = false;
            g.enabled = false;
            return g;
        }

        /// <summary>
        /// Показать вид на картинке <paramref name="icon"/>: трофей — сеткой мини-босса,
        /// любой другой скин — как раньше, самой картинкой. Цвет — тот, что вызывающий
        /// уже положил на картинку (затемнение закрытых карточек и т. п.).
        /// </summary>
        public static void Apply(Image icon, SkinLook look, bool visible = true)
        {
            if (icon == null) return;
            var g = look.IsBoss ? On(icon) : FindOn(icon);
            if (!look.IsBoss)
            {
                if (g != null) g.Set(false, default);
                icon.enabled = visible;
                return;
            }
            icon.enabled = false;
            g.color = icon.color;
            g.Set(visible, look.MiniBoss);
        }

        static MiniBossGraphic FindOn(Image icon)
        {
            var t = icon.transform.Find("MiniBoss");
            return t != null ? t.GetComponent<MiniBossGraphic>() : null;
        }

        void Set(bool on, MiniBossKey key)
        {
            _on = on;
            enabled = on;
            if (!on) return;
            if (!_key.Equals(key) || _tex == null)
            {
                _key = key;
                _tex = SpriteFactory.MiniBoss(key).texture;
                UpdateMaterial();
            }
            SetVerticesDirty();
        }

        public MiniBossKey Key => _key;
        public Vector2[] LastPositions => _pos;

        void Update()
        {
            if (_on) SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (!_on) return;
            if (_pos == null) _pos = new Vector2[MiniBossMotion.VertexCount];
            MiniBossMotion.Fill(_key, MiniBossMotion.Now, MiniBossMotion.Amplitude, _pos);

            // квадрат по меньшей стороне: рисунок не растягивается вместе с картинкой
            var r = GetPixelAdjustedRect();
            float side = Mathf.Min(r.width, r.height);
            Vector2 c = r.center;
            Color32 col = color;
            var uv = MiniBossMotion.Uvs;
            for (int i = 0; i < _pos.Length; i++)
                vh.AddVert(new Vector3(c.x + _pos[i].x * side, c.y + _pos[i].y * side, 0f), col, uv[i]);
            var tris = MiniBossMotion.Triangles;
            for (int i = 0; i < tris.Length; i += 3) vh.AddTriangle(tris[i], tris[i + 1], tris[i + 2]);
        }
    }
}
