using System;
using UnityEngine;

namespace BubbleRise
{
    /// <summary>Мини-боссы, часть вторая: рыба-меч, краб, морок, пиранья, черепаха, акула, скат.</summary>
    public static partial class SpriteFactory
    {
        /// <summary>Фигура в системе рыбы: поле расстояний и цвет считаются в (u, v).</summary>
        static void FillL(MiniBossCanvas c, MiniBossFrame f, Rect local, Func<Vector2, float> sdf,
                          Func<Vector2, float, Color> paint, float soft = 0f)
        {
            c.Fill(f.Box(local.xMin, local.yMin, local.xMax, local.yMax), p => sdf(f.ToL(p)) * f.S,
                   (p, d) => paint(f.ToL(p), d / f.S), soft);
        }

        static void FillL(MiniBossCanvas c, MiniBossFrame f, Rect local, Func<Vector2, float> sdf, Color color, float soft = 0f)
            => FillL(c, f, local, sdf, (q, d) => color, soft);

        static void LineL(MiniBossCanvas c, MiniBossFrame f, Vector2[] local, float width, Color color, float soft = 0f)
        {
            var w = new Vector2[local.Length];
            for (int i = 0; i < w.Length; i++) w[i] = f.ToW(local[i]);
            Line(c, w, width * f.S, color, soft);
        }

        static void TriL(MiniBossCanvas c, MiniBossFrame f, Vector2 a, Vector2 b, Vector2 t, Color color)
        {
            Vector2 wa = f.ToW(a), wb = f.ToW(b), wt = f.ToW(t);
            c.Fill(Sdf.Around(new[] { wa, wb, wt }, 0.02f), p => Sdf.Triangle(p, wa, wb, wt), color);
        }

        static Rect LR(float u0, float v0, float u1, float v1) => Rect.MinMaxRect(u0, v0, u1, v1);

        // ============================================================ РЫБА-МЕЧ
        // SpriteFactoryBosses: спина 172B3C, брюхо 8299AC, клинок E6EEF4 (здесь сталь
        // без белого), плавники 101E2E, шрамы 6E8699, глаз FF5A3C (тусклый).

        static readonly Color WBack = Palette.Hex("2A4258"), WBelly = Palette.Hex("7A90A2"),
                              WBlade = Palette.Hex("B4C2CC"), WBladeDark = Palette.Hex("6E8090"),
                              WFin = Palette.Hex("2E4660"), WScar = Palette.Hex("6E8699"),
                              WEye = Palette.Hex("B84A36"), WDark = Palette.Hex("0E1A26");

        /// <summary>Клинок: рыба-меч боком, носом вверх (MiniBossPose) — клинок, парус с лучами, вилка хвоста.</summary>
        static void SwordFish(MiniBossCanvas c)
        {
            var f = MiniBossPose.Swordfish;

            // хвостовая вилка и парус — за телом
            TriL(c, f, new Vector2(-0.58f, 0.01f), new Vector2(-0.74f, 0f), new Vector2(-0.92f, 0.36f), WFin);
            TriL(c, f, new Vector2(-0.58f, -0.01f), new Vector2(-0.74f, 0f), new Vector2(-0.92f, -0.36f), WFin);
            FillL(c, f, LR(-0.4f, 0.05f, 0.16f, 0.52f),
                  q => Sdf.Triangle(q, new Vector2(0.12f, 0.12f), new Vector2(-0.34f, 0.08f), new Vector2(-0.08f, 0.5f)),
                  (q, d) =>
                  {
                      float ray = Mathf.Repeat((q.x - q.y * 0.4f) * 14f, 1f);
                      Color col = Color.Lerp(WFin, WBack, 0.4f);
                      return Color.Lerp(col, WScar, ray < 0.18f ? 0.35f : 0f);
                  });
            // клинок от лба вперёд
            FillL(c, f, LR(0.1f, -0.1f, 1f, 0.12f), q => Sdf.Taper(q, new Vector2(0.18f, 0.02f), new Vector2(0.98f, 0.01f), 0.07f, 0.01f),
                  (q, d) =>
                  {
                      Color col = Color.Lerp(WBladeDark, WBlade, Sm(-0.03f, 0.02f, q.y));
                      return Color.Lerp(col, WBladeDark, Sm(-0.018f, 0f, d) * 0.6f);
                  });
            // тело — веретено с тёмной спиной и светлым брюхом
            FillL(c, f, LR(-0.72f, -0.3f, 0.42f, 0.3f), q => Mathf.Min(
                      Sdf.SMin(Sdf.Ellipse(q, new Vector2(-0.1f, 0f), 0.48f, 0.235f), Sdf.Ellipse(q, new Vector2(0.2f, 0.01f), 0.19f, 0.15f), 0.08f),
                      Sdf.Taper(q, new Vector2(-0.45f, 0f), new Vector2(-0.68f, 0f), 0.11f, 0.04f)),
                  (q, d) =>
                  {
                      float back = Sm(-0.09f, 0.07f, q.y);
                      Color col = Color.Lerp(WBelly, WBack, back);
                      col = Color.Lerp(col, WDark, Sm(-0.04f, 0f, d) * 0.35f);
                      float stripe = Sat01(1f - Mathf.Abs(q.y + 0.02f) / 0.012f);
                      return Color.Lerp(col, WBelly, stripe * 0.35f);
                  });
            TriL(c, f, new Vector2(0.1f, -0.1f), new Vector2(0.0f, -0.12f), new Vector2(-0.22f, -0.38f), WFin);
            // жаберная дуга, шрамы, глаз
            LineL(c, f, new[] { new Vector2(0.2f, 0.12f), new Vector2(0.15f, 0.02f), new Vector2(0.18f, -0.1f) }, 0.008f, WDark);
            LineL(c, f, new[] { new Vector2(-0.08f, -0.06f), new Vector2(-0.16f, 0.12f) }, 0.006f, WScar.WithAlpha(0.8f));
            LineL(c, f, new[] { new Vector2(-0.24f, -0.07f), new Vector2(-0.31f, 0.09f) }, 0.006f, WScar.WithAlpha(0.7f));
            var eye = f.ToW(new Vector2(0.27f, 0.05f));
            SlitEye(c, eye, 0.045f, 0.045f, WDark, WEye, Palette.Hex("6A2A1E"), WDark, vertical: false);
        }

