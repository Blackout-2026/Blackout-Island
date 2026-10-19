using System;

namespace Blackout.Editor
{
    /// <summary>
    /// Which library surface each bunker blockout part gets, chosen by room and part name (the part name is the
    /// generated name without its "&lt;Room&gt;_" prefix, for example "Floor", "Wall_N_0" or "Sill_S_D-Lab").
    /// The first matching rule wins; room "*" matches every room. Choices follow the reference's "Used on" column
    /// (docs/reference/world-builder.md section 3), mapped onto the twelve surfaces of <see cref="SurfaceLibrary"/>.
    /// </summary>
    public static class MaterialRules
    {
        public const string AnyRoom = "*";

        public readonly struct Rule
        {
            public readonly string Room;
            public readonly string PartPrefix;
            public readonly string Surface;

            public Rule(string room, string partPrefix, string surface)
            {
                Room = room;
                PartPrefix = partPrefix;
                Surface = surface;
            }
        }

        public static readonly Rule[] Rules =
        {
            new Rule(AnyRoom, "Sill_", "HazardStripe"), // door thresholds: the reference's hazard edges
            new Rule("Laboratory", "Floor", "Tile"), // tiles: lab floor
            new Rule("Laboratory", "Wall_", "Tile"), // wallLab: white lab walls
            new Rule("Laboratory", "Lintel_", "Tile"),
            new Rule("BunkerEntranceRamp", "Floor", "MetalPlate"), // diamondPlate: ramp floor
            new Rule("GeneratorRoom", "Wall_", "MetalPlate"), // wallGen: industrial panels
            new Rule("GeneratorRoom", "Lintel_", "MetalPlate"),
            new Rule("GeneratorRoom", "Floor", "MetalGrating"), // step C7: welded steel grating over dark holes, not the oil-stained concrete
            new Rule(AnyRoom, "Floor", "ConcreteStained"), // floorConcrete (oil): security, corridor, lobby, generator
            new Rule(AnyRoom, "Ceiling", "Concrete"), // ceiling
            new Rule(AnyRoom, "Wall_", "Concrete"), // wallPanel / wallSec: corridor, lobby, ramp, security
            new Rule(AnyRoom, "Lintel_", "Concrete"),
        };

        /// <summary>The surface for this part, or null if no rule matches (the part is then left alone).</summary>
        public static string SurfaceFor(string room, string part)
        {
            foreach (Rule rule in Rules)
            {
                if ((rule.Room == AnyRoom || rule.Room == room) && part.StartsWith(rule.PartPrefix, StringComparison.Ordinal))
                {
                    return rule.Surface;
                }
            }

            return null;
        }
    }
}
