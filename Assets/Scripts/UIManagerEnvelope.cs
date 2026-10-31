using UnityEngine;
using UnityEngine.UI;

namespace BubbleRise
{
    /// <summary>
    /// Оболочка столкновения в витрине.
    ///
    /// **Что общего с боем.** Всё, кроме единицы длины: траектории, слои, число
    /// частиц, толщина связи, растекание по мишени и ступени яркости берутся из
    /// <see cref="EnvelopeShape"/>, пределы — из <see cref="CollisionEnvelope"/>.
    /// Расходиться им нечем: формулы здесь не повторяются ни разу.
    ///
    /// **Почему отдельный пул.** Тот же случай, что и в бою: точкам показа
    /// эффектов задают скорость и отпускают, они летят по инерции и всплывают.
    /// Оболочка так не умеет — её частицы стоят там, где велела траектория.
    ///
    /// **Где её показывают.** В двух местах, и это не дублирование:
    ///
    /// * **в панели выбранного** — мелко (<see cref="ShopScale"/>), как подпись
    ///   к скину: видно, что эффект есть и какого он цвета;
    /// * **в большом показе** — почти в натуральную величину
    ///   (<see cref="DemoScale"/>), с тестовым препятствием. Сюда ведёт кнопка
    ///   «Показать эффекты», и ради этого экрана всё и затевалось: в панели
    ///   высотой 248 точек эффект радиусом в три с половиной диаметра показать
    ///   нельзя никак.
    /// </summary>
    public partial class UIManager
    {
        /// <summary>
        /// Доля натуральной величины в маленькой панели выбранного. Панель 248
        /// точек, шарик 112; натуральная оболочка — 760 точек поперёк.
        /// </summary>
        const float ShopScale = 0.42f;

        /// <summary>
        /// Доля натуральной величины в большом показе.
        ///
        /// Число посчитано, а не подобрано на глаз. Холст витрины — 500 точек
        /// шириной, панель показа занимает его целиком, шарик стоит посередине,
        /// значит до ближнего края 250 точек. Самая крупная оболочка имеет
        /// радиус 3.4 D, а искры уходят ещё на 8 % дальше — 3.67 D. При шарике
        /// в <see cref="DemoBallSize"/> точек (то есть 1 D = 90):
        ///
        ///     3.67 · 0.74 · 90 = 244.6 точки &lt; 250 — влезает с запасом.
        ///
        /// Проверено арифметикой, а не глазами: при 0.72 и шарике 96 вылет
        /// составлял 253.8 точки, и края обрезало.
        ///
        /// Меньше шарик делать нельзя — скин перестанет быть различим, а показ
        /// затеян ради него же.
        /// </summary>
        const float DemoScale = 0.74f;

        /// <summary>Диаметр шарика в большом показе, в точках холста.</summary>
        const float DemoBallSize = 90f;

        /// <summary>
        /// Один показ оболочки: корень, пул точек и своё время жизни.
        ///
        /// Устроен так же, как боевой: три кольца и россыпь. Кольца — отдельные
        /// картинки, а не полсотни точек, ровно по той же причине, что и в бою.
        /// </summary>
        class EnvelopeDemo
        {
            /// <summary>
            /// Точек в пуле россыпи и связи. В бою потолок 136; здесь столько же
            /// для большого показа и вчетверо меньше для маленькой панели — там
            /// эффект в два с половиной раза мельче, и плотность ни к чему.
            /// </summary>
            public const int PoolBig = 136;
            public const int PoolSmall = 48;

            RectTransform _root;
            Image[] _dots;
            Image _shadowRing, _coreRing, _accentRing;

            EnvelopeDef _def;
            bool _has;
            bool _loop;

            float _life;
            float _age;
            float _arcStep;

            /// <summary>Точек холста на один диаметр стража.</summary>
            float _unit;
            Vector2 _targetD;
            float _targetRadiusD = 0.5f;

            public bool Active => _life > 0f;

