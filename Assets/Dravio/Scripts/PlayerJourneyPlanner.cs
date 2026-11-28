using System;
using System.Collections.Generic;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Catalog;
using Dravio.Domain.Economy;
using Dravio.Domain.Health;
using Dravio.Domain.Core;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;

namespace Dravio
{
    /// <summary>Drawer pages in the order FantasyPreviewHud builds them for the playable Origin scene.</summary>
    public enum HudPage { Care=0, Biology=1, Dragons=2, Guide=3, Eggs=4, Manage=5, Research=6 }

    /// <summary>Optional one-tap action the banner may run itself (only for actions with no choice to make).</summary>
    public enum JourneyQuickAction { None, ResumeTutorial }

    /// <summary>One "what should I do now?" answer for the HUD. Presentation only: never mutates state.</summary>
    public sealed class JourneyPrompt
    {
        /// <summary>1 = Origin tutorial, 2 = road to Ocean, 3 = road to Sky, 4 = main sanctuary loop.</summary>
        public int Chapter;
        /// <summary>Stable code for tests/analytics, e.g. "tutorial.TUT_02", "claim.pack", "journey.claim-quest".</summary>
        public string Code;
        public HudPage Page;
        /// <summary>Open Research in Quests mode (canonical focus quest) instead of the last mode.</summary>
        public bool QuestMode;
        /// <summary>A reward/claim or safety action waiting; drawn with the reward accent.</summary>
        public bool Urgent;
        public JourneyQuickAction Quick;
        /// <summary>Steps left in this chapter (tutorial steps, or JourneyGuide steps to the next world); -1 in the main loop.</summary>
        public int ChapterRemaining=-1;
        /// <summary>Dragon the camera should frame when Go is tapped (null = none).</summary>
        public string FocusUid;
        public string Progress(bool vi)=>Chapter==1?(vi?"Bước ":"Step ")+(9-Math.Max(1,ChapterRemaining))+"/8":
            ChapterRemaining>0?(vi?"Còn "+ChapterRemaining+" bước":ChapterRemaining+" step"+(ChapterRemaining==1?"":"s")+" left"):"";
        public string TitleVi,TitleEn,HintVi,HintEn;
        public string Title(bool vi)=>vi?TitleVi:TitleEn;
        public string Hint(bool vi)=>vi?HintVi:HintEn;
        public string ChapterLabel(bool vi)=>PlayerJourneyPlanner.ChapterLabel(Chapter,vi);
    }

    /// <summary>
    /// U6.1 player journey: merges the canonical Domain answers (Tutorial.CurrentStep, JourneyGuide, claims,
    /// health cases, expedition/project/breeding availability) into one prioritized next step and the drawer
    /// page that holds its button. Reads only public queries; eligibility stays in the Domain.
    /// Priority: safety (disaster forecast) → waiting rewards → tutorial step → road to next world → ready side
    /// quests → main loop (expedition, sanctuary project, breeding, care).
    /// </summary>
    public static class PlayerJourneyPlanner
    {
        /// <summary>Display name from the species ID (no localized catalog names yet): "emberling" → "Emberling".</summary>
        public static string SpeciesName(string id)=>string.IsNullOrEmpty(id)?"—":char.ToUpperInvariant(id[0])+id.Substring(1);

        public static string ChapterLabel(int chapter,bool vi)=>chapter switch
        {
            1=>vi?"Chương 1 · Khởi Nguyên":"Chapter 1 · Origin",
            2=>vi?"Chương 2 · Đường tới Thủy Vực":"Chapter 2 · Road to Ocean",
            3=>vi?"Chương 3 · Đường tới Thiên Giới":"Chapter 3 · Road to Sky",
            _=>vi?"Khu bảo tồn · Vòng chơi chính":"Sanctuary · Main loop",
        };

        public static int ChapterOf(GameState s,JourneyDto journey)
        {
            if(s.Tutorial.Status==TutorialStatus.Active)return 1;
            if(journey.NextZoneId=="ocean")return 2;
            if(journey.NextZoneId=="sky")return 3;
            return journey.NextZoneId==null?4:2;
        }

        public static JourneyPrompt Next(GameState s)=>Next(s,s==null?null:ProgressionQueries.Journey(s));

        public static JourneyPrompt Next(GameState s,JourneyDto journey)
        {
            if(s==null)return null;
            journey??=ProgressionQueries.Journey(s);
            var p=Core(s,journey);
            if(p==null)return null;
            p.ChapterRemaining=p.Chapter==1?ProgressCatalog.TutorialStepIds.Count-s.Tutorial.Completed.Count:
                p.Chapter<4?journey.Steps.Count:-1;
            p.FocusUid??=FocusFor(s,p);
            return p;
        }

