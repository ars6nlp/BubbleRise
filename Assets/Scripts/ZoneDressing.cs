using UnityEngine;

namespace BubbleRise
{
    /// <summary>
    /// Оформление локации: корка по кромке, крупное на кромке, мелочи на стене,
    /// светящиеся точки, тусклые силуэты в глубине прохода и ворота входа и
    /// выхода. Только вид — на шары, их места и столкновения не влияет.
    ///
    /// Случайность — своя (System.Random от зерна локации), UnityEngine.Random
    /// не трогается: поток угроз остаётся тем же, что был до оформления.
    ///
    /// Тон каждого рисунка считается от его среднего цвета (как в
    /// <see cref="WorldLayers"/>): яркость слоя и доля цвета воды локации —
    /// поэтому кораллы каньона, колонны города и трубки впадины сидят в своей
    /// воде, а не наклеены поверх.
    /// </summary>
    public static class ZoneDressing
    {
        /// <summary>Яркость и доля цвета воды по слоям оформления.</summary>
        const float EdgeBright = 0.78f, EdgeFog = 0.22f;
        const float DetailBright = 0.72f, DetailFog = 0.3f;
        const float CrustBright = 0.72f, CrustToTheme = 0.65f;
        const float BackBright = 0.32f, BackFog = 0.74f;
        const float GateBright = 0.7f, GateFog = 0.25f;
        const float ArchBright = 0.42f, ArchFog = 0.62f;

        /// <summary>Отступ ворот от торца локации, юнитов: там, где стена уже целиком в кадре.</summary>
        public const float GateInset = OceanZone.Taper + 2.5f;

        /// <summary>Самый узкий просвет между столбами ворот, юнитов: столбы не смыкаются над проходом.</summary>
        const float GateGap = 1.7f;

