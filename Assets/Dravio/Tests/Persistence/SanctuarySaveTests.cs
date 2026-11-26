using System;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.Sanctuary;
using Dravio.Persistence.Json;
using Dravio.Persistence.Schema;
using Dravio.Persistence.Storage;
using Dravio.Persistence.Tests.Support;
using Dravio.Platform;
using NUnit.Framework;

namespace Dravio.Persistence.Tests
{
    /// <summary>
    /// U2.7 × Save Schema v6 (chỉ ở bản Unity): Bách khoa, trang trí, khóa, nâng cấp qua vòng lưu–nạp; migration v5 → v6 bảo toàn dữ liệu;
    /// lưu ngay sau bán / bán nhanh / nâng cấp / mua trang trí / khóa; không bán trùng, không nhận thưởng lại qua khởi động lại.
    /// </summary>
    public sealed class SanctuarySaveTests
    {
        private static DragonState Adult(string uid, string species, MutationType m = MutationType.Normal) => new DragonState
        {
            Uid = uid, SpeciesId = species, Genotype = Genetics(), Mutation = m, XpSec = SpeciesCatalog.Get(species).GrowthTimeSec,
            Location = DragonLocation.Storage, Personality = Personality.Calm, Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack, Activity = DragonActivity.Idle,
        };

        private static Genotype Genetics() { var g = new Genotype(); g.Set("pigment", "R", "r"); g.Set("pattern", "P", "p"); g.Set("eye", "B", "b"); return g; }

        private static GameState Rich()
        {
            GameState s = PFix.Fresh();
            s.Player.Gold = MicroGold.FromWholeGold(100_000);
            s.UnlockedAchievements.Add("ACH_FIRST_ADULT"); s.ClaimedAchievements.Add("ACH_FIRST_ADULT");
            s.Dragons.Add(Adult("c1", "emberling"));
            s.Dragons.Add(Adult("c2", "zephyr", MutationType.Golden));
            Codex.Observe(s, null);
            return s;
        }

        private static GameState Reload(ISaveStore store)
        {
            LoadOutcome r = new SaveManager(store, () => PFix.T0).Load();
            Assert.That(r.Kind, Is.EqualTo(LoadKind.Loaded));
            return r.Save.State;
        }

        [Test]
        public void V5_save_migrates_to_v6_backfilling_the_codex_and_keeping_every_other_field()
        {
            GameState s = Rich();
            s.Dragons.Add(Adult("gone", "gaia", MutationType.Prismatic));
            s.FindDragon("gone").Location = DragonLocation.Released;
            s.FindDragon("c1").XpSec = 150; // Juvenile (≥ 35 % của 300 s)
            s.FindZone("origin").DecorationIds.Add("deco_legacy"); // ID ngoài catalog: TS cho phép, phải được giữ
            s.SchemaVersion = 5;
            string v5 = PFix.EnvelopeAt(5, s);
            var store = new MemorySaveStore();
            store.Slots[SaveSlot.Main] = v5;
            var mgr = new SaveManager(store, () => PFix.T0);
            LoadOutcome r = mgr.Load();
            Assert.That((r.Kind, r.Save.MigratedFrom), Is.EqualTo((LoadKind.Loaded, (int?)5)), r.Kind.ToString());
            GameState m = r.Save.State;
            Assert.That(m.Codex.Select(e => e.SpeciesId + ":" + string.Join("/", e.DiscoveredMutations) + ":" + string.Join("/", e.ObservedStages) + ":" + e.TotalHatched),
                Is.EqualTo(new[] { "emberling:Normal:Baby/Juvenile:0", "zephyr:Golden:Baby/Juvenile/Adult:0", "gaia:Prismatic:Baby/Juvenile/Adult:0" }),
                "ghi bù từ mọi rồng, kể cả đã thả; stage ≤ stage hiện tại; totalHatched không suy diễn");
            Assert.That(m.FindZone("origin").DecorationIds, Is.EqualTo(new[] { "deco_legacy" }));
            Assert.That((m.Player.Gold, m.Dragons.Count, m.UnlockedAchievements.Count), Is.EqualTo((s.Player.Gold, s.Dragons.Count, s.UnlockedAchievements.Count)));
            Assert.That(mgr.Save(m), Is.True);
            Assert.That(store.GetArchive("archive:schema-5"), Is.EqualTo(v5));
            // Lần dispatch đầu sau migration: khám phá đã ghi bù mở thành tựu một lần (Golden, Prismatic, Legendary).
            DispatchResult d = CommandDispatcher.Dispatch(m, new AdvanceTimeCommand(PFix.Rid(), AdvanceMode.Online), new FixedClock(m.Clock.LastProcessedAtMs + 1000));
            Assert.That(d.Events.OfType<AchievementUnlocked>().Select(e => e.AchievementId), Is.EquivalentTo(new[] { "ACH_MUTATION_GOLDEN", "ACH_MUTATION_PRISMATIC", "ACH_LEGENDARY" }));
        }

