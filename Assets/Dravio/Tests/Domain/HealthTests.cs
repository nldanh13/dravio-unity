using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using Dravio.Domain.Health;
using Dravio.Domain.Queries;
using Dravio.Domain.World;
using NUnit.Framework;
using static Dravio.Domain.Tests.Support.Fixtures;

namespace Dravio.Domain.Tests
{
    /// <summary>
    /// U2.6 — sức khỏe, chăm sóc, thiên tai, trú ẩn, cứu hộ (GDD 3.1 tr. 9–11, 18, 24; ADR 0003; ADR 0201 mục 4; G0 S02; ADR 0202).
    /// Thiên tai tự nhiên bị TẮT ở bản chạy thật; các test máy trạng thái dùng bộ luật tiêm vào (chance 100 % để tất định hóa mốc).
    /// </summary>
    public sealed class HealthTests
    {
        private const long Min = 60_000, Hour = 60 * Min;

        private static readonly IReadOnlyList<KeyValuePair<string, HazardId>> ZoneHazards = new[]
        {
            new KeyValuePair<string, HazardId>("origin", HazardId.Wildfire),
            new KeyValuePair<string, HazardId>("ocean", HazardId.StrongCurrent),
            new KeyValuePair<string, HazardId>("sky", HazardId.Thunderstorm),
        };

        /// <summary>Mọi lần kiểm đều sinh cảnh báo (chance = 1) — để mốc thời gian kiểm được chính xác.</summary>
        private static readonly DisasterRules Always = new DisasterRules(true, Hour, 1.0, ZoneHazards, EventSeverityPolicy.Uniform, DisasterRules.ProposedImpact);

        private static DispatchResult Run(GameState s, GameCommand c, IClock clock, DisasterRules r = null)
        {
            DispatchResult res = CommandDispatcher.Dispatch(s, c, clock, null, r);
            Assert.That(res.Ok, Is.True, "expected ok, got " + res.Error);
            ValidationResult v = GameStateValidator.Validate(res.State);
            Assert.That(v.IsValid, Is.True, "invalid state after " + c.Type + ": " + v);
            return res;
        }

        private static DomainError Rejected(GameState s, GameCommand c, IClock clock, DomainErrorCode code, DisasterRules r = null)
        {
            DispatchResult res = CommandDispatcher.Dispatch(s, c, clock, null, r);
            Assert.That(res.Ok, Is.False, "expected " + code.ToWire());
            Assert.That(res.Error.Code, Is.EqualTo(code));
            Assert.That(res.State, Is.SameAs(s));
            return res.Error;
        }

        private static GameState With(GameState s, params DragonState[] ds) { GameState c = s.DeepClone(); c.Dragons.AddRange(ds); return c; }

        private static DragonState InMeadow(string uid, string species = "sprout", Action<DragonState> extra = null) =>
            Adult(uid, species, d => { d.Location = DragonLocation.Zone("origin", "origin_meadow"); extra?.Invoke(d); });

        /// <summary>Dựng một ca đang mở hợp lệ (không từ thiên tai) cho test quy trình chăm sóc.</summary>
        private static GameState WithCase(GameState s0, string uid, CaseSeverity sev, InjurySite site, string caseId, Action<Vitals> vitals = null)
        {
            GameState s = s0.DeepClone();
            DragonState d = s.FindDragon(uid);
            bool queued = CareCenter.ActiveCount(s) >= HealthCatalog.StationSlots;
            s.HealthCases.Add(new HealthCase
            {
                CaseId = caseId, DragonId = uid, Cause = HazardId.Thunderstorm, Site = site, Severity = sev,
                Symptoms = new List<string>(HealthCatalog.Site(site).Symptoms), CreatedAtMs = s.Clock.LastProcessedAtMs,
                Status = queued ? CaseStatus.Queued : CaseStatus.Active,
            });
            d.Location = queued ? DragonLocation.Shelter("origin") : DragonLocation.Care(caseId);
            d.Activity = DragonActivity.Care;
            s.ProductionDragonIds.Remove(uid);
            vitals?.Invoke(d.Vitals);
            Assert.That(GameStateValidator.Validate(s).IsValid, Is.True, GameStateValidator.Validate(s).ToString());
            return s;
        }

        private static string Rid2() => Rid();

        // ---------------- chỉ số + validator ----------------

