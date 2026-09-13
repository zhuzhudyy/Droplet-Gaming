using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DropletPrototype
{
    // One scene-wide solar direction and one bounded nearby reflection capture.
    [ExecuteAlways,DefaultExecutionOrder(250)]
    public sealed class SolarLightingRig : MonoBehaviour
    {
        public SolarSystemBackdrop backdrop;
        public Light sunLight;
        public LightingQualityProfile settings;
        public ReflectionProbe localProbe;
        public Volume postVolume;
        public MissionEffects missionEffects;
        public MissionController mission;
        public FusionDriveVisuals[] drives;
        public bool enhancedReflections=true;
        public bool bloomEnabled=true;
        public bool engineEffects=true;
        int capture=-1;
        float nextCapture;
        Vector3 capturedPosition;
        EffectQuality lastQuality=(EffectQuality)(-1);
        bool lastEngine;
        bool refreshRequested=true;
        bool awaitingCapture;
        RenderTexture captureTexture;
        bool previousRealtimeProbes,ownsRealtimeSetting;
        public bool ProbeHasFreshCapture { get; private set; }
        public int CaptureCount { get; private set; }
        void OnEnable()
        {
            if(Application.isPlaying){previousRealtimeProbes=QualitySettings.realtimeReflectionProbes;QualitySettings.realtimeReflectionProbes=true;ownsRealtimeSetting=true;}
            capture=-1;nextCapture=0;CaptureCount=0;lastQuality=(EffectQuality)(-1);RequestProbeRefresh();ApplyLighting();
            if(Application.isPlaying&&mission!=null){mission.Restarted+=RequestProbeRefresh;foreach(var target in mission.targets)if(target!=null)target.Destroyed+=OnTargetDestroyed;}
        }
        void OnDisable()
        {
            if(mission!=null){mission.Restarted-=RequestProbeRefresh;if(mission.targets!=null)foreach(var target in mission.targets)if(target!=null)target.Destroyed-=OnTargetDestroyed;}
            if(localProbe!=null)localProbe.realtimeTexture=null;
            if(captureTexture!=null){captureTexture.Release();if(Application.isPlaying)Destroy(captureTexture);else DestroyImmediate(captureTexture);captureTexture=null;}
            if(ownsRealtimeSetting){QualitySettings.realtimeReflectionProbes=previousRealtimeProbes;ownsRealtimeSetting=false;}
        }
        void OnTargetDestroyed(ShipTarget target)=>RequestProbeRefresh();
        public void RequestProbeRefresh()
        {refreshRequested=true;ProbeHasFreshCapture=false;if(localProbe!=null){localProbe.realtimeTexture=null;localProbe.size=Vector3.zero;}}
        void LateUpdate(){ApplyLighting();if(Application.isPlaying)UpdateProbe();}
        public void ApplyLighting()
        {
            if(backdrop==null||settings==null||sunLight==null)return;
            var camera=backdrop.observer;
            if(camera!=null&&backdrop.Layout!=null&&SolarLayoutMath.TryProjectBody(backdrop.Layout,backdrop.Layout.FindBody("sun"),camera.transform.position,out var sun))
                sunLight.transform.rotation=Quaternion.LookRotation(-sun.direction,Vector3.up);
            sunLight.intensity=settings.sunIntensity;sunLight.color=settings.sunColor;RenderSettings.sun=sunLight;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.48f,.61f,.8f)*settings.ambientStrength;
            EffectQuality quality=missionEffects==null?EffectQuality.High:missionEffects.Quality;
            if(postVolume!=null)postVolume.enabled=bloomEnabled&&quality!=EffectQuality.Off;
            if(localProbe!=null)localProbe.enabled=enhancedReflections&&quality==EffectQuality.High;
            if(Application.isPlaying&&(quality!=lastQuality||lastEngine!=engineEffects))
            {
                if(drives!=null)foreach(var drive in drives)if(drive!=null)drive.SetQuality(engineEffects?quality:EffectQuality.Off);
                lastQuality=quality;lastEngine=engineEffects;
            }
        }
        void UpdateProbe()
        {
            if(localProbe==null||!localProbe.enabled||backdrop==null||backdrop.observer==null||settings==null)return;
            if(capture>=0&&!localProbe.IsFinishedRendering(capture))return;
            if(awaitingCapture)
            {
                awaitingCapture=false;
                if(!refreshRequested){captureTexture.IncrementUpdateCount();localProbe.realtimeTexture=captureTexture;localProbe.size=Vector3.one*500;ProbeHasFreshCapture=true;}
            }
            var p=backdrop.observer.transform.position;
            if(Time.unscaledTime<nextCapture)return;
            if(!refreshRequested&&CaptureCount>0&&(p-capturedPosition).sqrMagnitude<settings.probeMoveDistance*settings.probeMoveDistance)return;
            localProbe.transform.position=p;capturedPosition=p;
            if(captureTexture==null)
            {captureTexture=new RenderTexture(localProbe.resolution,localProbe.resolution,16,RenderTextureFormat.DefaultHDR){name="Shared nearby reflection capture",dimension=TextureDimension.Cube,useMipMap=true,autoGenerateMips=false};captureTexture.Create();}
            localProbe.realtimeTexture=null;localProbe.size=Vector3.zero;ProbeHasFreshCapture=false;refreshRequested=false;
            capture=localProbe.RenderProbe(captureTexture);
            // Unity may immediately assign the explicit output; publish it only after all faces finish.
            localProbe.realtimeTexture=null;awaitingCapture=true;CaptureCount++;nextCapture=Time.unscaledTime+settings.probeInterval;
        }
    }
}
