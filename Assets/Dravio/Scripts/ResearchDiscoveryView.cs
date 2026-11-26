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
        public static string ObservationFeedback(ObservationOpportunity opportunity,bool vi)
        {
            if(opportunity==null)return vi?"Rồng chưa thể quan sát tại đây.":"Dragon is not observable here.";
            if(opportunity.Availability=="observation-cooldown")
                return (vi?"Quan sát lại sau ":"Observe again in ")+Math.Max(0,Math.Ceiling(opportunity.CooldownRemainingMs/1000d))+" s.";
            return Hint(opportunity.HintKey,vi);
        }
        public static string FoodFeedback(string speciesId,string uid,int xpGainedSec,bool vi)
        {
            string name=string.IsNullOrEmpty(speciesId)?uid:speciesId+" #"+(uid??"").Substring(0,Math.Min(6,(uid??"").Length));
            return (vi?"Rồng đã ăn: ":"Food consumed by: ")+name+" · +"+xpGainedSec+" XP";
        }
        public static string RelationSummary(RelationsDto relations,Func<string,string> partnerLabel,bool vi)
        {
            if(relations==null)return vi?"Chưa có hồ sơ quan hệ.":"No relationship profile.";
            var lines=new List<string>{vi?"Quan hệ với rồng khác (chỉ thông tin)":"Relationships (information only)"};
            if(relations.Relations.Count==0)
            {
                lines.Add(vi?"Chưa có cá thể khác.":"No other individuals yet.");
                return string.Join("\n",lines);
            }
            // Domain supplies the ordered familiarity snapshot. Show at most three rows.
            foreach(var relation in relations.Relations.Take(3))
            {
                string other=relation.UidA==relations.Uid?relation.UidB:relation.UidA;
                string tier=relation.Tier switch
                {
                    RelationTier.Stranger=>vi?"Xa lạ":"Stranger",
                    RelationTier.Acquainted=>vi?"Quen biết":"Acquainted",
                    RelationTier.Friend=>vi?"Bạn bè":"Friend",
                    RelationTier.Close=>vi?"Thân thiết":"Close",
                    _=>relation.Tier.ToString()
                };
                string name=partnerLabel?.Invoke(other)??other;
                lines.Add(name+" · "+tier+" · "+relation.Familiarity+"/100");
                lines.Add((vi?"Hợp tính: ":"Affinity: ")+relation.Affinity+"/100 · "+
                    (vi?"Sống chung ghi nhận: ":"Recorded shared time: ")+
                    (relation.SharedMs/3600000d).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+" h");
            }
            if(relations.Relations.Count>3)
                lines.Add((vi?"Cá thể khác: ":"More individuals: ")+(relations.Relations.Count-3));
            return string.Join("\n",lines);
        }
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
        public static string ZoneName(string id,bool vi)=>id switch
        {
            "origin"=>vi?"Khởi Nguyên":"Origin",
            "ocean"=>vi?"Thủy Vực":"Ocean",
            "sky"=>vi?"Thiên Giới":"Sky",
            _=>id
        };
        public static string QuestName(string id,bool vi)=>id switch
        {
            "Q_ORIGIN_FIELD_NOTES"=>vi?"Ghi chép Khởi Nguyên":"Origin field notes",
            "Q_ORIGIN_FOREST_SURVEY"=>vi?"Khảo sát rừng":"Forest survey",
            "QUEST_AQUATIC_EXPLORATION"=>vi?"Khám phá Thủy Vực":"Aquatic exploration",
            "QUEST_SKY_EXPLORATION"=>vi?"Khám phá Thiên Giới":"Sky exploration",
            "Q_RESEARCH_NIGHT_WATCH"=>vi?"Canh đêm":"Night watch",
            "Q_RESEARCH_PERSONALITIES"=>vi?"Tìm hiểu cá tính":"Personalities",
            "Q_RESEARCH_ETHOLOGIST"=>vi?"Nhà nghiên cứu tập tính":"Ethologist",
            "Q_RESEARCH_TIDE_JOURNAL"=>vi?"Nhật ký thủy triều":"Tide journal",
            "Q_RESEARCH_SKY_CHRONICLE"=>vi?"Biên niên Thiên Giới":"Sky chronicle",
            "Q_CONSERVE_AT_HOME"=>vi?"Bảo tồn tại nhà":"Conserve at home",
            "Q_CONSERVE_TIDES"=>vi?"Bảo tồn Thủy Vực":"Conserve tides",
            "Q_CONSERVE_SKIES"=>vi?"Bảo tồn Thiên Giới":"Conserve skies",
            "Q_SOCIAL_FIRST_FRIENDS"=>vi?"Những người bạn đầu tiên":"First friends",
            "Q_SOCIAL_CLOSE_BOND"=>vi?"Mối quan hệ thân thiết":"Close bond",
            "Q_EXPEDITION_PATHFINDER"=>vi?"Người tìm đường":"Pathfinder",
            "Q_EXPEDITION_REEF"=>vi?"Khảo sát rạn san hô":"Reef survey",
            "Q_EXPEDITION_CLOUD"=>vi?"Khảo sát sân mây":"Cloud survey",
            _=>id
        };
        public static string Missing(string id,bool vi)
        {
            if(id=="tutorial-completed")return vi?"Hoàn thành hướng dẫn":"Complete tutorial";
            if(id=="origin-habitats")return vi?"Mở đủ sinh cảnh Khởi Nguyên":"Unlock all Origin habitats";
            if(id.StartsWith("zone:",StringComparison.Ordinal))return (vi?"Mở ":"Unlock ")+ZoneName(id.Substring(5),vi);
            if(id.StartsWith("quest:",StringComparison.Ordinal))return (vi?"Nhận nhiệm vụ: ":"Claim quest: ")+QuestName(id.Substring(6),vi);
            if(id=="route-rewards-undefined")return vi?"Chưa duyệt bảng thưởng tuyến":"Route reward table not approved";
            return id;
        }
        // Return a claim or an exact zone ID, never both. Domain is the only eligibility authority.
        public static string QuestAction(QuestLogDto log,string id)
        {
            if(log==null||id==null)return null;
            var q=log.Quests.FirstOrDefault(x=>x.QuestId==id);
            if(q==null)return null;
            if(q.CanComplete&&log.ReadyQuestIds.Contains(id))return "claim";
            if(q.State!=QuestState.Completed||!q.IsCanonicalGate||!q.RewardId.StartsWith("zone.",StringComparison.Ordinal))return null;
            string zone=q.RewardId.Substring(5);
            return log.ZoneUnlockMissing.TryGetValue(zone,out var missing)&&missing.Count==0?zone:null;
        }
        public static string WorldProgress(QuestLogDto log,bool vi)
        {
            var lines=new List<string>();
            foreach(string zone in new[]{"ocean","sky"})
            {
                string name=ZoneName(zone,vi);
                if(!log.ZoneUnlockMissing.TryGetValue(zone,out var missing))lines.Add(name+": "+(vi?"Đã mở":"Unlocked"));
                else if(missing.Count==0)lines.Add(name+": "+(vi?"Có thể mở trong nhiệm vụ cổng":"Ready to unlock in gate quest"));
                else lines.Add(name+": "+string.Join("; ",missing.Select(m=>Missing(m,vi))));
            }
            return string.Join("\n",lines);
        }
        // Presentation-only navigation: inspect canonical blockers, never manufacture eligibility.
        public static string NextWorld(QuestLogDto log)
        {
            if(log==null)return null;
            return new[]{"ocean","sky"}.FirstOrDefault(z=>log.ZoneUnlockMissing.ContainsKey(z));
        }
        public static string JourneyQuest(QuestLogDto log)
        {
            string zone=NextWorld(log);
            if(zone==null)return null;
            string id=WorldUnlockCatalog.RuleFor(zone)?.RequiresQuest;
            var visited=new HashSet<string>(StringComparer.Ordinal);
            while(!string.IsNullOrEmpty(id)&&visited.Add(id))
            {
                var q=log.Quests.FirstOrDefault(x=>x.QuestId==id);
                if(q==null)return null;
                var prereq=q.LockedBy.FirstOrDefault(x=>x.StartsWith("quest:",StringComparison.Ordinal));
                if(prereq==null)return id;
                id=prereq.Substring(6);
            }
            return null; // Broken/cyclic catalog: do not choose an unrelated quest.
        }
        public static string JourneySummary(QuestLogDto log,bool vi)
        {
            string zone=NextWorld(log);
            if(zone==null)return vi?"Hành trình: đã mở đủ ba thế giới. Tiếp tục nhiệm vụ phụ và nghiên cứu.":
                "Journey: all three worlds unlocked. Continue side quests and research.";
            var lines=new List<string>{(vi?"Mục tiêu tiếp theo: ":"Next world: ")+ZoneName(zone,vi)};
            var missing=log.ZoneUnlockMissing[zone];
            if(missing.Contains("tutorial-completed"))
                lines.Add(vi?"Hướng dẫn: hoàn tất hoặc tiếp tục hướng dẫn trong Guide.":"Guide: finish or resume the tutorial.");
            if(missing.Contains("origin-habitats"))
                lines.Add(vi?"Hướng dẫn: khảo sát và mở đủ sinh cảnh Khởi Nguyên.":"Guide: survey and unlock all Origin habitats.");
            string id=JourneyQuest(log);
            var q=log.Quests.FirstOrDefault(x=>x.QuestId==id);
            if(q!=null)
            {
                string action=QuestAction(log,id);
                lines.Add((vi?"Nhiệm vụ trọng tâm: ":"Focus quest: ")+QuestName(id,vi)+
                    " · "+q.ProgressPct+"%");
                if(action=="claim")lines.Add(vi?"Đủ yêu cầu: nhận nhiệm vụ trong thẻ tương ứng.":"Ready: claim the quest on its card.");
                else if(action==zone)lines.Add(vi?"Đã đủ điều kiện: mở thế giới trong thẻ nhiệm vụ cổng.":"All requirements met: unlock the world on its gate quest card.");
                else if(q.State!=QuestState.Completed)
                    lines.Add(vi?"Xem yêu cầu còn thiếu trong thẻ nhiệm vụ.":"Review missing requirements on its quest card.");
            }
            if(log.ReadyQuestIds.Count>0)
                lines.Add((vi?"Có thể nhận: ":"Ready to claim: ")+string.Join(", ",log.ReadyQuestIds.Select(x=>QuestName(x,vi))));
            lines.Add(vi?"Nhấn lại Nhiệm vụ để về thẻ trọng tâm.":"Tap Quests again to return to the focus card.");
            return string.Join("\n",lines);
        }
        public static string CanonicalJourneyFocus(JourneyDto journey)
        {
            if(journey==null)return null;
            return journey.Steps.FirstOrDefault(x=>!string.IsNullOrEmpty(x.QuestId))?.QuestId ??
                (journey.NextZoneId==null?null:WorldUnlockCatalog.RuleFor(journey.NextZoneId)?.RequiresQuest);
        }
        public static string CanonicalJourneySummary(JourneyDto journey,bool vi)
        {
            if(journey==null)return "—";
            if(journey.NextZoneId==null)return vi?"Hành trình: đã mở đủ ba thế giới. Tiếp tục nhiệm vụ phụ và nghiên cứu.":"Journey: all three worlds unlocked. Continue side quests and research.";
            var lines=new List<string>{(vi?"Mục tiêu tiếp theo: ":"Next world: ")+ZoneName(journey.NextZoneId,vi)};
            foreach(var step in journey.Steps.Take(4))
            {
                string action=step.Code switch
                {
                    JourneyStepCode.ResumeTutorial=>vi?"Tiếp tục hướng dẫn":"Resume tutorial",
                    JourneyStepCode.FinishTutorial=>vi?"Hoàn thành bước hướng dẫn":"Finish tutorial step",
                    JourneyStepCode.SurveyHabitat=>vi?"Khảo sát sinh cảnh":"Survey habitat",
                    JourneyStepCode.UnlockHabitat=>vi?"Mở sinh cảnh":"Unlock habitat",
                    JourneyStepCode.QuestRequirement=>vi?"Yêu cầu nhiệm vụ":"Quest requirement",
                    JourneyStepCode.ClaimQuest=>vi?"Nhận nhiệm vụ":"Claim quest",
                    JourneyStepCode.UnlockZone=>vi?"Mở thế giới":"Unlock world",
                    _=>step.Code.ToString()
                };
                string target=step.QuestId!=null?QuestName(step.QuestId,vi):step.Target;
                if(step.Code==JourneyStepCode.UnlockZone)target=ZoneName(step.Target,vi);
                lines.Add(action+(string.IsNullOrEmpty(target)?"":" · "+target)+
                    (step.Requirement.HasValue?" · "+step.Requirement+" "+step.Current+"/"+step.Required:"")+
                    (step.Ready?(vi?" · Sẵn sàng":" · Ready"):"")+
                    (string.IsNullOrEmpty(step.Detail)?"":" · "+Missing(step.Detail,vi)));
                if(step.Candidates!=null&&step.Candidates.Count>0)
                    lines.Add((vi?"Gợi ý: ":"Candidates: ")+string.Join(", ",step.Candidates.Take(3)));
            }
            if(journey.Steps.Count>4)lines.Add((vi?"Các bước tiếp theo: ":"Further steps: ")+(journey.Steps.Count-4));
            if(journey.SideQuestsReady.Count>0)lines.Add((vi?"Nhiệm vụ phụ có thể nhận: ":"Side quests ready: ")+string.Join(", ",journey.SideQuestsReady.Select(x=>QuestName(x,vi))));
            return string.Join("\n",lines);
        }
        static bool InRealm(IEnumerable<string> habitats,Realm realm)=>
            habitats.Any(h=>HabitatCatalog.TryGet(h,out var profile)&&profile.Realm==realm);
        // Suggestions are bounded, read-only examples from currently owned species and catalog routes.
        // They do not certify placement, expedition departure or quest eligibility.
        public static string QuestGuidance(GameState s,QuestStatusView q,bool vi)
        {
            if(s==null||q==null||q.State==QuestState.Completed)return "";
            if(q.State==QuestState.Locked)
                return vi?"Hoàn thành điều kiện mở trước; xem mục tiêu trọng tâm ở trên.":"Finish the prerequisites first; see the focus objective above.";
            var lines=new List<string>();
            var owned=s.Dragons.Where(d=>d.Location.Kind!=LocationKind.Released).Select(d=>d.SpeciesId)
                .Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
            foreach(var req in q.Requirements.Where(x=>x.State!=RequirementState.Met))
            {
                var def=req.Def;
                if(req.State==RequirementState.Blocked||req.State==RequirementState.Unknown)
                {
                    lines.Add(RequirementName(def,vi)+": "+(vi?"Chưa khả dụng":"Unavailable")+
                        (string.IsNullOrEmpty(req.Detail)?"":" · "+Missing(req.Detail,vi)));
                    continue;
                }
                switch(def.Kind)
                {
                    case QuestRequirementKind.MaterialDelivery:
                    {
                        var routes=ExpeditionCatalog.Routes.Where(r=>r.RewardsDefined&&r.Materials.Contains(def.Param)&&s.FindZone(r.ZoneId)?.Unlocked==true)
                            .Select(r=>vi?r.NameVi:r.Id).ToArray();
                        lines.Add(RequirementName(def,vi)+" · "+(vi?"Còn thiếu ":"Missing ")+Math.Max(0,req.Target-req.Current)+
                            (routes.Length==0?(vi?" · Chưa có tuyến mở phù hợp":" · No suitable unlocked route"):
                            (vi?" · Quản lý > Thám hiểm: ":" · Manage > Expedition: ")+string.Join(", ",routes)));
                        break;
                    }
                    case QuestRequirementKind.ResearchTier:
                    case QuestRequirementKind.CircadianRecords:
                    case QuestRequirementKind.SpeciesRecordedInRealm:
                    {
                        var candidates=new List<string>();
                        foreach(string id in owned)
                        {
                            var profile=ProgressionQueries.ResearchProfile(s,id).Species;
                            if(def.Realm.HasValue&&!InRealm(profile.NativeHabitats,def.Realm.Value))continue;
                            string hint;
                            if(def.Kind==QuestRequirementKind.ResearchTier)
                            {
                                if(profile.Tier>=def.Tier)continue;
                                hint=Hint(profile.NextStepKey,vi);
                            }
                            else if(def.Kind==QuestRequirementKind.CircadianRecords)
                            {
                                if(profile.CircadianCovered)continue;
                                hint=vi?"Quan sát lúc hoạt động và nghỉ":"Observe active and resting phases";
                            }
                            else
                            {
                                if(!def.Realm.HasValue||InRealm(profile.ObservedHabitats,def.Realm.Value))continue;
                                hint=vi?"Chuyển đến sinh cảnh phù hợp rồi quan sát":"Move to a suitable habitat and observe";
                            }
                            candidates.Add(id+" — "+hint);
                            if(candidates.Count==3)break;
                        }
                        lines.Add(RequirementName(def,vi)+":\n"+(candidates.Count==0?
                            (vi?"Chưa có loài đang nuôi phù hợp; kiểm tra Bách khoa, gói trứng hoặc lai giống.":"No matching owned species; check Codex, egg packs or breeding."):
                            string.Join("\n",candidates)));
                        break;
                    }
                    case QuestRequirementKind.AdultWithLocomotion:
                    {
                        var candidates=owned.Where(id=>def.Locomotion.HasValue&&SpeciesCatalog.GetBiology(id).Locomotion.Contains(def.Locomotion.Value)&&
                            (!def.Realm.HasValue||InRealm(SpeciesCatalog.GetBiology(id).HabitatRules,def.Realm.Value)))
                            .Where(id=>s.Dragons.Any(d=>d.SpeciesId==id&&d.Location.Kind!=LocationKind.Released&&
                                (d.Location.Kind!=LocationKind.Zone||
                                (def.Realm.HasValue&&!InRealm(new[]{d.Location.HabitatId},def.Realm.Value))||
                                Dravio.Domain.Economy.EconomyRates.GrowthStageOf(d.XpSec,SpeciesCatalog.Get(id))!=GrowthStage.Adult)))
                            .Take(3).ToArray();
                        lines.Add(RequirementName(def,vi)+": "+(candidates.Length==0?
                            (vi?"Kiểm tra loài có khả năng di chuyển phù hợp.":"Check species with the required locomotion."):
                            string.Join(", ",candidates)+(vi?" — nuôi tới trưởng thành và đặt vào sinh cảnh phù hợp.":" — raise to Adult and place in a suitable habitat.")));
                        break;
                    }
                    default:
                        // Leave other objectives to their canonical requirement card rather than guessing.
                        break;
                }
            }
            return lines.Count==0?"":(vi?"Gợi ý thực hiện (không tự hoàn thành):":"Suggested actions (not automatic):")+"\n"+string.Join("\n\n",lines);
        }
        static string RequirementName(QuestRequirementDef def,bool vi)
        {
            string name=def.Kind switch
            {
                QuestRequirementKind.ResearchTier=>vi?"Loài đạt cấp nghiên cứu":"Species at research tier",
                QuestRequirementKind.AdultWithLocomotion=>vi?"Rồng trưởng thành ở sinh cảnh":"Adult residents with locomotion",
                QuestRequirementKind.MaterialDelivery=>vi?"Vật liệu giao khi nhận":"Materials delivered on claim",
                QuestRequirementKind.ResidentsThriving=>vi?"Cư dân có độ phù hợp tốt":"Residents with good suitability",
                QuestRequirementKind.ResidentsAtHome=>vi?"Cư dân ở sinh cảnh ưa thích":"Residents in preferred habitats",
                QuestRequirementKind.CircadianRecords=>vi?"Loài đủ ghi chép ngày/đêm":"Species with active/rest records",
                QuestRequirementKind.IndividualsKnown=>vi?"Cá thể đã rõ cá tính và nơi ưa thích":"Individuals with known traits and habitat",
                QuestRequirementKind.SpeciesRecordedInRealm=>vi?"Loài được ghi chép tại thế giới":"Species recorded in realm",
                QuestRequirementKind.RouteAvailable=>vi?"Tuyến thám hiểm khả dụng":"Expedition route available",
                QuestRequirementKind.RelationPairs=>vi?"Cặp rồng đạt bậc quan hệ":"Pairs at relationship tier",
                QuestRequirementKind.ExpeditionsClaimed=>vi?"Chuyến đã nhận thưởng":"Expeditions claimed",
                _=>def.Kind.ToString()
            };
            string param=def.Param;
            if(vi)param=param switch{"wood"=>"Gỗ","seed"=>"Hạt giống","crystal"=>"Pha lê","coral"=>"San hô",_=>param};
            return name+(string.IsNullOrEmpty(param)?"":" · "+param)+
                (def.Realm.HasValue?" · "+def.Realm:"")+(def.Locomotion.HasValue?" · "+def.Locomotion:"")+
                (def.Tier!=ResearchTier.Unknown?" · "+def.Tier:"")+
                (def.Kind==QuestRequirementKind.RelationPairs?" · "+def.Relation:"");
        }
        public static string Quest(QuestStatusView q,bool enabled,bool vi)
        {
            if(q==null)return "—";
            string status=q.State switch
            {
                QuestState.Completed=>vi?"Đã nhận":"Claimed",
                QuestState.Ready=>vi?"Đủ yêu cầu":"Ready",
                QuestState.Locked=>vi?"Đang khóa":"Locked",
                _=>vi?"Đang thực hiện":"In progress"
            };
            var lines=new List<string>{QuestName(q.QuestId,vi)+" · "+status+" · "+q.ProgressPct+"%",
                q.IsCanonicalGate?(vi?"Nhiệm vụ cổng. Nhận nhiệm vụ không tự mở thế giới.":"Gate quest. Claiming does not unlock the world."):(vi?"Nhiệm vụ phụ":"Side quest"),
                (vi?"Phần thưởng: ":"Reward: ")+q.RewardId};
            if(!enabled)lines.Add(vi?"Chưa bật hoàn thành và phần thưởng.":"Completion and rewards disabled.");
            if(q.LockedBy.Count>0)lines.Add((vi?"Điều kiện mở: ":"Locked by: ")+string.Join("; ",q.LockedBy.Select(m=>Missing(m,vi))));
            if(q.State==QuestState.Completed)return string.Join("\n\n",lines);
            foreach(var req in q.Requirements)
                lines.Add(RequirementName(req.Def,vi)+"\n"+req.Current+"/"+req.Target+" · "+req.State+
                    (string.IsNullOrEmpty(req.Detail)?"":" · "+Missing(req.Detail,vi)));
            if(q.CanComplete&&enabled)lines.Add(vi?"Nhấn Nhận nhiệm vụ để giao vật liệu yêu cầu (nếu có) và nhận thưởng.":"Press Claim quest to deliver required materials (if any) and claim the reward.");
            return string.Join("\n\n",lines);
        }
    }
}
