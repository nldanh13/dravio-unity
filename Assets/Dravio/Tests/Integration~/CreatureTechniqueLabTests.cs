using System;
using System.Linq;
using Dravio.TechnologyLab;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class CreatureTechniqueLabTests
    {
        [Test] public void ContactPlaneAndLimits()
        {
            var samples=new[]{new ContactSample(-1,-1,-.2),new ContactSample(1,-1,-.2),
                new ContactSample(-1,1,.4),new ContactSample(1,1,.4)};
            var p=ContactLab.Solve(samples,maxTilt:10,maxOffset:.05);
            Assert.That(p.Valid,Is.True);Assert.That(p.Pitch,Is.EqualTo(10).Within(1e-8));
            Assert.That(p.Roll,Is.EqualTo(0).Within(1e-8));Assert.That(p.BodyOffset,Is.EqualTo(.05).Within(1e-8));
            Assert.That(ContactLab.Solve(samples.Take(2)).Valid,Is.False);
            Assert.That(ContactLab.Solve(new[]{new ContactSample(0,0,0),new ContactSample(1,0,0),new ContactSample(2,0,0)}).Valid,Is.False);
        }
        [Test] public void FootReachAndRateAreBounded()
        {
            Assert.That(ContactLab.FootOffset(0,1,true,true,.2),Is.EqualTo(.2));
            Assert.That(ContactLab.FootOffset(0,1,false,true,.2),Is.Zero);
            Assert.That(ContactLab.FootOffset(0,1,true,false,.2),Is.Zero);
            Assert.That(ContactLab.Blend(0,40,.05,20,30),Is.LessThanOrEqualTo(1.5));
        }
        [Test] public void MemoryRespectsRangeOcclusionHabitatAndExpiry()
        {
            var memory=new LabMemory(1000);
            var seen=new LabStimulus("seen","h",StimulusKind.Food,new LabVec(1,0),5000);
            var hidden=new LabStimulus("hidden","h",StimulusKind.Food,new LabVec(2,0),5000);
            var far=new LabStimulus("far","h",StimulusKind.Player,new LabVec(4,0),5000);
            var other=new LabStimulus("other","x",StimulusKind.Point,new LabVec(1,0),5000);
            memory.Sense(new LabVec(),"h",3,100,new[]{other,far,hidden,seen},(a,b)=>b.X<2);
            Assert.That(memory.Snapshot().Select(x=>x.Id),Is.EqualTo(new[]{"seen"}));
            memory.Sense(new LabVec(),"h",3,999,Array.Empty<LabStimulus>(),(a,b)=>true);
            Assert.That(memory.Snapshot().Count,Is.EqualTo(1));
            memory.Sense(new LabVec(),"h",3,1100,Array.Empty<LabStimulus>(),(a,b)=>true);
            Assert.That(memory.Snapshot(),Is.Empty);
        }
        [Test] public void MemoryDropsIneligibleImmediatelyAndCannotCrossHabitat()
        {
            var m=new LabMemory();var s=new LabStimulus("f","h",StimulusKind.Food,new LabVec(1,0),10000);
            m.Sense(new LabVec(),"h",2,0,new[]{s},(a,b)=>true);
            m.Sense(new LabVec(),"h",2,1,new[]{new LabStimulus("f","h",StimulusKind.Food,s.Position,10000,false)},(a,b)=>true);
            Assert.That(m.Snapshot(),Is.Empty);
            m.Sense(new LabVec(),"h",2,2,new[]{s},(a,b)=>true);
            m.Sense(new LabVec(),"other",2,3,new[]{s},(a,b)=>true);
            Assert.That(m.Snapshot(),Is.Empty);
        }
        [Test] public void SequenceRequestsOnlyAfterAllGatesAndOnce()
        {
            var s=new ActionSequenceLab();s.Begin("f");
            Assert.That(s.Tick(true,true,false,false,true),Is.False);
            Assert.That(s.Tick(true,true,true,false,true),Is.False);
            Assert.That(s.Tick(true,true,true,true,false),Is.False);
            Assert.That(s.Tick(true,true,true,true,true),Is.True);
            Assert.That(s.Tick(true,true,true,true,true),Is.False);
            Assert.That(s.Requests,Is.EqualTo(1));s.Resolve(true);Assert.That(s.Step,Is.EqualTo(ActionStep.Complete));
        }
        [Test] public void ExpiredTargetOrBlockedPathCancelsWithoutRequest()
        {
            var s=new ActionSequenceLab();s.Begin("f");
            Assert.That(s.Tick(false,true,true,true,true),Is.False);Assert.That(s.Step,Is.EqualTo(ActionStep.Cancelled));
            s.Begin("f");Assert.That(s.Tick(true,false,false,false,true),Is.False);Assert.That(s.Requests,Is.Zero);
        }
        [Test] public void SocialPairsAreExclusiveMutualAndWithinHabitat()
        {
            var a=new SocialCandidate("a","h",new LabVec(0,0),true);
            var b=new SocialCandidate("b","h",new LabVec(1,0),true);
            var c=new SocialCandidate("c","h",new LabVec(1.5,0),true);
            var shy=new SocialCandidate("d","h",new LabVec(1.1,0),false);
            var other=new SocialCandidate("e","x",new LabVec(1,0),true);
            var busy=new SocialCandidate("f","h",new LabVec(1,0),true,false);
            var pairs=SocialPairLab.Match(new[]{busy,other,shy,c,b,a},(x,y)=>true);
            Assert.That(pairs.Count,Is.EqualTo(1));Assert.That(pairs[0].A,Is.EqualTo("b"));Assert.That(pairs[0].B,Is.EqualTo("c"));
            Assert.That(SocialPairLab.Match(new[]{a,b},(x,y)=>x.Uid=="a"),Is.Empty);
        }
    }
}
