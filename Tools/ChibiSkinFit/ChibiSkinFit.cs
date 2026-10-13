// Run with FitSkins.ps1 (Windows PowerShell 5.1, System.Drawing). Not part of the Unity project.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// Makes every ChibiCharacterSkin interchangeable: each pose sprite's rect is trimmed to the character and its pivot is
// put at the bottom of the character, under its centre of mass (where it sits). The pixels of sheet textures are not
// touched (only the sprite rects/pivots in the .meta); single-sprite textures are cropped losslessly instead, because a
// single sprite always covers its whole texture. Code then draws any skin at the same height on the same seat point.
public static class ChibiSkinFit
{
    public const int Padding = 2;
    const byte Solid = 40;

    public sealed class Fit
    {
        public string Field, Png, SpriteName; public long Id; public bool Single; public int TextureHeight;
        public Rectangle Cell, Box;   // image px, top-left origin
        public float PivotX;          // image px (centre of mass)
        public string Note = "";
    }

    static readonly Dictionary<string, string> guidToPng = new Dictionary<string, string>();

    public static void Index(string root)
    {
        guidToPng.Clear();
        foreach (string meta in Directory.GetFiles(root, "*.png.meta", SearchOption.AllDirectories))
        {
            Match m = Regex.Match(File.ReadAllText(meta), @"guid: ([0-9a-f]{32})");
            if (m.Success) guidToPng[m.Groups[1].Value] = meta.Substring(0, meta.Length - 5);
        }
    }

    sealed class Mask { public int W, H; public byte[] A; }

