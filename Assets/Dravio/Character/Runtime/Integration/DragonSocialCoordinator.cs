using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dravio.Character.Integration
{
    /// <summary>What a pair act asks of one dragon this tick (applied by <see cref="SanctuaryDragonSync"/>).</summary>
    public struct DragonSocialDirective
    {
        public bool active;
        /// <summary>Intent used instead of the frame's (None = keep the frame's intent).</summary>
        public DragonBehaviorIntent intent;
        public bool look;
        public Vector3 lookTarget;
        public float lookWeight, lookHold;
        public DragonExpression expression;
        /// <summary>Multiplier on the personality tempo (1 = unchanged).</summary>
        public float tempoScale;
    }

    /// <summary>Pair state of one dragon (read with <see cref="SanctuaryDragonSync.SocialStatusOf"/>).</summary>
    public struct DragonSocialStatus
    {
        public DragonSocialAct act;
        public string partnerUid;
        public DragonSocialRole role;
        public DragonSocialPhase phase;
        /// <summary>Root-to-root distance (m) the act is shown at; the Sanctuary places the dragons (0 = unknown sizes).</summary>
        public float desiredSpacing;
        /// <summary>Accepted deviation from desiredSpacing (m).</summary>
        public float spacingTolerance;
        /// <summary>Current horizontal root-to-root distance (m).</summary>
        public float distance;
        public bool inRange;
        /// <summary>True when the dragon should face its partner (the Sanctuary owns the rotation).</summary>
        public bool faceEachOther;
        /// <summary>Bouts or one-shots shown so far (Play: turns, Nurture: nuzzles + play cues).</summary>
        public int beats;
        public string note;
    }

    /// <summary>What the coordinator needs from the sync (an interface so it is testable without Unity).</summary>
    public interface IDragonSocialWorld
    {
        bool TryFrame(string uid, out SanctuaryDragonFrame frame);
        DragonBehaviorPlanner PlannerOf(string uid);
        DragonAnimationMap ClipsOf(string uid);
        DragonVisualCatalog.MotionInfo MotionInfoOf(string uid);
        /// <summary>World position of the dragon's head (the target's Head bone; a size-based estimate otherwise).</summary>
        Vector3 HeadOf(string uid, SanctuaryDragonFrame frame);
        /// <summary>Puts the follower's looping state at the leader's normalized time (same clip, same phase).</summary>
        void SyncPhase(string followerUid, string leaderUid);
    }

    /// <summary>
    /// U1-CHAR.8: turns a pair act the Sanctuary asks for (both frames name each other with the same
    /// <see cref="DragonSocialAct"/>) into per-dragon directives: who leads, facing (look at the partner's head),
    /// synchronized one-shot starts (cued on both planners in the same tick; the responder answers after
    /// <see cref="ResponseLatencySeconds"/>), turn-taking with role reversal in play, phase-locked breathing when resting
    /// together, and the older/younger roles of Nurture. Pure C#, deterministic per pair, no allocation per tick.
    /// It never moves a dragon, never decides who meets whom and creates no relationship: the Sanctuary places the
    /// pair at <see cref="DragonSocialStatus.desiredSpacing"/> and ends the act by clearing <c>social</c>.
    /// Timing values are art-direction defaults (documented in SOCIAL_ANIMATION.md), not Domain rules.
    /// </summary>
    public sealed class DragonSocialCoordinator
    {
        public const float ResponseLatencySeconds = 0.3f;
        public const float DoneHoldSeconds = 1.5f;
        public const float PlayTurnGapSeconds = 0.6f;
        /// <summary>A play bow is repeated every this many turns to keep the bout read as play (Bekoff 1995).</summary>
        public const int PlayBowEveryTurns = 3;
        public const float SpacingTolerance = 0.35f;
        public const float YoungTempoScale = 1.1f;

        private sealed class Pair
        {
            public string leader, responder;
            public DragonSocialAct act;
            public float t, nextBeat, responderAt;
            public DragonInteraction responderCue;
            public int beats;
            public bool started, leaderCued, responderCued, phaseSynced, seen;
            public float doneAt;
            public uint rng;
        }

        private readonly Dictionary<string, Pair> _pairs = new Dictionary<string, Pair>(StringComparer.Ordinal);   // by leader uid
        private readonly Dictionary<string, DragonSocialDirective> _directives = new Dictionary<string, DragonSocialDirective>(StringComparer.Ordinal);
        private readonly Dictionary<string, DragonSocialStatus> _status = new Dictionary<string, DragonSocialStatus>(StringComparer.Ordinal);
        private readonly List<string> _drop = new List<string>();
        private readonly Stack<Pair> _free = new Stack<Pair>();

        public bool TryDirective(string uid, out DragonSocialDirective d) { return _directives.TryGetValue(uid, out d); }
        public bool TryStatus(string uid, out DragonSocialStatus s) { s = default(DragonSocialStatus); return uid != null && _status.TryGetValue(uid, out s); }
        public int PairCount { get { return _pairs.Count; } }
        public bool HasState { get { return _pairs.Count > 0 || _status.Count > 0 || _directives.Count > 0; } }

        /// <summary>Plans every pair named in <paramref name="socialUids"/> (dragons whose frame has a social act).</summary>
        public void Plan(float dt, HashSet<string> socialUids, IDragonSocialWorld world)
        {
            _directives.Clear();
            _status.Clear();
            foreach (var p in _pairs.Values) p.seen = false;
            foreach (var uid in socialUids)
            {
                SanctuaryDragonFrame f, g;
                if (!world.TryFrame(uid, out f)) continue;
                string partner = f.partnerUid;
                if (string.IsNullOrEmpty(partner) || partner == uid || !world.TryFrame(partner, out g) || g.social != f.social || g.partnerUid != uid)
                {
                    _status[uid] = new DragonSocialStatus { act = f.social, partnerUid = partner, phase = DragonSocialPhase.WaitingForPartner,
                        note = "partner must be shown and name this dragon with the same act" };
                    continue;
                }
                if (string.CompareOrdinal(uid, partner) > 0) continue; // each pair once, from its smaller uid
                PlanPair(dt, uid, f, partner, g, world);
            }
            _drop.Clear();
            foreach (var kv in _pairs) if (!kv.Value.seen) _drop.Add(kv.Key);
            foreach (var k in _drop) { _free.Push(_pairs[k]); _pairs.Remove(k); }
        }

        public void Clear() { _pairs.Clear(); _directives.Clear(); _status.Clear(); }

        private void PlanPair(float dt, string ua, SanctuaryDragonFrame fa, string ub, SanctuaryDragonFrame fb, IDragonSocialWorld world)
        {
            var act = fa.social;
            // roles: older stage leads (Nurture: guardian), then UID order
            bool aLeads = fa.stage != fb.stage ? fa.stage > fb.stage : true;
            string lead = aLeads ? ua : ub, resp = aLeads ? ub : ua;
            var fl = aLeads ? fa : fb; var fr = aLeads ? fb : fa;
            var leadRole = DragonSocialRole.Leader; var respRole = DragonSocialRole.Responder;
            if (act == DragonSocialAct.Nurture)
            {
                if (fr.stage != DragonVisualStage.Baby || fl.stage == DragonVisualStage.Baby || fl.stage == DragonVisualStage.Unstaged)
                {
                    Reject(ua, ub, act, "Nurture needs a Baby and a Juvenile or Adult");
                    return;
                }
                leadRole = DragonSocialRole.Guardian; respRole = DragonSocialRole.Young;
            }
            Pair pair;
            if (!_pairs.TryGetValue(lead, out pair) || pair.responder != resp || pair.act != act)
            {
                if (pair != null) _pairs.Remove(lead);
                pair = _free.Count > 0 ? _free.Pop() : new Pair();
                pair.leader = lead; pair.responder = resp; pair.act = act;
                pair.t = pair.nextBeat = pair.responderAt = pair.doneAt = 0f; pair.beats = 0;
                pair.started = pair.leaderCued = pair.responderCued = pair.phaseSynced = false;
                pair.responderCue = DragonInteraction.None;
                pair.rng = DragonBehaviorPlanner.SeedFor(lead) ^ (DragonBehaviorPlanner.SeedFor(resp) * 16777619u);
                if (pair.rng == 0) pair.rng = 1;
                _pairs[lead] = pair;
            }
            pair.seen = true;

            // spacing from the two bodies (the Sanctuary places them; the act waits until they are there)
            var il = world.MotionInfoOf(lead); var ir = world.MotionInfoOf(resp);
            float desired = DesiredSpacing(act, il, ir);
            float dx = fl.position.x - fr.position.x, dz = fl.position.z - fr.position.z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            float tol = desired * SpacingTolerance;
            bool inRange = desired <= 0f || Math.Abs(dist - desired) <= tol;
            var pl = world.PlannerOf(lead); var pr = world.PlannerOf(resp);

            var sl = new DragonSocialStatus { act = act, partnerUid = resp, role = leadRole, desiredSpacing = desired, spacingTolerance = tol,
                distance = dist, inRange = inRange, faceEachOther = act != DragonSocialAct.RestTogether, beats = pair.beats };
            var sr = sl; sr.partnerUid = lead; sr.role = respRole;

            Vector3 headL = world.HeadOf(lead, fl), headR = world.HeadOf(resp, fr);
            var dl = new DragonSocialDirective { active = true, look = true, lookTarget = headR, lookWeight = 1f, lookHold = 1f, tempoScale = 1f, expression = ExpressionFor(act, leadRole) };
            var dr = new DragonSocialDirective { active = true, look = true, lookTarget = headL, lookWeight = 1f, lookHold = 1f, tempoScale = 1f, expression = ExpressionFor(act, respRole) };
            if (act == DragonSocialAct.RestTogether) { dl.lookWeight = dr.lookWeight = 0.6f; dl.lookHold = dr.lookHold = 0.3f; }
            if (act == DragonSocialAct.Nurture) { dl.lookHold = 0.85f; dr.lookHold = 0.7f; dr.tempoScale = YoungTempoScale; }

            string why = null;
            if (pl == null || pr == null) why = "waiting for both planners";
            else if (pl.Mode != pr.Mode && !pl.InTransition && !pr.InTransition) { Reject(ua, ub, act, "different media (bring both to the same medium)"); return; }
            else if (act == DragonSocialAct.RestTogether && (pl.Mode == DragonMotionMode.Air || pl.Mode == DragonMotionMode.Underground)) { Reject(ua, ub, act, "cannot rest together in the air or underground"); return; }
            else if (fl.intent == DragonBehaviorIntent.Sleep || fr.intent == DragonBehaviorIntent.Sleep) { if (act != DragonSocialAct.RestTogether) why = "asleep (waking is the Sanctuary's decision)"; }
            else if (pl.InTransition || pr.InTransition) why = "a medium transition is running";

            if (!inRange)
            {
                // the Sanctuary is bringing them together: they already watch each other, their own intents still apply
                dl.intent = dr.intent = DragonBehaviorIntent.None;
                Commit(lead, resp, DragonSocialPhase.OutOfRange, dl, dr, sl, sr, "bring the pair to desiredSpacing");
                return;
            }
            if (why != null)
            {
                dl.intent = dr.intent = DragonBehaviorIntent.None;
                Commit(lead, resp, DragonSocialPhase.Preparing, dl, dr, sl, sr, why);
                return;
            }

            pair.t += dt;
            var phase = DragonSocialPhase.Acting;
            var mode = pl.Mode;
            dl.intent = dr.intent = DragonBehaviorIntent.Social;
            var ml = world.ClipsOf(lead); var mr = world.ClipsOf(resp);
            switch (act)
            {
                case DragonSocialAct.Greet:
                case DragonSocialAct.Nuzzle:
                {
                    var first = act == DragonSocialAct.Greet ? Pick(ml, DragonInteraction.Greet, DragonInteraction.Happy, DragonInteraction.LookAround)
                                                             : Pick(ml, DragonInteraction.Nuzzle, DragonInteraction.Curious, DragonInteraction.Greet);
                    var answer = act == DragonSocialAct.Greet ? Pick(mr, DragonInteraction.Greet, DragonInteraction.Happy, DragonInteraction.LookAround)
                                                              : Pick(mr, DragonInteraction.Nuzzle, DragonInteraction.Happy, DragonInteraction.LookAround);
                    if (!pair.leaderCued)
                    {
                        if (pl.Settled && pr.Settled) // both free in the same tick: synchronized start
                        {
                            pl.Cue(first); pair.leaderCued = true; pair.responderAt = pair.t + ResponseLatencySeconds; pair.responderCue = answer; pair.beats++;
                        }
                    }
                    else if (!pair.responderCued)
                    {
                        if (pair.t >= pair.responderAt && pr.Settled) { pr.Cue(pair.responderCue); pair.responderCued = true; pair.beats++; }
                    }
                    else if (pl.Settled && pr.Settled && pl.PendingCue == DragonInteraction.None && pr.PendingCue == DragonInteraction.None)
                    {
                        if (pair.doneAt <= 0f) pair.doneAt = pair.t;
                        if (pair.t - pair.doneAt >= DoneHoldSeconds) phase = DragonSocialPhase.Done;
                    }
                    break;
                }
                case DragonSocialAct.Play:
                {
                    if (pair.responderCue != DragonInteraction.None && pair.t >= pair.responderAt)
                    {
                        // the partner of the active player answers after the latency
                        var answerer = pair.beats % 2 == 1 ? pr : pl;
                        if (answerer.Settled) { answerer.Cue(pair.responderCue); pair.responderCue = DragonInteraction.None; }
                    }
                    else if (pair.responderCue == DragonInteraction.None && pl.Settled && pr.Settled && pair.t >= pair.nextBeat)
                    {
                        // turn-taking with role reversal: even turns the leader acts, odd turns the responder
                        int turn = pair.beats;
                        bool leaderTurn = turn % 2 == 0;
                        var actor = leaderTurn ? pl : pr; var map = leaderTurn ? ml : mr; var other = leaderTurn ? mr : ml;
                        DragonInteraction move = turn % PlayBowEveryTurns == 0
                            ? Pick(map, DragonInteraction.Stretch, DragonInteraction.Happy, DragonInteraction.Curious)      // play bow
                            : (Next01(ref pair.rng) < 0.6f ? Pick(map, DragonInteraction.Happy, DragonInteraction.Curious, DragonInteraction.Stretch)
                                                           : Pick(map, DragonInteraction.Curious, DragonInteraction.Happy, DragonInteraction.LookAround));
                        actor.Cue(move);
                        pair.responderCue = Pick(other, DragonInteraction.Happy, DragonInteraction.LookAround, DragonInteraction.Curious);
                        pair.responderAt = pair.t + ResponseLatencySeconds;
                        pair.beats = turn + 1;
                        pair.nextBeat = pair.t + PlayTurnGapSeconds;
                    }
                    else if (pair.responderCue == DragonInteraction.None && !(pl.Settled && pr.Settled))
                        pair.nextBeat = Math.Max(pair.nextBeat, pair.t + PlayTurnGapSeconds);
                    break;
                }
                case DragonSocialAct.RestTogether:
                {
                    var rest = mode == DragonMotionMode.Water ? DragonBehaviorIntent.SwimRest : DragonBehaviorIntent.Rest;
                    dl.intent = dr.intent = rest;
                    if (!pair.phaseSynced && pl.Shown == pr.Shown && pl.Settled && pr.Settled
                        && (pl.Shown == DragonLocomotion.Rest || pl.Shown == DragonLocomotion.SwimRest || pl.Shown == DragonLocomotion.SwimIdle || pl.Shown == DragonLocomotion.Sleep))
                    {
                        world.SyncPhase(resp, lead); // breathing in phase
                        pair.phaseSynced = true;
                        pair.beats++;
                    }
                    if (pl.Shown != pr.Shown) pair.phaseSynced = false;
                    break;
                }
                case DragonSocialAct.Nurture:
                {
                    if (pair.t >= pair.nextBeat && pl.Settled && pr.Settled)
                    {
                        // the Baby plays around the older one; every few bouts the older one nuzzles it
                        bool nuzzle = pair.beats > 0 && pair.beats % 3 == 0;
                        if (nuzzle) pl.Cue(Pick(ml, DragonInteraction.Nuzzle, DragonInteraction.Curious, DragonInteraction.None));
                        else pr.Cue(pair.beats % 2 == 0 ? Pick(mr, DragonInteraction.Happy, DragonInteraction.Curious, DragonInteraction.Stretch)
                                                        : Pick(mr, DragonInteraction.Curious, DragonInteraction.Happy, DragonInteraction.LookAround));
                        pair.beats++;
                        pair.nextBeat = pair.t + 3f + 3f * Next01(ref pair.rng);
                    }
                    break;
                }
            }
            sl.beats = sr.beats = pair.beats;
            Commit(lead, resp, phase, dl, dr, sl, sr, null);
        }

        private void Commit(string lead, string resp, DragonSocialPhase phase, DragonSocialDirective dl, DragonSocialDirective dr,
                            DragonSocialStatus sl, DragonSocialStatus sr, string note)
        {
            sl.phase = sr.phase = phase;
            sl.note = sr.note = note;
            _directives[lead] = dl; _directives[resp] = dr;
            _status[lead] = sl; _status[resp] = sr;
        }

        private void Reject(string ua, string ub, DragonSocialAct act, string note)
        {
            _status[ua] = new DragonSocialStatus { act = act, partnerUid = ub, phase = DragonSocialPhase.Rejected, note = note };
            _status[ub] = new DragonSocialStatus { act = act, partnerUid = ua, phase = DragonSocialPhase.Rejected, note = note };
        }

        /// <summary>
        /// Root-to-root spacing of an act from the two bodies: face to face the roots are half a body length each
        /// from the snout, so snouts meet at (La + Lb) / 2; greeting leaves a small gap, nuzzling touches, play needs
        /// room; resting together lies side by side ((Wa + Wb) / 2). 0 when a size is unknown (no range check).
        /// </summary>
        public static float DesiredSpacing(DragonSocialAct act, DragonVisualCatalog.MotionInfo a, DragonVisualCatalog.MotionInfo b)
        {
            if (a == null || b == null || a.bodyLength <= 0f || b.bodyLength <= 0f) return 0f;
            float half = 0.5f * (a.bodyLength + b.bodyLength), small = Math.Min(a.bodyLength, b.bodyLength);
            switch (act)
            {
                case DragonSocialAct.Greet: return half + 0.1f * small;
                case DragonSocialAct.Nuzzle: return half - 0.05f * small;
                case DragonSocialAct.Play: case DragonSocialAct.Nurture: return half + 0.3f * small;
                case DragonSocialAct.RestTogether:
                    return a.bodyWidth > 0f && b.bodyWidth > 0f ? 0.5f * (a.bodyWidth + b.bodyWidth) + 0.05f * small : 0f;
                default: return 0f;
            }
        }

        public static DragonExpression ExpressionFor(DragonSocialAct act, DragonSocialRole role)
        {
            switch (act)
            {
                case DragonSocialAct.Greet: return DragonExpression.Happy;
                case DragonSocialAct.Play: return DragonExpression.Playful;
                case DragonSocialAct.RestTogether: return DragonExpression.Calm;
                case DragonSocialAct.Nuzzle: return DragonExpression.Affection;
                case DragonSocialAct.Nurture: return role == DragonSocialRole.Young ? DragonExpression.Playful : DragonExpression.Calm;
                default: return DragonExpression.Neutral;
            }
        }

        private static DragonInteraction Pick(DragonAnimationMap m, DragonInteraction a, DragonInteraction b, DragonInteraction c)
        {
            if (m == null) return DragonInteraction.None;
            if (m.CanPlay(a)) return a;
            if (m.CanPlay(b)) return b;
            return m.CanPlay(c) ? c : DragonInteraction.None;
        }

        private static float Next01(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / 16777216f;
        }
    }
}