        static string FocusFor(GameState s,JourneyPrompt p)
        {
            if(p.Page!=HudPage.Care)return null;
            // The dragon the step is about: the most grown resident (TUT_07, growth) or the first resident.
            return s.Dragons.Where(d=>d.Location.Kind==LocationKind.Zone && Ecosystem.StatusOf(s,d)==DragonStatus.InHabitat)
                .OrderByDescending(d=>d.XpSec/(double)Math.Max(1,SpeciesCatalog.Get(d.SpeciesId).GrowthTimeSec)).FirstOrDefault()?.Uid;
        }

        static JourneyPrompt Core(GameState s,JourneyDto journey)
        {
            int chapter=ChapterOf(s,journey);
            JourneyPrompt P(string code,HudPage page,string tVi,string tEn,string hVi,string hEn,bool urgent=false,bool quest=false)
                =>new JourneyPrompt{Chapter=chapter,Code=code,Page=page,TitleVi=tVi,TitleEn=tEn,HintVi=hVi,HintEn=hEn,Urgent=urgent,QuestMode=quest};

            // 1. Safety: a forecast/preparation disaster with dragons still exposed.
            foreach(var ev in s.Disasters)
            {
                if(ev.Phase!=DisasterPhase.Forecast && ev.Phase!=DisasterPhase.Preparation)continue;
                bool exposed=s.Dragons.Any(d=>d.Location.Kind==LocationKind.Zone && d.Location.ZoneId==ev.ZoneId &&
                    ev.Sheltered.All(x=>x.Uid!=d.Uid) && DisasterEngine.CheckMoveToShelter(s,d)==null);
                if(exposed)
                    return P("safety.shelter",HudPage.Manage,"Thiên tai sắp tới: đưa rồng vào nơi trú","Disaster coming: shelter your dragons",
                        "Quản lý › Sức khỏe › Đưa vào nơi trú ("+ResearchDiscoveryView.ZoneName(ev.ZoneId,true)+").",
                        "Manage › Health › Move to shelter ("+ResearchDiscoveryView.ZoneName(ev.ZoneId,false)+").",true);
            }
            var actionable=HealthQueries.Cases(s).FirstOrDefault(c=>c.NextStep!="wait" && c.NextStep!="waitForStation");
            if(actionable!=null)
                return P("care.case",HudPage.Manage,"Một rồng cần được chăm sóc","A dragon needs care",
                    "Quản lý › Sức khỏe: "+CareStepVi(actionable.NextStep)+".","Manage › Health: "+CareStepEn(actionable.NextStep)+".",true);

            // 2. Rewards that are already waiting.
            if(s.PendingPack!=null)
            {
                bool tutorialEgg=s.Tutorial.Status==TutorialStatus.Active;
                string sp=SpeciesName(s.PendingPack.SpeciesId),spEn=sp;
                return P("claim.pack",tutorialEgg?HudPage.Guide:HudPage.Eggs,"Trứng đã sẵn sàng: nhận "+sp,"Egg ready: claim "+spEn,
                    (tutorialEgg?"Hướng dẫn":"Trứng")+" › Nhận vào đồng cỏ (hoặc vào kho).",
                    (tutorialEgg?"Guide":"Eggs")+" › Claim to meadow (or storage).",true);
            }
            if(s.BreedingNest!=null && s.BreedingNest.Status==EggStatus.Ready)
                return P("claim.nest",HudPage.Eggs,"Trứng lai đã nở","Bred egg has hatched",
                    "Trứng › Nhận rồng lai vào đồng cỏ hoặc kho.","Eggs › Claim nest to meadow or storage.",true);
            if(ExpeditionPresentation.CanClaim(s))
                return P("claim.expedition",HudPage.Manage,"Rồng thám hiểm đã về","Expedition has returned",
                    "Quản lý › Nhận thưởng thám hiểm để lấy vật liệu.","Manage › Claim expedition rewards to collect materials.",true);

            // 3. Tutorial (chapter 1).
            if(s.Tutorial.Status==TutorialStatus.Active)
            {
                string step=Tutorial.CurrentStep(s.Tutorial);
                var t=TutorialPrompt(s,step);
                if(t!=null){t.Chapter=chapter;return t;}
            }

            // 4. Road to the next world (chapters 2–3), straight from JourneyGuide.
            var first=journey.Steps.FirstOrDefault(x=>x.Code!=JourneyStepCode.UnlockZone || x.Ready) ?? journey.Steps.FirstOrDefault();
            if(first!=null)
            {
                var j=JourneyStepPrompt(s,first,journey.NextZoneId);
                if(j!=null){j.Chapter=chapter;return j;}
            }

            // 5. Side quests already complete.
            if(journey.SideQuestsReady.Count>0)
            {
                string q=journey.SideQuestsReady[0];
                return P("quest.side",HudPage.Research,"Nhiệm vụ phụ hoàn thành: "+ResearchDiscoveryView.QuestName(q,true),
                    "Side quest complete: "+ResearchDiscoveryView.QuestName(q,false),
                    "Sổ › Nhiệm vụ › Nhận nhiệm vụ.","Log › Quests › Claim quest.",true,true);
            }

            // 6. Main loop.
            return LoopPrompt(s,chapter);
        }

