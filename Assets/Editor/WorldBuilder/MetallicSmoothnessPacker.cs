using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// URP Lit reads metallic from the red channel and smoothness from the alpha channel of one texture
    /// (_MetallicGlossMap); it has no roughness slot. This packs a Roughness map (smoothness = 1 − roughness) and a
    /// Metallic map into that layout. A missing map is replaced by the surface's constant value; green and blue are 0.
    /// The result is at most <c>maxSize</c> on its longer side, sampled bilinearly from the sources.
    /// </summary>
    public static class MetallicSmoothnessPacker
    {
        /// <param name="roughness">Roughness map, or null to use <paramref name="smoothness"/> everywhere.</param>
        /// <param name="metallic">Metallic map, or null to use <paramref name="metallicValue"/> everywhere.</param>
        public static Texture2D Pack(Texture2D roughness, float smoothness, Texture2D metallic, float metallicValue, int maxSize)
        {
            Texture2D source = roughness != null ? roughness : metallic;
            float scale = Mathf.Min(1f, (float)maxSize / Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float m = metallic != null ? metallic.GetPixelBilinear(u, v).r : metallicValue;
                    float s = roughness != null ? 1f - roughness.GetPixelBilinear(u, v).r : smoothness;
                    pixels[y * width + x] = new Color32(ToByte(m), 0, 0, ToByte(s));
                }
            }

            var packed = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            packed.SetPixels32(pixels);
            packed.Apply(false);
            return packed;
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }
    }
}
