using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Character.Integration;
using NUnit.Framework;
using UnityEngine;

namespace Dravio.Character.Tests
{
    /// <summary>
    /// U1-CHAR.7 motion contract: capabilities from clips (species x stage), medium transitions (water, air, perch,
    /// underground) with translation lock, chaining, honest fallbacks, readiness, root offsets, hidden underground,
    /// growth keeping the medium, motion shaping maths, and no per-frame allocation. Pure logic (Unity + dotnet).
    /// </summary>
    public sealed class MotionTests
    {
        private static readonly string[] Common = { "Idle", "Eat", "Sleep", "Happy", "Rest", "Wake", "LookAround", "Curious", "Greet" };
        private static string[] With(params string[] more) { return Common.Concat(more).ToArray(); }
        // clip sets as build_archetypes.py makes them (dragon-archetypes.json)
        private static readonly string[] Droplet = With("Walk", "Run", "Swim", "Drink", "Stretch", "Bask", "SwimIdle", "SwimRest", "SwimSurface", "WaterEnter", "WaterExit");
        private static readonly string[] Frostfin = With("Swim", "SwimIdle", "SwimRest");
        private static readonly string[] Zephyr = With("Walk", "Run", "Fly", "Drink", "Stretch", "Bask", "Perch", "TakeOff", "Glide", "Land");
        private static readonly string[] Sparkwing = With("Walk", "Run", "Fly", "Drink", "Stretch", "Bask", "TakeOff", "Glide", "Land");
        private static readonly string[] Sprout = With("Walk", "Run", "Burrow", "Drink", "Stretch", "Bask", "DigPrep", "BurrowEnter", "BurrowEmerge");
        private static readonly string[] Emberling = With("Walk", "Run", "Drink", "Stretch", "Bask");

        private static DragonBehaviorPlanner P(string[] clips) { return new DragonBehaviorPlanner(new DragonAnimationMap(clips), c => 1f, null, 9); }

        private static List<DragonBehaviorOutput> Run(DragonBehaviorPlanner p, DragonBehaviorIntent i, float seconds, float dt = 0.1f)
        {
            var l = new List<DragonBehaviorOutput>();
            for (float t = 0f; t < seconds - 1e-4f; t += dt) l.Add(p.Update(dt, i, 1f, 0f));
            return l;
        }

        [Test]
        public void Capabilities_FollowTheModelsClips()
        {
            var d = DragonMotionCapabilities.From(new DragonAnimationMap(Droplet));
            Assert.IsTrue(d.ground && d.water && d.waterEnter && d.waterExit); Assert.IsFalse(d.air || d.perch || d.underground);
            var f = DragonMotionCapabilities.From(new DragonAnimationMap(Frostfin));
            Assert.IsTrue(f.water); Assert.IsFalse(f.ground, "swims but does not walk: lives in the water");
            var z = DragonMotionCapabilities.From(new DragonAnimationMap(Zephyr));
            Assert.IsTrue(z.air && z.perch && z.takeOff && z.land);
            Assert.IsFalse(DragonMotionCapabilities.From(new DragonAnimationMap(Sparkwing)).perch, "no perching feet stated: no Perch");
            Assert.IsTrue(DragonMotionCapabilities.From(new DragonAnimationMap(Sprout)).underground);
            var e = DragonMotionCapabilities.From(new DragonAnimationMap(Emberling));
            Assert.IsTrue(e.ground); Assert.IsFalse(e.water || e.air || e.underground);
            Assert.AreEqual(3, (int)DragonLocomotion.Swim); Assert.AreEqual(8, (int)DragonLocomotion.Bask); Assert.AreEqual(7, (int)DragonInteraction.Curious);
        }

