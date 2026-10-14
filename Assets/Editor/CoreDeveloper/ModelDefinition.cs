using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    public enum PartShape
    {
        Box,
        CylinderX, // axis along the part's X
        CylinderY,
        CylinderZ,
        Sphere,
    }

    /// <summary>One primitive of a placeholder model, hung off the root or off a named pivot.</summary>
    public readonly struct PartSpec
    {
        public readonly string Name;
        public readonly PartShape Shape;
        public readonly Vector3 Position;
        public readonly Vector3 Size; // box: full extents; cylinder: (diameter, length, diameter) along its own axis; sphere: diameter x3
        public readonly Vector3 TiltDegrees; // extra rotation applied after the shape is aligned to its axis
        public readonly string Paint; // a key of the PartMaterials the model is built with
        public readonly string Parent; // a pivot name, or empty for the root

        public PartSpec(string name, PartShape shape, Vector3 position, Vector3 size, Vector3 tiltDegrees, string paint, string parent)
        {
            Name = name;
            Shape = shape;
            Position = position;
            Size = size;
            TiltDegrees = tiltDegrees;
            Paint = paint;
            Parent = parent;
        }
    }

    /// <summary>An empty object that a moving part rotates about (a bolt handle, a swinging hand), optionally inside another pivot.</summary>
    public readonly struct PivotSpec
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Vector3 EulerDegrees;
        public readonly string Parent;

        public PivotSpec(string name, Vector3 position, Vector3 eulerDegrees, string parent)
        {
            Name = name;
            Position = position;
            EulerDegrees = eulerDegrees;
            Parent = parent;
        }
    }

    public enum ColliderShape
    {
        Box,
        Capsule,
    }

    /// <summary>A collider on the prefab root. Box: Size is the full extents. Capsule: Size is (radius, height, 0), axis Y.</summary>
    public readonly struct ColliderSpec
    {
        public readonly ColliderShape Shape;
        public readonly Vector3 Center;
        public readonly Vector3 Size;

        public ColliderSpec(ColliderShape shape, Vector3 center, Vector3 size)
        {
            Shape = shape;
            Center = center;
            Size = size;
        }
    }

    /// <summary>An empty marker object (a muzzle, an anchor) that other roles find by name.</summary>
    public readonly struct SocketSpec
    {
        public readonly string Name;
        public readonly Vector3 Position;
        public readonly Vector3 EulerDegrees;
        public readonly string Parent;

        public SocketSpec(string name, Vector3 position, Vector3 eulerDegrees, string parent)
        {
            Name = name;
            Position = position;
            EulerDegrees = eulerDegrees;
            Parent = parent;
        }
    }

    /// <summary>
    /// Everything needed to build one model out of primitives: parts, moving-part pivots, colliders and sockets.
    /// It is the stand-in for an SM_*.fbx until the Blender model exists, with the same size, pivot and socket names.
    /// </summary>
    public sealed class ModelDefinition
    {
        public readonly string Name;
        public readonly List<PartSpec> Parts = new List<PartSpec>();
        public readonly List<PivotSpec> Pivots = new List<PivotSpec>();
        public readonly List<ColliderSpec> Colliders = new List<ColliderSpec>();
        public readonly List<SocketSpec> Sockets = new List<SocketSpec>();

        public ModelDefinition(string name)
        {
            Name = name;
        }

        // The helpers below read like the part tables in docs/reference/core-developer.md: centre first, then size.

        public ModelDefinition Box(string name, float x, float y, float z, float sx, float sy, float sz, string paint, string parent = "")
        {
            Parts.Add(new PartSpec(name, PartShape.Box, new Vector3(x, y, z), new Vector3(sx, sy, sz), Vector3.zero, paint, parent));
            return this;
        }

        public ModelDefinition TiltedBox(string name, float x, float y, float z, float sx, float sy, float sz, Vector3 tilt, string paint, string parent = "")
        {
            Parts.Add(new PartSpec(name, PartShape.Box, new Vector3(x, y, z), new Vector3(sx, sy, sz), tilt, paint, parent));
            return this;
        }

        public ModelDefinition CylinderX(string name, float x, float y, float z, float radius, float length, string paint, string parent = "")
        {
            return Cylinder(name, PartShape.CylinderX, x, y, z, radius, length, Vector3.zero, paint, parent);
        }

        public ModelDefinition CylinderY(string name, float x, float y, float z, float radius, float length, string paint, string parent = "")
        {
            return Cylinder(name, PartShape.CylinderY, x, y, z, radius, length, Vector3.zero, paint, parent);
        }

        public ModelDefinition CylinderZ(string name, float x, float y, float z, float radius, float length, string paint, string parent = "")
        {
            return Cylinder(name, PartShape.CylinderZ, x, y, z, radius, length, Vector3.zero, paint, parent);
        }

        public ModelDefinition Cylinder(string name, PartShape shape, float x, float y, float z, float radius, float length, Vector3 tilt, string paint, string parent = "")
        {
            Parts.Add(new PartSpec(name, shape, new Vector3(x, y, z), new Vector3(radius * 2f, length, radius * 2f), tilt, paint, parent));
            return this;
        }

        public ModelDefinition Sphere(string name, float x, float y, float z, float radius, string paint, string parent = "")
        {
            float d = radius * 2f;
            Parts.Add(new PartSpec(name, PartShape.Sphere, new Vector3(x, y, z), new Vector3(d, d, d), Vector3.zero, paint, parent));
            return this;
        }

        public ModelDefinition Pivot(string name, float x, float y, float z, Vector3 eulerDegrees = default, string parent = "")
        {
            Pivots.Add(new PivotSpec(name, new Vector3(x, y, z), eulerDegrees, parent));
            return this;
        }

        public ModelDefinition BoxCollider(float cx, float cy, float cz, float sx, float sy, float sz)
        {
            Colliders.Add(new ColliderSpec(ColliderShape.Box, new Vector3(cx, cy, cz), new Vector3(sx, sy, sz)));
            return this;
        }

        public ModelDefinition CapsuleCollider(float cx, float cy, float cz, float radius, float height)
        {
            Colliders.Add(new ColliderSpec(ColliderShape.Capsule, new Vector3(cx, cy, cz), new Vector3(radius, height, 0f)));
            return this;
        }

        public ModelDefinition Socket(string name, float x, float y, float z, Vector3 eulerDegrees = default, string parent = "")
        {
            Sockets.Add(new SocketSpec(name, new Vector3(x, y, z), eulerDegrees, parent));
            return this;
        }
    }
}
