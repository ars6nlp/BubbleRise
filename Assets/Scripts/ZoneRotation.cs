using System.Collections.Generic;

namespace BubbleRise
{
    /// <summary>
    /// Очередь видов локаций в забеге. Круг — все виды каталога столько раз, каков
    /// их вес; внутри круга порядок случайный, но двух одинаковых подряд не бывает
    /// никогда, и на стыке кругов тоже. За круг встречается каждый вид.
    ///
    /// Случайность — своя, System.Random от зерна забега: UnityEngine.Random задаёт
    /// угрозы, и лишний вызов сдвинул бы весь их поток.
    /// </summary>
    public sealed class ZoneRotation
    {
        readonly System.Random _rng;
        readonly ZoneDef[] _defs;
        readonly int[] _left;
        ZoneKind? _peeked;

        /// <summary>Последний сыгранный вид (или вызванный из панели разработчика).</summary>
        public ZoneKind? Last { get; private set; }

        public ZoneRotation(int seed) : this(seed, ZoneCatalog.All) { }

        public ZoneRotation(int seed, ZoneDef[] defs)
        {
            _rng = new System.Random(seed);
            _defs = defs;
            _left = new int[defs.Length];
        }

        /// <summary>Какой вид будет следующим — не забирая его из очереди.</summary>
        public ZoneKind Peek()
        {
            if (!_peeked.HasValue) _peeked = Draw();
            return _peeked.Value;
        }

        /// <summary>Следующий вид — забрать из очереди.</summary>
        public ZoneKind Next()
        {
            var kind = Peek();
            _peeked = null;
            Last = kind;
            return kind;
        }

        /// <summary>
        /// Вид запущен в обход очереди (отладка): следующий по очереди не будет
        /// таким же. Заглянутый вперёд вид, если совпал, возвращается в круг.
        /// </summary>
        public void Played(ZoneKind kind)
        {
            if (_peeked.HasValue && _peeked.Value == kind)
            {
                _left[IndexOf(kind)]++;
                _peeked = null;
            }
            Last = kind;
        }

        ZoneKind Draw()
        {
            int pick = PickFromCircle();
            if (pick < 0)
            {
                // в круге остался только прошлый вид (или ничего) — начинаем новый круг
                for (int i = 0; i < _defs.Length; i++) _left[i] = System.Math.Max(1, _defs[i].Weight);
                pick = PickFromCircle();
            }
            if (pick < 0) pick = 0;   // в каталоге один вид — повтор неизбежен
            _left[pick]--;
            return _defs[pick].Kind;
        }

        /// <summary>Случайный вид из остатка круга с весом остатка, кроме прошлого.</summary>
        int PickFromCircle()
        {
            int total = 0;
            for (int i = 0; i < _defs.Length; i++)
                if (_left[i] > 0 && !IsLast(i)) total += _left[i];
            if (total == 0) return -1;
            int roll = _rng.Next(total);
            for (int i = 0; i < _defs.Length; i++)
            {
                if (_left[i] <= 0 || IsLast(i)) continue;
                if (roll < _left[i]) return i;
                roll -= _left[i];
            }
            return -1;
        }

        bool IsLast(int i) => Last.HasValue && _defs[i].Kind == Last.Value;

        int IndexOf(ZoneKind kind)
        {
            for (int i = 0; i < _defs.Length; i++) if (_defs[i].Kind == kind) return i;
            return 0;
        }

        /// <summary>Первые n видов очереди для данного зерна (проверки и отчёт).</summary>
        public static List<ZoneKind> Sequence(int seed, int n)
        {
            var r = new ZoneRotation(seed);
            var list = new List<ZoneKind>(n);
            for (int i = 0; i < n; i++) list.Add(r.Next());
            return list;
        }
    }
}
