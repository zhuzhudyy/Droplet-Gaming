using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace DropletPrototype.Tests.PlayMode
{
    // No scene authoring or source writes. Image A/B is separately captured by the
    // actual rendered validation runner; these tests cover the source lifecycle.
    public sealed class VisualUpgradeReflectionTests
    {
        readonly List<Object> owned = new List<Object>();
        MissionController loadedMission;
        GameObject Make(string name)
        {
            var go = new GameObject("Chrome reflection fixture " + name);
            owned.Add(go);
            return go;
        }

        [TearDown]
        public void Cleanup()
        {
            if (loadedMission != null) loadedMission.Restart();
            loadedMission = null;
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
            Time.timeScale = 1;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [UnityTest]
        public IEnumerator ReactorSourcesStayWorldAnchoredAndQualitySuppressionDoesNotWaitForReselection()
        {
            Shader shader = Shader.Find("DropletPrototype/VisualUpgrade/PerfectChrome");
            Assert.IsNotNull(shader);
            Assert.IsTrue(shader.isSupported, "The current URP/GPU must support the chrome shader.");
            var chrome = new Material(shader);
            owned.Add(chrome);
            var player = Make("player");
            player.SetActive(false);
            var renderer = player.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = chrome;
            var response = player.AddComponent<DropletReflectionResponse>();
            response.targetRenderer = renderer;
            response.reactorSelectionInterval = 10;
            var sources = new Transform[5];
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i] = Make("reactor " + i).transform;
                sources[i].position = new Vector3(0, 0, 10 + i * 5);
                sources[i].gameObject.AddComponent<MeshRenderer>();
            }
            response.reactorSources = sources;
            player.SetActive(true);
            yield return null;
            yield return null;
            Assert.AreEqual(3, response.ActiveReactorSources);
            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            var before = properties.GetVectorArray("_ReactorPositions");
            Assert.AreEqual(new Vector4(0, 0, 10, response.reactorSourceStrength), before[0]);
            // Rotate and translate the observer, then move a selected source. The
            // shader receives world positions, never observer-relative decals.
            player.transform.SetPositionAndRotation(new Vector3(3, 2, 0), Quaternion.Euler(30, 120, 0));
            sources[0].position += new Vector3(2, 0, 0);
            yield return null;
            yield return null;
            renderer.GetPropertyBlock(properties);
            var after = properties.GetVectorArray("_ReactorPositions");
            Assert.AreEqual(new Vector4(2, 0, 10, response.reactorSourceStrength), after[0]);
            Assert.AreEqual(before[1], after[1]);

            foreach (Transform source in sources) source.GetComponent<Renderer>().forceRenderingOff = true;
            yield return null;
            yield return null;
            Assert.AreEqual(0, response.ActiveReactorSources, "Off must not leave blue chrome highlights for the ten-second selection interval.");
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(0, properties.GetFloat("_ReactorCount"));
            Assert.IsTrue(properties.GetVectorArray("_ReactorPositions").All(v => v == Vector4.zero));
            foreach (Transform source in sources) source.GetComponent<Renderer>().forceRenderingOff = false;
            response.ResetResponse();
            yield return null;
            yield return null;
            Assert.AreEqual(3, response.ActiveReactorSources);
            sources[0].gameObject.SetActive(false);
            yield return null;
            yield return null;
            Assert.AreEqual(2, response.ActiveReactorSources, "Destroyed inactive visual sources must disappear immediately.");
            response.enabled = false;
            renderer.GetPropertyBlock(properties);
            Assert.AreEqual(0, properties.GetFloat("_ReactorCount"));
            Assert.AreEqual(0, properties.GetFloat("_BurstCount"));
            Assert.AreSame(chrome, renderer.sharedMaterial, "Reflection updates must never create a Material instance.");
        }

        [UnityTest]
        public IEnumerator ActualBurstEnvelopeFreezesWithPauseAndLeavesNoChromeImpulseAfterRestart()
        {
            const string sceneName = "FleetAssault_VisualUpgrade";
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/" + sceneName + ".unity",
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
#endif
            yield return null;
            loadedMission = Object.FindAnyObjectByType<MissionController>();
            Assert.IsNotNull(loadedMission);
            loadedMission.enabled = false;
            loadedMission.motor.enabled = false;
            loadedMission.input.enabled = false;
            var response = Object.FindAnyObjectByType<DropletReflectionResponse>();
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.explosionPool);
            Assert.IsNotNull(response.targetRenderer);
            var effects = Object.FindAnyObjectByType<MissionEffects>();
            effects.Quality = EffectQuality.High;
            var rig = Object.FindAnyObjectByType<SolarLightingRig>();
            if (rig != null) { rig.engineEffects = true; rig.ApplyLighting(); }
            loadedMission.Restart();
            loadedMission.StartMission();
            ShipTarget target = loadedMission.targets.OrderBy(t => (t.transform.position - loadedMission.spawnPosition).sqrMagnitude).First();
            loadedMission.motor.ResetPose(target.transform.position - target.transform.forward * 8, target.transform.rotation);
            var materials = response.targetRenderer.sharedMaterials;
            Assert.IsTrue(target.TryDestroy(new ShipHitContext(target.transform.position, target.transform.forward, 100)));
            yield return new WaitForSeconds(.3f);
            yield return null;
            Assert.Greater(response.ActiveExplosionSources, 0);
            Assert.Greater(response.PeakExplosionStrength, 0);
            loadedMission.TogglePause();
            // Settle one full reflection upload after the pool's final Update.
            yield return null;
            yield return null;
            var properties = new MaterialPropertyBlock();
            response.targetRenderer.GetPropertyBlock(properties);
            Vector4[] positions = properties.GetVectorArray("_BurstPositions");
            Assert.AreEqual(response.ActiveExplosionSources, properties.GetFloat("_BurstCount"));
            Vector3 reactor = target.GetComponent<ReactorDestructionPresenter>().ReactorPosition;
            Assert.Less(Vector3.Distance(reactor, new Vector3(positions[0].x, positions[0].y, positions[0].z)), .001f,
                "The reflected source must be at the authored reactor, not a hit-point or camera offset.");
            float peak = response.PeakExplosionStrength;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.AreEqual(peak, response.PeakExplosionStrength, .00001f);
            response.targetRenderer.GetPropertyBlock(properties);
            CollectionAssert.AreEqual(positions, properties.GetVectorArray("_BurstPositions"));

            response.explosionReflections = false;
            yield return null;
            yield return null;
            Assert.AreEqual(0, response.ActiveExplosionSources);
            Assert.AreEqual(0, response.PeakExplosionStrength);
            response.explosionReflections = true;
            yield return null;
            yield return null;
            Assert.AreEqual(peak, response.PeakExplosionStrength, .00001f, "A/B must restore the same frozen envelope without a new explosion.");
            int score = loadedMission.score.Score;
            effects.Quality = EffectQuality.Off;
            yield return null;
            yield return null;
            Assert.AreEqual(0, response.ActiveExplosionSources);
            Assert.AreEqual(0, response.ActiveReactorSources);
            Assert.AreEqual(score, loadedMission.score.Score);
            Assert.IsTrue(target.IsDestroyed);
            effects.Quality = EffectQuality.High;
            loadedMission.Restart();
            yield return null;
            yield return null;
            Assert.AreEqual(0, response.ActiveExplosionSources);
            Assert.AreEqual(0, response.PeakExplosionStrength);
            response.targetRenderer.GetPropertyBlock(properties);
            Assert.IsTrue(properties.GetVectorArray("_BurstPositions").All(v => v == Vector4.zero));
            CollectionAssert.AreEqual(materials, response.targetRenderer.sharedMaterials);
            Assert.IsFalse(target.IsDestroyed);
            Assert.AreEqual(0, loadedMission.score.Score);
            Assert.AreEqual(1, Time.timeScale);
            loadedMission.StartMission();
            Assert.IsTrue(target.TryDestroy());
            yield return new WaitForSeconds(response.explosionPool.lifetime + .1f);
            yield return null;
            Assert.AreEqual(0, response.ActiveExplosionSources, "A normally expired burst must clear even without restart.");
            Assert.AreEqual(0, response.PeakExplosionStrength);
            response.targetRenderer.GetPropertyBlock(properties);
            Assert.IsTrue(properties.GetVectorArray("_BurstPositions").All(v => v == Vector4.zero));
            CollectionAssert.AreEqual(materials, response.targetRenderer.sharedMaterials);
        }
    }
}
