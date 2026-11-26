using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Dravio
{
    internal static class DravioVectorSkinValidation
    {
        [MenuItem("Dravio/UI/Validate Vector Skin Meshes")]
        static void Validate()
        {
            var go=new GameObject("Vector skin validation",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            try
            {
                var rt=go.GetComponent<RectTransform>();
                var skin=go.AddComponent<DravioVectorSkin>();
                var styles=new[]{"primary","secondary","reward","danger","panel","header","card","notice","round","portrait","slot_common","slot_rare","slot_epic","slot_legendary","divider","progress_frame","progress_fill",
                    "icon/care","icon/biology","icon/dragons","icon/eggs","icon/world","icon/quests","icon/expedition","icon/inventory","icon/feed","icon/pet","icon/rest","icon/inspect","icon/build","icon/research","icon/health","icon/settings","icon/day","icon/night","icon/lock","icon/done","icon/warning","icon/upgrade","icon/reward","icon/hunger","icon/water","icon/energy","icon/temperature","icon/bond","icon/leaf","icon/close","icon/back","icon/next","icon/gold","icon/wood","icon/seed","icon/stone","icon/crystal","icon/coral","icon/stardust","icon/fruit","icon/aquatic","icon/aether","icon/starter","icon/elemental","icon/mystic","icon/astral","icon/blueprint","icon/egg"};
                int count=0;
                foreach(var size in new[]{new Vector2(18,18),new Vector2(44,44),new Vector2(120,48),new Vector2(320,660),new Vector2(900,120)})
                {
                    rt.sizeDelta=size;
                    foreach(var style in styles)
                    {
                        skin.SetStyle(style);
                        using(var vh=new VertexHelper())
                        {
                            skin.ModifyMesh(vh);
                            if(vh.currentVertCount<=0 || vh.currentIndexCount<=0 || vh.currentIndexCount%3!=0)
                                throw new InvalidOperationException("Empty/invalid vector mesh: "+style+" at "+size);
                            var v=UIVertex.simpleVert;
                            for(int i=0;i<vh.currentVertCount;i++)
                            {
                                vh.PopulateUIVertex(ref v,i);
                                if(float.IsNaN(v.position.x)||float.IsInfinity(v.position.x)||float.IsNaN(v.position.y)||float.IsInfinity(v.position.y))
                                    throw new InvalidOperationException("Nonfinite vertex: "+style);
                            }
                            count++;
                        }
                    }
                }
                Debug.Log("Dravio UI: "+count+" vector mesh cases passed.");
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
