using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    // Art only. Existing buttons, targets, localized Text and listeners are preserved.
    internal static class DravioHudArt
    {
        const string Root = "DravioUI/";
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
        static readonly string[] Functions = { "care", "biology", "dragons", "eggs", "world", "quests", "expedition", "inventory", "feed", "pet", "rest", "inspect", "build", "research", "health", "settings", "day", "night", "lock", "done", "warning", "upgrade", "reward", "hunger", "water", "energy", "temperature", "bond", "leaf", "close", "back", "next" };
        static readonly string[] Items = { "gold", "wood", "seed", "stone", "crystal", "coral", "stardust", "fruit", "leaf", "aquatic", "aether", "starter", "elemental", "mystic", "astral", "blueprint", "egg" };
        static readonly Color Ivory = new Color(.98f,.93f,.81f);
        static readonly Color Ink = new Color(.13f,.10f,.06f);

        static Texture2D Texture(string name)
        {
            if (!Textures.TryGetValue(name, out var tex))
            {
                tex = Resources.Load<Texture2D>(Root + name);
                Textures[name] = tex;
            }
            return tex;
        }

        static Sprite Slice(string name)
        {
            if (Sprites.TryGetValue(name, out var sprite) && sprite != null) return sprite;
            var t = Texture(name);
            if (t == null) return null;
            // Crop the transparent margins from the original source images.
            // Coordinates normalized against their known source dimensions.
            Rect rect = new Rect(0,0,t.width,t.height);
            Vector4 border = new Vector4(60,42,60,42);
            float ppu = 300f;
            if (name == "danger")
            {
                rect = new Rect(0,t.height*.34f,t.width,t.height*.32f);
                border = new Vector4(25,20,25,20);
                ppu = 150f;
            }
            else if (name == "panel")
            {
                border = new Vector4(36,36,36,36);
                ppu = 200f;
            }
            else if (name == "primary" || name == "reward" || name == "secondary")
            {
                rect = new Rect(t.width*.045f,t.height*.10f,t.width*.91f,t.height*.80f);
            }
            sprite = Sprite.Create(t,rect,new Vector2(.5f,.5f),ppu,0,SpriteMeshType.FullRect,border,false);
            sprite.name = "Dravio UI " + name;
            Sprites[name] = sprite;
            return sprite;
        }

        internal static Sprite Icon(string id, bool item = false)
        {
            string key = (item ? "item/" : "function/") + id;
            if (Sprites.TryGetValue(key,out var sprite) && sprite != null) return sprite;
            int index = Array.IndexOf(item ? Items : Functions,id);
            if (index < 0) return null;
            var t = Texture(item ? "item_icons" : "function_icons");
            if (t == null || t.width % 8 != 0) return null;
            int size=t.width/8;
            int row=index/8;
            if (t.height < (row+1)*size) return null;
            sprite=Sprite.Create(t,new Rect((index%8)*size,t.height-(row+1)*size,size,size),
                                 new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            sprite.name=key;
            Sprites[key]=sprite;
            return sprite;
        }

        static bool Skin(Image image,string style)
        {
            if(image==null)return false;
            var sprite=Slice(style);
            if(sprite==null)return false;
            image.sprite=sprite;
            image.type=Image.Type.Sliced;
            image.preserveAspect=false;
            image.color=Color.white;
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
            var sprite=Icon(id,item);
            var label=button.GetComponentInChildren<Text>(true);
            if(sprite==null || label==null || button.transform.Find("Dravio Art Icon")!=null)return;
            var go=new GameObject("Dravio Art Icon",typeof(RectTransform),typeof(Image));
            go.transform.SetParent(button.transform,false);
            var rt=go.GetComponent<RectTransform>();
            rt.anchorMin=rt.anchorMax=new Vector2(0,.5f);
            rt.pivot=new Vector2(0,.5f);
            rt.anchoredPosition=new Vector2(12,0);
            rt.sizeDelta=new Vector2(21,21);
            var image=go.GetComponent<Image>();
            image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
            // Keep the translated label separate and leave room for the decorative icon.
            var lr=label.rectTransform;
            lr.offsetMin=new Vector2(35,lr.offsetMin.y);
            lr.offsetMax=new Vector2(-9,lr.offsetMax.y);
        }
    }
}