        public static void Dress(OceanZone zone, ZoneTheme theme, Playfield field, int seed)
        {
            var rng = new System.Random(seed * 7919 + 17);
            if (theme.Style == WallStyle.Ice) { DressIce(zone, theme, field, rng); return; }
            float h = zone.Height;
            float hw = field.HalfWidth;
            Color water = Color.Lerp(theme.WaterTop, theme.WaterBottom, 0.5f);
            bool high = Visuals.HighDetail;

            for (int s = 0; s < 2; s++)
            {
                bool right = s == 1;
                float sd = right ? 1f : -1f;

                // корка по кромке — внахлёст, по наклону края
                for (float y = -h * 0.5f + 0.4f; y < h * 0.5f - 0.4f; y += 0.55f + 0.2f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    float inner = zone.InnerEdge(t, right);
                    if (inner > hw + 0.6f) continue;
                    float slope = Slope(zone, t, right, h);
                    float thick = 0.5f + 0.18f * R(rng);
                    var info = SpriteFactory.World(theme.CrustPiece);
                    Color target = Color.Lerp(Mul(info.Avg, CrustBright), theme.Crust, CrustToTheme);
                    Color tint = WorldLayers.TintFor(target, info.Avg);
                    Vector2 at = new Vector2(sd * (inner + thick * 0.3f), y);
                    float angle = sd * (Mathf.PI * 0.5f - Mathf.Atan(slope));
                    zone.AddItem(Item(info, at, thick, angle, R(rng) < 0.5f, 0f, tint, OceanZone.FrontLayer, false));
                }

                // крупное на кромке — к тому, что уже поставила AddWallDecor
                for (float y = -h * 0.5f + OceanZone.Taper; y < h * 0.5f - OceanZone.Taper; y += 1.5f + 1.3f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    var piece = theme.Edge[rng.Next(theme.Edge.Length)];
                    EdgeFeature(zone, theme, piece, t, right, R(rng), (R(rng) - 0.3f) * 0.4f);
                }

                // мелочи на лице стены: ракушки, звёзды, обломки
                for (float y = -h * 0.5f + 1f; y < h * 0.5f - 1f; y += 0.5f + 0.7f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    float inner = zone.InnerEdge(t, right);
                    float r = R(rng), r2 = R(rng), r3 = R(rng), r4 = R(rng);
                    if (inner > hw - 0.1f) continue;
                    if (!high && r < 0.5f) continue;
                    var piece = theme.Details[(int)(r2 * theme.Details.Length) % theme.Details.Length];
                    WorldLayers.Size(piece, r3, out float ph, out float sw);
                    var info = SpriteFactory.World(piece);
                    Vector2 at = new Vector2(sd * (inner + 0.12f + r4 * 0.9f), y);
                    zone.AddItem(Item(info, at, ph, (r - 0.5f) * 0.8f, r3 < 0.5f, sw,
                                      Tone(info, DetailBright * (0.9f + 0.2f * r2), water, DetailFog),
                                      OceanZone.FrontLayer, false));
                }

                // светящиеся точки впадины: мягкие, без мерцания, «дышат» медленно
                if (theme.Glow)
                {
                    for (float y = -h * 0.5f + 2f; y < h * 0.5f - 2f; y += 1.1f + 1.2f * R(rng))
                    {
                        float t = (y + h * 0.5f) / h;
                        float inner = zone.InnerEdge(t, right);
                        float r = R(rng), r2 = R(rng);
                        if (inner > hw) continue;
                        var info = SpriteFactory.World(WorldPiece.Glow);
                        Color c = (r < 0.5f ? Palette.Hex("7FD8E8") : Palette.Hex("B48CF0")).WithAlpha(0.3f + 0.12f * r2);
                        Vector2 at = new Vector2(sd * (inner + 0.25f + r2 * 1.1f), y);
                        var item = Item(info, at, 0.32f + 0.3f * r, 0f, false, 0f, c, OceanZone.FrontLayer, false);
                        item.Breath = 0.1f;
                        zone.AddItem(item);
                    }
                }
            }

            // тусклые силуэты в глубине прохода — за стенами и за шарами
            for (float y = -h * 0.5f + 3f; y < h * 0.5f - 3f; y += 3.2f + 2.4f * R(rng))
            {
                float t = (y + h * 0.5f) / h;
                float passage = Mathf.Min(zone.Passage(t), hw);
                float r = R(rng), r2 = R(rng), r3 = R(rng);
                var piece = theme.Interior[(int)(r * theme.Interior.Length) % theme.Interior.Length];
                WorldLayers.Size(piece, r2, out float ph, out float sw);
                var info = SpriteFactory.World(piece);
                float side = r3 < 0.5f ? -1f : 1f;
                float x = side * Mathf.Lerp(0.35f, 1f, r2) * passage;
                // ближе к середине — тише: колонна остаётся спокойной (фон за угрозами
                // L ≤ 0.018 — ёж к нему ≥ 2.2:1 [ТЕСТ, z4c])
                float calm = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(Mathf.Abs(x) / Mathf.Max(0.5f, passage)));
                var back = Item(info, new Vector2(x, y), ph * 0.9f, 0f, side > 0f, sw,
                                Tone(info, BackBright * calm, water, BackFog), OceanZone.BackLayer, false);
                back.FadeBottom = true;     // плоский низ силуэта не режет проход
                zone.AddItem(back);
                if (piece == WorldPiece.WallWindow)
                {
                    // свет из окна: косой тусклый луч
                    var beam = SpriteFactory.World(WorldPiece.WindowBeam);
                    Color c = Palette.SafeLight(Color.Lerp(theme.Crust, Palette.Foam, 0.5f)).WithAlpha(0.06f);
                    zone.AddItem(Item(beam, new Vector2(x - side * 0.25f, y - ph * 0.2f), ph * 1.1f, side * 0.25f,
                                      false, 0f, c, OceanZone.BackLayer, false));
                }
            }

            Gate(zone, theme, field, -h * 0.5f + GateInset, water, rng);
            Gate(zone, theme, field, h * 0.5f - GateInset, water, rng);
        }

