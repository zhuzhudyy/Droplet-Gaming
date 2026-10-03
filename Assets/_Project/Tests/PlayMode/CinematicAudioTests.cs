using NUnit.Framework;
using UnityEngine;

namespace DropletPrototype.Tests.PlayMode
{
    public sealed class CinematicAudioTests
    {
        GameObject root;
        BattleAudioDirector audio;
        AudioClip clip;
        [SetUp] public void Setup()
        {
            root=new GameObject("Cinematic audio contract tests");
            audio=root.AddComponent<BattleAudioDirector>();audio.enabled=false;
            clip=AudioClip.Create("Pool duration test only",24000,1,24000,false);
            audio.laserClips=new[]{clip};audio.reflectClips=new[]{clip};audio.impactClips=new[]{clip};audio.explosionClips=new[]{clip};
        }
        [TearDown] public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(clip);}
        CombatEvent Event(long id,CombatEventKind kind,ShipTarget ship=null) => new CombatEvent(kind,ship,ship,Vector3.zero,DamageSource.Penetration,0,0,id,"source",ship!=null?ship.targetId:"target",Vector3.forward,Vector3.up,Vector3.zero);
        [Test] public void RecentCombatIntensityRisesThenFullyDecaysWithoutLifetimeKillCount()
        {
            Assert.AreEqual(0,audio.Intensity);
            long id=1;
            for(int i=0;i<80;i++){audio.Receive(Event(id++,CombatEventKind.HullPenetrated));audio.Tick(.1f);}
            Assert.Greater(audio.Intensity,.7f);
            for(int i=0;i<220;i++)audio.Tick(.1f);
            Assert.Less(audio.Intensity,.005f);
            Assert.AreEqual(0,audio.ActiveWorldSources);
        }
        [Test] public void ImmediateContactDeduplicatesAndWorldAudioRemainsBounded()
        {
            var message=Event(42,CombatEventKind.DropletContact);audio.Receive(message);audio.Receive(message);
            Assert.AreEqual(1,audio.PlayedWorldEvents,"One physical contact must not be replayed by a camera change or repeated delivery.");
            for(int i=0;i<2000;i++)audio.Receive(Event(100+i,CombatEventKind.ShipExploded));
            Assert.LessOrEqual(audio.ActiveWorldSources,audio.poolCapacity);
            Assert.LessOrEqual(audio.MaxWorldSourcesObserved,audio.poolCapacity);
            audio.ResetAudio();Assert.AreEqual(0,audio.ActiveWorldSources);Assert.AreEqual(0,audio.Intensity);Assert.AreEqual(0,audio.ReceivedEvents);
        }
        [Test] public void SpeakerExplosionStopsWholeCabinAndAllAuxiliaryTracks()
        {
            var ship=new GameObject("speaker").AddComponent<ShipTarget>();ship.transform.SetParent(root.transform);ship.targetId="SHIP-1";
            var radio=root.AddComponent<RadioController>();radio.enabled=false;
            radio.alarmSource=root.AddComponent<AudioSource>();radio.bedSource=root.AddComponent<AudioSource>();
            var library=ScriptableObject.CreateInstance<RadioLibrary>();library.premixedSceneAudio=true;
            library.combat=new[]{new RadioLine{id="SC01",eventKind="AttackIneffective",text="Command",voice=clip,premixed=true}};
            radio.library=library;radio.BeginSession(1);radio.Receive(Event(51,CombatEventKind.AttackIneffective,ship));radio.Tick(.01f);
            Assert.AreEqual("SC01",radio.CurrentLineId);Assert.IsTrue(radio.CurrentScenePremixed);
            radio.alarmSource.clip=clip;radio.bedSource.clip=clip;radio.alarmSource.Play();radio.bedSource.Play();
            radio.Receive(Event(52,CombatEventKind.CommunicationInterrupted,ship));
            Assert.IsNull(radio.CurrentSpeaker);Assert.IsFalse(radio.voiceSource.isPlaying);Assert.IsFalse(radio.alarmSource.isPlaying);Assert.IsFalse(radio.bedSource.isPlaying);
            Assert.AreEqual(0,radio.QueuedCount);Object.DestroyImmediate(library);
        }
        [Test] public void ReactorRetreatEscapeAndLaserBreachUseTheirEventAudioWithoutExtraSources()
        {
            audio.reactorClips = new[] { clip };
            audio.retreatEngineClips = new[] { clip };
            audio.escapeClips = new[] { clip };
            audio.laserBreachClips = new[] { clip };
            foreach (var kind in new[] { CombatEventKind.ReactorUnstable,
                CombatEventKind.RetreatOrdered, CombatEventKind.ShipEscaped })
            {
                audio.ResetAudio();
                audio.Receive(Event((long)kind + 100, kind));
                Assert.AreEqual(1, audio.PlayedWorldEvents, kind.ToString());
                Assert.LessOrEqual(audio.ActiveWorldSources, 12);
            }
            audio.ResetAudio();
            audio.impactClips = System.Array.Empty<AudioClip>();
            audio.Receive(new CombatEvent(CombatEventKind.HullPenetrated, null, null,
                Vector3.zero, DamageSource.ReflectedLaser, 0, 0, 999,
                "attacker", "target", Vector3.forward, Vector3.up, Vector3.zero));
            Assert.AreEqual(1, audio.PlayedWorldEvents,
                "A reflected laser breach must use its Seed cue even when droplet impacts are absent.");
            Assert.LessOrEqual(audio.MaxWorldSourcesObserved, 12);
        }