        [Test]
        public void New_and_hatched_dragons_start_at_the_baseline_and_vitals_are_range_checked()
        {
            var (s, _) = NewGame();
            Vitals v = Ember(s).Vitals;
            Assert.That((v.Health, v.Energy, v.Comfort, v.Mobility, v.Hydration), Is.EqualTo((100, 100, 100, 100, 100)));
            foreach (int bad in new[] { -1, 101 })
            {
                GameState c = s.DeepClone();
                c.Dragons[0].Vitals.Comfort = bad;
                Assert.That(GameStateValidator.Validate(c).Errors, Does.Contain("dragons[0].vitals: out of range"));
            }
            GameState missing = s.DeepClone();
            missing.Dragons[0].Vitals = null;
            Assert.That(GameStateValidator.Validate(missing).Errors, Does.Contain("dragons[0].vitals: out of range"), "thiếu chỉ số không được coi là khỏe");
            Assert.That(HealthChecks.BreedingShortfalls(missing, missing.Dragons[0]), Is.EqualTo(new[] { "vitals-missing" }));
        }

        // ---------------- lai (ADR 0003) ----------------

        [TestCase("health", 80, true)]
        [TestCase("health", 79, false)]
        [TestCase("mobility", 80, true)]
        [TestCase("mobility", 79, false)]
        [TestCase("comfort", 60, true)]
        [TestCase("comfort", 59, false)]
        public void Breeding_enforces_ADR_0003_health_thresholds_at_the_exact_boundary(string stat, int value, bool ok)
        {
            var (s0, clock) = NewGame();
            s0.Player.Gold = MicroGold.FromWholeGold(10_000);
            GameState s = With(s0, Adult("pa", "emberling"), Adult("pb", "sprout", d =>
            {
                if (stat == "health") d.Vitals.Health = value; else if (stat == "mobility") d.Vitals.Mobility = value; else d.Vitals.Comfort = value;
            }));
            if (ok)
            {
                Run(s, new StartBreedCommand(Rid(), "pa", "pb"), clock);
                Assert.That(Breeding.Hatchery.Preview(s, "pa", "pb", clock.NowMs(), out DomainErrorCode? e), Is.Not.Null);
                Assert.That(e, Is.Null);
            }
            else
            {
                Rejected(s, new StartBreedCommand(Rid(), "pa", "pb"), clock, DomainErrorCode.BreedVitalsBelowThreshold);
                Assert.That(Breeding.Hatchery.Preview(s, "pa", "pb", clock.NowMs(), out DomainErrorCode? e), Is.Null, "preview bị tắt, không tiêu RNG");
                Assert.That(e, Is.EqualTo(DomainErrorCode.BreedVitalsBelowThreshold));
            }
        }

        [Test]
        public void Dragons_with_an_open_case_cannot_breed_sell_travel_or_move()
        {
            var (s0, clock) = NewGame();
            s0.Player.Gold = MicroGold.FromWholeGold(10_000);
            GameState s = WithCase(With(s0, Adult("pa", "emberling"), Adult("pb", "sprout")), "pb", CaseSeverity.Mild, InjurySite.SkinScale, "case-a");
            Rejected(s, new StartBreedCommand(Rid(), "pa", "pb"), clock, DomainErrorCode.BreedBusy);
            Rejected(s, new SellDragonCommand(Rid(), "pb"), clock, DomainErrorCode.DragonBusy);
            Rejected(s, new StartExpeditionCommand(Rid(), "origin_forest", "pb"), clock, DomainErrorCode.DragonBusy);
            Rejected(s, new MoveDragonCommand(Rid(), "pb", MoveTarget.Storage), clock, DomainErrorCode.DragonUnavailable);
            Assert.That(HealthChecks.BreedingShortfalls(s, s.FindDragon("pb")), Does.Contain(HealthChecks.NoOpenCase));
            // Hộ chiếu (truy vấn) vẫn xem được.
            Assert.That(HealthQueries.Dragon(s, "pb").OpenCaseId, Is.EqualTo("case-a"));
        }

        // ---------------- thám hiểm (ADR 0201 mục 4) ----------------

        [TestCase(80, 80, 60, null)]
        [TestCase(79, 80, 60, "health>=80")]
        [TestCase(80, 79, 60, "mobility>=80")]
        [TestCase(80, 80, 59, "comfort>=60")]
        public void Expedition_enforces_ADR_0201_health_thresholds_at_the_exact_boundary(int h, int m, int c, string failed)
        {
            var (s0, clock) = NewGame();
            GameState s = With(s0, Adult("x", "emberling", d => { d.Vitals.Health = h; d.Vitals.Mobility = m; d.Vitals.Comfort = c; }));
            if (failed == null) Run(s, new StartExpeditionCommand(Rid(), "origin_forest", "x"), clock);
            else
            {
                Rejected(s, new StartExpeditionCommand(Rid(), "origin_forest", "x"), clock, DomainErrorCode.VitalsBelowThreshold);
                Assert.That(Expedition.Expeditions.DragonBlock(s, ExpeditionCatalog.Routes[0], s.FindDragon("x"), out string detail), Is.EqualTo(DomainErrorCode.VitalsBelowThreshold));
                Assert.That(detail, Is.EqualTo(failed));
            }
        }

