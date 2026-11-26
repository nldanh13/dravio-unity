using System;
using System.Collections.Generic;
using System.IO;
using Dravio.UI;
using UnityEditor;
using UnityEngine;

#pragma warning disable 0649 // fields are filled by JsonUtility

namespace Dravio.Editor.UiTools
{
    /// <summary>Editor view of Assets/Dravio/UI/Data/ui-sprite-slots.json (VIS.2a sprite contract).</summary>
    [Serializable] internal sealed class DravioUiSlotManifest
    {
        public const string AssetPath="Assets/Dravio/UI/Data/ui-sprite-slots.json";
        public const string SpritesRoot="Assets/Dravio/UI/Sprites/";

        [Serializable] public sealed class Slot
        {
            public string id,role,path,notes;
            public int sourceWidth,sourceHeight;
            public bool sliced;
            public int[] borderPx;
            public float borderUnits,displayWidthUnits,displayHeightUnits;
            public string[] states;
            public UiBorder Border=>borderPx!=null && borderPx.Length==4?new UiBorder(borderPx[0],borderPx[1],borderPx[2],borderPx[3]):default;
            public DravioUiRole Role=>Enum.TryParse<DravioUiRole>(role,out var r)?r:DravioUiRole.Icon;
            public float DisplayShortUnits=>displayWidthUnits>0?Mathf.Min(displayWidthUnits,displayHeightUnits):displayHeightUnits;
            public string StatePath(string state)=>state=="normal"?path:path.Substring(0,path.Length-4)+"_"+state+".png";
        }
        [Serializable] public sealed class IconSet
        {
            public string pathPattern;
            public int sourceSize;
            public int[] displayUnits;
            public string[] ids;
            public string PathFor(string id)=>pathPattern.Replace("{id}",id);
            public int MaxDisplayUnits{get{int m=0;if(displayUnits!=null)foreach(var d in displayUnits)m=Math.Max(m,d);return m;}}
        }

        public string version;
        public Slot[] slots;
        public IconSet icons,items;

        static DravioUiSlotManifest cached;
        static DateTime cachedStamp;

        public static DravioUiSlotManifest Load()
        {
            var full=Path.GetFullPath(AssetPath);
            if(!File.Exists(full))return null;
            var stamp=File.GetLastWriteTimeUtc(full);
            if(cached!=null && stamp==cachedStamp)return cached;
            cached=JsonUtility.FromJson<DravioUiSlotManifest>(File.ReadAllText(full));
            cachedStamp=stamp;
            return cached;
        }

        /// <summary>Finds the contract entry for an asset path: a slot (including state variants) or an icon/item set.</summary>
        public bool TryDescribe(string assetPath,out Slot slot,out IconSet set)
        {
            slot=null;set=null;
            assetPath=assetPath.Replace('\\','/');
            if(slots!=null)foreach(var s in slots)
            {
                if(s.path==assetPath){slot=s;return true;}
                if(s.states!=null)foreach(var st in s.states)if(s.StatePath(st)==assetPath){slot=s;return true;}
            }
            foreach(var candidate in new[]{icons,items})
            {
                if(candidate?.ids==null)continue;
                foreach(var i in candidate.ids)if(candidate.PathFor(i)==assetPath){set=candidate;return true;}
            }
            return false;
        }

        /// <summary>Import max size from UiSpritePolicy (sliced: border sharpness; simple: short side).</summary>
        public static int RecommendedMaxSize(Slot slot)=>slot.sliced
            ?UiSpritePolicy.RecommendedMaxSizeSliced(slot.sourceWidth,slot.sourceHeight,slot.Border,slot.borderUnits,slot.displayWidthUnits,slot.displayHeightUnits)
            :UiSpritePolicy.RecommendedMaxSize(slot.sourceWidth,slot.sourceHeight,slot.DisplayShortUnits);
        public static int RecommendedMaxSize(IconSet set)=>UiSpritePolicy.RecommendedMaxSize(set.sourceSize,set.sourceSize,set.MaxDisplayUnits);

        public IEnumerable<string> AllKnownPaths()
        {
            if(slots!=null)foreach(var s in slots)
                if(s.states!=null)foreach(var st in s.states)yield return s.StatePath(st);
                else yield return s.path;
            foreach(var set in new[]{icons,items})
                if(set?.ids!=null)foreach(var i in set.ids)yield return set.PathFor(i);
        }
    }
}
