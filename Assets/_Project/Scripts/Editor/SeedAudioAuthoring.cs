using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Object = UnityEngine.Object;

namespace DropletPrototype.Editor
{
    /// <summary>Imports accepted Seed recordings and creates a separate, saved 2000-ship scene.</summary>
    public static class SeedAudioAuthoring
    {
        public const string SourceScene = CinematicAudioIntegration.ScenePath;
        public const string ScenePath = "Assets/_Project/Scenes/FleetAssault_SeedAudio.unity";
        public const string SourceRoot = "ArtSource/Audio/SeedAudio20260929";
        public const string AudioRoot = "Assets/_Project/Audio/SeedAudio";
        public const string RadioRoot = AudioRoot + "/Radio";
        public const string DataRoot = "Assets/_Project/Data/SeedAudio";
        public const string CatalogPath = DataRoot + "/SeedAudioCatalog.asset";
        public const string MixerPath = DataRoot + "/SeedAudio.mixer";
        public const string RadioLibraryPath = DataRoot + "/SeedRadio.asset";
        public const string TimelinePath = DataRoot + "/SeedNarrative.playable";
        public const string FontPath = DataRoot + "/SeedChineseSDF.asset";
        public const string PlayerPath = "Builds/Windows-SeedAudio-20260929/DropletGaming.exe";

        [Serializable] sealed class CatalogFile { public CueFile[] cues; }
        [Serializable] sealed class CueFile
        {
            public string id, family, category;
            public bool loop;
            public int priority;
            public float cooldownSeconds;
        }
        [Serializable] sealed class Publication
        {
            public bool accepted;
            public string sha256, model, sourceJob, sourceSha256, sourceRequestId;
            public string[] sourceJobs, sourceHashes;
            public float durationSeconds;
            public string english, text;
            public RadioCaption[] captions;
        }
        [Serializable] sealed class SourceRequest { public string model; }
        [Serializable] sealed class SourceJob
        {
            public string status, sha256, audio, requestId, provider, resource_id;
            public SourceRequest request;
        }
        [Serializable] sealed class RevisionFile { public RevisionLine[] lines; }
        [Serializable] sealed class RevisionLine
        {
            public string id, english, text, role, channel;
            public int stage, cameraShot;
        }
        [Serializable] sealed class BuildRecord
        { public string scene, player, result; public int errors, warnings; public ulong bytes; public double seconds; }

