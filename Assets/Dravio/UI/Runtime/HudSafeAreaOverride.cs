using UnityEngine;

namespace Dravio.UI
{
    /// <summary>
    /// Editor/development-only safe-area simulation for the HUD notch QA of VIS.2a (notch left/right on any
    /// Game View size). Release builds always use Screen.safeArea. Unity's Device Simulator remains the
    /// reference for real device cut-outs.
    /// </summary>
    public static class HudSafeAreaOverride
    {
        public enum Notch{None=0,Left=1,Right=2,Both=3}
        public static Notch Simulated{get;private set;}
        /// <summary>Simulated inset as a fraction of screen width (≈ 132 px on a 2400 px wide phone).</summary>
        public const float InsetFraction=.055f;

        public static void Simulate(Notch notch)=>Simulated=notch;

        public static Rect Current
        {
            get
            {
                var safe=Screen.safeArea;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if(Simulated!=Notch.None)
                {
                    float inset=Screen.width*InsetFraction;
                    float left=(Simulated&Notch.Left)!=0?inset:0,right=(Simulated&Notch.Right)!=0?inset:0;
                    safe=new Rect(safe.x+left,safe.y,Mathf.Max(1,safe.width-left-right),safe.height);
                }
#endif
                return safe;
            }
        }
    }
}