        [Test]
        public void WaterEntry_LocksTranslationUntilDone_ThenWaterStatesNeedNoTransition()
        {
            var p = P(Droplet);
            Assert.AreEqual(DragonMotionMode.Ground, p.Mode);
            var enter = Run(p, DragonBehaviorIntent.Swim, 1.5f);
            Assert.AreEqual(DragonInteraction.WaterEnter, enter[0].trigger);
            Assert.IsTrue(enter.Take(10).All(o => !o.canTranslate && o.transition == DragonInteraction.WaterEnter && o.mode == DragonMotionMode.Ground));
            Assert.AreEqual(DragonLocomotion.Swim, enter[0].locomotion, "the Animator returns to Swim when the transition ends");
            int done = enter.FindIndex(o => o.completed == DragonInteraction.WaterEnter);
            Assert.AreEqual(10, done, "1 s clip at 0.1 s steps");
            Assert.IsTrue(enter.Skip(done).All(o => o.canTranslate && o.mode == DragonMotionMode.Water));
            Assert.IsTrue(Run(p, DragonBehaviorIntent.SwimIdle, 1f).All(o => o.trigger == DragonInteraction.None && o.locomotion == DragonLocomotion.SwimIdle));
            var exit = Run(p, DragonBehaviorIntent.Idle, 1.5f);
            Assert.AreEqual(DragonInteraction.WaterExit, exit[0].trigger, "Idle is a ground intent: leave the water first");
            Assert.AreEqual(DragonMotionMode.Ground, p.Mode);
        }

        [Test]
        public void Flight_TakeOffGlidePerchLand_AndNoPerchWithoutPerchingFeet()
        {
            var z = P(Zephyr);
            var up = Run(z, DragonBehaviorIntent.Fly, 1.5f);
            Assert.AreEqual(DragonInteraction.TakeOff, up[0].trigger);
            Assert.AreEqual(DragonInteraction.TakeOff, up.First(o => o.completed != DragonInteraction.None).completed);
            Assert.AreEqual(DragonMotionMode.Air, z.Mode);
            Assert.IsTrue(Run(z, DragonBehaviorIntent.Glide, 1f).All(o => o.locomotion == DragonLocomotion.Glide && o.trigger == DragonInteraction.None));
            var perch = Run(z, DragonBehaviorIntent.Perch, 1.5f);
            Assert.AreEqual(DragonInteraction.Land, perch[0].trigger);
            Assert.AreEqual(DragonMotionMode.Perched, z.Mode);
            Assert.AreEqual(DragonLocomotion.Perch, perch.Last().locomotion);
            Assert.AreEqual(DragonInteraction.TakeOff, Run(z, DragonBehaviorIntent.Fly, 0.2f)[0].trigger, "perch -> air: TakeOff");

            var s = P(Sparkwing);
            var o2 = s.Update(0.1f, DragonBehaviorIntent.Perch, 0f, 0f);
            Assert.AreEqual(DragonMotionMode.Ground, s.Mode, "cannot perch: stays where it is");
            Assert.IsTrue(o2.substituted);
            Assert.AreEqual(DragonMotionReadiness.Unsupported, s.ReadinessFor(DragonMotionMode.Perched));
            Assert.AreEqual(DragonMotionReadiness.Ready, s.ReadinessFor(DragonMotionMode.Air));
        }

        [Test]
        public void Burrow_DigsInChained_HiddenUnderground_VisibleWhileEmerging()
        {
            var p = P(Sprout);
            var dig = Run(p, DragonBehaviorIntent.Underground, 3f);
            Assert.AreEqual(DragonInteraction.DigPrep, dig[0].trigger);
            int next = dig.FindIndex(o => o.trigger == DragonInteraction.BurrowEnter);
            Assert.AreEqual(dig.FindIndex(o => o.completed == DragonInteraction.DigPrep), next, "BurrowEnter starts on the tick DigPrep ends: no gap");
            Assert.IsTrue(dig.Take(next + 10).All(o => !o.canTranslate && o.visible), "visible and locked while digging in");
            Assert.AreEqual(DragonMotionMode.Underground, p.Mode);
            Assert.IsFalse(dig.Last().visible, "hidden underground (the Sanctuary shows its own soil VFX)");
            Assert.IsTrue(dig.Last().canTranslate, "a hidden dragon may be moved by the Sanctuary");
            var up = Run(p, DragonBehaviorIntent.Idle, 1.5f);
            Assert.AreEqual(DragonInteraction.BurrowEmerge, up[0].trigger);
            Assert.IsTrue(up[0].visible, "visible from the first frame of Emerge");
            Assert.AreEqual(DragonMotionMode.Ground, p.Mode);
            Assert.AreEqual(DragonMotionReadiness.Unsupported, P(Emberling).ReadinessFor(DragonMotionMode.Underground));
        }

