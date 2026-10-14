using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Draws a <see cref="DecalSpec"/> into pixels: a coloured plate, an optional header bar, and centred lines of dot-matrix
    /// text. The picture is laid out in metres, not pixels, so the lettering keeps its proportions on the quad whatever the
    /// (power of two) texture size. Pure code: no assets, same bytes every run.
    /// </summary>
    public static class DecalRenderer
    {
        public const float PixelsPerMetre = 160f;
        public const int MinSize = 64;
        public const int MaxSize = 1024;
        public const int StripeTileSize = 64;
        private const int LineGapDots = 2;

        /// <summary>The texture size for a decal: about 160 pixels per metre, each side rounded to a power of two.</summary>
        public static void SizeOf(DecalSpec spec, out int width, out int height)
        {
            if (spec.StripeMetres > 0f)
            {
                width = StripeTileSize;
                height = StripeTileSize;
                return;
            }

            width = Snap(spec.Width * PixelsPerMetre);
            height = Snap(spec.Height * PixelsPerMetre);
        }

        public static Color32[] Render(DecalSpec spec, out int width, out int height)
        {
            SizeOf(spec, out width, out height);
            return spec.StripeMetres > 0f ? RenderStripeTile(spec, width) : RenderText(spec, width, height);
        }

        // One tile of diagonal yellow and black bands. (x + y) repeats every tile, so the tile joins its neighbours
        // seamlessly; the material's tiling repeats it every StripeMetres.
        private static Color32[] RenderStripeTile(DecalSpec spec, int size)
        {
            Color32 yellow = Parse(spec.Background);
            Color32 black = Parse(spec.Foreground);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = (x + y) % size < size / 2 ? yellow : black;
                }
            }

            return pixels;
        }

        private static Color32[] RenderText(DecalSpec spec, int width, int height)
        {
            float w = spec.Width;
            float h = spec.Height;
            float margin = 0.07f * Mathf.Min(w, h);
            bool hasHeader = !string.IsNullOrEmpty(spec.Header);
            float headerHeight = hasHeader ? 0.3f * h : 0f;

            Block header = hasHeader
                ? Layout(new[] { spec.Header }, margin, 0f, w - 2f * margin, headerHeight, margin * 0.5f)
                : default;
            Block body = Layout(spec.Lines, margin, headerHeight, w - 2f * margin, h - headerHeight, margin);

            Color32 background = Parse(spec.Background);
            Color32 foreground = Parse(spec.Foreground);
            Color32 headerBackground = hasHeader ? Parse(spec.HeaderBackground) : background;
            Color32 headerForeground = hasHeader ? Parse(spec.HeaderForeground) : foreground;

            var pixels = new Color32[width * height];
            for (int ty = 0; ty < height; ty++)
            {
                float my = (height - (ty + 0.5f)) / height * h; // metres from the top; texture row 0 is the bottom
                for (int tx = 0; tx < width; tx++)
                {
                    float mx = (tx + 0.5f) / width * w;
                    Color32 colour = my < headerHeight ? headerBackground : background;
                    if (hasHeader && IsLit(header, mx, my, w))
                    {
                        colour = headerForeground;
                    }
                    else if (IsLit(body, mx, my, w))
                    {
                        colour = foreground;
                    }

                    pixels[ty * width + tx] = colour;
                }
            }

            return pixels;
        }

        // A group of text lines that share one dot size and sit in a rectangle of the plate.
        private struct Block
        {
            public string[] Lines;
            public float Dot; // metres per font dot
            public float Top; // metres from the top of the plate to the first line
        }

        private static Block Layout(string[] lines, float areaX, float areaY, float areaW, float areaH, float verticalPad)
        {
            if (lines == null || lines.Length == 0)
            {
                return default;
            }

            int columns = 1;
            foreach (string line in lines)
            {
                columns = Mathf.Max(columns, BitmapFont.WidthInDots(line));
            }

            int rows = lines.Length * BitmapFont.GlyphHeight + (lines.Length - 1) * LineGapDots;
            float usableH = Mathf.Max(0.0001f, areaH - 2f * verticalPad);
            float dot = Mathf.Min(areaW / columns, usableH / rows);
            return new Block { Lines = lines, Dot = dot, Top = areaY + (areaH - rows * dot) / 2f };
        }

        private static bool IsLit(Block block, float mx, float my, float plateWidth)
        {
            if (block.Lines == null)
            {
                return false;
            }

            float pitch = (BitmapFont.GlyphHeight + LineGapDots) * block.Dot;
            for (int i = 0; i < block.Lines.Length; i++)
            {
                float top = block.Top + i * pitch;
                if (my < top || my >= top + BitmapFont.GlyphHeight * block.Dot)
                {
                    continue;
                }

                string line = block.Lines[i];
                float left = (plateWidth - BitmapFont.WidthInDots(line) * block.Dot) / 2f;
                int column = Mathf.FloorToInt((mx - left) / block.Dot);
                if (column < 0 || column >= BitmapFont.WidthInDots(line))
                {
                    return false;
                }

                int within = column % BitmapFont.Advance;
                if (within >= BitmapFont.GlyphWidth)
                {
                    return false; // the gap between two letters
                }

                int row = Mathf.FloorToInt((my - top) / block.Dot);
                return BitmapFont.IsOn(line[column / BitmapFont.Advance], within, row);
            }

            return false;
        }

        private static int Snap(float pixels)
        {
            return Mathf.Clamp(Mathf.ClosestPowerOfTwo(Mathf.RoundToInt(pixels)), MinSize, MaxSize);
        }

        private static Color32 Parse(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color colour);
            return colour;
        }
    }
}
