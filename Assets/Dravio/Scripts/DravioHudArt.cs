using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    // Art only. Existing buttons, targets, localized Text and listeners are preserved.
    internal static class DravioHudArt
    {
        static readonly Color Ivory = new Color(.98f,.93f,.81f);
        static readonly Color Ink = new Color(.13f,.10f,.06f);

        static bool Skin(Image image,string style)
        {
            if(image==null)return false;
            // Keep the original Image, its raycast, mask and Button target identity.
            // The mesh effect replaces its geometry; CanvasRenderer still applies
            // the original Button's ColorBlock transitions.
            image.sprite=null;
            image.overrideSprite=null;
            image.type=Image.Type.Simple;
            image.material=null;
            image.color=Color.white;
            var effect=image.GetComponent<DravioVectorSkin>();
            if(effect==null)effect=image.gameObject.AddComponent<DravioVectorSkin>();
            effect.SetStyle(style);
            return true;
        }

        static void ButtonStyle(Button button,string style)
        {
            if(button==null || !Skin(button.targetGraphic as Image,style))return;
            var c=button.colors;
            c.normalColor=Color.white;
            c.highlightedColor=new Color(1f,.98f,.90f);
            c.pressedColor=new Color(.73f,.73f,.69f);
            c.selectedColor=c.highlightedColor;
            c.disabledColor=new Color(.57f,.57f,.57f,.78f);
            c.colorMultiplier=1;
            c.fadeDuration=.10f;
            button.colors=c;
            var label=button.GetComponentInChildren<Text>(true);
            if(label!=null){label.color=style=="reward"?Ink:Ivory;label.fontStyle=FontStyle.Bold;}
        }

        internal static void ApplyTab(Button button,bool selected)
        {
            ButtonStyle(button,selected?"reward":"secondary");
        }

        internal static void Apply(RectTransform safe,RectTransform panel,Button[] tabs,Button language,Button collapse)
        {
            if(safe==null || panel==null)return;
            Skin(panel.GetComponent<Image>(),"panel");
            var header=safe.Find("Header");
            if(header!=null)Skin(header.GetComponent<Image>(),"header");
            var layout=panel.GetComponent<VerticalLayoutGroup>();
            if(layout!=null)layout.padding=new RectOffset(14,14,12,12);
            var nav=new HashSet<Button>(tabs ?? Array.Empty<Button>());
            foreach(var button in safe.GetComponentsInChildren<Button>(true))
            {
                if(nav.Contains(button))continue; // selected state is managed by FantasyPreviewUI
                string name=button.gameObject.name;
                string style=name.StartsWith("Claim",StringComparison.Ordinal) || name.StartsWith("Buy",StringComparison.Ordinal)
                    || name.StartsWith("Unlock",StringComparison.Ordinal) ? "reward"
                    : name.StartsWith("Next",StringComparison.Ordinal) || name=="Previous"
                      || name=="Resume exploring" || button==language || button==collapse ? "secondary" : "primary";
                ButtonStyle(button,style);
                if(button==language || button==collapse)continue;
                string id=null;
                bool item=false;
                switch(name)
                {
                    case "Feed":id="feed";break;
                    case "Pet":id="pet";break;
                    case "Doze / auto":case "Doze / wake":id="rest";break;
                    case "Inspect":case "Inspect selected":case "Inspect Emberling":id="inspect";break;
                    case "Resume exploring":id="back";break;
                    case "Next":case "Next dragon":id="next";break;
                    case "Previous":id="back";break;
                    case "Codex":id="biology";break;
                    case "Observe":id="inspect";break;
                    case "Quests":id="quests";break;
                    case "Depart":id="expedition";break;
                    case "Build next project":id="build";break;
                    case "Start breeding":case "Tap egg":id="eggs";break;
                    case "Visit Origin":case "Visit Ocean":case "Visit Sky":id="world";break;
                    case "Start care":case "Assess case":id="health";break;
                }
                if(name.StartsWith("Starter ·",StringComparison.Ordinal)){id="starter";item=true;}
                else if(name.StartsWith("Elemental ·",StringComparison.Ordinal)){id="elemental";item=true;}
                else if(name.StartsWith("Mystic ·",StringComparison.Ordinal)){id="mystic";item=true;}
                else if(name.StartsWith("Astral ·",StringComparison.Ordinal)){id="astral";item=true;}
                else if(name.StartsWith("Claim",StringComparison.Ordinal))id="reward";
                if(id!=null)AddIcon(button,id,item);
            }
        }

        static void AddIcon(Button button,string id,bool item)
        {
            var label=button.GetComponentInChildren<Text>(true);
            if(label==null || button.transform.Find("Dravio Art Icon")!=null)return;
            var go=new GameObject("Dravio Art Icon",typeof(RectTransform),typeof(Image));
            go.transform.SetParent(button.transform,false);
            var rt=go.GetComponent<RectTransform>();
            rt.anchorMin=rt.anchorMax=new Vector2(0,.5f);
            rt.pivot=new Vector2(0,.5f);
            rt.anchoredPosition=new Vector2(12,0);
            rt.sizeDelta=new Vector2(21,21);
            var image=go.GetComponent<Image>();
            image.raycastTarget=false;
            Skin(image,"icon/"+id);
            var effect=button.GetComponent<DravioVectorSkin>();
            if(effect!=null)effect.ConfigureIconLayout(rt,label.rectTransform);
            // The parent effect reserves space only when the button is wide enough.
        }
    }
}