            public void Build(RectTransform parent, Vector2 center, float unit, int pool)
            {
                _unit = unit;

                // Корень не внутри шарика: шарик дышит по закону скина, и
                // оболочка, повешенная на него, дышала бы вместе с ним. В бою
                // корень отвязан от стража по той же причине.
                _root = NewRect("Envelope", parent);
                _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
                _root.pivot = new Vector2(0.5f, 0.5f);
                _root.anchoredPosition = center;
                _root.sizeDelta = Vector2.zero;

                _shadowRing = MakeRing(SpriteFactory.EnvelopeRing());
                _coreRing = MakeRing(SpriteFactory.EnvelopeRing());
                _accentRing = MakeRing(SpriteFactory.EnvelopeSpeck());

                _dots = new Image[pool];
                for (int i = 0; i < pool; i++)
                {
                    var rt = NewRect("E" + i, _root);
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);

                    var img = rt.gameObject.AddComponent<Image>();
                    img.sprite = SpriteFactory.SoftDot();
                    img.raycastTarget = false;
                    img.enabled = false;
                    _dots[i] = img;
                }

                _root.gameObject.SetActive(false);
            }

            Image MakeRing(Sprite sprite)
            {
                var rt = NewRect("Ring", _root);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                var img = rt.gameObject.AddComponent<Image>();
                img.sprite = sprite;
                img.raycastTarget = false;
                img.enabled = false;
                return img;
            }

            /// <param name="targetD">Мишень в диаметрах от центра шарика.</param>
            /// <param name="loop">Повторять без конца — для большого показа.</param>
            public void Play(EnvelopeDef def, Vector2 targetD, float targetRadiusD, bool loop = false)
            {
                if (_root == null) return;

                _def = def;
                _has = true;
                _loop = loop;
                _life = def.Duration;
                _age = 0f;
                _targetD = targetD;
                _targetRadiusD = targetRadiusD;
                _root.gameObject.SetActive(true);
            }

            public void Stop()
            {
                _life = 0f;
                _loop = false;
                if (_root == null) return;
                for (int i = 0; i < _dots.Length; i++) _dots[i].enabled = false;
                _shadowRing.enabled = _coreRing.enabled = _accentRing.enabled = false;
                _root.gameObject.SetActive(false);
            }

            public void Tick(float dt)
            {
                if (_root == null || !_has) return;

                if (_life <= 0f)
                {
                    // Большой показ идёт по кругу: игрок разглядывает эффект, а
                    // не ловит его. Пауза в треть секунды между повторами —
                    // чтобы было видно, где он начинается и где кончается.
                    if (_loop)
                    {
                        _life = -_life > 0.3f ? _def.Duration : _life - dt;
                        if (_life > 0f) _age = 0f;
                        else return;
                    }
                    else
                    {
                        if (_root.gameObject.activeSelf) Stop();
                        return;
                    }
                }

                _life -= dt;
                _age += dt;
                _arcStep += dt * CollisionEnvelope.ArcHz * Visuals.ArcRate;

                float fade = Mathf.Clamp01(Mathf.Min(
                    _age / CollisionEnvelope.FadeIn,
                    _life > CollisionEnvelope.FadeOut ? 1f : _life / CollisionEnvelope.FadeOut));

                PlaceRings(fade);

                var split = EnvelopeShape.Layers(_def, 1f);
                int link = EnvelopeShape.LinkCount(_def);

                int room = _dots.Length - link;
                int want = split.Shape + split.Spark;
                if (want > room && want > 0)
                {
                    float k = room / (float)want;
                    split.Spark = Mathf.Max(2, Mathf.FloorToInt(split.Spark * k));
                    split.Shape = Mathf.Max(4, room - split.Spark);
                }

                int endShape = split.Shape;
                int endSpark = endShape + split.Spark;
                int endLink = endSpark + link;

                for (int i = 0; i < _dots.Length; i++)
                {
                    if (i >= endLink) { _dots[i].enabled = false; continue; }

                    float seed = (i * 0.6180339f) % 1f;
                    var img = _dots[i];
                    img.enabled = true;

                    Vector2 at;
                    float size;
                    Color color;
                    float alpha;

                    if (i >= endSpark)
                    {
                        var dot = EnvelopeShape.LinkAt(_def, i - endSpark, Vector2.zero,
                                                       _targetD, _arcStep, _targetRadiusD);
                        at = dot.Position;
                        size = dot.Size;
                        color = dot.OnTarget ? _def.Accent : _def.Hot;
                        alpha = CollisionEnvelope.LinkAlpha;
                    }
                    else
                    {
                        var layer = i < endShape ? EnvelopeLayer.Shape : EnvelopeLayer.Spark;
                        int idx = i < endShape ? i : i - endShape;
                        int cnt = i < endShape ? split.Shape : split.Spark;
                        var s = EnvelopeShape.Sample(_def, layer, idx, cnt, _age, seed, _arcStep);

                        at = s.Position;
                        size = s.Size;
                        color = s.Color;
                        alpha = Mathf.Clamp01(s.Alpha) * CeilingOf(layer);
                    }

                    var rt = img.rectTransform;
                    rt.anchoredPosition = at * _unit;
                    rt.sizeDelta = Vector2.one * Mathf.Max(3f, size * _unit);
                    img.color = color.WithAlpha(
                        Mathf.Clamp01(alpha * fade * Visuals.EnvelopeAlpha));
                }
            }

