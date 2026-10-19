using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// One environment surface: its look without textures (the reference's dark → light albedo range and tint), its
    /// roughness and metallic, and how often its texture repeats. Every value is copied from
    /// docs/reference/world-builder.md section 3, except where a comment in <see cref="SurfaceLibrary"/> says otherwise.
    /// </summary>
    public sealed class SurfaceSpec
    {
        public readonly string Name;
        public readonly string Reference; // the prototype material it comes from
        public readonly Color Dark;
        public readonly Color Light;
        public readonly Color Tint;
        public readonly float Roughness;
        public readonly float Metallic;
        public readonly float RepeatsPerMetre;
        public readonly float Alpha;
        public readonly bool DoubleSided;

        public SurfaceSpec(string name, string reference, string dark, string light, string tint, float roughness,
            float metallic, float repeatsPerMetre, float alpha = 1f, bool doubleSided = false)
        {
            Name = name;
            Reference = reference;
            Dark = Hex(dark);
            Light = Hex(light);
            Tint = Hex(tint);
            Roughness = roughness;
            Metallic = metallic;
            RepeatsPerMetre = repeatsPerMetre;
            Alpha = alpha;
            DoubleSided = doubleSided;
        }

        /// <summary>URP Lit uses smoothness, the opposite of the prototype's roughness.</summary>
        public float Smoothness => 1f - Roughness;

        public bool Transparent => Alpha < 1f;

        /// <summary>The colour used when there is no BaseColor texture: the middle of the albedo range, times the tint.</summary>
        public Color FlatColour
        {
            get
            {
                Color colour = Color.Lerp(Dark, Light, 0.5f) * Tint;
                colour.a = Alpha;
                return colour;
            }
        }

        /// <summary>The colour multiplied over a BaseColor texture.</summary>
        public Color TextureTint
        {
            get
            {
                Color colour = Tint;
                colour.a = Alpha;
                return colour;
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color colour);
            return colour;
        }
    }

    /// <summary>
    /// The twelve environment surfaces of the reference, plus the grating floor, that Build Material Library turns into Env_&lt;Name&gt; materials.
    /// RepeatsPerMetre is the prototype's world-UV scale for that surface (geometry.js worldUV, and the terrain shader
    /// for the four outdoor ones), so it assumes UVs in metres, which Apply Materials gives the blockout.
    /// </summary>
    public static class SurfaceLibrary
    {
        public static readonly IReadOnlyList<SurfaceSpec> All = new[]
        {
            new SurfaceSpec("Sand", "sand (terrain)", "#6F6450", "#9A8C70", "#FFFFFF", 0.92f, 0f, 0.22f),
            new SurfaceSpec("Grass", "grass (terrain)", "#1F2A15", "#3A4A24", "#FFFFFF", 0.92f, 0f, 0.30f),
            new SurfaceSpec("ForestFloor", "forest litter (terrain)", "#241C14", "#40331F", "#FFFFFF", 0.92f, 0f, 0.28f),
            new SurfaceSpec("Rock", "rock (terrain)", "#3F3E3B", "#7A7872", "#B8B8B4", 0.8f, 0f, 0.12f),
            new SurfaceSpec("Concrete", "concrete / wallPanel walls", "#5F5E59", "#86857D", "#FFFFFF", 1f, 0f, 0.3f),
            new SurfaceSpec("ConcreteStained", "floorConcrete (oil stains)", "#4C4D4A", "#6D6E69", "#FFFFFF", 1f, 0f, 0.25f),
            new SurfaceSpec("MetalPlate", "metalPlate + metal, diamond plate", "#4A4D4F", "#6A6E70", "#FFFFFF", 1f, 0.65f, 0.6f),
            // Rust is only on cylinders in the prototype, so it has no world-UV scale: 0.5 (one repeat per 2 m) is chosen.
            new SurfaceSpec("Rust", "rust", "#3E2214", "#8A4A24", "#FFFFFF", 1f, 0.3f, 0.5f),
            new SurfaceSpec("Tile", "tiles (lab floor)", "#B9C0C1", "#D8DFE0", "#D8DCDC", 1f, 0f, 0.25f),
            new SurfaceSpec("HazardStripe", "hazard", "#D9A521", "#D9A521", "#FFFFFF", 0.75f, 0f, 1.5f),
            // Glass and Fabric are single colours in the reference with no world-UV scale: one repeat per metre.
            new SurfaceSpec("Glass", "glass", "#9FC9D0", "#9FC9D0", "#FFFFFF", 0.06f, 0.2f, 1f, alpha: 0.12f, doubleSided: true),
            // The reference tints a neutral fabric texture with the sandbag colour; a CC0 fabric is already coloured,
            // so the sandbag colour is used flat and the texture is not tinted.
            new SurfaceSpec("Fabric", "sandbag / rope (fabric)", "#7D7158", "#7D7158", "#FFFFFF", 1f, 0f, 1f),
            // Not in the reference: the generator-room floor (step C7), a welded steel grating over dark holes. The texture tile
            // is 1 m (Art/Blender/Scripts/make_metal_grating.py), so it repeats once per metre.
            new SurfaceSpec("MetalGrating", "generator-room floor grating (C7)", "#2B2E30", "#6F7479", "#FFFFFF", 0.55f, 0.8f, 1f),
        };

        public static SurfaceSpec Find(string name)
        {
            foreach (SurfaceSpec spec in All)
            {
                if (spec.Name == name)
                {
                    return spec;
                }
            }

            return null;
        }
    }
}
