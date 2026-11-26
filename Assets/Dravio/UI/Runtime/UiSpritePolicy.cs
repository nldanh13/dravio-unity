using System;
using System.Collections.Generic;

namespace Dravio.UI
{
    /// <summary>Sprite border in source pixels, Unity order: left, bottom, right, top.</summary>
    public readonly struct UiBorder
    {
        public readonly int Left,Bottom,Right,Top;
        public UiBorder(int l,int b,int r,int t){Left=l;Bottom=b;Right=r;Top=t;}
        public bool IsZero=>Left==0 && Bottom==0 && Right==0 && Top==0;
        public int MaxSide=>Math.Max(Math.Max(Left,Right),Math.Max(Top,Bottom));
        public override string ToString()=>"L"+Left+" B"+Bottom+" R"+Right+" T"+Top;
    }

    /// <summary>
    /// Pure rules for sprite slots (VIS.2a): 9-slice border sanity, the Image.pixelsPerUnitMultiplier that
    /// renders a per-sprite border at its design size, and the smallest import size that does not upscale
    /// at the largest supported canvas scale. No UnityEngine dependency (tested with dotnet).
    /// </summary>
    public static class UiSpritePolicy
    {
        /// <summary>Largest canvas scale among supported targets: compact 960x540 reference on a 1440p phone.</summary>
        public const float MaxCanvasScale=1440f/540f;
        /// <summary>Above this ratio of imported to displayed pixels, mipmap-off UI starts to alias/shimmer.</summary>
        public const float MaxDownsample=2.5f;
        public static readonly int[] ImportSizes={32,64,128,256,512,1024,2048};

        /// <summary>Problems with a 9-slice border; empty list means usable.</summary>
        public static List<string> ValidateBorder(int width,int height,UiBorder b,bool sliced)
        {
            var issues=new List<string>();
            if(width<=0 || height<=0){issues.Add("sprite has no pixels");return issues;}
            if(b.Left<0||b.Right<0||b.Top<0||b.Bottom<0)issues.Add("negative border");
            if(!sliced)
            {
                if(!b.IsZero)issues.Add("border set on a non-sliced slot (icons must be Simple + preserveAspect)");
                return issues;
            }
            if(b.IsZero)issues.Add("sliced slot has no border: corners would stretch");
            if(b.Left+b.Right>=width)issues.Add("left+right border >= width: no stretchable centre");
            if(b.Top+b.Bottom>=height)issues.Add("top+bottom border >= height: no stretchable centre");
            return issues;
        }

        /// <summary>
        /// Image.pixelsPerUnitMultiplier so that a border of borderPixels (as reported by Sprite.border) renders as
        /// designUnits canvas units. uGUI draws a border at border / (spritePpu / referencePpu * multiplier), so the
        /// sprite's own pixels-per-unit (which Unity rescales when maxTextureSize downsizes an import) is included.
        /// </summary>
        public static float PixelsPerUnitMultiplier(float borderPixels,float designUnits,float spritePixelsPerUnit=100f,float referencePixelsPerUnit=100f)
        {
            if(borderPixels<=0 || designUnits<=0 || spritePixelsPerUnit<=0 || referencePixelsPerUnit<=0)return 1f;
            return borderPixels*referencePixelsPerUnit/(spritePixelsPerUnit*designUnits);
        }

        /// <summary>Displayed pixels of a logical length at a canvas scale.</summary>
        public static float DisplayPixels(float units,float canvasScale)=>units*canvasScale;

        /// <summary>
        /// Smallest power-of-two max texture size whose import keeps the short side at or above the pixels
        /// needed at maxCanvasScale. Never exceeds the source (no upscaling of art).
        /// </summary>
        public static int RecommendedMaxSize(int sourceWidth,int sourceHeight,float displayUnitsShortSide,float maxCanvasScale=MaxCanvasScale)
        {
            int longest=Math.Max(sourceWidth,sourceHeight),shortest=Math.Min(sourceWidth,sourceHeight);
            if(longest<=0)return ImportSizes[0];
            float needed=displayUnitsShortSide*maxCanvasScale;
            foreach(var size in ImportSizes)
            {
                if(size>=longest)return CeilPot(longest);
                float importedShort=shortest*(size/(float)longest);
                if(importedShort>=needed)return size;
            }
            return CeilPot(longest);
        }

        /// <summary>
        /// 9-slice version: only the corners/edges must stay sharp (the centre is a gentle stretchable texture).
        /// Axis with a border: imported border px >= borderUnits*scale. Axis without border (e.g. a divider's
        /// height): imported size >= display size. Never exceeds the source.
        /// </summary>
        public static int RecommendedMaxSizeSliced(int sourceWidth,int sourceHeight,UiBorder border,float borderUnits,
            float displayWidthUnits,float displayHeightUnits,float maxCanvasScale=MaxCanvasScale)
        {
            int longest=Math.Max(sourceWidth,sourceHeight);
            if(longest<=0)return ImportSizes[0];
            float need=0; // required import scale factor (imported/source)
            int bx=Math.Max(border.Left,border.Right),by=Math.Max(border.Top,border.Bottom);
            if(bx>0)need=Math.Max(need,borderUnits*maxCanvasScale/bx);
            else if(displayWidthUnits>0)need=Math.Max(need,displayWidthUnits*maxCanvasScale/sourceWidth);
            if(by>0)need=Math.Max(need,borderUnits*maxCanvasScale/by);
            else if(displayHeightUnits>0)need=Math.Max(need,displayHeightUnits*maxCanvasScale/sourceHeight);
            foreach(var size in ImportSizes)
            {
                if(size>=longest)return CeilPot(longest);
                if(size/(float)longest>=need)return size;
            }
            return CeilPot(longest);
        }

        /// <summary>Imported border px / displayed border px for a sliced sprite.</summary>
        public static float BorderSampleRatio(int importedBorderPx,float borderUnits,float canvasScale)
        {
            float display=borderUnits*canvasScale;
            return display<=0?0:importedBorderPx/display;
        }

        /// <summary>Ratio imported/displayed pixels (1 = pixel exact, below 1 = blurry upscale).</summary>
        public static float SampleRatio(int importedShortSide,float displayUnitsShortSide,float canvasScale)
        {
            float display=displayUnitsShortSide*canvasScale;
            return display<=0?0:importedShortSide/display;
        }

        /// <summary>Atlas padding from the guide: 4 px without mipmaps, 8 px when mips or block compression bleed.</summary>
        public static int AtlasPadding(bool mipmaps,bool blockCompressed)=>mipmaps||blockCompressed?8:4;

        static int CeilPot(int v){int p=1;while(p<v)p<<=1;return Math.Min(p,2048);}
    }
}
