using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using DropletPrototype;
using DropletPrototype.Editor;
public static class SolarSpatialProbe
{
    public static string Run()
    {
        SolarLayoutPipeline.Guard();var b=UnityEngine.Object.FindAnyObjectByType<SolarSystemBackdrop>();var rig=GameObject.Find("PreviewOnly_SolarLayout");SolarLayoutPipeline.Require(rig!=null,"Open solar review.");
        var original=b.observer;var groups=b.GetComponentsInChildren<LODGroup>();var method=typeof(SpaceEnvironmentFrameProbe).GetMethod("NativeGroup",BindingFlags.Static|BindingFlags.NonPublic);var views=new List<object>();
        var cameras=rig.GetComponentsInChildren<Camera>();foreach(var c in cameras)
        {
            b.observer=c;b.ApplyMapping(c);var data=groups.Select(g=>(SpaceEnvironmentFrameProbe.NativeGroupRecord)method.Invoke(null,new object[]{c,g})).ToArray();
            views.Add(new{camera=c.name,lod0=data.Count(x=>x.activeLODLevel==0),lod1=data.Count(x=>x.activeLODLevel==1),lod2=data.Count(x=>x.activeLODLevel==2),culled=data.Count(x=>x.nativeLodCulled),frustumSelectedResourceTriangleEstimate=data.Where(x=>x.selectedRendererIntersectsFrustum).Sum(x=>x.selectedMeshTriangles),groups=data});
        }
        var rock=b.GetComponentsInChildren<Transform>().Single(t=>t.name=="POSE_rock-001");var a=cameras.Single(c=>c.name=="ParallaxA");var z=cameras.Single(c=>c.name=="ParallaxB");
        b.observer=a;b.ApplyMapping(a);Vector3 rockA=a.WorldToViewportPoint(rock.position);Vector3 sunA=a.WorldToViewportPoint(b.sunProxy.position);
        b.observer=z;b.ApplyMapping(z);Vector3 rockB=z.WorldToViewportPoint(rock.position);Vector3 sunB=z.WorldToViewportPoint(b.sunProxy.position);
        float rockPixels=Vector2.Distance(new Vector2(rockA.x,rockA.y),new Vector2(rockB.x,rockB.y))*1280;
        float sunPixels=Vector2.Distance(new Vector2(sunA.x,sunA.y),new Vector2(sunB.x,sunB.y))*1280;
        SolarLayoutPipeline.Require(rockPixels>50&&sunPixels<.01f,"Parallax contract differs.");
        var sample=new GameObject("Solar temporary LOD diagnostic");var cam=sample.AddComponent<Camera>();cam.fieldOfView=65;cam.nearClipPlane=.1f;cam.farClipPlane=25000;cam.enabled=false;var diagnostic=new List<object>();
        try
        {
            var g=rock.GetComponent<LODGroup>();float size=g.size*rock.lossyScale.x;
            var heights=new[]{.2f,.04f,.004f,.0001f};for(int i=0;i<heights.Length;i++)
            {
                float distance=QualitySettings.lodBias*size/(2*heights[i]*Mathf.Tan(65*Mathf.Deg2Rad/2));cam.transform.position=rock.position-Vector3.forward*distance;cam.transform.rotation=Quaternion.identity;
                var d=(SpaceEnvironmentFrameProbe.NativeGroupRecord)method.Invoke(null,new object[]{cam,g});SolarLayoutPipeline.Require(i==3?d.nativeLodCulled:d.activeLODLevel==i,"Native automatic LOD level failed.");diagnostic.Add(new{requested=i==3?"culled":"LOD"+i,distance,beyondFlightBoundary=distance>b.Layout.boundaryRadius,beyondCameraFarClip=distance>cam.farClipPlane,result=d});
            }
        }
        finally {UnityEngine.Object.DestroyImmediate(sample);b.observer=original;b.ApplyMapping(original);}
        SolarLayoutPipeline.Write("spatial-native-lod.json",new{passed=true,views,diagnostic,rockHorizontalViewportShiftPixels=rockPixels,sunViewportShiftPixels=sunPixels,baselineMetres=Vector3.Distance(a.transform.position,z.transform.position),notes="Native LODUtility selection query, not measured GPU draws. Pixel shifts use actual camera projection, not image occupancy. Frustum triangle totals exclude ungrouped Sun/Sky and remain estimates. All camera poses restored."});
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());return "Spatial / native LOD PASS";
    }
}