        [Test] public void SustainedReactorAndRetreatFollowShipsAndStopOnTheirOwnResolution()
        {
            var reactorShip=new GameObject("reactor ship").AddComponent<ShipTarget>();
            reactorShip.transform.SetParent(root.transform);reactorShip.targetId="REACTOR-1";
            var retreatShip=new GameObject("retreat ship").AddComponent<ShipTarget>();
            retreatShip.transform.SetParent(root.transform);retreatShip.targetId="RETREAT-2";
            audio.reactorClips=new[]{clip};audio.retreatEngineClips=new[]{clip};
            audio.escapeClips=new[]{clip};

            audio.Receive(Event(1001,CombatEventKind.ReactorUnstable,reactorShip));
            audio.Tick(.2f); // The lower-priority retreat start has its normal short receiver budget.
            audio.Receive(Event(1002,CombatEventKind.RetreatOrdered,retreatShip));
            Assert.AreEqual(2,audio.ActiveWorldSources);
            Assert.AreEqual(2,SustainedSources());
            audio.Tick(clip.length*2f);
            Assert.AreEqual(2,SustainedSources(),
                "Both existing world slots must loop until a real resolution event.");
            reactorShip.transform.position=new Vector3(40,0,0);
            audio.Tick(.1f);
            Assert.IsTrue(System.Array.Exists(root.GetComponentsInChildren<AudioSource>(),
                source=>source.loop && Vector3.Distance(source.transform.position,reactorShip.transform.position)<.01f),
                "A sustained reactor bed follows its actual ship while the world origin changes.");

            audio.Receive(Event(1003,CombatEventKind.ShipExploded,reactorShip));
            Assert.AreEqual(1,SustainedSources(),"Only this ship's sustained reactor source stops.");
            audio.Tick(.2f);
            audio.Receive(Event(1004,CombatEventKind.ShipEscaped,retreatShip));
            Assert.AreEqual(0,SustainedSources(),"Escape stops the matching retreat loop.");
            Assert.LessOrEqual(audio.ActiveWorldSources,12);
            audio.ResetAudio();
            Assert.AreEqual(0,audio.ActiveWorldSources);
            Assert.AreEqual(0,SustainedSources(),"A reused world source must clear its loop flag.");
        }

        [Test] public void ReactorAndRetreatMayCoexistForOneShipButRepeatedKindDoesNotDoubleLoop()
        {
            var ship=new GameObject("shared sustained ship").AddComponent<ShipTarget>();
            ship.transform.SetParent(root.transform);ship.targetId="SHARED-1";
            audio.reactorClips=new[]{clip};audio.retreatEngineClips=new[]{clip};
            audio.Receive(Event(2001,CombatEventKind.ReactorUnstable,ship));
            audio.Receive(Event(2002,CombatEventKind.RetreatOrdered,ship));
            Assert.AreEqual(2,SustainedSources(),
                "Adjacent real states of the same ship retain separate Seed layers despite receiver cooldown.");
            audio.Receive(Event(2003,CombatEventKind.RetreatOrdered,ship));
            Assert.AreEqual(2,SustainedSources(),
                "Repeating the same event kind cannot consume a second persistent slot.");
            audio.Receive(Event(2004,CombatEventKind.ShipExploded,ship));
            Assert.AreEqual(0,SustainedSources(),"Resolution stops every sustained layer of that ship.");
        }

        int SustainedSources()
        {
            int count=0;
            foreach(var source in root.GetComponentsInChildren<AudioSource>())
                if(source.loop && source.clip!=null)count++;
            return count;
        }
    }
}
