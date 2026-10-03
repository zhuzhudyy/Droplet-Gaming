using System;
using System.IO;
using System.Security.Cryptography;
using DropletPrototype.Editor;
using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.EditMode
{
    public sealed class SeedAudioAuthoringTests
    {
        [Serializable] sealed class Request { public string model; }
        [Serializable] sealed class Job
        {
            public string status, sha256, audio, requestId, provider, resource_id;
            public Request request;
        }

        [Serializable] sealed class Publication
        {
            public bool accepted;
            public string sha256, model, sourceJob, sourceSha256;
            public string[] sourceJobs, sourceHashes;
        }

        static string Hash(string path)
        {
            using (var input = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        [Test]
        public void LongStereoBedsStreamWhileShortEventsStayResident()
        {
            Assert.IsTrue(SeedAudioAuthoring.ShouldStreamCategory("music"));
            Assert.IsTrue(SeedAudioAuthoring.ShouldStreamCategory("ambience"));
            Assert.IsFalse(SeedAudioAuthoring.ShouldStreamCategory("combat"));
            Assert.IsFalse(SeedAudioAuthoring.ShouldStreamCategory("ui"));
            Assert.IsFalse(SeedAudioAuthoring.ShouldStreamCategory("flight"));
        }

        [Test]
        public void PublicationRequiresAcceptanceAndBothUnchangedAudioHashes()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(project, "Temp", "SeedAudioAuthoringTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string original = Path.Combine(directory, "audio.wav");
                string output = Path.Combine(directory, "published.wav");
                string jobPath = Path.Combine(directory, "job.json");
                string sidecar = Path.Combine(directory, "published.json");
                File.WriteAllBytes(original, new byte[] { 1, 3, 5, 7 });
                File.WriteAllBytes(output, new byte[] { 2, 4, 6, 8 });
                File.WriteAllText(jobPath, JsonUtility.ToJson(new Job
                {
                    status = "complete", sha256 = Hash(original), audio = original,
                    requestId = "test-request", request = new Request { model = "seed-audio-1.0" }
                }));
                var publication = new Publication
                {
                    accepted = false, sha256 = Hash(output), model = "seed-audio-1.0",
                    sourceJob = jobPath, sourceSha256 = Hash(original)
                };
                File.WriteAllText(sidecar, JsonUtility.ToJson(publication));
                Assert.Throws<InvalidOperationException>(() => SeedAudioAuthoring.ValidatePublishedCue(output, sidecar));

                publication.accepted = true;
                File.WriteAllText(sidecar, JsonUtility.ToJson(publication));
                Assert.DoesNotThrow(() => SeedAudioAuthoring.ValidatePublishedCue(output, sidecar));

                File.WriteAllBytes(output, new byte[] { 9, 9, 9, 9 });
                Assert.Throws<InvalidOperationException>(() => SeedAudioAuthoring.ValidatePublishedCue(output, sidecar));
                File.WriteAllBytes(output, new byte[] { 2, 4, 6, 8 });

                File.WriteAllBytes(original, new byte[] { 0, 0, 0, 0 });
                Assert.Throws<InvalidOperationException>(() => SeedAudioAuthoring.ValidatePublishedCue(output, sidecar));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void RadioPublicationRejectsChangedRecipeAndOriginalRecording()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string directory = Path.Combine(project, "Temp", "SeedAudioAuthoringTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string original = Path.Combine(directory, "voice.wav");
                string jobPath = Path.Combine(directory, "job.json");
                string recipe = Path.Combine(directory, "mix_recipe.json");
                string output = Path.Combine(directory, "published.wav");
                string sidecar = Path.Combine(directory, "published.json");
                File.WriteAllBytes(original, new byte[] { 1, 4, 9, 16 });
                File.WriteAllBytes(output, new byte[] { 3, 5, 7, 11 });
                File.WriteAllText(jobPath, JsonUtility.ToJson(new Job
                {
                    status = "complete", sha256 = Hash(original), requestId = "seed-voice-test",
                    provider = "seed-tts", resource_id = "seed-tts-2.0"
                }));
                File.WriteAllText(recipe, "{\"source\":\"Seed\"}");
                var publication = new Publication
                {
                    accepted = true, sha256 = Hash(output),
                    model = "seed-tts-2.0+seed-audio-1.0",
                    sourceJob = recipe, sourceSha256 = Hash(recipe),
                    sourceJobs = new[] { jobPath }, sourceHashes = new[] { Hash(original) }
                };
                File.WriteAllText(sidecar, JsonUtility.ToJson(publication));
                Assert.DoesNotThrow(() => SeedAudioAuthoring.ValidatePublishedRadio(output, sidecar));

                File.WriteAllText(recipe, "{\"source\":\"changed\"}");
                Assert.Throws<InvalidOperationException>(() => SeedAudioAuthoring.ValidatePublishedRadio(output, sidecar));
                File.WriteAllText(recipe, "{\"source\":\"Seed\"}");

                File.WriteAllBytes(original, new byte[] { 0, 0, 0, 0 });
                Assert.Throws<InvalidOperationException>(() => SeedAudioAuthoring.ValidatePublishedRadio(output, sidecar));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
