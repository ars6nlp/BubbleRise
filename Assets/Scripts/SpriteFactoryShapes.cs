using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Силуэты стихий.
    ///
    /// **Зачем.** Все покупаемые скины рисовались одной и той же стеклянной
    /// оболочкой, и отличались только цветом начинки под ней. Издалека и боковым
    /// зрением это один шарик: цвет различают, когда на него смотрят, а форму —
    /// сразу. Поэтому у каждой стихии теперь свой контур.
    ///
    /// **Главное ограничение.** Все формы вписаны в единичный круг, и хитбокс у
    /// скинов общий (<c>GameConfig.bubbleRadius</c>). Шипы и лучи торчат наружу
    /// только на глаз — они не задевают препятствий и не защищают от них. Иначе
    /// дорогой скин был бы ещё и легче в игре, а это ровно то, чего просили не
    /// делать: отличия косметические, преимущества — нет.
    ///
    /// **Почему оболочка, а не целый шарик.** Внутри стекла лежит начинка
    /// (<see cref="MaterialCore"/>), и она у каждого материала своя. Форма меняет
    /// именно стекло: контур, ободок, блик. Так остаются видимыми оба слоя — и
    /// силуэт стихии, и материал под ним.
    /// </summary>
    public static partial class SpriteFactory
    {
        /// <summary>
        /// Оболочка по силуэту. Одна функция на все формы: контур считает
        /// <see cref="ShapeEdge"/>, остальное — общее стекло.
        /// </summary>
        public static Sprite ShapeShell(SkinShape shape)
        {
            return Build("shell_" + (int)shape, p =>
            {
                float d = p.magnitude;
                float edge = ShapeEdge(shape, p);      // где проходит край этой формы
                if (d > edge) return Color.clear;

                // Доля до края — по ней считается и дымка, и ободок. У ровного шара
                // это просто d, у остальных — d с поправкой на форму, поэтому ободок
                // идёт точно по контуру, а не по окружности.
                float t = edge > 0.001f ? d / edge : 1f;

                float haze = Mathf.Lerp(0.07f, 0.16f, t);
                Color c = new Color(0.82f, 0.98f, 0.96f, haze);

                if (t > 0.9f)
                {
                    float k = Mathf.InverseLerp(0.9f, 0.97f, Mathf.Min(t, 0.97f));
                    c = new Color(1f, 1f, 1f, 0.12f + k * 0.5f);
                }

                // Блик на месте у всех форм: это отражение света сверху слева, и
                // двигать его вслед за силуэтом не нужно — он от источника, а не
                // от предмета.
                Vector2 h = new Vector2((p.x + 0.32f) / 0.22f, (p.y - 0.36f) / 0.13f);
                if (h.sqrMagnitude < 1f && t < 0.95f) c = Blend(c, new Color(1f, 1f, 1f, 0.8f));

                return c;
            });
        }

        /// <summary>
        /// Радиус края формы в направлении точки. Единица — обычный круг.
        ///
        /// Все формы описаны одной функцией от угла, а не отдельными рисовалками:
        /// так форма и её оболочка не могут разойтись, и добавить новую — значит
        /// дописать сюда одну ветку.
        /// </summary>
        public static float ShapeEdge(SkinShape shape, Vector2 p)
        {
            float angle = Mathf.Atan2(p.y, p.x);
            float mag = Mathf.Max(0.0001f, p.magnitude);
            float ax = Mathf.Abs(p.x) / mag;      // |cos| направления
            float ay = Mathf.Abs(p.y) / mag;      // |sin| направления

            switch (shape)
            {
                // Капля: круглый низ и узкое остриё вверх.
                //
                // Остриё даёт высокая степень: при sin 0.9 прибавка ещё почти
                // нулевая, и только у самой вершины радиус резко растёт. Плавная
                // прибавка, как было сначала, давала не каплю, а тот же шар —
                // отличить его от обычного было нельзя.
                case SkinShape.Drop:
                {
                    float up = Mathf.Max(0f, p.y / mag);
                    return 0.72f + 0.28f * Mathf.Pow(up, 6f);
                }

                // Огранка: восьмиугольник. Расстояние до грани, а не до окружности.
                case SkinShape.Faceted:
                {
                    const int sides = 8;
                    float step = Mathf.PI * 2f / sides;
                    float local = Mathf.Repeat(angle + Mathf.PI / sides, step) - step * 0.5f;
                    return 0.97f * Mathf.Cos(step * 0.5f) / Mathf.Max(0.2f, Mathf.Cos(local));
                }

                // Шипы: корона коротких игл. Глубина между ними небольшая, иначе
                // силуэт перестаёт читаться как шарик.
                case SkinShape.Spiky:
                {
                    float teeth = Mathf.Cos(angle * 11f);
                    return 0.84f + 0.16f * Mathf.Pow(Mathf.Max(0f, teeth), 0.6f);
                }

                // Кольцо: шар и плоский поясок вокруг, как у Сатурна.
                //
                // Поясок виден с ребра, поэтому он именно полоса, а не окружность.
                // Тело пришлось уменьшить: при прежних 0.84 поясок торчал из него
                // двумя огрызками и читался как дефект рисунка, а не как кольцо.
                case SkinShape.Ringed:
                {
                    float band = Mathf.Abs(p.y + p.x * 0.2f);
                    if (band < 0.17f) return 1f;
                    return 0.70f;
                }

                // Клубок: мелкая рваная кромка. Три частоты, самая высокая —
                // главная: именно она читается как дымка, а не как форма.
                case SkinShape.Wisp:
                    return 0.86f + 0.04f * Mathf.Sin(angle * 5f)
                                 + 0.04f * Mathf.Sin(angle * 11f + 1.3f)
                                 + 0.03f * Mathf.Sin(angle * 19f + 0.4f);

                // Бугры: три крупных нароста и больше ничего. С прежними двумя
                // частотами выходил тот же клубок — различить их было нельзя.
                case SkinShape.Lumpy:
                    return 0.82f + 0.16f * Mathf.Sin(angle * 3f + 0.7f);

                // Звезда: четыре луча с узкой талией между ними.
                //
                // Степень больше единицы, а не меньше: при 0.45 лучи раздувались и
                // получался четырёхлистник, а не звезда.
                case SkinShape.Star:
                {
                    float arms = Mathf.Abs(Mathf.Cos(angle * 2f));
                    return 0.40f + 0.60f * Mathf.Pow(arms, 3f);
                }

                // Блок: квадрат со срезанными углами.
                //
                // Радиус квадрата в направлении θ — это сторона, делённая на
                // больший из |cos| и |sin|; фаска срезает углы по сумме. Прежняя
                // версия сравнивала то же самое с множителем и давала почти
                // окружность, неотличимую от огранки.
                case SkinShape.Blocky:
                {
                    float side = 0.70f / Mathf.Max(0.0001f, Mathf.Max(ax, ay));
                    float chamfer = 1.26f / Mathf.Max(0.0001f, ax + ay);
                    return Mathf.Min(1f, Mathf.Min(side, chamfer));
                }

                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Оболочка стража по той же форме. Середина прозрачнее, чем у пузырька:
        /// под стражем лежит начинка, и закрывать её нельзя — ровно на этом все
        /// скины однажды и стали одинаковыми.
        /// </summary>
        public static Sprite ShapeShellGuardian(SkinShape shape)
        {
            return Build("gshell_" + (int)shape, p =>
            {
                float d = p.magnitude;
                float edge = ShapeEdge(shape, p);
                if (d > edge) return Color.clear;

                float t = edge > 0.001f ? d / edge : 1f;

                Color c;
                if (t < 0.74f)
                {
                    c = new Color(0.86f, 1f, 0.98f, 0.1f + (t / 0.74f) * 0.08f);
                }
                else
                {
                    // Ободок яркий: по нему игрок читает радиус защиты, и он обязан
                    // быть виден в любой момент боя.
                    float k = Mathf.InverseLerp(0.74f, 0.95f, Mathf.Min(t, 0.95f));
                    c = new Color(0.72f, 1f, 0.94f, 0.2f + k * 0.65f);
                }

                Vector2 h = new Vector2((p.x + 0.3f) / 0.2f, (p.y - 0.34f) / 0.12f);
                if (h.sqrMagnitude < 1f && t < 0.9f) c = Blend(c, new Color(1f, 1f, 1f, 0.7f));

                return c;
            });
        }

        /// <summary>Прогреть силуэты: их девять, и все видны в магазине сразу.</summary>
        public static void PrewarmShapes()
        {
            for (int i = 0; i <= (int)SkinShape.Blocky; i++)
            {
                ShapeShell((SkinShape)i);
                ShapeShellGuardian((SkinShape)i);
            }
        }
    }
}
