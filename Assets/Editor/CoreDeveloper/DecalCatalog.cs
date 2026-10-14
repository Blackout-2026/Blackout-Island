using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One sign or floor marking: its size, colours and text (docs/reference/core-developer.md 7.8).</summary>
    public sealed class DecalSpec
    {
        public readonly string Name; // Prop_Sign_<Name> is the prefab, T_Sign_<Texture>_BaseColor the texture
        public readonly float Width; // metres
        public readonly float Height;
        public readonly bool Floor; // lies flat on the floor, otherwise hangs on a wall
        public readonly string Background;
        public readonly string Foreground;
        public readonly string Header; // optional bar across the top, with its own colours
        public readonly string HeaderBackground;
        public readonly string HeaderForeground;
        public readonly string[] Lines;
        public readonly string Texture; // shared by name when two decals use the same picture (the hazard stripes)
        public readonly float StripeMetres; // > 0: the picture is the hazard-stripe tile, repeated every this many metres
        public readonly int Count;

        public DecalSpec(string name, float width, float height, bool floor, string background, string foreground, string[] lines,
            string header = null, string headerBackground = null, string headerForeground = null, string texture = null,
            float stripeMetres = 0f, int count = 1)
        {
            Name = name;
            Width = width;
            Height = height;
            Floor = floor;
            Background = background;
            Foreground = foreground;
            Lines = lines;
            Header = header;
            HeaderBackground = headerBackground;
            HeaderForeground = headerForeground;
            Texture = texture ?? name;
            StripeMetres = stripeMetres;
            Count = count;
        }

        public string PrefabName => "Prop_Sign_" + Name;
        public string MaterialName => "M_Sign_" + Name;
        public string TexturePrefix => "T_Sign_" + Texture + "_";

        public PropEntry Entry => new PropEntry(PrefabName, PropCatalog.PropsFolder, PropSource.Generated, PropGroup.Sign,
            Floor ? PropPivot.BaseCentre : PropPivot.WallFace,
            Floor ? new Vector3(Width, 0f, Height) : new Vector3(Width, Height, 0f), Count,
            Floor ? "Lies flat, the picture's top edge toward +Z" : "Hangs on a wall, the picture faces +Z");
    }

    /// <summary>
    /// The signs and floor markings of docs/reference/core-developer.md 7.8 that the level uses, with the prototype's
    /// sizes and colours. Each becomes a quad prefab, a material and a texture. Left out on purpose: the facility plan and
    /// the outbreak screen (a drawn map, not text) and the generator's own labels (those belong to the generator model).
    /// </summary>
    public static class DecalCatalog
    {
        private const string Yellow = "#e8b52a";
        private const string Gold = "#d9a521";
        private const string Cream = "#e6e2d6";
        private const string Navy = "#1d2633";
        private const string OffWhite = "#e8e8e0";
        private const string Black = "#111111";
        private const string Red = "#b3241b";

        public static readonly IReadOnlyList<DecalSpec> All = new List<DecalSpec>
        {
            new DecalSpec("DangerHighVoltage", 1.0f, 0.62f, false, Yellow, Black, new[] { "HIGH", "VOLTAGE" }, "DANGER", Black, Yellow, count: 2),
            new DecalSpec("BiohazardLab", 0.95f, 0.6f, false, Cream, Black, new[] { "LAB-01", "CONTAINMENT" }, "BIOHAZARD", Gold, Black, count: 2),
            new DecalSpec("BiohazardLevel4", 0.95f, 0.6f, false, Cream, Black, new[] { "LEVEL 4", "CONTAINMENT" }, "BIOHAZARD", Gold, Black),
            new DecalSpec("FireHose", 0.6f, 0.3f, false, Red, "#ffffff", new[] { "FIRE HOSE" }),
            new DecalSpec("DieselFlammable", 1.6f, 0.5f, false, Gold, Black, new[] { "DIESEL FLAMMABLE" }, count: 2),
            new DecalSpec("AuthorisedEngineers", 0.9f, 0.45f, false, Navy, OffWhite, new[] { "AUTHORISED", "ENGINEERS ONLY" }),
            new DecalSpec("AllPersonnel", 0.9f, 0.45f, false, Navy, OffWhite, new[] { "ALL PERSONNEL", "PRESENT ID" }),
            new DecalSpec("SecurityCheckpoint", 4.6f, 0.72f, false, "#c9ccc8", "#2a2d28", new[] { "SECURITY CHECKPOINT B1-S" }),
            new DecalSpec("LevelB1", 1.6f, 0.5f, false, "#e2e0d8", "#2a2d28", new[] { "LEVEL B1" }),
            new DecalSpec("SignGenerator", 1.1f, 0.28f, false, Gold, Black, new[] { "GENERATOR >" }),
            new DecalSpec("SignLaboratory", 1.2f, 0.3f, false, Cream, "#2a2d28", new[] { "LABORATORY >" }),
            new DecalSpec("Restricted", 1.2f, 0.75f, false, Cream, Black, new[] { "AUTHORISED", "PERSONNEL ONLY" }, "RESTRICTED", Red, "#ffffff", count: 6),
            new DecalSpec("MedSupply", 0.9f, 0.45f, false, "#d8d2c0", "#3a3a34", new[] { "MED-SUPPLY", "04-117" }, count: 2),
            new DecalSpec("PrivateIsland", 1.4f, 0.88f, false, Cream, Black, new[] { "NO LANDING" }, "PRIVATE ISLAND", Black, Cream),
            new DecalSpec("GeneratorG1", 5.0f, 1.0f, false, "#2b2e31", Gold, new[] { "GENERATOR G-1" }),
            new DecalSpec("LabProject", 5.0f, 1.0f, false, "#cfd3cc", "#1f4d4f", new[] { "LAB-01 // PROJECT LAZARUS" }),
            new DecalSpec("KeepClear", 3.2f, 0.8f, true, Gold, Black, new[] { "KEEP CLEAR" }),
            new DecalSpec("HazardStripeRamp", 4.0f, 0.4f, true, Yellow, Black, new string[0], texture: "HazardStripe", stripeMetres: 0.4f),
            new DecalSpec("HazardStripeLab", 3.2f, 0.8f, true, Yellow, Black, new string[0], texture: "HazardStripe", stripeMetres: 0.4f),
        };

        public static IEnumerable<PropEntry> Entries
        {
            get
            {
                foreach (DecalSpec spec in All)
                {
                    yield return spec.Entry;
                }
            }
        }
    }
}
