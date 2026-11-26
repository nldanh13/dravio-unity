using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Behavior;
using Dravio.Domain.Catalog;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;

namespace Dravio
{
    public static class ResearchDiscoveryView
    {
        public static List<ObservationOpportunity> LocalOpportunities(GameState state,long now,DayPhase phase,string zone,Func<string,bool> live)
        {
            if(!ConnectedWorldPolicy.CanVisit(state,zone)||live==null)return new List<ObservationOpportunity>();
            return ProgressionQueries.ObservationOpportunities(state,now,phase)
                .Where(o=>o.ZoneId==zone && ConnectedWorldPolicy.InView(state,state.FindDragon(o.Uid),zone)&&live(o.Uid)).ToList();
        }
        public static bool CanRecord(ObservationOpportunity o)=>o!=null&&o.Availability=="ok";
        public static string KeepOrFirst(IEnumerable<string> ids,string current)
        {
            var list=ids.ToArray();return list.Contains(current,StringComparer.Ordinal)?current:list.FirstOrDefault();
        }
        public static string Next(IEnumerable<string> ids,string current)
        {
            var list=ids.ToArray();return list.Length==0?null:list[(Array.IndexOf(list,current)+1)%list.Length];
        }
        public static string Hint(string key,bool vi)=>key switch
        {
            "research.next.own"=>vi?"Sở hữu loài này":"Acquire this species",
            "research.next.viewProfile"=>vi?"Ghi nhận hồ sơ sinh học":"Record biology profile",
            "research.next.raiseToAdult"=>vi?"Quan sát đủ các giai đoạn lớn lên":"Record all growth stages",
            "research.next.observe"=>vi?"Ghi nhận tập tính còn thiếu":"Record missing behaviors",
            "research.next.returnTomorrow"=>vi?"Quay lại vào ngày UTC khác":"Return on another UTC day",
            "research.next.done"=>vi?"Đã hoàn thành nghiên cứu":"Research complete",
            "research.hint.newBehavior"=>vi?"Tập tính mới":"New behavior",
            "research.hint.newPhase"=>vi?"Pha ngày đêm mới":"New day phase",
            "research.hint.newHabitat"=>vi?"Sinh cảnh mới":"New habitat",
            "research.hint.individual"=>vi?"Nét cá thể mới":"New individual trait",
            "research.hint.tomorrow"=>vi?"Đã đạt giới hạn hôm nay (UTC)":"Daily limit reached (UTC)",
            "research.hint.cooldown"=>vi?"Đang chờ lượt quan sát":"Observation cooldown",
            "research.hint.nothingNew"=>vi?"Chưa có phát hiện mới":"Nothing new yet",
            _=>key??"—"
        };
        static string List<T>(IEnumerable<T> items)
        {var a=items.Select(x=>x.ToString()).ToArray();return a.Length==0?"—":string.Join(", ",a);}
        public static string Species(GameState state,string speciesId,bool vi)
        {
            var e=Codex.Find(state,speciesId);
            if(e==null)return vi?"Loài chưa khám phá. Sở hữu rồng để mở ô Codex.":"Undiscovered species. Acquire a dragon to reveal its Codex entry.";
            var sp=SpeciesCatalog.Get(speciesId);var dto=ProgressionQueries.ResearchProfile(state,speciesId);var p=dto.Species;
            var lines=new List<string>{sp.Id+" · "+p.Tier,
                (vi?"Biến thể: ":"Variants: ")+e.DiscoveredMutations.Count+"/"+Dravio.Domain.Economy.Mutation.Order.Count+" · "+List(e.DiscoveredMutations),
                (vi?"Giai đoạn: ":"Stages: ")+List(e.ObservedStages),
                (vi?"Cá thể hiện có: ":"Owned individuals: ")+p.Individuals,
                (vi?"Tiến độ tập tính: ":"Ethology records: ")+p.EthologyProgressPct+"%",
                (vi?"Tập tính đã ghi: ":"Recorded behaviors: ")+p.ObservedBehaviors.Count+"/"+p.ObservableBehaviors.Count+" · "+List(p.ObservedBehaviors),
                (vi?"Pha đã ghi: ":"Recorded phases: ")+List(p.ObservedPhases),
                (vi?"Sinh cảnh đã ghi: ":"Recorded habitats: ")+List(p.ObservedHabitats),
                (vi?"Ngày nghiên cứu UTC: ":"UTC research days: ")+p.DistinctDays+"/"+p.RequiredDistinctDays,
                (vi?"Chu kỳ hoạt động/nghỉ: ":"Active/rest coverage: ")+(p.CircadianCovered?(vi?"Đủ":"Complete"):(vi?"Chưa đủ":"Incomplete")),
                (vi?"Bước tiếp theo: ":"Next: ")+Hint(p.NextStepKey,vi)};
            if(p.RevealedSections.Contains(CodexSection.Identity))lines.Add((vi?"Nguyên tố / độ hiếm: ":"Elements / rarity: ")+List(sp.Elements)+" / "+sp.Rarity);
            if(p.RevealedSections.Contains(CodexSection.Habitat))lines.Add((vi?"Sinh cảnh phù hợp: ":"Compatible habitats: ")+List(sp.HabitatRules)+"\n"+(vi?"Thức ăn: ":"Diet: ")+List(sp.Biology.DietIds));
            if(p.RevealedSections.Contains(CodexSection.Behavior))lines.Add((vi?"Tập tính còn thiếu: ":"Missing behaviors: ")+List(p.ObservableBehaviors.Except(p.ObservedBehaviors)));
            return string.Join("\n\n",lines);
        }
        public static string Quest(QuestStatusView q,bool enabled,bool vi)
        {
            if(q==null)return "—";
            var lines=new List<string>{q.QuestId+" · "+q.State+" · "+q.ProgressPct+"%",
                q.IsCanonicalGate?(vi?"Cổng thế giới canonical":"Canonical world gate"):(vi?"Nhiệm vụ đề xuất":"Proposed quest"),
                enabled?(vi?"Luật hoàn thành đã bật; màn này chỉ đọc.":"Completion rules enabled; this screen is read-only."):(vi?"Chưa bật hoàn thành và phần thưởng.":"Completion and rewards disabled.")};
            if(q.LockedBy.Count>0)lines.Add((vi?"Điều kiện mở: ":"Locked by: ")+List(q.LockedBy));
            foreach(var req in q.Requirements)
                lines.Add(req.Def.Kind+" "+(req.Def.Param??"")+(req.Def.Realm.HasValue?" "+req.Def.Realm:"")+
                    (req.Def.Locomotion.HasValue?" "+req.Def.Locomotion:"")+(req.Def.Tier!=ResearchTier.Unknown?" "+req.Def.Tier:"")+
                    "\n"+req.Current+"/"+req.Target+" · "+req.State+(string.IsNullOrEmpty(req.Detail)?"":" · "+req.Detail));
            return string.Join("\n\n",lines);
        }
    }
}
