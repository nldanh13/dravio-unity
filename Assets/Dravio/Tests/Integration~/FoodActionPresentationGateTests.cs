using Dravio;
using NUnit.Framework;
namespace Dravio.Tests
{
    public sealed class FoodActionPresentationGateTests
    {
        [Test] public void MemoryExpiresAndDoesNotAuthorizeThroughObstacle()
        {
            var g=new FoodActionPresentationGate();
            Assert.That(g.Observe("a","f","h",100,10000,true,1,true),Is.True);
            Assert.That(g.Observe("a","f","h",1000,10000,true,1,false),Is.True);
            Assert.That(g.TryRequest("a","f","h",1100,10000,true,.4,false,true,true),Is.False);
            Assert.That(g.Observe("a","f","h",2100,10000,true,1,false),Is.False);
        }
        [Test] public void RequiresReachFacingReadinessAndOnlyOneRequest()
        {
            var g=new FoodActionPresentationGate();
            Assert.That(g.TryRequest("a","f","h",1,1000,true,.6,true,true,true),Is.False);
            Assert.That(g.TryRequest("a","f","h",2,1000,true,.4,true,false,true),Is.False);
            Assert.That(g.TryRequest("a","f","h",3,1000,true,.4,true,true,false),Is.False);
            Assert.That(g.TryRequest("a","f","h",4,1000,true,.4,true,true,true),Is.True);
            g.Resolve(false);
            Assert.That(g.TryRequest("a","f","h",5,1000,true,.4,true,true,true),Is.False);
            Assert.That(g.Phase,Is.EqualTo(FoodActionPhase.Rejected));Assert.That(g.Requests,Is.EqualTo(1));
            Assert.That(g.TryRequest("a","g","h",6,1000,true,.4,true,true,true),Is.True);
        }
        [Test] public void OwnerHabitatEligibilityAndExpiryAreRechecked()
        {
            var g=new FoodActionPresentationGate();
            Assert.That(g.Observe("a","f","h",10,100,true,1,true),Is.True);
            Assert.That(g.TryRequest("b","f","h",11,100,true,.4,true,true,true),Is.False);
            Assert.That(g.TryRequest("a","f","other",12,100,true,.4,true,true,true),Is.False);
            Assert.That(g.TryRequest("a","f","h",13,100,false,.4,true,true,true),Is.False);
            Assert.That(g.TryRequest("a","f","h",100,100,true,.4,true,true,true),Is.False);
            Assert.That(g.Requests,Is.Zero);
        }
        [Test] public void BackwardClockAndInvalidDistancesFailClosed()
        {
            var g=new FoodActionPresentationGate();
            g.Observe("a","f","h",100,1000,true,1,true);
            Assert.That(g.TryRequest("a","f","h",90,1000,true,.4,true,true,true),Is.False);
            Assert.That(g.TryRequest("a","f","h",101,1000,true,double.NaN,true,true,true),Is.False);
        }
    }
}
