// Run with CutHudSheet.ps1 (Windows PowerShell 5.1, System.Drawing). Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// Cuts the game HUD asset sheet (gold-framed pirate panels on a white background) into the sprites GameHudLayout loads
// from Resources/GameHud: removes the white background, finds each piece, names it by where it sits on the sheet,
// makes the stretchable frames 9-sliceable and writes the .meta files (sprite borders; GUIDs are kept on a rerun).
public static class HudArt
{
    public sealed class Img
    {
        public int W, H; public byte[] Px; // BGRA
        public Img(int w, int h) { W = w; H = h; Px = new byte[w * h * 4]; }
        public byte A(int x, int y) { return Px[(y * W + x) * 4 + 3]; }
        public bool Opaque(int x, int y) { return A(x, y) >= 20; }
    }

    // Name, top-left of the piece on the 1536x1024 sheet. A piece is matched to the nearest name within 60 px.
    static readonly string[] Names = { "ButtonMap", "ButtonSettings", "TimerPanel", "PhasePlaque", "NoticeScroll", "ChatAndInput",
                                       "ButtonSend", "ChairTeal", "CardBack", "ParchmentPanel", "TealPanel" };
    static readonly int[,] Spots = { { 1274, 81 }, { 1020, 87 }, { 503, 90 }, { 14, 103 }, { 17, 377 }, { 526, 389 },
                                     { 1325, 415 }, { 1228, 623 }, { 983, 628 }, { 485, 689 }, { 13, 700 } };

    public static string Run(string sheetPng, string outDir)
    {
        var log = new StringBuilder();
        Img sheet = Load(sheetPng);
        RemoveBackground(sheet);
        var pieces = new Dictionary<string, Img>();
        foreach (var piece in Pieces(sheet, 3000))
        {
            string name = null; double best = 60;
            for (int i = 0; i < Names.Length; i++)
            {
                double d = Math.Sqrt(Math.Pow(piece.Key.X - Spots[i, 0], 2) + Math.Pow(piece.Key.Y - Spots[i, 1], 2));
                if (d < best) { best = d; name = Names[i]; }
            }
            if (name == null) { log.AppendLine("unmatched piece at " + piece.Key); continue; }
            pieces[name] = piece.Value;
        }
        foreach (string n in Names) if (!pieces.ContainsKey(n)) log.AppendLine("missing " + n);

        // The chat frame and the input bar touch on the sheet: split them at the gap.
        Img both = pieces["ChatAndInput"]; pieces.Remove("ChatAndInput");
        int gap = GapColumn(both, both.W / 3, both.W * 2 / 3);
        pieces["ChatFrame"] = Trim(Crop(both, 0, 0, gap, both.H));
        pieces["InputFrame"] = Trim(Crop(both, gap, 0, both.W - gap, both.H));

        // Chat frame: stretched far wider than drawn, so its crest and side studs come off (the crest is drawn on top
        // separately) and the 9-slice borders cover the corners and the studs near them.
        Img chat = pieces["ChatFrame"];
        pieces["ChatFrameCrest"] = Decrest(chat, true, log, "ChatFrameCrest");
        RemoveSideStuds(chat);
        // Role description card: stretched a third wider, so both crests come off the same way.
        Img parchment = pieces["ParchmentPanel"];
        pieces["ParchmentCrestTop"] = Decrest(parchment, true, log, "ParchmentCrestTop");
        pieces["ParchmentCrestBottom"] = Decrest(parchment, false, log, "ParchmentCrestBottom");

        Directory.CreateDirectory(outDir);
        foreach (var p in pieces)
        {
            Vector4 border = BorderFor(p.Key, p.Value);
            string path = Path.Combine(outDir, p.Key + ".png");
            Save(p.Value, path);
            WriteMeta(path + ".meta", border);
            log.AppendFormat("{0,-15} {1}x{2} border(l,b,r,t) {3}\n", p.Key, p.Value.W, p.Value.H, border);
        }
        return log.ToString();
    }

    public struct Vector4
    {
        public int L, B, R, T;
        public Vector4(int l, int b, int r, int t) { L = l; B = b; R = r; T = t; }
        public override string ToString() { return L + "," + B + "," + R + "," + T; }
    }