        [Test]
        public void WaterDwellers_StayInWater_AndSleepWakesBeforeAMediumChange()
        {
            var f = P(Frostfin);
            Assert.AreEqual(DragonMotionMode.Water, f.Mode);
            Assert.IsTrue(Run(f, DragonBehaviorIntent.Idle, 2f).All(o => o.mode == DragonMotionMode.Water && o.trigger != DragonInteraction.WaterExit));
            Assert.IsTrue(f.Update(0.1f, DragonBehaviorIntent.Fly, 0f, 0f).substituted);

            var z = P(Zephyr);
            Run(z, DragonBehaviorIntent.Sleep, 1f);
            Assert.AreEqual(DragonMotionReadiness.NeedsWake, z.ReadinessFor(DragonMotionMode.Air));
            var go = Run(z, DragonBehaviorIntent.Fly, 2.5f);
            Assert.AreEqual(DragonInteraction.Wake, go[0].trigger);
            Assert.AreEqual(DragonInteraction.TakeOff, go.First(o => o.trigger != DragonInteraction.None && o.trigger != DragonInteraction.Wake).trigger);
            Assert.IsTrue(go.TakeWhile(o => o.trigger != DragonInteraction.TakeOff).All(o => !o.canTranslate), "locked through Wake too");
        }

        [Test]
        public void MissingTransitionClip_SwitchesAtOnceAndSaysSo()
        {
            var old = P(With("Walk", "Run", "Swim")); // pre-U1-CHAR.7 swimmer
            var o = old.Update(0.1f, DragonBehaviorIntent.Swim, 1f, 0f);
            Assert.AreEqual(DragonMotionMode.Water, old.Mode);
            Assert.AreEqual(DragonInteraction.None, o.trigger);
            StringAssert.Contains("WaterEnter", o.note);
            Assert.IsTrue(o.canTranslate);
        }

        [Test]
        public void Repeats_And_Reactions_WaitForTheMediumToSettle()
        {
            var p = P(Droplet);
            p.React(DragonReaction.Petted);
            var l = Run(p, DragonBehaviorIntent.Swim, 0.5f);
            Assert.IsTrue(l.All(o => o.trigger == DragonInteraction.None || o.trigger == DragonInteraction.WaterEnter), "no reaction in the middle of WaterEnter");
            var after = Run(p, DragonBehaviorIntent.Swim, 1.5f);
            Assert.IsTrue(after.Any(o => o.trigger == DragonInteraction.Happy), "plays once in the water");
        }

        [Test]
        public void MotionShape_BanksIntoTurnsInTheAir_PitchesWithClimb_NothingOnGroundButBend()
        {
            float bank, pitch, bend;
            MotionShapeSolver.Compute(DragonMotionMode.Air, 60f, 0f, 3f, 60f, out bank, out pitch, out bend);
            double expected = Math.Atan(3.0 * (60.0 * Math.PI / 180.0) / 9.81) * 180.0 / Math.PI;   // coordinated turn, 17.8 deg
            Assert.AreEqual(-expected, bank, 1e-3, "right turn: roll right (negative about forward), tan(bank) = V w / g");
            MotionShapeSolver.Compute(DragonMotionMode.Air, 60f, 0f, 0f, 60f, out bank, out pitch, out bend);
            Assert.AreEqual(-MotionShapeSolver.MaxBankDeg, bank, 1e-4f, "no speed sent: the U1-CHAR.7 linear mapping is kept");
            MotionShapeSolver.Compute(DragonMotionMode.Air, 600f, 50f, 1f, 60f, out bank, out pitch, out bend);
            Assert.AreEqual(-MotionShapeSolver.MaxBankDeg, bank, 1e-4f, "clamped");
            Assert.AreEqual(MotionShapeSolver.MaxPitchDeg, pitch, 1e-4f, "clamped climb");
            MotionShapeSolver.Compute(DragonMotionMode.Water, -45f, -1f, 1f, 90f, out bank, out pitch, out bend);
            Assert.AreEqual(0f, bank); Assert.Less(pitch, 0f); Assert.Less(bend, 0f);
            MotionShapeSolver.Compute(DragonMotionMode.Ground, 90f, 1f, 1f, 180f, out bank, out pitch, out bend);
            Assert.AreEqual(0f, bank); Assert.AreEqual(0f, pitch); Assert.Greater(bend, 0f);
        }

