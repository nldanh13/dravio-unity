using System;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.World;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using Dravio.Platform;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.5 × Save Schema v5 (implementation mới, chỉ ở bản Unity): tổ ấp qua vòng lưu–nạp, migration v4 → v5, lưu ngay sau giao dịch,
    /// chống mất vàng / nhân bản rồng / nhận thưởng hai lần qua khởi động lại, và cổng xác thực quảng cáo cho gói miễn phí.
    /// </summary>
    public sealed class BreedingSaveTests
    {
        private static DragonState Adult(string uid, string species)
        {
            var g = new Genotype(); g.Set("pigment", "R", "r"); g.Set("pattern", "P", "p"); g.Set("eye", "B", "b");
            return new DragonState
            {
                Uid = uid, SpeciesId = species, Genotype = g, Mutation = MutationType.Normal, XpSec = SpeciesCatalog.Get(species).GrowthTimeSec,
                Location = DragonLocation.Storage, Personality = Personality.Calm, Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack,
                Activity = DragonActivity.Idle,
            };
        }

        private static GameState RichPair()
        {
            GameState s = PFix.Fresh();
            s.Player.Gold = MicroGold.FromWholeGold(50_000);
            s.Dragons.Add(Adult("pa", "emberling"));
            s.Dragons.Add(Adult("pb", "sprout"));
            return s;
        }

        private static GameState Do(GameState s, GameCommand c, long nowMs)
        {
            DispatchResult r = CommandDispatcher.Dispatch(s, c, new FixedClock(nowMs));
            Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error);
            return r.State;
        }

        private static GameState Reload(ISaveStore store)
        {
            LoadOutcome r = new SaveManager(store, () => PFix.T0).Load();
            Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded));
            return r.Save.State;
        }

        [Test]
        public void Incubating_nest_round_trips_and_bred_child_lineage_is_readable_by_TypeScript_after_claim()
        {
            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            GameState s = RichPair();
            long t = s.Clock.LastProcessedAtMs + 1000;
            GameState incubating = Do(s, new StartBreedCommand(PFix.Rid(), "pa", "pb"), t);
            string text = SaveSchema.Serialize(incubating, t);
            ParseResult back = SaveFile.Parse(text);
            Assert.That(back.Ok, Is.True, back.ToString());
            Assert.That(PFix.Canonical(back.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(incubating))));
            Assert.That(back.Save.State.BreedingNest.ParentIds, Is.EqualTo(new[] { "pa", "pb" }));
            PFix.Export(dir, "u25-incubating", text);

            long done = incubating.BreedingNest.FinishAtMs;
            GameState claimed = Do(Do(incubating, new AdvanceTimeCommand(PFix.Rid(), AdvanceMode.Online), done), new ClaimBreedCommand(PFix.Rid(), MoveTarget.Storage), done);
            DragonState child = claimed.Dragons.Last();
            Assert.That((child.Origin, child.Generation, child.Ancestry), Is.EqualTo((DragonOrigin.Breed, 2, Ancestry.Known)));
            string text2 = SaveSchema.Serialize(claimed, done);
            Assert.That(PFix.Canonical(SaveFile.Parse(text2).Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(claimed))));
            // Tổ trống → có ảnh v4: TS đọc được rồng lai (parentIds, generation 2, ancestry known, origin breed).
            PFix.Export(dir, "u25-claimed", text2);
            Assert.That(SaveFile.Parse(PFix.AsV4Text(text2)).Ok, Is.True);
        }

        [Test]
        public void V4_save_migrates_to_v5_keeps_everything_archives_the_original_and_frees_orphan_breeding_dragons()
        {
            GameState s = RichPair();
            s.FindDragon("pa").Activity = DragonActivity.Breeding; // chỉ có thể có ở state v4 viết tay: không có tổ
            s.SchemaVersion = 4;
            string v4 = PFix.EnvelopeAt(4, s);
            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = v4;
            var mgr = new SaveManager(store, () => PFix.T0);
            LoadOutcome r = mgr.Load();
            Assert.That((r.Kind, r.Save.MigratedFrom), Is.EqualTo((LoadKind.Loaded, (int?)4)));
            GameState m = r.Save.State;
            Assert.That((m.SchemaVersion, m.BreedingNest, m.FindDragon("pa").Activity), Is.EqualTo((5, (BreedingEgg)null, DragonActivity.Idle)));
            Assert.That((m.Player.Gold, m.Dragons.Count, m.Player.PityCount), Is.EqualTo((s.Player.Gold, s.Dragons.Count, s.Player.PityCount)));
            Assert.That(mgr.Save(m), Is.True);
            Assert.That(store.GetArchive("archive:schema-4"), Is.EqualTo(v4), "save v4 gốc được giữ nguyên văn");
            Assert.That(SaveFile.Parse(store.Get(SaveSlot.Main)).Save.MigratedFrom, Is.Null);
        }

        [Test]
        public void Tampered_nest_is_rejected_on_load()
        {
            GameState s = RichPair();
            long t = s.Clock.LastProcessedAtMs + 1000;
            GameState inc = Do(s, new StartBreedCommand(PFix.Rid(), "pa", "pb"), t);
            GameState cheat = inc.DeepClone();
            cheat.BreedingNest.FinishAtMs -= 60_000; // rút ngắn ấp mà không qua tapEgg
            ParseResult r = SaveFile.Parse(SaveSchema.Serialize(cheat, t));
            Assert.That((r.Ok, r.Code), Is.EqualTo((false, SaveErrorCode.InvalidState)));
            GameState freed = inc.DeepClone();
            freed.FindDragon("pa").Activity = DragonActivity.Idle; // bố mẹ thoát tổ để lai lần hai
            Assert.That(SaveFile.Parse(SaveSchema.Serialize(freed, t)).Code, Is.EqualTo(SaveErrorCode.InvalidState));
        }

        private static (SessionRig rig, MemorySaveStore store) RigWith(GameState s, Func<bool> ads = null)
        {
            var store = new MemorySaveStore();
            var mgr = new SaveManager(store, () => s.Clock.LastProcessedAtMs);
            mgr.Load();
            mgr.Save(s);
            return (new SessionRig(store, s.Clock.LastProcessedAtMs) { RewardedAds = ads }, store);
        }

        [Test]
        public void Session_saves_immediately_after_buyPack_startBreed_and_claimBreed_and_restarts_never_duplicate()
        {
            var (rig, store) = RigWith(RichPair());
            GameSession a = rig.NewSession();
            int w = store.Writes;
            Assert.That(a.Send(id => new BuyPackCommand(id, "elemental")), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 1), "mua gói lưu ngay");
            Assert.That(a.Send(id => new StartBreedCommand(id, "pa", "pb")), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 2), "bắt đầu lai lưu ngay");
            long goldAfter = a.State.Player.Gold.Micro;
            string eggId = a.State.BreedingNest.Id;
            a.Dispose(); // tắt app ngay sau giao dịch

            GameSession b = rig.NewSession();
            Assert.That((b.State.BreedingNest?.Id, b.State.PendingPack != null), Is.EqualTo((eggId, true)), "trứng và gói còn nguyên, không roll lại");
            Assert.That(b.State.Player.Gold.Micro, Is.GreaterThanOrEqualTo(goldAfter), "vàng không bị trừ lần hai khi nạp lại");
            Assert.That(b.Send(id => new StartBreedCommand(id, "pa", "pb")).Code, Is.EqualTo(DomainErrorCode.NestBusy));
            rig.Run(b.State.BreedingNest.DurationMs + 2000, stepMs: 60_000);
            Assert.That(b.State.BreedingNest.Status, Is.EqualTo(EggStatus.Ready));
            Assert.That(b.Send(id => new ClaimBreedCommand(id, MoveTarget.Storage)), Is.Null);
            int dragons = b.State.Dragons.Count;
            b.Dispose();

            GameSession c = rig.NewSession();
            Assert.That((c.State.Dragons.Count, c.State.BreedingNest), Is.EqualTo((dragons, (BreedingEgg)null)), "nhận con một lần, không nhân bản");
            Assert.That(c.Send(id => new ClaimBreedCommand(id, MoveTarget.Storage)).Code, Is.EqualTo(DomainErrorCode.NoEgg));
            Assert.That(c.State.Player.Stats.TotalBred, Is.EqualTo(1));
            c.Dispose();
        }

        [Test]
        public void Free_starter_needs_a_verified_rewarded_ad_and_is_saved_immediately()
        {
            var (rig, store) = RigWith(RichPair());
            GameSession noAds = rig.NewSession();
            DomainError e = noAds.Send(id => new ClaimFreeStarterPackCommand(id));
            Assert.That((e.Code, e.Detail), Is.EqualTo((DomainErrorCode.InvalidCommand, "rewarded-ad-not-verified")));
            Assert.That(noAds.State.PendingPack, Is.Null);
            noAds.Dispose();

            int verified = 1;
            var (rig2, store2) = RigWith(RichPair(), () => verified-- > 0);
            GameSession s = rig2.NewSession();
            int w = store2.Writes;
            Assert.That(s.Send(id => new ClaimFreeStarterPackCommand(id)), Is.Null);
            Assert.That((store2.Writes, Reload(store2).PendingPack?.PackId), Is.EqualTo((w + 1, "starter")));
            Assert.That(s.Send(id => new ClaimPackCommand(id, MoveTarget.Storage)), Is.Null);
            Assert.That(s.Send(id => new ClaimFreeStarterPackCommand(id)).Detail, Is.EqualTo("rewarded-ad-not-verified"), "một lượt xem = một gói");
            Assert.That(Reload(store2).Player.Stats.DailyFreePacksClaimed, Is.EqualTo(1));
            s.Dispose();
        }

        [Test]
        public void Rejected_purchases_never_write_or_change_gold()
        {
            GameState poor = RichPair();
            poor.Player.Gold = MicroGold.FromWholeGold(10);
            // Đã nhận thưởng trưởng thành từ trước: lần advanceTime đầu của phiên không cộng thêm 100 G.
            poor.UnlockedAchievements.Add("ACH_FIRST_ADULT"); poor.ClaimedAchievements.Add("ACH_FIRST_ADULT");
            var (rig, store) = RigWith(poor);
            GameSession s = rig.NewSession();
            int w = store.Writes;
            long gold = s.State.Player.Gold.Micro;
            Assert.That(s.Send(id => new BuyPackCommand(id, "astral")).Code, Is.EqualTo(DomainErrorCode.NotEnoughGold));
            Assert.That(s.Send(id => new StartBreedCommand(id, "pa", "pb")).Code, Is.EqualTo(DomainErrorCode.NotEnoughGold));
            Assert.That((store.Writes, s.State.Player.Gold.Micro), Is.EqualTo((w, gold)));
            s.Dispose();
        }
    }
}
