using System;
using UnityEngine;
using Dravio.EnvironmentArt;

namespace Dravio.Navigation
{
    /// <summary>Read-only spatial adapter over the existing generated location data.
    /// Never crosses habitats, changes Domain locations, or invents a water/air volume.</summary>
    public sealed class SanctuaryNavigationSurface
    {
        readonly SanctuaryLocationData data;
        public SanctuaryNavigationSurface(SanctuaryLocationData data){this.data=data;}
        public static NavPoint Point(Vector3 v)=>new NavPoint(v.x,v.y,v.z);
        public static Vector3 Vector(NavPoint p)=>new Vector3(p.X,p.Y,p.Z);

        public bool HasAuthoredVolume(string habitat,NavigationMedium medium)
        {
            if(data==null || data.volumes==null || (medium!=NavigationMedium.Swim && medium!=NavigationMedium.Air))return false;
            string mode=medium==NavigationMedium.Swim?"swim":"fly";
            foreach(var volume in data.volumes)
                if(volume!=null && volume.habitatId==habitat && volume.medium==mode && volume.yMax>volume.yMin &&
                    volume.polygonXZ!=null && volume.polygonXZ.Length>=6)return true;
            return false;
        }

        public bool Clear(Vector3 point,string habitat,NavigationMedium medium,float clearance=.25f)
        {
            if(data==null || string.IsNullOrEmpty(habitat) || !Point(point).Finite || clearance<0f || float.IsNaN(clearance))return false;
            if(medium==NavigationMedium.Ground)
                return data.IsWalkable(point,habitat) && data.TryGroundHeight(point,out float y) && Mathf.Abs(point.y-y)<=.02f;
            if(medium!=NavigationMedium.Swim && medium!=NavigationMedium.Air)return false;
            string mode=medium==NavigationMedium.Swim?"swim":"fly";
            // Only authored 3D volumes. Origin pond has a 2D footprint, no certified depth/entry.
            // Keep the center, vertical margins and horizontal margins in one volume.
            if(data.volumes==null)return false;
            foreach(var volume in data.volumes)
            {
                if(volume==null || volume.medium!=mode || volume.habitatId!=habitat || !volume.Contains(point))continue;
                if(point.y-clearance<volume.yMin || point.y+clearance>volume.yMax)continue;
                if(SanctuaryLocationData.EdgeDistance(volume.polygonXZ,point.x,point.z)<clearance)continue;
                if(data.BlockedAt(point-Vector3.up*clearance,clearance))continue;
                return true;
            }
            return false;
        }
        // Separate horizontal/vertical margins for the U5.4.2 review tool only.
        public bool ClearReviewVolume(Vector3 point,string habitat,NavigationMedium medium,MediumReviewEnvelope envelope)
        {
            if(data==null || data.volumes==null || string.IsNullOrEmpty(habitat) || !Point(point).Finite || !envelope.Valid ||
                (medium!=NavigationMedium.Swim && medium!=NavigationMedium.Air))return false;
            string mode=medium==NavigationMedium.Swim?"swim":"fly";
            foreach(var volume in data.volumes)
            {
                if(volume==null || volume.medium!=mode || volume.habitatId!=habitat || !volume.Contains(point))continue;
                if(point.y-envelope.Vertical<volume.yMin || point.y+envelope.Vertical>volume.yMax ||
                    SanctuaryLocationData.EdgeDistance(volume.polygonXZ,point.x,point.z)<envelope.Horizontal)continue;
                if(!data.BlockedAt(point-Vector3.up*envelope.Vertical,envelope.Horizontal))return true;
            }
            return false;
        }
        public bool Segment(Vector3 a,Vector3 b,string habitat,NavigationMedium medium,float clearance=.25f)
            =>SanctuaryRoutePlanner.SegmentClear(Point(a),Point(b),p=>Clear(Vector(p),habitat,medium,clearance));

        public RouteResult Plan(Vector3 a,Vector3 b,string habitat,NavigationMedium medium,
            Func<Vector3,bool> additionalClear=null,int budget=768,float clearance=.25f)
            =>SanctuaryRoutePlanner.Plan(Point(a),Point(b),medium,p=>
                Clear(Vector(p),habitat,medium,clearance) && (additionalClear==null || additionalClear(Vector(p))),.45f,budget);
    }
}