        [Test]
        public void Codex_decorations_locks_and_upgrades_round_trip_and_export_for_TypeScript()
        {
            string dir = Environment.GetEnvironmentVariable("DRAVIO_EXPORT_DIR");
            GameState s = Rich();
            var clock = new FixedClock(s.Clock.LastProcessedAtMs + 1000);
            GameState Do(GameState st, GameCommand c) { DispatchResult r = CommandDispatcher.Dispatch(st, c, clock); Assert.That(r.Ok, Is.True, c.Type + ": " + r.Error); return r.State; }
            s = Do(s, new BuyDecorationCommand(PFix.Rid(), "origin", "sky_lantern"));
            s = Do(s, new BuyDecorationCommand(PFix.Rid(), "origin", "cherry_tree"));
            s = Do(s, new ToggleDecorationCommand(PFix.Rid(), "origin", "sky_lantern", false));
            s = Do(s, new LockDragonCommand(PFix.Rid(), "c2", true));
            s = Do(s, new BuyUpgradeCommand(PFix.Rid(), "mutationLuckLevel"));
            s = Do(s, new SellDragonCommand(PFix.Rid(), "c1"));
            string text = SaveSchema.Serialize(s, clock.NowMs());
            ParseResult back = SaveFile.Parse(text);
            Assert.That(back.Ok, Is.True, back.ToString());
            Assert.That(PFix.Canonical(back.Save.State), Is.EqualTo(PFix.Canonical(SaveSchema.ToPersistedState(s))));
            GameState b = back.Save.State;
            Assert.That((string.Join(",", b.FindZone("origin").DecorationIds), string.Join(",", b.FindZone("origin").ActiveDecorationIds)), Is.EqualTo(("cherry_tree,sky_lantern", "cherry_tree")));
            Assert.That((b.FindDragon("c2").Locked, b.Player.Upgrades.MutationLuckLevel, b.FindDragon("c1")), Is.EqualTo((true, 1, (DragonState)null)));
            Assert.That(Codex.Find(b, "emberling"), Is.Not.Null, "Bách khoa giữ loài đã bán");
            PFix.Export(dir, "u27-sanctuary", text);
            Assert.That(SaveFile.Parse(PFix.AsV4Text(text)).Ok, Is.True, "ảnh v4 (TS) vẫn hợp lệ: trang trí, khóa, cấp nâng cấp là trường có sẵn của v4");
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
        public void Sanctuary_transactions_save_immediately_and_restarts_never_resell_or_regrant()
        {
            var (rig, store) = RigWith(Rich());
            GameSession a = rig.NewSession();
            int w = store.Writes;
            Assert.That(a.Send(id => new SellDragonCommand(id, "c1")), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 1), "bán lưu ngay");
            Assert.That(a.Send(id => new BuyUpgradeCommand(id, "foodQualityLevel")), Is.Null);
            Assert.That(a.Send(id => new BuyDecorationCommand(id, "origin", "cherry_tree")), Is.Null);
            Assert.That(a.Send(id => new LockDragonCommand(id, "c2", true)), Is.Null);
            Assert.That(store.Writes, Is.EqualTo(w + 4));
            long gold = a.State.Player.Gold.Micro;
            var unlocked = a.State.UnlockedAchievements.ToList();
            a.Dispose();

            GameSession b = rig.NewSession();
            Assert.That((b.State.FindDragon("c1"), b.State.Player.Stats.TotalSold), Is.EqualTo(((DragonState)null, 1L)), "rồng đã bán không quay lại");
            Assert.That(b.State.Player.Gold.Micro, Is.GreaterThanOrEqualTo(gold));
            Assert.That(b.State.UnlockedAchievements, Is.EqualTo(unlocked));
            Assert.That(b.Send(id => new SellDragonCommand(id, "c1")).Code, Is.EqualTo(DomainErrorCode.UnknownDragon));
            Assert.That(b.Send(id => new SellDragonCommand(id, "c2")).Code, Is.EqualTo(DomainErrorCode.DragonLocked), "khóa đã được lưu");
            Assert.That(b.Send(id => new BuyDecorationCommand(id, "origin", "cherry_tree")).Code, Is.EqualTo(DomainErrorCode.DecorationOwned));
            Assert.That(b.State.Player.Upgrades.FoodQualityLevel, Is.EqualTo(1));
            b.Dispose();
        }

        [Test]
        public void Duplicate_requestId_never_sells_twice()
        {
            GameState s = Rich();
            var clock = new FixedClock(s.Clock.LastProcessedAtMs + 1000);
            DispatchResult one = CommandDispatcher.Dispatch(s, new SellDragonCommand("sell-1", "c1"), clock);
            DispatchResult two = CommandDispatcher.Dispatch(one.State, new SellDragonCommand("sell-1", "c1"), clock);
            Assert.That((two.Duplicate, two.State.Player.Gold, two.State.Player.Stats.TotalSold), Is.EqualTo((true, one.State.Player.Gold, 1L)));
            long quote = SanctuaryRules.BulkSellQuoteMicro(s);
            DispatchResult bulk = CommandDispatcher.Dispatch(s, new BulkSellCommand("bulk-1", quote), clock);
            DispatchResult again = CommandDispatcher.Dispatch(bulk.State, new BulkSellCommand("bulk-1", quote), clock);
            Assert.That((again.Duplicate, again.State.Dragons.Count), Is.EqualTo((true, bulk.State.Dragons.Count)));
        }
    }
}
