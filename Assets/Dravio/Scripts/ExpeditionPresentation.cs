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
        public static string MaterialName(string id,bool vi)
        {
            if(!vi)return id;
            return id switch {"wood"=>"Gỗ","seed"=>"Hạt giống","stone"=>"Đá","crystal"=>"Pha lê","coral"=>"San hô","stardust"=>"Bụi sao",_=>id};
        }
        public static string RouteProgress(RouteExplorationDto route,bool vi)
        {
            if(route==null)return "—";
            var lines=new List<string>{(vi?"Chuyến đã nhận thưởng: ":"Claimed expeditions: ")+route.Claimed};
            if(!route.RewardsDefined)
                lines.Add(vi?"Tuyến chưa được kích hoạt; bảng thưởng chờ duyệt.":"Route disabled; reward table awaiting approval.");
            else if(!route.Open)lines.Add(vi?"Cần mở thế giới của tuyến.":"Unlock the route world first.");
            if(route.NextMilestoneAt.HasValue)
            {
                lines.Add((vi?"Mốc kế tiếp: ":"Next milestone: ")+route.NextMilestoneAt.Value+
                    (vi?" · Còn ":" · Remaining ")+Math.Max(0,route.NextMilestoneAt.Value-route.Claimed));
                if(route.NextMilestoneBonusEach>0&&route.RewardsDefined)
                    lines.Add((vi?"Thưởng mốc mỗi vật liệu: +":"Milestone bonus per material: +")+route.NextMilestoneBonusEach);
                if(!string.IsNullOrEmpty(route.NextMilestoneEggSpeciesId))
                    lines.Add((vi?"Trứng thưởng một lần: ":"One-time egg reward: ")+route.NextMilestoneEggSpeciesId+
                        (!route.RewardsDefined?(vi?" (chưa thể đạt)":" (not yet attainable)"):""));
            }
            else lines.Add(vi?"Đã đạt hết mốc hiện có.":"All current milestones reached.");
            return string.Join("\n",lines);
        }
        public static string EggGrant(RewardEggGranted egg,bool vi)
        {
            if(egg==null)return "";
            return (vi?"Trứng thưởng: ":"Reward egg: ")+egg.SpeciesId+" · "+
                (egg.Delivered?(vi?"Đã giao vào ô trứng, nhận tại Hatchery.":"Delivered to pending egg; claim in Hatchery."):
                (vi?"Đang chờ giao; giải phóng kho hoặc ô trứng.":"Queued; free storage or the pending egg slot."));
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
