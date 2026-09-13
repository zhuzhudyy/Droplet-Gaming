using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace DropletPrototype.Editor
{
    /// <summary>Called only by the explicit new-scene builder. All writes live in the owned NarrativeCombat tree.</summary>
    public static class NarrativeRadioAssets
    {
        public const string DataRoot = "Assets/_Project/Data/NarrativeCombat";
        public const string AudioRoot = "Assets/_Project/Audio/NarrativeCombat";
        public const string LibraryPath = DataRoot + "/InterceptedRadio.asset";
        public const string TimelinePath = DataRoot + "/FleetApproach.playable";
        public const string FontPath = DataRoot + "/ChineseRadioSDF.asset";

        public static NarrativeApproachController Configure(Transform owner, MissionController mission, FleetCombatSimulation fleet)
        {
            if (owner == null || mission == null || fleet == null) throw new ArgumentNullException("Narrative radio requires owner, mission and fleet.");
            var library = BuildLibrary();
            var timeline = BuildTimeline(library);
            var font = BuildFont(library);
            var child = owner.Find("NarrativeRadio");
            GameObject root;
            if (child != null) root = child.gameObject;
            else { root = new GameObject("NarrativeRadio"); Undo.RegisterCreatedObjectUndo(root, "Create narrative radio"); root.transform.SetParent(owner, false); }
            var radio = GetOrAdd<RadioController>(root);
            radio.library = library; radio.mission = mission; radio.fleet = fleet; radio.listener = mission.motor.transform;
            radio.viewCamera = mission.chaseCamera != null ? mission.chaseCamera.GetComponent<Camera>() : Camera.main;
            var sources = root.GetComponents<AudioSource>();
            radio.voiceSource = sources.Length > 0 ? sources[0] : Undo.AddComponent<AudioSource>(root);
            radio.signalSource = sources.Length > 1 ? sources[1] : Undo.AddComponent<AudioSource>(root);
            foreach (var source in root.GetComponents<AudioSource>()) { source.playOnAwake = false; source.spatialBlend = 0; source.volume = .7f; }
            var narrative = GetOrAdd<NarrativeApproachController>(root);
            narrative.mission = mission; narrative.motor = mission.motor; narrative.chaseCamera = mission.chaseCamera; narrative.radio = radio;
            narrative.director = GetOrAdd<PlayableDirector>(root); narrative.director.playOnAwake = false;
            narrative.director.timeUpdateMode = DirectorUpdateMode.Manual; narrative.director.extrapolationMode = DirectorWrapMode.Hold;
            narrative.timeline = timeline; narrative.director.playableAsset = timeline; narrative.duration = 60;
            foreach (var output in timeline.outputs) narrative.director.SetGenericBinding(output.sourceObject, narrative);
            mission.narrative = narrative; mission.replayNarrativeOnStart = true;
            if (root.transform.Find("InterceptCanvas") == null) BuildUI(root.transform, radio, font);
            RefreshSavedUILayout(root.transform.Find("InterceptCanvas"), radio, font);
            EditorUtility.SetDirty(mission); EditorUtility.SetDirty(radio); EditorUtility.SetDirty(narrative);
            AssetDatabase.SaveAssets();
            return narrative;
        }
        static T GetOrAdd<T>(GameObject root) where T : Component
        { var value = root.GetComponent<T>(); return value != null ? value : Undo.AddComponent<T>(root); }
        public static RadioLibrary BuildLibrary()
        {
            Directory.CreateDirectory(DataRoot);
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(DataRoot + "/RadioContent.json");
            if (source == null) throw new InvalidOperationException("Import RadioContent.json before authoring narrative assets.");
            var content = JsonUtility.FromJson<RadioContent>(source.text);
            var library = AssetDatabase.LoadAssetAtPath<RadioLibrary>(LibraryPath);
            if (library == null) { library = ScriptableObject.CreateInstance<RadioLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
            Undo.RecordObject(library, "Configure intercepted radio library");
            library.combat = content.combat; library.narrative = content.narrative;
            foreach (var line in library.combat) line.voice = ImportVoice(line.id);
            foreach (var line in library.narrative) line.voice = ImportVoice(line.id);
            library.connectTone = ImportVoice("ConnectTone"); library.interruptTone = ImportVoice("InterruptTone");
            EditorUtility.SetDirty(library);
            return library;
        }
        static AudioClip ImportVoice(string id)
        {
            string path = AudioRoot + "/" + id + ".wav";
            if (!File.Exists(path)) throw new FileNotFoundException("Run Tools/NarrativeCombat/ExportRadioVoices.ps1 before scene generation.", path);
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(path) == null) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer != null)
            {
                var sample = importer.defaultSampleSettings;
                bool needsImport = sample.loadType != AudioClipLoadType.DecompressOnLoad || sample.compressionFormat != AudioCompressionFormat.Vorbis || !Mathf.Approximately(sample.quality, .55f) || !importer.forceToMono;
                if (needsImport)
                {
                    sample.loadType = AudioClipLoadType.DecompressOnLoad; sample.compressionFormat = AudioCompressionFormat.Vorbis; sample.quality = .55f;
                    importer.defaultSampleSettings = sample; importer.forceToMono = true; importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        public static TimelineAsset BuildTimeline(RadioLibrary library)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline != null) return timeline;
            timeline = ScriptableObject.CreateInstance<TimelineAsset>(); timeline.name = "Fleet Approach / 60 seconds";
            AssetDatabase.CreateAsset(timeline, TimelinePath);
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 60;
            var track = timeline.CreateTrack<NarrativeBroadcastTrack>(null, "Intercept / Camera / Autopilot cues");
            for (int i = 0; i < library.narrative.Length; i++)
            {
                var line = library.narrative[i]; var clip = track.CreateClip<NarrativeBroadcastClip>();
                clip.displayName = line.id + " " + line.channel; clip.start = line.time; clip.duration = line.duration;
                ((NarrativeBroadcastClip)clip.asset).lineIndex = i;
            }
            EditorUtility.SetDirty(timeline); AssetDatabase.SaveAssets(); return timeline;
        }
        public static TMP_FontAsset BuildFont(RadioLibrary library)
        {
            EnsureTextMeshProResources();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (FontIsComplete(font)) return font;
            // CreateAsset may reload a font immediately, dropping references to non-persistent children.
            // Build in a separate temporary object, persist every child first, then copy into the existing
            // asset. This repairs an interrupted generation while preserving its GUID and file ID.
            var generated = TMP_FontAsset.CreateFontAsset("Microsoft YaHei", "Regular", 48);
            if (generated == null) generated = TMP_FontAsset.CreateFontAsset("Microsoft YaHei UI", "Regular", 48);
            if (generated == null) throw new InvalidOperationException("An installed Microsoft YaHei font is required to bake the Chinese subtitle atlas.");
            generated.name = "ChineseRadioSDF";
            var text = new StringBuilder("截获无线电监听等待信号待机中断短促杂音正在接收量历史广播跳过剧情重最近十条记录无线音暂停零一二三四五六七八九十百千号舰 0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-+/%：〔〕[]()·.,!?。！？…\n");
            for (int code = 32; code < 127; code++) text.Append((char)code);
            foreach (var line in library.combat) text.Append(line.channel).Append(line.text);
            foreach (var line in library.narrative) text.Append(line.channel).Append(line.text);
            if (!generated.TryAddCharacters(text.ToString(), out string missing) && !string.IsNullOrWhiteSpace(missing))
                throw new InvalidOperationException("Chinese radio atlas missing glyphs: " + missing);
            generated.atlasPopulationMode = AtlasPopulationMode.Static;
            var atlases = generated.atlasTextures;
            var material = generated.material;
            if (material == null)
            {
                var shader = Shader.Find("TextMeshPro/Mobile/Distance Field");
                if (shader == null) throw new InvalidOperationException("The installed TMP SDF shader has not finished importing.");
                material = new Material(shader);
            }
            material.name = "ChineseRadioSDF Material";
            material.SetTexture("_MainTex", atlases[0]);
            material.SetFloat("_TextureWidth", generated.atlasWidth); material.SetFloat("_TextureHeight", generated.atlasHeight);
            material.SetFloat("_GradientScale", generated.atlasPadding + 1);
            material.SetFloat("_WeightNormal", generated.normalStyle); material.SetFloat("_WeightBold", generated.boldStyle);
            if (font == null)
            {
                font = ScriptableObject.CreateInstance<TMP_FontAsset>(); font.name = "ChineseRadioSDF";
                AssetDatabase.CreateAsset(font, FontPath);
            }
            if (!AssetDatabase.Contains(material)) AssetDatabase.AddObjectToAsset(material, font);
            for (int i = 0; i < atlases.Length; i++)
            {
                if (atlases[i] == null) continue;
                atlases[i].name = "ChineseRadioAtlas " + i;
                if (!AssetDatabase.Contains(atlases[i])) AssetDatabase.AddObjectToAsset(atlases[i], font);
            }
            EditorUtility.CopySerialized(generated, font);
            font.material = material; font.atlasTextures = atlases;
            material.SetTexture("_MainTex", atlases[0]);
            EditorUtility.SetDirty(font); EditorUtility.SetDirty(material);
            foreach (var atlas in atlases) if (atlas != null) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            UnityEngine.Object.DestroyImmediate(generated);
            AssetDatabase.ImportAsset(FontPath, ImportAssetOptions.ForceSynchronousImport);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (!FontIsComplete(font)) throw new InvalidOperationException("Chinese font did not retain its material/atlas subassets after save and reload.");
            return font;
        }
        static bool FontIsComplete(TMP_FontAsset font)
        {
            if (font == null || font.material == null || font.material.mainTexture == null || font.characterTable == null || font.characterTable.Count < 300) return false;
            var atlases = font.atlasTextures;
            if (atlases == null || atlases.Length == 0) return false;
            for (int i = 0; i < font.atlasTextureCount; i++) if (i >= atlases.Length || atlases[i] == null) return false;
            return true;
        }
        static void EnsureTextMeshProResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null && Shader.Find("TextMeshPro/Mobile/Distance Field") != null) return;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            if (package == null) throw new InvalidOperationException("Unable to resolve the installed ugui/TextMeshPro package.");
            string essential = Path.Combine(package.resolvedPath, "Package Resources/TMP Essential Resources.unitypackage");
            const string importedRoot = "Assets/TextMesh Pro";
            bool existed = AssetDatabase.IsValidFolder(importedRoot);
            AssetDatabase.ImportPackage(essential, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            // The package contains default Settings, shaders/includes, line-breaking rules and fallback fonts.
            // Move only a newly imported tree; never move or overwrite pre-existing authored TMP resources.
            string ownedRoot = DataRoot + "/TextMeshProResources";
            if (!existed && AssetDatabase.IsValidFolder(importedRoot) && !AssetDatabase.IsValidFolder(ownedRoot))
            {
                string error = AssetDatabase.MoveAsset(importedRoot, ownedRoot);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
            if (TMP_Settings.LoadDefaultSettings() == null) throw new InvalidOperationException("Imported TMP Settings could not be loaded from Resources.");
        }
        static void BuildUI(Transform parent, RadioController radio, TMP_FontAsset font)
        {
            var root = new GameObject("InterceptCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(root, "Create intercepted radio UI"); root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 8;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var presenter = Undo.AddComponent<RadioPresenter>(root); presenter.radio = radio; radio.presenter = presenter;
            var panel = Panel(root.transform, "RadioPanel", new Vector2(0, 0), new Vector2(28, 68), new Vector2(640, 204));
            presenter.radioPanel = panel;
            presenter.sourceLabel = Text(panel, "Source", font, new Vector2(18, -12), new Vector2(605, 30), 20, new Color(.42f, .82f, .91f));
            presenter.subtitleLabel = Text(panel, "Caption", font, new Vector2(18, -47), new Vector2(605, 74), 25, Color.white);
            presenter.signalLabel = Text(panel, "Signal", font, new Vector2(18, -124), new Vector2(605, 32), 17, new Color(.73f, .83f, .86f));
            presenter.controlsLabel = Text(panel, "Controls", font, new Vector2(18, -164), new Vector2(605, 28), 16, new Color(.56f, .69f, .74f));
            var history = Panel(root.transform, "HistoryPanel", new Vector2(0, 0), new Vector2(28, 288), new Vector2(640, 644));
            presenter.historyLabel = Text(history, "History", font, new Vector2(18, -16), new Vector2(603, 610), 18, new Color(.78f, .89f, .93f));
            presenter.historyPanel = history.gameObject; history.gameObject.SetActive(false);
        }
        static void RefreshSavedUILayout(Transform canvas, RadioController radio, TMP_FontAsset font)
        {
            var presenter = canvas.GetComponent<RadioPresenter>();
            var panel = canvas.Find("RadioPanel") as RectTransform;
            if (presenter == null || panel == null) throw new InvalidOperationException("Saved intercepted radio UI is missing its presenter or panel.");
            Undo.RecordObject(presenter, "Update radio panel layout"); Undo.RecordObject(panel, "Update radio panel layout");
            presenter.radio = radio; radio.presenter = presenter; presenter.radioPanel = panel;
            panel.sizeDelta = new Vector2(640, 204);
            panel.anchorMin = panel.anchorMax = panel.pivot = Vector2.zero; panel.anchoredPosition = new Vector2(28, 68);
            presenter.signalLabel = panel.Find("Signal").GetComponent<TMP_Text>();
            presenter.controlsLabel = panel.Find("Controls").GetComponent<TMP_Text>();
            SetSavedTextLayout(presenter.signalLabel, font, new Vector2(18, -124), new Vector2(605, 32));
            SetSavedTextLayout(presenter.controlsLabel, font, new Vector2(18, -164), new Vector2(605, 28));
            if (presenter.historyPanel != null)
            {
                var history = presenter.historyPanel.transform as RectTransform; Undo.RecordObject(history, "Update radio history position");
                history.anchorMin = history.anchorMax = history.pivot = Vector2.zero; history.anchoredPosition = new Vector2(28, 288);
            }
            EditorUtility.SetDirty(presenter); EditorUtility.SetDirty(panel);
        }
        static void SetSavedTextLayout(TMP_Text label, TMP_FontAsset font, Vector2 position, Vector2 size)
        {
            Undo.RecordObject(label, "Update radio label layout"); Undo.RecordObject(label.rectTransform, "Update radio label layout");
            label.font = font; label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = size;
            EditorUtility.SetDirty(label); EditorUtility.SetDirty(label.rectTransform);
        }
        static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = Vector2.zero; rect.anchoredPosition = position; rect.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(.014f, .031f, .045f, .88f); go.GetComponent<Image>().raycastTarget = false;
            return rect;
        }
        static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)); go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = font; text.fontSize = fontSize; text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Ellipsis; text.raycastTarget = false;
            text.text = name == "Caption" ? "等待截获信号" : name == "Source" ? "截获无线电" : "";
            return text;
        }
    }
}
