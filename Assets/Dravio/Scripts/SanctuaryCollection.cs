using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dravio.Domain;
using Dravio.Domain.Breeding;
using Dravio.Domain.Catalog;
using Dravio.Domain.Economy;
using Dravio.Domain.Progression;
using Dravio.Domain.Queries;

namespace Dravio
{
    /// <summary>One cell of the collection screen. Silhouette = not yet discovered (no Codex entry).</summary>
    public sealed class CollectionEntry
    {
        public string SpeciesId,Name;
        public Rarity Rarity;
        public bool Discovered;
        public int Owned;
        /// <summary>Research tier wire name (sighted, profiled, …) for discovered species, else null.</summary>
        public string Tier;
        public string HowVi,HowEn;
        public string How(bool vi)=>vi?HowVi:HowEn;
    }

    /// <summary>
    /// U6.2 collection screen (DragonVale-style book): every catalog species in catalog order, discovered or a
    /// silhouette, with how to get it read from the real rules: pack odds by rarity, world reward eggs,
    /// the Aurorawing expedition milestone, and BreedingRules.CandidateSpecies for parent pairs. Read-only.
    /// </summary>
    public static class SanctuaryCollection
    {
        public static string RarityName(Rarity r,bool vi)=>r switch
        {
            Rarity.Common=>vi?"Thường":"Common",
            Rarity.Rare=>vi?"Hiếm":"Rare",
            Rarity.Epic=>vi?"Sử thi":"Epic",
            _=>vi?"Huyền thoại":"Legendary",
        };
        static string Element(ElementType e,bool vi)=>e switch
        {
            ElementType.Fire=>vi?"Lửa":"Fire",
            ElementType.Water=>vi?"Nước":"Water",
            ElementType.Nature=>vi?"Thiên nhiên":"Nature",
            ElementType.Sky=>vi?"Trời":"Sky",
            _=>e.ToString()
        };

        public static List<CollectionEntry> Build(GameState s)
        {
            var list=new List<CollectionEntry>();
            foreach(var sp in SpeciesCatalog.All)
            {
                var codex=Codex.Find(s,sp.Id);
                var e=new CollectionEntry
                {
                    SpeciesId=sp.Id,Name=PlayerJourneyPlanner.SpeciesName(sp.Id),Rarity=sp.Rarity,Discovered=codex!=null,
                    Owned=s.Dragons.Count(d=>d.SpeciesId==sp.Id && d.Location.Kind!=LocationKind.Released),
                };
                if(codex!=null)e.Tier=ProgressionQueries.ResearchProfile(s,sp.Id).Species.Tier.ToWire();
                Ways(sp,out e.HowVi,out e.HowEn);
                list.Add(e);
            }
            return list;
        }

        public static int DiscoveredCount(GameState s)=>SpeciesCatalog.All.Count(sp=>Codex.Find(s,sp.Id)!=null);

        static void Ways(DragonSpecies sp,out string vi,out string en)
        {
            var wVi=new List<string>();var wEn=new List<string>();
            if(sp.PackEligible)
            {
                var packs=PackCatalog.All.Where(p=>PackCatalog.Odds(p,sp.Rarity)>0).Select(p=>PlayerJourneyPlanner.SpeciesName(p.Id)).ToList();
                if(packs.Count>0){wVi.Add("Gói "+string.Join(", ",packs));wEn.Add(string.Join(", ",packs)+" pack");}
            }
            foreach(var rule in WorldUnlockCatalog.Rules)
                if(rule.EggSpeciesId==sp.Id)
                {
                    wVi.Add("Trứng thưởng khi mở "+ResearchDiscoveryView.ZoneName(rule.ZoneId,true));
                    wEn.Add("Reward egg for unlocking "+ResearchDiscoveryView.ZoneName(rule.ZoneId,false));
                }
            if(sp.Id==SkyCloudBalance.AurorawingSpeciesId)
            {
                wVi.Add("Trứng thưởng sau "+SkyCloudBalance.AurorawingEggClaims+" chuyến Sân mây");
                wEn.Add("Reward egg after "+SkyCloudBalance.AurorawingEggClaims+" Cloud terrace expeditions");
            }
            var pair=BestPair(sp);
            if(pair!=null)
            {
                wVi.Add("Lai "+PlayerJourneyPlanner.SpeciesName(pair.Item1)+" × "+PlayerJourneyPlanner.SpeciesName(pair.Item2));
                wEn.Add("Breed "+PlayerJourneyPlanner.SpeciesName(pair.Item1)+" × "+PlayerJourneyPlanner.SpeciesName(pair.Item2));
            }
            string elems=string.Join(" + ",sp.Elements.Select(x=>Element(x,true))),elemsEn=string.Join(" + ",sp.Elements.Select(x=>Element(x,false)));
            vi=elems+" · "+(wVi.Count>0?string.Join(" · ",wVi):"Chưa có cách nhận trong bản hiện tại");
            en=elemsEn+" · "+(wEn.Count>0?string.Join(" · ",wEn):"Not obtainable in this build yet");
        }

