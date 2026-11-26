using System;
using System.Linq;
using Dravio.TechnologyLab;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class SteeringLabTests
    {
        [Test] public void SweptCrossingDetectsBetweenFrameContact()
        {
            Assert.That(SteeringLab.SweptDistance(new LabVec(-1,0),new LabVec(1,0),
                new LabVec(1,0),new LabVec(-1,0)),Is.EqualTo(0).Within(1e-9));
        }
        [Test] public void InvalidInitialOverlapRejected()
        {
            Assert.Throws<ArgumentException>(()=>new SteeringLab(new[]{
                new LabAgent(0,new LabVec(0,0),new LabVec(2,0)),
                new LabAgent(1,new LabVec(.1,0),new LabVec(-2,0))},null,6));
        }
        [Test] public void HeadOnStopOnlyStalls()
        {
            var lab=SteeringLab.Create(LabScenario.HeadOn);
            for(int i=0;i<400;i++)lab.Step(LabMode.StopOnly);
            Assert.That(lab.Arrived,Is.Zero);
            Assert.That(lab.StoppedAgentTicks,Is.GreaterThan(0));
            Assert.That(lab.MinGap,Is.GreaterThanOrEqualTo(.02499));
        }
        [Test] public void HeadOnSteeringMakesProgressWithoutContact()
        {
            var lab=SteeringLab.Create(LabScenario.HeadOn);
            for(int i=0;i<400;i++)lab.Step(LabMode.PredictiveSteering);
            Assert.That(lab.Arrived,Is.EqualTo(2));
            Assert.That(lab.MinGap,Is.GreaterThanOrEqualTo(.02499));
        }
        [TestCase(LabScenario.Crossing,5)]
        [TestCase(LabScenario.Crossing,15)]
        [TestCase(LabScenario.Obstacle,30)]
        [TestCase(LabScenario.MixedGroups,15)]
        public void EverySweptPairAndObstacleStaysClear(LabScenario scenario,int count)
        {
            var lab=SteeringLab.Create(scenario,count);
            for(int tick=0;tick<180;tick++)
            {
                var before=lab.Agents.Select(a=>a.Position).ToArray();
                lab.Step(LabMode.PredictiveSteering,flock:scenario==LabScenario.MixedGroups?.5:0);
                for(int i=0;i<count;i++)
                {
                    var a=lab.Agents[i];
                    Assert.That((a.Position-before[i]).Length,Is.LessThanOrEqualTo(a.Speed*.05+1e-8));
                    for(int j=0;j<i;j++)
                        Assert.That(SteeringLab.SweptDistance(before[i],a.Position,before[j],lab.Agents[j].Position),
                            Is.GreaterThanOrEqualTo(a.Radius+lab.Agents[j].Radius+.02499));
                    foreach(var o in lab.Obstacles)
                        Assert.That(SteeringLab.SweptDistance(before[i],a.Position,o.Center,o.Center),
                            Is.GreaterThanOrEqualTo(a.Radius+o.Radius+.02499));
                }
            }
        }
        [Test] public void DeterministicReplay()
        {
            var a=SteeringLab.Create(LabScenario.MixedGroups,5);var b=SteeringLab.Create(LabScenario.MixedGroups,5);
            for(int i=0;i<100;i++){a.Step(LabMode.PredictiveSteering,flock:.5);b.Step(LabMode.PredictiveSteering,flock:.5);}
            Assert.That(a.Agents.Select(x=>(x.Position.X,x.Position.Y)),Is.EqualTo(b.Agents.Select(x=>(x.Position.X,x.Position.Y))));
        }
        [Test] public void InvalidStepRejected()
        {
            var lab=SteeringLab.Create(LabScenario.HeadOn);
            Assert.Throws<ArgumentException>(()=>lab.Step(LabMode.PredictiveSteering,dt:double.NaN));
            Assert.Throws<ArgumentException>(()=>lab.Step(LabMode.PredictiveSteering,dt:.5));
        }
    }
}
