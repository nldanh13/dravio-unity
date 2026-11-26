using System;

namespace Dravio.UI
{
    /// <summary>Plain screen-space rectangle in pixels (bottom-left origin, like Screen.safeArea).</summary>
    public readonly struct HudPixelRect
    {
        public readonly float X,Y,Width,Height;
        public HudPixelRect(float x,float y,float w,float h){X=x;Y=y;Width=w;Height=h;}
        public float XMax=>X+Width; public float YMax=>Y+Height;
        public bool IsValid=>Width>0 && Height>0;
    }

    /// <summary>Result of the landscape HUD layout for one screen, in canvas logical units unless noted.</summary>
    public readonly struct HudLayoutResult
    {
        public readonly bool Compact;
        public readonly float ReferenceWidth,ReferenceHeight,CanvasScale;
        public readonly float SafeWidthUnits,SafeHeightUnits,RailWidthUnits;
        public readonly float AnchorMinX,AnchorMinY,AnchorMaxX,AnchorMaxY;
        public HudLayoutResult(bool compact,float refW,float refH,float scale,float safeW,float safeH,float rail,
            float aMinX,float aMinY,float aMaxX,float aMaxY)
        {
            Compact=compact;ReferenceWidth=refW;ReferenceHeight=refH;CanvasScale=scale;
            SafeWidthUnits=safeW;SafeHeightUnits=safeH;RailWidthUnits=rail;
            AnchorMinX=aMinX;AnchorMinY=aMinY;AnchorMaxX=aMaxX;AnchorMaxY=aMaxY;
        }
        /// <summary>Fraction of the safe width left free for the world (Focus/camera) on the left of the drawer.</summary>
        public float FreeViewportFraction=>SafeWidthUnits<=0?0:Math.Max(0,SafeWidthUnits-RailWidthUnits-HudLayoutPolicy.RailMargin)/SafeWidthUnits;
        public float UnitsToPixels(float units)=>units*CanvasScale;
    }

    /// <summary>
    /// Source of truth for the existing landscape HUD geometry (VIS.2a). Pure math so it is tested
    /// with dotnet outside Unity. Numbers are identical to U6.0.4b FantasyPreviewUI.Layout:
    /// reference 1280x720 (compact 960x540 on mobile or short windows), CanvasScaler matched on height,
    /// right drawer 326 units (compact: 36% of safe width clamped 280..380), never above 48% of safe width.
    /// Portrait is not supported; a narrow window keeps the rail.
    /// </summary>
    public static class HudLayoutPolicy
    {
        public const float ReferenceWidth=1280,ReferenceHeight=720;
        public const float CompactReferenceWidth=960,CompactReferenceHeight=540;
        public const float CompactHeightThreshold=600;
        public const float DesktopRailWidth=326,CompactRailMin=280,CompactRailMax=380;
        public const float CompactRailShare=.36f,MaxRailShare=.48f;
        public const float RailMargin=12,RailTop=72,HeaderHeight=48;
        public const float MinTouchTarget=44,PreferredTouchTarget=48;

        public static bool IsCompact(float screenHeight,bool mobile)=>mobile || screenHeight<CompactHeightThreshold;

        public static HudLayoutResult Compute(float screenWidth,float screenHeight,HudPixelRect safeArea,bool mobile)
        {
            float w=Math.Max(1,screenWidth),h=Math.Max(1,screenHeight);
            var safe=safeArea.IsValid?safeArea:new HudPixelRect(0,0,w,h);
            bool compact=IsCompact(h,mobile);
            float refW=compact?CompactReferenceWidth:ReferenceWidth,refH=compact?CompactReferenceHeight:ReferenceHeight;
            float scale=h/refH;
            float safeW=safe.Width/Math.Max(.01f,scale),safeH=safe.Height/Math.Max(.01f,scale);
            float rail=Math.Min(compact?Clamp(safeW*CompactRailShare,CompactRailMin,CompactRailMax):DesktopRailWidth,safeW*MaxRailShare);
            return new HudLayoutResult(compact,refW,refH,scale,safeW,safeH,rail,safe.X/w,safe.Y/h,safe.XMax/w,safe.YMax/h);
        }

        /// <summary>Width of one tab cell in a horizontal row inside the drawer (drawer padding 10+10, spacing 5).</summary>
        public static float TabCellWidth(float railWidth,int tabs,float padding=20,float spacing=5)
            =>tabs<=0?0:(railWidth-padding-spacing*(tabs-1))/tabs;

        /// <summary>Physical size in millimetres of a logical length at a given screen dpi (0 dpi = unknown).</summary>
        public static float PhysicalMillimetres(float units,float canvasScale,float dpi)
            =>dpi<=0?0:units*canvasScale/dpi*25.4f;

        static float Clamp(float v,float min,float max)=>v<min?min:(v>max?max:v);
    }
}
