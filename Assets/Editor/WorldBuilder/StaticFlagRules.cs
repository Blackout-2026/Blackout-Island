using UnityEditor;

namespace Blackout.Editor
{
    /// <summary>
    /// Which static flags Setup Lighting gives the environment's renderers, by scene root and the child under it.
    /// - Environment (the bunker parts, the dock, the terrain): Contribute GI (lightmapped, and it blocks and bounces
    ///   light in the bake), Occluder and Occludee (occlusion culling), Batching (static batching) and Reflection Probe
    ///   (it shows up in the baked room reflections).
    /// - The water never moves either, but it is see-through and 520 m wide, so it neither hides anything nor gets a
    ///   lightmap; it takes the light probes' light instead.
    /// - The boat bobs, so it stays dynamic.
    /// Anything else (markers, the lamps, props and other people's objects) is left as it is.
    /// </summary>
    public static class StaticFlagRules
    {
        public const StaticEditorFlags Environment = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

        public const StaticEditorFlags SeeThrough = StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

        /// <summary>The flags for renderers under scene root <paramref name="root"/>, child <paramref name="child"/>; null leaves them alone.</summary>
        public static StaticEditorFlags? For(string root, string child)
        {
            switch (root)
            {
                case "Bunker":
                    return Environment;
                case "Terrain":
                    return child == "IslandTerrain" ? Environment : (StaticEditorFlags?)null;
                case "Beach":
                    switch (child)
                    {
                        case "Dock": return Environment;
                        case "Water": return SeeThrough;
                        case "Boat": return 0;
                        default: return null;
                    }

                default:
                    return null;
            }
        }
    }
}