        // ---------------- chăm sóc (GDD tr. 10–11, G0 S02) ----------------

        [Test]
        public void Care_flow_assess_plan_start_review_discharge_with_G0_durations_and_phase_boundaries()
        {
            var (s0, clock) = NewGame();
            GameState s = WithCase(With(s0, Adult("z", "zephyr")), "z", CaseSeverity.Mild, InjurySite.LimbWing, "case-w", v => v.Mobility = 40);

            Rejected(s, new StartCareCommand(Rid(), "case-w"), clock, DomainErrorCode.CaseNotAssessed);
            Rejected(s, new SetCarePlanCommand(Rid(), "case-w", "rest_protect_gradual"), clock, DomainErrorCode.CaseNotAssessed);
            Rejected(s, new AssessCaseCommand(Rid(), "nope"), clock, DomainErrorCode.UnknownCase);
            DispatchResult a = Run(s, new AssessCaseCommand(Rid(), "case-w"), clock);
            Assert.That(a.Events.OfType<HealthCaseAssessed>().Single().Symptoms, Is.EqualTo(new[] { "limited_movement", "fatigue" }));
            s = a.State;
            Rejected(s, new AssessCaseCommand(Rid(), "case-w"), clock, DomainErrorCode.CaseAlreadyAssessed);

            // Lựa chọn không phù hợp: chỉ giải thích, state không đổi, không gây tổn thương.
            DomainError why = Rejected(s, new SetCarePlanCommand(Rid(), "case-w", "early_flight_training"), clock, DomainErrorCode.CarePlanUnsuitable);
            Assert.That(why.Detail, Is.EqualTo("carePlanWhy.early_flight_training"));
            Rejected(s, new SetCarePlanCommand(Rid(), "case-w", "clean_air_monitor"), clock, DomainErrorCode.CarePlanWrongSite);
            Rejected(s, new SetCarePlanCommand(Rid(), "case-w", "magic"), clock, DomainErrorCode.UnknownCarePlan);
            s = Run(s, new SetCarePlanCommand(Rid(), "case-w", "rest_protect_gradual"), clock).State;

            DispatchResult st = Run(s, new StartCareCommand(Rid(), "case-w"), clock);
            CareStarted started = st.Events.OfType<CareStarted>().Single();
            Assert.That((started.DurationMs, started.PlanBonus), Is.EqualTo((15 * Min, true)), "nhẹ 20 phút, kế hoạch phù hợp −25 % → 15 phút");
            s = st.State;
            long t0 = clock.NowMs();
            Rejected(s, new StartCareCommand(Rid(), "case-w"), clock, DomainErrorCode.CareAlreadyStarted);
            Rejected(s, new SetCarePlanCommand(Rid(), "case-w", "rest_protect_gradual"), clock, DomainErrorCode.CareAlreadyStarted);

            // Phase 10/50/30/10 % của 900 000 ms: mốc 90 000 / 540 000 / 810 000 / 900 000.
            Assert.That(CareCenter.PhaseBoundaries(15 * Min), Is.EqualTo(new long[] { 90_000, 540_000, 810_000, 900_000 }));
            var expected = new (long at, CasePhase phase)[] { (89_999, CasePhase.Stabilize), (90_000, CasePhase.Heal), (539_999, CasePhase.Heal), (540_000, CasePhase.Rehab), (809_999, CasePhase.Rehab), (810_000, CasePhase.Return) };
            foreach (var (at, phase) in expected)
            {
                clock.Set(t0 + at);
                s = Run(s, Online(), clock).State;
                Assert.That(s.FindCase("case-w").Phase, Is.EqualTo(phase), "t+" + at);
            }
            clock.Set(t0 + 899_999);
            s = Run(s, Online(), clock).State;
            Assert.That(s.FindCase("case-w").Progress, Is.EqualTo(999));
            Rejected(s, new ReviewCaseCommand(Rid(), "case-w"), clock, DomainErrorCode.CareNotComplete);
            Rejected(s, new DischargeDragonCommand(Rid(), "case-w", MoveTarget.Storage), clock, DomainErrorCode.CaseNotReviewed);

            clock.Set(t0 + 900_000);
            DispatchResult done = Run(s, Online(), clock);
            Assert.That(done.Events.OfType<CareCompleted>().Count(), Is.EqualTo(1));
            s = done.State;
            Assert.That((s.FindDragon("z").Vitals.Mobility, s.FindDragon("z").Location.Kind), Is.EqualTo((90, LocationKind.Care)), "phục hồi tới 90, không tự xuất viện");
            s = Run(s, new ReviewCaseCommand(Rid(), "case-w"), clock).State;
            Rejected(s, new ReviewCaseCommand(Rid(), "case-w"), clock, DomainErrorCode.CaseAlreadyReviewed);
            DispatchResult dis = Run(s, new DischargeDragonCommand(Rid(), "case-w", MoveTarget.Zone("origin", "origin_meadow")), clock);
            s = dis.State;
            Assert.That((s.HealthCases.Count, s.FindDragon("z").Location.Kind, s.FindDragon("z").Activity), Is.EqualTo((0, LocationKind.Zone, DragonActivity.Idle)));
            Assert.That(s.FindDragon("z").Vitals.Health, Is.EqualTo(100), "không bao giờ giảm chỉ số đang cao hơn mốc phục hồi");
            Assert.That(s.Player.Gold, Is.GreaterThanOrEqualTo(s0.Player.Gold), "không trừ vàng; thưởng ca thường = 0 (ADR 0202 D6)");
        }

