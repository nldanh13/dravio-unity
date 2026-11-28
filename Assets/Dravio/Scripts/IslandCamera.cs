using System.Collections.Generic;
using System.Linq;
using Dravio.EnvironmentArt;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Dravio
{
    /// <summary>
    /// U6.3 — island camera: starts closer than the fitted overview (dragons read at a glance), drag to look
    /// around, pinch or mouse wheel to zoom between <see cref="IslandViewMath.MinZoom"/> and the full overview.
    /// It only feeds <see cref="SanctuaryCameraFit.SetView"/>; the fitted framing, the drawer inset and the
    /// dragon follow camera (which disables the fit while it runs) are unchanged. Presentation only.
    /// Added next to the journey banner by <see cref="IslandHud"/>.
    /// </summary>
    public sealed class IslandCamera : MonoBehaviour
    {
        const float DragThresholdPx=12f;
        readonly HashSet<SanctuaryCameraFit> seen=new HashSet<SanctuaryCameraFit>();
        readonly List<RaycastResult> hits=new List<RaycastResult>();
        SanctuaryCameraFit fit;
        Camera view;
        float zoom=1f;
        Vector2 offset;
        bool pressed,onWorld,dragging;
        Vector2 pressAt,lastPoint;
        float pinchDistance;

        /// <summary>True while a drag is panning the island (taps on dragons are not drags).</summary>
        public bool Dragging=>dragging;

        void Update()
        {
            var cam=Camera.main;
            var f=cam!=null?cam.GetComponent<SanctuaryCameraFit>():null;
            if(f!=fit){fit=f;view=cam;pressed=dragging=false;pinchDistance=0f;if(fit!=null){zoom=fit.ViewZoom;offset=fit.ViewOffset;}}
            if(fit==null || !fit.enabled || view==null || !view.orthographic){pressed=dragging=false;pinchDistance=0f;return;}
            if(fit.FittedSize<=0f)return; // not applied yet
            var free=fit.FreeViewport;
            float halfH=fit.FittedSize,halfW=fit.FittedSize*Mathf.Max(.1f,view.aspect);
            if(seen.Add(fit) && Mathf.Approximately(fit.ViewZoom,1f))
            {
                zoom=IslandViewMath.DefaultZoom;
                IslandViewMath.Centre(zoom,free.xMin,free.xMax,free.yMin,free.yMax,halfW,halfH,out float cx,out float cy);
                offset=new Vector2(cx,cy);
            }
            ReadInput(free,halfW,halfH);
            float ox=offset.x,oy=offset.y;
            IslandViewMath.Clamp(ref ox,ref oy,zoom,free.xMin,free.xMax,free.yMin,free.yMax,halfW,halfH);
            offset=new Vector2(ox,oy);
            fit.SetView(zoom,offset);
        }

        void ReadInput(Rect free,float halfW,float halfH)
        {
            var touch=Touchscreen.current;
            int fingers=0;Vector2 p=default,q=default;
            if(touch!=null)
                foreach(var finger in touch.touches)
                {
                    if(!finger.press.isPressed)continue;
                    if(fingers==0)p=finger.position.ReadValue();else if(fingers==1)q=finger.position.ReadValue();
                    fingers++;
                }
            if(fingers>=2)
            {
                pressed=dragging=false;
                float d=Vector2.Distance(p,q);
                var mid=(p+q)*.5f;
                if(d>8f && pinchDistance>8f && OnWorld(mid,free))
                    ZoomAt(zoom*Mathf.Clamp(pinchDistance/d,.8f,1.25f),mid,free,halfW,halfH);
                pinchDistance=d;
                return;
            }
            pinchDistance=0f;
            bool down;Vector2 point;
            if(fingers==1){down=true;point=p;}
            else
            {
                var mouse=Mouse.current;
                if(mouse==null){pressed=dragging=false;return;}
                point=mouse.position.ReadValue();
                down=mouse.leftButton.isPressed;
                float scroll=mouse.scroll.ReadValue().y;
                if(!Mathf.Approximately(scroll,0f) && OnWorld(point,free))
                    ZoomAt(zoom*Mathf.Exp(-Mathf.Clamp(scroll/120f,-3f,3f)*.18f),point,free,halfW,halfH);
            }
            if(!down){pressed=dragging=false;return;}
            if(!pressed){pressed=true;onWorld=OnWorld(point,free);pressAt=lastPoint=point;dragging=false;return;}
            if(!onWorld)return;
            if(!dragging && (point-pressAt).sqrMagnitude>=DragThresholdPx*DragThresholdPx)dragging=true;
            if(dragging)
            {
                var delta=point-lastPoint;
                float ox=offset.x,oy=offset.y;
                IslandViewMath.Drag(ref ox,ref oy,delta.x,delta.y,Screen.height,zoom,halfH);
                offset=new Vector2(ox,oy);
            }
            lastPoint=point;
        }

        void ZoomAt(float z2,Vector2 screen,Rect free,float halfW,float halfH)
        {
            var v=view.ScreenToViewportPoint(screen);
            float ox=offset.x,oy=offset.y;
            z2=IslandViewMath.ClampZoom(z2);
            IslandViewMath.ZoomAt(ref ox,ref oy,zoom,z2,v.x,v.y,free.xMin,free.xMax,free.yMin,free.yMax,halfW,halfH);
            zoom=z2;offset=new Vector2(ox,oy);
        }

        bool OnWorld(Vector2 screen,Rect free)
        {
            var v=view.ScreenToViewportPoint(screen);
            if(!free.Contains(new Vector2(v.x,v.y)))return false;
            if(EventSystem.current==null)return true;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
            return !hits.Any(h=>h.module is GraphicRaycaster);
        }
    }
}