        static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
        static string Disk(string projectRelative) => Path.GetFullPath(Path.Combine(ProjectRoot, projectRelative));

        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Stop Play, compilation, and asset refresh before Seed audio authoring.");
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Close Prefab Mode before Seed audio authoring so staged prefab edits stay intact.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty) throw new InvalidOperationException("Unsaved scene protected: " + scene.path);
            }
        }

        static CueFile[] ReadCatalog()
        {
            string path = Disk(SourceRoot + "/catalog.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Missing authored Seed cue catalog", path);
            var data = JsonUtility.FromJson<CatalogFile>(File.ReadAllText(path));
            if (data == null || data.cues == null || data.cues.Length != 131)
                throw new InvalidOperationException("The Seed catalog must contain exactly 131 cues.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var cue in data.cues)
                if (cue == null || string.IsNullOrWhiteSpace(cue.id) || !ids.Add(cue.id) ||
                    !SafeId(cue.id) || string.IsNullOrWhiteSpace(cue.family) || string.IsNullOrWhiteSpace(cue.category))
                    throw new InvalidOperationException("Invalid or duplicate Seed cue ID: " + cue?.id);
            var categories = new Dictionary<string, int>
            {
                { "music", 19 }, { "ambience", 6 }, { "flight", 18 }, { "combat", 35 },
                { "cabin", 22 }, { "ui", 16 }, { "mission", 15 }
            };
            foreach (var pair in categories)
                if (data.cues.Count(c => c.category == pair.Key) != pair.Value)
                    throw new InvalidOperationException("Seed cue category count differs: " + pair.Key);
            foreach (var pair in new Dictionary<string, int>
            {
                { "Laser", 6 }, { "Reflect", 6 }, { "Penetration", 6 }, { "LaserBreach", 3 },
                { "Explosion", 6 }, { "Reactor", 3 }, { "RetreatEngine", 3 }, { "Escape", 2 }
            })
                if (data.cues.Count(c => c.family == pair.Key && c.category == "combat") != pair.Value)
                    throw new InvalidOperationException("Seed combat family count differs: " + pair.Key);
            return data.cues;
        }

        static bool SafeId(string id) => id.All(c => char.IsLetterOrDigit(c) || c == '_');

        // Both score and sustained ambience are 60-90 second stereo beds. Keep
        // them streaming instead of loading hundreds of MB of decompressed PCM.
        public static bool ShouldStreamCategory(string category) =>
            category == "music" || category == "ambience";

        static string Sha256(string path)
        {
            using (var input = File.OpenRead(path))
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        static string SourcePath(string relativeOrAbsolute)
        {
            if (string.IsNullOrWhiteSpace(relativeOrAbsolute)) throw new InvalidOperationException("Publication is missing a source path.");
            string full = Path.GetFullPath(Path.IsPathRooted(relativeOrAbsolute) ? relativeOrAbsolute : Disk(relativeOrAbsolute));
            string prefix = ProjectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Publication source lies outside the project: " + relativeOrAbsolute);
            return full;
        }

        /// <summary>Checks acceptance, resulting WAV hash, and a completed original Seed job.</summary>
        public static void ValidatePublishedCue(string wavPath, string sidecarPath)
        {
            var meta = ReadPublication(wavPath, sidecarPath);
            if (meta.model != "seed-audio-1.0") throw new InvalidOperationException("Non-Seed-Audio source: " + wavPath);
            string jobPath = SourcePath(meta.sourceJob);
            if (!File.Exists(jobPath)) throw new FileNotFoundException("Missing original Seed job", jobPath);
            var job = JsonUtility.FromJson<SourceJob>(File.ReadAllText(jobPath));
            if (job == null || job.status != "complete" || job.request == null ||
                job.request.model != "seed-audio-1.0" || string.IsNullOrWhiteSpace(job.sha256) ||
                !string.Equals(job.sha256, meta.sourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Original Seed job is incomplete or its hash differs: " + jobPath);
            // Some older job JSON encoded this machine's Unicode absolute path badly.
            // The original WAV is authoritative beside the completed job record.
            string original = Path.Combine(Path.GetDirectoryName(jobPath), "audio.wav");
            if (!File.Exists(original) || !string.Equals(Sha256(original), meta.sourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Original Seed WAV hash differs: " + original);
        }

        static Publication ReadPublication(string wavPath, string sidecarPath)
        {
            if (!File.Exists(wavPath) || !File.Exists(sidecarPath))
                throw new FileNotFoundException("Published WAV and acceptance sidecar must both exist", wavPath);
            var meta = JsonUtility.FromJson<Publication>(File.ReadAllText(sidecarPath));
            if (meta == null || !meta.accepted || string.IsNullOrWhiteSpace(meta.sha256) ||
                !string.Equals(Sha256(wavPath), meta.sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Publication is unaccepted or WAV hash differs: " + wavPath);
            return meta;
        }

        /// <summary>Checks accepted radio output against its recipe and every original Seed job.</summary>
        public static void ValidatePublishedRadio(string wavPath, string sidecarPath)
        {
            var meta = ReadPublication(wavPath, sidecarPath);
            if (meta.model != "seed-tts-2.0+seed-audio-1.0" ||
                string.IsNullOrWhiteSpace(meta.sourceJob) || string.IsNullOrWhiteSpace(meta.sourceSha256) ||
                meta.sourceJobs == null || meta.sourceJobs.Length == 0 ||
                meta.sourceHashes == null || meta.sourceHashes.Length != meta.sourceJobs.Length)
                throw new InvalidOperationException("Radio publication lacks complete Seed provenance: " + wavPath);
            string recipe = SourcePath(meta.sourceJob);
            if (!File.Exists(recipe) || !string.Equals(Sha256(recipe), meta.sourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Radio recipe hash differs: " + recipe);
            for (int i = 0; i < meta.sourceJobs.Length; i++)
            {
                string path = SourcePath(meta.sourceJobs[i]);
                if (!File.Exists(path) || string.IsNullOrWhiteSpace(meta.sourceHashes[i]))
                    throw new InvalidOperationException("Radio Seed source is missing: " + path);
                var job = JsonUtility.FromJson<SourceJob>(File.ReadAllText(path));
                if (job == null || job.status != "complete" ||
                    !string.Equals(job.sha256, meta.sourceHashes[i], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Radio Seed job is incomplete or its hash differs: " + path);
                string directory = Path.GetDirectoryName(path);
                string original = Path.Combine(directory, job.provider == "seed-tts" ? "voice.wav" : "audio.wav");
                bool audioModel = Path.GetFileName(original) == "audio.wav" &&
                    job.request != null && job.request.model == "seed-audio-1.0";
                bool voiceModel = Path.GetFileName(original) == "voice.wav" &&
                    job.provider == "seed-tts" && job.resource_id == "seed-tts-2.0";
                if (!audioModel && !voiceModel)
                    throw new InvalidOperationException("Radio source is not an enabled Seed model: " + path);
                if (!File.Exists(original) ||
                    !string.Equals(Sha256(original), meta.sourceHashes[i], StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Radio original Seed recording hash differs: " + original);
            }
        }

        static string CuePublished(string id) => Disk(SourceRoot + "/published/" + id + ".wav");
        static string RadioPublished(string id) => Disk(SourceRoot + "/published-radio/" + id + ".wav");
        static string Sidecar(string wav) => Path.ChangeExtension(wav, ".json");

        static void VerifyImported(string source, string destination)
        {
            if (!File.Exists(Disk(destination)) || !string.Equals(Sha256(source), Sha256(Disk(destination)), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Imported file missing or differs from accepted publication: " + destination);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
            if (clip == null || clip.length <= .01f || clip.channels < 1 || clip.frequency < 8000)
                throw new InvalidOperationException("Unity did not import an AudioClip: " + destination);
        }

        static void ImportWav(string source, string destination, bool streamedBed, bool worldMono)
        {
            string target = Disk(destination);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target) || !string.Equals(Sha256(source), Sha256(target), StringComparison.OrdinalIgnoreCase))
                File.Copy(source, target, true); // Existing .meta stays intact, so reimports preserve the GUID.
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(destination) as AudioImporter;
            if (importer == null) throw new InvalidOperationException("No Unity AudioImporter for " + destination);
            var sample = importer.defaultSampleSettings;
            var desiredLoad = streamedBed ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            var desiredCodec = streamedBed ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            bool changed = sample.loadType != desiredLoad || sample.compressionFormat != desiredCodec ||
                sample.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate || importer.forceToMono != worldMono;
            if (changed)
            {
                sample.loadType = desiredLoad;
                sample.compressionFormat = desiredCodec;
                sample.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                if (streamedBed) sample.quality = .85f;
                importer.forceToMono = worldMono;
                importer.defaultSampleSettings = sample;
                importer.SaveAndReimport();
            }
            VerifyImported(source, destination);
        }

        [MenuItem("DropletPrototype/Seed Audio/1 Import Accepted WAVs")]
        public static void ImportAccepted()
        {
            Guard();
            var cues = ReadCatalog();
            var ready = new List<CueFile>();
            foreach (var cue in cues)
            {
                string wav = CuePublished(cue.id), sidecar = Sidecar(wav);
                if (!File.Exists(wav) && !File.Exists(sidecar)) continue;
                ValidatePublishedCue(wav, sidecar);
                ready.Add(cue);
            }
            string radioDirectory = Disk(SourceRoot + "/published-radio");
            var radio = Directory.Exists(radioDirectory) ? Directory.GetFiles(radioDirectory, "*.wav") : Array.Empty<string>();
            var knownRadio = ExpectedRadioIds(AssetDatabase.LoadAssetAtPath<RadioLibrary>(CinematicAudioAssets.LibraryPath));
            foreach (var wav in radio)
            {
                string id = Path.GetFileNameWithoutExtension(wav);
                if (!SafeId(id) || !knownRadio.Contains(id))
                    throw new InvalidOperationException("Unrecognized radio publication: " + wav);
                ValidatePublishedRadio(wav, Sidecar(wav));
            }
            if (ready.Count > 0 || radio.Length > 0)
            {
                Directory.CreateDirectory(Disk(AudioRoot));
                Directory.CreateDirectory(Disk(RadioRoot));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            foreach (var cue in ready)
                ImportWav(CuePublished(cue.id), AudioRoot + "/" + cue.id + ".wav",
                    ShouldStreamCategory(cue.category), cue.category == "combat");
            foreach (var wav in radio)
                ImportWav(wav, RadioRoot + "/" + Path.GetFileName(wav), false, false);
            Debug.Log("Seed Audio: imported " + ready.Count + "/131 accepted cues and " + radio.Length + " accepted radio mixes. No scene was changed.");
        }

        static AudioClip Cue(string id)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioRoot + "/" + id + ".wav");
            if (clip == null) throw new InvalidOperationException("Missing imported Seed cue: " + id);
            return clip;
        }

        static AudioClip RadioClip(string id)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(RadioRoot + "/" + id + ".wav");
            if (clip == null) throw new InvalidOperationException("Missing imported Seed radio mix: " + id);
            return clip;
        }

        static HashSet<string> ExpectedRadioIds(RadioLibrary source)
        {
            if (source == null || source.narrative == null || source.combat == null)
                throw new InvalidOperationException("Saved cinematic radio library is unavailable.");
            var radioIds = new HashSet<string>(StringComparer.Ordinal) { "HELP" };
            foreach (var line in source.narrative) radioIds.Add(line.id);
            foreach (var line in source.combat)
            {
                radioIds.Add(line.id);
                radioIds.Add(line.id + "_low");
                radioIds.Add(line.id + "_high");
            }
            if (radioIds.Count != 158) throw new InvalidOperationException("Expected 80 radio mixes plus 78 battle variants; found " + radioIds.Count);
            return radioIds;
        }

        static void CheckComplete(CueFile[] cues, RadioLibrary source,
            Dictionary<string, RevisionLine> revision)
        {
            foreach (var cue in cues)
            {
                string wav = CuePublished(cue.id);
                ValidatePublishedCue(wav, Sidecar(wav));
                VerifyImported(wav, AudioRoot + "/" + cue.id + ".wav");
            }
            var radioIds = ExpectedRadioIds(source);
            foreach (var id in radioIds)
            {
                string wav = RadioPublished(id);
                ValidatePublishedRadio(wav, Sidecar(wav));
                VerifyImported(wav, RadioRoot + "/" + id + ".wav");
            }
            foreach (var line in source.narrative)
            {
                if (!revision.TryGetValue(line.id, out var revised))
                    throw new InvalidOperationException("Missing revised narrative line: " + line.id);
                Captions(line.id, RadioClip(line.id), revised);
            }
            foreach (var line in source.combat)
            {
                if (!revision.TryGetValue(line.id, out var revised))
                    throw new InvalidOperationException("Missing revised combat line: " + line.id);
                Captions(line.id, RadioClip(line.id), revised);
            }
        }

        static T CopyAsset<T>(string source, string destination) where T : Object
        {
            if (AssetDatabase.LoadAssetAtPath<T>(destination) == null && !AssetDatabase.CopyAsset(source, destination))
                throw new IOException("Could not copy saved asset: " + source);
            return AssetDatabase.LoadAssetAtPath<T>(destination);
        }

        static AudioMixerGroup Group(AudioMixer mixer, string name)
        {
            var match = mixer.FindMatchingGroups(name).FirstOrDefault(g => g.name == name);
            if (match == null) throw new InvalidOperationException("Copied mixer lacks working group: " + name);
            return match;
        }

        static Dictionary<string, RevisionLine> ReadRevision()
        {
            string path = Disk(SourceRoot + "/narrative-revision.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Missing revised English and Chinese story", path);
            var revision = JsonUtility.FromJson<RevisionFile>(File.ReadAllText(path));
            if (revision == null || revision.lines == null || revision.lines.Length != 87)
                throw new InvalidOperationException("Expected 87 revised dialogue entries.");
            var lines = new Dictionary<string, RevisionLine>(StringComparer.Ordinal);
            foreach (var line in revision.lines)
                if (line == null || string.IsNullOrWhiteSpace(line.id) || !lines.TryAdd(line.id, line) ||
                    string.IsNullOrWhiteSpace(line.english) || string.IsNullOrWhiteSpace(line.text))
                    throw new InvalidOperationException("Invalid revised dialogue entry: " + line?.id);
            return lines;
        }

        static RadioCaption[] Captions(string id, AudioClip clip, RevisionLine revision)
        {
            var publication = JsonUtility.FromJson<Publication>(File.ReadAllText(Sidecar(RadioPublished(id))));
            if (publication == null || publication.captions == null || publication.captions.Length == 0 ||
                publication.english != revision.english || publication.text != revision.text ||
                Mathf.Abs(publication.durationSeconds - clip.length) > .08f)
                throw new InvalidOperationException("Revised speech/subtitle publication does not match measured audio: " + id);
            foreach (var caption in publication.captions)
                if (caption == null || string.IsNullOrWhiteSpace(caption.text) ||
                    caption.time < 0 || caption.duration <= 0 || caption.time + caption.duration > clip.length + .08f)
                    throw new InvalidOperationException("Subtitle lies outside accepted audio: " + id);
            return publication.captions;
        }

        static TMP_FontAsset ConfigureChineseFont(RadioLibrary library)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                font = CopyAsset<TMP_FontAsset>(CinematicAudioAssets.DataRoot + "/CinematicChineseSDF.asset", FontPath);
            Undo.RecordObject(font, "Add revised Chinese subtitle glyphs to Seed copy");
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            var characters = new StringBuilder("截获无线电监听等待信号待机中断短促杂音正在接收音量历史广播跳过剧情重播最近十条记录暂停舰通信频道反应堆警报破损快离开%：〔〕[]()·.,!?。！？…\n");
            foreach (var line in library.narrative)
            {
                characters.Append(line.text).Append(line.channel);
                foreach (var caption in line.captions) characters.Append(caption.text);
            }
            foreach (var line in library.combat)
            {
                characters.Append(line.text).Append(line.channel);
                foreach (var caption in line.captions) characters.Append(caption.text);
            }
            if (!font.TryAddCharacters(characters.ToString(), out string missing) && !string.IsNullOrWhiteSpace(missing))
                throw new InvalidOperationException("Seed subtitle font is missing Chinese glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            return font;
        }

        static void Route(AudioSource source, AudioMixerGroup group)
        {
            if (source == null) return;
            Undo.RecordObject(source, "Route Seed radio source");
            // A saved clip on a reusable source would keep an obsolete recording in
            // the new scene's build dependency graph even if radio replaces it at Play.
            source.clip = null;
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = group;
            EditorUtility.SetDirty(source);
            if (PrefabUtility.IsPartOfPrefabInstance(source)) PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        }

        static SeedAudioCatalog ConfigureCatalog(CueFile[] cues)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<SeedAudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<SeedAudioCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            Undo.RecordObject(catalog, "Configure accepted Seed clips");
            catalog.cues = cues.Select(row =>
            {
                var pub = JsonUtility.FromJson<Publication>(File.ReadAllText(Sidecar(CuePublished(row.id))));
                var job = JsonUtility.FromJson<SourceJob>(File.ReadAllText(SourcePath(pub.sourceJob)));
                return new SeedAudioCue
                {
                    id = row.id, family = row.family, category = row.category,
                    clip = Cue(row.id), loop = row.loop, gain = 1f,
                    priority = row.priority, cooldownSeconds = row.cooldownSeconds,
                    sourceRequestId = string.IsNullOrWhiteSpace(pub.sourceRequestId) ? job.requestId : pub.sourceRequestId,
                    sourceHash = pub.sourceSha256
                };
            }).ToArray();
            if (catalog.MissingClipCount() != 0) throw new InvalidOperationException("Incomplete Seed catalog asset.");
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static RadioLibrary ConfigureRadio(MissionController mission, AudioMixer mixer,
            Dictionary<string, RevisionLine> revision)
        {
            var library = AssetDatabase.LoadAssetAtPath<RadioLibrary>(RadioLibraryPath);
            Undo.RecordObject(library, "Replace all radio premixes with accepted Seed audio");
            float cursor = 0f;
            foreach (var line in library.narrative)
            {
                if (!revision.TryGetValue(line.id, out var revised))
                    throw new InvalidOperationException("Missing revised narrative text: " + line.id);
                line.voice = RadioClip(line.id);
                line.english = revised.english;
                line.text = revised.text;
                line.channel = revised.channel;
                line.role = revised.role;
                line.stage = revised.stage;
                line.cameraShot = revised.cameraShot;
                line.captions = Captions(line.id, line.voice, revised);
                line.time = cursor;
                line.duration = line.voice.length;
                line.premixed = true;
                cursor += line.duration + .16f;
            }
            foreach (var line in library.combat)
            {
                if (!revision.TryGetValue(line.id, out var revised))
                    throw new InvalidOperationException("Missing revised combat text: " + line.id);
                line.voice = RadioClip(line.id);
                line.lowIntensityVoice = RadioClip(line.id + "_low");
                line.highIntensityVoice = RadioClip(line.id + "_high");
                if (line.shortVoice != null) line.shortVoice = RadioClip("HELP");
                line.english = revised.english;
                line.text = revised.text;
                line.channel = revised.channel;
                line.role = revised.role;
                line.captions = Captions(line.id, line.voice, revised);
                line.premixed = true;
            }
            library.connectTone = Cue("Connect_01");
            library.interruptTone = Cue("Disconnect_01");
            library.alarm = null;
            library.equipmentBed = null;
            library.premixedSceneAudio = true;
            library.voiceProvenance = "AI synthesized English / Seed-TTS 2.0 and Seed Audio 1.0";
            EditorUtility.SetDirty(library);
            var radio = mission.narrative.radio;
            Undo.RecordObject(radio, "Bind new Seed radio library and mixer");
            radio.library = library;
            radio.normalMix = mixer.FindSnapshot("Normal");
            radio.broadcastMix = mixer.FindSnapshot("Broadcast");
            if (radio.normalMix == null || radio.broadcastMix == null)
                throw new InvalidOperationException("Copied radio mixer lacks Normal/Broadcast snapshots.");
            radio.showVoiceProvenance = true;
            Route(radio.voiceSource, Group(mixer, "Voice"));
            Route(radio.signalSource, Group(mixer, "Signal"));
            Route(radio.alarmSource, Group(mixer, "Alarm"));
            Route(radio.bedSource, Group(mixer, "Background"));
            foreach (var source in new[] { radio.voiceSource, radio.signalSource, radio.alarmSource, radio.bedSource })
            {
                if (source == null) continue;
                Undo.RecordObject(source, "Clear superseded radio clip");
                source.clip = null;
                source.playOnAwake = false;
                EditorUtility.SetDirty(source);
            }
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            Undo.RecordObject(timeline, "Rebuild Seed story timing from real audio lengths");
            foreach (var track in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(track);
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = cursor + .25f;
            var broadcast = timeline.CreateTrack<NarrativeBroadcastTrack>(null, "Measured Seed dialogue / four story stages");
            for (int i = 0; i < library.narrative.Length; i++)
            {
                var line = library.narrative[i];
                var clip = broadcast.CreateClip<NarrativeBroadcastClip>();
                clip.displayName = line.id + " " + line.channel;
                clip.start = line.time;
                clip.duration = line.duration;
                ((NarrativeBroadcastClip)clip.asset).lineIndex = i;
            }
            Undo.RecordObject(mission.narrative, "Bind copied Seed narrative Timeline");
            Undo.RecordObject(mission.narrative.director, "Bind copied Seed narrative Timeline");
            mission.narrative.timeline = timeline;
            mission.narrative.duration = (float)timeline.fixedDuration;
            mission.narrative.director.playableAsset = timeline;
            foreach (var output in timeline.outputs)
                mission.narrative.director.SetGenericBinding(output.sourceObject, mission.narrative);
            var font = ConfigureChineseFont(library);
            if (radio.presenter != null)
                foreach (var label in radio.presenter.GetComponentsInChildren<TMP_Text>(true))
                {
                    Undo.RecordObject(label, "Use revised Seed Chinese subtitle font");
                    label.font = font;
                    EditorUtility.SetDirty(label);
                    if (PrefabUtility.IsPartOfPrefabInstance(label)) PrefabUtility.RecordPrefabInstancePropertyModifications(label);
                }
            EditorUtility.SetDirty(radio);
            EditorUtility.SetDirty(mission.narrative);
            EditorUtility.SetDirty(mission.narrative.director);
            EditorUtility.SetDirty(timeline);
            return library;
        }

        static AudioClip[] Family(SeedAudioCatalog catalog, string family) =>
            catalog.cues.Where(c => c.family == family).Select(c => c.clip).ToArray();

        [MenuItem("DropletPrototype/Seed Audio/2 Integrate Safe Scene Copy")]
        public static void Integrate()
        {
            Guard();
            var cues = ReadCatalog();
            var revision = ReadRevision();
            var existingRadio = AssetDatabase.LoadAssetAtPath<RadioLibrary>(CinematicAudioAssets.LibraryPath);
            if (existingRadio == null || existingRadio.narrative == null || existingRadio.combat == null)
                throw new InvalidOperationException("Saved cinematic radio library is unavailable.");
            CheckComplete(cues, existingRadio, revision); // No scene or asset mutation before all publications pass.
            if (!File.Exists(Disk(SourceScene))) throw new FileNotFoundException("Missing accepted 2000-ship source scene", SourceScene);
            Directory.CreateDirectory(Disk(DataRoot));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var mixer = CopyAsset<AudioMixer>(CinematicAudioAssets.MixerPath, MixerPath);
            CopyAsset<RadioLibrary>(CinematicAudioAssets.LibraryPath, RadioLibraryPath);
            CopyAsset<TimelineAsset>(CinematicAudioAssets.TimelinePath, TimelinePath);
            var catalog = ConfigureCatalog(cues);
            if (!File.Exists(Disk(ScenePath)) && !AssetDatabase.CopyAsset(SourceScene, ScenePath))
                throw new IOException("Could not copy accepted cinematic scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath);
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Integrate accepted Seed audio safe copy");
            var mission = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MissionController>(true)).SingleOrDefault();
            if (mission == null || mission.targets == null || mission.targets.Length != 2000 ||
                mission.narrative == null || mission.narrative.radio == null || mission.combat == null || mission.motor == null)
                throw new InvalidOperationException("Safe copy lacks the complete 2000-ship cinematic scene.");
            var ids = mission.targets.Select(t => t.targetId).ToArray();
            if (ids.Distinct().Count() != 2000) throw new InvalidOperationException("Fleet identities are not unique.");
            var owner = mission.transform.Find("SeedAudioPresentation");
            if (owner == null)
            {
                var go = new GameObject("SeedAudioPresentation");
                Undo.RegisterCreatedObjectUndo(go, "Create owned Seed audio subtree");
                Undo.SetTransformParent(go.transform, mission.transform, "Parent owned Seed audio subtree");
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                owner = go.transform;
            }
            var mix = owner.GetComponent<SeedAudioMixController>() ?? Undo.AddComponent<SeedAudioMixController>(owner.gameObject);
            Undo.RecordObject(mix, "Bind unified Seed audio mix");
            mix.mission = mission;
            mix.radio = mission.narrative.radio;
            Undo.RecordObject(mission.narrative.radio, "Bind Seed communications gain");
            mission.narrative.radio.seedMix = mix;
            var stage = owner.GetComponent<StageAudioDirector>() ?? Undo.AddComponent<StageAudioDirector>(owner.gameObject);
            Undo.RecordObject(stage, "Bind stage Seed audio");
            stage.mission = mission;
            stage.narrative = mission.narrative;
            stage.battle = mission.narrative.radio.GetComponent<BattleAudioDirector>();
            stage.catalog = catalog;
            stage.mix = mix;
            // New local buses play to Master; the one MixController owns their user gain and ducking.
            stage.musicGroup = null;
            stage.ambienceGroup = null;
            var flight = owner.GetComponent<FlightAudioController>() ?? Undo.AddComponent<FlightAudioController>(owner.gameObject);
            Undo.RecordObject(flight, "Bind Seed flight audio");
            flight.mission = mission;
            flight.motor = mission.motor;
            flight.catalog = catalog;
            flight.mix = mix;
            flight.flightGroup = null;
            var ui = owner.GetComponent<UiMissionAudioController>() ?? Undo.AddComponent<UiMissionAudioController>(owner.gameObject);
            Undo.RecordObject(ui, "Bind Seed UI and mission audio");
            ui.mission = mission;
            ui.score = mission.score;
            ui.narrative = mission.narrative;
            ui.catalog = catalog;
            ui.mix = mix;
            ui.uiGroup = null;
            ui.missionGroup = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var hud in root.GetComponentsInChildren<HudPresenter>(true))
                {
                    Undo.RecordObject(hud, "Bind Seed UI feedback");
                    hud.audioFeedback = ui;
                    hud.audioMix = mix;
                    EditorUtility.SetDirty(hud);
                    if (PrefabUtility.IsPartOfPrefabInstance(hud)) PrefabUtility.RecordPrefabInstancePropertyModifications(hud);
                }
            if (mission.narrative.radio.presenter != null)
            {
                var presenter = mission.narrative.radio.presenter;
                Undo.RecordObject(presenter, "Bind Seed radio controls feedback");
                presenter.audioFeedback = ui;
                EditorUtility.SetDirty(presenter);
                if (PrefabUtility.IsPartOfPrefabInstance(presenter)) PrefabUtility.RecordPrefabInstancePropertyModifications(presenter);
            }
            var world = stage.battle;
            if (world == null) throw new InvalidOperationException("Safe copy lacks the existing bounded BattleAudioDirector.");
            Undo.RecordObject(world, "Bind all accepted Seed combat clips");
            world.poolCapacity = 12;
            world.mix = mix;
            // The new mix controller owns combat ducking. Do not also route through
            // the old Combat snapshot, which would apply the duck a second time.
            world.worldGroup = null;
            world.laserClips = Family(catalog, "Laser");
            world.reflectClips = Family(catalog, "Reflect");
            world.impactClips = Family(catalog, "Penetration");
            world.explosionClips = Family(catalog, "Explosion");
            world.laserBreachClips = Family(catalog, "LaserBreach");
            world.reactorClips = Family(catalog, "Reactor");
            world.retreatEngineClips = Family(catalog, "RetreatEngine");
            world.escapeClips = Family(catalog, "Escape");
            ConfigureRadio(mission, mixer, revision);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var effects in root.GetComponentsInChildren<MissionEffects>(true))
                {
                    Undo.RecordObject(effects, "Remove superseded procedural effect clips");
                    effects.flightClip = null;
                    effects.impactClips = Array.Empty<AudioClip>();
                    effects.worldAudioEnabled = false;
                    EditorUtility.SetDirty(effects);
                    if (PrefabUtility.IsPartOfPrefabInstance(effects)) PrefabUtility.RecordPrefabInstancePropertyModifications(effects);
                }
                foreach (var beam in root.GetComponentsInChildren<LaserBeamPool>(true))
                {
                    Undo.RecordObject(beam, "Use Seed laser source only");
                    beam.shotAudio = Cue("Laser_01"); // Prevents the legacy runtime generator from creating a clip.
                    beam.worldAudioEnabled = false;
                    EditorUtility.SetDirty(beam);
                    if (PrefabUtility.IsPartOfPrefabInstance(beam)) PrefabUtility.RecordPrefabInstancePropertyModifications(beam);
                }
            }
            if (!ids.SequenceEqual(mission.targets.Select(t => t.targetId)) ||
                scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<AudioListener>(true).Count(a => a.enabled)) != 1)
                throw new InvalidOperationException("Fleet identities or unique listener changed during integration.");
            foreach (var component in new Component[] { mix, stage, flight, ui, world, mission.narrative.radio, mission.narrative })
            {
                EditorUtility.SetDirty(component);
                if (PrefabUtility.IsPartOfPrefabInstance(component)) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save Seed scene copy.");
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undo);
            Debug.Log("Seed Audio: saved separate 2000-ship scene " + ScenePath + " with 131 accepted cues and 158 accepted radio mixes.");
        }

        [MenuItem("DropletPrototype/Seed Audio/4 Set Default Build Scene")]
        public static void SetDefaultBuildScene()
        {
            Guard();
            if (!File.Exists(Disk(ScenePath)) || AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new FileNotFoundException("Integrate the saved Seed scene first", ScenePath);
            var scenes = EditorBuildSettings.scenes
                .Where(entry => entry.path != ScenePath)
                .Select(entry => new EditorBuildSettingsScene(entry.path, false))
                .ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            if (EditorBuildSettings.scenes.Length == 0 ||
                EditorBuildSettings.scenes[0].path != ScenePath || !EditorBuildSettings.scenes[0].enabled)
                throw new InvalidOperationException("Unity did not retain the Seed scene as the default build entry.");
            Debug.Log("Seed Audio: default Build Settings scene is " + ScenePath + "; previous entries remain disabled for rollback.");
        }

        [MenuItem("DropletPrototype/Seed Audio/3 Build Windows")]
        public static void Build()
        {
            Guard();
            if (!File.Exists(Disk(ScenePath))) throw new FileNotFoundException("Integrate the accepted Seed scene first", ScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(Disk(PlayerPath)));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = Disk(PlayerPath),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            var summary = report.summary;
            var record = new BuildRecord
            {
                scene = ScenePath, player = PlayerPath, result = summary.result.ToString(),
                errors = summary.totalErrors, warnings = summary.totalWarnings,
                bytes = summary.totalSize, seconds = summary.totalTime.TotalSeconds
            };
            string evidence = Disk("docs/verification/SeedAudio-20260929/build.json");
            Directory.CreateDirectory(Path.GetDirectoryName(evidence));
            File.WriteAllText(evidence, JsonUtility.ToJson(record, true));
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Seed Audio player build failed: " + summary.result);
        }
    }
}