        [TestCase(CaseSeverity.Mild, false, 20)]
        [TestCase(CaseSeverity.Mild, true, 15)]
        [TestCase(CaseSeverity.Moderate, false, 80)]
        [TestCase(CaseSeverity.Moderate, true, 60)]
        public void Care_duration_follows_G0_defaults_and_stays_in_the_GDD_ranges(CaseSeverity sev, bool plan, int minutes)
        {
            Assert.That(HealthCatalog.DurationMs(sev, plan), Is.EqualTo(minutes * Min));
            Assert.That(HealthCatalog.DurationInRange(sev, HealthCatalog.DurationMs(sev, plan)), Is.True);
            var (s0, clock) = NewGame();
            GameState s = WithCase(With(s0, Adult("z", "zephyr")), "z", sev, InjurySite.LimbWing, "c1");
            s = Run(s, new AssessCaseCommand(Rid(), "c1"), clock).State;
            if (plan) s = Run(s, new SetCarePlanCommand(Rid(), "c1", "rest_protect_gradual"), clock).State;
            s = Run(s, new StartCareCommand(Rid(), "c1"), clock).State;
            Assert.That(s.FindCase("c1").DurationMs, Is.EqualTo(minutes * Min));
        }

        [Test]
        public void Care_progress_is_identical_online_offline_and_across_a_24h_cap_without_auto_discharge()
        {
            var (s0, clock) = NewGame();
            GameState s = WithCase(With(s0, Adult("z", "zephyr")), "z", CaseSeverity.Moderate, InjurySite.LimbWing, "c1");
            s = Run(s, new AssessCaseCommand(Rid(), "c1"), clock).State;
            s = Run(s, new StartCareCommand(Rid(), "c1"), clock).State;
            long t0 = clock.NowMs();

            var cOn = new FixedClock(t0); GameState on = s;
            for (int i = 0; i < 90; i++) { cOn.Advance(Min); on = Run(on, Online(), cOn).State; }
            var cOff = new FixedClock(t0 + 90 * Min);
            GameState off = Run(s, Offline(), cOff).State;
            var cCap = new FixedClock(t0 + 30 * Hour);
            GameState cap = Run(s, Offline(), cCap).State;
            foreach (GameState x in new[] { on, off, cap })
            {
                HealthCase c = x.FindCase("c1");
                Assert.That((c.Phase, c.Progress, c.Reviewed, x.FindDragon("z").Location.Kind), Is.EqualTo((CasePhase.Return, 1000, false, LocationKind.Care)));
            }
            Assert.That(off.FindDragon("z").Vitals.Health, Is.EqualTo(on.FindDragon("z").Vitals.Health));
        }

        [Test]
        public void Station_has_two_slots_extra_cases_queue_in_shelter_and_are_admitted_FIFO()
        {
            var (s0, clock) = NewGame();
            GameState s = With(s0, Adult("a", "zephyr"), Adult("b", "zephyr"), Adult("c", "zephyr"), Adult("d", "zephyr"));
            s = WithCase(s, "a", CaseSeverity.Mild, InjurySite.LimbWing, "c-a");
            s = WithCase(s, "b", CaseSeverity.Mild, InjurySite.LimbWing, "c-b");
            s = WithCase(s, "c", CaseSeverity.Mild, InjurySite.LimbWing, "c-c");
            s = WithCase(s, "d", CaseSeverity.Mild, InjurySite.LimbWing, "c-d");
            Assert.That(HealthQueries.Station(s).Queue, Is.EqualTo(new[] { "c-c", "c-d" }));
            Assert.That(s.FindDragon("c").Location.Kind, Is.EqualTo(LocationKind.Shelter));
            Rejected(s, new StartCareCommand(Rid(), "c-c"), clock, DomainErrorCode.CaseQueued);
            // Ca chờ vẫn được nhận định (tự ổn định, chờ quyết định).
            s = Run(s, new AssessCaseCommand(Rid(), "c-c"), clock).State;
            foreach (string id in new[] { "c-a" })
            {
                s = Run(s, new AssessCaseCommand(Rid(), id), clock).State;
                s = Run(s, new StartCareCommand(Rid(), id), clock).State;
            }
            clock.Advance(20 * Min);
            s = Run(s, new ReviewCaseCommand(Rid(), "c-a"), clock).State;
            DispatchResult r = Run(s, new DischargeDragonCommand(Rid(), "c-a", MoveTarget.Storage), clock);
            Assert.That(r.Events.OfType<HealthCaseAdmitted>().Select(e => e.CaseId), Is.EqualTo(new[] { "c-c" }), "FIFO");
            Assert.That((r.State.FindCase("c-c").Status, r.State.FindDragon("c").Location.CaseId), Is.EqualTo((CaseStatus.Active, "c-c")));
            Assert.That(HealthQueries.Station(r.State).Queue, Is.EqualTo(new[] { "c-d" }));
        }

