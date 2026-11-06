#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Счётчик «образов стража» для дев-панели (F1): сколько рендереров в кадре рисуют
    /// стража и насколько далеко от него стоят его слои — сам рисунок и оболочка удара.
    ///
    /// Зачем. Игроки видели «вторую защиту» перед пузырьком при ударе. Причиной была
    /// оболочка удара, отстававшая от стража на кадр (GameManager.TickEnvelope); этот
    /// счётчик ловит и её, и любой будущий двойник — на телефоне, где отладчика нет.
    ///
    /// «Образ стража» — видимый рендерер со спрайтом стража или с его текстурой (сетка
    /// мини-босса). Норма — ровно один, на месте стража. Слои — образы и корни
    /// оболочек; допуск — <see cref="Tolerance"/>.
    ///
    /// Только в редакторе и дев-сборке: обход всех рендереров каждый кадр в релизе
    /// не нужен.
    /// </summary>
    public static class GuardianImageWatch
    {
        public const float Tolerance = 0.02f;

        public static int Images { get; private set; }
        public static int MaxImages { get; private set; }
        public static float Offset { get; private set; }
        public static int Violations { get; private set; }
        public static string LastViolation { get; private set; } = "—";

        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        static readonly int MainTex = Shader.PropertyToID("_MainTex");

        public static void Reset()
        {
            MaxImages = 0;
            Violations = 0;
            LastViolation = "—";
        }

        /// <summary>Посчитать образы стража и самый дальний его слой.</summary>
        public static void Scan(GuardianOrb guardian, out int images, out float offset)
        {
            images = 0;
            offset = 0f;
            if (guardian == null) return;

            var host = guardian.GetComponent<SpriteRenderer>();
            Sprite sprite = host != null ? host.sprite : null;
            Texture texture = sprite != null ? sprite.texture : null;
            Vector2 at = guardian.transform.position;

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff) continue;
                bool image = false;
                if (r is SpriteRenderer sr)
                    image = sprite != null && sr.sprite != null && sr.color.a > 0.02f
                            && (sr.sprite == sprite || sr.sprite.texture == texture);
                else if (r is MeshRenderer mr && texture != null)
                {
                    mr.GetPropertyBlock(Block);
                    image = Block.GetTexture(MainTex) == texture;
                }
                if (!image) continue;
                images++;
                offset = Mathf.Max(offset, Vector2.Distance(r.transform.position, at));
            }

            foreach (var root in CollisionEnvelope.DebugRoots)
                if (root != null && root.gameObject.activeInHierarchy)
                    offset = Mathf.Max(offset, Vector2.Distance(root.position, at));
        }

        public static void Tick(GuardianOrb guardian, string skin)
        {
            Scan(guardian, out int images, out float offset);
            Images = images;
            Offset = offset;
            if (images > MaxImages) MaxImages = images;
            if (images > 1 || offset > Tolerance)
            {
                Violations++;
                LastViolation = $"{Time.time:F1} с, {skin}: образов {images}, слой в {offset:F2} ю от стража";
            }
        }
    }
}
#endif
