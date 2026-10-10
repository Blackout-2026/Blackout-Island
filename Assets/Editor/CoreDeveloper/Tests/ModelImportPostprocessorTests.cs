using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Imports a tiny 1 m cube (ASCII FBX written by the test) through the real importer. The file is created
    /// in Assets/Models/Custom and deleted again in TearDown.
    /// </summary>
    public class ModelImportPostprocessorTests
    {
        private const string AssetPath = ModelImportRules.Folder + "SM_Prop_ImportTest.fbx";

        // Unit cube: 8 vertices, 6 quads (12 triangles), one UV set. UnitScaleFactor 100 means one file unit is a metre.
        private const string CubeFbx = @"; FBX 7.3.0 project file
FBXHeaderExtension:  {
	FBXHeaderVersion: 1003
	FBXVersion: 7300
}
GlobalSettings:  {
	Version: 1000
	Properties70:  {
		P: ""UpAxis"", ""int"", ""Integer"", """",1
		P: ""UpAxisSign"", ""int"", ""Integer"", """",1
		P: ""FrontAxis"", ""int"", ""Integer"", """",2
		P: ""FrontAxisSign"", ""int"", ""Integer"", """",1
		P: ""CoordAxis"", ""int"", ""Integer"", """",0
		P: ""CoordAxisSign"", ""int"", ""Integer"", """",1
		P: ""UnitScaleFactor"", ""double"", ""Number"", """",100
	}
}
Definitions:  {
	Version: 100
	Count: 2
	ObjectType: ""Model"" {
		Count: 1
	}
	ObjectType: ""Geometry"" {
		Count: 1
	}
}
Objects:  {
	Geometry: 100, ""Geometry::Cube"", ""Mesh"" {
		Vertices: *24 {
			a: -0.5,-0.5,-0.5,0.5,-0.5,-0.5,0.5,0.5,-0.5,-0.5,0.5,-0.5,-0.5,-0.5,0.5,0.5,-0.5,0.5,0.5,0.5,0.5,-0.5,0.5,0.5
		}
		PolygonVertexIndex: *24 {
			a: 0,3,2,-2,4,5,6,-8,0,1,5,-5,3,7,6,-3,0,4,7,-4,1,2,6,-6
		}
		GeometryVersion: 124
		LayerElementNormal: 0 {
			Version: 101
			Name: """"
			MappingInformationType: ""ByPolygonVertex""
			ReferenceInformationType: ""Direct""
			Normals: *72 {
				a: 0,0,-1,0,0,-1,0,0,-1,0,0,-1,0,0,1,0,0,1,0,0,1,0,0,1,0,-1,0,0,-1,0,0,-1,0,0,-1,0,0,1,0,0,1,0,0,1,0,0,1,0,-1,0,0,-1,0,0,-1,0,0,-1,0,0,1,0,0,1,0,0,1,0,0,1,0,0
			}
		}
		LayerElementUV: 0 {
			Version: 101
			Name: ""UVMap""
			MappingInformationType: ""ByPolygonVertex""
			ReferenceInformationType: ""Direct""
			UV: *48 {
				a: 0,0,1,0,1,1,0,1,0,0,1,0,1,1,0,1,0,0,1,0,1,1,0,1,0,0,1,0,1,1,0,1,0,0,1,0,1,1,0,1,0,0,1,0,1,1,0,1
			}
		}
		Layer: 0 {
			Version: 100
			LayerElement:  {
				Type: ""LayerElementNormal""
				TypedIndex: 0
			}
			LayerElement:  {
				Type: ""LayerElementUV""
				TypedIndex: 0
			}
		}
	}
	Model: 200, ""Model::Cube"", ""Mesh"" {
		Version: 232
		Properties70:  {
		}
		Shading: T
		Culling: ""CullingOff""
	}
}
Connections:  {
	C: ""OO"",200,0
	C: ""OO"",100,200
}
";

        [SetUp]
        public void SetUp()
        {
            System.IO.File.WriteAllText(AssetPath, CubeFbx);
            AssetDatabase.ImportAsset(AssetPath, ImportAssetOptions.ForceUpdate);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(AssetPath);
        }

        [Test]
        public void Import_AppliesTheRules()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(AssetPath);

            Assert.That(importer.globalScale, Is.EqualTo(1f));
            Assert.That(importer.useFileScale, Is.True);
            Assert.That(importer.bakeAxisConversion, Is.True);
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import));
            Assert.That(importer.importTangents, Is.EqualTo(ModelImporterTangents.CalculateMikk));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.materialLocation, Is.EqualTo(ModelImporterMaterialLocation.InPrefab));
            Assert.That(importer.materialName, Is.EqualTo(ModelImporterMaterialName.BasedOnMaterialName));
        }

        [Test]
        public void Reimporter_FindsTheModelAndReimportsIt()
        {
            Assert.That(ModelReimporter.FindModelPaths(), Does.Contain(AssetPath));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[ModelImport\] SM_Prop_ImportTest\.fbx: 1 mesh, 12 triangles"));
            LogAssert.Expect(LogType.Log, new Regex(@"^\[ModelImport\] Reimported \d+ model\(s\)"));
            ModelReimporter.ReimportAll();
        }

        [Test]
        public void Import_DoesNotCreateFilesNextToTheModel()
        {
            Assert.That(AssetDatabase.IsValidFolder(ModelImportRules.Folder + "Materials"), Is.False);
        }

        [Test]
        public void Import_KeepsMetresAndCountsTriangles()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetPath);

            Assert.That(mesh, Is.Not.Null);
            Assert.That(MeshStats.CountTriangles(mesh), Is.EqualTo(12));
            Assert.That(MeshStats.HasUv0(mesh), Is.True);
            Assert.That(mesh.bounds.size.x, Is.EqualTo(1f).Within(0.001f));
            Assert.That(mesh.bounds.size.y, Is.EqualTo(1f).Within(0.001f));
            Assert.That(mesh.bounds.size.z, Is.EqualTo(1f).Within(0.001f));
        }
    }
}