        [Test]
        public void CoordinatedBank_GrowsWithSpeedAndTurnRate_AndTheHeadStaysLevel()
        {
            float slow = MotionShapeSolver.CoordinatedBankDeg(45f, 1f), fast = MotionShapeSolver.CoordinatedBankDeg(45f, 4f);
            Assert.Less(fast, slow, "same yaw rate, faster flight: steeper bank (more negative for a right turn)");
            Assert.AreEqual(-slow, MotionShapeSolver.CoordinatedBankDeg(-45f, 1f), 1e-5f, "symmetric");
            Assert.AreEqual(0f, MotionShapeSolver.CoordinatedBankDeg(0f, 5f), 1e-6f);
            Assert.AreEqual(-MotionShapeSolver.MaxBankDeg, MotionShapeSolver.CoordinatedBankDeg(720f, 10f), 1e-5f, "clamped");
            // at the catalogue's air limit for the nominal speed the bank just reaches the limit (pipeline derives it the same way)
            float v = 1.2f;
            float limit = (float)(MotionShapeSolver.Gravity * Math.Tan(MotionShapeSolver.MaxBankDeg * Math.PI / 180.0) / v * 180.0 / Math.PI);
            Assert.AreEqual(-MotionShapeSolver.MaxBankDeg, MotionShapeSolver.CoordinatedBankDeg(limit, v), 1e-3f);
            float neck, head;
            MotionShapeSolver.HeadRollCompensation(-20f, 1f, out neck, out head);
            Assert.AreEqual(-20f + neck + head, 0f, 1e-5f, "full stabilisation: head level");
            MotionShapeSolver.HeadRollCompensation(-20f, 0f, out neck, out head);
            Assert.AreEqual(0f, neck + head, 1e-6f, "gain 0: no correction");
            MotionShapeSolver.HeadRollCompensation(-20f, 3f, out neck, out head);
            Assert.AreEqual(20f, neck + head, 1e-5f, "gain clamped to 1");
        }

        // ---- sync ----

        private sealed class MotionTarget : IDragonMotionTarget
        {
            public string[] Clips = Zephyr;
            public readonly HashSet<string> Live = new HashSet<string>();
            public readonly Dictionary<string, bool> Hidden = new Dictionary<string, bool>();
            public readonly DragonVisualCatalog.MotionInfo Info = new DragonVisualCatalog.MotionInfo { takeOffRootOffset = 0.39f, landRootOffset = 0.31f, maxTurnAir = 60f };
            public int Triggers;
            public DragonMotionMode LastMode;
            public bool IsLive(string uid) { return Live.Contains(uid); }
            public bool Spawn(DragonVisualSpawn s) { Live.Add(s.uid); return true; }
            public void Despawn(string uid) { Live.Remove(uid); }
            public bool SetStage(string uid, DragonVisualStage stage) { return true; }
            public bool ApplyState(string uid, DragonVisualState st) { return true; }
            public bool Trigger(string uid, DragonInteraction i) { Triggers++; return true; }
            public DragonAnimationMap ClipsOf(string uid) { return Live.Contains(uid) ? new DragonAnimationMap(Clips) : null; }
            public float ClipSeconds(string uid, DragonInteraction c) { return 1f; }
            public void SetTempo(string uid, float t) { }
            public void SetLook(string uid, bool a, Vector3 w, float weight, float hold) { }
            public DragonVisualCatalog.MotionInfo MotionInfoOf(string uid) { return Info; }
            public void SetHidden(string uid, bool hidden) { Hidden[uid] = hidden; }
            public void SetMotionShape(string uid, DragonMotionMode mode, float turn, float v, float s) { LastMode = mode; }
            public void SetExpression(string uid, float tail, float ear) { }
        }

        private static SanctuaryDragonFrame F(DragonBehaviorIntent i, DragonVisualStage stage = DragonVisualStage.Adult)
        {
            return new SanctuaryDragonFrame { uid = "z", speciesId = "zephyr", stage = stage, visible = true, intent = i, rotation = Quaternion.identity };
        }

