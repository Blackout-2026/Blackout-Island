using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>Where a prop's mesh comes from.</summary>
    public enum PropSource
    {
        Blender, // modelled low-poly by me: a primitive placeholder until SM_*.fbx exists
        Cc0Kit, // taken from a CC0 pack: the prefab is added by hand, the palette only lists the slot
        Generated, // a textured quad made by the decal tool
    }

    public enum PropGroup
    {
        Lab,
        Office,
        Power,
        Storage,
        Pickup,
        Weapon,
        Hero,
        Sign,
        Fittings,
    }

    /// <summary>Where the prefab's origin is (docs/art-pipeline.md section 5).</summary>
    public enum PropPivot
    {
        BaseCentre, // centre of the footprint at the bottom face: furniture, anything that stands on a surface
        Centre, // geometric centre: the crate is pushed and rotated by physics
        WallFace, // centre of the face that touches the wall
        Grip, // a weapon viewmodel: the prototype's gun frame (receiver centre), muzzle along +Z; the weapon system places it
    }

    /// <summary>One prefab the Props palette lists: its path, size in metres, pivot rule and how many the level uses.</summary>
    public sealed class PropEntry
    {
        public readonly string Name;
        public readonly string Folder;
        public readonly PropSource Source;
        public readonly PropGroup Group;
        public readonly PropPivot Pivot;
        public readonly Vector3 Size; // width (X), height (Y), depth (Z); the front of a prop looks along +Z
        public readonly int Count;
        public readonly string Note;

        public PropEntry(string name, string folder, PropSource source, PropGroup group, PropPivot pivot, Vector3 size, int count, string note)
        {
            Name = name;
            Folder = folder;
            Source = source;
            Group = group;
            Pivot = pivot;
            Size = size;
            Count = count;
            Note = note;
        }

        public string PrefabPath => $"{Folder}/{Name}.prefab";

        /// <summary>
        /// Where the Blender export goes: Prop_LabBench is SM_Prop_LabBench.fbx, Weapon_Smg_Visual is SM_Weapon_Smg.fbx and
        /// a pickup such as Medkit_Visual is SM_Prop_Medkit.fbx (docs/art-pipeline.md section 2).
        /// </summary>
        public string FbxPath
        {
            get
            {
                string stem = Name.Replace("_Visual", string.Empty);
                if (!stem.StartsWith("Prop_", System.StringComparison.Ordinal) && !stem.StartsWith("Weapon_", System.StringComparison.Ordinal))
                {
                    stem = "Prop_" + stem;
                }

                return $"Assets/Models/Custom/SM_{stem}.fbx";
            }
        }

        public PropSize SizeClass => PropSizes.Classify(Size);

        /// <summary>
        /// The triangle and material limits this prefab is compared with. Props use their size class, weapons the
        /// weapon budget, and the two hero visuals their own (SM_Generator, SM_SecurityRobot).
        /// </summary>
        public ModelBudget Budget
        {
            get
            {
                if (Group == PropGroup.Weapon)
                {
                    return new ModelBudget(6000, 3);
                }

                if (Group == PropGroup.Hero && ModelBudget.TryGet("SM_" + Name.Replace("_Visual", string.Empty), out ModelBudget hero))
                {
                    return hero;
                }

                return PropSizes.BudgetOf(SizeClass);
            }
        }
    }

    /// <summary>
    /// Every prop prefab step C4 knows about, with the numbers from docs/reference/core-developer.md sections 5 to 7.
    /// Sizes are the real-world envelope in Unity axes, so the placeholder, the Blender model and a CC0 replacement can all
    /// be checked against the same box. Counts are how many the prototype's level uses.
    /// </summary>
    public static class PropCatalog
    {
        public const string PropsFolder = "Assets/Prefabs/Props";
        public const string ModelsFolder = "Assets/Prefabs/Models";

        public static readonly IReadOnlyList<PropEntry> All = new List<PropEntry>
        {
            // Modelled in Blender (placeholder primitives until the FBX exists). Section 7.5, the laboratory.
            Prop("ContainmentChamber", PropGroup.Lab, PropPivot.BaseCentre, 3.8f, 2.88f, 3.8f, 1, "Under 2.9 m with its base (level-layout.md); opening faces +Z, the CureSocket's 180 degree yaw turns it to the D-Lab door"),
            Prop("LabBench", PropGroup.Lab, PropPivot.BaseCentre, 3.5f, 0.93f, 1.0f, 4, "White base and dark top; also cover"),
            Prop("Analyser", PropGroup.Lab, PropPivot.BaseCentre, 0.4f, 0.42f, 0.4f, 4, "Sits on a bench"),
            Prop("Microscope", PropGroup.Lab, PropPivot.BaseCentre, 0.18f, 0.34f, 0.3f, 4, "Sits on a bench"),
            Prop("SpecimenFridge", PropGroup.Lab, PropPivot.BaseCentre, 1.3f, 2.1f, 0.8f, 3, "Cyan glowing door on the front (+Z)"),
            Prop("FumeHood", PropGroup.Lab, PropPivot.BaseCentre, 3.0f, 2.6f, 1.0f, 1, "Glass front on +Z"),

            // Section 7.1 to 7.3, the security office, lobby and corridor.
            Prop("Desk", PropGroup.Office, PropPivot.BaseCentre, 3.0f, 0.78f, 1.0f, 2, "Drawer unit on the right"),
            Prop("DeskGuard", PropGroup.Office, PropPivot.BaseCentre, 1.9f, 0.78f, 1.0f, 1, "The lobby's guard desk"),
            Prop("DeskCommander", PropGroup.Office, PropPivot.BaseCentre, 4.4f, 0.85f, 1.0f, 1, "The commander's desk in the lab"),
            Prop("OfficeChair", PropGroup.Office, PropPivot.BaseCentre, 0.5f, 1.03f, 0.5f, 4, "Swivel base, seat and back"),
            Prop("MeetingTable", PropGroup.Office, PropPivot.BaseCentre, 2.0f, 0.76f, 1.0f, 1, "Four steel legs"),
            Prop("Monitor", PropGroup.Office, PropPivot.BaseCentre, 0.7f, 0.75f, 0.2f, 7, "Screen faces +Z"),
            Prop("WallMonitor", PropGroup.Office, PropPivot.WallFace, 1.16f, 0.68f, 0.08f, 1, "Back face on the wall, screen faces +Z"),
            Prop("Locker", PropGroup.Office, PropPivot.BaseCentre, 0.52f, 2.0f, 0.55f, 16, "Steel-blue, vent plate on the front"),

            // Section 7.4, the generator room.
            Prop("ElectricalCabinet", PropGroup.Power, PropPivot.BaseCentre, 1.2f, 2.2f, 0.7f, 6, "Inset panel and three LEDs on the front"),
            Prop("ControlConsole", PropGroup.Power, PropPivot.BaseCentre, 1.8f, 1.13f, 0.75f, 1, "The hold-E console, sloped top panel"),
            Prop("FuelTank", PropGroup.Power, PropPivot.BaseCentre, 2.0f, 2.35f, 4.2f, 2, "Lies along Z on two cradles"),

            // Section 7.3 and 7.6, the pushable crate and the dressing that moves.
            Prop("Crate", PropGroup.Storage, PropPivot.Centre, 1.5f, 1.5f, 1.5f, 1, "MED-SUPPLY 04-117: a clean cube with a centred pivot, pushed by the player"),
            Prop("CrateSmall", PropGroup.Storage, PropPivot.Centre, 1.0f, 1.0f, 1.0f, 8, "The compound and dock crates"),
            Prop("Barrel", PropGroup.Storage, PropPivot.BaseCentre, 0.6f, 0.9f, 0.6f, 6, "12 sides"),

            // Visual-only models that the Systems Engineer and the Agent Controller nest (Assets/Prefabs/Models).
            Model("CureCase_Visual", PropGroup.Pickup, PropPivot.BaseCentre, 0.42f, 0.28f, 0.24f, 1, "Glass case with three cyan vials; spins and bobs by script"),
            Model("Medkit_Visual", PropGroup.Pickup, PropPivot.BaseCentre, 0.42f, 0.33f, 0.28f, 3, "White case, red cross, carry handle"),
            Model("Weapon_Smg_Visual", PropGroup.Weapon, PropPivot.Grip, 0.65f, 0.53f, 0.77f, 1, "MP-9 with hands; muzzle along +Z"),
            Model("Weapon_Sniper_Visual", PropGroup.Weapon, PropPivot.Grip, 0.65f, 0.55f, 1.29f, 1, "R-700 bolt-action rifle with two hands; the bolt handle is part of the mesh, the BoltHandle empty marks its pivot"),
            Model("Weapon_Knife_Visual", PropGroup.Weapon, PropPivot.Grip, 0.32f, 0.58f, 0.66f, 1, "Tactical combat knife (blackened drop point, G10 handle) with a gripping hand; the whole hand swings"),
            Model("Generator_Visual", PropGroup.Hero, PropPivot.BaseCentre, 6.96f, 4.10f, 2.90f, 1, "Hero model 1 (step C2)"),
            Model("SecurityRobot_Visual", PropGroup.Hero, PropPivot.BaseCentre, 1.07f, 2.38f, 0.64f, 2, "Hero model 2 (step C3)"),

            // Taken from CC0 packs. The palette lists the slot; add a prefab with this name and it shows the triangles.
            Kit("PipeStraight", PropGroup.Fittings, PropPivot.Centre, 0.2f, 0.2f, 2.0f, 8, "Ceiling runs, r 0.06 to 0.22; join pieces end to end"),
            Kit("CableTray", PropGroup.Fittings, PropPivot.Centre, 0.7f, 0.1f, 2.0f, 2, "With cables; ceiling of the corridor and the office"),
            Kit("Vent", PropGroup.Fittings, PropPivot.WallFace, 1.0f, 0.5f, 0.03f, 3, "Grille with slats"),
            Kit("FireExtinguisher", PropGroup.Fittings, PropPivot.BaseCentre, 0.18f, 0.5f, 0.18f, 1, "Red, r 0.09"),
            Kit("Flask", PropGroup.Fittings, PropPivot.BaseCentre, 0.14f, 0.22f, 0.14f, 8, "Glass beaker or conical flask"),
            Kit("Jar", PropGroup.Fittings, PropPivot.BaseCentre, 0.14f, 0.22f, 0.14f, 27, "Emissive cyan, amber and green in rotation"),
        };

        public static PropEntry Find(string name)
        {
            foreach (PropEntry entry in All)
            {
                if (entry.Name == name)
                {
                    return entry;
                }
            }

            return null;
        }

        // Prop prefabs are Prop_<Name> in Assets/Prefabs/Props (art-pipeline.md section 1).
        private static PropEntry Prop(string name, PropGroup group, PropPivot pivot, float x, float y, float z, int count, string note)
        {
            return new PropEntry("Prop_" + name, PropsFolder, PropSource.Blender, group, pivot, new Vector3(x, y, z), count, note);
        }

        private static PropEntry Kit(string name, PropGroup group, PropPivot pivot, float x, float y, float z, int count, string note)
        {
            return new PropEntry("Prop_" + name, PropsFolder, PropSource.Cc0Kit, group, pivot, new Vector3(x, y, z), count, note);
        }

        // Visual prefabs are <Name>_Visual in Assets/Prefabs/Models.
        private static PropEntry Model(string name, PropGroup group, PropPivot pivot, float x, float y, float z, int count, string note)
        {
            return new PropEntry(name, ModelsFolder, PropSource.Blender, group, pivot, new Vector3(x, y, z), count, note);
        }
    }
}
