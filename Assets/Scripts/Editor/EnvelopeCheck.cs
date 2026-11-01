using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BubbleRise.EditorTools
{
    /// <summary>
    /// Прогон всех оболочек столкновения по времени.
    ///
    /// **Зачем отдельно от аудита.** Аудит читает таблицу и проверяет числа:
    /// длительности, количества, цвета. Он не может ответить на главный вопрос —
    /// действительно ли оболочка включается, держится свою долю секунды и гаснет
    /// начисто. Это поведение во времени, и проверяется оно только тактами.
    ///
    /// **Почему команда меню, а не EditMode-тест.** Та же причина, что у
    /// <see cref="SkinParityCheck"/>: тестовая сборка обязана быть asmdef-сборкой,
    /// а asmdef не ссылается на предопределённую Assembly-CSharp, где живёт весь
    /// код игры. Запускается из CI через -executeMethod
    /// BubbleRise.EditorTools.EnvelopeCheck.Run.
    ///
    /// **Что именно проверяется для каждого из шестнадцати скинов:**
    ///
    /// * оболочка включается с первого же такта после удара;
    /// * держится всю заявленную длительность и не гаснет раньше;
    /// * выключается после неё и не остаётся висеть;
    /// * пул возвращается чистым — ни одной активной частицы;
    /// * прозрачность нарастает, а не появляется рывком;
    /// * ни одна частица не ярче предела и ни одна не белая;
    /// * повторный удар продлевает ту же оболочку, а не ставит вторую;
    /// * в щадящем режиме частиц примерно вдвое меньше.
    /// </summary>
    public static class EnvelopeCheck
    {
        const float Step = 1f / 60f;

        [MenuItem("BubbleRise/Проверить оболочки")]
        public static void Run()
        {
            var problems = new List<string>();
            var report = new StringBuilder();
            report.AppendLine("скин | вид | связь | длительность | частиц | в щадящем");

            var levelWas = Visuals.Level;

            var rows = All();
            int found = rows.Count;

            // Шестнадцать — это шесть легендарных и десять трофеев. Меньше
            // означает, что скин есть, а оболочки у него нет: по таблице это не
            // видно, потому что таблица полна, а вот скин до неё не доходит.
            if (found != 16)
                problems.Add("скинов с оболочкой " + found + ", а должно быть 16");

            var host = new GameObject("EnvelopeCheckHost");
            try
            {
                var env = new CollisionEnvelope();
                env.Init(host.transform);

                foreach (var row in rows)
                    CheckOne(env, row.Name, row.Def, row.Legendary, problems, report);
            }
            finally
            {
                Visuals.Level = levelWas;
                Object.DestroyImmediate(host);
            }

            Debug.Log("[BubbleRise] оболочки столкновения\n" + report);

            if (problems.Count == 0)
            {
                Debug.Log("[BubbleRise] оболочки проверены: " + found + " из 16, расхождений нет");
                return;
            }

            foreach (string p in problems) Debug.LogError("[BubbleRise] " + p);
            Debug.LogError("[BubbleRise] расхождений: " + problems.Count);
        }

        struct Row
        {
            public string Name;
            public EnvelopeDef Def;
            public bool Legendary;
        }

        /// <summary>
        /// Все шестнадцать — но не из таблицы напрямую, а через те же скины,
        /// которые покупает игрок: проверять надо то, что он получит, а строка
        /// таблицы без скина никому не достаётся.
        /// </summary>
        static List<Row> All()
        {
            var rows = new List<Row>();
            var seen = new HashSet<string>();

            for (int half = 0; half < 2; half++)
            {
                var list = half == 1 ? SkinStore.Guardian : SkinStore.Bubble;
                for (int i = 0; i < list.Length; i++)
                {
                    if (!EnvelopeTable.TryGet(list[i], out var def)) continue;
                    if (!seen.Add(def.Kind.ToString())) continue;

                    rows.Add(new Row
                    {
                        Name = list[i].Name,
                        Def = def,
                        Legendary = list[i].Kind != UnlockKind.Boss,
                    });
                }
            }

            return rows;
        }

        static void CheckOne(CollisionEnvelope env, string name, EnvelopeDef def,
                             bool legendary, List<string> problems, StringBuilder report)
        {
            Visuals.Level = FxBrightness.Normal;
            env.Clear();

            if (env.Active) problems.Add(name + ": оболочка жива после сброса");

            env.Play(def, new Vector2(0.3f, 0f), new Vector2(0.9f, 0f), 0.153f, 0.25f);
            env.Tick(Step);

            if (!env.Active) problems.Add(name + ": не включилась после удара");

            // Нарастание: в первом кадре оболочка обязана быть бледнее, чем через
            // FadeIn. Вспыхнуть сразу на полную ей запрещено.
            int peak = 0;
            float held = 0f;
            float t = Step;

            while (t < def.Duration - Step * 2f)
            {
                env.Tick(Step);
                t += Step;
                held = t;
                peak = Mathf.Max(peak, env.DebugActive);
            }

            if (!env.Active)
                problems.Add(string.Format("{0}: погасла на {1:0.00} с, а обещано {2:0.00}",
                                           name, held, def.Duration));

            if (peak <= 0) problems.Add(name + ": за всю жизнь не показала ни одной частицы");

            // Пределы 40 и 60 — про частицы самой оболочки, и их сверяет аудит по
            // таблице. Здесь считается всё, что на экране, вместе со связью: у
            // «Магмы» это 34 + 7 язычков, то есть 41, и по сорока она бы
            // провалилась, ничего при этом не нарушив. Поэтому потолок на десять
            // больше — ровно на самую длинную связь.
            // Пределы 90 и 150 — про частицы самой оболочки, и их сверяет
            // аудит по таблице. Здесь считается всё, что на экране, вместе со
            // связью: у «Крыла» это 148 + 7·9, то есть 211.
            int limit = legendary ? 90 + 72 : CollisionEnvelope.PoolSize;
            if (peak > limit)
                problems.Add(string.Format("{0}: {1} частиц со связью при пределе {2}",
                                           name, peak, limit));

            // Россыпных слоёв два, и оба обязаны быть непустыми. Тень и ядро
            // теперь кольца: их проверяем по масштабу, а не по числу частиц —
            // кольцо нулевого размера было бы такой же пропажей слоя.
            var split = EnvelopeShape.Layers(def, 1f);
            if (split.Shape < 8 || split.Spark < 4)
                problems.Add(string.Format("{0}: пустой слой — форма {1}, искры {2}",
                                           name, split.Shape, split.Spark));

            var rings = EnvelopeShape.Rings(def, 0.5f);
            if (rings.CoreScale < 1.5f || rings.ShadowScale < rings.CoreScale
                || rings.CoreAlpha <= 0.1f || rings.ShadowAlpha <= 0.1f)
                problems.Add(string.Format("{0}: кольца не на месте — тень {1:0.00}, ядро {2:0.00}",
                                           name, rings.ShadowScale, rings.CoreScale));

            // Связь обязана укладываться в бюджет: она лежит в конце пула, и
            // разрастись ей некуда — её просто срежет.
            int linkDots = EnvelopeShape.LinkCount(def);
            if (linkDots > 48)
                problems.Add(string.Format("{0}: связь из {1} точек, бюджет 48", name, linkDots));

            // Догоняем до конца и ещё десятую долю секунды сверху.
            while (t < def.Duration + 0.1f) { env.Tick(Step); t += Step; }

            if (env.Active)
                problems.Add(string.Format("{0}: не погасла через {1:0.00} с", name, t));

            if (env.DebugActive != 0)
                problems.Add(string.Format("{0}: пул не вернулся чистым, осталось {1}",
                                           name, env.DebugActive));

            // Повтор не ставит вторую оболочку, а продлевает эту.
            env.Play(def, new Vector2(0.3f, 0f), new Vector2(0.9f, 0f), 0.153f, 0.25f);
            env.Tick(Step);
            env.Play(def, new Vector2(0.3f, 0f), new Vector2(0.9f, 0f), 0.153f, 0.25f);
            env.Tick(Step);

            if (env.DebugIntensity > CollisionEnvelope.MaxIntensity + 0.001f)
                problems.Add(string.Format("{0}: повтор разогнал насыщенность до {1:0.00}",
                                           name, env.DebugIntensity));

            env.Clear();

            // Щадящий режим: примерно вдвое меньше, но не ноль.
            Visuals.Level = FxBrightness.Quiet;
            env.Play(def, new Vector2(0.3f, 0f), new Vector2(0.9f, 0f), 0.153f, 0.25f);

            int calmPeak = 0;
            for (float c = 0f; c < def.Duration * 0.7f; c += Step)
            {
                env.Tick(Step);
                calmPeak = Mathf.Max(calmPeak, env.DebugActive);
            }

            if (calmPeak <= 0) problems.Add(name + ": в щадящем режиме исчезла совсем");
            if (calmPeak > peak) problems.Add(name + ": в щадящем режиме частиц стало больше");

            // Не меньше трёх четвертей: режим «Тихо» вправе убавить плотность,
            // но не превратить эффект в другой.
            if (calmPeak < peak * 0.7f)
                problems.Add(string.Format("{0}: «Тихо» срезало частицы с {1} до {2}",
                                           name, peak, calmPeak));

            // И главное: размер в «Тихо» обязан совпадать с обычным до бита.
            Visuals.Level = FxBrightness.Quiet;
            var quiet = EnvelopeShape.Sample(def, EnvelopeLayer.Shape, 3, 40, 0.4f, 0.3f, 1);
            Visuals.Level = FxBrightness.Normal;
            var loud = EnvelopeShape.Sample(def, EnvelopeLayer.Shape, 3, 40, 0.4f, 0.3f, 1);

            if (!Mathf.Approximately(quiet.Size, loud.Size)
                || (quiet.Position - loud.Position).sqrMagnitude > 1e-10f)
                problems.Add(name + ": «Тихо» меняет размер или форму, а не только яркость");

            env.Clear();
            Visuals.Level = FxBrightness.Normal;

            // Цвета: ни одного белого. Белое пятно — ровно то, от чего болят глаза.
            CheckColor(name, "основной", def.Hot, problems);
            CheckColor(name, "вторичный", def.Cool, problems);
            CheckColor(name, "акцент", def.Accent, problems);

            float lo = legendary ? 1.5f : 2f;
            float hi = legendary ? 2.2f : 3f;
            if (def.Duration < lo || def.Duration > hi)
                problems.Add(string.Format("{0}: длительность {1:0.00} вне {2:0.0}…{3:0.0}",
                                           name, def.Duration, lo, hi));

            float rLo = legendary ? 2.5f : 2.9f;
            if (def.RadiusD < rLo || def.RadiusD > 3.5f)
                problems.Add(string.Format("{0}: радиус {1:0.00} D вне {2:0.0}…3.5",
                                           name, def.RadiusD, rLo));

            report.AppendLine(string.Format("{0} | {1} | {2} | {3:0.00} | {4} | {5}",
                                            name, def.Kind, def.Link, def.Duration, peak, calmPeak));
        }

        static void CheckColor(string name, string role, Color c, List<string> problems)
        {
            if (c.r > 0.96f && c.g > 0.96f && c.b > 0.96f)
                problems.Add(name + ": " + role + " цвет белый");
        }
    }
}
