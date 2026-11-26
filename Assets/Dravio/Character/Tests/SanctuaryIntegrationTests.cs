using System.Collections.Generic;
using System.Linq;
using Dravio.Character.Integration;
using NUnit.Framework;
using UnityEngine;

namespace Dravio.Character.Tests
{
    /// <summary>
    /// U1-CHAR.5: roster bookkeeping behind DragonVisualManager (pooling, spawn/despawn, stage switching, many
    /// dragons) and the Unified Sanctuary sync API. Pure logic: runs in the Unity Test Runner and with
    /// `dotnet test Assets/Dravio/Character/Tests/DotNet~`.
    /// </summary>
    public sealed class SanctuaryIntegrationTests
    {
        private sealed class Inst { public readonly int Id; public string Prefab; public bool Pooled; public Inst(int id, string p) { Id = id; Prefab = p; } }

        private static DragonVisualRoster<string, Inst> Roster(int cap, List<Inst> destroyed = null)
        {
            int n = 0;
            return new DragonVisualRoster<string, Inst>(p => new Inst(++n, p), i => i.Pooled = false, i => i.Pooled = true,
                i => { if (destroyed != null) destroyed.Add(i); }, cap);
        }

        [Test]
        public void Roster_SpawnKeepsInstanceForSamePrefabAndPoolsOnDespawn()
        {
            var r = Roster(4);
            DragonVisualRoster<string, Inst>.AcquireResult how;
            Inst a = r.Acquire("d1", "emberling_Baby", out how);
            Assert.AreEqual(DragonVisualRoster<string, Inst>.AcquireResult.Created, how);
            Assert.AreSame(a, r.Acquire("d1", "emberling_Baby", out how), "re-spawn: same instance, no churn");
            Assert.AreEqual(DragonVisualRoster<string, Inst>.AcquireResult.Kept, how);
            Assert.IsTrue(r.Release("d1"));
            Assert.IsTrue(a.Pooled);
            Assert.IsFalse(r.Release("d1"), "double despawn harmless");
            Assert.AreEqual(1, r.FreeCount("emberling_Baby"));
            Assert.AreSame(a, r.Acquire("d2", "emberling_Baby", out how), "another dragon reuses the pooled one");
            Assert.AreEqual(DragonVisualRoster<string, Inst>.AcquireResult.Reused, how);
            Assert.IsFalse(a.Pooled);
            Assert.AreEqual((1, 1), (r.Created, r.Reused));
        }

        [Test]
        public void Roster_StageSwitchToOtherPrefabReturnsOldInstanceFirst()
        {
            var r = Roster(4);
            DragonVisualRoster<string, Inst>.AcquireResult how;
            Inst baby = r.Acquire("d1", "emberling_Baby", out how);
            Inst adult = r.Acquire("d1", "emberling_Adult", out how);
            Assert.AreEqual(DragonVisualRoster<string, Inst>.AcquireResult.Swapped, how);
            Assert.AreNotSame(baby, adult);
            Assert.IsTrue(baby.Pooled);
            Assert.AreEqual(1, r.Count, "still one live instance per UID");
            string key;
            Assert.IsTrue(r.TryGetKey("d1", out key));
            Assert.AreEqual("emberling_Adult", key);
            Assert.AreSame(baby, r.Acquire("d2", "emberling_Baby", out how), "the baby model is reused by the next baby");
        }

        [Test]
        public void Roster_ThirtyDragonsFivePrefabs_NoInstanceSharedAndCapDestroysOverflow()
        {
            var destroyed = new List<Inst>();
            var r = Roster(2, destroyed);
            string[] prefabs = { "emberling_Baby", "sprout_Baby", "droplet_Baby", "zephyr", "steamdrake_Juvenile" };
            DragonVisualRoster<string, Inst>.AcquireResult how;
            var live = new Dictionary<string, Inst>();
            for (int i = 0; i < 30; i++) live["d" + i] = r.Acquire("d" + i, prefabs[i % 5], out how);
            Assert.AreEqual(30, r.Count);
            Assert.AreEqual(30, live.Values.Distinct().Count(), "one instance per UID");
            for (int i = 0; i < 30; i++) r.Release("d" + i);
            Assert.AreEqual(0, r.Count);
            foreach (var p in prefabs) Assert.AreEqual(2, r.FreeCount(p), "free list capped per prefab");
            Assert.AreEqual(20, destroyed.Count, "6 per prefab, 2 kept, 4 destroyed");
            Assert.AreEqual(20, r.Destroyed);
            for (int i = 0; i < 30; i++) r.Acquire("e" + i, prefabs[i % 5], out how);
            Assert.AreEqual(30 + 20, r.Created, "10 reused, 20 new");
            Assert.AreEqual(10, r.Reused);
        }

        // ---- Sanctuary sync ----

        private sealed class FakeTarget : IDragonVisualTarget
        {
            public readonly Dictionary<string, DragonVisualSpawn> Live = new Dictionary<string, DragonVisualSpawn>();
            public readonly List<string> Log = new List<string>();
            public HashSet<string> RejectSpecies = new HashSet<string>();
            public bool IsLive(string uid) { return Live.ContainsKey(uid); }
            public bool Spawn(DragonVisualSpawn s)
            {
                Log.Add("spawn " + s.uid + " " + s.speciesId + "/" + s.stage);
                if (RejectSpecies.Contains(s.speciesId)) return false;
                Live[s.uid] = s;
                return true;
            }
            public void Despawn(string uid) { Log.Add("despawn " + uid); Live.Remove(uid); }
            public bool SetStage(string uid, DragonVisualStage stage) { Log.Add("stage " + uid + " " + stage); Live[uid].stage = stage; return true; }
            public bool ApplyState(string uid, DragonVisualState st) { Live[uid].state = st; return true; }
            public bool Trigger(string uid, DragonInteraction i) { Log.Add("trigger " + uid + " " + i); return true; }
        }

