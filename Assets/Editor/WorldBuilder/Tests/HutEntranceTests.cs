using System.IO;
using System.Linq;
using Blackout.Shared;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor.Tests
{
    /// <summary>Step B1: the military hut entrance, its materials and props, and the hut-styled concrete. The map's numbers must not move.</summary>
    public class HutEntranceTests
    {
        private static readonly string[] EntranceParts =
        {
            "Hut_Roof", "Hut_FrontWall", "Hut_Porch", "Hut_Pad", "Hut_WindowFrames", "Hut_Windows", "Hut_Bollards", "Hut_Signs", "Hut_Keypad",
        };

        private static GameObject Load(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, path + " must exist; run Blackout/World/Build Hut Entrance.");
            return asset;
        }

        private static BunkerLayout Layout() => AssetDatabase.LoadAssetAtPath<BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);

        [Test]
        public void Models_AreStaticAndHaveEveryPart()
        {
            GameObject entrance = Load(HutEntranceBuilder.EntranceModelPath);
            foreach (string part in EntranceParts)
                Assert.That(entrance.transform.Find(part), Is.Not.Null, "SM_Hut_Entrance has no " + part);
            GameObject props = Load(HutEntranceBuilder.PropsModelPath);
            foreach ((string _, string mesh) in HutEntranceBuilder.Props)
                Assert.That(props.transform.Find(mesh), Is.Not.Null, "SM_Hut_Props has no " + mesh);
            // The download's six animations (its own doors, shutters, cameras) are not used: the game keeps the cyberpunk doors.
            Assert.That(entrance.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(entrance.GetComponentsInChildren<SkinnedMeshRenderer>(true), Is.Empty);
        }

        [Test]
        public void Materials_UseTheHutMapsInUrpLit()
        {
            Assert.That(HutEntranceBuilder.Materials.Length, Is.EqualTo(12));
            foreach ((string _, string name) in HutEntranceBuilder.Materials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(HutEntranceBuilder.MaterialPath(name));
                Assert.That(material, Is.Not.Null, name);
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"), name);
                Assert.That(material.GetTexture("_BaseMap"), Is.Not.Null, name + " base colour");
                Assert.That(material.GetTexture("_MetallicGlossMap"), Is.Not.Null, name + " metal/smoothness");
                Assert.That(material.GetTexture("_OcclusionMap"), Is.Not.Null, name + " occlusion");
                Assert.That(material.IsKeywordEnabled("_METALLICSPECGLOSSMAP"), Is.True, name);
            }
        }

        [Test]
        public void EntrancePrefab_IsStaticLevelGeometryWithCollidersOnTheSolidParts()
        {
            GameObject prefab = Load(HutEntranceBuilder.EntrancePrefabPath);
            foreach (string part in EntranceParts)
            {
                Transform child = prefab.transform.Find(part);
                Assert.That(child, Is.Not.Null, part);
                Assert.That(child.gameObject.layer, Is.EqualTo(PhysicsLayers.Level), part);
                Assert.That(child.GetComponent<MeshRenderer>().sharedMaterials, Has.None.Null, part);
                bool solid = HutEntranceBuilder.SolidParts.Contains(part);
                Assert.That(child.GetComponent<MeshCollider>() != null, Is.EqualTo(solid), part + " collider");
            }

            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
        }

        /// <summary>The world-space bounds of one part of a prefab placed at a position (a part's mesh bounds are in the FBX's own axes).</summary>
        private static Bounds WorldBounds(string prefabPath, string part, Vector3 position)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load(prefabPath));
            try
            {
                instance.transform.position = position;
                return instance.transform.Find(part).GetComponent<MeshRenderer>().bounds;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Porch_EndsAtTheLobbyWallsOuterFace()
        {
            BunkerLayout layout = Layout();
            Room lobby = layout.FindRoom("BunkerEntranceLobby");
            float outerFace = lobby.position.z - lobby.size.z * 0.5f - layout.wallThickness;
            Bounds porch = WorldBounds(HutEntranceBuilder.EntrancePrefabPath, "Hut_Porch", HutEntranceBuilder.HutPosition);
            Assert.That(porch.max.z, Is.LessThanOrEqualTo(outerFace + 0.01f), "the porch must not reach into the lobby");
            Assert.That(porch.max.z, Is.GreaterThan(outerFace - 0.05f), "the porch must reach the lobby wall");
            Assert.That(porch.min.z, Is.LessThan(outerFace - 1f), "the porch must stand out into the yard");
        }

        [Test]
        public void Arch_ContainsTheLobbyShellAndCoversTheRamp()
        {
            BunkerLayout layout = Layout();
            Room lobby = layout.FindRoom("BunkerEntranceLobby");
            Vector3 position = HutEntranceBuilder.HutPosition;
            Bounds roof = WorldBounds(HutEntranceBuilder.EntrancePrefabPath, "Hut_Roof", position);
            float radius = roof.extents.x;
            float halfWidth = lobby.size.x * 0.5f + layout.wallThickness;
            float top = lobby.position.y + lobby.size.y + layout.wallThickness - position.y;
            Assert.That(Mathf.Sqrt(halfWidth * halfWidth + top * top), Is.LessThanOrEqualTo(radius),
                "the lobby shell's top corners would poke through the arch");
            Assert.That(roof.min.z, Is.LessThan(lobby.position.z - lobby.size.z * 0.5f - layout.wallThickness), "the roof covers the lobby's front");
            Assert.That(roof.max.z, Is.GreaterThanOrEqualTo(62f), "the roof runs on to the retaining wall (z 62), which hides its end");
        }

        [Test]
        public void Map_LayoutNumbersAreUnchanged()
        {
            BunkerLayout layout = Layout();
            Room lobby = layout.FindRoom("BunkerEntranceLobby");
            Assert.That(lobby.position, Is.EqualTo(new Vector3(0f, 10f, 50.2f)));
            Assert.That(lobby.size.x, Is.EqualTo(11.2f).Within(1e-4f));
            Assert.That(lobby.size.z, Is.EqualTo(3.6f).Within(1e-4f));
            DoorOpening door = lobby.openings.Find(opening => opening.id == "D-Bunker");
            Assert.That(door.width, Is.EqualTo(1.4f).Within(1e-4f));
            Assert.That(door.height, Is.EqualTo(2.3f).Within(1e-4f));
            Assert.That(HutEntranceBuilder.HutPosition.x, Is.Zero);
            Assert.That(HutEntranceBuilder.HutPosition.z, Is.EqualTo(48.2f).Within(1e-4f), "the hut sits on D-Bunker's socket");
        }

        [Test]
        public void Environment_HutIsPlacedAndOldFacadeKeepsEveryCollider()
        {
            WithEnvironment(scene =>
            {
                Transform exterior = Root(scene, "BunkerExterior");
                Transform hut = exterior.Find(HutEntranceBuilder.ContainerName + "/" + HutEntranceBuilder.InstanceName);
                Assert.That(hut, Is.Not.Null, "Hut_Entrance is not in Environment");
                Assert.That(Vector3.Distance(hut.position, HutEntranceBuilder.HutPosition), Is.LessThan(1e-3f));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(hut.gameObject),
                    Is.SameAs(Load(HutEntranceBuilder.EntrancePrefabPath)));

                Transform old = exterior.Find("EntranceExterior");
                Assert.That(old.childCount, Is.GreaterThan(30));
                int hidden = 0;
                foreach (Transform box in old)
                {
                    Assert.That(box.GetComponent<BoxCollider>().enabled, Is.True, box.name + " lost its collider");
                    string part = box.name.Substring("EntranceExterior_".Length);
                    bool kept = HutEntranceBuilder.IsKeptFacadePart(part);
                    Assert.That(box.GetComponent<MeshRenderer>().enabled, Is.EqualTo(kept), box.name);
                    if (!kept) hidden++;
                }

                Assert.That(hidden, Is.GreaterThanOrEqualTo(20));
                foreach (string kept in new[] { "Apron", "RetainingWest", "RetainingEast", "DoorTrimTop" })
                    Assert.That(old.Find("EntranceExterior_" + kept).GetComponent<MeshRenderer>().enabled, Is.True, kept);
            });
        }

        [Test]
        public void ConcreteSurfaces_UseTheHutTexturesAndTileWithoutASeam()
        {
            foreach (string surface in new[] { "Concrete", "ConcreteStained" })
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath(surface));
                Texture baseMap = material.GetTexture("_BaseMap");
                Assert.That(baseMap, Is.Not.Null, surface + " must be textured; run Blackout/World/Build Material Library.");
                string path = AssetDatabase.GetAssetPath(baseMap);
                Assert.That(path, Does.StartWith("Assets/Textures/Environment/" + surface + "/"));

                var pixels = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                try
                {
                    Assert.That(pixels.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                    Color32[] all = pixels.GetPixels32();
                    int w = pixels.width, h = pixels.height;
                    for (int i = 0; i < h; i++)
                        Assert.That(Same(all[i * w], all[i * w + w - 1]), Is.True, surface + " row " + i + ": left and right edges differ");
                    for (int i = 0; i < w; i++)
                        Assert.That(Same(all[i], all[(h - 1) * w + i]), Is.True, surface + " column " + i + ": top and bottom edges differ");
                }
                finally
                {
                    Object.DestroyImmediate(pixels);
                }
            }

            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath("Concrete")).GetTexture("_BumpMap"), Is.Not.Null);
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b;

        [Test]
        public void Props_FaceForwardFromTheWallAndAreInTheDressingTable()
        {
            foreach ((string prefabName, string mesh) in HutEntranceBuilder.Props)
            {
                GameObject prefab = Load(HutEntranceBuilder.PropFolder + "/" + prefabName + ".prefab");
                Bounds m = WorldBounds(HutEntranceBuilder.PropFolder + "/" + prefabName + ".prefab", mesh, Vector3.zero);
                Assert.That(m.min.z, Is.GreaterThanOrEqualTo(-0.002f), prefabName + " must not go into the wall");
                Assert.That(m.max.z, Is.GreaterThan(0.01f), prefabName + " must come out of the wall (+Z)");
                Assert.That(Mathf.Abs(m.center.x), Is.LessThan(0.01f), prefabName + " is centred on its pivot");
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            }

            var rows = DressingLayout.Parse(File.ReadAllText(DressingLayout.DocumentPath)).Where(spec => spec.Prefab.StartsWith("Prop_Hut")).ToList();
            Assert.That(rows.Count, Is.EqualTo(10));
            foreach (DressingSpec row in rows)
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(row.PrefabPath), Is.Not.Null, row.Id);
        }

        [Test]
        public void Environment_HasEveryHutProp()
        {
            var rows = DressingLayout.Parse(File.ReadAllText(DressingLayout.DocumentPath)).Where(spec => spec.Prefab.StartsWith("Prop_Hut")).ToList();
            WithEnvironment(scene =>
            {
                Transform container = Root(scene, "Props").Find(BunkerDresser.ContainerName);
                foreach (DressingSpec row in rows)
                {
                    Transform instance = container.Find(row.Room + "/" + row.Room + "_" + row.Id);
                    Assert.That(instance, Is.Not.Null, row.Id + " was not placed; check the Dress Bunker log.");
                    Assert.That(Vector3.Distance(instance.position, row.Position), Is.LessThan(1e-3f), row.Id);
                }
            });
        }

        private static Transform Root(Scene scene, string name) => scene.GetRootGameObjects().Single(root => root.name == name).transform;

        /// <summary>Opens the saved Environment scene next to whatever is open, runs the check, and closes it again if it was not open.</summary>
        private static void WithEnvironment(System.Action<Scene> check)
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Additive);
            try
            {
                check(scene);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
