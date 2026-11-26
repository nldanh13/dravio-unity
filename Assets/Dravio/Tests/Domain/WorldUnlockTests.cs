using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Queries;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// Quyết định thiết kế U2.8 do chủ dự án duyệt (Docs/adr/0201-u2-8-world-unlock-expedition-materials.md): mở Thủy Vực/Thiên Giới,
    /// trứng cố định một lần, hàng chờ khi kho đầy, vật liệu chuẩn, trang trí không hiệu ứng, kho trung lập. Implementation mới.
    /// </summary>
    public sealed class WorldUnlockTests
    {
        private static (GameState, FixedClock) Game()
        {
            var clock = new FixedClock(T0);
            GameState s = InitialStateFactory.Create("unlock", T0, Rng.Create(4));
            s.UnlockedAchievements.Add("ACH_FIRST_ADULT"); s.ClaimedAchievements.Add("ACH_FIRST_ADULT");
            return (s, clock);
        }

        /// <summary>Đủ điều kiện Thủy Vực: hướng dẫn hoàn tất, 3 sinh cảnh Khởi Nguyên, nhiệm vụ thủy sinh (hàm engine — nội dung nhiệm vụ TBD).</summary>
        private static GameState ReadyForOcean(GameState s0)
        {
            GameState s = s0.DeepClone();
            foreach (string d in ProgressCatalog.DiscoveryIds) Zones.RecordDiscovery(s, d, new List<DomainEvent>());
            foreach (string h in new[] { "origin_warm_rocks", "origin_pondbank" }) Zones.ApplyUnlock(s, h, new List<DomainEvent>());
            s.Tutorial.Status = TutorialStatus.Completed;
            s.Tutorial.Completed = ProgressCatalog.TutorialStepIds.ToList();
            Assert.That(WorldExpansion.ApplyQuestComplete(s, "QUEST_AQUATIC_EXPLORATION", new List<DomainEvent>()), Is.Null);
            return s;
        }

        private static string Missing(GameState s, string zone) => string.Join(",", WorldExpansion.MissingRequirements(s, zone));

        [Test]
        public void Ocean_requires_tutorial_all_origin_habitats_and_the_aquatic_quest_without_gold_or_level()
        {
            var (s0, clock) = Game();
            Assert.That(Missing(s0, "ocean"), Is.EqualTo("tutorial-completed,origin-habitats,quest:QUEST_AQUATIC_EXPLORATION"));
            DispatchResult no = CommandDispatcher.Dispatch(s0, new UnlockZoneCommand(Rid(), "ocean"), clock);
            Assert.That(no.Error.Code, Is.EqualTo(DomainErrorCode.UnlockRequirementMissing));
            GameState ready = ReadyForOcean(s0);
            Assert.That(Missing(ready, "ocean"), Is.Empty);
            Assert.That(ready.Dragons.Any(d => d.SpeciesId == "droplet"), Is.False, "không cần sở hữu Droplet");
            long gold = ready.Player.Gold.Micro;
            DispatchResult r = Ok(ready, new UnlockZoneCommand(Rid(), "ocean"), clock);
            Assert.That(r.State.FindZone("ocean").Unlocked, Is.True);
            Assert.That(r.State.Player.Gold.Micro, Is.EqualTo(gold), "không chi phí vàng");
            Assert.That((r.State.PendingPack.SpeciesId, r.State.PendingPack.Mutation, r.State.PendingPack.PackId), Is.EqualTo(("droplet", MutationType.Normal, "ZONE_EGG_OCEAN")));
            Assert.That(r.State.Player.PityCount, Is.EqualTo(ready.Player.PityCount), "trứng cố định không tính pity");
            Assert.That(r.Events.OfType<RewardEggGranted>().Single().Delivered, Is.True);
            Fail(r.State, new UnlockZoneCommand(Rid(), "ocean"), clock, DomainErrorCode.AlreadyThere);
            Fail(r.State, new UnlockZoneCommand(Rid(), "moon"), clock, DomainErrorCode.UnknownZone);
            Fail(r.State, new UnlockZoneCommand(Rid(), "origin"), clock, DomainErrorCode.UnknownZone);
        }

        [Test]
        public void Sky_requires_ocean_and_the_sky_quest_and_gives_a_Normal_Zephyr_once()
        {
            var (s0, clock) = Game();
            GameState s = ReadyForOcean(s0);
            Assert.That(Missing(s, "sky"), Is.EqualTo("zone:ocean,quest:QUEST_SKY_EXPLORATION"));
            s = Ok(s, new UnlockZoneCommand(Rid(), "ocean"), clock).State;
            s = Ok(s, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock).State;
            WorldExpansion.ApplyQuestComplete(s, "QUEST_SKY_EXPLORATION", null);
            DispatchResult sky = Ok(s, new UnlockZoneCommand(Rid(), "sky"), clock);
            Assert.That((sky.State.PendingPack.SpeciesId, sky.State.PendingPack.Mutation), Is.EqualTo(("zephyr", MutationType.Normal)));
            Assert.That(sky.State.GrantedRewards, Is.EqualTo(new[] { "ZONE_EGG_OCEAN", "ZONE_EGG_SKY" }));
        }

        [Test]
        public void Reward_egg_waits_when_an_egg_is_pending_or_storage_is_full_and_is_never_lost_or_duplicated()
        {
            var (s0, clock) = Game();
            GameState s = ReadyForOcean(s0);
            s.Player.Gold = MicroGold.FromWholeGold(1000);
            s = Ok(s, new BuyPackCommand(Rid(), "starter"), clock).State; // đã có trứng chờ nhận
            DispatchResult u = Ok(s, new UnlockZoneCommand(Rid(), "ocean"), clock);
            Assert.That(u.Events.OfType<RewardEggGranted>().Single().Delivered, Is.False);
            Assert.That((u.State.RewardQueue.Single().RewardId, u.State.PendingPack.PackId), Is.EqualTo(("ZONE_EGG_OCEAN", "starter")));
            // Nhận trứng starter → trứng thưởng được giao ngay trong cùng lệnh.
            DispatchResult c = Ok(u.State, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock);
            Assert.That((c.State.RewardQueue.Count, c.State.PendingPack.PackId), Is.EqualTo((0, "ZONE_EGG_OCEAN")));
            Assert.That(c.Events.OfType<RewardEggDelivered>().Count(), Is.EqualTo(1));
            GameState hatched = Ok(c.State, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock).State;
            Assert.That(hatched.Dragons.Count(d => d.SpeciesId == "droplet"), Is.EqualTo(1));
            clock.Advance(60_000);
            DispatchResult later = Ok(hatched, Online(), clock);
            Assert.That((later.State.PendingPack, later.State.RewardQueue.Count), Is.EqualTo(((PendingPack)null, 0)), "không cấp lại");

            // Kho đầy: trứng nằm trong hàng chờ cho tới khi có chỗ.
            GameState full = ReadyForOcean(s0);
            for (int i = 0; full.Dragons.Count < StorageCatalog.MaxDragons; i++) full.Dragons.Add(Adult("f" + i, "zephyr"));
            DispatchResult uf = Ok(full, new UnlockZoneCommand(Rid(), "ocean"), clock);
            Assert.That((uf.State.PendingPack, uf.State.RewardQueue.Count), Is.EqualTo(((PendingPack)null, 1)));
            Assert.That(Ecosystem.ZoneUnlocks(uf.State).Single(z => z.ZoneId == "ocean").RewardQueued, Is.True);
            DispatchResult sold = Ok(uf.State, new SellDragonCommand(Rid(), "f0"), clock);
            Assert.That((sold.State.PendingPack?.PackId, sold.State.RewardQueue.Count), Is.EqualTo(("ZONE_EGG_OCEAN", 0)), "có chỗ → giao");
        }

        [Test]
        public void Quest_contents_are_TBD_and_nothing_in_normal_play_completes_them()
        {
            Assert.That(WorldUnlockCatalog.Quests.All(q => !q.ObjectiveDefined), Is.True);
            var (s0, clock) = Game();
            GameState s = s0;
            foreach (GameCommand c in new GameCommand[] { Online(), new ViewBiologyProfileCommand(Rid(), "emberling"), Feed() }) s = CommandDispatcher.Dispatch(s, c, clock).State;
            Assert.That(s.CompletedQuests, Is.Empty);
            GameState q = s.DeepClone();
            Assert.That(WorldExpansion.ApplyQuestComplete(q, "QUEST_AQUATIC_EXPLORATION", null), Is.Null);
            Assert.That(WorldExpansion.ApplyQuestComplete(q, "QUEST_AQUATIC_EXPLORATION", null), Is.EqualTo(DomainErrorCode.AlreadyThere));
            Assert.That(WorldExpansion.ApplyQuestComplete(q, "QUEST_NOPE", null), Is.EqualTo(DomainErrorCode.InvalidCommand));
        }

        [Test]
        public void Materials_are_the_approved_ids_stone_is_inactive_and_quantities_stay_in_2_to_4()
        {
            // U2.13: stardust thêm ở cuối, chưa kích hoạt khi bảng sky_cloud chưa duyệt.
            Assert.That(ExpeditionCatalog.MaterialIds, Is.EqualTo(new[] { "wood", "seed", "stone", "crystal", "coral", "stardust" }));
            Assert.That(ExpeditionCatalog.Materials.Where(m => !m.Active).Select(m => m.Id), Is.EqualTo(new[] { "stone", "stardust" }));
            var sources = ExpeditionCatalog.Routes.SelectMany(r => r.Materials).Distinct().ToList();
            Assert.That(ExpeditionCatalog.Materials.Where(m => m.Active).Select(m => m.Id), Is.EquivalentTo(sources), "chỉ vật liệu có nguồn mới kích hoạt");
            Assert.That(ExpeditionCatalog.Routes.All(r => r.SuccessPermille == 1000), Is.True, "MVP thành công 100 %");
            var (s0, clock) = Game();
            for (int i = 0; i < 200; i++)
            {
                GameState s = s0.DeepClone();
                s.Dragons.Add(Adult("x", "emberling"));
                s.SaveId = "mat-" + i;
                GameState a = Ok(s, new StartExpeditionCommand("r", "origin_forest", "x"), clock).State;
                Assert.That(a.Expedition.Rewards.Sum(kv => kv.Value), Is.InRange(2, 4), "tổng 2–4 đơn vị, không phải mỗi loại");
                clock.Set(a.Expedition.FinishAtMs);
                GameState c = Ok(a, new ClaimExpeditionCommand("c"), clock).State;
                // U2.11: + mốc thám hiểm đầu tiên (2 gỗ + 2 hạt giống, một lần mỗi save).
                Assert.That(Ecosystem.Materials(c).Sum(m => m.Quantity), Is.EqualTo(a.Expedition.Rewards.Sum(kv => kv.Value) + 4));
                clock.Set(T0);
            }
        }

        [Test]
        public void Decoration_effects_are_configured_but_all_zero()
        {
            Assert.That(DecorationCatalog.All.All(d => d.Effects.IsZero), Is.True);
        }

        [Test]
        public void Expedition_health_thresholds_are_checked_since_U2_6()
        {
            // U2.8 báo bốn điều kiện là "chưa kiểm được"; U2.6 kiểm thật nên danh sách rỗng và rồng thiếu chỉ số bị chặn.
            Assert.That(Expedition.Expeditions.UncheckedHealthConditions, Is.Empty);
            var (s0, _) = Game();
            GameState s = s0.DeepClone(); s.Dragons.Add(Adult("x", "emberling")); s.Dragons.Add(Adult("weak", "emberling", d => d.Vitals.Mobility = 79));
            ExpeditionCandidate c = Ecosystem.ExpeditionOptions(s).First().Candidates.Single(x => x.DragonUid == "x");
            Assert.That((c.Blocked, c.UncheckedConditions.Count), Is.EqualTo(((DomainErrorCode?)null, 0)));
            ExpeditionCandidate w = Ecosystem.ExpeditionOptions(s).First().Candidates.Single(x => x.DragonUid == "weak");
            Assert.That((w.Blocked, w.Detail), Is.EqualTo(((DomainErrorCode?)DomainErrorCode.VitalsBelowThreshold, "mobility>=80")));
        }
    }
}