        /// <summary>
        /// Крупный рисунок на кромке: основание в стене, верх — в проход.
        /// Водоросли, трава, колонны и цепи стоят прямо, кораллы и актинии
        /// растут вбок-вверх, от стены.
        /// </summary>
        public static void EdgeFeature(OceanZone zone, ZoneTheme theme, WorldPiece piece, float t, bool right,
                                       float sizeK, float jitter)
        {
            float h = zone.Height;
            float y = -h * 0.5f + t * h;
            float inner = zone.InnerEdge(t, right);
            float sd = right ? 1f : -1f;
            WorldLayers.Size(piece, Mathf.Clamp01(sizeK), out float ph, out float sw);
            // круглые веера на кромке крупными читались как сетки-мячи [ТЕСТ]
            ph *= piece == WorldPiece.FanA || piece == WorldPiece.FanB ? 0.65f : 0.85f;
            var info = SpriteFactory.World(piece);
            float w = ph * info.Aspect;
            bool upright = Upright(piece);
            float lean = upright ? 0f : sd * (0.35f + 0.25f * Mathf.Repeat(t * 37.1f, 1f));
            // основание чуть в стене: на кромке ничего не висит в воде
            Vector2 at = new Vector2(sd * (inner + 0.1f + Mathf.Max(0f, jitter) * 0.5f + (upright ? w * 0.2f : 0f)), y);
            Color water = Color.Lerp(theme.WaterTop, theme.WaterBottom, 0.5f);
            float jit = 0.9f + 0.2f * Mathf.Repeat(t * 91.7f, 1f);
            zone.AddItem(Item(info, at, ph, lean, right, sw, Tone(info, EdgeBright * jit, water, EdgeFog),
                              OceanZone.FrontLayer, true));
        }

        /// <summary>
        /// Ворота: два столба по краям прохода и, если у темы есть, тусклая арка
        /// над проходом — знак «здесь начинается место». Водоросли вместо арки
        /// клонятся друг к другу.
        /// </summary>
        static void Gate(OceanZone zone, ZoneTheme theme, Playfield field, float y, Color water, System.Random rng)
        {
            float h = zone.Height;
            float t = Mathf.Clamp01((y + h * 0.5f) / h);
            float hw = field.HalfWidth;
            var post = SpriteFactory.World(theme.GatePost);
            WorldLayers.Size(theme.GatePost, 0.8f, out float ph, out float sw);
            ph *= 0.7f;
            float pw = ph * post.Aspect;
            float passage = Mathf.Clamp(zone.Passage(t), GateGap, Mathf.Max(GateGap, hw - pw * 0.2f));
            bool kelp = !theme.GateArch;
            for (int s = 0; s < 2; s++)
            {
                float sd = s == 0 ? -1f : 1f;
                float lean = kelp ? sd * 0.2f : 0f;
                // столб — за стеной, у самой кромки: стена закрывает его основание
                Vector2 at = new Vector2(sd * (passage + pw * 0.1f), y - ph * 0.45f);
                var item = Item(post, at, ph, lean, s == 1, sw, Tone(post, GateBright, water, GateFog),
                                OceanZone.BackLayer, true);
                item.FadeBottom = !kelp;   // водоросли качаются — у них низ не тает
                zone.AddItem(item);
            }
            if (!theme.GateArch) return;
            var arch = SpriteFactory.World(WorldPiece.ArchMid);
            float aw = 2f * passage + pw * 0.8f;
            float ah = aw / arch.Aspect;
            Vector2 c = new Vector2(0f, y + ph * 0.55f - ah * 0.4f);
            zone.AddItem(Item(arch, c, ah, 0f, R(rng) < 0.5f, 0f, Tone(arch, ArchBright, water, ArchFog),
                              OceanZone.BackLayer, false));
        }

        // ------------------------------------------------------------ лёд

        /// <summary>Крупное на ледяной кромке: кристаллы, колонны, валуны, вмёрзшая ламинария.</summary>
        public static readonly IcePiece[] IceEdge =
        {
            IcePiece.CrystalA, IcePiece.FrozenColumn, IcePiece.CrystalB, IcePiece.BoulderA,
            IcePiece.FrozenKelp, IcePiece.CrystalC, IcePiece.BoulderB, IcePiece.FrostRock,
        };

        static readonly IcePiece[] IceDrift = { IcePiece.FloeA, IcePiece.FloeB, IcePiece.FloeC };

        const float IceBright = 0.72f, IceFog = 0.2f;
        const float IceBackBright = 0.42f, IceBackFog = 0.7f;

