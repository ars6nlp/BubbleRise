using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Общее течение: водоросли, мягкие кораллы, актинии и черви качаются в одну
    /// сторону, с лёгким запаздыванием по высоте — волна идёт снизу вверх.
    ///
    /// Один качок за <see cref="Period"/> с, поверх — медленная вторая волна:
    /// без неё всё поле водорослей качалось бы метрономом.
    /// </summary>
    public static class OceanCurrent
    {
        /// <summary>Период основного качка, с. Правило — не быстрее 6–8 с.</summary>
        public const float Period = 6.5f;

        public static float Time { get; private set; }

        public static void Tick(float dt) => Time += dt;

        /// <summary>Отклонение −1…1 для точки на высоте <paramref name="y"/> (мировые юниты).</summary>
        public static float At(float y, float seed = 0f)
        {
            float a = (Time / Period) * Mathf.PI * 2f - y * 0.35f + seed;
            float b = (Time / (Period * 2.3f)) * Mathf.PI * 2f - y * 0.18f + seed * 0.5f;
            return Mathf.Sin(a) * 0.75f + Mathf.Sin(b) * 0.25f;
        }
    }
}
