using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DropletPrototype.Editor
{
    public static class FleetAssaultBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/FleetAssault.unity";
        public const string PrefabFolder = "Assets/_Project/Prefabs/Fleet/";
        const string Materials = "Assets/_Project/Art/FleetMaterials/";
        [MenuItem("DropletPrototype/Create or Update Fleet Assault")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play before authoring FleetAssault.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save unsaved scene changes before updating FleetAssault.");
            EnsureAssets();
            var scene=SceneManager.GetActiveScene();
            if(scene.path!=ScenePath)scene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)!=null?EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single):EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var roots=scene.GetRootGameObjects().Where(g=>g.GetComponent<FleetSceneRoot>()!=null).ToArray();
            if(roots.Length>1)throw new InvalidOperationException("Multiple owned FleetSceneRoot objects; resolve before regeneration.");
            bool first=roots.Length==0;
            var root=first?new GameObject("FleetAssault"):roots[0];
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Update FleetAssault");
            if(first){Undo.RegisterCreatedObjectUndo(root,"Create FleetAssault");root.AddComponent<FleetSceneRoot>();}
            else Undo.RegisterFullObjectHierarchyUndo(root,"Update FleetAssault references");
            var fleet=Child(root.transform,"GeneratedFleet");
            var targets=FleetLayoutImporter.Import(AssetDatabase.LoadAssetAtPath<GameObject>(FleetAssetPipeline.ModelFolder+"FleetLayout.fbx"),fleet,
                Prefab("Frigate"),Prefab("Cruiser"),Prefab("Command"));
            var settings=AssetDatabase.LoadAssetAtPath<DropletSettings>("Assets/_Project/Data/FleetMission.asset");
            var player=root.transform.Find("PlayerRoot");
            if(player==null){player=((GameObject)PrefabUtility.InstantiatePrefab(Prefab("DropletFlight"),root.transform)).transform;player.name="PlayerRoot";Undo.RegisterCreatedObjectUndo(player.gameObject,"Create player");player.position=new Vector3(0,8,0);}
            var motor=player.GetComponent<DropletMotor>();
            var camera=Child(root.transform,"Chase Camera").GetComponent<Camera>();
            if(camera==null)
            {
                camera=root.transform.Find("Chase Camera").gameObject.AddComponent<Camera>();camera.tag="MainCamera";camera.farClipPlane=5000;
                camera.nearClipPlane=.1f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.003f,.006f,.015f);
                camera.gameObject.AddComponent<AudioListener>();camera.allowHDR=true;
            }
            var chase=Get<ChaseCamera>(camera.gameObject);chase.target=motor;chase.settings=settings;chase.ResetCamera();
            var systems=Child(root.transform,"Mission Systems").gameObject;
            var score=Get<ScoreSystem>(systems);score.settings=settings;
            var mission=Get<MissionController>(systems);mission.settings=settings;mission.motor=motor;mission.input=motor.input;mission.chaseCamera=chase;mission.score=score;mission.targets=targets;
            mission.spawnPosition=new Vector3(0,8,0);mission.arenaCenter=new Vector3(0,20,260);motor.mission=mission;
            var options=Get<PlayerOptions>(systems);options.mission=mission;
            var effects=Get<MissionEffects>(Child(root.transform,"Presentation Pool").gameObject);
            effects.settings=AssetDatabase.LoadAssetAtPath<EffectSettings>("Assets/_Project/Data/FleetEffects.asset");effects.mission=mission;effects.motor=motor;effects.chaseCamera=chase;
            effects.flashMaterial=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Impact.mat");effects.sparkMaterial=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Spark.mat");
            effects.wreckPrefabs=new[]{Prefab("FrigateWreck"),Prefab("CruiserWreck"),Prefab("CommandWreck")};
            effects.impactClips=new[]{Audio("Impact_Light"),Audio("Impact_Heavy")};effects.flightClip=Audio("Flight_Resonance");
            var benchmark=Get<FleetBenchmark>(systems);benchmark.mission=mission;benchmark.effects=effects;
            foreach(var target in targets){var p=Get<DestructionPresenter>(target.gameObject);p.target=target;p.effects=effects;p.wreckKind=target.targetId.Contains("Small")?0:target.targetId.Contains("Large")?1:2;PrefabUtility.RecordPrefabInstancePropertyModifications(p);}
            var trail=Get<DropletTrail>(player.gameObject);trail.motor=motor;trail.mission=mission;trail.effects=effects;trail.settings=effects.settings;trail.trailMaterial=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Trail.mat");
            var hud=Get<HudPresenter>(Child(root.transform,"HUD").gameObject);hud.mission=mission;hud.viewCamera=camera;hud.options=options;hud.effects=effects;
            if(first)FleetEnvironmentBuilder.Create(Child(root.transform,"Environment"));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Undo.CollapseUndoOperations(group);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)}.Concat(EditorBuildSettings.scenes.Where(s=>s.path!=ScenePath)).ToArray();
            Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,30,220),Quaternion.Euler(24,-26,0),370);
            Debug.Log("FleetAssault saved: "+targets.Length+" independent targets.");
        }
        static AudioClip Audio(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/Fleet/"+name+".wav");
        static GameObject Prefab(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder+name+".prefab");
        static T Get<T>(GameObject go) where T:Component=>go.GetComponent<T>()??go.AddComponent<T>();
        static Transform Child(Transform parent,string name){var t=parent.Find(name);if(t!=null)return t;var go=new GameObject(name);go.transform.SetParent(parent,false);Undo.RegisterCreatedObjectUndo(go,"Create "+name);return go.transform;}
        public static void EnsureAssets()
        {
            foreach(string path in new[]{Materials,PrefabFolder,"Assets/_Project/Audio/Fleet/"})Directory.CreateDirectory(path);
            if(Directory.Exists("ArtSource/Audio/Exports"))foreach(var file in Directory.GetFiles("ArtSource/Audio/Exports","*.wav"))File.Copy(file,"Assets/_Project/Audio/Fleet/"+Path.GetFileName(file),true);
            AssetDatabase.Refresh();
            Mat("Hull",new Color(.42f,.52f,.63f),.6f,.48f);Mat("Armor",new Color(.12f,.19f,.27f),.65f,.4f);
            Mat("Trim",new Color(.7f,.75f,.78f),.5f,.55f);Mat("Engine",new Color(.09f,.4f,.6f),.15f,.6f,new Color(.1f,.65f,1.1f));
            Mat("MetalDroplet",new Color(.86f,.9f,.95f),1,.94f);
            Mat("Wreck",new Color(.17f,.2f,.24f),.5f,.25f,new Color(.11f,.026f,.004f));
            FxMat("Impact",new Color(1,.36f,.08f,1));FxMat("Spark",new Color(1,.3f,.06f,1));FxMat("Trail",new Color(.3f,.7f,1,.4f));
            string settingsPath="Assets/_Project/Data/FleetMission.asset";
            var settings=AssetDatabase.LoadAssetAtPath<DropletSettings>(settingsPath);
            if(settings==null){settings=ScriptableObject.CreateInstance<DropletSettings>();settings.missionSeconds=240;settings.boundaryWarningRadius=610;settings.boundaryRadius=730;settings.comboWindow=6;settings.cameraDistance=8;settings.cameraHeight=2.2f;settings.initialSpeed=24;settings.maxCruiseSpeed=52;AssetDatabase.CreateAsset(settings,settingsPath);}
            string fxPath="Assets/_Project/Data/FleetEffects.asset";if(AssetDatabase.LoadAssetAtPath<EffectSettings>(fxPath)==null)AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<EffectSettings>(),fxPath);
            foreach(string kind in new[]{"Frigate","Cruiser","Command"}){EnsureShip(kind);EnsureWreck(kind);}
            if(Prefab("DropletFlight")==null)
            {
                var player=new GameObject("DropletFlight");
                try
                {
                    var visual=new GameObject("VisualRoot");visual.transform.SetParent(player.transform,false);Model("Droplet",visual.transform,false);
                    var m=player.AddComponent<DropletMotor>();m.settings=settings;m.visualRoot=visual.transform;m.input=player.AddComponent<DropletInput>();m.input.motor=m;
                    m.hitDetector=player.AddComponent<DropletHitDetector>();m.hitDetector.settings=settings;PrefabUtility.SaveAsPrefabAsset(player,PrefabFolder+"DropletFlight.prefab");
                }finally{UnityEngine.Object.DestroyImmediate(player);}
            }
            AssetDatabase.SaveAssets();
        }
        static void EnsureShip(string kind)
        {
            if(Prefab(kind)!=null)return;var go=new GameObject(kind);
            try
            {
                var visual=new GameObject("VisualRoot");visual.transform.SetParent(go.transform,false);Model(kind,visual.transform,false);
                var hit=new GameObject("HitVolumes");hit.transform.SetParent(go.transform,false);
                void Box(string name,Vector3 center,Vector3 size){var c=new GameObject(name);c.transform.SetParent(hit.transform,false);c.layer=LayerMask.NameToLayer("ShipTarget");var box=c.AddComponent<BoxCollider>();box.center=center;box.size=size;box.isTrigger=true;}
                if(kind=="Frigate") {Box("Main hull",new Vector3(0,.1f,1),new Vector3(4.5f,3.4f,19));Box("Port drive",new Vector3(-3.45f,0,-5),new Vector3(2.1f,2.6f,12));Box("Starboard drive",new Vector3(3.45f,0,-5),new Vector3(2.1f,2.6f,12));}
                else {Box("Main hull",Vector3.zero,new Vector3(10,6,kind=="Command"?36:32));Box("Port armor",new Vector3(-7,0,-2),new Vector3(5,5,23));Box("Starboard armor",new Vector3(7,0,-2),new Vector3(5,5,23));Box("Dorsal structure",new Vector3(0,4,-3),new Vector3(6,4,12));}
                var target=go.AddComponent<ShipTarget>();target.visualRoot=visual;target.hitVolumes=hit.GetComponentsInChildren<Collider>();
                TuneVolumes(go,kind);
                var anchor=new GameObject("EffectsAnchor");anchor.transform.SetParent(go.transform,false);
                go.AddComponent<DestructionPresenter>().target=target;
                PrefabUtility.SaveAsPrefabAsset(go,PrefabFolder+kind+".prefab");
            }finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void EnsureWreck(string kind)
        {
            if(Prefab(kind+"Wreck")!=null)return;var go=new GameObject(kind+"Wreck");
            try {Model(kind+"Wreck",go.transform,true);PrefabUtility.SaveAsPrefabAsset(go,PrefabFolder+kind+"Wreck.prefab");}finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void Model(string name,Transform parent,bool wreck)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(FleetAssetPipeline.ModelFolder+name+".fbx");
            if(asset==null)throw new InvalidOperationException("Missing exported model: "+name);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(asset,parent);
            foreach(var renderer in model.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>AssetDatabase.LoadAssetAtPath<Material>(Materials+(name=="Droplet"?"MetalDroplet":wreck?"Wreck":m!=null&&m.name.Contains("Engine")?"Engine":m!=null&&m.name.Contains("Armor")?"Armor":m!=null&&m.name.Contains("Trim")?"Trim":"Hull")+".mat")).ToArray();
            }
        }
        static void Mat(string name,Color color,float metal,float smooth,Color emission=default)
        {
            string path=Materials+name+".mat";if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)return;
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);
            if(emission.maxColorComponent>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",emission);}
            AssetDatabase.CreateAsset(m,path);
        }
        static void FxMat(string name,Color color){string path=Materials+name+".mat";if(AssetDatabase.LoadAssetAtPath<Material>(path)!=null)return;var m=new Material(Shader.Find("DropletPrototype/Effect")){name=name};m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,path);}
        // Explicit refinement for the assets authored in this batch. Existing manual children are retained.
        public static void RefineCurrentBatchAssets()
        {
            foreach(string kind in new[]{"Frigate","Cruiser","Command"})
            {
                string path=PrefabFolder+kind+".prefab";var root=PrefabUtility.LoadPrefabContents(path);
                try{TuneVolumes(root,kind);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var armor=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Armor.mat");armor.SetColor("_BaseColor",new Color(.20f,.28f,.36f));EditorUtility.SetDirty(armor);
            var wreck=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Wreck.mat");wreck.SetColor("_BaseColor",new Color(.24f,.26f,.29f));EditorUtility.SetDirty(wreck);
            var droplet=AssetDatabase.LoadAssetAtPath<Material>(Materials+"MetalDroplet.mat");droplet.SetFloat("_Metallic",.92f);droplet.SetFloat("_Smoothness",.90f);EditorUtility.SetDirty(droplet);
            var trail=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Trail.mat");trail.SetFloat("_UseUVFade",1);EditorUtility.SetDirty(trail);
            var impact=AssetDatabase.LoadAssetAtPath<Material>(Materials+"Impact.mat");impact.SetFloat("_UseUVFade",2);EditorUtility.SetDirty(impact);
            AssetDatabase.SaveAssets();
        }
        static void TuneVolumes(GameObject root,string kind)
        {
            var volumes=root.transform.Find("HitVolumes");
            void Box(string name,Vector3 center,Vector3 size)
            {
                var t=volumes.Find(name);if(t==null){t=new GameObject(name).transform;t.SetParent(volumes,false);t.gameObject.layer=LayerMask.NameToLayer("ShipTarget");}
                var c=t.GetComponent<BoxCollider>();if(c==null)c=t.gameObject.AddComponent<BoxCollider>();c.center=center;c.size=size;c.isTrigger=true;
            }
            if(kind=="Frigate")
            {
                Box("Main hull",new Vector3(0,-.1f,.5f),new Vector3(4.2f,3.2f,21));
                Box("Port drive",new Vector3(-3.45f,-.1f,-4.1f),new Vector3(2.1f,2.6f,14));
                Box("Starboard drive",new Vector3(3.45f,-.1f,-4.1f),new Vector3(2.1f,2.6f,14));
            }
            else
            {
                Box("Main hull",new Vector3(0,-.35f,.5f),new Vector3(11.5f,5.9f,33));
                Box("Port armor",new Vector3(-7.1f,.3f,-1),new Vector3(4.5f,5,30));
                Box("Starboard armor",new Vector3(7.1f,.3f,-1),new Vector3(4.5f,5,30));
                Box("Dorsal structure",new Vector3(0,3.05f,-3),new Vector3(8,3.1f,16));
                if(kind=="Command")
                {
                    Box("Command crown",new Vector3(0,4.96f,-2.75f),new Vector3(6,3.67f,10.5f));
                    Box("Command prow",new Vector3(0,.07f,16),new Vector3(2.38f,1.24f,10));
                    Box("Port command wing",new Vector3(-9.7f,.6f,-4),new Vector3(2.99f,2.2f,18));
                    Box("Starboard command wing",new Vector3(9.7f,.6f,-4),new Vector3(2.99f,2.2f,18));
                }
            }
            root.GetComponent<ShipTarget>().hitVolumes=volumes.GetComponentsInChildren<Collider>();
        }
    }
}