            void PlaceRings(float fade)
            {
                var r = EnvelopeShape.Rings(_def, _age);
                float a = fade * Visuals.EnvelopeAlpha;

                SetRing(_shadowRing, r.ShadowScale, _def.Edge,
                        r.ShadowAlpha * a * CollisionEnvelope.ShadowAlpha, _age * 0.35f);
                SetRing(_coreRing, r.CoreScale, _def.Hot,
                        r.CoreAlpha * a * CollisionEnvelope.CoreAlpha, _age * 0.5f);
                SetRing(_accentRing, r.CoreScale * 1.02f, _def.Accent,
                        r.AccentAlpha * a * CollisionEnvelope.CoreAlpha, -_age * 0.8f);
            }

            void SetRing(Image img, float scale, Color tint, float alpha, float spin)
            {
                if (img == null) return;

                float a = Mathf.Clamp01(alpha);
                if (a <= 0.01f) { if (img.enabled) img.enabled = false; return; }

                if (!img.enabled) img.enabled = true;

                // Спрайт кольца занимает квадрат со стороной 2·масштаб в D:
                // полоса стоит на 0.72 половины, и в бою то же самое делает
                // localScale корня.
                var rt = img.rectTransform;
                rt.sizeDelta = Vector2.one * (scale * _unit);
                rt.localRotation = Quaternion.Euler(0f, 0f, spin * Mathf.Rad2Deg * 0.25f);
                img.color = tint.WithAlpha(a);
            }

            static float CeilingOf(EnvelopeLayer layer)
                => layer == EnvelopeLayer.Spark
                    ? CollisionEnvelope.SparkAlpha
                    : CollisionEnvelope.ShapeAlpha;
        }

        EnvelopeDemo _previewEnvelope;
        EnvelopeDemo _compareEnvelope;
        EnvelopeDemo _bigEnvelope;

        EnvelopeDef _previewEnvDef;
        bool _hasPreviewEnv;
        EnvelopeDef _compareEnvDef;
        bool _hasCompareEnv;

        /// <summary>
        /// Два корня оболочки в панели выбранного: под выбранным скином и под
        /// надетым.
        ///
        /// Координаты — центры шариков, а не их якоря: <see cref="Anchor"/> ставит
        /// pivot по верхнему краю, и якорь шарика лежит на его макушке.
        ///
        /// Панели здесь же выдаётся обрезка по краю: оболочка больше неё, и без
        /// обрезки частицы вылезали бы на карточки списка.
        /// </summary>
        void BuildShopEnvelopes(RectTransform panel)
        {
            if (panel.GetComponent<RectMask2D>() == null)
                panel.gameObject.AddComponent<RectMask2D>();

            _previewEnvelope = new EnvelopeDemo();
            _previewEnvelope.Build(panel,
                new Vector2(ShopLayout.PreviewLeftX,
                            ShopLayout.PreviewBallY - ShopLayout.PreviewBallSize * 0.5f),
                ShopLayout.PreviewBallSize * ShopScale, EnvelopeDemo.PoolSmall);

            _compareEnvelope = new EnvelopeDemo();
            _compareEnvelope.Build(panel,
                new Vector2(ShopLayout.PreviewLeftX,
                            ShopLayout.PreviewCompareY - ShopLayout.PreviewCompareSize * 0.5f),
                ShopLayout.PreviewCompareSize * ShopScale, EnvelopeDemo.PoolSmall);
        }

