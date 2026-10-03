using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Timeline;

namespace DropletPrototype.Editor
{
    /// <summary>Root agent invokes this on the safe scene copy. Never edits accepted Enhancement assets.</summary>
    public static class CinematicAudioAssets
    {
        public const string DataRoot="Assets/_Project/Data/CinematicAudio";
        public const string AudioRoot="Assets/_Project/Audio/CinematicAudio";
        public const string LibraryPath=DataRoot+"/CinematicRadio.asset";
        public const string MixerPath=DataRoot+"/CinematicAudio.mixer";
        public const string TimelinePath=DataRoot+"/TheOpenCorridor.playable";
        public static void Configure(MissionController mission)
        {
            if(Application.isPlaying || mission==null || mission.narrative?.radio==null)throw new InvalidOperationException("Configure saved narrative in safe scene copy first.");
            Directory.CreateDirectory(DataRoot);
            var content=JsonUtility.FromJson<RadioContent>(File.ReadAllText(DataRoot+"/CinematicRadioContent.json",Encoding.UTF8));
            if(content.narrative.Length!=40 || content.combat.Length!=36)throw new InvalidOperationException("The real 76-line Seed bank must be complete.");
            var radio=mission.narrative.radio;var narrative=mission.narrative;
            var library=AssetDatabase.LoadAssetAtPath<RadioLibrary>(LibraryPath);
            if(library==null){library=ScriptableObject.CreateInstance<RadioLibrary>();AssetDatabase.CreateAsset(library,LibraryPath);}
            Undo.RecordObject(library,"Configure complete local communications");
            library.narrative=content.narrative;
            var extras=content.sceneExtras.Where(x=>x.id!="HELP").ToArray();
            foreach(var extra in extras)
            {
                extra.weight=.9f;extra.maximumIntensity=1;
                if(extra.id=="SC01"){extra.eventKind="AttackIneffective";extra.maximumIntensity=.45f;}
                if(extra.id=="SA01"){extra.eventKind="HullPenetrated";extra.pendingOnly=true;extra.important=true;}
                if(extra.id=="SE01"){extra.eventKind="RetreatOrdered";extra.evacuationOnly=true;extra.important=true;}
            }
            library.combat=content.combat.Concat(extras).ToArray();
            float cursor=0;
            foreach(var line in library.narrative)
            {
                line.voice=Import(line.id);line.duration=line.voice.length;line.time=cursor;line.premixed=true;
                cursor+=line.duration+.16f;
            }
            foreach(var line in library.combat)
            {
                line.maximumIntensity=line.id=="SC01"?.45f:1;
                line.voice=Import(line.id);line.lowIntensityVoice=Import(line.id+"_low");line.highIntensityVoice=Import(line.id+"_high");line.premixed=true;
                if(line.pendingOnly || line.eventKind=="HullPenetrated" || line.eventKind=="ReactorUnstable" || line.eventKind=="RescueRequested")line.shortVoice=Import("HELP");
            }
            library.connectTone=Import("Connect_1");library.interruptTone=Import("Disconnect_1");
            library.alarm=null;library.equipmentBed=null;library.premixedSceneAudio=true;
            library.voiceProvenance="AI synthesized English / Seed-TTS 2.0 / original cabin scenes / subjective listening pending";
            library.queueLimit=4;library.eventBudgetPerFrame=24;library.globalCooldown=1.6f;library.eventExpiry=3.5f;library.speakerEventCooldown=14;library.recentLineLimit=7;
            var mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if(mixer==null)
            {
                if(!AssetDatabase.CopyAsset(EnhancementVoiceAssets.MixerPath,MixerPath))throw new InvalidOperationException("Copy the existing working mixer through Editor API.");
                mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            }
            Undo.RecordObject(radio,"Wire local premixed radio scenes");
            radio.library=library;radio.showVoiceProvenance=true;radio.normalMix=mixer.FindSnapshot("Normal");radio.broadcastMix=mixer.FindSnapshot("Broadcast");
            Assign(radio.voiceSource,Group(mixer,"Voice"));Assign(radio.signalSource,Group(mixer,"Signal"));
            Assign(radio.alarmSource,Group(mixer,"Alarm"));Assign(radio.bedSource,Group(mixer,"Background"));
            var world=radio.GetComponent<BattleAudioDirector>() ?? Undo.AddComponent<BattleAudioDirector>(radio.gameObject);
            Undo.RecordObject(world,"Wire event-driven local world audio");
            world.mission=mission;world.fleet=mission.combat;world.radio=radio;world.listener=mission.motor.transform;
            world.shots=mission.GetComponentInChildren<ShotDirector>(true);
            world.viewCamera=mission.chaseCamera.GetComponent<Camera>();world.worldGroup=Group(mixer,"Combat");world.poolCapacity=12;
            world.laserClips=Variants("Laser");world.reflectClips=Variants("Reflect");world.impactClips=Variants("Impact");world.explosionClips=Variants("Explosion");
            foreach(var root in mission.gameObject.scene.GetRootGameObjects())
            {
                foreach(var effects in root.GetComponentsInChildren<MissionEffects>(true))
                {Undo.RecordObject(effects,"Use one event-world sound owner");effects.worldAudioEnabled=false;effects.outputMixerGroup=Group(mixer,"Combat");EditorUtility.SetDirty(effects);}
                foreach(var beam in root.GetComponentsInChildren<LaserBeamPool>(true))
                {Undo.RecordObject(beam,"Use one event-world sound owner");beam.worldAudioEnabled=false;beam.outputMixerGroup=Group(mixer,"Combat");EditorUtility.SetDirty(beam);}
            }
            var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if(timeline==null){timeline=ScriptableObject.CreateInstance<TimelineAsset>();AssetDatabase.CreateAsset(timeline,TimelinePath);}
            Undo.RecordObject(timeline,"Align narrative to real audio samples");
            foreach(var track in timeline.GetRootTracks().ToArray())timeline.DeleteTrack(track);
            timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=cursor+.25f;
            var broadcast=timeline.CreateTrack<NarrativeBroadcastTrack>(null,"Actual Seed scene lengths / subtitles / four narrative stages");
            for(int i=0;i<library.narrative.Length;i++)
            {
                var line=library.narrative[i];var clip=broadcast.CreateClip<NarrativeBroadcastClip>();
                clip.displayName=line.id+" "+line.channel;clip.start=line.time;clip.duration=line.duration;((NarrativeBroadcastClip)clip.asset).lineIndex=i;
            }
            Undo.RecordObject(narrative,"Assign audio-duration narrative");Undo.RecordObject(narrative.director,"Assign measured timeline");
            narrative.timeline=timeline;narrative.duration=(float)timeline.fixedDuration;narrative.director.playableAsset=timeline;
            foreach(var output in timeline.outputs)narrative.director.SetGenericBinding(output.sourceObject,narrative);
            var font=Font(library);
            if(radio.presenter!=null)foreach(var label in radio.presenter.GetComponentsInChildren<TMP_Text>(true))
            {Undo.RecordObject(label,"Set complete bilingual caption glyphs");label.font=font;EditorUtility.SetDirty(label);}
            foreach(var obj in new UnityEngine.Object[]{library,radio,narrative,narrative.director,timeline,world})EditorUtility.SetDirty(obj);
            AssetDatabase.SaveAssets();
        }
        static AudioClip[] Variants(string name)=>Enumerable.Range(1,3).Select(i=>Import(name+"_"+i)).ToArray();
        static AudioMixerGroup Group(AudioMixer mixer,string name)=>mixer.FindMatchingGroups(name).First(x=>x.name==name);
        static void Assign(AudioSource source,AudioMixerGroup group)
        {
            if(source==null)return;Undo.RecordObject(source,"Route whole cabin scene");source.outputAudioMixerGroup=group;source.playOnAwake=false;source.spatialBlend=0;source.loop=false;EditorUtility.SetDirty(source);
        }
        static AudioClip Import(string name)
        {
            string path=AudioRoot+"/"+name+".wav";
            if(!File.Exists(path))throw new FileNotFoundException("Generate and mix actual local clip before integration",path);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=AssetImporter.GetAtPath(path) as AudioImporter;
            if(importer!=null)
            {
                var settings=importer.defaultSampleSettings;
                if(settings.compressionFormat!=AudioCompressionFormat.PCM || settings.loadType!=AudioClipLoadType.DecompressOnLoad || !importer.forceToMono)
                {settings.compressionFormat=AudioCompressionFormat.PCM;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;importer.forceToMono=true;importer.defaultSampleSettings=settings;importer.SaveAndReimport();}
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static TMP_FontAsset Font(RadioLibrary library)
        {
            string path=DataRoot+"/CinematicChineseSDF.asset";var existing=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);if(existing!=null)return existing;
            var font=TMP_FontAsset.CreateFontAsset("Microsoft YaHei","Regular",48);if(font==null)throw new InvalidOperationException("Installed Chinese font missing.");
            var text=new StringBuilder("截获无线电监听等待信号待机中断短促杂音正在接收音量历史广播跳过剧情重播最近十条记录暂停舰通信频道反应堆警报破损快离开%：〔〕[]()·.,!?。！？…\n");
            for(int i=32;i<127;i++)text.Append((char)i);
            foreach(var l in library.narrative){text.Append(l.text).Append(l.channel);foreach(var c in l.captions)text.Append(c.text);}
            foreach(var l in library.combat){text.Append(l.text).Append(l.channel);foreach(var c in l.captions)text.Append(c.text);}
            if(!font.TryAddCharacters(text.ToString(),out string missing)&&!string.IsNullOrWhiteSpace(missing))throw new InvalidOperationException("Missing caption characters: "+missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static;var atlases=font.atlasTextures;var material=font.material;
            AssetDatabase.CreateAsset(font,path);
            foreach(var atlas in atlases)if(atlas!=null&&!AssetDatabase.Contains(atlas))AssetDatabase.AddObjectToAsset(atlas,font);
            if(!AssetDatabase.Contains(material))AssetDatabase.AddObjectToAsset(material,font);
            font.atlasTextures=atlases;font.material=material;EditorUtility.SetDirty(font);return font;
        }
    }
}
