using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Core;
using Dravio.Domain.Catalog;
using Dravio.Domain.Progression;
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
            foreach(var need in quote.NextProjectMaterials)
                if(need.Missing>0)missing.Add(new KeyValuePair<string,long>(need.MaterialId,need.Missing));
            return missing;
        }
        public static string ProjectName(SanctuaryProjectDef project,bool vi)
        {
            if(project==null)return "—";
            if(vi)return project.NameVi;
            if(project.Id.StartsWith("vista_",StringComparison.Ordinal))
                return "Landscape expansion "+project.Id.Split('_').Last();
            return project.Id switch
            {
                "origin_mossy_steps"=>"Mossy stone steps","origin_lantern_row"=>"Stone lantern path",
                "origin_meadow_arbor"=>"Meadow arbor","origin_trail_shrine"=>"Trail shrine",
                "origin_lily_pond"=>"Lily pond","origin_rune_circle"=>"Rune circle",
                "origin_ancient_gate"=>"Ancient gate","origin_elder_statue"=>"Elder dragon statue",
                "ocean_tide_pools"=>"Tide pools","ocean_coral_arch"=>"Coral arch",
                "ocean_shell_path"=>"Shell path","ocean_kelp_garden"=>"Kelp garden",
                "ocean_glow_buoys"=>"Glowing buoys","ocean_sunken_shrine"=>"Sunken shrine",
                "sky_cloud_bridge"=>"Cloud bridge","sky_cloud_garden"=>"Cloud garden",
                "sky_wind_chimes"=>"Wind chimes","sky_falling_spring"=>"Falling spring",
                "sky_star_observatory"=>"Star observatory","sky_aurora_spire"=>"Aurora spire",
                _=>project.Id
            };
        }
        public static string ProjectZone(string id,bool vi)=>id switch
        {
            "origin"=>vi?"Khởi Nguyên":"Origin","ocean"=>vi?"Thủy Vực":"Ocean",
            "sky"=>vi?"Thiên Giới":"Sky",_=>id??"—"
        };
        public static string ProjectBlock(string code,bool vi)=>code switch
        {
            null=>vi?"Đủ điều kiện xây":"Ready to build",
            "not-enough-gold"=>vi?"Chưa đủ vàng":"Not enough gold",
            "not-enough-materials"=>vi?"Chưa đủ vật liệu":"Not enough materials",
            "projects-maxed"=>vi?"Đã xây đủ dự án":"All projects built",
            _=>code
        };
        public static string ProjectSummary(GameState state,SanctuaryEconomyDto quote,bool vi)
        {
            if(state==null||quote==null)return "—";
            var lines=new List<string>{(vi?"Dự án Sanctuary: ":"Sanctuary projects: ")+quote.ProjectsBuilt+"/"+quote.ProjectsMax};
            if(quote.NextProject==null||!quote.NextProjectCostGoldMicro.HasValue)
            {
                lines.Add(vi?"Đã hoàn thành toàn bộ dự án.":"All projects completed.");
                return string.Join("\n",lines);
            }
            var next=quote.NextProject;
            var view=ProgressionQueries.SanctuaryProjects(state).FirstOrDefault(p=>!p.Built);
            lines.Add((vi?"Tiếp theo: ":"Next: ")+ProjectName(next,vi)+" · "+ProjectZone(next.ZoneId,vi));
            if(view!=null && view.DisplayZoneId!=next.ZoneId)
                lines.Add((vi?"Vị trí tạm thời: ":"Temporary display world: ")+ProjectZone(view.DisplayZoneId,vi));
            if(view!=null && view.StyleVariant)
                lines.Add(vi?"Đã mở mẫu trang trí.":"Decorative blueprint unlocked.");
            lines.Add((vi?"Vàng: ":"Gold: ")+(quote.NextProjectCostGoldMicro.Value/(decimal)MicroGold.MicroPerGold).ToString("0.##",CultureInfo.InvariantCulture)+" G");
            foreach(var need in quote.NextProjectMaterials)
                lines.Add(MaterialName(need.MaterialId,vi)+": "+need.Have+"/"+need.Required+
                    (need.Missing>0?(vi?" · Thiếu ":" · Missing ")+need.Missing:""));
            if(quote.NextProjectMaterials.Count==0)lines.Add(vi?"Không cần vật liệu.":"No materials required.");
            else if(quote.NextProjectMaterialFallback)
                lines.Add((vi?"Nguồn vật liệu thay thế: ":"Fallback material source: ")+ProjectZone(quote.NextProjectMaterialZoneId,vi)+
                    (vi?" (thế giới đích chưa mở hoặc chưa có nguồn).":" (target locked or source unavailable)."));
            var deficits=quote.NextProjectMaterials.Where(n=>n.Missing>0).Select(n=>n.MaterialId).ToArray();
            if(deficits.Length>0)
            {
                var routes=ProgressionQueries.ExplorationRoutes(state).Where(r=>r.Open&&r.RewardsDefined&&r.Materials.Any(deficits.Contains)).Select(r=>r.RouteId).ToArray();
                if(routes.Length>0)lines.Add((vi?"Tuyến tìm vật liệu: ":"Material routes: ")+string.Join(", ",routes));
            }
            lines.Add(ProjectBlock(quote.NextProjectBlockedBy,vi));
            lines.Add(vi?"Chỉ trang trí; không tăng sản xuất. Hình ảnh tùy tài nguyên đã tích hợp.":"Cosmetic only; no production bonus. Visuals depend on integrated assets.");
            return string.Join("\n",lines);
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