        [Test]
        public void Discharge_into_a_full_zone_waits_safely_and_storage_is_always_allowed()
        {
            var (s0, clock) = NewGame();
            GameState s = With(s0, InMeadow("m1"), InMeadow("m2"), InMeadow("m3"), InMeadow("m4"), Adult("z", "sprout"));
            s = WithCase(s, "z", CaseSeverity.Mild, InjurySite.SkinScale, "c1");
            s = Run(s, new AssessCaseCommand(Rid(), "c1"), clock).State;
            s = Run(s, new StartCareCommand(Rid(), "c1"), clock).State;
            clock.Advance(20 * Min);
            s = Run(s, new ReviewCaseCommand(Rid(), "c1"), clock).State;
            Assert.That(Zones.ZoneOccupancy(s, "origin"), Is.EqualTo(5));
            Rejected(s, new DischargeDragonCommand(Rid(), "c1", MoveTarget.Zone("origin", "origin_meadow")), clock, DomainErrorCode.ZoneFull);
            Assert.That(HealthQueries.DischargeEligibility(s, "c1", MoveTarget.Zone("origin", "origin_meadow")), Is.EqualTo(DomainErrorCode.ZoneFull));
            Run(s, new DischargeDragonCommand(Rid(), "c1", MoveTarget.Storage), clock);
        }

        [Test]
        public void Duplicate_requestIds_do_not_repeat_care_steps()
        {
            var (s0, clock) = NewGame();
            GameState s = WithCase(With(s0, Adult("z", "zephyr")), "z", CaseSeverity.Mild, InjurySite.LimbWing, "c1");
            var assess = new AssessCaseCommand("same-assess", "c1");
            s = Run(s, assess, clock).State;
            DispatchResult again = CommandDispatcher.Dispatch(s, assess, clock);
            Assert.That((again.Ok, again.Duplicate, again.Events.Count), Is.EqualTo((true, true, 0)));
            Assert.That(again.State, Is.SameAs(s));
        }

        [Test]
        public void Validator_rejects_hand_edited_care_data()
        {
            var (s0, clock) = NewGame();
            GameState s = WithCase(With(s0, Adult("z", "zephyr"), Adult("y", "zephyr"), Adult("x", "zephyr")), "z", CaseSeverity.Moderate, InjurySite.LimbWing, "c1");
            s = Run(s, new AssessCaseCommand(Rid(), "c1"), clock).State;
            s = Run(s, new StartCareCommand(Rid(), "c1"), clock).State;
            void Bad(Action<GameState> edit, string error)
            {
                GameState c = s.DeepClone(); edit(c);
                Assert.That(GameStateValidator.Validate(c).Errors, Has.Some.Contains(error), error);
            }
            Bad(c => { c.FindCase("c1").DurationMs = 10 * Min; c.FindCase("c1").FinishAtMs = c.FindCase("c1").StartedAtMs + 10 * Min; }, "invalid care timing");
            Bad(c => { c.FindCase("c1").Progress = 1000; c.FindCase("c1").Phase = CasePhase.Return; }, "progress ahead of time");
            Bad(c => c.FindCase("c1").Reviewed = true, "reviewed before completion");
            Bad(c => c.ProductionDragonIds.Add("z"), "producing while in care");
            Bad(c => c.HealthCases.Add(c.FindCase("c1").Clone()), "invalid or duplicate id");
            Bad(c => { HealthCase d = c.FindCase("c1").Clone(); d.CaseId = "c2"; c.HealthCases.Add(d); }, "more than one open case");
            Bad(c => c.FindCase("c1").CarePlanId = "early_flight_training", "carePlanId: invalid");
            Bad(c => c.FindCase("c1").Status = CaseStatus.Queued, "queued dragon not sheltered");
            Bad(c =>
            {
                foreach (string u in new[] { "y", "x" })
                {
                    c.FindDragon(u).Location = DragonLocation.Care("k" + u); c.FindDragon(u).Activity = DragonActivity.Care;
                    c.HealthCases.Add(new HealthCase { CaseId = "k" + u, DragonId = u, Site = InjurySite.LimbWing, Symptoms = new List<string>(), Status = CaseStatus.Active });
                }
            }, "exceed station slots");
        }