        [Test]
        public void Sync_ReportsStatus_RootOffsets_Readiness_AndKeepsTheMediumOnGrowth()
        {
            var t = new MotionTarget();
            var sync = new SanctuaryDragonSync(t);
            sync.Sync(new[] { F(DragonBehaviorIntent.Idle) }, 0.1f);
            Assert.AreEqual(DragonMotionReadiness.Ready, sync.ReadinessFor("z", DragonMotionMode.Air));
            Assert.AreEqual(DragonMotionReadiness.Unsupported, sync.ReadinessFor("z", DragonMotionMode.Water));
            Assert.AreEqual(DragonMotionReadiness.NotLive, sync.ReadinessFor("nobody", DragonMotionMode.Air));
            float offset = 0f; bool locked = false;
            for (int k = 0; k < 15; k++)
            {
                sync.Sync(new[] { F(DragonBehaviorIntent.Fly) }, 0.1f);
                var s = sync.MotionStatusOf("z");
                offset += s.rootOffsetToApply;
                locked |= !s.canTranslate;
            }
            Assert.IsTrue(locked);
            Assert.AreEqual(0.39f, offset, 1e-5f, "raise by the TakeOff root offset once, on completion");
            Assert.AreEqual(DragonMotionMode.Air, sync.MotionStatusOf("z").mode);
            Assert.AreEqual(DragonMotionMode.Air, t.LastMode);
            Assert.IsTrue(sync.CapabilitiesOf("z").air);

            int before = t.Triggers;
            sync.Sync(new[] { F(DragonBehaviorIntent.Fly, DragonVisualStage.Baby) }, 0.1f); // grows into another model
            sync.Sync(new[] { F(DragonBehaviorIntent.Fly) }, 0.1f);
            Assert.AreEqual(before, t.Triggers, "growth keeps the medium: no TakeOff replay");

            sync.Sync(new[] { F(DragonBehaviorIntent.Idle) }, 0.1f);
            Assert.AreEqual(-0.31f, sync.MotionStatusOf("z").rootOffsetToApply, 1e-5f, "lower by the Land root offset when Land starts");
        }

        [Test]
        public void Sync_HidesUnderground_LegacyDragonsReportDefaults()
        {
            var t = new MotionTarget { Clips = Sprout };
            var sync = new SanctuaryDragonSync(t);
            for (int k = 0; k < 40; k++) sync.Sync(new[] { F(DragonBehaviorIntent.Underground) }, 0.1f);
            Assert.IsTrue(t.Hidden["z"]);
            Assert.IsFalse(sync.MotionStatusOf("z").visible);
            sync.Sync(new[] { F(DragonBehaviorIntent.Idle) }, 0.1f);
            Assert.IsFalse(t.Hidden["z"], "emerging: shown again");
            sync.Sync(new[] { F(DragonBehaviorIntent.None) }, 0.1f);
            var s = sync.MotionStatusOf("z");
            Assert.IsTrue(s.live && s.canTranslate && s.visible && s.mode == DragonMotionMode.Ground, "legacy path: defaults");
            Assert.IsFalse(t.Hidden["z"]);
        }

#if DRAVIO_DOTNET_TESTS
        [Test]
        public void Sync_MotionIntents_AllocateNothingPerFrame()
        {
            var t = new MotionTarget { Clips = Droplet };
            var sync = new SanctuaryDragonSync(t);
            var cycle = new[] { DragonBehaviorIntent.Swim, DragonBehaviorIntent.SwimIdle, DragonBehaviorIntent.Idle, DragonBehaviorIntent.Greet, DragonBehaviorIntent.SwimSurface, DragonBehaviorIntent.Fly };
            var frames = Enumerable.Range(0, 30).Select(i => new SanctuaryDragonFrame
            {
                uid = "d" + i, speciesId = "droplet", stage = DragonVisualStage.Adult, visible = true, rotation = Quaternion.identity,
                intent = cycle[i % cycle.Length], personality = (DragonPersonalityStyle)(i % 5), turnRateDegPerSec = 30f, verticalSpeed = 0.2f,
            }).ToArray();
            for (int k = 0; k < 300; k++) sync.Sync(frames, 1f / 30f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int k = 0; k < 300; k++) sync.Sync(frames, 1f / 30f);
            Assert.Less(System.GC.GetAllocatedBytesForCurrentThread() - before, 300L);
        }
#endif
    }
}