        private static SanctuaryDragonFrame F(string uid, string species, DragonVisualStage stage, bool visible = true, DragonLocomotion loco = DragonLocomotion.Idle)
        {
            return new SanctuaryDragonFrame { uid = uid, speciesId = species, stage = stage, visible = visible, locomotion = loco, rotation = Quaternion.identity };
        }

        [Test]
        public void Sync_SpawnsUpdatesStagesAndDespawnsToMatchTheSanctuary()
        {
            var t = new FakeTarget();
            var sync = new SanctuaryDragonSync(t);
            SanctuarySyncReport r = sync.Sync(new[] { F("a", "emberling", DragonVisualStage.Baby), F("b", "sproutling", DragonVisualStage.Adult) });
            Assert.AreEqual((2, 0, 0, 0), (r.spawned, r.despawned, r.stageChanged, r.updated));
            r = sync.Sync(new[] { F("a", "emberling", DragonVisualStage.Juvenile, loco: DragonLocomotion.Walk), F("b", "sproutling", DragonVisualStage.Adult) });
            Assert.AreEqual((0, 0, 1, 2), (r.spawned, r.despawned, r.stageChanged, r.updated), "growth = SetStage, not respawn");
            Assert.AreEqual(DragonLocomotion.Walk, t.Live["a"].state.locomotion);
            r = sync.Sync(new[] { F("b", "sproutling", DragonVisualStage.Adult) });
            Assert.AreEqual(1, r.despawned, "sold / released dragon disappears");
            Assert.IsFalse(t.IsLive("a"));
            r = sync.Sync(new[] { F("b", "sproutling", DragonVisualStage.Adult, visible: false) });
            Assert.AreEqual(1, r.despawned, "away on an expedition: hidden");
            r = sync.Sync(new[] { F("b", "sproutling", DragonVisualStage.Adult) });
            Assert.AreEqual(1, r.spawned, "back: shown again");
            Assert.AreEqual(1, sync.ShownCount);
        }

        [Test]
        public void Sync_SpeciesOrVariantChangeRespawns_DuplicatesAndFailuresAreCounted()
        {
            var t = new FakeTarget { RejectSpecies = new HashSet<string> { "unknown" } };
            var sync = new SanctuaryDragonSync(t);
            sync.Sync(new[] { F("a", "emberling", DragonVisualStage.Adult) });
            var v = F("a", "emberling", DragonVisualStage.Adult); v.variantId = "frost";
            SanctuarySyncReport r = sync.Sync(new[] { v });
            Assert.AreEqual(1, r.spawned, "variant change: respawn so the phenotype is reapplied");
            r = sync.Sync(new[] { v, F("a", "zephyr", DragonVisualStage.Adult), F("x", "unknown", DragonVisualStage.Baby), F(null, "emberling", DragonVisualStage.Baby) });
            Assert.AreEqual(3, r.failed, "duplicate UID, missing model, empty UID");
            Assert.AreEqual("emberling", t.Live["a"].speciesId, "first entry for a UID wins");
            Assert.IsFalse(sync.IsShown("x"));
            Assert.IsTrue(sync.Trigger("a", DragonInteraction.Eat));
            Assert.IsFalse(sync.Trigger("x", DragonInteraction.Eat));
            sync.Clear();
            Assert.AreEqual(0, t.Live.Count);
        }

        [Test]
        public void Sync_ThirtyDragonsSteadyStateDoesNoSpawnWork()
        {
            var t = new FakeTarget();
            var sync = new SanctuaryDragonSync(t);
            var frames = Enumerable.Range(0, 30).Select(i => F("d" + i, i % 2 == 0 ? "emberling" : "zephyr", DragonVisualStage.Adult)).ToArray();
            Assert.AreEqual(30, sync.Sync(frames).spawned);
            t.Log.Clear();
            for (int k = 0; k < 10; k++) Assert.AreEqual(30, sync.Sync(frames.Reverse().ToArray()).updated, "order does not matter");
            Assert.AreEqual(0, t.Log.Count, "no spawn/despawn/stage calls in steady state");
        }

        [Test]
        public void StageMapping_AcceptsCanonicalNamesOnly()
        {
            DragonVisualStage s;
            Assert.IsTrue(SanctuaryVisualMapping.TryStage("Adult", out s)); Assert.AreEqual(DragonVisualStage.Adult, s);
            Assert.IsTrue(SanctuaryVisualMapping.TryStage("juvenile", out s)); Assert.AreEqual(DragonVisualStage.Juvenile, s);
            Assert.IsTrue(SanctuaryVisualMapping.TryStage(" baby ", out s)); Assert.AreEqual(DragonVisualStage.Baby, s);
            Assert.IsFalse(SanctuaryVisualMapping.TryStage("elder", out s));
            Assert.IsFalse(SanctuaryVisualMapping.TryStage(null, out s));
        }
    }
}
