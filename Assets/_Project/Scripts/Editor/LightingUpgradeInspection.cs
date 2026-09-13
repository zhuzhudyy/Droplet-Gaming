using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DropletPrototype.Editor
{
    public static class LightingUpgradeInspection
    {
        [MenuItem("DropletPrototype/Lighting Upgrade/2 Probe Diagnostics")]
        public static void ProbeDiagnostics()
        {
            var rig=UnityEngine.Object.FindAnyObjectByType<SolarLightingRig>();
            string status="frame="+Time.frameCount+" rendered="+Time.renderedFrameCount+" realtimeProbes="+QualitySettings.realtimeReflectionProbes;
            if(rig!=null)status+=" captures="+rig.CaptureCount+" fresh="+rig.ProbeHasFreshCapture+" probeEnabled="+rig.localProbe.enabled+" texture="+(rig.localProbe.texture==null?"null":rig.localProbe.texture.name)+" size="+rig.localProbe.size+" camera="+rig.backdrop.observer.enabled;
            Directory.CreateDirectory(Evidence);File.AppendAllText(Evidence+"probe-diagnostics.txt",DateTime.UtcNow.ToString("o")+" "+status+"\n");Debug.Log(status);
        }
        public const string Evidence="docs/verification/LightingUpgrade/";
        [MenuItem("DropletPrototype/Lighting Upgrade/0 Capture Current Views")]
        public static void CaptureViews()
        {
            Directory.CreateDirectory(Evidence);
            var m=UnityEngine.Object.FindFirstObjectByType<MissionController>();
            var b=UnityEngine.Object.FindFirstObjectByType<SolarSystemBackdrop>();
            var c=m.chaseCamera.GetComponent<Camera>();
            var p=c.transform.position;var q=c.transform.rotation;float fov=c.fieldOfView;
            string prefix=m.gameObject.scene.name.Contains("Lighting")?"After":"Before";
            var sun=b.Layout.FindBody("sun");SolarLayoutMath.TryProjectBody(b.Layout,sun,p,out var projection);
            var t=m.targets.OrderBy(x=>Vector3.Distance(x.transform.position,m.spawnPosition)).First().transform;
            var rock=b.GetComponentsInChildren<MeshRenderer>(true).First(r=>r.sharedMaterial!=null&&r.sharedMaterial.name.Contains("Rock"));
            try
            {
                Shot("Spawn",p,q,65);
                Shot("SpawnSun",p,Quaternion.LookRotation(projection.direction),65);
                var edge=new Vector3(1000,350,300);Shot("Outskirts",edge,Quaternion.LookRotation(projection.direction),65);
                var open=new Vector3(-2800,700,-1200);Shot("OpenSpace",open,Quaternion.LookRotation(projection.direction),65);
                Shot("EngineRear",t.position+new Vector3(0,0,-23),Quaternion.LookRotation(Vector3.forward),55);
                Shot("EngineQuarter",t.position+new Vector3(17,9,-26),Quaternion.LookRotation(t.position-new Vector3(0,0,4)-(t.position+new Vector3(17,9,-26))),55);
                Shot("EngineFar",t.position+new Vector3(0,0,-190),Quaternion.LookRotation(Vector3.forward),65);
                var extra=c.GetUniversalAdditionalCameraData();bool post=extra.renderPostProcessing;
                try{extra.renderPostProcessing=false;Shot("EngineRear-NoPost",t.position+new Vector3(0,0,-23),Quaternion.LookRotation(Vector3.forward),55);}
                finally{extra.renderPostProcessing=post;}
                Shot("FleetPanorama",new Vector3(950,720,-1150),Quaternion.LookRotation(new Vector3(0,40,600)-new Vector3(950,720,-1150)),65);
                var rb=rock.bounds;var rp=rb.center+new Vector3(-.6f,.35f,-1).normalized*rb.size.magnitude*1.05f;
                Shot("RockDetail",rp,Quaternion.LookRotation(rb.center-rp),55);
                var lit=rb.center+(projection.direction+new Vector3(.35f,.35f,-.05f)).normalized*rb.size.magnitude*1.05f;
                Shot("RockSunlitDetail",lit,Quaternion.LookRotation(rb.center-lit),55);
            }
            finally{c.transform.SetPositionAndRotation(p,q);c.fieldOfView=fov;b.ApplyMapping(c);}
            void Shot(string name,Vector3 pos,Quaternion rot,float f)
            {c.transform.SetPositionAndRotation(pos,rot);c.fieldOfView=f;b.ApplyMapping(c);Capture(c,Evidence+prefix+"-"+name+".png");}
            File.WriteAllText(Evidence+prefix+"-scene.txt",Describe());
            Debug.Log("LIGHTING VIEWS SAVED "+prefix);
        }
        public static void Capture(Camera c,string path)
        {
            var rt=RenderTexture.GetTemporary(1920,1080,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=c.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{c.targetTexture=rt;c.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{c.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);RenderTexture.ReleaseTemporary(rt);}
        }
        public static string Describe()
        {
            var b=UnityEngine.Object.FindFirstObjectByType<SolarSystemBackdrop>();
            string body(Transform t)=>t.name+" scale "+t.localScale+"\n"+string.Join("\n",t.GetComponentsInChildren<MeshRenderer>(true).Select(r=>r.name+" "+r.GetComponent<MeshFilter>().sharedMesh.bounds+" "+r.sharedMaterial.name+" "+r.sharedMaterial.shader.name));
            return Application.unityVersion+" "+SystemInfo.graphicsDeviceName+" "+SystemInfo.graphicsDeviceType+" HW RT="+SystemInfo.supportsRayTracing+"\n"+
                body(b.sunProxy)+"\n"+body(b.earthProxy)+"\n"+
                string.Join("\n",UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l=>l.name+" intensity "+l.intensity+" forward "+l.transform.forward+" enabled "+l.enabled))+"\n"+
                string.Join("\n",UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None).Select(v=>v.name+" "+AssetDatabase.GetAssetPath(v.sharedProfile)))+"\n"+
                string.Join("\n",AssetDatabase.FindAssets("t:ScriptableRendererData").Select(AssetDatabase.GUIDToAssetPath));
        }
    }
}
