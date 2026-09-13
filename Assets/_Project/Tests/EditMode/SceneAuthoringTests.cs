using System;
using System.Linq;
using DropletPrototype.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class SceneAuthoringTests
    {
        [Test]
        public void SavedRangeSurvivesReopenAndRepeatedBuildPreservesManualObjects()
        {
            TestRangeBuilder.Build();
            var sentinel = new GameObject("G01_ManualPreservationProbe");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            try
            {
                TestRangeBuilder.Build(); TestRangeBuilder.Build();
                EditorSceneManager.OpenScene(TestRangeBuilder.ScenePath, OpenSceneMode.Single);
                var roots = SceneManager.GetActiveScene().GetRootGameObjects();
                Assert.AreEqual(1, roots.Count(r => r.GetComponent<GeneratedRangeRoot>() != null));
                Assert.IsTrue(roots.Any(r => r.name == "G01_ManualPreservationProbe"));
                var owned = roots.Single(r => r.GetComponent<GeneratedRangeRoot>() != null);
                var fleet = owned.transform.Find("GeneratedFleet");
                Assert.AreEqual(10, fleet.childCount);
                foreach (Transform ship in fleet)
                {
                    Assert.AreEqual(Vector3.one, ship.localScale);
                    Assert.IsNotNull(ship.Find("VisualRoot")); Assert.IsNotNull(ship.Find("HitVolumes"));
                    Assert.IsTrue(PrefabUtility.IsPartOfPrefabInstance(ship));
                }
                Assert.AreEqual(1, owned.GetComponentsInChildren<Camera>().Length);
                Assert.AreEqual(Vector3.one, owned.transform.Find("PlayerRoot").localScale);
                Assert.AreEqual(5, AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project/Art/Materials" }).Length);
            }
            finally
            {
                var probe = GameObject.Find("G01_ManualPreservationProbe");
                if (probe != null) UnityEngine.Object.DestroyImmediate(probe);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
        }

        [Test]
        public void DirtySceneIsRefusedRatherThanOverwritten()
        {
            TestRangeBuilder.Build();
            var probe = new GameObject("G01_UnsavedProbe");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            try { Assert.Throws<InvalidOperationException>(() => TestRangeBuilder.Build()); Assert.IsNotNull(probe); }
            finally { UnityEngine.Object.DestroyImmediate(probe); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); }
        }
    }
}