        // ---------------- thiên tai (GDD tr. 9; ADR 0003; G0) ----------------

        [Test]
        public void Natural_disasters_are_off_in_production_until_ADR_0202_is_approved()
        {
            var (s, clock) = NewGame();
            for (int i = 0; i < 30; i++) { clock.Advance(Hour); s = Run(s, Online(), clock).State; }
            Assert.That((s.Disasters.Count, s.HealthCases.Count, s.Hazard.ForegroundMs, s.Hazard.ChecksDone), Is.EqualTo((0, 0, 0L, 0L)));
            DisasterSystemOverview o = HealthQueries.Disasters(s);
            Assert.That(o.NaturalDisastersEnabled, Is.False);
            Assert.That(o.PendingDecisions, Is.Not.Empty);
            Assert.That(DisasterRules.Production.Enabled, Is.False);
        }

        [Test]
        public void Hazard_check_fires_exactly_after_60_minutes_of_World_foreground_and_never_offline()
        {
            var (s, clock) = NewGame();
            clock.Advance(Hour - 1);
            s = Run(s, Online(), clock, Always).State;
            Assert.That(s.Disasters, Is.Empty, "59:59.999");
            clock.Advance(1);
            DispatchResult r = Run(s, Online(), clock, Always);
            HazardForecast f = r.Events.OfType<HazardForecast>().Single();
            Assert.That((f.ZoneId, f.Hazard, f.ForecastMs), Is.EqualTo(("origin", HazardId.Wildfire, 10 * Min)), "chỉ Khởi Nguyên đang mở");
            Assert.That(r.State.Disasters.Single().Phase, Is.EqualTo(DisasterPhase.Forecast));

            var (o, oc) = NewGame();
            oc.Advance(30 * Hour);
            GameState off = Run(o, Offline(), oc, Always).State;
            Assert.That((off.Disasters.Count, off.Hazard.ForegroundMs), Is.EqualTo((0, 0L)), "không sinh thiên tai offline");

            var (z, zc) = NewGame();
            z.Player.Settings.Mode = AppMode.Zen;
            zc.Advance(3 * Hour);
            Assert.That(Run(z, Online(), zc, Always).State.Disasters, Is.Empty, "Zen không tích dự báo");
        }

        [Test]
        public void Forecast_lasts_10_minutes_then_strikes_once_with_the_GDD_risk_formula_and_at_most_two_cases()
        {
            var (s0, clock) = NewGame();
            GameState s = With(s0, InMeadow("a", "sprout"), InMeadow("b", "sprout"), InMeadow("c", "zephyr"), InMeadow("d", "sprout"));
            clock.Advance(Hour);
            s = Run(s, Online(), clock, Always).State;
            DisasterState ev = s.Disasters.Single();
            clock.Advance(10 * Min - 1);
            s = Run(s, Online(), clock, Always).State;
            Assert.That(s.Disasters.Single().Phase, Is.EqualTo(DisasterPhase.Forecast));
            GameState before = s;
            clock.Advance(1);
            DispatchResult r = Run(s, Online(), clock, Always);
            DisasterStruck struck = r.Events.OfType<DisasterStruck>().Single();

            // Kỳ vọng tính độc lập theo GDD: risk = severity × 1 × (1 − resilience) × (1 − 0); roll một lần theo eventId + uid; >2 ca: risk giảm dần rồi UID.
            var expected = new List<(string uid, double risk)>();
            foreach (DragonState d in before.Dragons)
            {
                if (d.Location.Kind != LocationKind.Zone || d.Activity != DragonActivity.Idle) continue;
                double res = SpeciesCatalog.GetBiology(d.SpeciesId).ResiliencePct[HazardId.Wildfire] / 100.0;
                double risk = HealthCatalog.HazardSeverityCoefficient(ev.Severity) * (1 - res);
                Assert.That(struck.Risks.Single(x => x.Key == d.Uid).Value, Is.EqualTo(risk).Within(1e-12), d.Uid);
                if (Rng.Create(Rng.HashSeed("injury", ev.Id, d.Uid)).Next() < risk) expected.Add((d.Uid, risk));
            }
            var chosen = expected.OrderByDescending(x => x.risk).ThenBy(x => x.uid, StringComparer.Ordinal).Take(2).Select(x => x.uid).ToList();
            Assert.That(r.State.HealthCases.Where(c => c.EventId == ev.Id).Select(c => c.DragonId), Is.EqualTo(chosen));
            Assert.That(struck.CaseIds.Count, Is.LessThanOrEqualTo(HealthCatalog.MaxCasesPerEvent));
            foreach (HealthCase c in r.State.HealthCases)
                Assert.That(HealthCatalog.SitesFor(HazardId.Wildfire), Does.Contain(c.Site));
            // Không roll lại: cùng state + cùng thời điểm luôn cho cùng kết quả.
            DispatchResult again = Run(before, new AdvanceTimeCommand("other-rid", AdvanceMode.Online), clock, Always);
            Assert.That(again.State.HealthCases.Select(c => c.CaseId + c.DragonId + c.Site), Is.EqualTo(r.State.HealthCases.Select(c => c.CaseId + c.DragonId + c.Site)));
            if (chosen.Count == 0)
            {
                Assert.That(r.State.Disasters, Is.Empty, "không có ca → đóng ngay");
                Assert.That(r.State.Hazard.CooldownOf("origin"), Is.EqualTo(clock.NowMs() + HealthCatalog.CooldownMs));
            }
            else Assert.That(r.State.Disasters.Single().Phase, Is.EqualTo(DisasterPhase.Recovery));
        }