        /// <summary>«Остриё»: клинок рыбы-меч остриём вверх, у основания — голова клином: глаз, жаберная крышка, край паруса.</summary>
        static void SwordTip(MiniBossCanvas c)
        {
            // край паруса слева и грудной плавник справа
            Vector2 s0 = new Vector2(-0.12f, -0.46f), s1 = new Vector2(-0.16f, -0.82f), s2 = new Vector2(-0.58f, -0.5f);
            c.Fill(Sdf.Around(new[] { s0, s1, s2 }, 0.02f), p => Sdf.Triangle(p, s0, s1, s2), (p, d) =>
            {
                float ray = Mathf.Repeat((p.x + p.y * 0.6f) * 13f, 1f);
                return Color.Lerp(Color.Lerp(WFin, WBack, 0.4f), WScar, ray < 0.18f ? 0.35f : 0f);
            });
            Vector2 f0 = new Vector2(0.12f, -0.6f), f1 = new Vector2(0.16f, -0.7f), f2 = new Vector2(0.46f, -0.78f);
            c.Fill(Sdf.Around(new[] { f0, f1, f2 }, 0.02f), p => Sdf.Triangle(p, f0, f1, f2), WFin);
            // клинок: стальной, с гребнем посередине, сужается к острию
            Vector2 b0 = new Vector2(0f, -0.34f), b1 = new Vector2(0f, 0.9f);
            c.Fill(R(-0.14f, -0.4f, 0.14f, 0.92f), p => Sdf.Taper(p, b0, b1, 0.1f, 0.008f), (p, d) =>
            {
                float t = Sat01((p.y + 0.34f) / 1.24f);
                float half = Mathf.Lerp(0.1f, 0.008f, t);
                float x = p.x / Mathf.Max(0.01f, half);
                Color col = Color.Lerp(WBlade, WBladeDark, Mathf.Abs(x) * 0.7f);
                col = Color.Lerp(col, Palette.Hex("C4CED6"), Sat01(1f - Mathf.Abs(x + 0.15f) / 0.12f) * 0.5f);
                return Color.Lerp(col, WDark, Sm(-0.012f, 0f, d) * 0.45f);
            });
            Dot(c, new Vector2(0.065f, 0.05f), 0.014f, WDark.WithAlpha(0.8f));
            Dot(c, new Vector2(-0.05f, 0.32f), 0.012f, WDark.WithAlpha(0.8f));
            Line(c, new[] { new Vector2(-0.03f, -0.14f), new Vector2(0.04f, -0.02f) }, 0.004f, WScar);

            // голова клином переходит в клинок: тёмная спина слева, светлое брюхо справа
            var hc = new Vector2(0f, -0.68f);
            c.Fill(R(-0.3f, -0.92f, 0.3f, -0.26f), p => Sdf.SMin(Sdf.Taper(p, new Vector2(0f, -0.3f), new Vector2(0f, -0.62f), 0.085f, 0.22f),
                                                                Sdf.Ellipse(p, hc, 0.25f, 0.2f), 0.08f),
                   (p, d) =>
                   {
                       Color col = Color.Lerp(WBack, WBelly, Sm(-0.06f, 0.12f, p.x));
                       return Vol(WDark, col, Color.Lerp(col, WBelly, 0.5f), (p - hc) / 0.3f, d, 0.04f, 0.4f);
                   });
            Line(c, new[] { new Vector2(-0.18f, -0.6f), new Vector2(-0.06f, -0.7f), new Vector2(0.1f, -0.7f), new Vector2(0.2f, -0.6f) }, 0.008f, WDark);
            Line(c, new[] { new Vector2(-0.02f, -0.36f), new Vector2(0.0f, -0.55f) }, 0.006f, WBelly.WithAlpha(0.5f));
            SlitEye(c, new Vector2(-0.07f, -0.48f), 0.05f, 0.05f, WDark, WEye, Palette.Hex("6A2A1E"), WDark, vertical: false);
        }

        // ============================================================ КРАБ
        // SpriteFactoryBosses: панцирь B4432A, тёмный 7E2C1B, блик E0764F, суставы 5C1E12,
        // зубы клешни F2E3D4 — всё приглушено.

        static readonly Color CShell = Palette.Hex("B4543A"), CShellDk = Palette.Hex("823420"),
                              CHilite = Palette.Hex("CC7858"), CJoint = Palette.Hex("5C1E12"),
                              CTeeth = Palette.Hex("C8BAA8"), CPupil = Palette.Hex("1E0A06");

        static void CrabLimb(MiniBossCanvas c, Vector2 a, Vector2 b, Vector2 e, float r0, float r1)
        {
            var pts = new[] { a, b, e };
            c.Fill(Sdf.Around(pts, r0 + 0.02f), p => Sdf.Chain(p, pts, r0, r1), (p, d) =>
                Color.Lerp(Color.Lerp(CShell, CHilite, 0.25f), CShellDk, Sm(-0.02f, 0f, d) * 0.5f));
            Dot(c, b, r0 * 0.75f, CShellDk);
        }

