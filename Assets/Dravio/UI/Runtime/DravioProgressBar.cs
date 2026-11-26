using UnityEngine;
using UnityEngine.UI;

namespace Dravio.UI
{
    /// <summary>
    /// Progress bar with separate frame, fill and optional highlight (VIS.2a). The frame is never stretched by
    /// value. Filled mode uses Image.Type.Filled (cheap, clips the art's end cap); Masked mode keeps a sliced fill
    /// at full size inside a RectMask2D whose width follows the value, so rounded caps stay intact.
    /// Presentation only: it displays a value, it does not compute progress.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DravioProgressBar : MonoBehaviour
    {
        public enum FillMode{Filled=0,Masked=1}
        [SerializeField] Image frame,fill,highlight;
        [SerializeField] RectTransform mask;
        [SerializeField] FillMode mode=FillMode.Filled;
        [SerializeField,Range(0,1)] float value;
        [SerializeField] DravioUiTheme theme;

        public float Value=>value;

        public void Configure(Image frameImage,Image fillImage,Image highlightImage,RectTransform maskRect,FillMode fillMode,DravioUiTheme skin)
        {
            frame=frameImage;fill=fillImage;highlight=highlightImage;mask=maskRect;mode=fillMode;theme=skin;
            ApplyTheme();SetValue01(value);
        }

        public void ApplyTheme()
        {
            if(theme==null)return;
            DravioUiSkin.ApplySurface(frame,theme,DravioUiRole.ProgressFrame);
            DravioUiSkin.ApplySurface(fill,theme,DravioUiRole.ProgressFill);
            if(highlight!=null)
            {
                var slot=theme.Find(DravioUiRole.ProgressHighlight);
                highlight.gameObject.SetActive(slot!=null && slot.sprite!=null);
                if(slot!=null && slot.sprite!=null)DravioUiSkin.ApplySprite(highlight,slot.sprite,slot,theme);
            }
            foreach(var img in new[]{frame,fill,highlight})if(img!=null)img.raycastTarget=false;
        }

        public void SetValue01(float v)
        {
            value=Mathf.Clamp01(float.IsNaN(v)?0:v);
            if(fill==null)return;
            if(mode==FillMode.Filled || mask==null)
            {
                fill.type=Image.Type.Filled;
                fill.fillMethod=Image.FillMethod.Horizontal;
                fill.fillOrigin=(int)Image.OriginHorizontal.Left;
                fill.fillAmount=value;
            }
            else
            {
                mask.anchorMin=Vector2.zero;mask.anchorMax=new Vector2(value,1);
                mask.offsetMin=Vector2.zero;mask.offsetMax=Vector2.zero;
            }
        }

        void Start(){ApplyTheme();SetValue01(value);}
        void OnValidate(){if(isActiveAndEnabled)SetValue01(value);}
    }
}