        [Test]
        public void Full_shelter_protection_gives_zero_risk_and_dragons_return_to_their_habitat_with_production()
        {
            var (s0, clock) = NewGame();
            GameState s = With(s0, InMeadow("a", "sprout"));
            string ember = Ember(s).Uid;
            Rejected(s, new MoveToShelterCommand(Rid(), "a"), clock, DomainErrorCode.NoDisasterWarning, Always);
            clock.Advance(Hour);
            s = Run(s, Online(), clock, Always).State;
            DispatchResult sh = Run(s, new MoveToShelterCommand(Rid(), ember), clock, Always);
            Assert.That(sh.Events.OfType<ProductionChanged>().Single().Enabled, Is.False);
            s = Run(sh.State, new MoveToShelterCommand(Rid(), "a"), clock, Always).State;
            Rejected(s, new MoveToShelterCommand(Rid(), "a"), clock, DomainErrorCode.DragonUnavailable, Always);
            long gold = s.Player.Gold.Micro;
            clock.Advance(10 * Min);
            DispatchResult r = Run(s, Online(), clock, Always);
            DisasterStruck struck = r.Events.OfType<DisasterStruck>().Single();
            Assert.That(struck.Risks.All(x => x.Value == 0), Is.True);
            Assert.That(struck.CaseIds, Is.Empty);
            Assert.That(r.State.Player.Gold.Micro, Is.EqualTo(gold), "trong nơi trú không sản xuất");
            Assert.That(r.State.FindDragon(ember).Location.HabitatId, Is.EqualTo("origin_meadow"));
            Assert.That(r.State.ProductionDragonIds, Does.Contain(ember), "trả lại ngân sách sản xuất");
            Assert.That(r.State.Disasters, Is.Empty);
            Assert.That(r.Events.OfType<DisasterClosed>().Single().CooldownUntilMs, Is.EqualTo(clock.NowMs() + 24 * Hour));
        }

        [Test]
        public void Offline_and_Zen_during_a_forecast_auto_shelter_and_never_injure_and_cooldown_blocks_new_events()
        {
            var (s0, clock) = NewGame();
            GameState s = With(s0, InMeadow("a", "sprout"), InMeadow("b", "sprout"));
            clock.Advance(Hour);
            s = Run(s, Online(), clock, Always).State;
            clock.Advance(5 * Min);
            s = Run(s, Online(), clock, Always).State;
            clock.Advance(8 * Hour);
            DispatchResult off = Run(s, Offline(), clock, Always);
            Assert.That(off.Events.OfType<DragonSheltered>().All(e => e.Auto), Is.True);
            Assert.That(off.Events.OfType<DragonSheltered>().Count(), Is.EqualTo(3));
            Assert.That(off.State.Disasters.Single().ForecastProgressMs, Is.EqualTo(5 * Min), "offline không tích dự báo");
            Assert.That(off.State.Hazard.ForegroundMs, Is.EqualTo(Hour + 5 * Min));
            s = off.State;
            clock.Advance(5 * Min);
            DispatchResult strike = Run(s, Online(), clock, Always);
            Assert.That(strike.State.HealthCases, Is.Empty, "đã trú đủ → risk 0");
            Assert.That(strike.State.Disasters, Is.Empty);
            s = strike.State;
            // Cooldown 24 h theo đồng hồ; các lần kiểm (60 phút foreground) trong cooldown bỏ qua.
            for (int i = 0; i < 5; i++) { clock.Advance(Hour); s = Run(s, Online(), clock, Always).State; }
            Assert.That(s.Disasters, Is.Empty);
            Assert.That(s.Hazard.ChecksDone, Is.GreaterThanOrEqualTo(5));
        }

