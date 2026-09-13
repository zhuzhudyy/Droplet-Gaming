using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Editor
{
    public static class FusionFleetVerification
    {
        [Serializable] public class RepeatReport {public bool passed,identitiesRetained,manualContentRetained,environmentRetained,reopenedConsistent;public int count;public string signature;}
        static ShipTarget[] Targets()=>UnityEngine.Object.FindObjectsByType<ShipTarget>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(t=>t.targetId).ToArray();
        static string Pose(Transform t)=>t.name+":"+t.localPosition.ToString("F5")+":"+t.localRotation.ToString("F5")+":"+t.localScale.ToString("F5");
        static string EnvironmentState()
        {
            var root=UnityEngine.Object.FindAnyObjectByType<SolarSystemBackdrop>();
            return string.Join("\n",root.GetComponentsInChildren<Transform>(true).Select(t=>Pose(t)+":"+t.gameObject.activeSelf+":"+string.Join(",",t.GetComponents<Component>().Select(c=>c==null?"MISSING":EditorJsonUtility.ToJson(c)))));
        }
        [MenuItem("DropletPrototype/Fleet Expansion/6 Verify Repeat and Reopen")]
        public static void Repeat()
        {
            FusionFleetPipeline.Require(!Application.isPlaying&&SceneManager.GetActiveScene().path==FusionFleetPipeline.ScenePath&&!SceneManager.GetActiveScene().isDirty,"Open the saved expanded scene in Edit mode.");
            var scene=SceneManager.GetActiveScene();var original=Targets();var ids=original.Select(t=>t.GetEntityId()).ToArray();var before=EnvironmentState();var config=FusionFleetPipeline.ReadConfig();var signature=FusionFleetPipeline.Validate(scene,config).signature;
            var sentinel=new GameObject("Temporary_manual_preservation_probe");sentinel.transform.position=new Vector3(3,5,7);
            var internalSentinel=new GameObject("Temporary_manual_fleet_child");internalSentinel.transform.SetParent(original[0].transform.parent.parent,false);
            try
            {
                EditorSceneManager.SaveScene(scene);FusionFleetPipeline.ImportModel();FusionFleetPipeline.Build();FusionFleetPipeline.Build();
                var report=new RepeatReport{count=Targets().Length,identitiesRetained=Targets().Select(t=>t.GetEntityId()).SequenceEqual(ids),manualContentRetained=sentinel!=null&&internalSentinel!=null&&sentinel.transform.position==new Vector3(3,5,7),environmentRetained=before==EnvironmentState(),signature=FusionFleetPipeline.Validate(SceneManager.GetActiveScene(),config).signature};
                FusionFleetPipeline.Require(report.identitiesRetained&&report.manualContentRetained&&report.environmentRetained&&signature==report.signature,"Repeated generation changed identity/manual content/environment/poses.");
                UnityEngine.Object.DestroyImmediate(sentinel);UnityEngine.Object.DestroyImmediate(internalSentinel);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                EditorSceneManager.OpenScene(FusionFleetPipeline.Baseline,OpenSceneMode.Single);scene=EditorSceneManager.OpenScene(FusionFleetPipeline.ScenePath,OpenSceneMode.Single);
                report.reopenedConsistent=FusionFleetPipeline.Validate(scene,config).signature==signature;report.passed=report.reopenedConsistent;File.WriteAllText(FusionFleetPipeline.Evidence+"repeat-reopen.json",JsonUtility.ToJson(report,true));FusionFleetPipeline.Require(report.passed,"Reopened signature changed.");
                Debug.Log("FLEET repeat/reopen/manual preservation passed.");
            }
            finally
            {
                if(sentinel!=null)UnityEngine.Object.DestroyImmediate(sentinel);if(internalSentinel!=null)UnityEngine.Object.DestroyImmediate(internalSentinel);
            }
        }
        [MenuItem("DropletPrototype/Fleet Expansion/7 Save Inspection Views")]
        public static void Screens()
        {
            FusionFleetPipeline.Require(!Application.isPlaying&&SceneManager.GetActiveScene().path==FusionFleetPipeline.ScenePath&&!SceneManager.GetActiveScene().isDirty,"Open the saved expanded scene in Edit mode.");
            var m=UnityEngine.Object.FindAnyObjectByType<MissionController>();var camera=m.chaseCamera.GetComponent<Camera>();var pos=camera.transform.position;var rot=camera.transform.rotation;var target=camera.targetTexture;float aspect=camera.aspect;
            var environment=UnityEngine.Object.FindAnyObjectByType<SolarSystemBackdrop>();var transforms=environment.GetComponentsInChildren<Transform>(true);var positions=transforms.Select(t=>t.localPosition).ToArray();var rotations=transforms.Select(t=>t.localRotation).ToArray();var scales=transforms.Select(t=>t.localScale).ToArray();
            var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32);var active=RenderTexture.active;
            try
            {
                var first=Targets().First();var center=new Vector3(55,40,820);
                var views=new[]{("Expanded-Spawn",pos,pos+camera.transform.forward*100),("Fleet-Panorama",new Vector3(1050,1150,-900),center),("Squadron-Spacing",new Vector3(320,360,-100),new Vector3(50,50,390)),("Single-Ship-Inspection",first.transform.position+new Vector3(14,7,-22),first.transform.position)};
                foreach(var v in views)
                {
                    camera.transform.SetPositionAndRotation(v.Item2,Quaternion.LookRotation(v.Item3-v.Item2));camera.targetTexture=rt;camera.aspect=1920f/1080;camera.Render();RenderTexture.active=rt;
                    var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(FusionFleetPipeline.Evidence+v.Item1+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                camera.targetTexture=target;camera.aspect=aspect;camera.transform.SetPositionAndRotation(pos,rot);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
                for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];transforms[i].localScale=scales[i];}
            }
            Debug.Log("FLEET four camera inspection views saved; scene transforms restored.");
        }
    }
}
