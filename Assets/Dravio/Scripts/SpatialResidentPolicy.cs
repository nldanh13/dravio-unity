using System;
using Dravio.Navigation;

namespace Dravio
{
    // Editor trial policy. Canonical biology and placement remain separate from presentation eligibility.
    public static class SpatialResidentPolicy
    {
        public static NavigationMedium RequestedMedium(string zone,bool swim,bool fly,bool enabled)
            =>!enabled?NavigationMedium.Ground:zone=="ocean"&&swim?NavigationMedium.Swim:
                zone=="sky"&&fly?NavigationMedium.Air:NavigationMedium.Ground;
        public static bool MayTranslate(bool live,bool exactAsset,bool correctClip,bool actionPlaying,bool movementRequested)
            =>live&&exactAsset&&correctClip&&!actionPlaying&&movementRequested;
        public static bool Positive(float x)=>x>0&&!float.IsInfinity(x)&&!float.IsNaN(x);
        // Horizontal exclusion deliberately ignores depth: conservative mixed-medium proxy.
        public static bool Separated(NavPoint a,float radiusA,NavPoint b,float radiusB)
        {
            if(!a.Finite||!b.Finite||!Positive(radiusA)||!Positive(radiusB))return false;
            double x=(double)a.X-b.X,z=(double)a.Z-b.Z,required=(double)radiusA+radiusB;
            return x*x+z*z>=required*required;
        }
    }
}
