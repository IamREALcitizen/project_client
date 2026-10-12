// Sprite import metadata only: source PNGs are preserved without raster modification.
using System;
using UnityEditor;
using UnityEngine;

public static class PrepareRoomArt
{
    public static void Run()
    {
        Import("ParchmentRefined", 80);
        Import("FrameRefined", 130);
        Import("HelmRefined", 0);
    }

    static void Import(string name, int border)
    {
        string path = "Assets/Resources/RoomLobby/" + name + ".png";
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var pixels = texture.GetPixels32();
        int left = texture.width, right = -1, bottom = texture.height, top = -1;
        for (int y = 0; y < texture.height; y++)
            for (int x = 0; x < texture.width; x++)
                if (pixels[y * texture.width + x].a > 16)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y);
                }
        if (right < left) throw new Exception("Empty room art: " + name);
        left = Math.Max(0, left - 2); bottom = Math.Max(0, bottom - 2);
        right = Math.Min(texture.width - 1, right + 2); top = Math.Min(texture.height - 1, top + 2);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
#pragma warning disable CS0618
        importer.spritesheet = new[] { new SpriteMetaData {
            name = name, rect = new Rect(left, bottom, right - left + 1, top - bottom + 1),
            alignment = 0, pivot = new Vector2(.5f, .5f), border = Vector4.one * border
        } };
#pragma warning restore CS0618
        importer.isReadable = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.SaveAndReimport();
        Debug.Log($"Room art imported: {name}, rect {left},{bottom}–{right},{top}, border {border}");
    }
}
