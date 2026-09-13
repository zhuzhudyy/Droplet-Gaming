using System;
using System.Collections.Generic;
using DropletPrototype.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class NarrativeRadioAssetTests
    {
        [Test] public void OriginalLibraryCoversEventsAndSixtySecondNarrative()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(NarrativeRadioAssets.DataRoot + "/RadioContent.json"); Assert.IsNotNull(source);
            var content = JsonUtility.FromJson<RadioContent>(source.text);
            Assert.GreaterOrEqual(content.combat.Length, 30); Assert.GreaterOrEqual(content.narrative.Length, 12);
            var kinds = new HashSet<string>(); var ids = new HashSet<string>();
            foreach (var line in content.combat)
            {
                Assert.IsTrue(ids.Add(line.id)); Assert.IsTrue(Enum.TryParse<CombatEventKind>(line.eventKind, out _));
                Assert.Greater(line.weight, 0); Assert.IsNotEmpty(line.channel); Assert.Greater(line.text.Length, 8); kinds.Add(line.eventKind);
            }
            foreach (CombatEventKind kind in Enum.GetValues(typeof(CombatEventKind))) Assert.IsTrue(kinds.Contains(kind.ToString()), kind.ToString());
            foreach (var line in content.narrative) { Assert.IsTrue(ids.Add(line.id)); Assert.LessOrEqual(line.time + line.duration, 60); }
            StringAssert.Contains("缓存", content.narrative[0].channel);
        }
        [Test] public void AllFortyEightOfflineChineseVoicesAndBothSignalTonesAreRealImportedClips()
        {
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>(NarrativeRadioAssets.DataRoot + "/RadioContent.json");
            var content = JsonUtility.FromJson<RadioContent>(source.text);
            foreach (var line in content.combat) AssertVoice(line.id, 1);
            foreach (var line in content.narrative) { var clip = AssertVoice(line.id, 1); Assert.LessOrEqual(clip.length, 5, line.id + " must finish before the next cue."); }
            AssertVoice("ConnectTone", .15f); AssertVoice("InterruptTone", .15f);
        }
        [Test] public void SavedChineseFontRetainsItsMaterialAndEveryUsedAtlas()
        {
            var font = AssetDatabase.LoadMainAssetAtPath(NarrativeRadioAssets.FontPath); Assert.IsNotNull(font);
            var serialized = new SerializedObject(font);
            var material = serialized.FindProperty("m_Material").objectReferenceValue as Material;
            Assert.IsNotNull(material); Assert.IsNotNull(material.mainTexture);
            Assert.AreEqual(NarrativeRadioAssets.FontPath, AssetDatabase.GetAssetPath(material));
            var atlases = serialized.FindProperty("m_AtlasTextures"); int last = serialized.FindProperty("m_AtlasTextureIndex").intValue;
            Assert.Greater(atlases.arraySize, last);
            for (int i = 0; i <= last; i++)
            {
                var atlas = atlases.GetArrayElementAtIndex(i).objectReferenceValue as Texture2D;
                Assert.IsNotNull(atlas, "Atlas " + i); Assert.Greater(atlas.width, 1);
                Assert.AreEqual(NarrativeRadioAssets.FontPath, AssetDatabase.GetAssetPath(atlas));
            }
        }
        static AudioClip AssertVoice(string id, float minimumLength)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(NarrativeRadioAssets.AudioRoot + "/" + id + ".wav");
            Assert.IsNotNull(clip, id); Assert.Greater(clip.length, minimumLength, id); Assert.Greater(clip.samples, 1000, id); return clip;
        }
    }
}
