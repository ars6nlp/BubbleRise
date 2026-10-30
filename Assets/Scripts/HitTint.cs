using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Отклик тела босса на попадание: плавный подмес цвета вместо подмены
    /// спрайта светлой копией.
    ///
    /// **Почему не подмена.** Прежде попадание ставило светлый, местами чисто
    /// белый спрайт на 0.22–0.25 с и так же возвращало обычный: два фронта по
    /// 0 мс. Тело ската на трети экрана светлело за кадр, у спрута и краба серия
    /// ударов давала 4–6 перепадов в секунду на четверти экрана. [ТЕСТ]
    ///
    /// **Как теперь.** Над каждой отмеченной частью тела лежит её силуэт — маска
    /// из <see cref="SpriteFactory.MaskOf"/>, — окрашенный в <see cref="Tone"/>.
    /// Его прозрачность идёт по импульсу: нарастание <see cref="Rise"/>, спад
    /// <see cref="Fall"/>, не выше <see cref="MaxMix"/>. Новый импульс не раньше
    /// <see cref="MinGap"/> после начала прошлого: сколько бы попаданий ни
    /// пришлось на это время, в любой секунде не больше двух импульсов.
    ///
    /// **«Без вспышек».** Силуэт уходит за тело и чуть раздаётся — получается
    /// контур по кромке, а яркость самого тела не меняется вовсе.
    ///
    /// Части с общим порядком отрисовки, которые сильно перекрывают друг друга
    /// (звенья щупалец, суставы клешней), не отмечаются: два силуэта в месте
    /// перекрытия сложились бы почти вдвое.
    /// </summary>
    public sealed class HitTint
    {
        /// <summary>Нарастание подмеса, с. Не резче 0.06.</summary>
        public const float Rise = 0.08f;
        /// <summary>Спад подмеса, с.</summary>
        public const float Fall = 0.28f;
        /// <summary>
        /// Не чаще одного импульса за это время, с. 0.5, а не 0.45: при 0.45 в
        /// одну секунду влезали три импульса, то есть шесть перепадов.
        /// </summary>
        public const float MinGap = 0.5f;
        /// <summary>
        /// Доля цвета в подмесе на пике.
        ///
        /// Проект в линейном цветовом пространстве: смешение идёт по линейной
        /// яркости, и светлый цвет поверх тёмного тела светлит его сильнее, чем
        /// говорят проценты. Замер: 25 % чистого Gold поднимали яркость тёмного
        /// тела на 0.13–0.15, у ската — на 26 % экрана. [ТЕСТ] Приглушённое
        /// золото (<see cref="ToneLevel"/>) и 14 % дают около +0.04: ниже
        /// чувствительного порога 0.05, а сдвиг в тёплое на тёмном теле виден.
        /// </summary>
        public const float MaxMix = 0.14f;
        /// <summary>Во сколько раз золото подмеса темнее Palette.Gold.</summary>
        public const float ToneLevel = 0.75f;
        /// <summary>
        /// Плотность контура в «Без вспышек»: он лежит только по кромке. 0.4, а
        /// не 0.6: при 0.6 кромка краба меняла яркость плиток на 5.8 % экрана
        /// сильнее чувствительного порога 0.05. [ТЕСТ]
        /// </summary>
        public const float OutlineAlpha = 0.4f;
        /// <summary>Толщина контура в юнитах мира.</summary>
        public const float OutlineWidth = 0.05f;

        /// <summary>
        /// Цвет подмеса. Gold значит «удар по боссу» (Coral оставлен опасности);
        /// приглушён до ToneLevel, сумма каналов не выше 2.3 — белого в нём нет.
        /// </summary>
        public static Color Tone
        {
            get
            {
                Color g = Palette.Gold;
                return Palette.SafeLight(new Color(g.r * ToneLevel, g.g * ToneLevel, g.b * ToneLevel, 1f));
            }
        }

        readonly List<SpriteRenderer> _bodies = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> _overlays = new List<SpriteRenderer>();
        float _age = -1f;
        float _sinceStart = MinGap;

        /// <summary>Текущая форма импульса, 0…1.</summary>
        public float Level { get; private set; }

        /// <summary>Сила подмеса прямо сейчас: доля цвета поверх тела.</summary>
        public float Mix => Level * MaxMix * Visuals.BossFlash;

        /// <summary>Отметить часть тела. Силуэт заводится один раз и потом только перекрашивается.</summary>
        public void Track(SpriteRenderer body)
        {
            if (body == null || _bodies.Contains(body)) return;

            var go = new GameObject("HitTint");
            go.transform.SetParent(body.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = SpriteFactory.Unlit;
            sr.enabled = false;

            _bodies.Add(body);
            _overlays.Add(sr);
        }

        /// <summary>
        /// Попадание. Ложь, если прошлый импульс начался меньше MinGap назад:
        /// урон засчитан, частицы и звук уже идут, а лишний перепад яркости не нужен.
        /// </summary>
        public bool Hit()
        {
            if (_sinceStart < MinGap) return false;
            _age = 0f;
            _sinceStart = 0f;
            return true;
        }

        /// <summary>Погасить сразу — на появлении босса.</summary>
        public void Reset()
        {
            _age = -1f;
            _sinceStart = MinGap;
            Level = 0f;
            Apply();
        }

        public void Tick(float dt)
        {
            _sinceStart += dt;
            if (_age >= 0f)
            {
                _age += dt;
                Level = Pulse(_age);
                if (_age >= Rise + Fall) _age = -1f;
            }
            else Level = 0f;

            Apply();
        }

        /// <summary>Форма импульса: SmoothStep вверх за Rise, вниз за Fall. Фронтов в 0 мс нет.</summary>
        public static float Pulse(float t)
        {
            if (t <= 0f || t >= Rise + Fall) return 0f;
            return t < Rise
                ? Mathf.SmoothStep(0f, 1f, t / Rise)
                : 1f - Mathf.SmoothStep(0f, 1f, (t - Rise) / Fall);
        }

        void Apply()
        {
            bool outline = Visuals.NoFlashes;
            float alpha = Level * (outline ? OutlineAlpha * Visuals.BossFlash : MaxMix * Visuals.BossFlash);
            Color tone = Tone;

            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                var over = _overlays[i];
                if (over == null) continue;

                Sprite mask = alpha > 0.002f && body != null && body.enabled
                    ? SpriteFactory.MaskOf(body.sprite) : null;
                if (mask == null)
                {
                    if (over.enabled) over.enabled = false;
                    continue;
                }

                over.sprite = mask;
                over.flipX = body.flipX;
                over.flipY = body.flipY;
                over.sortingLayerID = body.sortingLayerID;

                if (outline)
                {
                    // За телом и чуть шире его: видна только кромка. Дальше по
                    // глубине — чтобы соседнее звено того же порядка его закрыло.
                    over.sortingOrder = body.sortingOrder - 1;
                    Vector3 size = mask.bounds.size;
                    Vector3 world = body.transform.lossyScale;
                    float wx = Mathf.Max(0.01f, size.x * Mathf.Abs(world.x));
                    float wy = Mathf.Max(0.01f, size.y * Mathf.Abs(world.y));
                    over.transform.localScale = new Vector3(1f + 2f * OutlineWidth / wx,
                                                            1f + 2f * OutlineWidth / wy, 1f);
                    over.transform.localPosition = new Vector3(0f, 0f, 0.01f);
                }
                else
                {
                    // Тот же порядок, чуть ближе к камере: ложится сразу на своё
                    // тело и под части с порядком выше.
                    over.sortingOrder = body.sortingOrder;
                    over.transform.localScale = Vector3.one;
                    over.transform.localPosition = new Vector3(0f, 0f, -0.01f);
                }

                // Контур лежит ЗА телом. Сквозь полупрозрачное тело (скат в
                // фоне) он просвечивал бы целиком, то есть был бы уже не контуром,
                // а подмесом по всей площади. Поэтому в контуре — куб альфы тела.
                float bodyA = body.color.a;
                if (outline) bodyA = bodyA * bodyA * bodyA;
                over.color = tone.WithAlpha(alpha * bodyA);
                if (!over.enabled) over.enabled = true;
            }
        }
    }
}