        /// <summary>Клешня: ладонь, неподвижный и подвижный пальцы, треугольные зубы по изнанке.</summary>
        static void CrabClawShape(MiniBossCanvas c, Vector2 palm, float pr, Vector2[] fixedF, Vector2[] moveF, float f0, float f1,
                                  float tilt, float tall = 0.92f)
        {
            Color Finger(Vector2 p, float d, float shift)
            {
                float band = Mathf.Repeat(p.y * 9f + shift, 1f);
                Color col = Vol(CShellDk, CShell, CHilite, (p - palm) / 0.6f, d, 0.03f, 0.5f);
                return Color.Lerp(col, CShellDk, band < 0.12f ? 0.4f : 0f);
            }
            c.Fill(Sdf.Around(fixedF, f0 + 0.02f), p => Sdf.Chain(p, fixedF, f0, f1), (p, d) => Finger(p, d, 0f));
            c.Fill(Sdf.Around(moveF, f0 + 0.02f), p => Sdf.Chain(p, moveF, f0 * 0.85f, f1), (p, d) => Finger(p, d, 0.5f));
            // зубы по внутренним кромкам пальцев — к зазору между ними
            for (int k = 0; k < 2; k++)
            {
                var pts = k == 0 ? fixedF : moveF;
                var other = k == 0 ? moveF : fixedF;
                float r0 = k == 0 ? f0 : f0 * 0.85f;
                for (int i = 2; i < pts.Length - 1; i++)
                {
                    Vector2 dir = (pts[i + 1] - pts[i - 1]).normalized;
                    Vector2 inner = new Vector2(dir.y, -dir.x);
                    if (Vector2.Dot(inner, other[Mathf.Min(i, other.Length - 1)] - pts[i]) < 0f) inner = -inner;
                    float w = Mathf.Lerp(r0, f1, (float)i / (pts.Length - 1));
                    Vector2 bse = pts[i] + inner * w * 0.8f;
                    Vector2 a = bse + dir * w * 0.35f, b = bse - dir * w * 0.35f, tip = bse + inner * w * 0.55f;
                    c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), CTeeth);
                }
            }
            c.Fill(Sdf.Box(palm.x, palm.y, pr * 1.5f, pr * 1.5f), p => Sdf.Ellipse(p, palm, pr, pr * tall, tilt), (p, d) =>
            {
                Color col = Vol(CShellDk, CShell, CHilite, (p - palm) / (pr * tall), d, 0.05f, 0.6f);
                float bump = Sat01(1f - (p - palm - new Vector2(-pr * 0.3f, pr * 0.3f)).magnitude / (pr * 0.35f));
                return Color.Lerp(col, CHilite, bump * 0.35f);
            });
        }

        /// <summary>Крабик: широкий панцирь с шипами, глаза на стебельках, ножки, поднятые клешни.</summary>
        static void CrabBody(MiniBossCanvas c)
        {
            // ножки — по три с каждой стороны
            for (int s = -1; s <= 1; s += 2)
                for (int k = 0; k < 3; k++)
                    CrabLimb(c, new Vector2(s * 0.4f, -0.06f - 0.07f * k), new Vector2(s * 0.64f, -0.06f - 0.1f * k),
                             new Vector2(s * 0.69f, -0.3f - 0.12f * k), 0.045f, 0.022f);
            // руки к клешням
            for (int s = -1; s <= 1; s += 2)
                CrabLimb(c, new Vector2(s * 0.42f, 0.1f), new Vector2(s * 0.64f, 0.26f), new Vector2(s * 0.54f, 0.42f), 0.055f, 0.045f);

            // шипы по верхней кромке панциря
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 0.17f;
                float top = 0.3f * Mathf.Sqrt(Mathf.Max(0f, 1f - (x / 0.56f) * (x / 0.56f)));
                Vector2 a = new Vector2(x - 0.055f, top - 0.04f), b = new Vector2(x + 0.055f, top - 0.04f);
                Vector2 tip = new Vector2(x * 1.08f, top + (i == 0 ? 0.11f : 0.085f));
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), CShellDk);
            }
            // панцирь
            var cc = new Vector2(0f, 0f);
            c.Fill(R(-0.6f, -0.34f, 0.6f, 0.34f), p => Sdf.Ellipse(p, cc, 0.56f, 0.3f), (p, d) =>
            {
                var n = new Vector2(p.x / 0.56f, p.y / 0.3f);
                Color col = Vol(CShellDk, CShell, CHilite, n, d, 0.05f, 0.6f);
                // радиальные рёбра
                float ang = Mathf.Atan2(p.y * 3.7f + 0.6f, p.x);
                float rib = Mathf.Repeat(ang * 3.5f, 1f);
                col = Color.Lerp(col, CShellDk, rib < 0.1f && p.y > -0.18f ? 0.35f : 0f);
                // зубчатая нижняя кромка
                float saw = -0.17f - 0.05f * Mathf.Cos(2.6f * p.x) + Mathf.Abs(Mathf.Repeat(p.x * 8f, 1f) - 0.5f) * 0.05f;
                return p.y < saw ? Color.Lerp(col, CJoint, 0.45f) : col;
            });
            // бугорки и шрам
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f + 0.3f;
                Dot(c, new Vector2(Mathf.Cos(a) * 0.3f, 0.04f + Mathf.Sin(a) * 0.12f), 0.035f, CHilite.WithAlpha(0.7f));
            }
            Line(c, new[] { new Vector2(0.18f, 0.12f), new Vector2(0.26f, 0.02f), new Vector2(0.24f, -0.06f) }, 0.006f, CJoint.WithAlpha(0.8f));
            // ротовые пластины
            c.Fill(R(-0.12f, -0.26f, 0.12f, -0.1f), p => Sdf.Box(p, new Vector2(0f, -0.17f), new Vector2(0.09f, 0.05f), 0.025f), CJoint);
            Line(c, new[] { new Vector2(0f, -0.13f), new Vector2(0f, -0.21f) }, 0.006f, CShellDk);

            // глаза на стебельках, с плоским веком — сердитый взгляд
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 a = new Vector2(s * 0.12f, 0.22f), b = new Vector2(s * 0.19f, 0.44f);
                c.Fill(Sdf.Around(a, b, 0.06f), p => Sdf.Capsule(p, a, b, 0.032f), CShellDk);
                var eye = new Vector2(s * 0.2f, 0.48f);
                Dot(c, eye, 0.072f, CShellDk);
                Dot(c, eye, 0.058f, CHilite);
                Dot(c, eye + new Vector2(-s * 0.01f, -0.005f), 0.03f, CPupil);
                c.Fill(Sdf.Box(eye.x, eye.y + 0.05f, 0.08f, 0.04f),
                       p => Mathf.Max(Sdf.Circle(p, eye, 0.075f), eye.y + 0.028f + s * (p.x - eye.x) * 0.35f - p.y), CShellDk);
            }

            // клешни подняты
            for (int s = -1; s <= 1; s += 2)
            {
                var palm = new Vector2(s * 0.5f, 0.52f);
                var fx = new[] { palm + new Vector2(s * 0.06f, 0.06f), new Vector2(s * 0.52f, 0.7f), new Vector2(s * 0.42f, 0.78f) };
                var mv = new[] { palm + new Vector2(-s * 0.06f, 0.05f), new Vector2(s * 0.36f, 0.63f), new Vector2(s * 0.3f, 0.7f) };
                CrabClawShape(c, palm, 0.13f, fx, mv, 0.07f, 0.02f, s * 30f);
            }
        }

        /// <summary>«Клешня»: большая клешня остриями вверх — овальная ладонь, неподвижный и подвижный пальцы, зубы по изнанке.</summary>
        static void CrabPincer(MiniBossCanvas c)
        {
            var wrist = new Vector2(0.12f, -0.76f);
            c.Fill(Sdf.Box(wrist.x, wrist.y, 0.18f, 0.16f), p => Sdf.Ellipse(p, wrist, 0.15f, 0.12f, -20f), (p, d) =>
                Color.Lerp(CShellDk, CJoint, Sm(-0.03f, 0f, d) * 0.6f));
            // шипы по внешней кромке ладони
            for (int i = 0; i < 3; i++)
            {
                float y = -0.52f + 0.17f * i;
                Vector2 a = new Vector2(-0.27f, y - 0.05f), b = new Vector2(-0.29f, y + 0.05f), tip = new Vector2(-0.42f, y + 0.03f);
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), CShellDk);
            }
            var fx = Bez3(new Vector2(-0.12f, -0.14f), new Vector2(-0.3f, 0.24f), new Vector2(-0.22f, 0.6f), new Vector2(-0.04f, 0.84f), 7);
            var mv = Bez3(new Vector2(0.16f, -0.08f), new Vector2(0.34f, 0.22f), new Vector2(0.24f, 0.55f), new Vector2(0.07f, 0.74f), 7);
            CrabClawShape(c, new Vector2(0.02f, -0.34f), 0.3f, fx, mv, 0.2f, 0.035f, 8f, 1.08f);
            Line(c, new[] { new Vector2(0.1f, -0.36f), new Vector2(0.18f, -0.44f), new Vector2(0.15f, -0.52f) }, 0.006f, CJoint.WithAlpha(0.8f));
        }

        // ============================================================ МОРОК
        // SpriteFactoryInk: мантия A772E9 / 8957CE / 67429E (здесь приглушённые), блик
        // B8A8E8, шрамы A99CCB, жилки 24123C, край 140A24, клюв 2C2238, глаза C8B6FF.

        static readonly Color ITop = Palette.Hex("9C70D0"), IMid = Palette.Hex("8C66C2"), ILow = Palette.Hex("7656AC"),
                              IWet = Palette.Hex("9C8CC8"), IScar = Palette.Hex("8C82A8"), IVein = Palette.Hex("24123C"),
                              IRim = Palette.Hex("140A24"), IBeak = Palette.Hex("2C2238"), IEye = Palette.Hex("A898D0"),
                              IEyeDark = Palette.Hex("6A5A98"), IInk = Palette.Hex("1C1030"), IInkEdge = Palette.Hex("2A1A48"),
                              IInkRim = Palette.Hex("7A64AC"), IArmTip = Palette.Hex("6852A0"),
                              IInkMid = Palette.Hex("58468A");

        /// <summary>Два глаза морока: левый открыт, правый под веком — как у босса.</summary>
        static void InkEyes(MiniBossCanvas c, Vector2 at, float k)
        {
            SlitEye(c, at + new Vector2(-0.15f, -0.005f) * k, 0.1f * k, 0.072f * k, IRim, IEye, IEyeDark, IRim, vertical: false, tilt: -6f);
            SlitEye(c, at + new Vector2(0.15f, 0f) * k, 0.088f * k, 0.064f * k, IRim, IEye, IEyeDark, IRim, vertical: false,
                    lid: 0.42f, lidColor: Color.Lerp(IRim, ILow, 0.4f), tilt: 9f);
        }

        /// <summary>Каракатица: высокая мантия-яйцо, два глаза с веком, складка-хмурь, клюв, восемь рук веером.</summary>
        static void InkMantle(MiniBossCanvas c)
        {
            for (int i = 0; i < 8; i++)
            {
                int s = i < 4 ? -1 : 1;
                int j = i < 4 ? 3 - i : i - 4;            // 0 — ближе к центру
                float x0 = s * (0.05f + 0.075f * j);
                float ang = (6f + 14f * j) * Mathf.Deg2Rad;
                float len = 0.76f - 0.04f * j;
                Vector2 a = new Vector2(x0, -0.02f);
                Vector2 dir = new Vector2(s * Mathf.Sin(ang), -Mathf.Cos(ang));
                Vector2 b = a + dir * len * 0.55f;
                Vector2 e = a + dir * len + new Vector2(s * 0.08f, 0.04f);
                Vector2 tip = e + new Vector2(s * 0.07f, 0.07f);
                var pts = Bez3(a, b, e, tip, 7);
                c.Fill(Sdf.Around(pts, 0.08f), p => Sdf.Chain(p, pts, 0.045f, 0.012f), (p, d) =>
                {
                    Along(p, pts, out float t, out _);
                    return Color.Lerp(Color.Lerp(IMid, ILow, t), IRim, Sm(-0.02f, 0f, d) * 0.2f);
                });
                for (int q = 2; q < pts.Length - 1; q += 2)
                    Dot(c, pts[q], Mathf.Lerp(0.045f, 0.012f, q / 7f) * 0.38f, IScar.WithAlpha(0.6f));
            }

            var mc = new Vector2(0f, 0.26f);
            Func<Vector2, float> mantle = p => Sdf.Ellipse(p, mc, 0.37f, p.y > mc.y ? 0.56f : 0.36f);
            c.Fill(R(-0.42f, -0.14f, 0.42f, 0.86f), mantle, (p, d) =>
            {
                float y = Sat01((p.y + 0.1f) / 0.92f);
                Color col = y < 0.5f ? Color.Lerp(ILow, IMid, y * 2f) : Color.Lerp(IMid, ITop, (y - 0.5f) * 2f);
                col *= 1f - 0.22f * Mathf.Pow(Mathf.Abs(p.x) / 0.37f, 2f);
                col.a = 1f;
                float mott = Sdf.Fbm(p.x * 5f + 3f, p.y * 5f + 1f);
                col = Color.Lerp(col, IVein, Sm(0.55f, 0.75f, mott) * 0.12f);
                float vein = Mathf.Abs(Sdf.Fbm(p.x * 4.6f + 11f, p.y * 4.6f) - 0.5f);
                col = Color.Lerp(col, IVein, Sat01(1f - vein / 0.018f) * 0.22f);
                return Color.Lerp(col, Color.Lerp(IRim, ILow, 0.5f), Sm(-0.03f, 0f, d) * 0.45f);
            });
            // мокрый блик и шрамы
            c.Fill(R(-0.34f, 0.42f, -0.04f, 0.8f), p => Sdf.Ellipse(p, new Vector2(-0.18f, 0.6f), 0.055f, 0.16f, -25f), IWet.WithAlpha(0.4f), 0.03f);
            Line(c, new[] { new Vector2(0.18f, 0.66f), new Vector2(0.24f, 0.58f), new Vector2(0.22f, 0.5f) }, 0.006f, IScar.WithAlpha(0.8f));
            Line(c, new[] { new Vector2(-0.27f, 0.08f), new Vector2(-0.19f, 0.02f) }, 0.005f, IScar.WithAlpha(0.7f));
            // складка-хмурь: внутренние концы ниже
            Line(c, new[] { new Vector2(-0.28f, 0.44f), new Vector2(-0.06f, 0.36f) }, 0.013f, IVein.WithAlpha(0.8f));
            Line(c, new[] { new Vector2(0.28f, 0.45f), new Vector2(0.06f, 0.36f) }, 0.013f, IVein.WithAlpha(0.8f));
            InkEyes(c, new Vector2(0f, 0.29f), 0.92f);
            // клюв
            c.Fill(R(-0.06f, 0.0f, 0.06f, 0.18f), p => Sdf.Triangle(p, new Vector2(-0.04f, 0.16f), new Vector2(0.04f, 0.16f), new Vector2(0.01f, 0.03f)), IBeak);
        }

        /// <summary>«Чернила»: капля чернил морока с подтёками, а в ней — его два глаза.</summary>
        static void InkDrop(MiniBossCanvas c)
        {
            var o = new Vector2(0f, 0.12f);
            Func<Vector2, float> blob = p =>
            {
                Vector2 q = p - o;
                float a = Mathf.Atan2(q.y, q.x);
                float r = 0.46f * (1f + 0.08f * Mathf.Sin(3f * a + 1f) + 0.05f * Mathf.Sin(5f * a + 2f));
                float d = q.magnitude - r;
                float[] dx = { -0.24f, -0.06f, 0.13f, 0.28f };
                float[] dl = { 0.58f, 0.76f, 0.66f, 0.52f };
                for (int i = 0; i < dx.Length; i++)
                {
                    Vector2 s0 = new Vector2(dx[i], -0.14f), s1 = new Vector2(dx[i] * 1.1f, -0.14f - dl[i] + 0.12f);
                    d = Sdf.SMin(d, Sdf.Taper(p, s0, s1, 0.09f, 0.04f), 0.08f);
                    d = Sdf.SMin(d, Sdf.Circle(p, s1 + new Vector2(0f, -0.02f), 0.055f), 0.03f);
                }
                return d;
            };
            c.Fill(R(-0.58f, -0.86f, 0.58f, 0.68f), blob, (p, d) =>
            {
                float rad = Sat01((p - o).magnitude / 0.5f);
                Color col = Color.Lerp(IInkEdge, IInkMid, Sm(0.25f, 0.85f, rad));
                col = Color.Lerp(col, IInk, Sm(0.35f, 0f, rad) * 0.6f);
                // лиловый отлив к краю — как у чернильных шаров босса, без свечения
                return Color.Lerp(col, IInkRim, Sm(-0.06f, -0.01f, d) * 0.7f);
            });
            Dot(c, new Vector2(-0.48f, -0.6f), 0.042f, IInkEdge);
            Dot(c, new Vector2(0.44f, -0.68f), 0.034f, IInkEdge);
            c.Fill(R(-0.4f, 0.2f, -0.02f, 0.56f), p => Sdf.Ellipse(p, new Vector2(-0.2f, 0.38f), 0.05f, 0.12f, -40f), IWet.WithAlpha(0.3f), 0.03f);
            InkEyes(c, new Vector2(0f, 0.14f), 0.85f);
        }

        // ============================================================ ПИРАНЬЯ
        // SpriteFactorySwarm: спина 35434D, тело 7C8D96, брюхо C4451C (приглушено),
        // плавники 1E272E, пасть 4A0C15, зубы F6F2E4 (здесь кость), глаз FF5A2B (тускло).

        static readonly Color PBack = Palette.Hex("56646E"), PBody = Palette.Hex("94A4AE"), PBelly = Palette.Hex("A8522F"),
                              PFin = Palette.Hex("5E6E7A"), PMaw = Palette.Hex("3E1018"), PTooth = Palette.Hex("C4BCAE"),
                              PEye = Palette.Hex("B04A30"), PDark = Palette.Hex("1E272E");

        /// <summary>Пиранья: высокое тело с горбом, рыжее брюхо, распахнутая пасть с пилой зубов, злая бровь.</summary>
        static void PiranhaFish(MiniBossCanvas c)
        {
            var f = MiniBossPose.Piranha;
            // хвостовая вилка с выкушенной выемкой
            FillL(c, f, LR(-0.92f, -0.36f, -0.5f, 0.4f), q => Mathf.Max(
                      Mathf.Min(Sdf.Triangle(q, new Vector2(-0.52f, 0.04f), new Vector2(-0.68f, 0f), new Vector2(-0.88f, 0.34f)),
                                Sdf.Triangle(q, new Vector2(-0.52f, -0.04f), new Vector2(-0.68f, 0f), new Vector2(-0.86f, -0.3f))),
                      -Sdf.Circle(q, new Vector2(-0.8f, 0.2f), 0.05f)), PFin);
            // спинные шипы
            for (int i = 0; i < 3; i++)
            {
                float u = -0.12f - 0.12f * i;
                TriL(c, f, new Vector2(u + 0.06f, 0.36f - 0.04f * i), new Vector2(u - 0.06f, 0.33f - 0.04f * i),
                     new Vector2(u - 0.1f, 0.52f - 0.05f * i), PFin);
            }
            TriL(c, f, new Vector2(-0.02f, -0.38f), new Vector2(-0.16f, -0.36f), new Vector2(-0.16f, -0.52f), PFin);
            // тело: высокий диск с горбом
            FillL(c, f, LR(-0.66f, -0.5f, 0.62f, 0.56f), q => Sdf.SMin(Sdf.Ellipse(q, new Vector2(-0.04f, -0.02f), 0.6f, 0.44f),
                                                                     Sdf.Ellipse(q, new Vector2(0.06f, 0.14f), 0.3f, 0.36f), 0.1f),
                  (q, d) =>
                  {
                      Color col = q.y > 0.12f ? Color.Lerp(PBody, PBack, Sm(0.12f, 0.36f, q.y)) : Color.Lerp(PBelly, PBody, Sm(-0.14f, 0.02f, q.y));
                      col = Color.Lerp(col, PDark, Sm(-0.04f, 0f, d) * 0.35f);
                      for (int i = 0; i < 7; i++)
                      {
                          float a = i * 2.4f;
                          var s = new Vector2(-0.1f + Mathf.Cos(a) * 0.3f, 0.12f + Mathf.Sin(a) * 0.14f);
                          if ((q - s).sqrMagnitude < 0.03f * 0.03f) col = Color.Lerp(col, PBack, 0.5f);
                      }
                      return col;
                  });
            // жаберная дуга, шрам
            LineL(c, f, new[] { new Vector2(0.16f, 0.22f), new Vector2(0.1f, 0.06f), new Vector2(0.13f, -0.14f) }, 0.009f, PDark);
            LineL(c, f, new[] { new Vector2(-0.3f, -0.1f), new Vector2(-0.2f, 0.06f) }, 0.006f, PBody.WithAlpha(0.9f));
            // пасть с пилой зубов
            Vector2 m0 = new Vector2(0.12f, -0.04f), m1 = new Vector2(0.62f, 0.13f), m2 = new Vector2(0.6f, -0.26f);
            FillL(c, f, LR(0.1f, -0.3f, 0.66f, 0.16f), q => Sdf.Triangle(q, m0, m1, m2), PMaw);
            for (int i = 0; i < 5; i++)
            {
                float k = (i + 0.5f) / 5f;
                Vector2 up = Vector2.Lerp(new Vector2(0.18f, -0.02f), m1, k), dn = Vector2.Lerp(new Vector2(0.18f, -0.07f), m2, k);
                TriL(c, f, up + new Vector2(-0.035f, 0.01f), up + new Vector2(0.035f, 0.01f), up + new Vector2(0.0f, -0.06f), PTooth);
                TriL(c, f, dn + new Vector2(-0.035f, -0.01f), dn + new Vector2(0.035f, -0.01f), dn + new Vector2(0.0f, 0.06f), PTooth);
            }
            // глаз и злая бровь
            var eye = f.ToW(new Vector2(0.3f, 0.18f));
            SlitEye(c, eye, 0.055f, 0.055f, PDark, PEye, Palette.Hex("6A2418"), PDark, vertical: false);
            LineL(c, f, new[] { new Vector2(0.2f, 0.28f), new Vector2(0.4f, 0.2f) }, 0.022f, PDark);
        }

        /// <summary>
        /// «Челюсть»: голова пираньи разинула пасть вверх — слева толстая верхняя челюсть
        /// с глазом и бровью, справа нижняя с рыжим подбородком, по изнанке — пила зубов.
        /// </summary>
        static void PiranhaJaws(MiniBossCanvas c)
        {
            var mawC = new Vector2(0.0f, -0.12f);
            c.Fill(R(-0.3f, -0.62f, 0.32f, 0.36f), p => Sdf.Ellipse(p, mawC, 0.26f, 0.42f), (p, d) =>
                Color.Lerp(PMaw, Palette.Hex("6A2830"), Sm(-0.5f, 0.2f, p.y) * 0.6f));
            // спинные шипы за головой
            for (int i = 0; i < 3; i++)
            {
                float y = -0.36f + 0.16f * i;
                Vector2 a = new Vector2(-0.56f, y - 0.05f), b = new Vector2(-0.54f, y + 0.06f), tip = new Vector2(-0.72f, y + 0.02f);
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), PFin);
            }
            var low = Bez3(new Vector2(0.06f, -0.66f), new Vector2(0.42f, -0.52f), new Vector2(0.54f, 0.0f), new Vector2(0.26f, 0.6f), 9);
            var up = Bez3(new Vector2(0.02f, -0.68f), new Vector2(-0.46f, -0.52f), new Vector2(-0.52f, 0.06f), new Vector2(-0.16f, 0.74f), 9);
            for (int jaw = 0; jaw < 2; jaw++)
            {
                var pts = jaw == 0 ? low : up;
                bool lower = jaw == 0;
                float r0 = lower ? 0.22f : 0.32f, r1 = lower ? 0.065f : 0.075f;
                c.Fill(Sdf.Around(pts, r0 + 0.02f), p => Sdf.Chain(p, pts, r0, r1), (p, d) =>
                {
                    // снаружи — по ту сторону от пасти: у нижней челюсти рыжий подбородок, у верхней тёмная спина
                    float outer = Sm(0.02f, 0.12f, (p - mawC).magnitude - (lower ? 0.36f : 0.4f));
                    Color col = lower ? Color.Lerp(PBody, PBelly, Mathf.Max(outer, Sm(-0.3f, -0.5f, p.y)))
                                      : Color.Lerp(PBody, PBack, outer * 0.5f);
                    return Vol(PDark, col, Color.Lerp(col, PBody, 0.5f), new Vector2(p.x * 1.4f, p.y), d, 0.04f, 0.4f);
                });
                // пила зубов по внутренней кромке; у кончика — клыки крупнее
                for (int i = 2; i < pts.Length - 1; i++)
                {
                    Vector2 dir = (pts[i + 1] - pts[i - 1]).normalized;
                    Vector2 inner = new Vector2(-dir.y, dir.x);
                    if (Vector2.Dot(inner, mawC - pts[i]) < 0f) inner = -inner;
                    float w = Mathf.Lerp(r0, r1, (float)i / (pts.Length - 1));
                    float tl = i >= pts.Length - 3 ? 0.12f : 0.075f;
                    Vector2 bse = pts[i] + inner * w * 0.82f;
                    Vector2 a = bse + dir * 0.033f, b = bse - dir * 0.033f, tip = bse + inner * tl - dir * 0.015f;
                    c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), PTooth);
                }
            }
            // жаберная дуга, пятна, глаз и злая бровь — на верхней челюсти
            Line(c, new[] { new Vector2(-0.18f, -0.58f), new Vector2(-0.3f, -0.48f), new Vector2(-0.44f, -0.46f) }, 0.009f, PDark);
            Dot(c, new Vector2(-0.46f, -0.12f), 0.03f, PBack.WithAlpha(0.7f));
            Dot(c, new Vector2(-0.4f, 0.14f), 0.026f, PBack.WithAlpha(0.7f));
            var eye = new Vector2(-0.33f, -0.24f);
            SlitEye(c, eye, 0.065f, 0.065f, PDark, PEye, Palette.Hex("6A2418"), PDark, vertical: false);
            Line(c, new[] { new Vector2(-0.46f, -0.14f), new Vector2(-0.24f, -0.17f) }, 0.022f, PDark);
        }

        // ============================================================ ЧЕРЕПАХА
        // SpriteFactorySwarm: панцирь 5E7F3E, тёмный 374E26, кожа 8AA65E, глаз FFB13D
        // (тускло); камень снаряда 7D8B93.

        static readonly Color TShell = Palette.Hex("5E7F3E"), TShellDk = Palette.Hex("374E26"), TSkin = Palette.Hex("7E9858"),
                              TSkinDk = Palette.Hex("4E6436"), TEye = Palette.Hex("C08A38"), TRock = Palette.Hex("6E7A80"),
                              TRockDk = Palette.Hex("434C52"), TRockLt = Palette.Hex("8A969C");

        /// <summary>Ласта: корень и широкое весло на конце.</summary>
        static void Flipper(MiniBossCanvas c, Vector2 a, Vector2 b, float r0, float r1)
        {
            Vector2 dir = b - a;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 pc = Vector2.Lerp(a, b, 0.62f);
            float len = dir.magnitude;
            c.Fill(Sdf.Around(a, b, Mathf.Max(r0, len * 0.4f) + 0.03f),
                   p => Sdf.SMin(Sdf.Taper(p, a, b, r0, r1), Sdf.Ellipse(p, pc, len * 0.5f, r0 * 1.15f, ang), 0.04f), (p, d) =>
            {
                Color col = Color.Lerp(TSkin, TSkinDk, Sm(-0.03f, 0f, d) * 0.7f);
                float spot = Sdf.Noise(p.x * 18f, p.y * 18f);
                return Color.Lerp(col, TSkinDk, Sm(0.6f, 0.8f, spot) * 0.5f);
            });
        }

        /// <summary>Черепашка сверху: панцирь со щитками и шипами по краю, четыре ласты, голова с клювом.</summary>
        static void TurtleTop(MiniBossCanvas c)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                Flipper(c, new Vector2(s * 0.16f, 0.2f), new Vector2(s * 0.5f, 0.42f), 0.1f, 0.055f);
                Flipper(c, new Vector2(s * 0.14f, -0.34f), new Vector2(s * 0.36f, -0.56f), 0.08f, 0.045f);
            }
            c.Fill(R(-0.06f, -0.74f, 0.06f, -0.5f), p => Sdf.Triangle(p, new Vector2(-0.05f, -0.5f), new Vector2(0.05f, -0.5f), new Vector2(0f, -0.7f)), TSkinDk);
            // голова с клювом и щелями глаз
            Vector2 hc = new Vector2(0f, 0.55f);
            c.Fill(R(-0.18f, 0.36f, 0.18f, 0.78f), p => Sdf.SMin(Sdf.Circle(p, hc, 0.13f), Sdf.Capsule(p, new Vector2(0f, 0.36f), hc, 0.09f), 0.05f),
                   (p, d) => Vol(TSkinDk, TSkin, Color.Lerp(TSkin, Palette.Hex("A2B47A"), 0.5f), (p - hc) / 0.13f, d, 0.03f, 0.5f));
            c.Fill(R(-0.08f, 0.6f, 0.08f, 0.76f), p => Sdf.Triangle(p, new Vector2(-0.055f, 0.62f), new Vector2(0.055f, 0.62f), new Vector2(0f, 0.74f)), TSkinDk);
            for (int s = -1; s <= 1; s += 2)
            {
                var e = new Vector2(s * 0.065f, 0.57f);
                c.Fill(Sdf.Box(e.x, e.y, 0.05f, 0.03f), p => Sdf.Ellipse(p, e, 0.038f, 0.018f, s * 15f), TEye);
                c.Fill(Sdf.Box(e.x, e.y, 0.05f, 0.03f), p => Sdf.Ellipse(p, e, 0.02f, 0.008f, s * 15f), PDark);
            }

            // шипы по краю панциря (кроме передней кромки у головы)
            var sc = new Vector2(0f, -0.04f);
            const float rx = 0.4f, ry = 0.49f;
            for (int k = 0; k < 10; k++)
            {
                float a = (110f + k * 32f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 e0 = sc + new Vector2(Mathf.Cos(a - 0.12f) * rx, Mathf.Sin(a - 0.12f) * ry) * 0.94f;
                Vector2 e1 = sc + new Vector2(Mathf.Cos(a + 0.12f) * rx, Mathf.Sin(a + 0.12f) * ry) * 0.94f;
                Vector2 tip = sc + new Vector2(dir.x * rx, dir.y * ry) * 1.2f;
                c.Fill(Sdf.Around(new[] { e0, e1, tip }, 0.02f), p => Sdf.Triangle(p, e0, e1, tip), TShellDk);
            }
            c.Fill(R(-0.44f, -0.56f, 0.44f, 0.48f), p => Sdf.Ellipse(p, sc, rx, ry), (p, d) =>
            {
                var n = new Vector2((p.x - sc.x) / rx, (p.y - sc.y) / ry);
                float rad = n.magnitude;
                Color col = Vol(TShellDk, TShell, Color.Lerp(TShell, Palette.Hex("8AA05E"), 0.6f), n, d, 0.06f, 0.6f);
                // щитки: кольцевой шов и шесть лучевых, плюс хребет
                float ring = Mathf.Abs(rad - 0.48f);
                float ang = Mathf.Atan2(n.y, n.x) / (Mathf.PI * 2f) * 6f;
                float ray = Mathf.Abs(Mathf.Repeat(ang + 0.25f, 1f) - 0.5f) * 2f;
                float seam = Mathf.Min(ring / 0.03f, rad > 0.48f ? (1f - ray) * rad / 0.05f : 99f);
                if (rad < 0.48f) seam = Mathf.Min(seam, Mathf.Abs(n.x) / 0.05f);
                col = Color.Lerp(col, TShellDk, Sat01(1f - seam) * 0.75f);
                float stone = Sdf.Fbm(p.x * 7f, p.y * 7f);
                return Color.Lerp(col, TShellDk, Sm(0.55f, 0.8f, stone) * 0.3f);
            });
            Line(c, new[] { new Vector2(0.16f, 0.2f), new Vector2(0.22f, 0.1f), new Vector2(0.19f, 0.0f) }, 0.006f, TShellDk);
            Line(c, new[] { new Vector2(-0.2f, -0.24f), new Vector2(-0.12f, -0.3f), new Vector2(-0.14f, -0.38f) }, 0.006f, TShellDk);
        }

        /// <summary>«Валун»: камень, обросший щитками панциря: грани, швы щитков, мох и каменные шипы.</summary>
        static void TurtleBoulder(MiniBossCanvas c)
        {
            var o = new Vector2(0f, -0.04f);
            float[] sa = { 78f, 112f, 40f, 4f, 150f };
            foreach (float deg in sa)
            {
                float a = deg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), side = new Vector2(-dir.y, dir.x);
                Vector2 e0 = o + dir * 0.5f + side * 0.1f, e1 = o + dir * 0.5f - side * 0.1f, tip = o + dir * 0.86f;
                c.Fill(Sdf.Around(new[] { e0, e1, tip }, 0.02f), p => Sdf.Triangle(p, e0, e1, tip), TRockDk);
            }
            // щитки: несколько крупных многоугольников (ячейки Вороного), как на панцире
            Vector2[] seeds = { new Vector2(0.14f, 0.24f), new Vector2(0.4f, 0.04f), new Vector2(0.1f, -0.06f),
                                new Vector2(0.32f, -0.3f), new Vector2(-0.12f, 0.34f), new Vector2(0.38f, 0.34f), new Vector2(-0.08f, 0.08f) };
            // семь неровных граней: многоугольник из полуплоскостей, шире, чем выше
            float[] faceA = { 0.2f, 1.05f, 1.75f, 2.5f, 3.4f, 4.3f, 5.3f };
            float[] faceR = { 0.6f, 0.5f, 0.56f, 0.62f, 0.5f, 0.54f, 0.46f };
            Func<Vector2, float> rock = p =>
            {
                Vector2 q = p - o;
                q.y /= 0.86f;
                float d = -9f;
                for (int i = 0; i < faceA.Length; i++)
                    d = Mathf.Max(d, Vector2.Dot(q, new Vector2(Mathf.Cos(faceA[i]), Mathf.Sin(faceA[i]))) - faceR[i]);
                return d * 0.9f;
            };
            c.Fill(R(-0.74f, -0.76f, 0.74f, 0.7f), rock, (p, d) =>
            {
                Vector2 q = p - o;
                float a = Mathf.Atan2(q.y, q.x);
                float facet = Mathf.Floor((a + Mathf.PI) / (Mathf.PI * 2f / 7f));
                float shade = 0.82f + 0.18f * Sdf.Hash(facet, 3f);
                Color col = Vol(TRockDk, TRock, TRockLt, q / 0.62f, d, 0.06f, 0.6f) * shade;
                col.a = 1f;
                float d1 = 9f, d2 = 9f;
                foreach (var s in seeds)
                {
                    float dd = (q - s).magnitude;
                    if (dd < d1) { d2 = d1; d1 = dd; } else if (dd < d2) d2 = dd;
                }
                float plate = Sm(-0.3f, 0.0f, q.x * 0.8f + q.y) * Sm(0.6f, 0.46f, q.magnitude);
                Color pc = Color.Lerp(TShell, Color.Lerp(TShell, Palette.Hex("8AA05E"), 0.5f), Sm(0.2f, 0.0f, d1));
                pc = Color.Lerp(pc, TShellDk, Sm(0.035f, 0.01f, d2 - d1));
                col = Color.Lerp(col, pc, plate * 0.9f);
                float moss = Sdf.Fbm(p.x * 9f + 5f, p.y * 9f);
                return Color.Lerp(col, TShell, Sm(0.62f, 0.76f, moss) * 0.4f * (1f - plate));
            });
            Line(c, new[] { new Vector2(-0.36f, 0.1f), new Vector2(-0.24f, 0.0f), new Vector2(-0.26f, -0.14f), new Vector2(-0.12f, -0.26f) }, 0.008f, TRockDk);
            Line(c, new[] { new Vector2(0.1f, -0.42f), new Vector2(0.2f, -0.32f), new Vector2(0.34f, -0.34f) }, 0.007f, TRockDk);
        }

        // ============================================================ АКУЛА
        // SpriteFactoryBosses: спина 222E38, тело 4E6069, брюхо E4E9EA (здесь светло-серое,
        // без белого), плавники 27333C, пасть 5B1220, зубы FBF7EC (кость), глаз 101418.

        static readonly Color KBack = Palette.Hex("3C4C58"), KBody = Palette.Hex("5C6E76"), KBelly = Palette.Hex("AEB8BE"),
                              KFin = Palette.Hex("465866"), KMaw = Palette.Hex("4A1420"), KTooth = Palette.Hex("C4BCAE"),
                              KEye = Palette.Hex("101418"), KEdge = Palette.Hex("5A6C78"), KWater = Palette.Hex("6E8A9A");

        /// <summary>Акулёнок боком, носом вверх (MiniBossPose): тёмная спина, светлое брюхо, плавник, приоткрытая пасть с зубами.</summary>
        static void SharkFish(MiniBossCanvas c)
        {
            var f = MiniBossPose.Shark;
            TriL(c, f, new Vector2(-0.62f, 0.03f), new Vector2(-0.78f, 0f), new Vector2(-0.92f, 0.38f), KFin);
            TriL(c, f, new Vector2(-0.62f, -0.02f), new Vector2(-0.76f, 0f), new Vector2(-0.88f, -0.24f), KFin);
            FillL(c, f, LR(-0.26f, 0.1f, 0.22f, 0.5f), q => Mathf.Max(
                      Sdf.Triangle(q, new Vector2(0.18f, 0.16f), new Vector2(-0.24f, 0.14f), new Vector2(-0.06f, 0.48f)),
                      -Sdf.Circle(q, new Vector2(-0.3f, 0.42f), 0.16f)),
                  (q, d) => Color.Lerp(KFin, KEdge, Sm(-0.04f, 0f, d) * Sm(-0.05f, 0.05f, q.x - (-0.06f)) * 0.6f));
            FillL(c, f, LR(-0.74f, -0.3f, 0.92f, 0.3f), q => Mathf.Min(
                      Sdf.SMin(Sdf.Ellipse(q, new Vector2(0.06f, 0f), 0.6f, 0.24f), Sdf.Ellipse(q, new Vector2(0.52f, 0.01f), 0.38f, 0.16f), 0.08f),
                      Sdf.Taper(q, new Vector2(-0.34f, 0f), new Vector2(-0.72f, 0.02f), 0.18f, 0.05f)),
                  (q, d) =>
                  {
                      float belly = Sm(-0.05f, -0.12f, q.y);
                      Color col = Color.Lerp(Color.Lerp(KBody, KBack, Sm(-0.02f, 0.14f, q.y)), KBelly, belly);
                      float seam = Sat01(1f - Mathf.Abs(q.y + 0.085f) / 0.01f);
                      col = Color.Lerp(col, KBelly, seam * 0.3f);
                      return Color.Lerp(col, KEye, Sm(-0.03f, 0f, d) * 0.25f);
                  });
            TriL(c, f, new Vector2(0.26f, -0.12f), new Vector2(0.08f, -0.13f), new Vector2(-0.08f, -0.42f), KFin);
            // жаберные щели, шрамы
            for (int i = 0; i < 4; i++)
            {
                float u = 0.22f + 0.045f * i;
                LineL(c, f, new[] { new Vector2(u, 0.08f), new Vector2(u - 0.03f, -0.06f) }, 0.006f, KEye.WithAlpha(0.8f));
            }
            LineL(c, f, new[] { new Vector2(-0.12f, 0.04f), new Vector2(-0.04f, 0.12f) }, 0.005f, KEdge);
            // приоткрытая пасть с зубами
            Vector2 m0 = new Vector2(0.42f, -0.08f), m1 = new Vector2(0.82f, -0.07f), m2 = new Vector2(0.66f, -0.18f);
            FillL(c, f, LR(0.38f, -0.22f, 0.86f, -0.03f), q => Sdf.Triangle(q, m0, m1, m2), KMaw);
            for (int i = 0; i < 5; i++)
            {
                float k = (i + 0.5f) / 5f;
                Vector2 up = Vector2.Lerp(m0, m1, k) + new Vector2(0f, 0.004f);
                TriL(c, f, up + new Vector2(-0.025f, 0f), up + new Vector2(0.025f, 0f), up + new Vector2(0f, -0.045f), KTooth);
            }
            for (int i = 0; i < 3; i++)
            {
                float k = (i + 0.5f) / 3f;
                Vector2 dn = Vector2.Lerp(m0, m2, k) + new Vector2(0.02f, 0f);
                TriL(c, f, dn + new Vector2(-0.022f, -0.004f), dn + new Vector2(0.022f, -0.004f), dn + new Vector2(0.01f, 0.038f), KTooth);
            }
            var eye = f.ToW(new Vector2(0.54f, 0.06f));
            Dot(c, eye, 0.034f, KEdge);
            Dot(c, eye, 0.026f, KEye);
        }

        /// <summary>«Плавник»: спинной плавник режет воду — над куполом спины, тающим книзу, с приглушёнными бурунами.</summary>
        static void SharkFin(MiniBossCanvas c)
        {
            // спина — большой купол снизу: видна только верхняя дуга, ниже растворяется в воде
            var bc = new Vector2(0f, -1.5f);
            c.Fill(R(-0.86f, -0.96f, 0.86f, -0.4f), p => Sdf.Circle(p, bc, 1.06f), (p, d) =>
            {
                Color col = Color.Lerp(KBody, KBack, Sm(-0.8f, -0.5f, p.y));
                col = Color.Lerp(col, KEdge, Sm(-0.05f, -0.01f, d) * 0.6f);
                // и не выходит за круг игрока
                return col.WithAlpha(Sm(-0.95f, -0.62f, p.y) * Sm(0.86f, 0.55f, Mathf.Abs(p.x)) * Sm(0.9f, 0.8f, p.magnitude));
            });
            Func<Vector2, float> fin = p => Mathf.Max(
                Sdf.SMin(Sdf.Triangle(p, new Vector2(-0.44f, -0.5f), new Vector2(0.36f, -0.5f), new Vector2(0.06f, 0.84f)),
                         Sdf.Circle(p, new Vector2(-0.2f, -0.14f), 0.2f), 0.12f),
                Mathf.Max(-Sdf.Circle(p, new Vector2(0.62f, 0.28f), 0.44f), -Sdf.Circle(p, new Vector2(0.27f, 0.12f), 0.055f)));
            c.Fill(R(-0.5f, -0.54f, 0.4f, 0.88f), fin, (p, d) =>
            {
                Color col = Color.Lerp(KFin, KBack, 0.3f);
                // светлая передняя кромка — плавник читается на тёмной воде
                float lead = Sm(0.1f, -0.25f, p.x - (-0.44f + (p.y + 0.5f) * 0.38f));
                col = Color.Lerp(col, KEdge, Sm(-0.09f, 0f, d) * lead * 0.85f);
                col = Color.Lerp(col, KEdge, Sat01(Vector2.Dot((p - new Vector2(-0.1f, 0f)).normalized, MiniLightDir)) * 0.3f);
                return Color.Lerp(col, KBody, Sm(-0.3f, -0.5f, p.y) * 0.5f);
            });
            Line(c, new[] { new Vector2(-0.12f, 0.1f), new Vector2(-0.02f, 0.26f) }, 0.006f, KEdge);
            Line(c, new[] { new Vector2(0.06f, -0.2f), new Vector2(0.13f, -0.06f) }, 0.005f, KEdge.WithAlpha(0.8f));
            // буруны вдоль спины по обе стороны плавника
            for (int s = -1; s <= 1; s += 2)
            {
                Line(c, new[] { new Vector2(s * 0.4f, -0.46f), new Vector2(s * 0.52f, -0.43f), new Vector2(s * 0.62f, -0.47f) }, 0.012f, KWater.WithAlpha(0.6f), 0.008f);
                Line(c, new[] { new Vector2(s * 0.48f, -0.54f), new Vector2(s * 0.58f, -0.53f) }, 0.009f, KWater.WithAlpha(0.45f), 0.006f);
            }
            Dot(c, new Vector2(-0.6f, -0.32f), 0.022f, KWater.WithAlpha(0.55f));
            Dot(c, new Vector2(0.58f, -0.3f), 0.018f, KWater.WithAlpha(0.5f));
        }

        // ============================================================ СКАТ
        // SpriteFactoryManta: спина 04070B (здесь чуть светлее), холодная кромка 22454F,
        // пятна 16252E, глаза FF4A3A (тускло), жало E8E2D0 (кость).

        static readonly Color MBack = Palette.Hex("222E3A"), MEdge = Palette.Hex("467884"), MSpot = Palette.Hex("324450"),
                              MEye = Palette.Hex("B04030"), MSpine = Palette.Hex("B0AA9A"), MSpineDk = Palette.Hex("6E6A5E"),
                              MDeep = Palette.Hex("0A1016");

        /// <summary>Скатик сверху: крылья с рваной задней кромкой, рожки, два тусклых красных глаза, хвост-хлыст с жалом.</summary>
        static void MantaRay(MiniBossCanvas c)
        {
            Vector2 t0 = new Vector2(0f, -0.1f), t1 = new Vector2(0.02f, -0.86f);
            c.Fill(Sdf.Around(t0, t1, 0.06f), p => Sdf.Taper(p, t0, t1, 0.035f, 0.01f), Color.Lerp(MBack, MEdge, 0.35f));
            c.Fill(R(-0.06f, -0.38f, 0.06f, -0.18f), p => Sdf.Triangle(p, new Vector2(-0.03f, -0.2f), new Vector2(0.03f, -0.2f), new Vector2(0.006f, -0.36f)), MSpine);

            Func<Vector2, float> wings = p =>
            {
                float a = Mathf.Abs(p.x) / 0.86f;
                if (a >= 1f) return Mathf.Abs(p.x) - 0.86f + 0.01f;
                float yc = 0.12f - 0.24f * a * a;
                float half = 0.3f * Mathf.Pow(1f - a * a, 0.55f);
                float lead = yc + half * 0.85f;
                float trail = yc - half * (1.15f - 0.45f * Mathf.Sin(Mathf.PI * a)) * (1f - 0.12f * Mathf.Abs(Mathf.Sin(30f * a)));
                return Mathf.Max(Mathf.Max(p.y - lead, trail - p.y) * 0.8f, Mathf.Abs(p.x) - 0.86f);
            };
            Func<Vector2, float> head = p => Mathf.Min(Sdf.Box(p, new Vector2(0f, 0.24f), new Vector2(0.15f, 0.2f), 0.06f),
                Mathf.Min(Sdf.Taper(p, new Vector2(-0.12f, 0.36f), new Vector2(-0.2f, 0.56f), 0.05f, 0.022f),
                          Sdf.Taper(p, new Vector2(0.12f, 0.36f), new Vector2(0.2f, 0.56f), 0.05f, 0.022f)));
            c.Fill(R(-0.9f, -0.42f, 0.9f, 0.6f), p => Mathf.Min(wings(p), head(p)), (p, d) =>
            {
                Color col = Color.Lerp(MBack, MDeep, Sm(0.2f, 0.8f, Mathf.Abs(p.x)) * 0.5f);
                for (int s = -1; s <= 1; s += 2)
                    for (int i = 0; i < 4; i++)
                    {
                        var sp = new Vector2(s * (0.24f + 0.15f * i), 0.06f - 0.06f * i);
                        float r = 0.065f - 0.01f * i;
                        if ((p - sp).sqrMagnitude < r * r) col = Color.Lerp(col, MSpot, 0.8f);
                    }
                // холодная кромка — тусклая, как у босса
                return Color.Lerp(col, MEdge, Sm(-0.095f, -0.01f, d) * 0.9f);
            });
            for (int i = 0; i < 5; i++) Dot(c, new Vector2(0f, 0.04f - 0.06f * i), 0.014f, MSpine.WithAlpha(0.75f));
            // глаза: тусклый красный и мягкий ореол — единственное, что светится
            for (int s = -1; s <= 1; s += 2)
            {
                var e = new Vector2(s * 0.08f, 0.34f);
                c.Fill(Sdf.Box(e.x, e.y, 0.1f, 0.1f), p => Sdf.Circle(p, e, 0.03f), MEye.WithAlpha(0.22f), 0.04f);
                c.Fill(Sdf.Box(e.x, e.y, 0.05f, 0.04f), p => Sdf.Ellipse(p, e, 0.034f, 0.02f), MEye);
            }
        }

        /// <summary>«Жало»: костяное зазубренное жало остриём вверх на тёмном хвосте-хлысте.</summary>
        static void MantaSting(MiniBossCanvas c)
        {
            var tail = Bez3(new Vector2(0f, -0.88f), new Vector2(0.16f, -0.62f), new Vector2(-0.1f, -0.36f), new Vector2(0f, -0.08f), 8);
            c.Fill(Sdf.Around(tail, 0.14f), p => Sdf.Chain(p, tail, 0.075f, 0.11f), (p, d) =>
            {
                Along(p, tail, out _, out float side);
                Color col = Color.Lerp(MBack, MDeep, side > 0f ? 0f : 0.5f);
                return Color.Lerp(col, MEdge, Sm(-0.04f, -0.006f, d) * (side > 0f ? 0.9f : 0.5f));
            });
            for (int i = 2; i < 7; i += 2)
            {
                Vector2 at = tail[i], dir = (tail[i + 1] - tail[i - 1]).normalized, side = new Vector2(-dir.y, dir.x);
                Vector2 a = at + side * 0.06f + dir * 0.03f, b = at + side * 0.06f - dir * 0.03f, tip = at + side * 0.12f + dir * 0.05f;
                c.Fill(Sdf.Around(new[] { a, b, tip }, 0.02f), p => Sdf.Triangle(p, a, b, tip), MSpineDk);
            }
            // жало: лист с зазубринами назад, гребень посередине
            c.Fill(R(-0.24f, -0.16f, 0.24f, 0.9f), p =>
            {
                float t = (p.y + 0.12f) / 1.0f;
                if (t < 0f) return -t * 1f + Mathf.Abs(p.x) - 0.06f;
                if (t > 1f) return (t - 1f) + Mathf.Abs(p.x);
                float w = 0.2f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 0.8f + 0.2f)), 0.8f) * (1f - 0.3f * t);
                float saw = 1f - 0.34f * Mathf.Repeat(p.y * 10f, 1f);
                return (Mathf.Abs(p.x) - w * saw) * 0.9f;
            }, (p, d) =>
            {
                float x = Mathf.Abs(p.x);
                Color col = Color.Lerp(MSpine, MSpineDk, Sm(0.02f, 0.12f, x));
                col = Color.Lerp(col, MSpineDk, Sat01(1f - Mathf.Abs(p.x) / 0.008f) * 0.6f);
                return Color.Lerp(col, MSpineDk, Sm(-0.012f, 0f, d) * 0.5f);
            });
        }
    }
}
