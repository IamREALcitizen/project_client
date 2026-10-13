// Run with CutNautical.ps1 (Windows PowerShell 5.1, System.Drawing), compiled with HudArt.cs. Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Cuts GameScene3's nautical concepts into the sprites GameLayoutTabs and GameHudChat load from Resources/GameHud/Nautical:
// - chat-nautical.png (1942x809): the chat window. The game lays it 1080 wide, so 1.8 px a canvas unit.
// - night-protect-nautical.png (2172x724): the action line and the player strip in one frame, 2.01 px a canvas unit.
// What the game draws itself (texts, portraits, name plates, buttons, the chat rows and input bar) is painted out of the
// panels, the plates are rebuilt with a plain middle so they 9-slice, and the dark backdrop is taken out as alpha, worked
// out in linear colour as the game blends, so on the game's backdrop of that colour every piece looks as on the concept.
// The coordinates are those concepts' pixels: a redrawn concept needs them measured again.
public static class NauticalArt
{
    // The concepts' backdrops (sRGB): around and inside the chat window and around the night panel (the game's backdrop
    // under them is this colour), under the portraits and name plates, and the night panel's sky behind its badge and buttons.
    static readonly byte[] ChatBack = { 0, 8, 14 }, NightBack = { 0, 8, 14 }, RosterBack = { 2, 14, 20 }, HeaderBack = { 0, 15, 27 };

    public static string Run(string chatPng, string nightPng, string outDir, string previewDir)
    {
        var log = new StringBuilder();
        HudArt.Img chat = HudArt.Load(chatPng), night = HudArt.Load(nightPng);
        if (chat.W != 1942 || chat.H != 809 || night.W != 2172 || night.H != 724)
            throw new Exception("The concepts are not the measured 1942x809 and 2172x724 pictures.");
        Directory.CreateDirectory(outDir);
        var pieces = new List<Piece>();

        pieces.Add(new Piece("ChatFrame", ChatFrame(chat), new HudArt.Vector4(120, 364, 120, 240), 2048));
        pieces.Add(new Piece("ChatHarbor", ChatHarbor(chat), new HudArt.Vector4(0, 0, 0, 0), 2048));
        pieces.Add(new Piece("ChatTag", Extract(Plate(chat, 86, 436, 360, 511, 112, 330, new[] { 160, 190, 295, 325 }), ChatBack), Caps(26, 30), 2048));
        pieces.Add(new Piece("RuleStar", Unblend(HudArt.Crop(chat, 1796, 418, 52, 34), ChatBack), Caps(0, 0), 2048));
        pieces.Add(new Piece("InputPill", Extract(Pill(chat), ChatBack), Caps(36, 36), 2048));
        pieces.Add(new Piece("ChatDisc", ChatDisc(chat), Caps(0, 0), 2048));
        pieces.Add(new Piece("SendButton", Extract(HudArt.Crop(chat, 1686, 598, 164, 145), ChatBack), Caps(0, 0), 2048));

        pieces.Add(new Piece("NightPanel", NightPanel(night), new HudArt.Vector4(100, 284, 100, 356), 4096));
        pieces.Add(new Piece("TitleRule", TitleRule(night), Caps(0, 0), 2048));
        pieces.Add(new Piece("Badge", Extract(Plate(night, 70, 64, 410, 170, 190, 372, new[] { 355, 371 }), HeaderBack), Caps(120, 38), 2048));
        pieces.Add(new Piece("ButtonTeal", Extract(Plate(night, 1426, 72, 1734, 252, 1494, 1666, new[] { 1490, 1500, 1660, 1668 }), HeaderBack), Caps(68, 68), 2048));
        pieces.Add(new Piece("ButtonGold", Extract(Plate(night, 1744, 72, 2094, 252, 1842, 2004, new[] { 1842, 1864, 1986, 2004 }), HeaderBack), Caps(98, 90), 2048));
        pieces.Add(new Piece("NamePlate", Extract(Plate(night, 62, 572, 296, 650, 106, 252, new[] { 108, 150, 206, 250 }), RosterBack), Caps(44, 44), 2048));
        pieces.Add(new Piece("PortraitRing", PortraitRing(night, log), Caps(0, 0), 2048));

        foreach (Piece p in pieces)
        {
            string path = Path.Combine(outDir, p.Name + ".png");
            HudArt.Save(p.Img, path);
            HudArt.WriteMeta(path + ".meta", p.Border, p.MaxSize);
            log.AppendFormat("{0,-13} {1}x{2} border(l,b,r,t) {3}\n", p.Name, p.Img.W, p.Img.H, p.Border);
            if (!string.IsNullOrEmpty(previewDir)) // PowerShell passes no folder as ""
            {
                Directory.CreateDirectory(previewDir);
                HudArt.Save(Over(p.Img, new byte[] { 40, 160, 60 }), Path.Combine(previewDir, p.Name + ".png"));
            }
        }
        return log.ToString();
    }

