using UnityEngine;
using UnityEngine.UI;

namespace Dravio.UI
{
    /// <summary>
    /// Tag for new or hand-authored UI: declares the element's sprite role and applies the theme on enable.
    /// Generated HUD scenes do not need it; FantasyPreviewUI maps roles at runtime instead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DravioUiSkinned : MonoBehaviour
    {
        public DravioUiTheme theme;
        public DravioUiRole role=DravioUiRole.Panel;

        void OnEnable()=>Apply();

        public void Apply()
        {
            if(theme==null)return;
            if(TryGetComponent<Button>(out var b) && DravioUiRoles.IsButton(role))DravioUiSkin.ApplyButton(b,theme,role);
            else if(TryGetComponent<Image>(out var img))
            {
                DravioUiSkin.ApplySurface(img,theme,role);
                if(role==DravioUiRole.Icon || role==DravioUiRole.Divider)img.raycastTarget=false;
            }
        }
    }
}
