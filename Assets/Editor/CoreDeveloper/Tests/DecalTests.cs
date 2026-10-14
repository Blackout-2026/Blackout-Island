using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class BitmapFontTests
    {
        [Test]
        public void EveryCharacterOfEverySign_IsInTheFont()
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                var texts = new List<string>(spec.Lines);
                if (spec.Header != null)
                {
                    texts.Add(spec.Header);
                }

                foreach (string text in texts)
                {
                    foreach (char c in text)
                    {
                        Assert.IsTrue(BitmapFont.Supports(c), $"{spec.Name}: '{c}' in \"{text}\"");
                    }
                }
            }
        }

        [Test]
        public void Glyphs_AreFiveByTheSevenAndLookRight()
        {
            // 'I' is a bar with serifs: the top row is 01110 and the middle rows are the centre column only.
            Assert.IsTrue(BitmapFont.IsOn('I', 1, 0));
            Assert.IsFalse(BitmapFont.IsOn('I', 0, 0));
            Assert.IsTrue(BitmapFont.IsOn('I', 2, 3));
            Assert.IsFalse(BitmapFont.IsOn('I', 1, 3));
            Assert.IsFalse(BitmapFont.IsOn('?', 2, 3), "an unknown character is blank");
            Assert.IsFalse(BitmapFont.IsOn(' ', 2, 3));
        }

        [Test]
        public void WidthInDots_CountsTheGapsBetweenLettersOnly()
        {
            Assert.AreEqual(0, BitmapFont.WidthInDots(string.Empty));
            Assert.AreEqual(5, BitmapFont.WidthInDots("A"));
            Assert.AreEqual(11, BitmapFont.WidthInDots("AB"));
        }
    }

    public class DecalCatalogTests
    {
        [Test]
        public void Names_AreUniqueAndAlsoNotTakenByAProp()
        {
            var seen = new HashSet<string>();
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                Assert.IsTrue(seen.Add(spec.PrefabName), spec.PrefabName);
                Assert.IsNull(PropCatalog.Find(spec.PrefabName), spec.PrefabName);
            }
        }

        [Test]
        public void EveryColour_Parses()
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                foreach (string hex in new[] { spec.Background, spec.Foreground, spec.HeaderBackground ?? "#000000", spec.HeaderForeground ?? "#000000" })
                {
                    Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out _), $"{spec.Name}: {hex}");
                }
            }
        }

        [Test]
        public void TheSizes_AreThePrototypes()
        {
            // docs/reference/core-developer.md 7.8
            DecalSpec voltage = Find("DangerHighVoltage");
            Assert.AreEqual(1.0f, voltage.Width);
            Assert.AreEqual(0.62f, voltage.Height);
            Assert.AreEqual(5.0f, Find("GeneratorG1").Width);
            Assert.AreEqual(3.2f, Find("KeepClear").Width);
            Assert.AreEqual(0.6f, Find("FireHose").Width);
            Assert.AreEqual(4.0f, Find("HazardStripeRamp").Width); // floor stripes: ramp mouth 4.0 x 0.4 and lab door 3.2 x 0.8
            Assert.AreEqual(0.8f, Find("HazardStripeLab").Height);
        }

        [Test]
        public void Entries_DescribeAWallSignAndAFloorMarking()
        {
            PropEntry wall = Find("FireHose").Entry;
            PropEntry floor = Find("KeepClear").Entry;

            Assert.AreEqual(PropPivot.WallFace, wall.Pivot);
            Assert.AreEqual(new Vector3(0.6f, 0.3f, 0f), wall.Size);
            Assert.AreEqual(PropPivot.BaseCentre, floor.Pivot);
            Assert.AreEqual(new Vector3(3.2f, 0f, 0.8f), floor.Size);
            Assert.AreEqual(PropSource.Generated, wall.Source);
            Assert.AreEqual(PropGroup.Sign, wall.Group);
        }

        private static DecalSpec Find(string name)
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                if (spec.Name == name)
                {
                    return spec;
                }
            }

            Assert.Fail("No decal " + name);
            return null;
        }
    }

    public class DecalRendererTests
    {
        private static DecalSpec Spec(string name)
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                if (spec.Name == name)
                {
                    return spec;
                }
            }

            Assert.Fail("No decal " + name);
            return null;
        }

        [Test]
        public void TextureSizes_ArePowersOfTwoInsideTheLimits()
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                DecalRenderer.SizeOf(spec, out int w, out int h);
                Assert.IsTrue(Mathf.IsPowerOfTwo(w) && Mathf.IsPowerOfTwo(h), $"{spec.Name}: {w} x {h}");
                Assert.GreaterOrEqual(Mathf.Min(w, h), DecalRenderer.MinSize, spec.Name);
                Assert.LessOrEqual(Mathf.Max(w, h), DecalRenderer.MaxSize, spec.Name);
            }
        }

        [Test]
        public void Render_IsDeterministicAndFillsEveryPixel()
        {
            DecalSpec spec = Spec("DangerHighVoltage");

            Color32[] a = DecalRenderer.Render(spec, out int w, out int h);
            Color32[] b = DecalRenderer.Render(spec, out _, out _);

            Assert.AreEqual(w * h, a.Length);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void Render_DrawsTheHeaderBarAndTheTextInTheirColours()
        {
            DecalSpec spec = Spec("DangerHighVoltage"); // black header with yellow text, yellow plate with black text
            Color32[] pixels = DecalRenderer.Render(spec, out int w, out int h);

            Color32 yellow = Parse(spec.Background);
            Color32 black = Parse(spec.Foreground);
            Assert.AreEqual(black, pixels[(h - 1) * w + 1], "top-left corner is the header bar"); // row h-1 is the top of the picture
            Assert.AreEqual(yellow, pixels[1], "bottom-left corner is the plate");
            Assert.Greater(Count(pixels, yellow), 0);
            Assert.Greater(Count(pixels, black), 0);

            // Yellow letters inside the black bar: the header must contain both colours.
            int yellowInHeader = 0;
            for (int y = h - h * 3 / 10; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].Equals(yellow))
                    {
                        yellowInHeader++;
                    }
                }
            }

            Assert.Greater(yellowInHeader, 20, "the word DANGER is drawn in the header");
        }

        [Test]
        public void Render_LeavesAMarginAtTheLeftAndRightEdges()
        {
            // The longest sign (24 letters on a 4.6 m plate) must fit: its text never touches the first or last column.
            DecalSpec spec = Spec("SecurityCheckpoint");
            Color32[] pixels = DecalRenderer.Render(spec, out int w, out int h);
            Color32 background = Parse(spec.Background);

            for (int y = 0; y < h; y++)
            {
                Assert.AreEqual(background, pixels[y * w], "left edge");
                Assert.AreEqual(background, pixels[y * w + w - 1], "right edge");
            }
        }

        [Test]
        public void StripeTile_AlternatesTwoColoursAndJoinsItsNeighbours()
        {
            DecalSpec spec = Spec("HazardStripeRamp");
            Color32[] tile = DecalRenderer.Render(spec, out int w, out int h);

            Assert.AreEqual(DecalRenderer.StripeTileSize, w);
            Assert.AreEqual(w, h);
            Assert.AreNotEqual(tile[0], tile[w / 2], "bands alternate along a row");
            // The colour depends on (x + y) mod 64 only, so the tile repeats seamlessly; every pixel on a 45 degree
            // line has the same colour.
            Assert.AreEqual(tile[5 * w + 10], tile[6 * w + 9]);
            Assert.AreEqual(tile[0 * w + 20], tile[10 * w + 10]);
        }

        private static Color32 Parse(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color colour);
            return colour;
        }

        private static int Count(Color32[] pixels, Color32 colour)
        {
            int count = 0;
            foreach (Color32 pixel in pixels)
            {
                if (pixel.Equals(colour))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public class DecalBuilderTests
    {
        private static DecalSpec FireHose()
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                if (spec.Name == "FireHose")
                {
                    return spec;
                }
            }

            return null;
        }

        private static DecalSpec Stripe()
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                if (spec.Name == "HazardStripeRamp")
                {
                    return spec;
                }
            }

            return null;
        }

        private static DecalSpec Floor()
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                if (spec.Name == "KeepClear")
                {
                    return spec;
                }
            }

            return null;
        }

        [SetUp]
        public void SetUp()
        {
            TestAssetFolders.Create();
        }

        [TearDown]
        public void TearDown()
        {
            TestAssetFolders.Delete();
        }

        private static GameObject Make(DecalSpec spec)
        {
            DecalBuilder.WriteTexture(spec, TestAssetFolders.Textures);
            Material material = DecalBuilder.BuildMaterial(spec, TestAssetFolders.Materials, TestAssetFolders.Textures);
            string path = DecalBuilder.BuildPrefab(spec, material, TestAssetFolders.Prefabs);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        [Test]
        public void WriteTexture_ImportsAnSrgbColourMapWithMipmaps()
        {
            string path = DecalBuilder.WriteTexture(FireHose(), TestAssetFolders.Textures);

            Assert.AreEqual($"{TestAssetFolders.Textures}/T_Sign_FireHose_BaseColor.png", path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.IsTrue(importer.sRGBTexture);
            Assert.IsTrue(importer.mipmapEnabled);
            Assert.AreEqual(TextureImporterType.Default, importer.textureType);
        }

        [Test]
        public void WriteTexture_DoesNotRewriteAnUnchangedFile()
        {
            string path = DecalBuilder.WriteTexture(FireHose(), TestAssetFolders.Textures);
            string full = Path.GetFullPath(path);
            File.SetLastWriteTimeUtc(full, new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));

            DecalBuilder.WriteTexture(FireHose(), TestAssetFolders.Textures);

            Assert.AreEqual(new System.DateTime(2020, 1, 1, 0, 0, 0, System.DateTimeKind.Utc), File.GetLastWriteTimeUtc(full));
        }

        [Test]
        public void BuildMaterial_UsesTheTextureAndTheLitShader()
        {
            DecalBuilder.WriteTexture(FireHose(), TestAssetFolders.Textures);

            Material material = DecalBuilder.BuildMaterial(FireHose(), TestAssetFolders.Materials, TestAssetFolders.Textures);

            Assert.AreEqual("M_Sign_FireHose", material.name);
            Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name);
            Assert.IsNotNull(material.GetTexture("_BaseMap"));
            Assert.AreEqual(Color.white, material.GetColor("_BaseColor"));
        }

        [Test]
        public void TheStripeMaterial_RepeatsTheTileEveryFortyCentimetres()
        {
            DecalBuilder.WriteTexture(Stripe(), TestAssetFolders.Textures);

            Material material = DecalBuilder.BuildMaterial(Stripe(), TestAssetFolders.Materials, TestAssetFolders.Textures);

            Assert.AreEqual(new Vector2(10f, 1f), material.GetTextureScale("_BaseMap")); // 4.0 x 0.4 m
            Assert.AreEqual(new Vector2(10f, 1f), material.GetTextureScale("_MainTex"));
        }

        [Test]
        public void BuildingTheStripeMaterialAgain_DoesNotChangeItsFile()
        {
            DecalBuilder.WriteTexture(Stripe(), TestAssetFolders.Textures);
            DecalBuilder.BuildMaterial(Stripe(), TestAssetFolders.Materials, TestAssetFolders.Textures);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            string file = Path.GetFullPath($"{TestAssetFolders.Materials}/M_Sign_HazardStripeRamp.mat");
            string first = File.ReadAllText(file);

            DecalBuilder.BuildMaterial(Stripe(), TestAssetFolders.Materials, TestAssetFolders.Textures);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Assert.AreEqual(first, File.ReadAllText(file));
        }

        [Test]
        public void AWallSign_FacesPlusZWithThePivotOnItsBackFace()
        {
            GameObject prefab = Make(FireHose());
            GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            try
            {
                Transform quad = contents.transform.Find("Quad");
                Assert.AreEqual(0f, Vector3.Distance(Vector3.forward, -quad.forward), 0.001f, "a Unity quad shows its face opposite its forward");
                Assert.AreEqual(0, PropStats.Colliders(contents));
                Bounds bounds = PropStats.LocalBounds(contents);
                Assert.IsNull(PivotRules.CheckPivot(bounds, PropPivot.WallFace));
                Assert.IsNull(PivotRules.CheckSize(bounds, FireHose().Entry.Size));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void AFloorMarking_LiesFlatFacingUp()
        {
            GameObject prefab = Make(Floor());
            GameObject contents = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(prefab));
            try
            {
                Transform quad = contents.transform.Find("Quad");
                Assert.AreEqual(0f, Vector3.Distance(Vector3.up, -quad.forward), 0.001f);
                Bounds bounds = PropStats.LocalBounds(contents);
                Assert.IsNull(PivotRules.CheckPivot(bounds, PropPivot.BaseCentre));
                Assert.IsNull(PivotRules.CheckSize(bounds, Floor().Entry.Size));
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        [Test]
        public void ANewSign_UsesItsOwnMaterialAndNotTheDefaultOne()
        {
            foreach (DecalSpec spec in new[] { FireHose(), Floor(), Stripe() })
            {
                GameObject prefab = Make(spec);

                Material used = prefab.transform.Find("Quad").GetComponent<Renderer>().sharedMaterial;

                Assert.AreEqual(spec.MaterialName, used.name);
                StringAssert.StartsWith("Assets/", AssetDatabase.GetAssetPath(used));
                Assert.IsNotNull(used.GetTexture("_BaseMap"), spec.Name + " shows its picture");
            }
        }

        [Test]
        public void EveryGeneratedSignInTheProject_ShowsItsOwnPicture()
        {
            // The real prefabs in Assets/Prefabs/Props, not scratch copies. A sign that fell back to the default
            // material looks like a blank grey panel in the level.
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Entry.PrefabPath);
                if (prefab == null)
                {
                    continue; // not generated on this checkout yet
                }

                Material used = prefab.transform.Find("Quad").GetComponent<Renderer>().sharedMaterial;
                Assert.IsNotNull(used, spec.Name);
                Assert.AreEqual(spec.MaterialName, used.name, spec.Name);
                Assert.IsNotNull(used.GetTexture("_BaseMap"), spec.Name + " has no picture");
            }
        }

        [Test]
        public void ASignWithTheDefaultMaterial_IsRepairedOnTheNextRun()
        {
            GameObject prefab = Make(FireHose());
            string path = AssetDatabase.GetAssetPath(prefab);
            var stray = GameObject.CreatePrimitive(PrimitiveType.Quad); // brings the pipeline's default material
            Material builtIn = stray.GetComponent<Renderer>().sharedMaterial;
            Object.DestroyImmediate(stray);
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            contents.transform.Find("Quad").GetComponent<Renderer>().sharedMaterial = builtIn;
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            GameObject again = Make(FireHose());

            Assert.AreEqual("M_Sign_FireHose", again.transform.Find("Quad").GetComponent<Renderer>().sharedMaterial.name);
        }

        [Test]
        public void ARunningAgain_LeavesAHandAssignedMaterialAlone()
        {
            GameObject prefab = Make(FireHose());
            string path = AssetDatabase.GetAssetPath(prefab);
            var mine = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Mine" };
            AssetDatabase.CreateAsset(mine, $"{TestAssetFolders.Materials}/Mine.mat");
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            contents.transform.Find("Quad").GetComponent<Renderer>().sharedMaterial = mine;
            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);

            Make(FireHose());

            GameObject again = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.AreEqual(mine, again.transform.Find("Quad").GetComponent<Renderer>().sharedMaterial);
        }
    }
}