        // ------------------------------------------------- большой показ

        GameObject _bigDemo;
        Image _bigBallShell, _bigBallCore, _bigTarget;
        RectTransform _bigBall;
        Text _bigTitle, _bigSwitchLabel, _bigHint;
        Button _bigSwitch;
        bool _bigShowsWorn;
        float _bigPhase;
        SkinLook _bigLook;

        /// <summary>
        /// Большой показ эффекта: половина экрана, крупный шарик и тестовое
        /// препятствие.
        ///
        /// **Зачем он нужен.** Оболочка радиусом 3.4 D — это 760 точек поперёк
        /// при шарике в 112. В панели выбранного (248 точек высотой) её можно
        /// показать только вчетверо мельче, и игрок покупал скин, не увидев
        /// толком, за что платит. Здесь — 0.72 натуральной величины, с запасом
        /// до краёв и без обрезки.
        ///
        /// **Почему поверх витрины, а не отдельным экраном.** Экранов в игре
        /// шесть, и каждый завязан на состояние игры, паузу и баннеры. Показ
        /// эффекта — это не состояние, а заглянуть и вернуться; отдельный экран
        /// потребовал бы вплести его в тот автомат ради одной кнопки.
        ///
        /// Сетку карточек он не трогает: лежит поверх, закрывается кнопкой и
        /// щелчком по затемнению.
        /// </summary>
        void BuildBigDemo(GameObject shopScreen)
        {
            _bigDemo = new GameObject("BigDemo", typeof(RectTransform));
            var root = (RectTransform)_bigDemo.transform;
            root.SetParent(shopScreen.transform, false);
            Stretch(root);

            // Затемнение: по нему же и закрывается. Плотное — за ним список
            // карточек, и он не должен спорить с эффектом за внимание.
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = new Color(Palette.Deep.r, Palette.Deep.g, Palette.Deep.b, 0.93f);
            var dimButton = _bigDemo.AddComponent<Button>();
            dimButton.targetGraphic = dim;
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(() => { ButtonClicked?.Invoke(); CloseBigDemo(); });

            var panel = NewRect("Panel", root);
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(500f, 560f);

            var bg = panel.gameObject.AddComponent<Image>();
            bg.sprite = SpriteFactory.RoundedRect("panel", 0.3f);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(Palette.Deep2.r, Palette.Deep2.g, Palette.Deep2.b, 0.96f);
            bg.raycastTarget = true;

            // Обрезка по панели: даже на 0.72 самая крупная оболочка задевает
            // углы, и без неё искры ушли бы на затемнение.
            panel.gameObject.AddComponent<RectMask2D>();

            _bigTitle = NewText("Title", panel, "", 22, Palette.Aqua, TextAnchor.MiddleCenter);
            Anchor(_bigTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                   new Vector2(460f, 30f));
            _bigTitle.horizontalOverflow = HorizontalWrapMode.Overflow;

            _bigHint = NewText("Hint", panel, "", 14,
                               Palette.Foam.WithAlpha(0.55f), TextAnchor.MiddleCenter);
            Anchor(_bigHint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -44f),
                   new Vector2(460f, 20f));
            _bigHint.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Центр сцены: шарик слева от середины, мишень справа от него —
            // те же доли, что и в бою.
            var stage = NewRect("Stage", panel);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.pivot = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = new Vector2(0f, 16f);
            stage.sizeDelta = Vector2.zero;

            _bigTarget = NewRect("Target", stage).gameObject.AddComponent<Image>();
            _bigTarget.sprite = SpriteFactory.Rock();
            _bigTarget.preserveAspect = true;
            _bigTarget.raycastTarget = false;
            var trt = _bigTarget.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.anchoredPosition = new Vector2(DemoBallSize * 1.45f, 0f);
            trt.sizeDelta = Vector2.one * (DemoBallSize * 0.9f);

