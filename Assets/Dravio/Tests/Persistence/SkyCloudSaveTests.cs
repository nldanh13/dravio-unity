using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.World;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Tests.Support;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.13 (ADR 0208) × Save Schema v10 — không đổi schema, không migration. Bụi sao nằm trong <c>player.inventory</c>, bộ đếm sky_cloud trong
    /// <c>expeditionsClaimed</c>, trứng Aurorawing trong <c>grantedRewards</c> / <c>rewardQueue</c> sẵn có.
    /// </summary>
    public sealed class SkyCloudSaveTests
    {
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static DragonState Adult(string uid, string species, DragonLocation at)
        {
            var g = new Genotype();
            g.Set("pigment", "R", "r"); g.Set("pattern", "P", "p"); g.Set("eye", "B", "b");
            return new DragonState
            {
                Uid = uid, SpeciesId = species, Genotype = g, Mutation = MutationType.Normal, XpSec = SpeciesCatalog.Get(species).GrowthTimeSec, Location = at,
                Personality = Personality.Calm, Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack, Activity = DragonActivity.Idle,
            };
        }

        /// <summary>Ba thế giới mở, một Zephyr ở Thiên Giới, kho đầy (500 rồng) để trứng Aurorawing phải chờ trong rewardQueue.</summary>
        private static GameState Flown(int trips, bool fullStorage)
        {
            GameState s = InitialStateFactory.Create("sky-save", Day0, Rng.Create(7u));
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            s.Dragons.Add(Adult("flyer", "zephyr", DragonLocation.Zone("sky", "sky_low_island")));
            if (fullStorage) for (int i = 0; i < StorageCatalog.MaxDragons - 1; i++) s.Dragons.Add(Adult("fill-" + i.ToString("000"), "sprout", DragonLocation.Storage));
            foreach (ZoneState z in s.Zones) if (z.Id == "origin") z.UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            WorldExpansion.ApplyZoneUnlock(s, "ocean", null);
            WorldExpansion.ApplyZoneUnlock(s, "sky", null);
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = new List<string>(ProgressCatalog.TutorialStepIds);
            s.Clock = new ClockState(Day0, Day0);
            Codex.Observe(s, null);
            long now = Day0;
            for (int i = 0; i < trips; i++)
            {
                DispatchResult a = CommandDispatcher.Dispatch(s, new StartExpeditionCommand(PFix.Rid(), "sky_cloud", "flyer"), new FixedClock(now));
                Assert.That(a.Ok, Is.True, a.Error?.ToString());
                now = a.State.Expedition.FinishAtMs;
                DispatchResult c = CommandDispatcher.Dispatch(a.State, new ClaimExpeditionCommand(PFix.Rid()), new FixedClock(now));
                Assert.That(c.Ok, Is.True, c.Error?.ToString());
                s = c.State;
            }
            return s;
        }

        [Test]
        public void Queued_aurorawing_egg_and_stardust_round_trip_byte_for_byte_under_schema_v10()
        {
            GameState s;
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA)) s = Flown(15, fullStorage: true);
            Assert.That(s.SchemaVersion, Is.EqualTo(10), "không Schema v11");
            Assert.That(s.RewardQueue.Select(q => q.RewardId), Is.EqualTo(new[] { "EXPEDITION_EGG_AURORAWING" }));
            Assert.That(s.Player.Inventory["stardust"], Is.GreaterThan(0));
            string text = SaveSchema.Serialize(s, PFix.T0);
            // Nạp lại khi bảng sky_cloud TẮT (vd chủ dự án tạm tắt tuyến): save vẫn hợp lệ, trứng vẫn chờ, bụi sao vẫn giữ.
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.ToString());
            GameState back = r.Save.State;
            Assert.That(PFix.Canonical(back), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            Assert.That(back.RewardQueue.Single().SpeciesId, Is.EqualTo("aurorawing"));
            Assert.That(back.ExpeditionsClaimed["sky_cloud"], Is.EqualTo(15));
            // Sau khi nạp: bán một rồng → trứng được giao; nhận thêm chuyến không trao lại (bảng bật lại).
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA))
            {
                var clock = new FixedClock(back.Clock.HighWaterAtMs + 1000);
                DispatchResult sold = CommandDispatcher.Dispatch(back, new SellDragonCommand(PFix.Rid(), "fill-000"), clock);
                Assert.That(sold.Ok, Is.True);
                Assert.That(sold.State.PendingPack.SpeciesId, Is.EqualTo("aurorawing"));
                GameState again = SaveFile.Parse(SaveSchema.Serialize(sold.State, PFix.T0)).Save.State;
                Assert.That(again.GrantedRewards.Count(id => id == "EXPEDITION_EGG_AURORAWING"), Is.EqualTo(1));
                Assert.That(again.RewardQueue, Is.Empty);
            }
        }

        [Test]
        public void Old_v10_saves_without_stardust_or_sky_claims_load_unchanged()
        {
            GameState s = Flown(0, fullStorage: false);
            string text = SaveSchema.Serialize(s, PFix.T0);
            Assert.That(text, Does.Not.Contain("stardust"));
            Assert.That(text, Does.Not.Contain("EXPEDITION_EGG"));
            ParseResult r = SaveFile.Parse(text);
            Assert.That(r.Ok, Is.True, r.ToString());
        }

        [Test]
        public void Forged_aurorawing_grant_is_rejected_on_load()
        {
            GameState s;
            using (SkyCloudBalance.Use(SkyCloudBalance.ProposedA)) s = Flown(3, fullStorage: false);
            GameState forged = s.DeepClone();
            forged.GrantedRewards.Add("EXPEDITION_EGG_AURORAWING");
            forged.RewardQueue.Add(new QueuedEgg { RewardId = "EXPEDITION_EGG_AURORAWING", SpeciesId = "aurorawing", Mutation = MutationType.Normal, QueuedAtMs = Day0 });
            ParseResult r = SaveFile.Parse(SaveSchema.Serialize(forged, PFix.T0));
            Assert.That(r.Ok, Is.False, "trứng Aurorawing trước mốc 15 chuyến bị từ chối");
        }
    }
}