        /// <summary>
        /// «Ледяной разлом»: иней по кромке, кристаллы и ледяные колонны, сосульки
        /// под кромкой, ледяные уступы, вмёрзшие рыбы, мягкие искры кристаллов,
        /// льдины в толще. Вход — ледяная арка с колоннами, выход — потолок из
        /// льдин с сосульками. Рисунки — из ледяного атласа.
        /// </summary>
        static void DressIce(OceanZone zone, ZoneTheme theme, Playfield field, System.Random rng)
        {
            float h = zone.Height;
            float hw = field.HalfWidth;
            Color water = Color.Lerp(theme.WaterTop, theme.WaterBottom, 0.5f);
            bool high = Visuals.HighDetail;

            for (int s = 0; s < 2; s++)
            {
                bool right = s == 1;
                float sd = right ? 1f : -1f;

                // иней по кромке — сугробы-полосы по наклону края
                for (float y = -h * 0.5f + 0.4f; y < h * 0.5f - 0.4f; y += 0.85f + 0.3f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    float inner = zone.InnerEdge(t, right);
                    if (inner > hw + 0.6f) continue;
                    float thick = 0.36f + 0.14f * R(rng);
                    var info = SpriteFactory.Ice(IcePiece.Drift);
                    float angle = sd * (Mathf.PI * 0.5f - Mathf.Atan(Slope(zone, t, right, h)));
                    zone.AddItem(Item(info, new Vector2(sd * (inner + thick * 0.3f), y), thick, angle, R(rng) < 0.5f, 0f,
                                      Tone(info, 0.7f, water, 0.2f), OceanZone.IceLayer, false));
                }

                // крупное — к тому, что уже поставила AddWallDecor
                for (float y = -h * 0.5f + OceanZone.Taper; y < h * 0.5f - OceanZone.Taper; y += 1.5f + 1.3f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    IceFeature(zone, theme, IceEdge[rng.Next(IceEdge.Length)], t, right, R(rng), (R(rng) - 0.3f) * 0.4f);
                }

                // сосульки: основание у кромки, висят вниз
                for (float y = -h * 0.5f + 2f; y < h * 0.5f - 1f; y += 1.1f + 1.4f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    float inner = zone.InnerEdge(t, right);
                    float r = R(rng), r2 = R(rng);
                    if (inner > hw) continue;
                    var info = SpriteFactory.Ice(r < 0.5f ? IcePiece.IcicleA : IcePiece.IcicleB);
                    var item = Item(info, new Vector2(sd * (inner + 0.12f + 0.25f * r2), y), 0.8f + 0.8f * r2, 0f, r < 0.25f, 0f,
                                    Tone(info, IceBright, water, IceFog), OceanZone.IceLayer, false);
                    item.PivotY = 1f;      // основание — вверху
                    zone.AddItem(item);
                }

                // вмёрзшие рыбы на лице стены
                for (float y = -h * 0.5f + 1.5f; y < h * 0.5f - 1.5f; y += 1.4f + 1.8f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    float inner = zone.InnerEdge(t, right);
                    float r = R(rng), r2 = R(rng);
                    if (inner > hw - 0.3f || (!high && r < 0.5f)) continue;
                    var info = SpriteFactory.Ice(IcePiece.IceFish);
                    Color c = Tone(info, 0.55f, water, 0.35f).WithAlpha(0.7f);
                    zone.AddItem(Item(info, new Vector2(sd * (inner + 0.4f + 1.1f * r2), y), 0.18f + 0.08f * r, (r - 0.5f) * 0.6f,
                                      r2 < 0.5f, 0f, c, OceanZone.IceLayer, false));
                }

                // искры в кристаллах: мягкие, без мерцания, дышат медленно
                for (float y = -h * 0.5f + 2f; y < h * 0.5f - 2f; y += 1.6f + 1.6f * R(rng))
                {
                    float t = (y + h * 0.5f) / h;
                    float inner = zone.InnerEdge(t, right);
                    float r = R(rng);
                    if (inner > hw) continue;
                    var info = SpriteFactory.World(WorldPiece.Glow);
                    var item = Item(info, new Vector2(sd * (inner + 0.2f + 0.9f * r), y), 0.3f + 0.25f * r, 0f, false, 0f,
                                    Palette.Hex("8FC8E0").WithAlpha(0.22f), OceanZone.FrontLayer, false);
                    item.Breath = 0.08f;
                    zone.AddItem(item);
                }
            }

            // льдины в толще — за стенами и шарами, тонут в синеве
            for (float y = -h * 0.5f + 4f; y < h * 0.5f - 4f; y += 3.5f + 2.5f * R(rng))
            {
                float t = (y + h * 0.5f) / h;
                float passage = Mathf.Min(zone.Passage(t), hw);
                float r = R(rng), r2 = R(rng);
                var info = SpriteFactory.Ice(IceDrift[rng.Next(IceDrift.Length)]);
                float side = r < 0.5f ? -1f : 1f;
                float x = side * Mathf.Lerp(0.3f, 0.9f, r2) * passage;
                float calm = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(Mathf.Abs(x) / Mathf.Max(0.5f, passage)));
                zone.AddItem(Item(info, new Vector2(x, y), 0.9f + 0.4f * r2, (r2 - 0.5f) * 0.2f, side > 0f, 0f,
                                  Tone(info, IceBackBright * calm, water, IceBackFog), OceanZone.IceBackLayer, false));
            }

