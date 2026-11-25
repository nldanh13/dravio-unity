using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using Dravio.Platform;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.8 × Save Schema v7 (chỉ ở bản Unity): thám hiểm qua vòng lưu–nạp và qua thoát game giữa chừng, migration v6 → v7,
    /// thế giới đã mở rộng (Thủy Vực/Thiên Giới) qua lưu–nạp và cho TS đọc ảnh v4, sửa tay bị từ chối, không nhận thưởng hai lần.
    /// </summary>
    public sealed class ExpeditionSaveTests
    {
        private static DragonState Adult(string uid, string species) => new DragonState
        {
            Uid = uid, SpeciesId = species, Genotype = Gen(), Mutation = MutationType.Normal, XpSec = SpeciesCatalog.Get(species).GrowthTimeSec,
            Location = DragonLocation.Storage, Personality = Personality.Calm, Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack, Activity = DragonActivity.Idle,
        };

        private static Genotype Gen() { var g = new Genotype(); g.Set("pigment", "R", "r"); g.Set("pattern", "P", "p"); g.Set("eye", "B", "b"); return g; }

        private static GameState Base()
        {
            GameState s = PFix.Fresh();
            s.UnlockedAchievements.Add("ACH_FIRST_ADULT"); s.ClaimedAchievements.Add("ACH_FIRST_ADULT");
            s.Dragons.Add(Adult("x", "emberling"));
            Dravio.Domain.Progression.Codex.Observe(s, null);
            return s;
        }

        private static GameState Do(GameState s, GameCommand c, long now)
        {
            DispatchResult r = CommandDispatcher.Dispatch(s, c, new FixedClock(now));
            Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error);
            return r.State;
        }

        [Test]
        public void V6_save_migrates_to_v7_keeping_materials_and_freeing_orphan_expedition_dragons()
        {
            GameState s = Base();
            s.FindDragon("x").Activity = DragonActivity.Expedition; // chỉ có thể có ở save cũ viết tay: không có chuyến nào
            s.Player.Inventory["wood"] = 3;
            s.SchemaVersion = 6;
            string v6 = PFix.EnvelopeAt(6, s);
            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = v6;
            var mgr = new SaveManager(store, () => PFix.T0);
            LoadOutcome r = mgr.Load();
            Assert.That((r.Kind, r.Save.MigratedFrom), Is.EqualTo((LoadKind.Loaded, (int?)6)), r.Kind.ToString());
            GameState m = r.Save.State;
            Assert.That((m.SchemaVersion, m.Expedition, m.FindDragon("x").Activity, m.Player.Inventory["wood"]), Is.EqualTo((7, (ExpeditionJob)null, DragonActivity.Idle, 3L)));
            Assert.That(mgr.Save(m), Is.True);
            Assert.That(store.GetArchive("archive:schema-6"), Is.EqualTo(v6));
        }

        [Test]
        public void Expedition_and_expanded_world_round_trip_and_tampering_is_rejected()
        {
            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            GameState s = Base();
            WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
            WorldExpansion.ApplyZoneUnlock(s, "sky", null);
            long t = s.Clock.LastProcessedAtMs + 1000;
            GameState away = Do(s, new StartExpeditionCommand(PFix.Rid(), "origin_forest", "x"), t);
            string text = SaveSchema.Serialize(away, t);
            ParseResult back = SaveFile.Parse(text);
            Assert.That(back.Ok, Is.True, back.ToString());
            Assert.That(PFix.Canonical(back.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(away))));
            Assert.That(back.Save.State.Expedition.Rewards, Is.EqualTo(away.Expedition.Rewards), "phần thưởng đã chốt được lưu, không roll lại khi nạp");
            PFix.Export(dir, "u28-expedition", text);

            GameState cheat = away.DeepClone(); cheat.Expedition.FinishAtMs -= 60_000;
            Assert.That(SaveFile.Parse(SaveSchema.Serialize(cheat, t)).Code, Is.EqualTo(SaveErrorCode.InvalidState), "rút ngắn chuyến đi");
            GameState greedy = away.DeepClone(); greedy.Expedition.Rewards = new List<KeyValuePair<string, int>> { new KeyValuePair<string, int>("wood", 99) };
            Assert.That(SaveFile.Parse(SaveSchema.Serialize(greedy, t)).Code, Is.EqualTo(SaveErrorCode.InvalidState), "thưởng ngoài 2–4");

            long done = away.Expedition.FinishAtMs;
            GameState claimed = Do(away, new ClaimExpeditionCommand(PFix.Rid()), done);
            string text2 = SaveSchema.Serialize(claimed, done);
            PFix.Export(dir, "u28-world-claimed", text2);
            Assert.That(SaveFile.Parse(PFix.AsV4Text(text2)).Ok, Is.True, "ảnh v4: thế giới đã mở và vật liệu trong inventory vẫn hợp lệ với TS");
        }

        private static (SessionRig rig, MemorySaveStore store) RigWith(GameState s)
        {
            var store = new MemorySaveStore();
            var mgr = new SaveManager(store, () => s.Clock.LastProcessedAtMs);
            mgr.Load();
            mgr.Save(s);
            return (new SessionRig(store, s.Clock.LastProcessedAtMs), store);
        }

        [Test]
        public void Quitting_mid_expedition_completes_offline_and_rewards_are_claimed_exactly_once_across_restarts()
        {
            var (rig, store) = RigWith(Base());
            GameSession a = rig.NewSession();
            int w = store.Writes;
            Assert.That(a.Send(id => new StartExpeditionCommand(id, "origin_forest", "x")), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 1), "khởi hành lưu ngay (seed + thưởng đã chốt)");
            var rewards = a.State.Expedition.Rewards.ToList();
            long finish = a.State.Expedition.FinishAtMs;
            a.Dispose(); // thoát game giữa chuyến

            rig.Frozen(finish - rig.Clock.Now + 3_600_000); // một giờ sau khi chuyến đi lẽ ra đã xong
            GameSession b = rig.NewSession();
            Assert.That((b.State.Expedition.Status, b.State.FindDragon("x").Activity), Is.EqualTo((ExpeditionStatus.Ready, DragonActivity.Idle)), "hoàn tất offline");
            Assert.That(b.State.Expedition.Rewards, Is.EqualTo(rewards));
            int w2 = store.Writes;
            Assert.That(b.Send(id => new ClaimExpeditionCommand(id)), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w2 + 1), "nhận thưởng lưu ngay");
            var inv = new Dictionary<string, long>(b.State.Player.Inventory);
            b.Dispose();

            GameSession c = rig.NewSession();
            Assert.That(c.State.Expedition, Is.Null);
            Assert.That(c.State.Player.Inventory, Is.EqualTo(inv), "không cộng thưởng lần hai");
            Assert.That(c.Send(id => new ClaimExpeditionCommand(id)).Code, Is.EqualTo(DomainErrorCode.NoExpedition));
            c.Dispose();
        }

        /// <summary>ADR 0201: đủ điều kiện mở Thủy Vực (hướng dẫn xong, 3 sinh cảnh Khởi Nguyên, nhiệm vụ thủy sinh) và kho đầy.</summary>
        private static GameState OceanReadyFullStorage()
        {
            GameState s = Base();
            foreach (string d in ProgressCatalog.DiscoveryIds) Zones.RecordDiscovery(s, d, new List<DomainEvent>());
            foreach (string h in new[] { "origin_warm_rocks", "origin_pondbank" }) Zones.ApplyUnlock(s, h, new List<DomainEvent>());
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = ProgressCatalog.TutorialStepIds.ToList();
            WorldExpansion.ApplyQuestComplete(s, "QUEST_AQUATIC_EXPLORATION", null);
            for (int i = 0; s.Dragons.Count < StorageCatalog.MaxDragons; i++) s.Dragons.Add(Adult("f" + i, "zephyr"));
            return s;
        }

        [Test]
        public void Queued_world_reward_egg_survives_save_reload_and_offline_and_is_delivered_exactly_once()
        {
            var (rig, store) = RigWith(OceanReadyFullStorage());
            GameSession a = rig.NewSession();
            int w = store.Writes;
            Assert.That(a.Send(id => new UnlockZoneCommand(id, "ocean")), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 1), "mở thế giới lưu ngay");
            Assert.That((a.State.PendingPack, a.State.RewardQueue.Count), Is.EqualTo(((PendingPack)null, 1)), "kho đầy: trứng vào hàng chờ");
            a.Dispose();

            rig.Frozen(8 * 3_600_000L); // offline lâu: không sinh thêm trứng
            GameSession b = rig.NewSession();
            Assert.That((b.State.RewardQueue.Count, b.State.GrantedRewards.Single(), b.State.PendingPack), Is.EqualTo((1, "ZONE_EGG_OCEAN", (PendingPack)null)));
            Assert.That(b.Send(id => new UnlockZoneCommand(id, "ocean")).Code, Is.EqualTo(DomainErrorCode.AlreadyThere));
            Assert.That(b.Send(id => new SellDragonCommand(id, "f0")), Is.Null);
            Assert.That((b.State.PendingPack.PackId, b.State.RewardQueue.Count), Is.EqualTo(("ZONE_EGG_OCEAN", 0)), "có chỗ → giao");
            b.Dispose();

            GameSession c = rig.NewSession();
            Assert.That(c.State.PendingPack.PackId, Is.EqualTo("ZONE_EGG_OCEAN"));
            Assert.That(c.Send(id => new ClaimPackCommand(id, MoveTarget.Storage)), Is.Null);
            int droplets = c.State.Dragons.Count(d => d.SpeciesId == "droplet");
            c.Dispose();
            GameSession d2 = rig.NewSession();
            Assert.That((d2.State.Dragons.Count(d => d.SpeciesId == "droplet"), d2.State.PendingPack, d2.State.RewardQueue.Count), Is.EqualTo((droplets, (PendingPack)null, 0)), "không cấp lại sau khi nạp");
            Assert.That(droplets, Is.EqualTo(1));
            d2.Dispose();
        }

        [Test]
        public void World_unlock_state_round_trips_migrates_empty_and_tampering_is_rejected()
        {
            GameState s = OceanReadyFullStorage();
            long t = s.Clock.LastProcessedAtMs + 1000;
            GameState q = Do(s, new UnlockZoneCommand(PFix.Rid(), "ocean"), t);
            string text = SaveSchema.Serialize(q, t);
            ParseResult back = SaveFile.Parse(text);
            Assert.That(back.Ok, Is.True, back.ToString());
            Assert.That(PFix.Canonical(back.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(q))));
            Assert.That(back.Save.State.RewardQueue.Single().SpeciesId, Is.EqualTo("droplet"));

            GameState dupGrant = q.DeepClone(); dupGrant.GrantedRewards.Add("ZONE_EGG_OCEAN");
            GameState ghost = q.DeepClone(); ghost.GrantedRewards.Clear();
            GameState noZone = q.DeepClone(); noZone.FindZone("ocean").Unlocked = false;
            GameState fakeQuest = q.DeepClone(); fakeQuest.CompletedQuests.Add("QUEST_NOPE");
            GameState twice = q.DeepClone(); twice.RewardQueue.Add(twice.RewardQueue[0].Clone());
            foreach (var (bad, why) in new[] { (dupGrant, "trao trùng"), (ghost, "hàng chờ chưa được trao"), (noZone, "thưởng của vùng chưa mở"), (fakeQuest, "nhiệm vụ lạ"), (twice, "trứng nhân đôi") })
                Assert.That(SaveFile.Parse(SaveSchema.Serialize(bad, t)).Code, Is.EqualTo(SaveErrorCode.InvalidState), why);

            GameState v6 = Base(); v6.SchemaVersion = 6;
            MigrateResult m = Migrations.Migrate(6, (Dravio.Persistence.Json.JsonObject)((Dravio.Persistence.Json.JsonObject)Dravio.Persistence.Json.JsonParser.Parse(PFix.EnvelopeAt(6, v6))).Get("state"));
            Assert.That(m.Ok, Is.True);
            foreach (string k in new[] { "completedQuests", "grantedRewards", "rewardQueue" })
                Assert.That(((Dravio.Persistence.Json.JsonArray)m.State.Get(k)).Count, Is.EqualTo(0), k);
        }
    }
}
