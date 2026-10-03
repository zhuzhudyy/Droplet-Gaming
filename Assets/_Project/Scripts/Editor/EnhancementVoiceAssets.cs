using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Timeline;

namespace DropletPrototype.Editor
{
    /// <summary>Root-only explicit authoring, confined to new Enhancement assets and the provided scene.</summary>
    public static class EnhancementVoiceAssets
    {
        public const string DataRoot = "Assets/_Project/Data/Enhancement";
        public const string AudioRoot = "Assets/_Project/Audio/EnhancementEnglish";
        public const string MixerPath = DataRoot + "/EnhancementAudio.mixer";
        const string LibraryPath = DataRoot + "/EnhancementRadio.asset";
        const string TimelinePath = DataRoot + "/TheOpenCorridor.playable";
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        /// <returns>False retains the legacy, matched Chinese audio/captions/Timeline because cloud English generation is blocked.</returns>
        public static bool Configure(MissionController mission)
        {
            if (Application.isPlaying || mission == null || mission.narrative == null || mission.narrative.radio == null)
                throw new InvalidOperationException("Configure the saved approach in the new scene before enhancing voice assets.");
            var narrative = mission.narrative; var radio = narrative.radio;
            Directory.CreateDirectory(DataRoot);
            var mixer = BuildMixer();
            var library = AssetDatabase.LoadAssetAtPath<RadioLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<RadioLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            Undo.RecordObject(library, "Configure new enhancement radio");
            // Copy the baseline library, never edit its accepted audio or text. This is also
            // the explicit fallback if the real English bank is incomplete or inaccessible.
            EditorUtility.CopySerialized(radio.library, library);
            library.name = "Enhancement Radio";
            bool english = TryBuildEnglish(library, narrative);
            library.connectTone = Import("ConnectTone"); library.interruptTone = Import("InterruptTone");
            library.alarm = Import("Alarm"); library.equipmentBed = Import("EquipmentBed");
            Undo.RecordObject(radio, "Wire radio mixer groups");
            radio.library = library;
            radio.showVoiceProvenance = true;
            radio.normalMix = mixer.FindSnapshot("Normal"); radio.broadcastMix = mixer.FindSnapshot("Broadcast");
            radio.alarmSource = ChildSource(radio.transform, "Alarm");
            radio.bedSource = ChildSource(radio.transform, "EquipmentBed");
            Assign(radio.voiceSource, Group(mixer, "Voice"), 1);
            Assign(radio.signalSource, Group(mixer, "Signal"), .3f);
            Assign(radio.alarmSource, Group(mixer, "Alarm"), .38f);
            Assign(radio.bedSource, Group(mixer, "Background"), .18f);
            foreach (var root in mission.gameObject.scene.GetRootGameObjects())
            {
                foreach (var effects in root.GetComponentsInChildren<MissionEffects>(true))
                { Undo.RecordObject(effects, "Route combat effects"); effects.outputMixerGroup = Group(mixer, "Combat"); EditorUtility.SetDirty(effects); }
                foreach (var beams in root.GetComponentsInChildren<LaserBeamPool>(true))
                { Undo.RecordObject(beams, "Route laser audio"); beams.outputMixerGroup = Group(mixer, "Combat"); EditorUtility.SetDirty(beams); }
            }
            EditorUtility.SetDirty(library); EditorUtility.SetDirty(radio); EditorUtility.SetDirty(narrative);
            AssetDatabase.SaveAssets();
            return english;
        }

        static bool TryBuildEnglish(RadioLibrary library, NarrativeApproachController narrative)
        {
            string path = DataRoot + "/EnhancementEnglish.json";
            if (!File.Exists(path)) return false;
            var content = JsonUtility.FromJson<RadioContent>(File.ReadAllText(path));
            if (content?.narrative == null || content.narrative.Length < 4 || content.combat == null || content.combat.Length < 30) return false;
            foreach (var line in content.narrative) if (!File.Exists(AudioRoot + "/" + line.id + ".wav")) return false;
            foreach (var line in content.combat) if (!File.Exists(AudioRoot + "/" + line.id + ".wav")) return false;
            float cursor = 0;
            foreach (var line in content.narrative)
            {
                line.voice = Import(line.id); line.time = cursor; line.duration = line.voice.length;
                cursor += line.duration + .16f;
            }
            cursor += .25f;
            if (cursor < 240 || cursor > 360) throw new InvalidOperationException("The real English opening is outside 4-6 minutes. No artificial padding is allowed.");
            foreach (var line in content.combat) line.voice = Import(line.id);
            library.narrative = content.narrative; library.combat = content.combat;
            library.voiceProvenance = "AI synthesized English / Seed-TTS 2.0 / Original fictional dialogue";
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, TimelinePath); }
            Undo.RecordObject(timeline, "Build audio-duration approach Timeline");
            foreach (var track in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(track);
            timeline.name = "The Open Corridor / AI English";
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = cursor;
            var broadcast = timeline.CreateTrack<NarrativeBroadcastTrack>(null, "Four stages / local voice and subtitles");
            for (int i = 0; i < library.narrative.Length; i++)
            {
                var line = library.narrative[i]; var clip = broadcast.CreateClip<NarrativeBroadcastClip>();
                clip.displayName = line.id + " " + line.channel; clip.start = line.time; clip.duration = line.duration;
                ((NarrativeBroadcastClip)clip.asset).lineIndex = i;
            }
            Undo.RecordObject(narrative, "Assign real-duration English approach");
            Undo.RecordObject(narrative.director, "Assign real-duration English Timeline");
            narrative.timeline = timeline; narrative.duration = cursor; narrative.director.playableAsset = timeline;
            foreach (var output in timeline.outputs) narrative.director.SetGenericBinding(output.sourceObject, narrative);
            var font = BuildCaptionFont(library);
            if (narrative.radio.presenter != null)
                foreach (var text in narrative.radio.presenter.GetComponentsInChildren<TMP_Text>(true))
                { Undo.RecordObject(text, "Assign expanded caption font"); text.font = font; EditorUtility.SetDirty(text); }
            EditorUtility.SetDirty(timeline); EditorUtility.SetDirty(narrative.director);
            return true;
        }