        static JourneyPrompt TutorialPrompt(GameState s,string step)
        {
            JourneyPrompt P(HudPage page,string tVi,string tEn,string hVi,string hEn)
                =>new JourneyPrompt{Code="tutorial."+step,Page=page,TitleVi=tVi,TitleEn=tEn,
                    HintVi="Hướng dẫn "+(s.Tutorial.Completed.Count+1)+"/8 · "+hVi,HintEn="Tutorial "+(s.Tutorial.Completed.Count+1)+"/8 · "+hEn};
            switch(step)
            {
                case "TUT_01":
                    if(!s.Dragons.Any(d=>d.Location.Kind==LocationKind.Zone))
                        return P(HudPage.Dragons,"Đưa Emberling ra sinh cảnh","Place Emberling in a habitat","Rồng › Chuyển sinh cảnh.","Dragons › Move to habitat.");
                    return P(HudPage.Care,"Quan sát Emberling","Watch your Emberling","Ngắm rồng vài giây, bước này tự hoàn thành.","Watch for a few seconds; this completes by itself.");
                case "TUT_02":
                    return P(HudPage.Care,"Cho Emberling ăn","Feed your Emberling","Chăm sóc › Cho ăn, rồi chờ rồng tới ăn.","Care › Feed, then let it walk over and eat.");
                case "TUT_03":
                    return P(HudPage.Biology,"Xem hồ sơ sinh học","Read the biology profile","Sinh học › Ghi nhận đã xem sinh học.","Biology › Record profile observation.");
                case "TUT_04":
                    return P(HudPage.Guide,"Mở Bãi đá ấm","Unlock the Warm rocks","Hướng dẫn › Khảo sát rồi Mở khóa bãi đá ấm.","Guide › Survey, then Unlock warm rocks.");
                case "TUT_05":
                    return P(HudPage.Guide,"Ấp quả trứng đầu tiên","Hatch your first egg",
                        s.Tutorial.StarterPackUsed?"Hướng dẫn › Nhận trứng vào đồng cỏ.":"Hướng dẫn › Mua Starter (100 G), rồi nhận trứng.",
                        s.Tutorial.StarterPackUsed?"Guide › Claim the egg to the meadow.":"Guide › Buy Starter (100 G), then claim the egg.");
                case "TUT_06":
                    return P(HudPage.Guide,"Mở Bờ hồ","Unlock the Pondbank","Hướng dẫn › Khảo sát rồi Mở khóa bờ hồ.","Guide › Survey, then Unlock pondbank.");
                case "TUT_07":
                {
                    var best=s.Dragons.Where(d=>d.Location.Kind!=LocationKind.Released)
                        .OrderByDescending(d=>d.XpSec/(double)Math.Max(1,SpeciesCatalog.Get(d.SpeciesId).GrowthTimeSec)).FirstOrDefault();
                    string pct=best==null?"":" ("+(int)Math.Min(99,100*best.XpSec/(double)Math.Max(1,SpeciesCatalog.Get(best.SpeciesId).GrowthTimeSec))+"%)";
                    return P(HudPage.Care,"Nuôi một rồng tới trưởng thành"+pct,"Raise a dragon to adulthood"+pct,
                        "Rồng lớn theo thời gian trong sinh cảnh; cho ăn để lớn nhanh hơn.","Dragons grow over time in a habitat; feeding speeds it up.");
                }
                case "TUT_08":
                    return P(HudPage.Guide,"Mở đủ ba sinh cảnh Khởi Nguyên","Unlock all three Origin habitats","Hướng dẫn › Mở các sinh cảnh còn khóa.","Guide › Unlock the remaining habitats.");
            }
            return null;
        }

