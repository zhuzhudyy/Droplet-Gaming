using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DropletPrototype.Editor
{
    public static class FleetEnvironmentBuilder
    {
        const string Folder = "Assets/_Project/Art/FleetMaterials/";
        public static void Create(Transform parent)
        {
            var key = NewLight("Cold key", parent, new Color(.8f,.9f,1), 2.6f, new Vector3(38,-35,0));
            NewLight("Blue fill", parent, new Color(.36f,.55f,1), 1.1f, new Vector3(-25,135,0));
            NewLight("Rim", parent, new Color(.9f,.92f,1), 1.6f, new Vector3(12,170,0));
            key.shadows = LightShadows.Soft;
            var earth = GameObject.CreatePrimitive(PrimitiveType.Sphere); earth.name = "Earth - distant non-target";
            earth.transform.SetParent(parent,false); earth.transform.localPosition = new Vector3(-1050,-570,2450);
            earth.transform.localScale = Vector3.one * 1550;
            Object.DestroyImmediate(earth.GetComponent<Collider>());
            earth.GetComponent<Renderer>().sharedMaterial = Material("Earth", "DropletPrototype/Earth");
            earth.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var stars = new GameObject("Starfield - single static mesh"); stars.transform.SetParent(parent,false);
            const string meshPath = Folder + "Starfield.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                var vertices = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
                var rng = new System.Random(6209);
                for (int i=0;i<1200;i++)
                {
                    float y=(float)rng.NextDouble()*2-1, a=(float)rng.NextDouble()*Mathf.PI*2;
                    Vector3 direction=new Vector3(Mathf.Sqrt(1-y*y)*Mathf.Cos(a),y,Mathf.Sqrt(1-y*y)*Mathf.Sin(a));
                    Vector3 center=direction*3800, right=Vector3.Cross(direction,Vector3.up).normalized, up=Vector3.Cross(right,direction);
                    float size=.7f+(float)rng.NextDouble()*1.5f;
                    Color color=Color.Lerp(new Color(.22f,.32f,.5f),new Color(.85f,.87f,.78f),(float)rng.NextDouble());
                    int b=vertices.Count;
                    vertices.Add(center+(-right-up)*size); vertices.Add(center+(right-up)*size);
                    vertices.Add(center+(right+up)*size); vertices.Add(center+(-right+up)*size);
                    for(int k=0;k<4;k++)colors.Add(color);
                    triangles.AddRange(new[]{b,b+1,b+2,b,b+2,b+3});
                }
                mesh=new Mesh{name="Authored starfield"};mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh,meshPath);
            }
            stars.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=stars.AddComponent<MeshRenderer>(); renderer.sharedMaterial=Material("Stars","DropletPrototype/Stars");
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            ConfigureRenderSettings();
        }
        public static void ConfigureRenderSettings()
        {
            RenderSettings.skybox=null; RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.19f,.26f,.4f);RenderSettings.ambientEquatorColor=new Color(.11f,.16f,.24f);RenderSettings.ambientGroundColor=new Color(.07f,.1f,.16f);
            const string path=Folder+"DeepSpaceReflection.asset";
            var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if(cube==null)
            {
                int size=128;cube=new Cubemap(size,TextureFormat.RGBAHalf,true){name="Deep space reflection with broad cold light sources"};
                for(int face=0;face<6;face++)
                {
                    Color[] pixels=new Color[size*size];
                    for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                    {
                        float u=2*(x+.5f)/size-1,v=2*(y+.5f)/size-1;
                        Vector3 d=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);d.Normalize();
                        float band=Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(d.y-.35f)*3),5);
                        float light=Mathf.Pow(Mathf.Max(0,Vector3.Dot(d,new Vector3(-.5f,.6f,-.5f).normalized)),20);
                        float edge=Mathf.Pow(Mathf.Max(0,1-Mathf.Abs(d.x-.4f)*5),9)*Mathf.Clamp01(d.y+.5f);
                        pixels[y*size+x]=new Color(.06f,.095f,.17f)+new Color(.9f,1.05f,1.25f)*(band*1.8f+light*4+edge*2);
                    }
                    cube.SetPixels(pixels,(CubemapFace)face);
                }
                cube.Apply(true,false);AssetDatabase.CreateAsset(cube,path);
            }
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;RenderSettings.reflectionIntensity=1;
        }
        static Light NewLight(string name,Transform parent,Color color,float intensity,Vector3 euler)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localRotation=Quaternion.Euler(euler);var l=go.AddComponent<Light>();l.type=LightType.Directional;l.color=color;l.intensity=intensity;return l;}
        static Material Material(string name,string shader)
        {string path=Folder+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find(shader)){name=name};AssetDatabase.CreateAsset(m,path);}return m;}
    }
}