        [Test]
        public void One_big_online_advance_equals_one_second_ticks_including_gold_cases_and_hazard_clock()
        {
            var (s0, _) = NewGame();
            GameState start = With(s0, InMeadow("a", "sprout"), InMeadow("b", "sprout"), InMeadow("c", "zephyr"), InMeadow("d", "sprout"));
            long t0 = start.Clock.LastProcessedAtMs;
            var big = new FixedClock(t0 + 75 * Min);
            GameState a = Run(start, Online(), big, Always).State;
            var tick = new FixedClock(t0);
            GameState b = start;
            for (int i = 0; i < 75 * 60; i++)
            {
                tick.Advance(1000);
                b = CommandDispatcher.Dispatch(b, Online(), tick, null, Always).State;
            }
            Assert.That(b.Player.Gold.Micro, Is.EqualTo(a.Player.Gold.Micro));
            Assert.That(b.Player.GoldRemainder, Is.EqualTo(a.Player.GoldRemainder));
            Assert.That((b.Hazard.ForegroundMs, b.Hazard.ChecksDone), Is.EqualTo((a.Hazard.ForegroundMs, a.Hazard.ChecksDone)));
            Assert.That(b.HealthCases.Select(c => c.CaseId + ":" + c.DragonId + ":" + c.Site), Is.EqualTo(a.HealthCases.Select(c => c.CaseId + ":" + c.DragonId + ":" + c.Site)));
            Assert.That(b.Disasters.Select(d => d.Id + d.Phase), Is.EqualTo(a.Disasters.Select(d => d.Id + d.Phase)));
            Assert.That(b.Dragons.Select(d => d.Uid + d.Location + d.Vitals.Health), Is.EqualTo(a.Dragons.Select(d => d.Uid + d.Location + d.Vitals.Health)));
        }

        [Test]
        public void At_most_one_open_event_per_world_and_Cozy_events_are_mild()
        {
            var (s, clock) = NewGame();
            s.Player.Settings.Difficulty = Difficulty.Cozy;
            clock.Advance(Hour);
            s = Run(s, Online(), clock, Always).State;
            Assert.That(s.Disasters.Single().Severity, Is.EqualTo(CaseSeverity.Mild));
            // Bỏ qua mốc kiểm thứ hai bằng cách giữ sự kiện mở: đặt lại dự báo về 0 không được phép — thay vào đó kiểm ngay mốc tiếp theo.
            GameState held = s.DeepClone();
            held.Hazard.ForegroundMs = 2 * Hour - 1; held.Disasters[0].ForecastProgressMs = 0;
            clock.Advance(1);
            GameState after = Run(held, Online(), clock, Always).State;
            Assert.That(after.Disasters.Count, Is.EqualTo(1), "thế giới đang có sự kiện mở → không tạo thêm");
        }

        [Test]
        public void Undefined_ecosystem_severity_policy_creates_no_event_even_when_enabled()
        {
            var rules = new DisasterRules(true, Hour, 1.0, ZoneHazards, EventSeverityPolicy.Undefined, null);
            var (s, clock) = NewGame();
            clock.Advance(3 * Hour);
            Assert.That(Run(s, Online(), clock, rules).State.Disasters, Is.Empty);
        }

        [Test]
        public void Health_queries_describe_vitals_cases_and_the_disaster_system()
        {
            var (s0, clock) = NewGame();
            GameState s = WithCase(With(s0, Adult("z", "zephyr", d => d.Vitals.Comfort = 55)), "z", CaseSeverity.Mild, InjurySite.LimbWing, "c1");
            DragonHealthOverview z = HealthQueries.Dragon(s, "z");
            Assert.That(z.BreedingShortfalls, Is.EqualTo(new[] { "comfort>=60", "no-open-care-case" }));
            Assert.That(HealthQueries.Dragon(s, Ember(s).Uid).HabitatScore, Is.Not.Null);
            CaseOverview c = HealthQueries.Cases(s).Single();
            Assert.That((c.NextStep, c.Symptoms.Count), Is.EqualTo(("assessCase", 0)), "triệu chứng chỉ hiện sau khi nhận định");
            Assert.That(HealthQueries.CarePlans(s, "c1").Select(p => p.PlanId + ":" + p.Suitable), Is.EqualTo(new[] { "rest_protect_gradual:True", "early_flight_training:False" }));
            Assert.That(HealthQueries.Disasters(s, Always).Zones.Select(x => x.ZoneId), Is.EqualTo(new[] { "origin", "ocean", "sky" }));
        }
    }
}