            _bigBall = NewRect("Ball", stage);
            _bigBall.anchorMin = _bigBall.anchorMax = new Vector2(0.5f, 0.5f);
            _bigBall.pivot = new Vector2(0.5f, 0.5f);
            _bigBall.anchoredPosition = Vector2.zero;
            _bigBall.sizeDelta = Vector2.one * DemoBallSize;

            var coreRect = NewRect("Core", _bigBall);
            Stretch(coreRect);
            _bigBallCore = coreRect.gameObject.AddComponent<Image>();
            _bigBallCore.raycastTarget = false;
            _bigBallCore.preserveAspect = true;

            var shellRect = NewRect("Shell", _bigBall);
            Stretch(shellRect);
            _bigBallShell = shellRect.gameObject.AddComponent<Image>();
            _bigBallShell.raycastTarget = false;
            _bigBallShell.preserveAspect = true;

            // Оболочка строится последней: она обязана лежать поверх шарика,
            // как и в бою, где её слои идут выше стража.
            _bigEnvelope = new EnvelopeDemo();
            _bigEnvelope.Build(panel, StageCenterInPanel(), DemoBallSize * DemoScale,
                               EnvelopeDemo.PoolBig);

            // Переключатель «выбранный / надетый». Показывать два шарика рядом
            // здесь нельзя: каждому нужно по 236 точек от центра, а панель
            // шириной 500. Поэтому по очереди — зато каждый в полную величину.
            var sw = NewButton("Switch", panel, "", Palette.Deep2, Palette.Foam);
            Anchor(sw.rt, new Vector2(0.5f, 0f), new Vector2(0f, 112f), new Vector2(300f, 48f));
            sw.text.fontSize = 17;
            sw.button.onClick.AddListener(() =>
            {
                ButtonClicked?.Invoke();
                _bigShowsWorn = !_bigShowsWorn;
                RefreshBigDemo();
            });
            _bigSwitch = sw.button;
            _bigSwitchLabel = sw.text;

            var close = NewButton("Close", panel, Loc.Get("fx.close"), Palette.Aqua, Palette.Deep);
            Anchor(close.rt, new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(300f, 52f));
            close.text.fontSize = 20;
            close.button.onClick.AddListener(() => { ButtonClicked?.Invoke(); CloseBigDemo(); });

