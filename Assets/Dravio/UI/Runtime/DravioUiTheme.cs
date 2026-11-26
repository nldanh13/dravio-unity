using System;
using UnityEngine;

namespace Dravio.UI
{
    /// <summary>
    /// Swappable UI skin (VIS.2a). Holds one entry per DravioUiRole: approved sprite(s) from AI3, the border's
    /// design size in canvas units, tints and a flat fallback colour used when art is missing or unsafe to slice.
    /// A theme never changes layout, callbacks, text content, localisation or gameplay.
    /// </summary>
    [CreateAssetMenu(menuName="Dravio/UI/Theme",fileName="DravioUiTheme")]
    public sealed class DravioUiTheme : ScriptableObject
    {
        [Serializable] public sealed class SpriteSlot
        {
            public DravioUiRole role;
            [Tooltip("Normal state. Empty = flat fallback colour (current HUD look).")] public Sprite sprite;
            [Tooltip("Optional sprite swap states; empty = colour tint on the normal sprite.")] public Sprite highlighted,pressed,disabled;
            [Tooltip("On-screen size, in canvas units, of the sprite's largest border side. Sets Image.pixelsPerUnitMultiplier per sprite.")]
            [Min(1)] public float borderUnits=16;
            public Color tint=Color.white;
            [Tooltip("Flat colour when the sprite is missing or its border is unusable.")] public Color fallback=Color.gray;
            [Tooltip("Label colour drawn on this surface.")] public Color label=Color.black;
        }
        [Serializable] public sealed class TextColor
        {
            public DravioTextRole role;
            public Color color=Color.white;
            public bool bold;
        }

        [Tooltip("Palette preset this asset was created from, for the PR/QA record.")] public string presetName="Legacy U6.0.4b";
        public SpriteSlot[] slots=Array.Empty<SpriteSlot>();
        public TextColor[] text=Array.Empty<TextColor>();
        [Header("Button states (multiplied with the tint)")]
        public Color highlighted=Color.white;
        public Color pressed=new Color(.86f,.86f,.86f,1f);
        public Color disabled=new Color(.72f,.72f,.72f,.78f);
        [Range(0,.3f)] public float fadeDuration=.10f;
        [Tooltip("Visual-only press scale of the children; the hit area is never animated.")]
        [Range(.9f,1f)] public float pressedScale=.97f;
        [Tooltip("Accessibility: disables press scale/sheen animation.")] public bool reduceMotion;

        public SpriteSlot Find(DravioUiRole role)
        {
            if(slots!=null)foreach(var s in slots)if(s!=null && s.role==role)return s;
            return null;
        }
        public TextColor FindText(DravioTextRole role)
        {
            if(text!=null)foreach(var t in text)if(t!=null && t.role==role)return t;
            return null;
        }
        public Color TextColorOr(DravioTextRole role,Color fallback)=>FindText(role)?.color??fallback;

        /// <summary>True when the slot has a sprite that can be drawn safely for its role.</summary>
        public static bool CanDraw(SpriteSlot slot,out string reason)
        {
            reason=null;
            if(slot==null || slot.sprite==null){reason="no sprite";return false;}
            var rect=slot.sprite.rect;var b=slot.sprite.border;
            var issues=UiSpritePolicy.ValidateBorder((int)rect.width,(int)rect.height,
                new UiBorder((int)b.x,(int)b.y,(int)b.z,(int)b.w),DravioUiRoles.IsSliced(slot.role));
            if(issues.Count==0)return true;
            reason=string.Join("; ",issues);
            return false;
        }

        // Presets -----------------------------------------------------------------------------------------
        /// <summary>Same colours as the current U6.0.4b runtime HUD: assigning it without sprites is visually neutral.</summary>
        public void ResetToLegacyPalette()
        {
            presetName="Legacy U6.0.4b";
            var ink=new Color(.075f,.13f,.12f,.97f);var accent=new Color(.83f,.68f,.40f);
            var ivory=new Color(.96f,.91f,.79f);var buttonInk=new Color(.10f,.18f,.16f);
            var tabIdle=new Color(.19f,.29f,.25f);
            slots=BuildSlots(ink,accent,tabIdle,new Color(.035f,.075f,.065f,.65f),buttonInk,ivory);
            text=new[]{T(DravioTextRole.Title,accent,true),T(DravioTextRole.Body,ivory),T(DravioTextRole.Muted,new Color(.76f,.70f,.56f)),
                T(DravioTextRole.ButtonLabel,buttonInk,true),T(DravioTextRole.TabLabel,ivory,true),T(DravioTextRole.TabLabelActive,buttonInk,true)};
        }