    static Mask Load(string png)
    {
        using (var file = new Bitmap(png))
        using (var b = file.Clone(new Rectangle(0, 0, file.Width, file.Height), PixelFormat.Format32bppArgb))
        {
            var d = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new byte[d.Stride * b.Height];
            Marshal.Copy(d.Scan0, px, 0, px.Length);
            b.UnlockBits(d);
            var m = new Mask { W = b.Width, H = b.Height, A = new byte[b.Width * b.Height] };
            for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) m.A[y * b.Width + x] = px[y * d.Stride + x * 4 + 3];
            return m;
        }
    }

    /// <summary>Reads the skin asset and measures every referenced sprite. The current rect is used as the search cell.</summary>
    public static List<Fit> Measure(string skinAsset)
    {
        var fits = new List<Fit>();
        var masks = new Dictionary<string, Mask>();
        foreach (Match m in Regex.Matches(File.ReadAllText(skinAsset), @"\n  (\w+): \{fileID: (-?\d+), guid: ([0-9a-f]{32})"))
        {
            string png;
            if (!guidToPng.TryGetValue(m.Groups[3].Value, out png)) continue;
            Mask mask;
            if (!masks.TryGetValue(png, out mask)) { mask = Load(png); masks[png] = mask; }
            var f = new Fit { Field = m.Groups[1].Value, Png = png, Id = long.Parse(m.Groups[2].Value), TextureHeight = mask.H };
            string meta = File.ReadAllText(png + ".meta");
            f.Single = Regex.IsMatch(meta, @"\n  spriteMode: 1\b");
            if (f.Single) f.Cell = new Rectangle(0, 0, mask.W, mask.H);
            else
            {
                string entry = SpriteEntry(meta, f.Id);
                if (entry == null) throw new InvalidDataException("sprite " + f.Id + " not in " + png);
                f.SpriteName = Regex.Match(entry, @"name: (\S+)").Groups[1].Value;
                Func<string, int> num = k => (int)Math.Round(float.Parse(Regex.Match(entry, @"\n\s+" + k + @": (-?[\d.]+)").Groups[1].Value, CultureInfo.InvariantCulture));
                int x = num("x"), y = num("y"), w = num("width"), h = num("height");
                f.Cell = new Rectangle(x, mask.H - y - h, w, h);
            }
            Measure(f, mask);
            fits.Add(f);
        }
        return fits;
    }

    // The character = the biggest blob plus anything big enough to be part of it (a lifted tankard, a tail tip).
    // Small blobs that touch the cell edge are paint from the neighbouring cell and are ignored.
    static void Measure(Fit f, Mask m)
    {
        Rectangle c = f.Cell;
        const int step = 2;
        int gw = (c.Width + step - 1) / step, gh = (c.Height + step - 1) / step;
        var on = new bool[gw * gh];
        for (int gy = 0; gy < gh; gy++)
            for (int gx = 0; gx < gw; gx++)
            {
                bool any = false;
                for (int y = c.Top + gy * step; y < Math.Min(c.Bottom, c.Top + gy * step + step) && !any; y++)
                    for (int x = c.Left + gx * step; x < Math.Min(c.Right, c.Left + gx * step + step); x++)
                        if (m.A[y * m.W + x] > Solid) { any = true; break; }
                on[gy * gw + gx] = any;
            }
        var label = new int[gw * gh];
        var sizes = new List<int> { 0 }; var edge = new List<bool> { false };
        var queue = new Queue<int>();
        for (int i = 0; i < on.Length; i++)
        {
            if (!on[i] || label[i] != 0) continue;
            int id = sizes.Count, size = 0; bool touches = false;
            label[i] = id; queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int p = queue.Dequeue(); size++;
                int px = p % gw, py = p / gw;
                if (px == 0 || py == 0 || px == gw - 1 || py == gh - 1) touches = true;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = px + dx, ny = py + dy;
                        if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue;
                        int n = ny * gw + nx;
                        if (on[n] && label[n] == 0) { label[n] = id; queue.Enqueue(n); }
                    }
            }
            sizes.Add(size); edge.Add(touches);
        }
        int biggest = 0;
        for (int i = 1; i < sizes.Count; i++) if (sizes[i] > biggest) biggest = sizes[i];
        var keep = new bool[sizes.Count];
        int dropped = 0;
        for (int i = 1; i < sizes.Count; i++)
        {
            keep[i] = sizes[i] == biggest || (sizes[i] >= biggest * .02f && !(edge[i] && sizes[i] < biggest * .15f));
            if (!keep[i] && sizes[i] > 4) dropped++;
        }
        int l = int.MaxValue, r = -1, t = int.MaxValue, b = -1; double sx = 0; long n2 = 0;
        for (int y = c.Top; y < c.Bottom; y++)
            for (int x = c.Left; x < c.Right; x++)
            {
                if (m.A[y * m.W + x] <= Solid) continue;
                int id = label[((y - c.Top) / step) * gw + (x - c.Left) / step];
                if (id == 0 || !keep[id]) continue;
                l = Math.Min(l, x); r = Math.Max(r, x); t = Math.Min(t, y); b = Math.Max(b, y); sx += x; n2++;
            }
        f.Box = r < 0 ? c : new Rectangle(l, t, r - l + 1, b - t + 1);
        f.PivotX = n2 > 0 ? (float)(sx / n2) : c.Left + c.Width / 2f;
        if (dropped > 0) f.Note = "ignored " + dropped + " stray blob(s)";
    }

    static string SpriteEntry(string meta, long id)
    {
        int at = meta.IndexOf("\n      internalID: " + id + "\n", StringComparison.Ordinal);
        if (at < 0) at = meta.IndexOf("\n      internalID: " + id + "\r\n", StringComparison.Ordinal);
        if (at < 0) return null;
        int start = meta.LastIndexOf("\n    - serializedVersion: 2", at, StringComparison.Ordinal);
        return start < 0 ? null : meta.Substring(start, at - start);
    }

    /// <summary>Writes the trimmed rect and sit pivot. Returns a one-line report.</summary>
    public static string Apply(Fit f)
    {
        string metaPath = f.Png + ".meta";
        string meta = File.ReadAllText(metaPath);
        Rectangle r = Rectangle.Intersect(Rectangle.Inflate(f.Box, Padding, Padding), f.Cell);
        float pivotX = (f.PivotX - r.Left) / r.Width, pivotY = (r.Bottom - (f.Box.Bottom)) / (float)r.Height;
        string piv = string.Format(CultureInfo.InvariantCulture, "{{x: {0:0.#####}, y: {1:0.#####}}}", pivotX, pivotY);
        if (f.Single)
        {
            if (r != f.Cell) Crop(f.Png, r);
            meta = Regex.Replace(meta, @"\n  alignment: \d+", "\n  alignment: 9");
            meta = Regex.Replace(meta, @"\n  spritePivot: \{[^}]*\}", "\n  spritePivot: " + piv);
            int at = meta.IndexOf("\n      internalID: " + f.Id, StringComparison.Ordinal);
            if (at >= 0) // the importer's copy of the single sprite: keep it consistent
            {
                int start = meta.LastIndexOf("\n    - serializedVersion: 2", at, StringComparison.Ordinal);
                string entry = meta.Substring(start, at - start);
                string fixedEntry = Regex.Replace(entry, @"\n      alignment: \d+", "\n      alignment: 9");
                fixedEntry = Regex.Replace(fixedEntry, @"\n      pivot: \{[^}]*\}", "\n      pivot: " + piv);
                fixedEntry = SetRect(fixedEntry, 0, 0, r.Width, r.Height);
                meta = meta.Substring(0, start) + fixedEntry + meta.Substring(at);
            }
        }
        else
        {
            string entry = SpriteEntry(meta, f.Id);
            int start = meta.IndexOf(entry, StringComparison.Ordinal);
            string fixedEntry = SetRect(entry, r.Left, f.TextureHeight - r.Bottom, r.Width, r.Height);
            fixedEntry = Regex.Replace(fixedEntry, @"\n      alignment: \d+", "\n      alignment: 9");
            fixedEntry = Regex.Replace(fixedEntry, @"\n      pivot: \{[^}]*\}", "\n      pivot: " + piv);
            meta = meta.Substring(0, start) + fixedEntry + meta.Substring(start + entry.Length);
        }
        File.WriteAllText(metaPath, meta, new UTF8Encoding(false));
        return string.Format("{0,-17} {1,-34} cell {2}x{3} -> {4}x{5} pivot {6} {7}", f.Field,
            f.SpriteName ?? Path.GetFileName(f.Png), f.Cell.Width, f.Cell.Height, r.Width, r.Height, piv, f.Note);
    }

    static string SetRect(string entry, int x, int y, int w, int h)
    {
        int at = entry.IndexOf("\n      rect:", StringComparison.Ordinal);
        string head = entry.Substring(0, at), tail = entry.Substring(at);
        tail = ReplaceFirst(tail, @"\n        x: [-\d.]+", "\n        x: " + x);
        tail = ReplaceFirst(tail, @"\n        y: [-\d.]+", "\n        y: " + y);
        tail = ReplaceFirst(tail, @"\n        width: [-\d.]+", "\n        width: " + w);
        tail = ReplaceFirst(tail, @"\n        height: [-\d.]+", "\n        height: " + h);
        return head + tail;
    }

    static string ReplaceFirst(string s, string pattern, string value) { return new Regex(pattern).Replace(s, value, 1); }

    static void Crop(string png, Rectangle r)
    {
        Bitmap cropped;
        using (var file = new Bitmap(png))
        using (var b = file.Clone(new Rectangle(0, 0, file.Width, file.Height), PixelFormat.Format32bppArgb))
            cropped = b.Clone(r, PixelFormat.Format32bppArgb);
        using (cropped) cropped.Save(png, ImageFormat.Png);
    }
}
