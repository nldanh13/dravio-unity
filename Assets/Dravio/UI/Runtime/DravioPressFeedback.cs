using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dravio.UI
{
    /// <summary>
    /// Visual press feedback: scales the button's children (art + label) to ~0.97 while pressed.
    /// The root RectTransform, which is the raycast/hit area and the layout element, is never scaled.
    /// No Update loop, no allocation per press.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DravioPressFeedback : MonoBehaviour,IPointerDownHandler,IPointerUpHandler,IPointerExitHandler
    {
        [SerializeField,Range(.9f,1f)] float pressedScale=.97f;
        Selectable selectable;
        bool pressed;

        public void Configure(float scale){pressedScale=Mathf.Clamp(scale,.9f,1f);if(pressed)Apply(true);}

        void Awake()=>selectable=GetComponent<Selectable>();
        void OnDisable(){if(pressed)Apply(false);}

        public void OnPointerDown(PointerEventData e)
        {
            if(selectable!=null && !selectable.IsInteractable())return;
            Apply(true);
        }
        public void OnPointerUp(PointerEventData e)=>Apply(false);
        public void OnPointerExit(PointerEventData e)=>Apply(false);

        void Apply(bool down)
        {
            pressed=down;
            var s=down?pressedScale:1f;
            var scale=new Vector3(s,s,1f);
            for(int i=0;i<transform.childCount;i++)transform.GetChild(i).localScale=scale;
        }
    }
}
