using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Щупальца грозовой медузы: два яруса, один цикл обновления.
    ///
    /// **Ярус первый — четыре толстых ротовых рукава.** Каждый собран из звеньев
    /// и идёт волной с запаздыванием: звено повторяет движение предыдущего не
    /// сразу, а через <see cref="Lag"/>. Отсюда инерция — кончик догоняет
    /// основание, а не дёргается вместе с ним.
    ///
    /// **Ярус второй — полосы тонких нитей.** Нитей в текстуре восемь на полосу,
    /// полос шесть: около полусотни нитей за шесть объектов. Отдельными
    /// объектами это стоило бы сотен спрайтов — см. расчёт в docs. [РАСЧЁТ]
    ///
    /// **Почему один компонент на всё.** Требование задания и просто здравый
    /// смысл: двадцать шесть независимых MonoBehaviour с собственным Update
    /// стоили бы двадцати шести вызовов движка в кадр вместо одного, а
    /// согласовать между ними фазу волны было бы нечем.
    ///
    /// **Медленно и тяжело.** Период волны 2.4 с, запаздывание между звеньями
    /// четверть периода. Резких рывков нет ни одного — это правило для глаз, а
    /// не стилистика.
    /// </summary>
    public class JellyTentacles : MonoBehaviour
    {
        /// <summary>Толстых рукавов.</summary>
        public const int Arms = 4;
        /// <summary>Звеньев в рукаве.</summary>
        public const int Links = 5;
        /// <summary>Полос тонких нитей.</summary>
        public const int Fringes = 6;

        /// <summary>Период волны, секунд. Медленнее — уже стоячие верёвки.</summary>
        const float Wave = 2.4f;
        /// <summary>Запаздывание звена за предыдущим, в долях периода.</summary>
        const float Lag = 0.25f;

        Transform[] _arm;          // Arms * Links
        SpriteRenderer[] _armSr;
        Transform[] _fringe;
        SpriteRenderer[] _fringeSr;

        float _phase;
        float _pull = 1f;          // подтянутость: 1 — свободно, меньше — к куполу
        float _alpha = 1f;
        float _tremble;            // дрожь нитей на подготовке
        float _reach;              // рывок вслед снаряду
        float _aim;                // куда смотрит глаз, градусы

        /// <summary>Сколько объектов занимают щупальца. Нужно отчёту по нагрузке.</summary>
        public static int SpriteCount => Arms * Links + Fringes;

        public void Build(Transform parent)
        {
            _arm = new Transform[Arms * Links];
            _armSr = new SpriteRenderer[Arms * Links];
            _fringe = new Transform[Fringes];
            _fringeSr = new SpriteRenderer[Fringes];

            var armSprite = SpriteFactory.JellyArm();

            for (int a = 0; a < Arms; a++)
            {
                for (int k = 0; k < Links; k++)
                {
                    int i = a * Links + k;
                    var go = new GameObject("Arm" + a + "_" + k);
                    go.transform.SetParent(parent, false);

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = armSprite;
                    sr.sharedMaterial = SpriteFactory.Unlit;

                    // Рукава под куполом: купол обязан перекрывать их основания.
                    sr.sortingOrder = SortingOrders.JellyArm;

                    _arm[i] = go.transform;
                    _armSr[i] = sr;
                }
            }

            for (int f = 0; f < Fringes; f++)
            {
                var go = new GameObject("Fringe" + f);
                go.transform.SetParent(parent, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.JellyFringe(f % 2);
                sr.sharedMaterial = SpriteFactory.Unlit;
                sr.sortingOrder = SortingOrders.JellyFringe;

                _fringe[f] = go.transform;
                _fringeSr[f] = sr;
            }
        }

        /// <summary>Подтянуть щупальца к куполу перед атакой: 1 — свободно, 0.7 — поджато.</summary>
        public void SetPull(float pull) => _pull = Mathf.Clamp(pull, 0.5f, 1.2f);

        /// <summary>
        /// Дрожь тонких нитей на подготовке, 0…1.
        ///
        /// Это дрожание ПОЛОЖЕНИЯ, а не яркости: правило «не больше трёх резких
        /// изменений яркости в секунду» оно не задевает, потому что цвет нитей
        /// при этом не меняется вовсе.
        /// </summary>
        public void SetTremble(float tremble) => _tremble = Mathf.Clamp01(tremble);

        /// <summary>
        /// Рывок вслед снаряду: щупальца тянутся в сторону выстрела и
        /// расслабляются. <paramref name="aim"/> — градусы, куда смотрит глаз.
        /// </summary>
        public void SetReach(float reach, float aim)
        {
            _reach = Mathf.Clamp01(reach);
            _aim = aim;
        }

        /// <summary>Общая прозрачность — ею медуза гаснет при смерти.</summary>
        public void SetAlpha(float alpha) => _alpha = Mathf.Clamp01(alpha);

        public void SetVisible(bool on)
        {
            if (_arm == null) return;
            for (int i = 0; i < _armSr.Length; i++) _armSr[i].enabled = on;
            for (int i = 0; i < _fringeSr.Length; i++) _fringeSr[i].enabled = on;
        }

        public void ResetState()
        {
            _phase = 0f;
            _pull = 1f;
            _alpha = 1f;
            _tremble = 0f;
            _reach = 0f;
            _aim = 0f;
        }

        /// <summary>
        /// Один проход по всем звеньям. Координаты локальные, в долях диаметра
        /// купола: родитель уже отмасштабирован, и второй раз размер не
        /// применяется.
        /// </summary>
        public void Tick(float dt)
        {
            if (_arm == null) return;

            _phase += dt / Wave;

            float w = Mathf.PI * 2f;

            for (int a = 0; a < Arms; a++)
            {
                // Рукава висят под куполом веером.
                float baseX = Mathf.Lerp(-0.42f, 0.42f, Arms <= 1 ? 0.5f : a / (float)(Arms - 1));
                float sway = 0.5f + a * 0.37f;     // своя фаза у каждого рукава

                // Рукава расходятся веером: строго параллельные читаются как
                // решётка, а не как щупальца.
                float splay = Mathf.Lerp(-16f, 16f, Arms <= 1 ? 0.5f : a / (float)(Arms - 1));

                Vector2 pos = new Vector2(baseX, -0.12f);

                // Ноль — это «вниз»: dir ниже построен как (sin, -cos), и при
                // 180 градусах он смотрел ВВЕРХ — рукава торчали из макушки
                // купола. [ТЕСТ]
                // На рывке веер разворачивается в сторону, куда смотрит глаз:
                // щупальца тянутся вслед снаряду, а не висят как висели.
                float toAim = Mathf.DeltaAngle(-90f, _aim);
                float angle = Mathf.Lerp(splay, splay * 0.4f + toAim * 0.45f, _reach);

                for (int k = 0; k < Links; k++)
                {
                    int i = a * Links + k;

                    // Запаздывание: чем дальше звено, тем позже оно повторяет волну.
                    float t = (_phase - k * Lag) * w + sway;

                    // Изгиб растёт к кончику: основание тяжёлое, конец
                    // свободный. Отсюда и ощущение инерции.
                    float bend = Mathf.Sin(t) * (9f + k * 6f);

                    angle += bend;

                    float len = 0.2f * _pull * Mathf.Lerp(1f, 0.74f, k / (float)Links);
                    var dir = new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad),
                                          -Mathf.Cos(angle * Mathf.Deg2Rad));

                    Vector2 mid = pos + dir * (len * 0.5f);
                    pos += dir * len;

                    var tr = _arm[i];
                    tr.localPosition = new Vector3(mid.x, mid.y, 0f);
                    // Спрайт звена нарисован «вверх ногами» относительно хода
                    // рукава: его верх — это основание, поэтому поворот без
                    // доворота на 180.
                    tr.localRotation = Quaternion.Euler(0f, 0f, -angle);
                    // Длина спрайта больше шага: звенья перекрываются, и между
                    // ними не видно стыков. Без запаса рукав разваливался на
                    // отдельные кубики. [ТЕСТ]
                    tr.localScale = new Vector3(0.15f * Mathf.Lerp(1f, 0.55f, k / (float)Links),
                                                len * 1.35f, 1f);

                    var c = Color.white;
                    c.a = _alpha * Mathf.Lerp(1f, 0.82f, k / (float)Links);
                    _armSr[i].color = c;
                }
            }

            for (int f = 0; f < Fringes; f++)
            {
                float u = Fringes <= 1 ? 0.5f : f / (float)(Fringes - 1);
                float baseX = Mathf.Lerp(-0.72f, 0.72f, u);

                // Полосы качаются с запаздыванием друг за другом — так видно
                // волну, идущую вдоль края купола.
                float t = (_phase - u * Lag * 2f) * w;
                float tilt = Mathf.Sin(t + f) * 7f;

                // Дрожь: мелкая и частая, но только по углу. На подготовке она
                // и выдаёт, что сейчас что-то будет.
                if (_tremble > 0.01f)
                    tilt += Mathf.Sin(_phase * 95f + f * 2.3f) * 3.5f * _tremble;
                float drop = 0.34f * _pull * Mathf.Lerp(1f, 1.18f, _reach);

                var tr = _fringe[f];
                tr.localPosition = new Vector3(baseX + Mathf.Sin(t * 0.5f + f) * 0.03f,
                                               -0.14f - drop, 0f);
                tr.localRotation = Quaternion.Euler(0f, 0f, tilt);
                tr.localScale = new Vector3(0.3f, drop * 2f, 1f);

                var c = Color.white;
                c.a = _alpha * 0.9f;
                _fringeSr[f].color = c;
            }
        }
    }
}