        /// <summary>The cheapest parent pair (lowest combined rarity, catalog order) whose offspring may be this species.</summary>
        static Tuple<string,string> BestPair(DragonSpecies target)
        {
            Tuple<string,string> best=null;int bestRank=int.MaxValue;
            var all=SpeciesCatalog.All;
            for(int i=0;i<all.Count;i++)for(int j=i;j<all.Count;j++)
            {
                var a=all[i];var b=all[j];
                if(a.Id==target.Id||b.Id==target.Id)continue;
                if(!BreedingRules.CandidateSpecies(a,b).Any(x=>x.Id==target.Id))continue;
                int rank=(int)a.Rarity+(int)b.Rarity;
                if(rank<bestRank){bestRank=rank;best=Tuple.Create(a.Id,b.Id);}
            }
            return best;
        }
    }

    /// <summary>"While you were away" summary for the first screen after an offline settlement.</summary>
    public sealed class AwayReport
    {
        public string Id;
        public string TitleVi,TitleEn;
        public List<string> LinesVi=new List<string>(),LinesEn=new List<string>();
        public string Title(bool vi)=>vi?TitleVi:TitleEn;
        public IReadOnlyList<string> Lines(bool vi)=>vi?LinesVi:LinesEn;
    }

    public static class AwaySummary
    {
        public static string Duration(long ms,bool vi)
        {
            long min=Math.Max(1,ms/60000),h=min/60,m=min%60;
            if(h>=24){long d=h/24;h%=24;return vi?d+" ngày "+h+" giờ":d+"d "+h+"h";}
            return h>0?(vi?h+" giờ "+m+" phút":h+"h "+m+"m"):(vi?m+" phút":m+"m");
        }

        /// <summary>null when there is no offline receipt. Same receipt id = same report (show once per id).</summary>
        public static AwayReport Build(GameState s)
        {
            var r=s?.OfflineReceipt;
            if(r==null)return null;
            var o=new AwayReport{Id=r.Id};
            string d=Duration(r.ToMs-r.FromMs,true),dEn=Duration(r.ToMs-r.FromMs,false);
            o.TitleVi="Trong lúc bạn vắng mặt ("+d+")";o.TitleEn="While you were away ("+dEn+")";
            string gold=(r.GoldMicro/(decimal)MicroGold.MicroPerGold).ToString("0.##",CultureInfo.InvariantCulture);
            if(r.GoldMicro>0){o.LinesVi.Add("Khu bảo tồn sinh +"+gold+" vàng");o.LinesEn.Add("Your sanctuary earned +"+gold+" gold");}
            if(r.MaturedIds!=null && r.MaturedIds.Count>0)
            {
                var names=string.Join(", ",r.MaturedIds.Select(id=>s.FindDragon(id)).Where(x=>x!=null).Select(x=>PlayerJourneyPlanner.SpeciesName(x.SpeciesId)).Distinct());
                o.LinesVi.Add(r.MaturedIds.Count+" rồng đã trưởng thành: "+names);o.LinesEn.Add(r.MaturedIds.Count+" dragon(s) grew up: "+names);
            }
            if(s.BreedingNest!=null && s.BreedingNest.Status==EggStatus.Ready){o.LinesVi.Add("Trứng lai đã nở, đang chờ nhận");o.LinesEn.Add("A bred egg hatched and is waiting");}
            if(s.PendingPack!=null){o.LinesVi.Add("Có trứng đang chờ nhận");o.LinesEn.Add("An egg is waiting to be claimed");}
            if(s.Expedition!=null && s.Expedition.Status==ExpeditionStatus.Ready){o.LinesVi.Add("Rồng thám hiểm đã về với vật liệu");o.LinesEn.Add("Your expedition returned with materials");}
            else if(s.Expedition!=null){o.LinesVi.Add("Rồng vẫn đang thám hiểm");o.LinesEn.Add("Your expedition is still out");}
            if(s.RewardQueue!=null && s.RewardQueue.Count>0){o.LinesVi.Add(s.RewardQueue.Count+" trứng thưởng đang xếp hàng");o.LinesEn.Add(s.RewardQueue.Count+" reward egg(s) queued");}
            if(o.LinesVi.Count==0){o.LinesVi.Add("Rồng nghỉ ngơi bình yên.");o.LinesEn.Add("Your dragons rested peacefully.");}
            return o;
        }
    }
}
