using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Куда повесить медузу и насколько ужать её вид, чтобы она влезла в экран.
    ///
    /// **Почему отдельная функция, а не метод контроллера.** Её обязаны считать
    /// двое: сам босс в рантайме и проверка по форматам экрана, которая работает
    /// без Play Mode. Две копии формулы разошлись бы при первой же правке — в
    /// этом проекте это уже случалось со следом скина и с оболочкой.
    ///
    /// **Что считается.** Высота мира постоянна (worldHeight), а ширина игровой
    /// колонки зависит от соотношения сторон, и размер босса считается от неё.
    /// На широком экране колонка упирается в designAspect, босс выходит крупнее,
    /// и верх колец поля подходит к кромке вплотную: при PlayHalfWidth = 3.2
    /// запас был 0.096 юнита — меньше процента высоты экрана. [РАСЧЁТ]
    ///
    /// **Чего она НЕ трогает.** Радиус купола и радиус поля — это геймплей
    /// (хитбокс и зона отталкивания). Ужимается только вид.
    /// </summary>
    public static class JellyFit
    {
        /// <summary>Отступ от кромки экрана, в долях половины его высоты.</summary>
        public const float Margin = 0.04f;

        /// <summary>Насколько вид торчит вверх от центра, в долях своего масштаба.</summary>
        public static float Up(float hazeSpan, float fieldSpan)
            => Mathf.Max(0.78f, Mathf.Max(hazeSpan, fieldSpan)) * 0.5f;

        /// <summary>Насколько вид торчит вниз: щупальца длиннее купола.</summary>
        public static float Down(float hazeSpan, float fieldSpan, int links)
            => Mathf.Max(0.12f + links * 0.2f * 1.15f,
                         Mathf.Max(hazeSpan, fieldSpan) * 0.5f) * 0.5f;

        /// <summary>
        /// Решить, где зависнуть и во сколько раз ужать вид.
        /// </summary>
        /// <param name="halfHeight">Половина высоты мира.</param>
        /// <param name="bellRadius">Радиус купола в мире (геймплейный).</param>
        /// <param name="fieldSpan">Радиус поля в долях купола.</param>
        /// <param name="hoverY">Желаемая высота зависания.</param>
        public static void Solve(float halfHeight, float bellRadius, float hazeSpan,
                                 float fieldSpan, float hoverY, int links,
                                 out float targetY, out float visualFit)
        {
            visualFit = 1f;

            float scale = bellRadius * 2f;
            float margin = halfHeight * Margin;

            float up = Up(hazeSpan, fieldSpan) * scale;
            float down = Down(hazeSpan, fieldSpan, links) * scale;

            float roomTop = halfHeight - margin;
            float roomBottom = -halfHeight + margin;

            // Если вид не помещается даже по всей высоте — ужимаем его.
            float need = up + down;
            float have = roomTop - roomBottom;
            if (need > have && need > 0.0001f)
            {
                visualFit = have / need;
                up *= visualFit;
                down *= visualFit;
            }

            // Опускаем, если верх выходит за кромку; поднимаем, если низ.
            targetY = Mathf.Min(hoverY, roomTop - up);
            targetY = Mathf.Max(targetY, roomBottom + down);
        }

        /// <summary>
        /// Влезает ли вид целиком. Для проверки по форматам: считает то же, что
        /// <see cref="Solve"/>, и сверяет результат с кромками.
        /// </summary>
        public static bool Fits(float halfHeight, float bellRadius, float hazeSpan,
                                float fieldSpan, float hoverY, int links,
                                out float top, out float bottom)
        {
            Solve(halfHeight, bellRadius, hazeSpan, fieldSpan, hoverY, links,
                  out float targetY, out float fit);

            float scale = bellRadius * 2f * fit;
            top = targetY + Up(hazeSpan, fieldSpan) * scale;
            bottom = targetY - Down(hazeSpan, fieldSpan, links) * scale;

            return top <= halfHeight + 0.0001f && bottom >= -halfHeight - 0.0001f;
        }

        /// <summary>
        /// Половина ширины игровой колонки — та же формула, что в Playfield.
        /// Нужна проверке, которая считает без запущенной сцены.
        /// </summary>
        public static float PlayHalfWidth(float halfHeight, float aspect, float designAspect)
            => Mathf.Min(halfHeight * aspect, halfHeight * designAspect);
    }
}
