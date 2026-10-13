using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

// The white shapes the game screen tints (HudShapes' rounded rectangle and its outline, NightFxSprites' circle) as sprite
// assets, so the layouts baked into a scene can keep them (a sprite made at run time is not saved with the scene).
// Same pixels as the code makes when the asset is missing. GUIDs are kept on a rerun.
public static class HudShapeArt
{
    const int Size = 64, Corner = 16, Border = 20; // HudShapes
    const float Line = 3f;

    public static void Run(string outDir)
    {
        Write(outDir, "HudRoundedFill", Size, Size, Border, (x, y) => Rounded(x, y, false));
        Write(outDir, "HudRoundedOutline", Size, Size, Border, (x, y) => Rounded(x, y, true));
        Write(outDir, "HudCircle", 128, 128, 0, (x, y) =>
        {
            float u = (x + 0.5f) / 128f * 2f - 1f, v = (y + 0.5f) / 128f * 2f - 1f;
            return (1f - (float)Math.Sqrt(u * u + v * v)) * 64f;
        });
    }

    // Signed distance to the rounded rectangle's edge (negative inside), as HudShapes.Make.
    static float Rounded(int x, int y, bool outlineOnly)
    {
        float half = Size * 0.5f;
        float qx = Math.Abs(x + 0.5f - half) - (half - Corner);
        float qy = Math.Abs(y + 0.5f - half) - (half - Corner);
        float ox = Math.Max(qx, 0f), oy = Math.Max(qy, 0f);
        float d = (float)Math.Sqrt(ox * ox + oy * oy) + Math.Min(Math.Max(qx, qy), 0f) - Corner;
        float a = Clamp01(0.5f - d);
        if (outlineOnly) a *= 1f - Clamp01(0.5f - (d + Line));
        return a;
    }

    static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }

    // alpha(x, y) with y up, like a Unity texture; white with that alpha.
    static void Write(string outDir, string name, int w, int h, int border, Func<int, int, float> alpha)
    {
        string path = Path.Combine(outDir, name + ".png");
        var px = new byte[w * h * 4];
        for (int row = 0; row < h; row++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (row * w + x) * 4;
                px[i] = px[i + 1] = px[i + 2] = 255;
                px[i + 3] = (byte)(int)Math.Round(Clamp01(alpha(x, h - 1 - row)) * 255f);
            }
        }
        using (var b = new Bitmap(w, h, PixelFormat.Format32bppArgb))
        {
            var d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int row = 0; row < h; row++) Marshal.Copy(px, row * w * 4, d.Scan0 + row * d.Stride, w * 4);
            b.UnlockBits(d);
            b.Save(path, ImageFormat.Png);
        }
        WriteMeta(path + ".meta", border);
        Console.WriteLine(path);
    }

    // As HudArt.WriteMeta (a UI sprite, full-rect mesh for 9-slicing), but uncompressed: the shapes are tiny and their
    // soft edges must stay exactly as made.
    static void WriteMeta(string path, int border)
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
            "    linearTexture: 0", "  isReadable: 0", "  maxTextureSize: 2048", "  textureSettings:", "    serializedVersion: 2",
            "    filterMode: 1", "    aniso: 1", "    mipBias: 0", "    wrapU: 1", "    wrapV: 1", "    wrapW: 1", "  nPOTScale: 0",
            "  spriteMode: 1", "  spriteExtrude: 1", "  spriteMeshType: 0", "  alignment: 0", "  spritePivot: {x: 0.5, y: 0.5}",
            "  spritePixelsToUnits: 100",
            string.Format("  spriteBorder: {{x: {0}, y: {0}, z: {0}, w: {0}}}", border),
            "  alphaUsage: 1", "  alphaIsTransparency: 1", "  textureType: 8", "  textureShape: 1", "  platformSettings:",
            "  - serializedVersion: 4", "    buildTarget: DefaultTexturePlatform", "    maxTextureSize: 2048", "    resizeAlgorithm: 0",
            "    textureFormat: -1", "    textureCompression: 0", "    compressionQuality: 50", "    crunchedCompression: 0",
            "  userData: ", "  assetBundleName: ", "  assetBundleVariant: ", ""
        });
        File.WriteAllText(path, meta, new UTF8Encoding(false));
    }
}