            IceGate(zone, theme, field, -h * 0.5f + GateInset, water);
            IceCeiling(zone, theme, field, h * 0.5f - GateInset * 0.6f, water, rng);
        }

        /// <summary>Крупный ледяной рисунок на кромке: основание в стене, кристаллы растут вбок-вверх.</summary>
        public static void IceFeature(OceanZone zone, ZoneTheme theme, IcePiece piece, float t, bool right,
                                      float sizeK, float jitter)
        {
            float h = zone.Height;
            float y = -h * 0.5f + t * h;
            float inner = zone.InnerEdge(t, right);
            float sd = right ? 1f : -1f;
            IceSize(piece, Mathf.Clamp01(sizeK), out float ph, out float sw);
            var info = SpriteFactory.Ice(piece);
            float w = ph * info.Aspect;
            bool crystal = piece == IcePiece.CrystalA || piece == IcePiece.CrystalB || piece == IcePiece.CrystalC;
            float lean = crystal ? sd * (0.3f + 0.25f * Mathf.Repeat(t * 37.1f, 1f)) : 0f;
            Vector2 at = new Vector2(sd * (inner + 0.1f + Mathf.Max(0f, jitter) * 0.4f + (crystal ? 0f : w * 0.25f)), y);
            Color water = Color.Lerp(theme.WaterTop, theme.WaterBottom, 0.5f);
            float jit = 0.9f + 0.2f * Mathf.Repeat(t * 91.7f, 1f);
            zone.AddItem(Item(info, at, ph, lean, right, sw, Tone(info, IceBright * jit, water, IceFog),
                              OceanZone.IceLayer, true));
        }

        /// <summary>Вход в разлом: ледяная арка над проходом и две ледяные колонны.</summary>
        static void IceGate(OceanZone zone, ZoneTheme theme, Playfield field, float y, Color water)
        {
            float h = zone.Height;
            float t = Mathf.Clamp01((y + h * 0.5f) / h);
            float hw = field.HalfWidth;
            var post = SpriteFactory.Ice(IcePiece.FrozenColumn);
            float ph = 3.6f, pw = ph * post.Aspect;
            float passage = Mathf.Clamp(zone.Passage(t), GateGap, Mathf.Max(GateGap, hw - pw * 0.2f));
            for (int s = 0; s < 2; s++)
            {
                float sd = s == 0 ? -1f : 1f;
                var item = Item(post, new Vector2(sd * (passage + pw * 0.1f), y - ph * 0.45f), ph, 0f, s == 1, 0f,
                                Tone(post, 0.66f, water, 0.25f), OceanZone.IceBackLayer, true);
                item.FadeBottom = true;
                zone.AddItem(item);
            }
            var arch = SpriteFactory.Ice(IcePiece.IceArch);
            float aw = 2f * passage + pw * 0.8f;
            float ah = aw / arch.Aspect;
            zone.AddItem(Item(arch, new Vector2(0f, y + ph * 0.55f - ah * 0.4f), ah, 0f, false, 0f,
                              Tone(arch, 0.5f, water, 0.6f), OceanZone.IceBackLayer, false));
        }

        /// <summary>Выход из разлома: потолок из льдин во всю ширину, с края свисают сосульки.</summary>
        static void IceCeiling(OceanZone zone, ZoneTheme theme, Playfield field, float y, Color water, System.Random rng)
        {
            float hw = field.HalfWidth;
            float x = -hw - 0.5f;
            while (x < hw + 0.5f)
            {
                var info = SpriteFactory.Ice(IceDrift[rng.Next(IceDrift.Length)]);
                float ph = 1.1f + 0.3f * R(rng);
                float w = ph * info.Aspect;
                // над колонной темнее: потолок читается, но шары под ним ярче его
                float calm = Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(Mathf.Abs(x + w * 0.4f) / hw));
                zone.AddItem(Item(info, new Vector2(x + w * 0.4f, y + (R(rng) - 0.5f) * 0.3f), ph, 0f, R(rng) < 0.5f, 0f,
                                  Tone(info, 0.5f * calm, water, 0.55f), OceanZone.IceBackLayer, false));
                x += w * 0.75f;
            }
        }

        static void IceSize(IcePiece p, float r, out float height, out float sway)
        {
            sway = 0f;
            switch (p)
            {
                case IcePiece.CrystalA: case IcePiece.CrystalB: case IcePiece.CrystalC: height = Mathf.Lerp(0.9f, 1.4f, r); break;
                case IcePiece.FrozenColumn: height = Mathf.Lerp(3.2f, 4.6f, r); break;
                case IcePiece.BoulderA: case IcePiece.BoulderB: height = Mathf.Lerp(0.8f, 1.2f, r); break;
                case IcePiece.FrozenKelp: height = Mathf.Lerp(3f, 4.2f, r); sway = 0.012f; break;   // вмёрзла: едва шевелится
                case IcePiece.FrostRock: height = Mathf.Lerp(1.1f, 1.5f, r); break;
                default: height = 1f; break;
            }
        }

        static bool Upright(WorldPiece p)
        {
            switch (p)
            {
                case WorldPiece.KelpTallA: case WorldPiece.KelpTallB: case WorldPiece.KelpShort:
                case WorldPiece.GrassA: case WorldPiece.GrassB:
                case WorldPiece.ColumnA: case WorldPiece.ColumnB: case WorldPiece.Chain:
                case WorldPiece.DrumA: case WorldPiece.DrumB: case WorldPiece.Amphora: case WorldPiece.AnchorOld:
                case WorldPiece.SpireA: case WorldPiece.SpireB: case WorldPiece.SpireC:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Наклон кромки: на сколько юнитов край уходит вбок на юнит высоты.</summary>
        static float Slope(OceanZone zone, float t, bool right, float h)
        {
            const float e = 0.004f;
            float a = zone.InnerEdge(Mathf.Clamp01(t - e), right), b = zone.InnerEdge(Mathf.Clamp01(t + e), right);
            return (b - a) / (2f * e * h);
        }

        static OceanZone.Item Item(AtlasPiece info, Vector2 at, float height, float angle, bool flip, float sway,
                                   Color tint, byte layer, bool pivotBottom)
        {
            return new OceanZone.Item
            {
                Uv = info.Uv,
                Pos = at,
                Half = new Vector2(height * info.Aspect * 0.5f, height * 0.5f),
                Angle = angle,
                Flip = flip,
                Sway = sway,
                PivotY = pivotBottom ? -1f : 0f,
                Color = tint,
                Layer = layer,
            };
        }

        static Color Tone(AtlasPiece info, float bright, Color water, float fog)
        {
            Color target = Color.Lerp(Mul(info.Avg, bright), water, fog);
            return WorldLayers.TintFor(target, info.Avg);
        }

        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static float R(System.Random rng) => (float)rng.NextDouble();
    }
}
