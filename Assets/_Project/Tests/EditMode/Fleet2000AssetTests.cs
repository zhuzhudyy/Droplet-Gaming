using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using DropletPrototype.Editor;
namespace DropletPrototype.Tests.EditMode
{
    public sealed class Fleet2000AssetTests
    {
        [Test]public void LayoutHasExactly2000EqualScaleUniquePoses()
        {
            var layout=Fleet2000Pipeline.ReadLayout();Assert.That(layout.markers.Length,Is.EqualTo(2000));
            Assert.That(layout.columns,Is.EqualTo(50));Assert.That(layout.rows,Is.EqualTo(20));Assert.That(layout.layers,Is.EqualTo(2));
            Assert.That(layout.markers.Select(m=>m.id).Distinct().Count(),Is.EqualTo(2000));
            Assert.That(layout.markers.All(m=>m.scale.SequenceEqual(new float[]{1,1,1})),Is.True);
        }
        [Test]public void ExistingModelAndNewSceneRemainSeparate()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Fleet2000Pipeline.Baseline),Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Fleet2000Pipeline.ScenePath),Is.Not.Null);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Fleet2000Pipeline.PrefabPath);Assert.That(prefab,Is.Not.Null);
            var target=prefab.GetComponent<ShipTarget>();Assert.That(target.hitVolumes.Length,Is.EqualTo(9));
            var lod=target.visualRoot.GetComponent<LODGroup>();Assert.That(lod.lodCount,Is.EqualTo(3));
            Assert.That(lod.GetLODs().SelectMany(l=>l.renderers).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).All(m=>m.enableInstancing),Is.True);
        }
    }
}
