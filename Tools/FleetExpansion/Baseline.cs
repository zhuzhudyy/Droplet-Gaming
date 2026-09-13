using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using DropletPrototype;
public static class FleetExpansionBaseline
{
    [Serializable] public class Target { public string id,prefab; public Vector3 position,size; public int colliders,renderers; }
    [Serializable] public class Report { public string scene,unity; public int count; public Vector3 centerMin,centerMax,centerSize; public float nearestMin,nearestMedian,nearestMax; public Target[] targets; public float missionSeconds,cruise,boost,boundary,warning; }
    public static string Run()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.isDirty || Application.isPlaying) throw new Exception("Baseline requires saved edit scene.");
        var m=UnityEngine.Object.FindFirstObjectByType<MissionController>();
        var ts=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ShipTarget>(true)).OrderBy(t=>t.targetId).ToArray();
        var distances=ts.Select(t=>ts.Where(o=>o!=t).Min(o=>Vector3.Distance(t.transform.position,o.transform.position))).OrderBy(x=>x).ToArray();
        var bounds=new Bounds(ts[0].transform.position,Vector3.zero); foreach(var t in ts) bounds.Encapsulate(t.transform.position);
        var report=new Report{scene=scene.path,unity=Application.unityVersion,count=ts.Length,centerMin=bounds.min,centerMax=bounds.max,centerSize=bounds.size,nearestMin=distances.First(),nearestMedian=(distances[(distances.Length-1)/2]+distances[distances.Length/2])*.5f,nearestMax=distances.Last(),missionSeconds=m.settings.missionSeconds,cruise=m.settings.maxCruiseSpeed,boost=m.settings.maxCruiseSpeed*m.settings.boostMultiplier,boundary=m.settings.boundaryRadius,warning=m.settings.boundaryWarningRadius,
        targets=ts.Select(t=>{var rs=t.visualRoot.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return new Target{id=t.targetId,position=t.transform.position,size=b.size,prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),colliders=t.hitVolumes.Length,renderers=rs.Length};}).ToArray()};
        var json=JsonUtility.ToJson(report,true);Directory.CreateDirectory("docs/verification/FleetExpansion");File.WriteAllText("docs/verification/FleetExpansion/baseline-scene.json",json);return json;
    }
}