        static JourneyPrompt JourneyStepPrompt(GameState s,JourneyStep step,string nextZone)
        {
            string zVi=ResearchDiscoveryView.ZoneName(nextZone,true),zEn=ResearchDiscoveryView.ZoneName(nextZone,false);
            JourneyPrompt P(string code,HudPage page,string tVi,string tEn,string hVi,string hEn,bool urgent=false,bool quest=false,JourneyQuickAction quick=JourneyQuickAction.None)
                =>new JourneyPrompt{Code="journey."+code,Page=page,TitleVi=tVi,TitleEn=tEn,HintVi=hVi,HintEn=hEn,Urgent=urgent,QuestMode=quest,Quick=quick};
            switch(step.Code)
            {
                case JourneyStepCode.ResumeTutorial:
                    return P("resume-tutorial",HudPage.Guide,"Tiếp tục hướng dẫn","Resume the tutorial",
                        "Cần hoàn thành hướng dẫn để mở "+zVi+". Nhấn Đi tới để tiếp tục.","The tutorial is required for "+zEn+". Tap Go to resume.",false,false,JourneyQuickAction.ResumeTutorial);
                case JourneyStepCode.FinishTutorial:
                    return TutorialPrompt(s,step.Target);
                case JourneyStepCode.SurveyHabitat:
                case JourneyStepCode.UnlockHabitat:
                    return P(step.Code.ToWire(),HudPage.Guide,"Mở "+ResearchDiscoveryView.HabitatName(step.Target,true),"Unlock "+ResearchDiscoveryView.HabitatName(step.Target,false),
                        "Hướng dẫn › Khảo sát rồi Mở khóa.","Guide › Survey, then Unlock.");
                case JourneyStepCode.ClaimQuest:
                    return P("claim-quest",HudPage.Research,"Nhận nhiệm vụ: "+ResearchDiscoveryView.QuestName(step.QuestId,true),"Claim quest: "+ResearchDiscoveryView.QuestName(step.QuestId,false),
                        "Sổ › Nhiệm vụ › Nhận nhiệm vụ.","Log › Quests › Claim quest.",true,true);
                case JourneyStepCode.UnlockZone:
                    return P("unlock-zone",HudPage.Manage,"Mở "+zVi+"!","Unlock "+zEn+"!",
                        "Quản lý › Mở "+zVi+", rồi Xem "+zVi+".","Manage › Unlock "+zEn+", then Visit "+zEn+".",true);
                case JourneyStepCode.QuestRequirement:
                    return RequirementPrompt(s,step,zVi,zEn);
            }
            return null;
        }

