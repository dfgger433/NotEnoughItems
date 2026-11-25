using System.Buffers.Binary;
using System.IO.Compression;
using NotEnoughItems;

internal static class GuiArtworkTests
{
    internal static void BackAndRectangularTabs()
    {
        var pixel = ReadRgba(NeiAssets.StatesAtlas);
        var back = ModernityFrames.Find("back")!.Value;
        var colors = Enumerable.Range(0, back.Height).SelectMany(y => Enumerable.Range(0, back.Width)
            .Select(x => pixel(back.X + x, back.Y + y))).ToArray();
        Check(colors.Count(c => c == 0xF2F2F2FFu) == 46 && colors.Count(c => c == 0x4D4D67FFu) == 14 &&
            colors.Count(c => (c & 0xFFu) == 0) == 196, "Back must be the original two-tone sprite, not the generated replacement or an atlas placeholder.");

        var selected = ModernityFrames.Find(NeiAssets.RecipeTabSelected)!.Value;
        var candidate = ModernityFrames.Find(NeiAssets.RecipeTabCandidate)!.Value;
        Check(selected.ResourcePath == NeiAssets.StatesAtlas && candidate.ResourcePath == NeiAssets.StatesAtlas &&
            (selected.X, selected.Y, selected.Width, selected.Height) == (160, 112, 48, 47) &&
            (candidate.X, candidate.Y, candidate.Width, candidate.Height) == (208, 112, 48, 47),
            "Crop the two 48x47 painted areas, excluding the transparent bottom row of each cell.");

        var panel = ReadRgba(NeiAssets.RecipeBackgroundTexture);
        int rim = NeiAssets.RecipeBackgroundTransparentInset;
        uint outline = panel(9, rim), highlight = panel(9, rim + 1), fill = panel(9, rim + 2);
        Check((outline, highlight, fill) == (0x413F54FFu, 0xF2F2F2FFu, 0xCBCCD4FFu),
            "The panel rim starts with outline, highlight and fill in that order.");
        foreach (var frame in new[] { selected, candidate })
        {
            int seam = frame.Y + NeiAssets.RecipeTabHighlightRow;
            for (int x = frame.X; x < frame.X + frame.Width; x++)
            {
                for (int y = frame.Y; y < frame.Y + frame.Height; y++)
                {
                    uint color = pixel(x, y);
                    Check((color & 0xFFu) == 0xFFu && color is not (0xFF00AAFFu or 0xFFBEAAFFu),
                        "Both tab frames are opaque and contain no atlas placeholder pixels.");
                }
                Check((pixel(x, frame.Y + frame.Height) & 0xFFu) == 0,
                    "The row immediately below the painted tab faces is transparent.");
                Check(pixel(x, seam + 1) == fill && pixel(x, seam + 2) == fill,
                    "Both rows below the tab highlight match the panel fill.");
            }
            Check(pixel(frame.X, seam - 1) == outline && pixel(frame.X, seam) == highlight,
                "Each tab edge transitions from outline to the one-row panel highlight.");
        }
        Check(pixel(selected.X + 24, 145) == fill && pixel(candidate.X + 24, 145) == 0x9A9FB4FFu,
            "Selected and candidate bodies still use their own artwork.");
    }

    // Read these noninterlaced 8-bit RGBA or indexed PNGs without native Unity calls.
    private static Func<int, int, uint> ReadRgba(string path)
    {
        using var png = typeof(Catalog).Assembly.GetManifestResourceStream(NeiAssets.ResourceName(path))!;
        using var compressed = new MemoryStream();
        Span<byte> chunk = stackalloc byte[8];
        png.ReadExactly(chunk); // PNG signature, checked by EmbeddedGuiTextures.
        int width = 0, height = 0, bytesPerPixel = 0;
        byte[] palette = [], alpha = [];
        while (true)
        {
            png.ReadExactly(chunk);
            byte[] data = new byte[BinaryPrimitives.ReadInt32BigEndian(chunk)];
            png.ReadExactly(data);
            png.Position += 4; // CRC
            if (chunk[4..].SequenceEqual("IEND"u8)) break;
            if (chunk[4..].SequenceEqual("IDAT"u8)) compressed.Write(data);
            if (chunk[4..].SequenceEqual("PLTE"u8)) palette = data;
            if (chunk[4..].SequenceEqual("tRNS"u8)) alpha = data;
            if (!chunk[4..].SequenceEqual("IHDR"u8)) continue;
            width = BinaryPrimitives.ReadInt32BigEndian(data);
            height = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(4));
            Check(data[8] == 8 && data[9] is 3 or 6 && data[10] == 0 && data[11] == 0 && data[12] == 0,
                "Expected indexed or RGBA 8-bit PNG without interlacing: " + path);
            bytesPerPixel = data[9] == 3 ? 1 : 4;
        }
        compressed.Position = 0;
        using var zlib = new ZLibStream(compressed, CompressionMode.Decompress);
        int stride = width * bytesPerPixel;
        var pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            int filter = zlib.ReadByte(), row = y * stride;
            zlib.ReadExactly(pixels.AsSpan(row, stride));
            for (int x = 0; x < stride; x++)
            {
                int at = row + x;
                int left = x >= bytesPerPixel ? pixels[at - bytesPerPixel] : 0, up = y > 0 ? pixels[at - stride] : 0;
                int diagonal = y > 0 && x >= bytesPerPixel ? pixels[at - stride - bytesPerPixel] : 0;
                int prediction = filter switch
                {
                    0 => 0, 1 => left, 2 => up, 3 => (left + up) / 2, 4 => Paeth(left, up, diagonal),
                    _ => throw new InvalidDataException("Unsupported PNG filter.")
                };
                pixels[at] = unchecked((byte)(pixels[at] + prediction));
            }
        }
        if (bytesPerPixel == 4)
            return (x, y) => BinaryPrimitives.ReadUInt32BigEndian(pixels.AsSpan(y * stride + x * 4));
        return (x, y) =>
        {
            int index = pixels[y * stride + x], rgb = index * 3;
            return ((uint)palette[rgb] << 24) | ((uint)palette[rgb + 1] << 16) | ((uint)palette[rgb + 2] << 8) |
                (index < alpha.Length ? alpha[index] : 255u);
        };
    }

    private static int Paeth(int left, int up, int diagonal)
    {
        int prediction = left + up - diagonal;
        int a = Math.Abs(prediction - left), b = Math.Abs(prediction - up), c = Math.Abs(prediction - diagonal);
        return a <= b && a <= c ? left : b <= c ? up : diagonal;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
