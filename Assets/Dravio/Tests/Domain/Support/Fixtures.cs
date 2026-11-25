using System;
using Dravio.Domain.Catalog;
using Dravio.Domain.Core;
using NUnit.Framework;

namespace Dravio.Domain.Tests.Support
{
    /// <summary>Fixture dùng chung, tương ứng tests/unit/fixtures.ts và helper đầu gameState.test.ts.</summary>
    public static class Fixtures
    {
        /// <summary>Date.UTC(2026, 10, 24, 10, 0, 0).</summary>
        public static readonly long T0 = new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        private static int _seq;
        public static string Rid() => "req-" + (++_seq);

        public static (GameState state, FixedClock clock) NewGame(uint seed = 101)
        {
            var clock = new FixedClock(T0);
            return (InitialStateFactory.Create("save-1", T0, Rng.Create(seed)), clock);
        }

        public static DragonState Ember(GameState s) => s.Dragons[0];

        /// <summary>Gửi lệnh, bắt buộc thành công và state kết quả hợp lệ.</summary>
        public static DispatchResult Ok(GameState state, GameCommand cmd, IClock clock)
        {
            DispatchResult r = CommandDispatcher.Dispatch(state, cmd, clock);
            Assert.That(r.Ok, Is.True, "expected ok, got " + r.Error);
            ValidationResult v = GameStateValidator.Validate(r.State);
            Assert.That(v.IsValid, Is.True, "invalid state after " + cmd.Type + ": " + v);
            return r;
        }

        public static DispatchResult Fail(GameState state, GameCommand cmd, IClock clock, DomainErrorCode code)
        {
            DispatchResult r = CommandDispatcher.Dispatch(state, cmd, clock);
            Assert.That(r.Ok, Is.False, "expected " + code.ToWire());
            Assert.That(r.Error.Code, Is.EqualTo(code));
            Assert.That(r.State, Is.SameAs(state), "rejected command must not replace the state object");
            return r;
        }

        public static AdvanceTimeCommand Online() => new AdvanceTimeCommand(Rid(), AdvanceMode.Online);
        public static AdvanceTimeCommand Offline() => new AdvanceTimeCommand(Rid(), AdvanceMode.Offline);
        public static FeedCommand Feed(string habitatId = "origin_meadow", DietId diet = DietId.Fruit, string zoneId = "origin") => new FeedCommand(Rid(), diet, zoneId, habitatId);

        /// <summary>adult() trong fixtures.ts: rồng trưởng thành ở kho, genotype Rr/Pp/Bb.</summary>
        public static DragonState Adult(string uid, string speciesId, Action<DragonState> extra = null)
        {
            var g = new Genotype();
            g.Set("pigment", "R", "r"); g.Set("pattern", "P", "p"); g.Set("eye", "B", "b");
            var d = new DragonState
            {
                Uid = uid, SpeciesId = speciesId, Genotype = g, Mutation = MutationType.Normal, XpSec = SpeciesCatalog.Get(speciesId).GrowthTimeSec,
                Locked = false, Location = DragonLocation.Storage, HatchedAtMs = 0, Personality = Personality.Calm, Bond = 0, ParentIds = null,
                Generation = 1, Ancestry = Ancestry.Unknown, Origin = DragonOrigin.Pack, Activity = DragonActivity.Idle, BreedCooldownUntilMs = 0,
            };
            extra?.Invoke(d);
            return d;
        }

        /// <summary>Bản sao môi trường với một số thuộc tính đổi (spread {...env, x} trong test TS).</summary>
        public static HabitatProfile Env(HabitatProfile h, string zoneId = null, TemperatureBand? temperature = null, SalinityBand? salinity = null,
            DepthBand? depth = null, bool? perch = null, QualityValues? quality = null) =>
            new HabitatProfile(h.Id, zoneId ?? h.ZoneId, h.Realm, temperature ?? h.TemperatureBand, h.HumidityBand, salinity ?? h.SalinityBand,
                depth ?? h.DepthBand, h.AltitudeBand, perch ?? h.Perch, quality ?? h.Quality);

        public static HabitatProfile Habitat(string id)
        {
            Assert.That(HabitatCatalog.TryGet(id, out HabitatProfile h), Is.True, id);
            return h;
        }
    }
}