    sealed class Piece
    {
        public string Name; public HudArt.Img Img; public HudArt.Vector4 Border; public int MaxSize;
        public Piece(string name, HudArt.Img img, HudArt.Vector4 border, int maxSize) { Name = name; Img = img; Border = border; MaxSize = maxSize; }
    }

    // Horizontal plates: fixed ends, the middle stretches; the height is scaled whole.
    static HudArt.Vector4 Caps(int left, int right) { return new HudArt.Vector4(left, 0, right, 0); }

    // ------------------------------------------------------------------ panels

    // The chat window: its frame, ropes, anchors and crests, the inside and the stage's edge above it painted out.
    static HudArt.Img ChatFrame(HudArt.Img src)
    {
        HudArt.Img img = Copy(src);
        // The studs half way down the sides would stretch: plain side from higher up instead.
        CopyRows(img, 0, 72, 300, 330, 392);
        CopyRows(img, 1868, 1942, 300, 330, 392);
        var paint = new bool[img.W * img.H];
        Box(paint, img.W, 55, 58, 1887, 755, true);                    // inside the frame's inner line
        Box(paint, img.W, 55, 58, 106, 102, false);                    // the corner stars and anchors reach in
        Box(paint, img.W, 1836, 58, 1887, 102, false);
        Box(paint, img.W, 55, 598, 96, 755, false);
        Box(paint, img.W, 1846, 598, 1887, 755, false);
        Box(paint, img.W, 252, 0, 932, 39, true);                      // the stage's edge above the frame, but not the crest
        Box(paint, img.W, 1008, 0, 1694, 39, true);
        Box(paint, img.W, 262, 770, 1680, 809, true);                  // under the frame, between the ropes
        Flat(img, paint, ChatBack);
        return Unblend(img, ChatBack);
    }

