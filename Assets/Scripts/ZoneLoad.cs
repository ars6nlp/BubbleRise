using System.Collections.Generic;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Нагрузка локации на линию пузырька. Пузырёк стоит на x = 0, мир едет вниз:
    /// шар «на линии», если, не разбитый, он заденет пузырёк — по x ближе, чем
    /// радиус × hazardHitboxScale + хитбокс пузырька (как в GameManager.HitsBubble).
    /// Счёт — для панели разработчика, проверок и отчёта; на игру не влияет.
    /// </summary>
    public static class ZoneLoad
    {
        /// <summary>Окно счёта, с.</summary>
        public const float Window = 2f;

        public static bool OnLine(float x, float radius, float bubbleHit)
            => Mathf.Abs(x) < radius * Difficulty.Current.hazardHitboxScale + bubbleHit;

        /// <summary>Шары на линии (x, y, радиус в z), по возрастанию y.</summary>
        public static List<Vector3> LineBlobs(IList<Vector3> blobs, float bubbleHit)
        {
            var line = new List<Vector3>();
            for (int i = 0; i < blobs.Count; i++)
                if (OnLine(blobs[i].x, blobs[i].z, bubbleHit)) line.Add(blobs[i]);
            line.Sort((a, b) => a.y.CompareTo(b.y));
            return line;
        }

        /// <summary>Больше всего шаров на линии за любые 2 с при скорости speed.</summary>
        public static int Peak(IList<Vector3> blobs, float speed, float bubbleHit)
        {
            var line = LineBlobs(blobs, bubbleHit);
            float span = Window * speed;
            int best = 0;
            for (int i = 0, j = 0; i < line.Count; i++)
            {
                while (line[i].y - line[j].y > span) j++;
                best = Mathf.Max(best, i - j + 1);
            }
            return best;
        }

        /// <summary>В среднем шаров на линии за 2 с по всей длине локации.</summary>
        public static float Mean(IList<Vector3> blobs, float height, float speed, float bubbleHit)
            => height > 0.01f ? LineBlobs(blobs, bubbleHit).Count * Window * speed / height : 0f;

        /// <summary>
        /// Сейчас: живые шары на линии от пузырька и на 2 с вперёд (панель разработчика).
        /// </summary>
        public static int Ahead(OceanZone zone, float bubbleY, float speed, float bubbleHit)
        {
            if (zone == null) return 0;
            int n = 0;
            float top = bubbleY + Window * speed;
            var blobs = zone.Blobs;
            for (int i = 0; i < blobs.Count; i++)
            {
                var b = blobs[i];
                if (!b.Alive) continue;
                var c = b.Center;
                if (c.y < bubbleY || c.y > top) continue;
                if (OnLine(c.x, b.Radius, bubbleHit)) n++;
            }
            return n;
        }

        /// <summary>Что успевает безупречный игрок.</summary>
        public struct Perfect
        {
            /// <summary>Шары на линии, которые не разбить вовремя ни при какой игре.</summary>
            public int Misses;
            /// <summary>Самый малый запас по времени до последнего мгновения, с.</summary>
            public float MinSlack;
            /// <summary>Самый быстрый переход стража между двумя ударами, юн./с.</summary>
            public float PeakMove;
        }

        /// <summary>
        /// Безупречный игрок против шаров на линии. Шар виден, когда его край
        /// въезжает в верх экрана; раньше, чем через reaction, страж к нему не
        /// трогается. Разбить его можно, пока он не подошёл к пузырьку ближе, чем
        /// ближняя граница стража + досягаемость + радиус (страж прямо над пузырьком —
        /// самый поздний удар). Между ударами — перезарядка стража; переход — со
        /// скоростью не выше moveSpeed. Порядок — по сроку (кто раньше подойдёт).
        /// Шары не на линии не трогаются: безупречный игрок их обходит.
        /// </summary>
        /// <param name="blobs">x, y относительно середины локации, радиус в z.</param>
        /// <param name="zoneY0">Середина локации в момент появления (мир).</param>
        public static Perfect Simulate(IList<Vector3> blobs, float zoneY0, float speed, float fieldTop,
                                       float bubbleY, float bubbleHit, float reach, float minDistance,
                                       float cooldown, float reaction, float moveSpeed, Vector2 home)
        {
            var line = LineBlobs(blobs, bubbleHit);
            line.Sort((a, b) => (a.y - a.z).CompareTo(b.y - b.z));   // по сроку
            var result = new Perfect { MinSlack = float.MaxValue };
            float t = -1e6f;
            Vector2 pos = home;
            bool first = true;
            for (int i = 0; i < line.Count; i++)
            {
                var b = line[i];
                float y0 = zoneY0 + b.y;
                float release = (y0 - b.z - fieldTop) / speed + reaction;
                float deadline = (y0 - (bubbleY + minDistance + reach + b.z)) / speed;

                // Трогается к шару не раньше, чем его заметил, и не раньше прошлого удара;
                // бьёт не раньше конца перезарядки. Шар едет навстречу — первый момент,
                // когда страж успевает дотянуться (касание на r + досягаемость).
                float ready = first ? release : Mathf.Max(t, release);
                float earliest = first ? release : Mathf.Max(release, t + cooldown);
                float start = float.NaN;
                Vector2 hit = pos;
                float touch = (b.z + reach) * 0.9f;
                for (float tau = earliest; tau <= deadline + 1e-4f; tau += 0.005f)
                {
                    Vector2 at = new Vector2(b.x, y0 - speed * tau);
                    Vector2 way = at - pos;
                    float need = Mathf.Max(0f, way.magnitude - touch);
                    if (need > moveSpeed * (tau - ready)) continue;
                    start = tau;
                    hit = way.magnitude > touch ? pos + way.normalized * need : pos;
                    break;
                }
                if (float.IsNaN(start))
                {
                    result.Misses++;
                    continue;   // не успел — шар бьёт пузырёк, страж свободен
                }
                float slack = deadline - start;
                if (!first && start - t > 1e-4f)
                    result.PeakMove = Mathf.Max(result.PeakMove, Vector2.Distance(pos, hit) / (start - t));
                result.MinSlack = Mathf.Min(result.MinSlack, slack);
                t = start;
                pos = hit;
                first = false;
            }
            if (result.MinSlack == float.MaxValue) result.MinSlack = 99f;   // на линии ничего
            return result;
        }

        /// <summary>
        /// Самая малая скорость пальца, при которой безупречный игрок успевает всё, что
        /// вообще можно успеть (промахов не больше, чем при скорости 40 юн./с).
        /// Мерило рывков: чем она выше, тем резче приходится водить стража.
        /// </summary>
        public static float RequiredSpeed(IList<Vector3> blobs, float zoneY0, float speed, float fieldTop,
                                          float bubbleY, float bubbleHit, float reach, float minDistance,
                                          float cooldown, float reaction, Vector2 home)
        {
            int floor = Simulate(blobs, zoneY0, speed, fieldTop, bubbleY, bubbleHit, reach, minDistance,
                                 cooldown, reaction, 40f, home).Misses;
            float lo = 0.25f, hi = 40f;
            for (int k = 0; k < 14; k++)
            {
                float mid = (lo + hi) * 0.5f;
                int m = Simulate(blobs, zoneY0, speed, fieldTop, bubbleY, bubbleHit, reach, minDistance,
                                 cooldown, reaction, mid, home).Misses;
                if (m <= floor) hi = mid; else lo = mid;
            }
            return hi;
        }
    }
}
