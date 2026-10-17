using System;
using System.Collections.Generic;
using System.IO;
using Blackout.Shared;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class DressingLayoutTests
    {
        private static string Table(string row) => DressingLayout.Begin + "\n" + row + "\n" + DressingLayout.End;
        private const string Row = "| SecurityArea | Desk01 | Prop_Desk | 6.6 | 2.5 | 76.6 | 90 | 1 | 1 | 1 |";

        [Test]
        public void DocumentTable_UsesExistingRoomsAndUniqueIds()
        {
            BunkerLayout layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            var rows = DressingLayout.Parse(File.ReadAllText(DressingLayout.DocumentPath));
            Assert.That(rows.Count, Is.GreaterThan(40));
            foreach (DressingSpec row in rows)
                Assert.That(layout.FindRoom(row.Room), Is.Not.Null, row.Id);
        }

        [Test]
        public void Parse_PreservesMetresYawAndScale()
        {
            DressingSpec spec = DressingLayout.Parse(Table(Row))[0];
            Assert.That(spec.Position, Is.EqualTo(new Vector3(6.6f, 2.5f, 76.6f)));
            Assert.That(spec.Yaw, Is.EqualTo(90));
            Assert.That(spec.Scale, Is.EqualTo(Vector3.one));
            Assert.That(spec.PrefabPath, Is.EqualTo("Assets/Prefabs/Props/Prop_Desk.prefab"));
        }

        [Test]
        public void Parse_RejectsDuplicateIds() => Assert.Throws<FormatException>(() => DressingLayout.Parse(Table(Row + "\n" + Row)));

        [TestCase("no table")]
        [TestCase("<!-- W10-DRESSING-BEGIN -->\n<!-- W10-DRESSING-END -->")]
        public void Parse_RejectsMissingOrEmptyTable(string text)
            => Assert.Throws<FormatException>(() => DressingLayout.Parse(text));

        [TestCase("| A | B | ../Other | 0 | 0 | 0 | 0 | 1 | 1 | 1 |")]
        [TestCase("| A | B | Prop_Desk | NaN | 0 | 0 | 0 | 1 | 1 | 1 |")]
        [TestCase("| A | B | Prop_Desk | 0 | 0 | 0 | 0 | 0 | 1 | 1 |")]
        [TestCase("| A | B |")]
        public void Parse_RejectsUnsafeOrMalformedRow(string row)
            => Assert.Throws<FormatException>(() => DressingLayout.Parse(Table(row)));

        [TestCase(0, 3, 90, "1.2 m walking path")]
        [TestCase(0, 7.5f, 61, "1.2 m ramp walking path")]
        [TestCase(1.4f, 3, 95, "dynamic barricade space")]
        [TestCase(-11, 4, 106, "generator space")]
        [TestCase(0, 4, 135, "cure chamber space")]
        public void Clearance_ProtectsRoutesAndGameplay(float x, float y, float z, string reason)
            => Assert.That(DressingLayout.ClearanceIssue(new Bounds(new Vector3(x, y, z), Vector3.one * 0.1f),
                Array.Empty<MarkerSpec>()), Is.EqualTo(reason));

        [Test]
        public void Clearance_LeavesHeadroomPropsButProtectsMarkerStandingSpace()
        {
            var markers = new List<MarkerSpec> { new MarkerSpec(MarkerType.CoverPoint, "Cover", "", new Vector3(6, 2.5f, 130), 0) };
            Assert.That(DressingLayout.ClearanceIssue(new Bounds(new Vector3(6, 3, 130), Vector3.one), markers), Is.EqualTo("Cover"));
            Assert.That(DressingLayout.ClearanceIssue(new Bounds(new Vector3(0, 5.1f, 103), Vector3.one * 0.2f), markers), Is.Null);
        }
    }
}