    // The harbour by moonlight at the window's right, from its top to the input bar: lies under the frame and the rows.
    static HudArt.Img ChatHarbor(HudArt.Img src)
    {
        HudArt.Img img = HudArt.Crop(src, 1060, 58, 1887 - 1060, 600 - 58);
        // The rows' rules and their stars cross it.
        var paint = new bool[img.W * img.H];
        foreach (int rule in new[] { 433, 513, 592 })
        {
            Box(paint, img.W, 0, rule - 58 - 3, img.W, rule - 58 + 7, true);
            Box(paint, img.W, 1796 - 1060, rule - 58 - 18, 1850 - 1060, rule - 58 + 18, true);
        }
        Box(paint, img.W, 1862 - 1060, 330 - 58, img.W, 392 - 58, true); // the stud on the frame's side reaches in
        Inpaint(img, paint, 7, true, 0f);
        img = Unblend(img, ChatBack);
        // Fades into the window on the left, the top and the bottom; the corner star above it is the frame's.
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                float f = Smooth(x / 300f) * Smooth(y / 60f) * Smooth((img.H - 1 - y) / 24f) * (1f - Smooth((x - 740) / 40f) * Smooth((70 - y) / 30f));
                int i = (y * img.W + x) * 4 + 3;
                img.Px[i] = (byte)(img.Px[i] * f);
            }
        return img;
    }

    // The action line and the player strip: one frame with the harbour in its head; the badge, texts, buttons, portraits
    // and name plates painted out.
    static HudArt.Img NightPanel(HudArt.Img src)
    {
        HudArt.Img img = Copy(src);
        var paint = new bool[img.W * img.H];
        Box(paint, img.W, 60, 66, 420, 180, true);                     // badge (under the rope hanging from the top)
        Mark(paint, img, 425, 58, 1266, 156, delegate(int r, int g, int b) { return r - b > 20 && Luma(r, g, b) > 70; }, 5);  // title
        Mark(paint, img, 135, 177, 830, 246, delegate(int r, int g, int b) { return Luma(r, g, b) > 62; }, 3);              // line under it
        Mark(paint, img, 430, 169, 1350, 177, delegate(int r, int g, int b) { return r - b > 0; }, 2);                         // the rule under the title
        Mark(paint, img, 1026, 148, 1076, 196, delegate(int r, int g, int b) { return r - b > 10; }, 2);                     // and its star (TitleRule)
        Inpaint(img, paint, 11, true, 4f);
        // The buttons from above and below only: beside them are the frame's rope and flourish.
        paint = new bool[img.W * img.H];
        Box(paint, img.W, 1420, 62, 2106, 250, true);
        Box(paint, img.W, 2084, 216, 2160, 270, false);
        Inpaint(img, paint, 13, false, 4f);

        // The strip's inside is made again plain (it stretches with the screen), its corner flourishes and the anchors kept.
        var strip = new bool[img.W * img.H];
        Box(strip, img.W, 22, 289, 2150, 664, true);
        Box(strip, img.W, 18, 286, 92, 342, false);                    // clear of the first and last portraits
        Box(strip, img.W, 2080, 286, 2156, 342, false);
        Box(strip, img.W, 18, 556, 64, 664, false);                    // and of the name plates
        Box(strip, img.W, 18, 646, 90, 664, false);
        Box(strip, img.W, 2108, 556, 2156, 664, false);
        Box(strip, img.W, 2082, 646, 2156, 664, false);
        Texture(img, strip, RosterBack, 652, 664, 5);

        // Around the frame (outside its lines) only the ropes, stars and anchors stay.
        HudArt.Img outside = Unblend(img, NightBack);
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                if (x >= 16 && x <= 2156 && y >= 26 && y <= 698) continue;
                int i = (y * img.W + x) * 4;
                Buffer.BlockCopy(outside.Px, i, img.Px, i, 4);
            }
        return img;
    }

    // ------------------------------------------------------------------ pieces

    // The gold rule with its star under the night panel's title (430..1350 x 148..196 on the concept), kept apart so the
    // game can hide it under a two-line text: only its gold (the sky and the clouds behind it are not).
    static HudArt.Img TitleRule(HudArt.Img src)
    {
        HudArt.Img img = Unblend(HudArt.Crop(src, 430, 148, 920, 48), HeaderBack);
        HudArt.Img raw = HudArt.Crop(src, 430, 148, 920, 48);
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                int i = (y * img.W + x) * 4;
                bool line = y + 148 >= 170 && y + 148 <= 175, star = x + 430 >= 1026 && x + 430 < 1076;
                float gold = Clamp01((raw.Px[i + 2] - raw.Px[i] + 5) / 40f);
                img.Px[i + 3] = (byte)(line || star ? img.Px[i + 3] * gold : 0);
            }
        return img;
    }

    // A plate of [x0, x1) x [y0, y1) rebuilt for 9-slicing: its left end up to leftEnd, a plain middle (each row the
    // median of the clean column ranges, given as from/to pairs, clear of the plate's text: its strokes would show as a
    // line through the middle), its right end from rightStart. The ends fade into the middle over a few pixels so no seam shows.
    static HudArt.Img Plate(HudArt.Img src, int x0, int y0, int x1, int y1, int leftEnd, int rightStart, int[] clean)
    {
        const int Middle = 8, Fade = 6;
        int h = y1 - y0, lw = leftEnd - x0, rw = x1 - rightStart;
        var o = new HudArt.Img(lw + Middle + rw, h);
        var mid = new byte[h * 4];
        for (int y = 0; y < h; y++)
        {
            var samples = new List<int>[4];
            for (int c = 0; c < 4; c++) samples[c] = new List<int>();
            for (int k = 0; k + 1 < clean.Length; k += 2)
                for (int x = clean[k]; x < clean[k + 1]; x++)
                    for (int c = 0; c < 4; c++) samples[c].Add(src.Px[((y0 + y) * src.W + x) * 4 + c]);
            for (int c = 0; c < 4; c++) { samples[c].Sort(); mid[y * 4 + c] = (byte)samples[c][samples[c].Count / 2]; }
        }
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < o.W; x++)
            {
                int sx = x < lw ? x0 + x : x >= lw + Middle ? rightStart + (x - lw - Middle) : -1;
                float toMid = sx < 0 ? 1f : x < lw ? Math.Max(0f, 1f - (lw - x) / (float)Fade) : Math.Max(0f, 1f - (x - lw - Middle + 1) / (float)Fade);
                for (int c = 0; c < 4; c++)
                {
                    float a = sx < 0 ? 0f : src.Px[((y0 + y) * src.W + sx) * 4 + c];
                    o.Px[(y * o.W + x) * 4 + c] = (byte)Math.Round(a + (mid[y * 4 + c] - a) * toMid);
                }
            }
        }
        return o;
    }

    // The input pill: its right end (clear of the smiley), mirrored for the left end the chat disc hides.
    static HudArt.Img Pill(HudArt.Img src)
    {
        HudArt.Img right = Plate(src, 1600, 616, 1676, 737, 1601, 1640, new[] { 900, 1000 });
        HudArt.Img both = new HudArt.Img(36 + 8 + 36, right.H);
        for (int y = 0; y < right.H; y++)
            for (int x = 0; x < both.W; x++)
            {
                int sx = x < 36 ? right.W - 1 - x : x < 44 ? 3 : right.W - 36 + (x - 44);
                Buffer.BlockCopy(right.Px, (y * right.W + sx) * 4, both.Px, (y * both.W + x) * 4, 4);
            }
        return both;
    }

    // The chat mark's ring with its compass points (centre measured from the points), its speech bubble painted out (the
    // game draws its own mark). Its right half lies on the pill, so the left half is mirrored over it.
    static HudArt.Img ChatDisc(HudArt.Img src)
    {
        const double Cx = 154.6, Cy = 674.5, Ring = 61.5, Bubble = 39;
        int half = 74, x0 = (int)Math.Round(Cx) - half, y0 = (int)Math.Round(Cy) - half;
        HudArt.Img img = HudArt.Crop(src, x0, y0, half * 2, half * 2);
        double lx = Cx - x0, ly = Cy - y0;
        for (int y = 0; y < img.H; y++)
            for (int x = (int)Math.Ceiling(lx); x < img.W; x++)
            {
                int sx = (int)Math.Round(2 * lx - x);
                if (sx >= 0) Buffer.BlockCopy(img.Px, (y * img.W + sx) * 4, img.Px, (y * img.W + x) * 4, 4);
            }
        var paint = new bool[img.W * img.H];
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
                paint[y * img.W + x] = Dist(x, y, lx, ly) < Bubble;
        Inpaint(img, paint, 3, true, 3f);
        HudArt.Img soft = Unblend(img, ChatBack);
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                int i = (y * img.W + x) * 4;
                double d = Dist(x, y, lx, ly);
                if (d <= Ring) img.Px[i + 3] = 255;
                else if (d <= 69) Buffer.BlockCopy(soft.Px, i, img.Px, i, 4); // the points stand on the window's backdrop
                else img.Px[i + 3] = 0;                                     // and the frame's rope beside them is not theirs
            }
        return HudArt.Trim(img);
    }

    // The gold ring around a portrait with the anchor badge on its foot; inside and around it clear.
    static HudArt.Img PortraitRing(HudArt.Img src, StringBuilder log)
    {
        double cx, cy, rIn, rOut;
        FitRing(src, 176, 434, 100, 132, delegate(int deg) { return deg > 60 && deg < 120 || deg > 262 && deg < 278; }, out cx, out cy, out rIn, out rOut);
        log.AppendFormat("PortraitRing centre ({0:F1}, {1:F1}) r {2:F1}..{3:F1}\n", cx, cy, rIn, rOut);
        // The badge (measured): a disc 113.6 below the centre, points up into the ring and down past it.
        double bx = 177, by = cy + 113.6, bR = 21.5, top = cy + 83.6, foot = cy + 143.1;
        int half = (int)Math.Ceiling(rOut) + 6, x0 = (int)Math.Round(cx) - half, y0 = (int)Math.Round(cy) - half;
        HudArt.Img img = HudArt.Crop(src, x0, y0, half * 2, (int)Math.Ceiling(foot) + 2 - y0);
        double lx = cx - x0, ly = cy - y0;
        bx -= x0; by -= y0; top -= y0; foot -= y0;
        HudArt.Img soft = Unblend(img, RosterBack);
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                int i = (y * img.W + x) * 4;
                double d = Dist(x, y, lx, ly);
                double badge = Clamp01(bR - Dist(x, y, bx, by) + 0.5);
                double up = (y - top) / (by - bR + 4 - top), down = (foot - y) / (foot - (by + bR - 4));
                if (up > 0 && up < 1) badge = Math.Max(badge, Clamp01(7 * up - Math.Abs(x - bx) + 0.5));
                if (down > 0 && down < 1) badge = Math.Max(badge, Clamp01(7 * down - Math.Abs(x - bx) + 0.5));
                double ring = Clamp01(d - (rIn - 0.5)) * Clamp01(rOut - 1.5 - d + 0.5);
                double a = Math.Max(ring, badge);
                if (badge < 1 && d > rOut - 2.5 && d < rOut + 3) // the ring's soft outer edge on the strip
                {
                    double edge = soft.Px[i + 3] / 255.0;
                    if (edge > a) { for (int c = 0; c < 3; c++) img.Px[i + c] = soft.Px[i + c]; a = edge; }
                }
                img.Px[i + 3] = (byte)Math.Round(255 * a);
            }
        return img;
    }

    // ------------------------------------------------------------------ painting out

    // Masks: fill or clear a box [x0, x1) x [y0, y1) (the part inside the mask).
    static void Box(bool[] mask, int w, int x0, int y0, int x1, int y1, bool value)
    {
        int h = mask.Length / w;
        for (int y = Math.Max(0, y0); y < Math.Min(h, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(w, x1); x++) mask[y * w + x] = value;
    }

    delegate bool Pick(int r, int g, int b);

    // Marks the pixels of a box that pick, grown by grow pixels (strokes of text with their soft edges).
    static void Mark(bool[] mask, HudArt.Img img, int x0, int y0, int x1, int y1, Pick pick, int grow)
    {
        var hit = new bool[img.W * img.H];
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                int i = (y * img.W + x) * 4;
                if (pick(img.Px[i + 2], img.Px[i + 1], img.Px[i])) hit[y * img.W + x] = true;
            }
        for (int y = Math.Max(0, y0 - grow); y < Math.Min(img.H, y1 + grow); y++)
            for (int x = Math.Max(0, x0 - grow); x < Math.Min(img.W, x1 + grow); x++)
            {
                bool near = false;
                for (int dy = -grow; dy <= grow && !near; dy++)
                    for (int dx = -grow; dx <= grow && !near; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        near = nx >= 0 && ny >= 0 && nx < img.W && ny < img.H && dx * dx + dy * dy <= grow * grow && hit[ny * img.W + nx];
                    }
                if (near) mask[y * img.W + x] = true;
            }
    }

    // Masked pixels from their surroundings: the nearest clear pixel above and below (and left and right with rows),
    // weighted by nearness, then smoothed, with a little grain like the painting's.
    static void Inpaint(HudArt.Img img, bool[] mask, int seed, bool rows, float grainSize)
    {
        int w = img.W, h = img.H;
        var sum = new float[w * h * 3];
        var weight = new float[w * h];
        for (int pass = rows ? 0 : 1; pass < 2; pass++) // rows, then columns
        {
            int lines = pass == 0 ? h : w, length = pass == 0 ? w : h;
            for (int line = 0; line < lines; line++)
            {
                int last = -1;
                var before = new int[length];
                for (int k = 0; k < length; k++)
                {
                    int i = pass == 0 ? line * w + k : k * w + line;
                    if (!mask[i]) last = k;
                    before[k] = last;
                }
                int next = -1;
                for (int k = length - 1; k >= 0; k--)
                {
                    int i = pass == 0 ? line * w + k : k * w + line;
                    if (!mask[i]) { next = k; continue; }
                    foreach (int s in new[] { before[k], next })
                    {
                        if (s < 0) continue;
                        int j = pass == 0 ? line * w + s : s * w + line;
                        float wt = 1f / Math.Abs(k - s);
                        for (int c = 0; c < 3; c++) sum[i * 3 + c] += img.Px[j * 4 + c] * wt;
                        weight[i] += wt;
                    }
                }
            }
        }
        var value = new float[w * h * 3];
        for (int i = 0; i < w * h; i++)
            for (int c = 0; c < 3; c++)
                value[i * 3 + c] = mask[i] && weight[i] > 0 ? sum[i * 3 + c] / weight[i] : img.Px[i * 4 + c];
        for (int it = 0; it < (rows ? 40 : 160); it++)
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    if (!mask[i]) continue;
                    for (int c = 0; c < 3; c++)
                        value[i * 3 + c] = rows
                            ? (value[(i - 1) * 3 + c] + value[(i + 1) * 3 + c] + value[(i - w) * 3 + c] + value[(i + w) * 3 + c]) * 0.25f
                            : (value[(i - 1) * 3 + c] + value[(i + 1) * 3 + c] + 2f * value[i * 3 + c]) * 0.25f; // columns: blur along the rows only
                }
        var random = new Random(seed);
        for (int i = 0; i < w * h; i++)
        {
            if (!mask[i]) continue;
            float grain = (float)(random.NextDouble() - 0.5) * grainSize;
            for (int c = 0; c < 3; c++) img.Px[i * 4 + c] = ToByte(value[i * 3 + c] + grain);
            img.Px[i * 4 + 3] = 255;
        }
    }

    // Masked pixels become a plain colour.
    static void Flat(HudArt.Img img, bool[] mask, byte[] rgb)
    {
        for (int i = 0; i < img.W * img.H; i++)
        {
            if (!mask[i]) continue;
            img.Px[i * 4] = rgb[2]; img.Px[i * 4 + 1] = rgb[1]; img.Px[i * 4 + 2] = rgb[0]; img.Px[i * 4 + 3] = 255;
        }
    }

    // Masked pixels become the colour with a soft mottle (value noise every 28 px) and grain; from row blendFrom to
    // blendTo they fade back into the painting.
    static void Texture(HudArt.Img img, bool[] mask, byte[] rgb, int blendFrom, int blendTo, int seed)
    {
        const int Cell = 28;
        var random = new Random(seed);
        int gw = img.W / Cell + 2, gh = img.H / Cell + 2;
        var grid = new float[gw * gh];
        for (int i = 0; i < grid.Length; i++) grid[i] = (float)(random.NextDouble() - 0.5) * 7f;
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
            {
                int i = y * img.W + x;
                if (!mask[i]) continue;
                float fx = x / (float)Cell, fy = y / (float)Cell;
                int gx = (int)fx, gy = (int)fy;
                float tx = Smooth(fx - gx), ty = Smooth(fy - gy);
                float n = Lerp(Lerp(grid[gy * gw + gx], grid[gy * gw + gx + 1], tx), Lerp(grid[(gy + 1) * gw + gx], grid[(gy + 1) * gw + gx + 1], tx), ty);
                n += (float)(random.NextDouble() - 0.5) * 3f;
                float keep = y < blendFrom ? 0f : Clamp01((y - blendFrom) / (float)(blendTo - blendFrom));
                for (int c = 0; c < 3; c++)
                {
                    float made = rgb[2 - c] + n * (c == 0 ? 1.2f : 1f);
                    img.Px[i * 4 + c] = ToByte(made + (img.Px[i * 4 + c] - made) * keep);
                }
                img.Px[i * 4 + 3] = 255;
            }
    }

    // Rows y0..y1 of columns [x0, x1) copied from the row at from (and below it, repeating its height).
    static void CopyRows(HudArt.Img img, int x0, int x1, int from, int y0, int y1)
    {
        for (int y = y0; y < y1; y++)
            Buffer.BlockCopy(img.Px, ((from + (y - y0) % 20) * img.W + x0) * 4, img.Px, (y * img.W + x0) * 4, (x1 - x0) * 4);
    }

    // ------------------------------------------------------------------ alpha

    // The backdrop taken out: each pixel becomes the least opaque colour that, laid on back, gives the pixel again. In
    // linear colour, as the game blends (the project is linear).
    static HudArt.Img Unblend(HudArt.Img img, byte[] back)
    {
        var o = new HudArt.Img(img.W, img.H);
        float[] b = { Lin(back[2]), Lin(back[1]), Lin(back[0]) }; // B, G, R as stored
        for (int i = 0; i < img.W * img.H; i++)
        {
            float a = 0f;
            var p = new float[3];
            for (int c = 0; c < 3; c++)
            {
                p[c] = Lin(img.Px[i * 4 + c]);
                float d = p[c] > b[c] ? (p[c] - b[c]) / (1f - b[c]) : p[c] < b[c] ? (b[c] - p[c]) / b[c] : 0f;
                a = Math.Max(a, d);
            }
            a *= img.Px[i * 4 + 3] / 255f;
            if (a < 0.02f) { o.Px[i * 4 + 3] = 0; continue; }
            for (int c = 0; c < 3; c++) o.Px[i * 4 + c] = Srgb(b[c] + (p[c] - b[c]) / a);
            o.Px[i * 4 + 3] = ToByte(a * 255f);
        }
        return o;
    }

    // A piece on its backdrop: what the backdrop reaches from the edges (through pixels near its colour) is taken out
    // (Unblend), the rest stays opaque. Trimmed.
    static HudArt.Img Extract(HudArt.Img img, byte[] back)
    {
        int w = img.W, h = img.H;
        var outside = new bool[w * h];
        var queue = new Queue<int>();
        Action<int, int> seed = delegate(int x, int y)
        {
            int i = y * w + x;
            if (outside[i]) return;
            int r = img.Px[i * 4 + 2], g = img.Px[i * 4 + 1], b = img.Px[i * 4];
            if (Math.Abs(r - back[0]) + Math.Abs(g - back[1]) + Math.Abs(b - back[2]) > 70) return;
            outside[i] = true; queue.Enqueue(i);
        };
        for (int x = 0; x < w; x++) { seed(x, 0); seed(x, h - 1); }
        for (int y = 0; y < h; y++) { seed(0, y); seed(w - 1, y); }
        while (queue.Count > 0)
        {
            int i = queue.Dequeue(), x = i % w, y = i / w;
            if (x > 0) seed(x - 1, y); if (x < w - 1) seed(x + 1, y);
            if (y > 0) seed(x, y - 1); if (y < h - 1) seed(x, y + 1);
        }
        // The rim next to the backdrop is soft too.
        var soft = new bool[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (outside[y * w + x]) { soft[y * w + x] = true; continue; }
                for (int d = -2; d <= 2; d++)
                {
                    if (x + d >= 0 && x + d < w && outside[y * w + x + d]) soft[y * w + x] = true;
                    if (y + d >= 0 && y + d < h && outside[(y + d) * w + x]) soft[y * w + x] = true;
                }
            }
        HudArt.Img un = Unblend(img, back);
        var o = Copy(img);
        for (int i = 0; i < w * h; i++)
        {
            if (soft[i]) Buffer.BlockCopy(un.Px, i * 4, o.Px, i * 4, 4);
            else o.Px[i * 4 + 3] = 255;
        }
        return HudArt.Trim(o);
    }

    // ------------------------------------------------------------------ rings

    // A gold ring near (x, y) with an outer radius between rMin and rMax: along rays from that point, where the gold
    // starts and ends; the centre is fitted to the outer edge (least squares), the radii averaged. Rays through the
    // badge, the points or whatever else lies on the ring are skipped (degrees, clockwise from the right as y grows down).
    static void FitRing(HudArt.Img img, double x, double y, double rMin, double rMax, Skip skip, out double cx, out double cy, out double rIn, out double rOut)
    {
        var outer = new List<double[]>();
        var inner = new List<double>();
        for (int deg = 0; deg < 360; deg += 2)
        {
            if (skip(deg)) continue;
            double ux = Math.Cos(deg * Math.PI / 180), uy = Math.Sin(deg * Math.PI / 180);
            double first = -1, last = -1;
            for (double r = rMin - 20; r <= rMax; r += 0.5)
            {
                int px = (int)Math.Round(x + ux * r), py = (int)Math.Round(y + uy * r);
                int i = (py * img.W + px) * 4;
                int R = img.Px[i + 2], G = img.Px[i + 1], B = img.Px[i];
                bool gold = R > 120 && R - B > 50 && G > 70;
                if (gold) { if (first < 0 && r >= rMin - 20) first = r; last = r; }
            }
            if (last < rMin) continue;
            outer.Add(new[] { x + ux * last, y + uy * last });
            inner.Add(first);
        }
        // Kasa circle fit: x^2 + y^2 + Dx + Ey + F = 0.
        double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0, sz = 0, sxz = 0, syz = 0; int n = outer.Count;
        foreach (double[] p in outer)
        {
            double z = p[0] * p[0] + p[1] * p[1];
            sx += p[0]; sy += p[1]; sxx += p[0] * p[0]; syy += p[1] * p[1]; sxy += p[0] * p[1]; sz += z; sxz += p[0] * z; syz += p[1] * z;
        }
        double[,] m = { { sxx, sxy, sx }, { sxy, syy, sy }, { sx, sy, n } };
        double[] v = { -sxz, -syz, -sz };
        double[] s = Solve(m, v);
        cx = -s[0] / 2; cy = -s[1] / 2;
        rOut = Math.Sqrt(cx * cx + cy * cy - s[2]);
        inner.Sort();
        double rInRay = inner[inner.Count / 2]; // from (x, y), close enough to the centre for the median
        rIn = rInRay;
    }

    delegate bool Skip(int deg);

    static double[] Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        for (int col = 0; col < n; col++)
        {
            int pivot = col;
            for (int r = col + 1; r < n; r++) if (Math.Abs(a[r, col]) > Math.Abs(a[pivot, col])) pivot = r;
            for (int c = 0; c < n; c++) { double t = a[col, c]; a[col, c] = a[pivot, c]; a[pivot, c] = t; }
            double tb = b[col]; b[col] = b[pivot]; b[pivot] = tb;
            for (int r = 0; r < n; r++)
            {
                if (r == col) continue;
                double f = a[r, col] / a[col, col];
                for (int c = 0; c < n; c++) a[r, c] -= f * a[col, c];
                b[r] -= f * b[col];
            }
        }
        var x = new double[n];
        for (int i = 0; i < n; i++) x[i] = b[i] / a[i, i];
        return x;
    }

    // ------------------------------------------------------------------ helpers

    static HudArt.Img Copy(HudArt.Img img)
    {
        var o = new HudArt.Img(img.W, img.H);
        Buffer.BlockCopy(img.Px, 0, o.Px, 0, img.Px.Length);
        return o;
    }

    // For the previews: the piece over a plain colour.
    static HudArt.Img Over(HudArt.Img img, byte[] rgb)
    {
        var o = new HudArt.Img(img.W, img.H);
        for (int i = 0; i < img.W * img.H; i++)
        {
            float a = img.Px[i * 4 + 3] / 255f;
            for (int c = 0; c < 3; c++) o.Px[i * 4 + c] = ToByte(rgb[2 - c] + (img.Px[i * 4 + c] - rgb[2 - c]) * a);
            o.Px[i * 4 + 3] = 255;
        }
        return o;
    }

    static double Dist(double x, double y, double cx, double cy) { return Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)); }
    static float Luma(int r, int g, int b) { return 0.299f * r + 0.587f * g + 0.114f * b; }
    static float Smooth(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }
    static float Lerp(float a, float b, float t) { return a + (b - a) * t; }
    static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
    static double Clamp01(double v) { return v < 0 ? 0 : v > 1 ? 1 : v; }
    static byte ToByte(float v) { return (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v))); }

    static float Lin(byte c)
    {
        float s = c / 255f;
        return s <= 0.04045f ? s / 12.92f : (float)Math.Pow((s + 0.055f) / 1.055f, 2.4f);
    }

    static byte Srgb(float l)
    {
        l = Clamp01(l);
        float s = l <= 0.0031308f ? l * 12.92f : 1.055f * (float)Math.Pow(l, 1f / 2.4f) - 0.055f;
        return ToByte(s * 255f);
    }
}
