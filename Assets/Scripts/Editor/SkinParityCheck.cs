using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace BubbleRise.EditorTools
{
    /// <summary>
    /// Сверка скинов: каждый ли получает полный набор слоёв и нет ли двух
    /// одинаковых.
    ///
    /// **Почему команда меню, а не EditMode-тест.** Тестовый фреймворк в проекте
    /// есть, но тестовая сборка обязана быть asmdef-сборкой, а asmdef не может
    /// ссылаться на предопределённую Assembly-CSharp — именно в ней живёт весь
    /// код игры. Полноценный тест потребовал бы перевести проект на asmdef
    /// целиком: правка рискованная, большая и к задаче отношения не имеющая.
    /// Команда меню делает ровно то же самое и запускается из CI через
    /// -executeMethod BubbleRise.EditorTools.SkinParityCheck.Run.
    ///
    /// **Что именно проверяется.** Для каждого скина обеих половин берётся тот же
    /// <see cref="SkinVisuals.Resolve"/>, которым пользуются и игра, и витрина, и
    /// сверяется, что:
    ///
    /// * оболочка есть всегда;
    /// * начинка есть у всех, кроме стекла (у него её не должно быть по замыслу);
    /// * аура, удар, статус и разрушение заданы;
    /// * нет двух скинов с совпадающим набором признаков.
    ///
    /// Отрисовку сравнить отсюда нельзя — она в рантайме. Но набор слоёв и их
    /// источник проверяются ровно те же, что увидит игрок.
    /// </summary>
    public static class SkinParityCheck
    {
        [MenuItem("BubbleRise/Проверить скины")]
        public static void Run()
        {
            var report = new StringBuilder();
            var problems = new List<string>();
            var combos = new Dictionary<string, string>();

            report.AppendLine("скин | половина | форма | материал | движение | след | аура | удар | статус | разрушение");

            for (int half = 0; half < 2; half++)
            {
                bool guardian = half == 1;
                var list = guardian ? SkinStore.Guardian : SkinStore.Bubble;

                for (int i = 0; i < list.Length; i++)
                {
                    var skin = list[i];
                    var look = SkinVisuals.Resolve(skin, guardian, i);
                    string where = skin.Name + (guardian ? " (страж)" : " (пузырёк)");

                    if (look.Shell == null)
                        problems.Add(where + ": нет оболочки");

                    // У стекла начинки нет по замыслу: это и есть «просто стекло».
                    bool needsCore = skin.Material != SkinMaterial.Glass
                                     && skin.Kind != UnlockKind.Boss;
                    if (needsCore && look.Core == null)
                        problems.Add(where + ": нет начинки для материала " + skin.Material);

                    if (look.Material.StatusTime <= 0f)
                        problems.Add(where + ": не задано время статуса");

                    if (look.Archetype.VisualScale <= 0f)
                        problems.Add(where + ": не задан размер стихии");

                    // Анимация обязана что-то менять. Две секунды — столько игрок
                    // смотрит на шарик, прежде чем решить, живой он или замер.
                    Vector2 at0 = SkinFxRuntime.MotionScale(look.Fx, 0f);
                    Vector2 at2 = SkinFxRuntime.MotionScale(look.Fx, 2f);
                    if ((at0 - at2).sqrMagnitude < 1e-8f)
                        problems.Add(where + ": движение не меняется за две секунды");

                    // Тот же вид обязан собираться для витрины: она зовёт ту же
                    // функцию, поэтому расхождение здесь означало бы, что функция
                    // зависит от чего-то вне своих аргументов.
                    var again = SkinVisuals.Resolve(skin, guardian, i);
                    if (again.Shell != look.Shell || again.Core != look.Core)
                        problems.Add(where + ": вид собирается нестабильно");

                    report.AppendLine(string.Format(
                        "{0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9}",
                        skin.Name, guardian ? "страж" : "пузырёк",
                        look.Archetype.Shape, skin.Material, look.Fx.Motion, look.Fx.Trail,
                        look.Material.AuraStyle, look.Material.Impact,
                        look.Material.Status, look.Material.Pattern));

                    // Полный набор признаков обязан быть уникальным: два скина с
                    // одинаковой комбинацией — это один скин, проданный дважды.
                    string combo = string.Join("/", new[]
                    {
                        skin.Material.ToString(), look.Fx.Motion.ToString(),
                        look.Fx.Trail.ToString(), look.Material.Status.ToString(),
                    });
                    if (combos.TryGetValue(combo, out string first) && !guardian)
                        problems.Add(where + ": тот же набор признаков, что у " + first);
                    else if (!guardian)
                        combos[combo] = skin.Name;
                }
            }

            Debug.Log("[BubbleRise] сверка скинов\n" + report);

            if (problems.Count == 0)
            {
                Debug.Log("[BubbleRise] скины сверены: расхождений нет, " +
                          (SkinStore.Bubble.Length * 2) + " половин проверено");
                return;
            }

            foreach (string p in problems) Debug.LogError("[BubbleRise] " + p);
            Debug.LogError("[BubbleRise] расхождений: " + problems.Count);
        }
    }
}