    // Stretchable pieces: caps (telescopes, wheels) or corners stay, the middle stretches.
    static Vector4 BorderFor(string name, Img img)
    {
        switch (name)
        {
            case "ChatFrame": return new Vector4(118, 80, 119, 80);
            case "ParchmentPanel": return new Vector4(100, 70, 100, 70);
            case "NoticeScroll": { int l, r; Caps(img, out l, out r); return new Vector4(l, 0, r, 0); }
            case "InputFrame": return new Vector4(80, 0, 82, 0); // the wheels are as tall as the bar, so measured by eye
            default: return new Vector4(0, 0, 0, 0);
        }
    }

    // ------------------------------------------------------------------ background and pieces

    static void RemoveBackground(Img img)
    {
        int w = img.W, h = img.H; byte[] px = img.Px;
        var bg = new bool[w * h]; var q = new Queue<int>();
        Func<int, bool> white = i =>
        {
            int b = px[i * 4], g = px[i * 4 + 1], r = px[i * 4 + 2];
            int mn = Math.Min(r, Math.Min(g, b)), mx = Math.Max(r, Math.Max(g, b));
            return mn >= 226 && mx - mn <= 24;
        };
        Action<int, int> seed = (x, y) => { int i = y * w + x; if (!bg[i] && white(i)) { bg[i] = true; q.Enqueue(i); } };
        for (int x = 0; x < w; x++) { seed(x, 0); seed(x, h - 1); }
        for (int y = 0; y < h; y++) { seed(0, y); seed(w - 1, y); }
        while (q.Count > 0)
        {
            int i = q.Dequeue(), x = i % w, y = i / w;
            if (x > 0) seed(x - 1, y); if (x < w - 1) seed(x + 1, y);
            if (y > 0) seed(x, y - 1); if (y < h - 1) seed(x, y + 1);
        }
        for (int i = 0; i < w * h; i++) if (bg[i]) px[i * 4 + 3] = 0;
        // Soft edge: rim pixels keep only their non-white part.
        for (int pass = 0; pass < 2; pass++)
        {
            var rim = new List<int>();
            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int i = y * w + x;
                    if (px[i * 4 + 3] == 0) continue;
                    if (px[i * 4 - 1] == 0 || px[i * 4 + 7] == 0 || px[(i - w) * 4 + 3] == 0 || px[(i + w) * 4 + 3] == 0) rim.Add(i);
                }
            foreach (int i in rim)
            {
                float mn = Math.Min(px[i * 4 + 2], Math.Min(px[i * 4 + 1], px[i * 4]));
                float a = Math.Max(0f, Math.Min(1f, (255f - mn) / 90f));
                if (a < 0.08f) { px[i * 4 + 3] = 0; continue; }
                for (int c = 0; c < 3; c++) px[i * 4 + c] = (byte)Math.Max(0, Math.Min(255, (px[i * 4 + c] - (1f - a) * 255f) / a));
                px[i * 4 + 3] = (byte)Math.Min(px[i * 4 + 3], (int)(a * 255));
            }
        }
    }

    static List<KeyValuePair<Point, Img>> Pieces(Img img, int minPixels)
    {
        int w = img.W, h = img.H;
        var label = new int[w * h]; var q = new Queue<int>(); int n = 0;
        var result = new List<KeyValuePair<Point, Img>>();
        for (int start = 0; start < w * h; start++)
        {
            if (img.Px[start * 4 + 3] < 20 || label[start] != 0) continue;
            n++; int l = w, t = h, r = -1, b = -1, count = 0;
            label[start] = n; q.Enqueue(start);
            while (q.Count > 0)
            {
                int i = q.Dequeue(), x = i % w, y = i / w; count++;
                l = Math.Min(l, x); r = Math.Max(r, x); t = Math.Min(t, y); b = Math.Max(b, y);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                        int j = ny * w + nx;
                        if (label[j] == 0 && img.Px[j * 4 + 3] >= 20) { label[j] = n; q.Enqueue(j); }
                    }
            }
            if (count < minPixels) continue;
            l = Math.Max(0, l - 2); t = Math.Max(0, t - 2); r = Math.Min(w - 1, r + 2); b = Math.Min(h - 1, b + 2);
            var piece = new Img(r - l + 1, b - t + 1);
            for (int y = 0; y < piece.H; y++)
                for (int x = 0; x < piece.W; x++)
                {
                    int i = (t + y) * w + l + x;
                    if (label[i] != n && img.Px[i * 4 + 3] >= 20) continue; // another piece's pixel
                    Buffer.BlockCopy(img.Px, i * 4, piece.Px, (y * piece.W + x) * 4, 4);
                }
            result.Add(new KeyValuePair<Point, Img>(new Point(l, t), piece));
        }
        return result;
    }

    static int GapColumn(Img img, int from, int to)
    {
        int best = from, least = int.MaxValue;
        for (int x = from; x < to; x++)
        {
            int count = 0;
            for (int y = 0; y < img.H; y++) if (img.Opaque(x, y)) count++;
            if (count < least) { least = count; best = x; }
        }
        return best;
    }

    // ------------------------------------------------------------------ chat frame clean-up

    // A crest sticks out past the top (or bottom) bar in the middle third. It is cut out as its own sprite (not
    // trimmed: GameHudLayout puts it back centred on that edge) and replaced on the frame by the plain bar beside it.
    static Img Decrest(Img img, bool top, StringBuilder log, string name)
    {
        Func<int, int> depth = x =>
        {
            for (int i = 0; i < img.H; i++) if (img.Opaque(x, top ? i : img.H - 1 - i)) return i;
            return img.H;
        };
        var depths = new List<int>();
        for (int x = img.W / 5; x < img.W * 4 / 5; x++) depths.Add(depth(x));
        depths.Sort();
        int bar = depths[depths.Count * 3 / 4]; // most columns are plain bar
        int left = int.MaxValue, right = -1;
        for (int x = img.W / 3; x < img.W * 2 / 3; x++)
            if (depth(x) < bar - 3) { left = Math.Min(left, x); right = Math.Max(right, x); }
        if (right < 0) { log.AppendLine(name + ": no crest found"); return new Img(1, 1); }
        left -= 6; right += 6;
        int band = bar + 34, y0 = top ? 0 : img.H - band;
        Img crest = Crop(img, left, y0, right - left + 1, band);
        CopyColumns(img, left - 10, left, right, y0, y0 + band);
        log.AppendFormat("{0}: {1}x{2}, centre offset from the frame centre {3} px\n", name, crest.W, crest.H, (left + right + 1) / 2f - img.W / 2f);
        return crest;
    }

    static void CopyColumns(Img img, int source, int from, int to, int y0, int y1)
    {
        for (int x = from; x <= to; x++)
            for (int y = y0; y < Math.Min(img.H, y1); y++)
                Buffer.BlockCopy(img.Px, (y * img.W + source) * 4, img.Px, (y * img.W + x) * 4, 4);
    }

    // Studs half way down the sides would stretch into long lumps: replace those rows with plain side bar.
    static void RemoveSideStuds(Img img)
    {
        foreach (bool leftSide in new[] { true, false })
        {
            Func<int, int> edge = y =>
            {
                for (int i = 0; i < img.W / 4; i++)
                {
                    int x = leftSide ? i : img.W - 1 - i;
                    if (img.Opaque(x, y)) return i;
                }
                return img.W;
            };
            int y0 = img.H / 3, y1 = img.H * 2 / 3;
            var depths = new List<int>();
            for (int y = y0; y < y1; y++) depths.Add(edge(y));
            var sorted = new List<int>(depths); sorted.Sort();
            int plain = sorted[sorted.Count * 3 / 4];
            int studTop = -1, studBottom = -1;
            for (int y = y0; y < y1; y++)
                if (edge(y) < plain - 2) { if (studTop < 0) studTop = y; studBottom = y; }
            if (studTop < 0) continue;
            studTop -= 4; studBottom += 4;
            int sourceRow = studTop - 6, band = img.W / 5;
            for (int y = studTop; y <= studBottom; y++)
                for (int i = 0; i < band; i++)
                {
                    int x = leftSide ? i : img.W - 1 - i;
                    Buffer.BlockCopy(img.Px, (sourceRow * img.W + x) * 4, img.Px, (y * img.W + x) * 4, 4);
                }
        }
    }

    // Caps at both ends are taller than the bar between them.
    static void Caps(Img img, out int left, out int right)
    {
        var heights = new int[img.W];
        for (int x = 0; x < img.W; x++) for (int y = 0; y < img.H; y++) if (img.Opaque(x, y)) heights[x]++;
        var middle = new List<int>();
        for (int x = img.W * 2 / 5; x < img.W * 3 / 5; x++) middle.Add(heights[x]);
        middle.Sort();
        int bar = middle[middle.Count / 2];
        left = 0; right = 0;
        for (int x = img.W / 2; x >= 0; x--) if (heights[x] > bar + 6) { left = x + 4; break; }
        for (int x = img.W / 2; x < img.W; x++) if (heights[x] > bar + 6) { right = img.W - x + 4; break; }
    }

    // ------------------------------------------------------------------ files

    public static Img Crop(Img img, int x0, int y0, int w, int h)
    {
        var o = new Img(w, h);
        for (int y = 0; y < h; y++) Buffer.BlockCopy(img.Px, ((y0 + y) * img.W + x0) * 4, o.Px, y * w * 4, w * 4);
        return o;
    }

    public static Img Trim(Img img)
    {
        int l = img.W, t = img.H, r = -1, b = -1;
        for (int y = 0; y < img.H; y++)
            for (int x = 0; x < img.W; x++)
                if (img.Opaque(x, y)) { l = Math.Min(l, x); r = Math.Max(r, x); t = Math.Min(t, y); b = Math.Max(b, y); }
        if (r < 0) return img;
        l = Math.Max(0, l - 2); t = Math.Max(0, t - 2); r = Math.Min(img.W - 1, r + 2); b = Math.Min(img.H - 1, b + 2);
        return Crop(img, l, t, r - l + 1, b - t + 1);
    }

    public static Img Load(string path)
    {
        using (var file = new Bitmap(path))
        using (var b = file.Clone(new Rectangle(0, 0, file.Width, file.Height), PixelFormat.Format32bppArgb))
        {
            var img = new Img(b.Width, b.Height);
            var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < b.Height; y++) Marshal.Copy(d.Scan0 + y * d.Stride, img.Px, y * img.W * 4, img.W * 4);
            b.UnlockBits(d);
            return img;
        }
    }

    public static void Save(Img img, string path)
    {
        using (var b = new Bitmap(img.W, img.H, PixelFormat.Format32bppArgb))
        {
            var d = b.LockBits(new Rectangle(0, 0, img.W, img.H), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < img.H; y++) Marshal.Copy(img.Px, y * img.W * 4, d.Scan0 + y * d.Stride, img.W * 4);
            b.UnlockBits(d);
            b.Save(path, ImageFormat.Png);
        }
    }

    // A UI sprite: single, no mipmaps, alpha is transparency, full-rect mesh (needed for 9-slicing).
    public static void WriteMeta(string path, Vector4 border, int maxSize = 2048)
    {
        string guid = Guid.NewGuid().ToString("N");
        if (File.Exists(path))
        {
            Match m = Regex.Match(File.ReadAllText(path), @"guid: ([0-9a-f]{32})");
            if (m.Success) guid = m.Groups[1].Value;
        }
        string meta = string.Join("\n", new[]
        {
            "fileFormatVersion: 2", "guid: " + guid, "TextureImporter:", "  internalIDToNameTable: []", "  externalObjects: {}",
            "  serializedVersion: 13", "  mipmaps:", "    mipMapMode: 0", "    enableMipMap: 0", "    sRGBTexture: 1",
            "    linearTexture: 0", "  isReadable: 0", "  maxTextureSize: " + maxSize, "  textureSettings:", "    serializedVersion: 2",
            "    filterMode: 1", "    aniso: 1", "    mipBias: 0", "    wrapU: 1", "    wrapV: 1", "    wrapW: 1", "  nPOTScale: 0",
            "  spriteMode: 1", "  spriteExtrude: 1", "  spriteMeshType: 0", "  alignment: 0", "  spritePivot: {x: 0.5, y: 0.5}",
            "  spritePixelsToUnits: 100",
            string.Format(CultureInfo.InvariantCulture, "  spriteBorder: {{x: {0}, y: {1}, z: {2}, w: {3}}}", border.L, border.B, border.R, border.T),
            "  alphaUsage: 1", "  alphaIsTransparency: 1", "  textureType: 8", "  textureShape: 1", "  platformSettings:",
            "  - serializedVersion: 4", "    buildTarget: DefaultTexturePlatform", "    maxTextureSize: " + maxSize, "    resizeAlgorithm: 0",
            "    textureFormat: -1", "    textureCompression: 1", "    compressionQuality: 50", "    crunchedCompression: 0",
            "  userData: ", "  assetBundleName: ", "  assetBundleVariant: ", ""
        });
        File.WriteAllText(path, meta, new UTF8Encoding(false));
    }
}
