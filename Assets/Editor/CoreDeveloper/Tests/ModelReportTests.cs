using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class ModelReportTests
    {
        private static readonly Vector3 GeneratorSize = new Vector3(6.96f, 4.1f, 2.9f);

        private static List<MeshEntry> Meshes(params int[] triangles)
        {
            var list = new List<MeshEntry>();
            for (int i = 0; i < triangles.Length; i++)
            {
                list.Add(new MeshEntry("Part" + i, triangles[i], true));
            }

            return list;
        }

        [Test]
        public void Build_SumsTrianglesAndPrintsEachMesh()
        {
            ModelReport report = ModelReport.Build("Assets/Models/Custom/SM_Generator.fbx",
                Meshes(6000, 3874), 5, GeneratorSize);

            Assert.That(report.TotalTriangles, Is.EqualTo(9874));
            Assert.That(report.Text, Does.Contain("SM_Generator.fbx: 2 meshes, 9,874 triangles (target 10,000, max 15,000)"));
            Assert.That(report.Text, Does.Contain("5 materials, size 6.96 x 4.10 x 2.90 m"));
            Assert.That(report.Text, Does.Contain("Part0: 6,000 triangles"));
            Assert.That(report.Text, Does.Contain("Part1: 3,874 triangles"));
            Assert.That(report.Warnings, Is.Empty);
        }

        [Test]
        public void Build_WarnsAboveTheHardMax()
        {
            ModelReport report = ModelReport.Build("Assets/Models/Custom/SM_Generator.fbx",
                Meshes(15001), 5, GeneratorSize);

            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.Warnings[0], Does.Contain("over the hard max of 15,000"));
        }

        [Test]
        public void Build_ExactlyAtTheHardMaxIsFine()
        {
            ModelReport report = ModelReport.Build("Assets/Models/Custom/SM_Generator.fbx",
                Meshes(15000), 5, GeneratorSize);

            Assert.That(report.Warnings, Is.Empty);
        }

        [Test]
        public void Build_WarnsWhenThereAreTooManyMaterials()
        {
            ModelReport report = ModelReport.Build("Assets/Models/Custom/SM_Prop_Desk.fbx",
                Meshes(500), 3, new Vector3(3f, 0.78f, 1f));

            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.Warnings[0], Does.Contain("3 materials is over the limit of 2"));
        }

        [Test]
        public void Build_WarnsAboutAMeshWithoutUv0()
        {
            var meshes = new List<MeshEntry> { new MeshEntry("Flywheel", 200, false) };

            ModelReport report = ModelReport.Build("Assets/Models/Custom/SM_Generator.fbx", meshes, 1, GeneratorSize);

            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.Warnings[0], Does.Contain("mesh 'Flywheel' has no UV0"));
        }

        [Test]
        public void Build_WarnsWhenTheFileNameHasNoSmPrefix()
        {
            ModelReport report = ModelReport.Build("Assets/Models/Custom/Generator.fbx",
                Meshes(100), 1, GeneratorSize);

            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.Warnings[0], Does.Contain("should start with SM_"));
            Assert.That(report.Text, Does.Not.Contain("target"));
        }

        [TestCase(100f, 100f, 100f)]
        [TestCase(0.01f, 0.01f, 0.01f)]
        public void Build_WarnsAboutAScaleThatLooksLikeAUnitMistake(float x, float y, float z)
        {
            ModelReport report = ModelReport.Build("Assets/Models/Custom/SM_Prop_Desk.fbx",
                Meshes(500), 1, new Vector3(x, y, z));

            Assert.That(report.Warnings, Has.Count.EqualTo(1));
            Assert.That(report.Warnings[0], Does.Contain("check the export scale"));
        }

        [Test]
        public void Build_UsesSingularWordsForOneMeshAndOneMaterial()
        {
            ModelReport report = ModelReport.Build("Assets/Models/Custom/SM_Prop_Desk.fbx",
                Meshes(500), 1, new Vector3(3f, 0.78f, 1f));

            Assert.That(report.Text, Does.Contain("1 mesh, 500 triangles"));
            Assert.That(report.Text, Does.Contain("1 material,"));
        }
    }
}
