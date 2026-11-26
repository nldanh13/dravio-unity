using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Dravio.Editor
{
    internal static class CarePreviewHud
    {
        public static void Build(GameObject dragon, Animator animator, DragonTurntable turntable)
        {
            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Care Preview Canvas", typeof(Canvas), typeof(CanvasScaler));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280,720);
                scaler.matchWidthOrHeight = .5f;
            }
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
            var eventSystem = Object.FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
                eventSystem = new GameObject("Care Preview EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
            foreach (var module in eventSystem.GetComponents<BaseInputModule>())
                if (!(module is InputSystemUIInputModule)) module.enabled = false;
            var input = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (input == null) input = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            input.enabled = true;
            if (input.actionsAsset == null) input.AssignDefaultActions();

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            foreach (var text in canvas.GetComponentsInChildren<Text>(true))
                if (text.text.Contains("Drag to rotate"))
                    text.text = "Care preview - F: Feed / P: Pet / N: Doze / Space: Inspect";
            var panel = new GameObject("Care controls", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform,false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1,1);rect.anchorMax = new Vector2(1,1);
            rect.pivot = new Vector2(1,1);rect.anchoredPosition = new Vector2(-16,-16);
            rect.sizeDelta = new Vector2(204,248);
            panel.GetComponent<Image>().color = new Color(.18f,.13f,.09f,.91f);
            var title = Label(panel.transform,font,"EMBERLING",16,new Color(1,.85f,.56f));
            Place(title.rectTransform,8,8,188,22);
            var status = Label(panel.transform,font,"Resting",14,new Color(1,.95f,.82f));
            Place(status.rectTransform,8,33,188,23);
            var feed = ButtonAt(panel.transform,font,"Feed (F)",63);
            var pet = ButtonAt(panel.transform,font,"Pet (P)",99);
            var sleep = ButtonAt(panel.transform,font,"Doze / wake (N)",135);
            var inspect = ButtonAt(panel.transform,font,"Inspect (Space)",171);
            var resume = ButtonAt(panel.transform,font,"Resume exploring",207);
            dragon.AddComponent<DragonCarePreview>().Configure(animator,turntable,status,feed,pet,sleep,inspect,resume);
        }

        static void Place(RectTransform r,float x,float y,float w,float h)
        {
            r.anchorMin=new Vector2(0,1);r.anchorMax=new Vector2(0,1);
            r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);
            r.sizeDelta=new Vector2(w,h);
        }

        static Text Label(Transform parent,Font font,string value,int size,Color color)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(Text));
            go.transform.SetParent(parent,false);
            var text=go.GetComponent<Text>();text.font=font;text.text=value;text.fontSize=size;
            text.color=color;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=10;text.resizeTextMaxSize=size;
            return text;
        }

        static Button ButtonAt(Transform parent,Font font,string name,float y)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));
            go.transform.SetParent(parent,false);
            Place(go.GetComponent<RectTransform>(),8,y,188,31);
            var image=go.GetComponent<Image>();image.color=Color.white;
            var button=go.GetComponent<Button>();button.targetGraphic=image;
            var colors=button.colors;
            colors.normalColor=new Color(.79f,.68f,.45f);
            colors.highlightedColor=new Color(.95f,.83f,.57f);
            colors.pressedColor=new Color(.63f,.54f,.34f);
            colors.selectedColor=colors.highlightedColor;
            button.colors=colors;
            var text=Label(go.transform,font,name,14,new Color(.18f,.12f,.07f));
            text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
            text.rectTransform.offsetMin=Vector2.zero;text.rectTransform.offsetMax=Vector2.zero;
            return button;
        }
    }
}
