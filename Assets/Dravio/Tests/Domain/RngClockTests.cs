using System.Collections.Generic;
using Dravio.Domain.Core;
using NUnit.Framework;

namespace Dravio.Domain.Tests
{
    /// <summary>Port của tests/unit/rng-clock.test.ts.</summary>
    public sealed class RngClockTests
    {
        [Test]
        public void Rng_is_deterministic_for_the_same_seed()
        {
            IRng a = Rng.Create(42), b = Rng.Create(42);
            for (int i = 0; i < 100; i++) Assert.That(a.Next(), Is.EqualTo(b.Next()));
        }

        [Test]
        public void Rng_stays_within_0_inclusive_1_exclusive()
        {
            IRng r = Rng.Create(Rng.HashSeed("event-1", "dragon-1"));
            for (int i = 0; i < 10_000; i++)
            {
                double x = r.Next();
                Assert.That(x, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
            }
        }

        [Test]
        public void PickWeighted_never_picks_zero_weight_entries()
        {
            IRng r = Rng.Create(7);
            var entries = new List<KeyValuePair<string, double>> { new KeyValuePair<string, double>("a", 0), new KeyValuePair<string, double>("b", 1), new KeyValuePair<string, double>("c", 0) };
            for (int i = 0; i < 2_000; i++) Assert.That(r.PickWeighted(entries), Is.EqualTo("b"));
        }

        [Test]
        public void PickWeighted_rejects_non_positive_total_and_RandomInt_rejects_bad_bounds()
        {
            IRng r = Rng.Create(1);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => r.PickWeighted(new List<KeyValuePair<string, double>> { new KeyValuePair<string, double>("a", 0) }));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => r.RandomInt(0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => r.PickUniform(new string[0]));
        }

        [Test]
        public void HashSeed_separates_seed_parts()
        {
            Assert.That(Rng.HashSeed("ab", "c"), Is.Not.EqualTo(Rng.HashSeed("a", "bc")));
        }

        [Test]
        public void Rng_state_can_be_snapshotted_and_resumed()
        {
            var a = new Mulberry32(123);
            a.Next(); a.Next();
            var b = new Mulberry32(a.State);
            Assert.That(b.Next(), Is.EqualTo(a.Next()));
        }

        private static readonly ClockState Base = new ClockState(1_000, 1_000);

        [Test]
        public void Clock_handles_0_60s_and_61s()
        {
            Assert.That(TimeRules.ComputeElapsed(Base, 1_000).ElapsedMs, Is.EqualTo(0));
            Assert.That(TimeRules.ComputeElapsed(Base, 61_000).ElapsedMs, Is.EqualTo(60_000));
            Assert.That(TimeRules.ComputeElapsed(Base, 62_000).ElapsedMs, Is.EqualTo(61_000));
        }

        [Test]
        public void Clock_caps_offline_time_at_24h()
        {
            Assert.That(TimeRules.ComputeElapsed(Base, 1_000 + 3 * TimeRules.OfflineCapMs).ElapsedMs, Is.EqualTo(TimeRules.OfflineCapMs));
        }

        [Test]
        public void Clock_going_backwards_gives_no_time_and_keeps_high_water_mark()
        {
            ElapsedResult r = TimeRules.ComputeElapsed(new ClockState(10_000, 10_000), 5_000);
            Assert.That(r.ElapsedMs, Is.EqualTo(0));
            Assert.That(r.Next.HighWaterAtMs, Is.EqualTo(10_000));
            // Sau đó đồng hồ trở lại: chỉ tính phần vượt mốc cũ.
            Assert.That(TimeRules.ComputeElapsed(r.Next, 12_000).ElapsedMs, Is.EqualTo(2_000));
        }

        [Test]
        public void FixedClock_set_and_advance()
        {
            var c = new FixedClock(5);
            c.Advance(10);
            Assert.That(c.NowMs(), Is.EqualTo(15));
            c.Set(-3);
            Assert.That(c.NowMs(), Is.EqualTo(-3));
        }

        [Test]
        public void JsCompat_round_matches_Math_round_half_up()
        {
            Assert.That(JsCompat.Round(2.5), Is.EqualTo(3));
            Assert.That(JsCompat.Round(-2.5), Is.EqualTo(-2));
            Assert.That(JsCompat.Round(0.49999999999999994), Is.EqualTo(0));
            Assert.That(JsCompat.Round(1000.4999), Is.EqualTo(1000));
        }

        [Test]
        public void UtcDay_formats_like_toISOString_slice()
        {
            Assert.That(JsCompat.UtcDay(Support.Fixtures.T0), Is.EqualTo("2026-11-24"));
            Assert.That(JsCompat.UtcDay(0), Is.EqualTo("1970-01-01"));
            Assert.That(JsCompat.IsUtcDayPattern("2026-11-24"), Is.True);
            Assert.That(JsCompat.IsUtcDayPattern("2026-1-24"), Is.False);
            Assert.That(JsCompat.IsUtcDayPattern("２０２６-11-24"), Is.False);
        }
    }
}
