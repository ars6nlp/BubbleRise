using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Фон океана. Собирает слои, каждый — один меш и один вызов отрисовки:
    ///
    /// * <see cref="WaterBackdrop"/> — толща воды по <see cref="DepthLook"/>,
    ///   свет у краёв, виньетка в углах;
    /// * <see cref="OceanLight"/> — лучи в три слоя и каустики у кромки;
    /// * <see cref="WorldLayers"/> — четыре слоя параллакса: дальний, средний,
    ///   ближний и передний, с биомами по глубине;
    /// * <see cref="FarLife"/> — далёкие косяки, скаты, медузы, кит;
    /// * <see cref="MarineSnow"/> — морской снег в два слоя и пузырьки со дна.
    ///
    /// Геймплея не касается ничто из этого. Вид воды берётся из высоты подъёма:
    /// на 0 м бездна, к 10 000 м — бирюза под поверхностью, дальше круг заново.
    /// </summary>
    public class BackgroundController : MonoBehaviour
    {
        GameConfig _cfg;
        Playfield _field;

        WaterBackdrop _water;
        OceanLight _light;
        MarineSnow _snow;
        FarLife _life;
        WorldLayers _layers;
        bool _empty, _menu;
        bool _hasLook;

        Color _themeTop, _themeBottom;
        float _themeBlend, _themeTarget;
        int _zoneBiome = -1;
        bool _iceOn;
        float _ice;

        // Свет и туман новой локации: к ним вид воды уходит не быстрее ThemeFade.
        int _zoneKit = -1;
        float _zoneMix;
        ZoneLight _zoneLight;
        DepthLook _depthLook;

        /// <summary>Снег во льду: гуще и холоднее (сумма каналов 2.27 — без белого).</summary>
        static readonly Color IceSnow = Palette.Hex("AEC2D2");
        float _time;

        /// <summary>Вид воды в этом кадре — его же берут декорации и звук.</summary>
        public DepthLook Look { get; private set; }

        public void Init(GameConfig cfg, Playfield field)
        {
            _cfg = cfg;
            _field = field;
            _water = new WaterBackdrop(transform, field);
            _light = new OceanLight(transform, field);
            _life = new FarLife(transform, field);
            _snow = new MarineSnow(transform, field);
            _layers = new WorldLayers(transform, field);
            Look = DepthLook.At(0f);
            _water.Tick(Look, _themeTop, _themeBottom, 0f);
        }

        /// <summary>Раскладка пересчитывается каждый кадр в Tick — экран мог повернуться.</summary>
        public void ApplyLayout() { }

        /// <param name="depth">Высота подъёма в метрах.</param>
        /// <param name="scrollSpeed">Скорость подъёма пузырька, юнитов/с.</param>
        public void Tick(float dt, float depth, float scrollSpeed)
        {
            _time += dt;
            OceanCurrent.Tick(dt);
            // Вид воды догоняет глубину за ~1 с: новый заплыв (глубина сбрасывается
            // в ноль) и прыжок глубины в отладке не меняют яркость за кадр.
            var target = DepthLook.At(depth);
            var look = _hasLook ? DepthLook.Lerp(_depthLook, target, 1f - Mathf.Exp(-dt * 2f)) : target;
            _hasLook = true;
            _depthLook = look;
            // свет новой локации — поверх глубины, по доле не быстрее ThemeFade
            _zoneMix = Mathf.MoveTowards(_zoneMix, _zoneKit >= 0 ? 1f : 0f, dt / ThemeFade);
            if (_zoneMix > 0f) look = ZoneLook(look, _zoneLight, _zoneMix * _zoneLight.Weight);
            Look = look;
            bool high = Visuals.HighDetail;

            // Вода локации подмешивается не быстрее, чем за ThemeFade: на большой
            // скорости локация закрывает экран за ~1 с, а смена цвета всего
            // экрана должна идти не меньше 1.5 с.
            _themeBlend = Mathf.MoveTowards(_themeBlend, _themeTarget, dt / ThemeFade);
            _ice = Mathf.MoveTowards(_ice, _iceOn ? 1f : 0f, dt / ThemeFade);
            _water.Tick(look, _themeTop, _themeBottom, _themeBlend);
            _layers.ZoneBiome = _zoneBiome;
            _layers.IceActive = _iceOn;
            _layers.ZoneKit = _zoneKit;
            _light.Tick(dt, look, high);
            _layers.Tick(dt, scrollSpeed, depth, look, high, _empty, _menu ? 1f : 0f);
            _life.Tick(dt, scrollSpeed, look, high);
            var snowLook = look;
            if (_ice > 0f)
            {
                snowLook.Snow = Mathf.Lerp(look.Snow, 1f, _ice * 0.6f);
                snowLook.SnowTint = Color.Lerp(look.SnowTint, IceSnow, _ice);
            }
            _snow.Tick(dt, scrollSpeed, snowLook, high, _time);
        }

        /// <summary>
        /// Подмешать цвет воды локации. blend = 0 — обычная вода глубины,
        /// 1 — полностью цвет локации.
        /// </summary>
        public void SetTheme(Color top, Color bottom, float blend)
        {
            _themeTop = top;
            _themeBottom = bottom;
            _themeTarget = Mathf.Clamp01(blend);
        }

        /// <summary>Вода локации уходит так же плавно, как пришла.</summary>
        public void ClearTheme()
        {
            _themeTarget = 0f;
            _zoneBiome = -1;
            _iceOn = false;
            _zoneKit = -1;
        }

        /// <summary>
        /// На экране новая локация (ZoneKind; -1 — нет): её дальний набор в слое
        /// параллакса и её свет и туман. Свет уходит так же плавно, как пришёл.
        /// </summary>
        public void SetZoneKind(int kind)
        {
            _zoneKit = kind;
            if (kind >= 0) _zoneLight = ZoneCatalog.Get((ZoneKind)kind).Light;
        }

        static DepthLook ZoneLook(DepthLook a, ZoneLight z, float t)
        {
            a.EdgeLight = Color.Lerp(a.EdgeLight, z.EdgeLight, t);
            a.Fog = Color.Lerp(a.Fog, z.Fog, t);
            a.Shafts = Mathf.Lerp(a.Shafts, z.Shafts, t);
            a.Caustics = Mathf.Lerp(a.Caustics, z.Caustics, t);
            a.Snow = Mathf.Lerp(a.Snow, z.Snow, t);
            a.SnowTint = Color.Lerp(a.SnowTint, z.SnowTint, t);
            return a;
        }

        /// <summary>На экране «Ледяной разлом»: ледяной слой, холодный снег.</summary>
        public void SetIce(bool on) => _iceOn = on;

        /// <summary>Самая быстрая смена воды локации целиком, с.</summary>
        public const float ThemeFade = 1.6f;

        /// <summary>
        /// Набор дальних слоёв внутри локации (-1 — по глубине). Новый набор
        /// получают только слоты, входящие сверху: смена идёт вместе с миром.
        /// </summary>
        public void SetZoneBiome(int biome) => _zoneBiome = biome;

        /// <summary>Пустая сцена (выход «Великой тени»): декор уходит сразу и не возвращается, пока не снимут.</summary>
        public void SetEmptyStage(bool empty) => _empty = empty;

        /// <summary>В меню и на итогах слои медленно плывут вбок — живой фон.</summary>
        public void SetMenuDrift(bool on) => _menu = on;
    }
}
