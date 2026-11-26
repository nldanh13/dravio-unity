using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Queries;
using Dravio.Domain.Expedition;

namespace Dravio
{
    // UI projection only; all authoritative validation and mutation stay in Domain.
    public static class ExpeditionPresentation
    {
        public static ExpeditionOption Route(GameState state,string routeId)
            =>state==null?null:Ecosystem.ExpeditionOptions(state).FirstOrDefault(r=>r.RouteId==routeId);
        public static bool CanStart(GameState state,string routeId,string uid,out string reason)
        {
            reason="not-selected";
            if(state==null||string.IsNullOrEmpty(routeId)||string.IsNullOrEmpty(uid))return false;
            var block=Ecosystem.ExpeditionEligibility(state,routeId,uid,out string detail);
            reason=block==null?null:block.Value.ToWire()+(string.IsNullOrEmpty(detail)?"":" · "+detail);
            return block==null;
        }
        public static bool CanClaim(GameState state)
            =>state?.Expedition!=null&&state.Expedition.Status==ExpeditionStatus.Ready&&
                Expeditions.CheckClaim(state,state.Clock.LastProcessedAtMs)==null;
        public static string Remaining(long ms)
        {
            long seconds=Math.Max(0,ms/1000+(ms%1000>0?1:0));
            return (seconds/3600).ToString("00",CultureInfo.InvariantCulture)+":"+
                (seconds/60%60).ToString("00",CultureInfo.InvariantCulture)+":"+(seconds%60).ToString("00",CultureInfo.InvariantCulture);
        }
        public static IReadOnlyList<KeyValuePair<string,long>> ProjectShortfalls(GameState state,SanctuaryEconomyDto quote)
        {
            var missing=new List<KeyValuePair<string,long>>();
            if(state==null||quote==null||!quote.NextProjectCostGoldMicro.HasValue||quote.NextProjectMaterialEach<=0)return missing;
            foreach(var id in quote.NextProjectMaterialIds)
            {
                long stock=state.Player.Inventory.TryGetValue(id,out var n)?n:0;
                long deficit=Math.Max(0,(long)quote.NextProjectMaterialEach-stock);
                if(deficit>0)missing.Add(new KeyValuePair<string,long>(id,deficit));
            }
            return missing;
        }
        public static string Reason(string code,bool vi)
        {
            if(string.IsNullOrEmpty(code))return vi?"Đủ điều kiện":"Eligible";
            string first=code.Split('·')[0].Trim();
            string translated=first switch
            {
                "route-locked"=>vi?"Thế giới chưa mở khóa":"World locked",
                "route-rewards-undefined"=>vi?"Chưa có bảng thưởng được duyệt":"Reward table unavailable",
                "expedition-busy"=>vi?"Đang có chuyến đi hoặc thưởng chờ nhận":"Expedition slot occupied",
                "dragon-busy"=>vi?"Rồng đang bận hoặc có ca chăm sóc":"Dragon busy or in care",
                "dragon-unsuited"=>vi?"Rồng chưa trưởng thành hoặc không phù hợp tuyến":"Adult or locomotion requirement not met",
                "vitals-below-threshold"=>vi?"Sức khỏe chưa đạt yêu cầu":"Vitals below threshold",
                "not-selected"=>vi?"Chọn tuyến và rồng":"Choose a route and dragon",
                _=>first
            };
            return code.Length>first.Length?translated+code.Substring(first.Length):translated;
        }
    }
}