            _bigDemo.SetActive(false);
        }

        /// <summary>
        /// Центр сцены в координатах панели.
        ///
        /// Панель 500 на 560 с pivot в середине, сцена поднята на 16 точек
        /// (место под кнопки снизу). Корень оболочки живёт с якорем (0, 1),
        /// поэтому координата считается от левого верхнего угла панели.
        ///
        /// Запас до краёв: 250 влево и вправо, 264 вверх, 296 вниз — против
        /// 244.6 точки вылета самой крупной оболочки.
        /// </summary>
        static Vector2 StageCenterInPanel() => new Vector2(250f, -(280f - 16f));

        void OpenBigDemo()
        {
            if (_bigDemo == null) return;

            _bigShowsWorn = false;
            _bigDemo.SetActive(true);
            _bigPhase = 0f;
            RefreshBigDemo();
        }

        void CloseBigDemo()
        {
            if (_bigDemo == null) return;
            _bigEnvelope.Stop();
            _bigDemo.SetActive(false);
        }

        /// <summary>Набрать показ заново: шарик, мишень, подпись и оболочка.</summary>
        void RefreshBigDemo()
        {
            if (_bigDemo == null || !_bigDemo.activeSelf) return;

            var skins = _shopTab == 0 ? SkinStore.Bubble : SkinStore.Guardian;
            int shown = _shopTab == 0 ? SkinStore.SelectedBubble : SkinStore.SelectedGuardian;
            var skin = skins[shown];

            _bigLook = SkinVisuals.Resolve(skin, _shopTab == 1, shown);
            _bigBallShell.sprite = _bigLook.Shell;
            _bigBallShell.color = _bigLook.ShellColor;
            _bigBallCore.sprite = _bigLook.Core;
            _bigBallCore.enabled = _bigLook.Core != null;
            _bigBallCore.color = _bigLook.Tint;

            _bigTitle.text = Clip(skin.Name.ToUpperInvariant(), 20);
            _bigTitle.color = RarityColor(skin.Rarity);
            if (_bigHint != null) _bigHint.text = Loc.Get("fx.bigHint");

            // Переключатель нужен только тогда, когда есть что переключать.
            bool canSwitch = _shopComparing;
            _bigSwitch.gameObject.SetActive(canSwitch);
            if (canSwitch)
                _bigSwitchLabel.text = _bigShowsWorn ? Loc.Get("fx.current") : Loc.Get("fx.tryOn");

            // Мишень: 0.9 диаметра шарика в поперечнике, то есть радиус 0.45 D —
            // как у среднего обломка в бою.
            const float targetXD = 1.45f;
            const float targetRD = 0.45f;

            if (EnvelopeTable.TryGet(skin, out var def))
                _bigEnvelope.Play(def, new Vector2(targetXD / DemoScale, 0f),
                                  targetRD / DemoScale, loop: true);
            else
                _bigEnvelope.Stop();
        }

        /// <summary>Такт большого показа: шарик дышит так же, как в бою.</summary>
        void TickBigDemo(float dt)
        {
            if (_bigDemo == null || !_bigDemo.activeSelf) return;

            _bigPhase += dt;

            Vector2 s = SkinFxRuntime.MotionScale(_bigLook.Fx, _bigPhase) * _bigLook.VisualScale;
            _bigBall.localScale = new Vector3(s.x, s.y, 1f);

            if (_bigBallCore.enabled)
                _bigBallCore.rectTransform.localRotation =
                    Quaternion.Euler(0f, 0f, _bigPhase * SkinVisuals.CoreSpin);

            _bigEnvelope.Tick(dt);
        }

        // ------------------------------------------------- маленькая панель

        /// <summary>
        /// Запустить показ оболочки в панели выбранного. Зовётся «Сравнить» —
        /// чтобы рядом с двумя шариками сразу было видно разницу.
        /// </summary>
        void PlayShopEnvelope()
        {
            // Камень витрины: 44 точки при шарике в 112, то есть 0.39 D в
            // поперечнике. Те же доли, что и в бою.
            const float targetXD = 62f / ShopLayout.PreviewBallSize;
            const float targetRD = 22f / ShopLayout.PreviewBallSize;

            if (_hasPreviewEnv && _previewEnvelope != null)
                _previewEnvelope.Play(_previewEnvDef,
                                      new Vector2(targetXD / ShopScale, 0f),
                                      targetRD / ShopScale);
            else if (_previewEnvelope != null)
                _previewEnvelope.Stop();

            if (_shopComparing && _hasCompareEnv && _compareEnvelope != null)
                _compareEnvelope.Play(_compareEnvDef,
                                      new Vector2(targetXD / ShopScale, 0f),
                                      targetRD / ShopScale);
            else if (_compareEnvelope != null)
                _compareEnvelope.Stop();
        }

        void TickShopEnvelopes(float dt)
        {
            if (_previewEnvelope != null) _previewEnvelope.Tick(dt);
            if (_compareEnvelope != null) _compareEnvelope.Tick(dt);
        }

        /// <summary>Погасить показы: смена вкладки, скина или выход из витрины.</summary>
        void StopShopEnvelopes()
        {
            if (_previewEnvelope != null) _previewEnvelope.Stop();
            if (_compareEnvelope != null) _compareEnvelope.Stop();
            if (_bigDemo != null && _bigDemo.activeSelf) RefreshBigDemo();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Дев-панель: открыть большой показ оболочки выбранного скина.</summary>
        public void DebugPlayShopEnvelope()
        {
            PlayShopEnvelope();
            OpenBigDemo();
        }

        public bool DebugShopEnvelopeActive =>
            _bigEnvelope != null && _bigEnvelope.Active;
#endif
    }
}
