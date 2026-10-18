using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor.Tests
{
    public class WorldMemoryTunerTests
    {
        [Test]
        public void DockScaleChangesOnlyDirectGeneratedPostsAndPreservesCollision()
        {
            var dock = new GameObject("Dock");
            try
            {
                GameObject post = Cube(dock.transform, "Dock_Post_W00");
                GameObject deck = Cube(dock.transform, "Dock_Deck");
                GameObject boat = Cube(dock.transform, "Boat");
                GameObject nested = Cube(boat.transform, "Dock_Post_Unexpected");
                var renderer = post.GetComponent<MeshRenderer>();
                GameObjectUtility.SetStaticEditorFlags(post, StaticFlagRules.Environment);
                string collider = EditorJsonUtility.ToJson(post.GetComponent<Collider>());
                string transform = EditorJsonUtility.ToJson(post.transform);
                Mesh mesh = post.GetComponent<MeshFilter>().sharedMesh;

                Assert.That(WorldMemoryTuner.ApplyDockPostScales(dock.transform), Is.EqualTo(1));

                Assert.That(renderer.scaleInLightmap, Is.EqualTo(0.5f));
                Assert.That(deck.GetComponent<MeshRenderer>().scaleInLightmap, Is.EqualTo(1f));
                Assert.That(boat.GetComponent<MeshRenderer>().scaleInLightmap, Is.EqualTo(1f));
                Assert.That(nested.GetComponent<MeshRenderer>().scaleInLightmap, Is.EqualTo(1f));
                Assert.That(post.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                Assert.That(EditorJsonUtility.ToJson(post.GetComponent<Collider>()), Is.EqualTo(collider));
                Assert.That(EditorJsonUtility.ToJson(post.transform), Is.EqualTo(transform));
                Assert.That(GameObjectUtility.GetStaticEditorFlags(post), Is.EqualTo(StaticFlagRules.Environment));
                Assert.That(WorldMemoryTuner.ApplyDockPostScales(dock.transform), Is.Zero);
            }
            finally { Object.DestroyImmediate(dock); }
        }

        [Test]
        public void GrassCompressionReducesPayloadAndKeepsDimensionsMipsAndSampling()
        {
            const int size = VegetationPlaceholderBuilder.GrassTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            try
            {
                texture.SetPixels32(VegetationMeshes.GrassBlades(size));
                texture.Apply(true);
                int mipCount = texture.mipmapCount;
                int bytes = texture.GetRawTextureData<byte>().Length;

                Assert.That(WorldMemoryTuner.CompressGrass(texture), Is.True);

                Assert.That(texture.format, Is.EqualTo(TextureFormat.DXT5));
                Assert.That(texture.width, Is.EqualTo(size));
                Assert.That(texture.height, Is.EqualTo(size));
                Assert.That(texture.mipmapCount, Is.EqualTo(mipCount));
                Assert.That(texture.GetRawTextureData<byte>().Length, Is.LessThan(bytes));
                Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Bilinear));
                Assert.That(WorldMemoryTuner.CompressGrass(texture), Is.False);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test]
        public void GrassCompressionRefusesAnUnexpectedTextureWithoutChangingItsFormat()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            try
            {
                Assert.Throws<InvalidOperationException>(() => WorldMemoryTuner.CompressGrass(texture));
                Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        private static GameObject Cube(Transform parent, string name)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