        static JourneyPrompt RequirementPrompt(GameState s,JourneyStep step,string zVi,string zEn)
        {
            string qVi=ResearchDiscoveryView.QuestName(step.QuestId,true),qEn=ResearchDiscoveryView.QuestName(step.QuestId,false);
            string progress=" "+step.Current+"/"+step.Required;
            string who=step.Candidates!=null&&step.Candidates.Count>0?step.Candidates[0]:null;
            string Name(string id,bool vi)=>id==null?null:SpeciesName(s.FindDragon(id)?.SpeciesId??id);
            string suffixVi=who==null?"":" Gợi ý: "+Name(who,true)+".",suffixEn=who==null?"":" Try: "+Name(who,false)+".";
            string focus=who!=null && s.FindDragon(who)!=null?who:null;
            JourneyPrompt P(HudPage page,string tVi,string tEn,string hVi,string hEn,bool quest=false)
                =>new JourneyPrompt{Code="journey.requirement."+step.Requirement.Value.ToWire(),Page=page,QuestMode=quest,FocusUid=focus,
                    TitleVi=tVi+progress,TitleEn=tEn+progress,HintVi="Nhiệm vụ "+qVi+" (mở "+zVi+"). "+hVi+suffixVi,HintEn="Quest "+qEn+" (unlocks "+zEn+"). "+hEn+suffixEn};
            if(step.Detail=="need-species")
                return P(HudPage.Eggs,"Cần thêm loài rồng phù hợp","Need a suitable species","Trứng › Mua gói hoặc lai tạo rồng mới.","Eggs › Buy a pack or breed a new dragon.");
            switch(step.Requirement.Value)
            {
                case QuestRequirementKind.ResearchTier:
                case QuestRequirementKind.CircadianRecords:
                case QuestRequirementKind.IndividualsKnown:
                case QuestRequirementKind.SpeciesRecordedInRealm:
                    return P(HudPage.Research,"Ghi quan sát nghiên cứu","Record research observations",
                        "Sổ › Quan sát › Ghi quan sát khi rồng đang làm việc gì đó (ngày và đêm).","Log › Observe › Record observation while dragons act (day and night).");
                case QuestRequirementKind.AdultWithLocomotion:
                    return P(HudPage.Dragons,"Có rồng trưởng thành phù hợp","Have a suitable adult dragon",
                        "Rồng › chọn rồng, Chuyển sinh cảnh; chờ rồng lớn.","Dragons › pick one, Move to habitat; let it grow.");
                case QuestRequirementKind.ResidentsThriving:
                case QuestRequirementKind.ResidentsAtHome:
                    return P(HudPage.Dragons,"Đưa rồng về sinh cảnh hợp","Settle dragons in suitable habitats",
                        "Rồng › Sinh cảnh tiếp theo › Chuyển sinh cảnh (điểm hợp cao).","Dragons › Next habitat › Move to habitat (high suitability).");
                case QuestRequirementKind.RelationPairs:
                    return P(HudPage.Care,"Để rồng sống chung","Let dragons live together",
                        "Giữ các rồng chung một sinh cảnh để chúng quen nhau.","Keep dragons in the same habitat so they bond.");
                case QuestRequirementKind.MaterialDelivery:
                case QuestRequirementKind.ExpeditionsClaimed:
                case QuestRequirementKind.RouteAvailable:
                    return P(HudPage.Manage,"Gửi rồng đi thám hiểm","Send a dragon on an expedition",
                        "Quản lý › chọn tuyến, chọn rồng trưởng thành, Khởi hành.","Manage › pick a route and an adult, Depart.");
            }
            return P(HudPage.Research,"Hoàn thành yêu cầu nhiệm vụ","Complete the quest requirement","Sổ › Nhiệm vụ để xem chi tiết.","Log › Quests for details.",true);
        }

        static JourneyPrompt LoopPrompt(GameState s,int chapter)
        {
            JourneyPrompt P(string code,HudPage page,string tVi,string tEn,string hVi,string hEn)
                =>new JourneyPrompt{Chapter=chapter,Code="loop."+code,Page=page,TitleVi=tVi,TitleEn=tEn,HintVi=hVi,HintEn=hEn};
            if(s.Expedition==null)
            {
                var route=Ecosystem.ExpeditionOptions(s).FirstOrDefault(r=>r.Blocked==null && r.Candidates.Any(c=>c.Blocked==null));
                if(route!=null)
                    return P("expedition",HudPage.Manage,"Gửi rồng đi "+route.NameVi,"Send a dragon to "+route.NameVi,
                        "Quản lý › Khởi hành. Vật liệu dùng để xây dự án khu bảo tồn.","Manage › Depart. Materials build sanctuary projects.");
            }
            var economy=ProgressionQueries.SanctuaryEconomy(s,s.Clock.LastProcessedAtMs);
            if(economy.NextProjectCostGoldMicro.HasValue && economy.NextProjectBlockedBy==null)
                return P("project",HudPage.Manage,"Xây dự án khu bảo tồn tiếp theo","Build the next sanctuary project",
                    "Quản lý › Xây dự án tiếp theo.","Manage › Build next project.");
            int adults=s.Dragons.Count(d=>d.Location.Kind==LocationKind.Zone && Ecosystem.StatusOf(s,d)==DragonStatus.InHabitat &&
                EconomyRates.GrowthStageOf(d.XpSec,SpeciesCatalog.Get(d.SpeciesId))==GrowthStage.Adult);
            if(s.BreedingNest==null && adults>=2)
                return P("breed",HudPage.Eggs,"Lai tạo rồng mới","Breed a new dragon",
                    "Trứng › Chọn A, Chọn B, Bắt đầu lai tạo.","Eggs › Select A, Select B, Start breeding.");
            if(s.Expedition!=null)
                return P("wait.expedition",HudPage.Care,"Chăm rồng trong lúc chờ thám hiểm","Care for dragons while the expedition runs",
                    "Cho ăn, vuốt ve, ghi quan sát. Rồng về sẽ có thông báo.","Feed, pet and observe. You will be told when it returns.");
            return P("care",HudPage.Care,"Chăm sóc và quan sát rồng","Care for and observe your dragons",
                "Cho ăn để rồng lớn, vàng tăng theo thời gian để mua trứng và nâng cấp.","Feed to grow them; gold accrues over time for eggs and upgrades.");
        }

