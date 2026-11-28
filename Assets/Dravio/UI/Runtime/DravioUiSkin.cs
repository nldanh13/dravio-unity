using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio.UI
{
    /// <summary>
    /// Applies a DravioUiTheme to existing uGUI objects without changing their RectTransforms, layout,
    /// raycast area, serialized callbacks or text content (VIS.2a).
    /// Rules: sliced roles use Image.Type.Sliced with a per-sprite pixelsPerUnitMultiplier; icons are Simple +
    /// preserveAspect; a missing sprite or an unusable border falls back to the flat colour (never a stretched
    /// corner). Buttons with art get a non-raycast "Skin visual" child so the press scale never moves the hit area.
    /// </summary>
    public static class DravioUiSkin
    {
        public const string VisualName="Skin visual";
        public const string IndicatorName="Skin active indicator";
        static readonly HashSet<string> warned=new HashSet<string>();

        /// <summary>Panel, header, page background, card, slot, divider, progress frame.</summary>
        public static void ApplySurface(Image image,DravioUiTheme theme,DravioUiRole role)
        {
            if(image==null || theme==null)return;
            var slot=theme.Find(role);
            if(slot==null)return;
            if(Drawable(slot,image))ApplySprite(image,slot.sprite,slot,theme);
            else ApplyFlat(image,slot.fallback);
        }

        /// <summary>Configures an Image for a sprite of the given slot (type, aspect, multiplier). Does not touch raycastTarget.</summary>
        public static void ApplySprite(Image image,Sprite sprite,DravioUiTheme.SpriteSlot slot,DravioUiTheme theme)
        {
            image.sprite=sprite;
            image.color=slot.tint;
            bool sliced=DravioUiRoles.IsSliced(slot.role);
            image.type=sliced?Image.Type.Sliced:Image.Type.Simple;
            image.fillCenter=true;
            image.preserveAspect=!sliced && DravioUiRoles.PreserveAspect(slot.role);
            var b=sprite.border;
            float maxBorder=Mathf.Max(Mathf.Max(b.x,b.y),Mathf.Max(b.z,b.w));
            var canvas=image.canvas;
            float referencePpu=canvas!=null?canvas.referencePixelsPerUnit:100f;
            image.pixelsPerUnitMultiplier=sliced?UiSpritePolicy.PixelsPerUnitMultiplier(maxBorder,slot.borderUnits,sprite.pixelsPerUnit,referencePpu):1f;
        }

        static void ApplyFlat(Image image,Color color)
        {
            image.sprite=null;image.type=Image.Type.Simple;image.preserveAspect=false;
            image.pixelsPerUnitMultiplier=1f;image.color=color;
        }

        /// <summary>Buttons keep their root Image as the hit area; art goes to a child visual.</summary>
        public static void ApplyButton(Button button,DravioUiTheme theme,DravioUiRole role)
        {
            if(button==null || theme==null)return;
            var slot=theme.Find(role);
            if(slot==null)return;
            var hit=button.GetComponent<Image>();
            var colors=button.colors;
            colors.fadeDuration=theme.fadeDuration;
            colors.colorMultiplier=1f;
            if(hit!=null && Drawable(slot,hit))
            {
                var visual=EnsureVisual(button.transform,hit);
                ApplySprite(visual,slot.sprite,slot,theme);
                // Keep the full rect raycastable while drawing nothing on the root.
                hit.sprite=null;hit.type=Image.Type.Simple;hit.color=new Color(1,1,1,0);hit.raycastTarget=true;
                hit.canvasRenderer.cullTransparentMesh=true;
                bool swap=slot.pressed!=null || slot.highlighted!=null || slot.disabled!=null;
                button.targetGraphic=visual;
                if(swap)
                {
                    button.transition=Selectable.Transition.SpriteSwap;
                    var state=button.spriteState;
                    state.highlightedSprite=slot.highlighted;state.selectedSprite=slot.highlighted;
                    state.pressedSprite=slot.pressed;state.disabledSprite=slot.disabled;
                    button.spriteState=state;
                }
                else button.transition=Selectable.Transition.ColorTint;
                colors.normalColor=Color.white;colors.highlightedColor=theme.highlighted;colors.selectedColor=theme.highlighted;
                colors.pressedColor=theme.pressed;colors.disabledColor=theme.disabled;
            }
            else
            {
                RemoveVisual(button.transform,hit);
                if(hit!=null){hit.sprite=null;hit.type=Image.Type.Simple;hit.color=Color.white;hit.raycastTarget=true;hit.canvasRenderer.cullTransparentMesh=false;button.targetGraphic=hit;}
                button.transition=Selectable.Transition.ColorTint;
                var f=slot.fallback;
                colors.normalColor=f;
                colors.highlightedColor=Color.Lerp(f,Color.white,.25f);colors.selectedColor=colors.highlightedColor;
                colors.pressedColor=Color.Lerp(f,Color.black,.2f);
                colors.disabledColor=Desaturate(f,.68f);
            }
            button.colors=colors;
            var label=button.GetComponentInChildren<Text>(true);
            if(label!=null){label.color=slot.label;label.fontStyle=FontStyle.Bold;}
            var tmp=FindTmpLabel(button);
            if(tmp!=null)tmp.color=slot.label;
            var press=button.GetComponent<DravioPressFeedback>();
            if(press==null)press=button.gameObject.AddComponent<DravioPressFeedback>();
            press.Configure(theme.reduceMotion?1f:theme.pressedScale);
        }

        /// <summary>Tab state: active differs by sprite/colour AND by an indicator shape, never by colour alone.</summary>
        public static void ApplyTab(Button tab,DravioUiTheme theme,bool active)
        {
            if(tab==null || theme==null)return;
            ApplyButton(tab,theme,active?DravioUiRole.TabActive:DravioUiRole.Tab);
            var label=tab.GetComponentInChildren<Text>(true);
            var labelColor=theme.TextColorOr(active?DravioTextRole.TabLabelActive:DravioTextRole.TabLabel,label!=null?label.color:Color.white);
            if(label!=null)label.color=labelColor;
            var indicator=EnsureIndicator(tab.transform);
            var slot=theme.Find(DravioUiRole.TabIndicator);
            if(slot!=null && Drawable(slot,indicator))ApplySprite(indicator,slot.sprite,slot,theme);
            else ApplyFlat(indicator,slot!=null?slot.fallback:labelColor);
            indicator.gameObject.SetActive(active);
        }

        public static void ApplyText(Graphic text,DravioUiTheme theme,DravioTextRole role)
        {
            if(text==null || theme==null)return;
            var t=theme.FindText(role);
            if(t==null)return;
            text.color=t.color;
            if(t.bold && text is Text legacy)legacy.fontStyle=FontStyle.Bold;
        }

        static bool Drawable(DravioUiTheme.SpriteSlot slot,Object context)
        {
            if(slot.sprite==null)return false;
            if(DravioUiTheme.CanDraw(slot,out var reason))return true;
            string key=slot.role+":"+slot.sprite.name;
            if(warned.Add(key))
                Debug.LogWarning("Dravio UI theme: "+slot.role+" sprite '"+slot.sprite.name+"' not used ("+reason+"); flat fallback shown.",context);
            return false;
        }

        static Image EnsureVisual(Transform parent,Image hit)
        {
            var existing=parent.Find(VisualName);
            Image visual;
            if(existing!=null)visual=existing.GetComponent<Image>();
            else
            {
                var go=new GameObject(VisualName,typeof(RectTransform),typeof(Image));
                go.transform.SetParent(parent,false);
                go.transform.SetSiblingIndex(0);
                var layout=go.AddComponent<LayoutElement>();layout.ignoreLayout=true;
                visual=go.GetComponent<Image>();
            }
            var r=(RectTransform)visual.transform;
            r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=Vector2.zero;r.offsetMax=Vector2.zero;
            visual.raycastTarget=false;
            if(hit!=null)visual.maskable=hit.maskable;
            return visual;
        }

        static void RemoveVisual(Transform parent,Image _)
        {
            var existing=parent.Find(VisualName);
            if(existing==null)return;
            existing.gameObject.SetActive(false);
        }

        static Image EnsureIndicator(Transform parent)
        {
            var existing=parent.Find(IndicatorName);
            if(existing!=null)return existing.GetComponent<Image>();
            var go=new GameObject(IndicatorName,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(parent,false);
            go.AddComponent<LayoutElement>().ignoreLayout=true;
            var r=(RectTransform)go.transform;
            // A short bar along the bottom edge; inside the rect so the hit area is unchanged.
            r.anchorMin=new Vector2(.25f,0);r.anchorMax=new Vector2(.75f,0);r.pivot=new Vector2(.5f,0);
            r.anchoredPosition=new Vector2(0,3);r.sizeDelta=new Vector2(0,4);
            var img=go.GetComponent<Image>();img.raycastTarget=false;
            return img;
        }

        static Graphic FindTmpLabel(Button button)
        {
            foreach(var g in button.GetComponentsInChildren<Graphic>(true))
                if(g.GetType().Name=="TextMeshProUGUI")return g;
            return null;
        }

        static Color Desaturate(Color c,float alpha)
        {
            float l=c.r*.299f+c.g*.587f+c.b*.114f;
            var grey=new Color(l,l,l,alpha);
            return Color.Lerp(new Color(c.r,c.g,c.b,alpha),grey,.65f);
        }
    }
}
