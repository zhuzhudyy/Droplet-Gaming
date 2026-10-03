using System;
using System.IO;
using System.Linq;
using DropletPrototype.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class CinematicFleetLayoutTests
    {
        [Test]
        public void AuthoredFullFleetRetainsIdentitiesAndRealCubicDepth()
        {
            var current = CinematicFleetLayout.ReadLayout();
            var old = JsonUtility.FromJson<CinematicFleetLayout.Layout>(File.ReadAllText("Tools/Blender/Fleet2000Sun/layout_config.json"));
            CollectionAssert.AreEquivalent(old.markers.Select(m => m.name), current.markers.Select(m => m.name));
            Assert.AreEqual(2000, current.markers.Length);
            Assert.AreEqual(4, current.markers.Select(m => m.position[2]).Distinct().Count());
            Assert.AreEqual(25, current.markers.Select(m => m.position[1]).Distinct().Count());
            Assert.AreEqual(20, current.markers.Select(m => m.position[0]).Distinct().Count());
            var min = current.markers.Select(m => CinematicFleetLayout.V(m.position)).Aggregate(Vector3.Min);
            var max = current.markers.Select(m => CinematicFleetLayout.V(m.position)).Aggregate(Vector3.Max);
            Vector3 span = max - min;
            Assert.That(Mathf.Max(span.x, span.y, span.z) / Mathf.Min(span.x, span.y, span.z), Is.LessThan(1.05));
            Assert.AreEqual(4, current.markers.Count(m => Mathf.Abs(m.position[0]) < .01f && Mathf.Abs(m.position[1] - 8) < .01f));
        }

        [Test]
        public void EveryBowUsesTheFixedCombatSpawnNotTheSunOrLivePlayer()
        {
            var layout = CinematicFleetLayout.ReadLayout();
            Vector3 spawn = CinematicFleetLayout.V(layout.playerSpawn);
            foreach (var marker in layout.markers)
            {
                Vector3 forward = CinematicFleetLayout.V(marker.forward);
                Vector3 toSpawn = (spawn - CinematicFleetLayout.V(marker.position)).normalized;
                Assert.Greater(Vector3.Dot(forward, toSpawn), .999999f, marker.name);
                Quaternion rotation = Quaternion.LookRotation(forward, CinematicFleetLayout.V(marker.up));
                // Existing calibrated +Z bow / -Z exhaust, independent of visual-root LOD.
                Assert.Greater(Vector3.Dot(rotation * Vector3.forward, toSpawn), .99999f, marker.name);
                Assert.Less(Vector3.Dot(rotation * Vector3.back, toSpawn), -.99999f, marker.name);
            }
        }

        [Test]
        public void UnsafeDuplicateAndReversedBowAreRejectedBeforeSceneMutation()
        {
            var layout = CinematicFleetLayout.ReadLayout();
            layout.markers[1].id = layout.markers[0].id;
            layout.markers[1].name = layout.markers[0].name;
            Assert.Throws<InvalidOperationException>(() => CinematicFleetLayout.ValidateLayout(layout));
            layout = CinematicFleetLayout.ReadLayout();
            layout.markers[0].forward = layout.markers[0].forward.Select(v => -v).ToArray();
            Assert.Throws<InvalidOperationException>(() => CinematicFleetLayout.ValidateLayout(layout));
        }

        [Test]
        public void ExportMatchesTheSingleAuthoringAuthorityByteForByte()
        {
            CollectionAssert.AreEqual(File.ReadAllBytes("Tools/Blender/CinematicFleet/layout_config.json"),
                File.ReadAllBytes(CinematicFleetLayout.ExportPath));
        }
    }
}
