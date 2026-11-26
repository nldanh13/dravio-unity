using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;
using Dravio.Domain.World;
using Dravio.Domain.Tests.Support;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.10 luật thật đã duyệt (ADR 0204): E1 tối đa 5 gói Starter / ngày UTC, E2 Starter 150 G (gói hướng dẫn giữ 100 G),
    /// E5 dự án Sanctuary (vàng + gỗ, hạt), R11 nhật ký đêm và lệnh quan sát ghi vào sổ nghiên cứu của save.
    /// </summary>
    public sealed class U210LiveEconomyTests
    {
        private const long Min = 60_000, Hour = 60 * Min;
        private static readonly long Day0 = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static GameState Rich(long gold = 1_000_000, long wood = 0, long seed = 0)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            s.Player.Gold = MicroGold.FromWholeGold(gold);
            if (wood > 0) s.Player.Inventory["wood"] = wood;
            if (seed > 0) s.Player.Inventory["seed"] = seed;
            return s;
        }

        private static GameState WithDragonsAt(long now, params DragonState[] ds)
        {
            (GameState s, _) = NewGame();
            s = s.DeepClone();
            s.Dragons.Clear();
            s.ProductionDragonIds.Clear();
            s.Dragons.AddRange(ds);
            foreach (ZoneState z in s.Zones) if (z.Id == "origin") z.UnlockedHabitatIds = Zones.HabitatsOfZone("origin");
            foreach (string disc in ProgressCatalog.DiscoveryIds) if (!s.Discoveries.Contains(disc)) s.Discoveries.Add(disc);
            s.Discoveries.Sort(StringComparer.Ordinal);
            Codex.Observe(s, null);
            s.Clock = new ClockState(now, now);
            return s;
        }

        private static DragonState At(string uid, string species, string habitatId) =>
            Adult(uid, species, d => d.Location = DragonLocation.Zone(HabitatCatalog.ZoneOf(habitatId), habitatId));

        // ------------------------------------------------------------------ E1 + E2

        [Test]
        public void Starter_costs_150_gold_while_the_tutorial_pack_keeps_100()
        {
            Assert.That(PackCatalog.Get("starter").PriceG, Is.EqualTo(150));
            Assert.That(ProgressCatalog.TutorialPackPriceG, Is.EqualTo(100));
            (GameState s, FixedClock clock) = NewGame();
            DispatchResult tut = Ok(s, new BuyTutorialPackCommand(Rid()), clock);
            Assert.That(tut.Events.OfType<PackBought>().Single().PriceGoldMicro, Is.EqualTo(MicroGold.FromWholeGold(100).Micro));
            Assert.That(PackDailyLimit.StarterBoughtToday(tut.State, clock.NowMs()), Is.Zero, "gói hướng dẫn không tính vào giới hạn");

            GameState r = Rich();
            DispatchResult buy = Ok(r, new BuyPackCommand(Rid(), "starter"), new FixedClock(T0));
            Assert.That(buy.Events.OfType<PackBought>().Single().PriceGoldMicro, Is.EqualTo(MicroGold.FromWholeGold(150).Micro));
            Assert.That(r.Player.Gold.Micro - buy.State.Player.Gold.Micro, Is.EqualTo(MicroGold.FromWholeGold(150).Micro));
        }

        [Test]
        public void At_most_five_starter_packs_per_UTC_day_and_the_counter_resets_next_day()
        {
            GameState s = Rich();
            var clock = new FixedClock(Day0 + 23 * Hour);
            for (int i = 0; i < PackCatalog.StarterDailyLimit; i++)
            {
                s = Ok(s, new BuyPackCommand(Rid(), "starter"), clock).State;
                s = Ok(s, new ClaimPackCommand(Rid(), MoveTarget.Storage), clock).State;
            }
            Assert.That(PackDailyLimit.StarterLeftToday(s, clock.NowMs()), Is.Zero);
            Fail(s, new BuyPackCommand(Rid(), "starter"), clock, DomainErrorCode.PackDailyLimit);
            Assert.That(new DomainError(DomainErrorCode.PackDailyLimit).MessageKey, Is.EqualTo("commandError.pack-daily-limit"));
            Ok(s, new BuyPackCommand(Rid(), "elemental"), clock);   // gói khác không bị giới hạn

            clock.Set(Day0 + 24 * Hour);
            Assert.That(PackDailyLimit.StarterLeftToday(s, clock.NowMs()), Is.EqualTo(PackCatalog.StarterDailyLimit));
            GameState next = Ok(s, new BuyPackCommand(Rid(), "starter"), clock).State;
            Assert.That((next.PackDaily.Day, next.PackDaily.StarterBought), Is.EqualTo(("2026-12-02", 1)));
        }

        [Test]
        public void Rejected_or_duplicate_buys_do_not_count_toward_the_limit()
        {
            GameState s = Rich(gold: 100);
            var clock = new FixedClock(T0);
            Fail(s, new BuyPackCommand(Rid(), "starter"), clock, DomainErrorCode.NotEnoughGold);
            Assert.That(PackDailyLimit.StarterBoughtToday(s, clock.NowMs()), Is.Zero);
            GameState r = Rich();
            var cmd = new BuyPackCommand(Rid(), "starter");
            GameState once = Ok(r, cmd, clock).State;
            DispatchResult again = CommandDispatcher.Dispatch(once, cmd, clock);
            Assert.That(again.Duplicate, Is.True);
            Assert.That(again.State.PackDaily.StarterBought, Is.EqualTo(1));
        }

        // ------------------------------------------------------------------ E5

        [Test]
        public void Project_costs_grow_by_a_quarter_and_need_wood_and_seed_from_the_third()
        {
            Assert.That(Enumerable.Range(0, 6).Select(n => SanctuaryProjectCatalog.CostG(n)), Is.EqualTo(new long?[] { 20_000, 25_000, 31_250, 39_062, 48_828, 61_035 }));
            Assert.That(Enumerable.Range(0, 11).Select(SanctuaryProjectCatalog.MaterialEach), Is.EqualTo(new[] { 0, 0, 1, 1, 1, 2, 2, 2, 2, 2, 3 }));
            Assert.That(SanctuaryProjectCatalog.CostG(SanctuaryProjectCatalog.Max - 1), Is.GreaterThan(0));
            Assert.That(SanctuaryProjectCatalog.CostG(SanctuaryProjectCatalog.Max), Is.Null);
            Assert.That(MicroGold.FromWholeGold(SanctuaryProjectCatalog.CostG(SanctuaryProjectCatalog.Max - 1).Value).Micro, Is.GreaterThan(0), "giá cuối vẫn nằm trong long microG");
        }

        [Test]
        public void Building_projects_spends_gold_then_materials_and_gives_nothing_back()
        {
            GameState s = Rich(gold: 200_000, wood: 1, seed: 1);
            var clock = new FixedClock(T0);
            DispatchResult a = Ok(s, new BuildSanctuaryProjectCommand(Rid()), clock);
            SanctuaryProjectBuilt ev = a.Events.OfType<SanctuaryProjectBuilt>().Single();
            Assert.That((ev.Index, ev.CostGoldMicro, ev.MaterialEach), Is.EqualTo((0, MicroGold.FromWholeGold(20_000).Micro, 0)));
            Assert.That(s.Player.Gold.Micro - a.State.Player.Gold.Micro, Is.EqualTo(MicroGold.FromWholeGold(20_000).Micro));
            Assert.That(a.State.Player.TotalGoldEarned, Is.EqualTo(s.Player.TotalGoldEarned), "dự án không cho vàng");
            GameState stripped = a.State.DeepClone();
            stripped.Player.Gold = s.Player.Gold; stripped.SanctuaryProjectsBuilt = 0; stripped.Revision = s.Revision; stripped.ProcessedRequestIds = s.ProcessedRequestIds;
            Assert.That(MiniJson.Diff(StateJson.ToJson(s), StateJson.ToJson(stripped)), Is.Null, "chỉ thẩm mỹ: ngoài vàng và bộ đếm, không đổi gì (không tăng sản xuất)");
            GameState b = Ok(a.State, new BuildSanctuaryProjectCommand(Rid()), clock).State;
            GameState c = Ok(b, new BuildSanctuaryProjectCommand(Rid()), clock).State;
            Assert.That((c.SanctuaryProjectsBuilt, c.Player.Inventory["wood"], c.Player.Inventory["seed"]), Is.EqualTo((3, 0L, 0L)));
            Fail(c, new BuildSanctuaryProjectCommand(Rid()), clock, DomainErrorCode.NotEnoughMaterials);

            GameState poor = Rich(gold: 19_999);
            Fail(poor, new BuildSanctuaryProjectCommand(Rid()), clock, DomainErrorCode.NotEnoughGold);
            GameState maxed = Rich(gold: 1);
            maxed.SanctuaryProjectsBuilt = SanctuaryProjectCatalog.Max;
            Fail(maxed, new BuildSanctuaryProjectCommand(Rid()), clock, DomainErrorCode.ProjectsMaxed);
        }

        [Test]
        public void Economy_query_reports_packs_left_and_the_next_project()
        {
            GameState s = Rich(gold: 30_000);
            s.SanctuaryProjectsBuilt = 2;
            SanctuaryEconomyDto d = ProgressionQueries.SanctuaryEconomy(s, T0);
            Assert.That((d.StarterPacksLeftToday, d.StarterDailyLimit, d.StarterPriceGoldMicro), Is.EqualTo((5, 5, MicroGold.FromWholeGold(150).Micro)));
            Assert.That((d.ProjectsBuilt, d.NextProjectCostGoldMicro, d.NextProjectMaterialEach, d.NextProjectBlockedBy),
                Is.EqualTo((2, (long?)MicroGold.FromWholeGold(31_250).Micro, 1, "not-enough-gold")));
            s.Player.Gold = MicroGold.FromWholeGold(40_000);
            Assert.That(ProgressionQueries.SanctuaryEconomy(s, T0).NextProjectBlockedBy, Is.EqualTo("not-enough-materials"));
            s.Player.Inventory["wood"] = 1; s.Player.Inventory["seed"] = 1;
            Assert.That(ProgressionQueries.SanctuaryEconomy(s, T0).NextProjectBlockedBy, Is.Null);
        }

        // ------------------------------------------------------------------ R9 + R11

        [Test]
        public void Observe_command_records_into_the_save_with_cooldown_and_never_pays()
        {
            long t = Day0 + 12 * Hour;
            GameState s = WithDragonsAt(t, At("a", "emberling", "origin_meadow"));
            var clock = new FixedClock(t);
            DispatchResult r = Ok(s, new ObserveDragonCommand(Rid(), "a", DayPhase.Day), clock);
            DragonObserved ev = r.Events.OfType<DragonObserved>().Single();
            Assert.That((ev.DragonUid, ev.SpeciesId, ev.Findings.Count > 0), Is.EqualTo(("a", "emberling", true)));
            Assert.That(r.State.Research.SpeciesRecord("emberling").Phases, Does.Contain(DayPhase.Day));
            Assert.That(r.State.Player.Gold.Micro - s.Player.Gold.Micro, Is.EqualTo(r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro)), "quan sát không cho vàng (chỉ thưởng thành tựu sẵn có)");
            Assert.That(r.State.FindDragon("a").Bond, Is.EqualTo(s.FindDragon("a").Bond));
            Assert.That(s.Research.IsEmpty, Is.True, "state gốc không đổi");
            Fail(r.State, new ObserveDragonCommand(Rid(), "a", DayPhase.Day), clock, DomainErrorCode.ObservationCooldown);
            Fail(r.State, new ObserveDragonCommand(Rid(), "ghost", DayPhase.Day), clock, DomainErrorCode.UnknownDragon);
            clock.Advance(31_000);
            Ok(r.State, new ObserveDragonCommand(Rid(), "a", DayPhase.Day), clock);
        }

        [Test]
        public void Selling_an_observed_dragon_prunes_its_record_so_the_save_stays_valid()
        {
            long t = Day0 + 12 * Hour;
            GameState s = WithDragonsAt(t, At("a", "emberling", "origin_meadow"), At("b", "sprout", "origin_meadow"));
            var clock = new FixedClock(t);
            s = Ok(s, new ObserveDragonCommand(Rid(), "a", DayPhase.Day), clock).State;
            Assert.That(s.Research.IndividualRecord("a"), Is.Not.Null);
            s = Ok(s, new MoveDragonCommand(Rid(), "a", MoveTarget.Storage), clock).State;
            s = Ok(s, new SellDragonCommand(Rid(), "a"), clock).State;
            Assert.That(s.Research.IndividualRecord("a"), Is.Null);
            Assert.That(ResearchSchemaDraft.Validate(s.Research.ToSnapshot(), s), Is.Empty);
        }

        [Test]
        public void Night_journal_is_written_once_when_an_offline_night_is_settled()
        {
            long evening = Day0 - 4 * Hour, morning = Day0 + 9 * Hour;
            GameState s = WithDragonsAt(evening, At("a", "emberling", "origin_meadow"), At("b", "sprout", "origin_meadow"));
            var clock = new FixedClock(morning);
            DispatchResult r = Ok(s, Offline(), clock);
            NightJournalWritten nj = r.Events.OfType<NightJournalWritten>().Single();
            Assert.That(nj.SampleAtMs, Is.EqualTo(Day0 + 2 * Hour));
            Assert.That(nj.Findings.Count, Is.EqualTo(nj.SpeciesIds.Count));
            Assert.That(nj.Findings.Count, Is.InRange(1, 2), "tối đa một phát hiện mỗi loài");
            foreach (string sp in nj.SpeciesIds) Assert.That(r.State.Research.SpeciesRecord(sp), Is.Not.Null);
            Assert.That(r.State.Player.Gold.Micro - s.Player.Gold.Micro, Is.EqualTo(r.Events.OfType<TimeAdvanced>().Sum(e => e.GoldMicro) + r.Events.OfType<AchievementUnlocked>().Sum(e => e.RewardGoldMicro)), "nhật ký đêm không cho vàng");

            // Online, hay offline lần nữa không qua 02:00: không ghi thêm.
            clock.Advance(Hour);
            Assert.That(Ok(r.State, Offline(), clock).Events.OfType<NightJournalWritten>(), Is.Empty);
            Assert.That(Ok(r.State, Online(), clock).Events.OfType<NightJournalWritten>(), Is.Empty);
        }

        [Test]
        public void Production_rule_flags_show_recording_on_and_everything_unapproved_still_off()
        {
            RuleFlagsDto f = ProgressionQueries.Rules();
            // U2.11: hoàn thành nhiệm vụ chạy thật (ADR 0206).
            Assert.That((f.ResearchRecordingEnabled, f.NightJournalEnabled, f.QuestCompletionEnabled), Is.EqualTo((true, true, true)));
            Assert.That(f.ResearchPacingEnabled || f.SocialGameplayEffectsEnabled || f.ComfortDriftEnabled || f.NaturalDisastersEnabled || f.EnergyDrainEnabled, Is.False);
        }
    }
}
