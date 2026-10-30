using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Размеры игрового поля в мировых координатах.
    /// Высота мира фиксирована (worldHeight), ширина зависит от аспекта экрана,
    /// но игровая зона ограничена «портретной» шириной, чтобы геймплей на широких
    /// экранах не отличался от мобильного.
    /// </summary>
    public class Playfield : MonoBehaviour
    {
        public Camera Cam { get; private set; }
        public float HalfHeight { get; private set; }
        public float HalfWidth { get; private set; }       // реальная половина ширины экрана
        public float PlayHalfWidth { get; private set; }   // половина ширины игровой зоны

        public float Top => HalfHeight;
        public float Bottom => -HalfHeight;

        float _worldHeight = 10f;
        float _designAspect = 0.64f;
        int _lastW, _lastH;


        public void Init(Camera cam, float worldHeight, float designAspect)
        {
            Cam = cam;
            _worldHeight = worldHeight;
            _designAspect = designAspect;
            Recalculate(true);
        }

        /// <summary>
        /// Камера неподвижна всегда: мир едет мимо неё. Тряски нет ни на одной
        /// ступени яркости — от неё у владельца болят глаза, а «совсем слабая
        /// тряска» всё равно остаётся тряской.
        /// </summary>
        public void Tick(float dt)
        {
            if (Screen.width != _lastW || Screen.height != _lastH) Recalculate(false);
        }

        void Recalculate(bool force)
        {
            _lastW = Screen.width;
            _lastH = Screen.height;
            HalfHeight = _worldHeight * 0.5f;
            Cam.orthographic = true;
            Cam.orthographicSize = HalfHeight;
            float aspect = Cam.aspect > 0.01f ? Cam.aspect : 0.64f;
            HalfWidth = HalfHeight * aspect;
            PlayHalfWidth = Mathf.Min(HalfWidth, HalfHeight * _designAspect);
        }

        /// <summary>Y пузырька: доля высоты снизу.</summary>
        public float YFromBottom(float t) => Bottom + _worldHeight * t;

        /// <summary>Y: доля высоты сверху.</summary>
        public float YFromTop(float t) => Top - _worldHeight * t;

        public Vector2 ClampToScreen(Vector2 p, float radius)
        {
            return new Vector2(
                Mathf.Clamp(p.x, -HalfWidth + radius, HalfWidth - radius),
                Mathf.Clamp(p.y, Bottom + radius, Top - radius));
        }
    }
}