        static TMP_FontAsset BuildCaptionFont(RadioLibrary library)
        {
            const string path = DataRoot + "/EnhancementChineseSDF.asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return font;
            font = TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular", 48);
            if (font == null) throw new InvalidOperationException("Installed Chinese font unavailable.");
            var characters = new StringBuilder("截获无线电监听等待信号待机中断短促杂音正在接收音量历史广播跳过剧情重播最近十条记录暂停舰通信频道反应堆警报%：〔〕[]()·.,!?。！？…\n");
            for (int i = 32; i < 127; i++) characters.Append((char)i);
            foreach (var line in library.narrative) characters.Append(line.text).Append(line.channel);
            foreach (var line in library.combat) characters.Append(line.text).Append(line.channel);
            if (!font.TryAddCharacters(characters.ToString(), out string missing) && !string.IsNullOrWhiteSpace(missing)) throw new InvalidOperationException("New captions contain missing glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            var atlases = font.atlasTextures; var material = font.material;
            AssetDatabase.CreateAsset(font, path);
            foreach (var atlas in atlases) if (atlas != null && !AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
            if (!AssetDatabase.Contains(material)) AssetDatabase.AddObjectToAsset(material, font);
            font.atlasTextures = atlases; font.material = material; EditorUtility.SetDirty(font);
            return font;
        }

        static AudioSource ChildSource(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create separate radio audio channel");
                go.transform.SetParent(parent, false); child = go.transform;
            }
            var source = child.GetComponent<AudioSource>(); return source != null ? source : Undo.AddComponent<AudioSource>(child.gameObject);
        }
        static void Assign(AudioSource source, AudioMixerGroup group, float level)
        {
            if (source == null) throw new InvalidOperationException("A saved radio audio source is missing.");
            Undo.RecordObject(source, "Route radio channel"); source.outputAudioMixerGroup = group;
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = level; EditorUtility.SetDirty(source);
        }
        static AudioClip Import(string id)
        {
            string path = AudioRoot + "/" + id + ".wav";
            if (!File.Exists(path)) throw new FileNotFoundException("Generate local enhancement audio first.", path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path); var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.Vorbis; settings.quality = .7f;
            importer.forceToMono = true; importer.defaultSampleSettings = settings; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static AudioMixerGroup Group(AudioMixer mixer, string name) => mixer.FindMatchingGroups(name).First(g => g.name == name);
        public static AudioMixer BuildMixer()
        {
            Directory.CreateDirectory(DataRoot);
            var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
            if (existing != null && existing.FindSnapshot("Broadcast") != null) return existing;
            // Unity exposes runtime mixer groups but its asset authoring controller is Editor-internal.
            // Resolve the installed Editor API, do not synthesize serialized mixer YAML.
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.Audio.AudioMixerController")).FirstOrDefault(t => t != null);
            if (type == null) throw new InvalidOperationException("Installed Editor AudioMixer controller is unavailable.");
            var mixer = existing ?? (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { MixerPath });
            var master = type.GetProperty("masterGroup", Flags).GetValue(mixer);
            var normal = (AudioMixerSnapshot)type.GetProperty("TargetSnapshot", Flags).GetValue(mixer); normal.name = "Normal";
            string[] names = { "Voice", "Signal", "Alarm", "Background", "Combat" };
            float[] normalDb = { 0, -4, -6, -12, -1 }, broadcastDb = { 0, -4, -8, -18, -4 };
            var groups = new object[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                groups[i] = mixer.FindMatchingGroups(names[i]).FirstOrDefault(g => g.name == names[i]);
                if (groups[i] == null)
                {
                    groups[i] = type.GetMethod("CreateNewGroup", Flags).Invoke(mixer, new object[] { names[i], true });
                    type.GetMethod("AddChildToParent", Flags).Invoke(mixer, new[] { groups[i], master });
                }
                SetVolume(normal, groups[i], normalDb[i]);
            }
            type.GetMethod("CloneNewSnapshotFromTarget", Flags).Invoke(mixer, new object[] { true });
            var broadcast = (AudioMixerSnapshot)type.GetProperty("TargetSnapshot", Flags).GetValue(mixer); broadcast.name = "Broadcast";
            for (int i = 0; i < names.Length; i++) SetVolume(broadcast, groups[i], broadcastDb[i]);
            type.GetProperty("TargetSnapshot", Flags).SetValue(mixer, normal);
            EditorUtility.SetDirty(normal); EditorUtility.SetDirty(broadcast); EditorUtility.SetDirty(mixer);
            AssetDatabase.SaveAssets(); return mixer;
        }
        static void SetVolume(AudioMixerSnapshot snapshot, object group, float decibels)
        {
            var id = group.GetType().GetMethod("GetGUIDForVolume", Flags).Invoke(group, null);
            snapshot.GetType().GetMethod("SetValue", Flags).Invoke(snapshot, new[] { id, (object)decibels });
        }
    }
}