        /// <summary>
        /// Fallback colours from Art Style Guide v1.0 section 02/06 (PR #85, not yet merged): UI cream #FFF0CA,
        /// wood #865433, honey #F3BB56, green #4CA85B, dark ink #49372C. Proposed only; AI3/owner approve.
        /// </summary>
        public void ResetToBrightFantasyV1()
        {
            presetName="Bright Fantasy v1 (proposed, PR #85)";
            var cream=Hex(0xFFF0CA);var wood=Hex(0x865433);var honey=Hex(0xF3BB56);var green=Hex(0x4CA85B);var ink=Hex(0x49372C);
            var sky=Hex(0x8CCFF1);var coral=Hex(0xE5735F);
            slots=BuildSlots(new Color(wood.r,wood.g,wood.b,.97f),green,Hex(0xEAD9B0),new Color(cream.r,cream.g,cream.b,.92f),ink,cream);
            Set(DravioUiRole.SecondaryButton,cream,ink);Set(DravioUiRole.RewardButton,honey,ink);
            Set(DravioUiRole.DangerButton,coral,Color.white);Set(DravioUiRole.TabActive,honey,ink);
            Set(DravioUiRole.Tab,Hex(0xEAD9B0),ink);Set(DravioUiRole.ProgressFill,green,ink);
            Set(DravioUiRole.SlotRare,sky,ink);Set(DravioUiRole.SlotEpic,Hex(0xB89BDB),ink);Set(DravioUiRole.SlotLegendary,honey,ink);
            text=new[]{T(DravioTextRole.Title,honey,true),T(DravioTextRole.Body,cream),T(DravioTextRole.Muted,Hex(0xEAD9B0)),
                T(DravioTextRole.ButtonLabel,ink,true),T(DravioTextRole.TabLabel,ink,true),T(DravioTextRole.TabLabelActive,ink,true)};
        }

        SpriteSlot[] BuildSlots(Color surface,Color accent,Color tabIdle,Color page,Color onAccent,Color onSurface)
        {
            var roles=(DravioUiRole[])Enum.GetValues(typeof(DravioUiRole));
            var result=new SpriteSlot[roles.Length];
            for(int i=0;i<roles.Length;i++)
            {
                var r=roles[i];
                var fill=r switch
                {
                    DravioUiRole.Panel or DravioUiRole.HeaderBar or DravioUiRole.Card or DravioUiRole.ProgressFrame=>surface,
                    DravioUiRole.PageBackground=>page,
                    DravioUiRole.Tab=>tabIdle,
                    DravioUiRole.Divider=>new Color(accent.r,accent.g,accent.b,.6f),
                    _=>accent,
                };
                bool onDark=r is DravioUiRole.Panel or DravioUiRole.HeaderBar or DravioUiRole.Card or DravioUiRole.PageBackground or DravioUiRole.Tab;
                result[i]=new SpriteSlot{role=r,fallback=fill,label=onDark?onSurface:onAccent,borderUnits=DefaultBorderUnits(r)};
            }
            return result;
        }
        void Set(DravioUiRole role,Color fill,Color label){var s=Find(role);if(s!=null){s.fallback=fill;s.label=label;}}

        /// <summary>Design border sizes in canvas units for the 1280x720 reference (see ui-sprite-slots.json).</summary>
        public static float DefaultBorderUnits(DravioUiRole r)=>r switch
        {
            DravioUiRole.Panel=>24,
            DravioUiRole.HeaderBar=>18,
            DravioUiRole.Card or DravioUiRole.SlotCommon or DravioUiRole.SlotRare or DravioUiRole.SlotEpic or DravioUiRole.SlotLegendary=>16,
            DravioUiRole.Tab or DravioUiRole.TabActive=>14,
            DravioUiRole.ProgressFrame=>10,
            DravioUiRole.ProgressFill=>7,
            DravioUiRole.Divider=>8,
            DravioUiRole.PageBackground=>12,
            _=>16,
        };

        static TextColor T(DravioTextRole r,Color c,bool bold=false)=>new TextColor{role=r,color=c,bold=bold};
        static Color Hex(int rgb)=>new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1f);

        void Reset()=>ResetToLegacyPalette();
    }
}