        static string CareStepVi(string next)=>next switch
        {
            "assessCase"=>"Nhận định ca",
            "setCarePlan|startCare"=>"Chọn kế hoạch rồi Bắt đầu chăm sóc",
            "reviewCase"=>"Lượng giá phục hồi",
            "dischargeDragon"=>"Xuất viện",
            _=>next
        };
        static string CareStepEn(string next)=>next switch
        {
            "assessCase"=>"Assess case",
            "setCarePlan|startCare"=>"Apply a care plan, then Start care",
            "reviewCase"=>"Review recovery",
            "dischargeDragon"=>"Discharge",
            _=>next
        };

        /// <summary>
        /// End-of-chapter screen content when this batch of events closes a chapter (tutorial done, a world
        /// unlocked), with any reward egg granted in the same batch; null otherwise.
        /// </summary>
        public static AwayReport ChapterFinale(GameState s,IReadOnlyList<DomainEvent> events)
        {
            int done=0;string zone=null;
            foreach(var e in events)
            {
                if(e is TutorialStatusChanged c && c.Status==TutorialStatus.Completed)done=Math.Max(done,1);
                if(e is ZoneUnlocked z){zone=z.ZoneId;done=Math.Max(done,z.ZoneId=="sky"?3:2);}
            }
            if(done==0)return null;
            var o=new AwayReport{Id="chapter-"+done,TitleVi="Hoàn thành "+ChapterLabel(done,true),TitleEn="Completed "+ChapterLabel(done,false)};
            if(zone!=null)
            {
                o.LinesVi.Add("Thế giới mới đã mở: "+ResearchDiscoveryView.ZoneName(zone,true)+". Vào Quản lý › Xem để ghé thăm.");
                o.LinesEn.Add("New world unlocked: "+ResearchDiscoveryView.ZoneName(zone,false)+". Manage › Visit to go there.");
            }
            else{o.LinesVi.Add("Khu bảo tồn Khởi Nguyên đã đủ ba sinh cảnh.");o.LinesEn.Add("Origin now has all three habitats.");}
            foreach(var e in events)
                if(e is RewardEggGranted g)
                {
                    o.LinesVi.Add("Phần thưởng: trứng "+SpeciesName(g.SpeciesId)+(g.Delivered?" (nhận ở tab Trứng)":" (đang xếp hàng, kho đầy)"));
                    o.LinesEn.Add("Reward: "+SpeciesName(g.SpeciesId)+" egg"+(g.Delivered?" (claim it in Eggs)":" (queued, storage full)"));
                }
            int next=done+1;
            o.LinesVi.Add("Tiếp theo: "+ChapterLabel(next>3?4:next,true));o.LinesEn.Add("Next: "+ChapterLabel(next>3?4:next,false));
            return o;
        }

        /// <summary>Short celebration for a domain event, or null. Shown as a toast above the banner.</summary>
        public static string Celebrate(GameState s,DomainEvent e,bool vi)
        {
            switch(e)
            {
                case TutorialStepCompleted t:
                    return (vi?"Hoàn thành bước hướng dẫn ":"Tutorial step done ")+(ProgressCatalog.TutorialStepIds.ToList().IndexOf(t.StepId)+1)+"/8";
                case TutorialStatusChanged c when c.Status==TutorialStatus.Completed:
                    return vi?"Hoàn thành hướng dẫn! Hành trình tới Thủy Vực bắt đầu.":"Tutorial complete! The road to Ocean begins.";
                case HabitatUnlocked h:
                    return (vi?"Mở sinh cảnh: ":"Habitat unlocked: ")+ResearchDiscoveryView.HabitatName(h.HabitatId,vi);
                case ZoneUnlocked z:
                    return (vi?"Thế giới mới: ":"New world: ")+ResearchDiscoveryView.ZoneName(z.ZoneId,vi)+(vi?" · Quản lý › Xem để ghé thăm":" · Manage › Visit to go there");
                case QuestCompleted q:
                    return (vi?"Hoàn thành nhiệm vụ: ":"Quest complete: ")+ResearchDiscoveryView.QuestName(q.QuestId,vi);
                case ExpeditionReady:
                    return vi?"Rồng thám hiểm đã về!":"Your expedition is back!";
            }
            return null;
        }
    }
}
