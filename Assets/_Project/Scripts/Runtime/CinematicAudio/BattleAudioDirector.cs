using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace DropletPrototype
{
    /// <summary>Local presentation of the existing immutable combat events. Never drives simulation.</summary>
    [DisallowMultipleComponent]
    public sealed class BattleAudioDirector : MonoBehaviour
    {
        public MissionController mission;
        public FleetCombatSimulation fleet;
        public RadioController radio;
        public Transform listener;
        public Camera viewCamera;
        public ShotDirector shots;
        public AudioMixerGroup worldGroup;
        public SeedAudioMixController mix;
        public AudioClip[] laserClips = Array.Empty<AudioClip>(), reflectClips = Array.Empty<AudioClip>(), impactClips = Array.Empty<AudioClip>(), explosionClips = Array.Empty<AudioClip>();
        public AudioClip[] laserBreachClips = Array.Empty<AudioClip>(), reactorClips = Array.Empty<AudioClip>(), retreatEngineClips = Array.Empty<AudioClip>(), escapeClips = Array.Empty<AudioClip>();
        [Range(4,16)] public int poolCapacity = 12;
        public float Intensity { get; private set; }
        public string DensityState => Intensity < .18f ? "Calm / aftermath" : Intensity < .45f ? "Engaging" : Intensity < .75f ? "Heavy combat" : "Chaos";
        public int ReceivedEvents { get; private set; }
        public int PlayedWorldEvents { get; private set; }
        public int DroppedWorldEvents { get; private set; }
        public int ActiveWorldSources { get { int count=0; foreach(var slot in slots) if(slot.remaining>0) count++; return count; } }
        public int MaxWorldSourcesObserved { get; private set; }
        public int Capacity => slots.Length;
        readonly HashSet<long> processed = new HashSet<long>();
        readonly Queue<long> processedOrder = new Queue<long>();
        readonly float[] activity = new float[32];
        sealed class Slot
        {
            public AudioSource source;
            public float remaining, importance, baseVolume, emphasis;
            public long eventId;
            public Vector3 eventPosition, eventOrigin;
            public string targetId;
            public ShipTarget follow;
            public bool sustained;
            public CombatEventKind sustainedKind;
        }
        Slot[] slots = Array.Empty<Slot>();
        FleetCombatSimulation boundFleet;
        MissionController boundMission;
        ShotDirector boundShots;
        float time, binTime, recentActivity, lastWorldAt=-100;
        int bin, variant;
        bool paused;

        void OnEnable() { Bind(); }
        void Start() { InitializePool(); Bind(); }
        public void Bind()
        {
            if(boundShots!=shots)
            {
                if(boundShots!=null){boundShots.ShotStarted-=EmphasizeShot;boundShots.ShotEnded-=EndEmphasis;}
                boundShots=shots;
                if(boundShots!=null){boundShots.ShotStarted+=EmphasizeShot;boundShots.ShotEnded+=EndEmphasis;}
            }
            if(boundFleet!=fleet)
            {
                if(boundFleet!=null) { boundFleet.EventRaised-=Receive; boundFleet.SimulationReset-=ResetAudio; }
                boundFleet=fleet;
                if(boundFleet!=null) { boundFleet.EventRaised+=Receive; boundFleet.SimulationReset+=ResetAudio; }
            }
            if(boundMission!=mission)
            {
                if(boundMission!=null) { boundMission.StateChanged-=StateChanged; boundMission.Restarted-=ResetAudio; }
                boundMission=mission;
                if(boundMission!=null) { boundMission.StateChanged+=StateChanged; boundMission.Restarted+=ResetAudio; }
            }
        }
        void EmphasizeShot(CombatEvent message,CombatShotKind kind)
        {
            // Mix existing sound only; never call Receive or Play for a camera transition.
            foreach(var slot in slots)if(slot.remaining>0)
            { slot.emphasis=slot.eventId==message.eventId?1.12f:.92f; ApplyVolume(slot); }
        }
        void EndEmphasis(){foreach(var slot in slots)if(slot.remaining>0){slot.emphasis=1;ApplyVolume(slot);}}
        void ApplyVolume(Slot slot)
        {
            float gain=mix!=null?mix.CombatGain:radio!=null&&radio.IsBroadcasting?.72f:1f;
            slot.source.volume=slot.baseVolume*slot.emphasis*gain;
        }
        public void InitializePool()
        {
            if(slots.Length>0) return;
            slots=new Slot[Mathf.Clamp(poolCapacity,4,16)];
            for(int i=0;i<slots.Length;i++)
            {
                var go=new GameObject("WorldAudio_"+i); go.transform.SetParent(transform,false);
                var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.dopplerLevel=0;source.rolloffMode=AudioRolloffMode.Linear;
                source.minDistance=Units(20000);source.maxDistance=Units(1200000);source.outputAudioMixerGroup=worldGroup;
                slots[i]=new Slot{source=source};
            }
        }
        float Units(float meters) => fleet!=null && fleet.scale!=null ? fleet.scale.MetersToUnits(meters) : meters*.01f;
        void StateChanged(MissionState state)
        {
            paused=state==MissionState.Paused;
            foreach(var slot in slots) { if(paused)slot.source.Pause();else slot.source.UnPause(); }
            if(state==MissionState.Ready || state==MissionState.Results) ResetAudio();
        }
        public void ResetAudio()
        {
            foreach(var slot in slots) StopSlot(slot);
            Array.Clear(activity,0,activity.Length); processed.Clear();processedOrder.Clear();
            Intensity=recentActivity=time=binTime=0;bin=variant=0;lastWorldAt=-100;paused=false;
            ReceivedEvents=PlayedWorldEvents=DroppedWorldEvents=MaxWorldSourcesObserved=0;
            if(radio!=null)radio.CombatIntensity=0;
        }
        static void StopSlot(Slot slot)
        {
            slot.source.Stop();
            slot.source.loop=false;
            slot.source.clip=null;
            slot.remaining=slot.importance=0;
            slot.targetId=null;
            slot.follow=null;
            slot.sustained=false;
            slot.sustainedKind=default;
        }
        void StopSustained(string targetId)
        {
            if(string.IsNullOrEmpty(targetId))return;
            foreach(var slot in slots)
                if(slot.sustained && slot.targetId==targetId)StopSlot(slot);
        }
        void Update() { Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            if(paused || dt<=0)return;
            time+=dt;binTime+=dt;
            while(binTime>=.25f) { binTime-=.25f;bin=(bin+1)%activity.Length;activity[bin]=0; }
            recentActivity=0;
            for(int i=0;i<activity.Length;i++) recentActivity+=activity[(bin-i+activity.Length)%activity.Length]*Mathf.Exp(-i*.25f/2.6f);
            float target=Mathf.Clamp01(recentActivity/1.7f);
            Intensity=Mathf.Lerp(Intensity,target,1-Mathf.Exp(-dt/(target>Intensity?.55f:1.8f)));
            if(radio!=null)radio.CombatIntensity=Intensity;
            Vector3 origin=fleet!=null?fleet.AccumulatedOriginOffset:Vector3.zero;
            foreach(var slot in slots)
            {
                if(slot.remaining<=0)continue;
                if(slot.sustained && slot.follow!=null && slot.follow.IsResolved)
                { StopSlot(slot);continue; }
                if(!slot.sustained)slot.remaining-=dt;
                slot.source.transform.position=slot.sustained && slot.follow!=null ?
                    slot.follow.transform.position : slot.eventPosition+slot.eventOrigin-origin;
                if(slot.remaining<=0)StopSlot(slot);
                else ApplyVolume(slot);
            }
        }
        public void Receive(CombatEvent message)
        {
            if(paused || (fleet!=null && message.generation!=fleet.Generation))return;
            if(mission!=null && mission.State!=MissionState.Playing)return;
            if(message.eventId!=0)
            {
                if(!processed.Add(message.eventId))return;
                processedOrder.Enqueue(message.eventId);if(processedOrder.Count>1024)processed.Remove(processedOrder.Dequeue());
            }
            // Lifecycle cleanup is authoritative even when the resolved ship is
            // distant or the explosion/escape event is too old to present anew.
            if(message.kind==CombatEventKind.ShipExploded || message.kind==CombatEventKind.ShipEscaped)
                StopSustained(message.targetId);
            if(fleet!=null && fleet.SimulatedTime-message.simulationTime>.35f){DroppedWorldEvents++;return;}
            ReceivedEvents++;
            Vector3 position=message.PositionAtOrigin(fleet!=null?fleet.AccumulatedOriginOffset:Vector3.zero);
            float distance=listener!=null?Vector3.Distance(listener.position,position):0;
            float proximity=1/(1+distance/Mathf.Max(1,Units(240000)));
            float activityWeight=0,importance=0;AudioClip[] clips=null;
            switch(message.kind)
            {
                case CombatEventKind.WeaponFired: activityWeight=.018f;importance=.25f;clips=laserClips;break;
                case CombatEventKind.DropletContact: activityWeight=.085f;importance=.9f;clips=reflectClips;break;
                case CombatEventKind.HullPenetrated:
                    activityWeight=.30f;importance=1;
                    clips=message.source==DamageSource.Penetration ? impactClips :
                        laserBreachClips.Length>0 ? laserBreachClips : impactClips;
                    break;
                case CombatEventKind.ShipExploded:activityWeight=.24f;importance=.85f;clips=explosionClips;break;
                case CombatEventKind.RetreatOrdered:activityWeight=.04f;importance=.36f;clips=retreatEngineClips;break;
                case CombatEventKind.ReactorUnstable:activityWeight=.09f;importance=.72f;clips=reactorClips;break;
                case CombatEventKind.ShipEscaped:activityWeight=.02f;importance=.35f;clips=escapeClips;break;
            }
            activity[bin]=Mathf.Min(1.4f,activity[bin]+activityWeight*proximity);
            if(clips==null || clips.Length==0)return;
            bool sustained=message.kind==CombatEventKind.ReactorUnstable ||
                message.kind==CombatEventKind.RetreatOrdered;
            if(sustained && !string.IsNullOrEmpty(message.targetId))
                foreach(var activeSlot in slots)
                    if(activeSlot.sustained && activeSlot.targetId==message.targetId &&
                        activeSlot.sustainedKind==message.kind && activeSlot.remaining>0)return;
            if(distance>Units(1200000)){DroppedWorldEvents++;return;}
            // Nearby impacts are immediate. Repeated distant fire is coalesced by a short
            // receiver budget, without producing sounds for events that did not happen.
            if(!sustained && importance<.5f && time-lastWorldAt<Mathf.Lerp(.14f,.07f,Intensity)){DroppedWorldEvents++;return;}
            InitializePool();int index=-1;float weakest=float.PositiveInfinity;
            int budget=Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(5,poolCapacity,Intensity)),4,slots.Length);
            float priority=importance*proximity;
            if(viewCamera!=null){var p=viewCamera.WorldToViewportPoint(position);if(p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1)priority+=.1f;}
            for(int i=0;i<budget;i++)
            {
                if(slots[i].remaining<=0){index=i;break;}
                if(slots[i].importance<weakest){weakest=slots[i].importance;index=i;}
            }
            if(index<0 || (slots[index].remaining>0 && priority<weakest)){DroppedWorldEvents++;return;}
            var clip=clips[(variant++)%clips.Length];if(clip==null)return;
            var slot=slots[index];StopSlot(slot);slot.source.clip=clip;slot.source.loop=sustained;
            slot.eventPosition=message.position;slot.eventOrigin=message.originOffset;slot.source.transform.position=position;
            slot.importance=priority;slot.remaining=sustained?float.PositiveInfinity:clip.length;
            slot.sustained=sustained;slot.targetId=sustained?message.targetId:null;
            slot.follow=sustained?message.subject:null;
            slot.sustainedKind=sustained?message.kind:default;
            slot.eventId=message.eventId;slot.baseVolume=message.kind==CombatEventKind.ShipExploded?.68f:.52f;
            slot.emphasis=1;ApplyVolume(slot);
            slot.source.priority=importance>.7f?70:150;slot.source.Play();lastWorldAt=time;PlayedWorldEvents++;
            MaxWorldSourcesObserved=Mathf.Max(MaxWorldSourcesObserved,ActiveWorldSources);
        }
        void OnDisable()
        {
            if(boundFleet!=null){boundFleet.EventRaised-=Receive;boundFleet.SimulationReset-=ResetAudio;}
            if(boundMission!=null){boundMission.StateChanged-=StateChanged;boundMission.Restarted-=ResetAudio;}
            if(boundShots!=null){boundShots.ShotStarted-=EmphasizeShot;boundShots.ShotEnded-=EndEmphasis;}
            boundFleet=null;boundMission=null;boundShots=null;ResetAudio();
        }
    }
}
