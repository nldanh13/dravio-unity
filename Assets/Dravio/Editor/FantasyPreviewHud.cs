using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Dravio.Platform.Unity;
using Dravio.Character;
using Dravio.Domain.Catalog;
using Dravio.EnvironmentArt;
using Dravio.EnvironmentArt.EditorTools;
using Object = UnityEngine.Object;

namespace Dravio.Editor
{
    internal static class FantasyPreviewHud
    {
        [Serializable] private class BiologyRoot { public Biology emberling; }
        [Serializable] private class Biology
        {
            public string group,respiration,protection,activity,silhouetteVi;
            public string[] locomotion,dietIds,thermalBands,habitatRules;
        }

        private static readonly Color Ink=new Color(.15f,.11f,.07f);
        private static readonly Color Wood=new Color(.19f,.15f,.10f,.96f);
        private static readonly Color Gold=new Color(.91f,.78f,.51f);
        private static readonly Color Paper=new Color(.95f,.88f,.72f);
        private static readonly Color Muted=new Color(.76f,.70f,.56f);
        private sealed class Layout
        {
            public Font font;
            public readonly List<FantasyPreviewUI.Copy> copy=new();
        }

        public static void Build(GameObject dragon,Animator animator,DragonTurntable turntable,bool integrated=false,bool playable=false)
        {
            var asset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Dravio/Data/species-biology.json");
            if(asset==null)throw new InvalidOperationException("Canonical biology JSON missing.");
            var biology=JsonUtility.FromJson<BiologyRoot>(asset.text)?.emberling;
            if(biology==null || biology.locomotion==null || biology.dietIds==null ||
                biology.thermalBands==null || biology.habitatRules==null)
                throw new InvalidOperationException("Emberling biology incomplete.");
            // On this new scene only, replace the old visual placeholder HUD.
            foreach(var old in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if(old.renderMode!=RenderMode.WorldSpace)
                {
                    old.enabled=false;
                    foreach(var raycaster in old.GetComponents<GraphicRaycaster>())raycaster.enabled=false;
                }
            var canvasGo=new GameObject("Dravio Fantasy HUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            var canvas=canvasGo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,720);
            scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            EnsureEventSystem();
            var ui=new Layout {font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")};
            var safe=RectNode("Safe area",canvasGo.transform);
            Stretch(safe,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var header=Surface("Header",safe,Wood);
            Stretch(header,new Vector2(0,1),Vector2.one,new Vector2(12,-60),new Vector2(-12,-12));
            var bar=header.gameObject.AddComponent<HorizontalLayoutGroup>();
            bar.padding=new RectOffset(12,8,5,5);bar.spacing=6;bar.childAlignment=TextAnchor.MiddleLeft;
            bar.childControlWidth=true;bar.childControlHeight=true;bar.childForceExpandHeight=true;bar.childForceExpandWidth=false;
            var brand=Label(ui,header,"DRAVIO","DRAVIO",23,Gold,TextAnchor.MiddleLeft,38);
            brand.GetComponent<LayoutElement>().flexibleWidth=1;
            Text gold=null;
            if(integrated)
            {
                gold=Label(ui,header,"— G","— G",20,Gold,TextAnchor.MiddleCenter,38,false);
                gold.GetComponent<LayoutElement>().preferredWidth=128;
            }
            var lang=Button(ui,header,"Tiếng Việt","English",38,108);
            var hide=Button(ui,header,"Hide","Ẩn",38,70);
            var panel=Surface("Sanctuary drawer",safe,Wood);
            Stretch(panel,new Vector2(1,0),new Vector2(1,1),new Vector2(-326,12),new Vector2(-12,-72));
            var vertical=panel.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.padding=new RectOffset(10,10,10,10);vertical.spacing=8;
            vertical.childControlHeight=true;vertical.childControlWidth=true;
            vertical.childForceExpandHeight=false;vertical.childForceExpandWidth=true;
            if(playable)panel.gameObject.AddComponent<SanctuaryUiInsetSource>();

            var selectedTitle=Label(ui,panel,integrated?"Emberling • Normal":"Emberling • Baby / Normal",integrated?"Emberling • Thường":"Emberling • Rồng non / Thường",20,Gold,TextAnchor.MiddleLeft,40,!playable);
            var status=Label(ui,panel,"Resting","Đang nghỉ",16,Paper,TextAnchor.MiddleLeft,30,false);
            var tabsRoot=RectNode("Navigation",panel);
            var tabLayout=tabsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabLayout.spacing=5;tabLayout.childControlWidth=true;tabLayout.childControlHeight=true;
            tabLayout.childForceExpandWidth=true;tabLayout.childForceExpandHeight=true;
            var navSize=tabsRoot.gameObject.AddComponent<LayoutElement>();
            navSize.minHeight=40;navSize.preferredHeight=44;navSize.flexibleHeight=0;navSize.layoutPriority=10;
            tabLayout.childForceExpandHeight=false;
            var tabs=new List<Button>{Button(ui,tabsRoot,"Care","Chăm sóc",44),Button(ui,tabsRoot,"Biology","Sinh học",44),Button(ui,tabsRoot,"Dragons","Rồng",44)};
            if(playable)
            {
                // Two rows keep the existing tabs usable on narrow landscape screens.
                var second=RectNode("Management navigation",panel);
                var layout=second.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing=5;layout.childControlWidth=true;layout.childControlHeight=true;
                layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                var size=second.gameObject.AddComponent<LayoutElement>();
                size.minHeight=40;size.preferredHeight=44;size.layoutPriority=10;
                tabs.Add(Button(ui,second,"Guide","Hướng dẫn",44));
                tabs.Add(Button(ui,second,"Eggs","Trứng",44));
                tabs.Add(Button(ui,second,"Manage","Quản lý",44));
            }
            var host=RectNode("Page host",panel);
            var hostSize=host.gameObject.AddComponent<LayoutElement>();
            hostSize.minHeight=0;hostSize.preferredHeight=0;hostSize.flexibleHeight=1;hostSize.layoutPriority=10;
            var care=Page(host,"Care");
            var info=Page(host,"Biology");
            var roster=Page(host,"Dragons");

            Text progress=null,notice=null,saveStatus=null,tutorialStatus=null,rosterStatus=null,guideFeedback=null;
            RectTransform guide=null,hatchery=null,management=null;
            Text healthStatus=null,careCaseStatus=null,carePlanStatus=null;
            Button healthNext=null,healthShelter=null,healthAssess=null,healthNextPlan=null,healthApplyPlan=null,healthStart=null,healthReview=null,healthDischarge=null;
            Text managementSummary=null,managementWorld=null,managementCodex=null,managementMaterials=null,managementExpedition=null,managementFeedback=null;
            var upgradeStatus=new List<Text>();
            var upgradeButtons=new List<Button>();
            Button unlockOcean=null,unlockSky=null,claimExpedition=null;
            Text hatcheryStatus=null,parentsStatus=null,hatcheryFeedback=null;
            Button standard=null,elemental=null,mystic=null,astral=null,packMeadow=null,packStorage=null,selectA=null,selectB=null,breed=null,tap=null,breedMeadow=null,breedStorage=null;
            Button saveButton=null,bioRecord=null,surveyRocks=null,unlockRocks=null,surveyPond=null,unlockPond=null,buy=null,claim=null,store=null;
            if(integrated)
            {
                progress=Label(ui,care,"—","—",15,Paper,TextAnchor.MiddleLeft,48,false);
                notice=Label(ui,care,"Session only","Chỉ trong phiên chơi",14,Muted,TextAnchor.MiddleLeft,52,false);
            }
            var row1=ActionRow(care);
            var feed=Button(ui,row1,"Feed","Cho ăn",56);
            var pet=Button(ui,row1,"Pet","Vuốt ve",56);
            var row2=ActionRow(care);
            var sleep=Button(ui,row2,playable?"Doze / auto":"Doze / wake",playable?"Nghỉ / tự động":"Ngủ / thức",56);
            var inspect=Button(ui,row2,"Inspect","Quan sát",56);
            var resume=Button(ui,care,"Resume exploring","Tiếp tục khám phá",52);
            Label(ui,care,playable?"Tap a dragon or select it in Dragons. Inspect follows the selected model.":"Drag to rotate in inspection. F / P / N / Space / R on keyboard.",playable?"Chạm vào rồng hoặc chọn trong danh sách Rồng. Quan sát sẽ theo dõi cá thể.":"Kéo để xoay khi quan sát. Bàn phím: F / P / N / Space / R.",14,Muted,TextAnchor.MiddleLeft,64);
            if(playable)
            {
                saveStatus=Label(ui,care,"Loading save","Đang mở lưu trữ",14,Paper,TextAnchor.MiddleLeft,32,false);
                saveButton=Button(ui,care,"Save now","Lưu ngay",44);
            }
            else if(integrated)Label(ui,care,"Session-only game data; no save or offline reward. Sleep and inspection are visual.","Dữ liệu trong phiên; chưa lưu hay thưởng offline. Ngủ và quan sát chỉ là hoạt ảnh.",14,Muted,TextAnchor.MiddleLeft,70);
            else Label(ui,care,"Visual actions only. No food or bond changes yet.","Chỉ thử hoạt ảnh. Chưa trừ thức ăn hay thay đổi gắn kết.",14,Muted,TextAnchor.MiddleLeft,54);

            Label(ui,info,"Emberling reference","Hồ sơ Emberling",18,Gold,TextAnchor.MiddleLeft,36);
            if(playable)bioRecord=Button(ui,info,"Record profile observation","Ghi nhận đã xem sinh học",44);
            Fact(ui,info,"Group","Nhóm",biology.group,biology.group);
            Fact(ui,info,"Respiration","Hô hấp",biology.respiration,Translate(biology.respiration));
            Fact(ui,info,"Locomotion","Di chuyển",Join(biology.locomotion,false),Join(biology.locomotion,true));
            Fact(ui,info,"Protection","Bảo vệ",biology.protection,Translate(biology.protection));
            Fact(ui,info,"Activity","Hoạt động",biology.activity,Translate(biology.activity));
            Fact(ui,info,"Diet","Thức ăn",Join(biology.dietIds,false),Join(biology.dietIds,true));
            Fact(ui,info,"Temperature","Nhiệt độ",Join(biology.thermalBands,false),Join(biology.thermalBands,true));
            Fact(ui,info,"Habitats","Sinh cảnh",Join(biology.habitatRules,false),Join(biology.habitatRules,true));
            Label(ui,info,"Four legs, round flame tail, no wings.",biology.silhouetteVi,15,Paper,TextAnchor.MiddleLeft,62);
            Label(ui,info,"Reference data, not live health or comfort.","Dữ liệu loài, không phải chỉ số sức khỏe hay tiện nghi hiện tại.",13,Muted,TextAnchor.MiddleLeft,56);

            Label(ui,roster,"Preview residents","Rồng trong bản thử",18,Gold,TextAnchor.MiddleLeft,36);
            if(!playable)Label(ui,roster,integrated?"Emberling • Normal":"Emberling • Baby / Normal",integrated?"Emberling • Thường":"Emberling • Rồng non / Thường",17,Paper,TextAnchor.MiddleLeft,42);
            var rosterInspect=Button(ui,roster,playable?"Inspect selected":"Inspect Emberling",playable?"Quan sát rồng đã chọn":"Quan sát Emberling",46);
            Text livingProfile=null,livingTravel=null;
            Button nextDragon=null,observeDragon=null,nextTravel=null,moveDragon=null,storeDragon=null;
            if(playable)
            {
                rosterStatus=Label(ui,roster,"Loading residents","Đang tải danh sách",15,Paper,TextAnchor.UpperLeft,120,false);
                livingProfile=Label(ui,roster,"Loading character catalog","Đang tải thư viện rồng",15,Paper,TextAnchor.UpperLeft,420,false);
                nextDragon=Button(ui,roster,"Next dragon","Rồng tiếp theo",46);
                observeDragon=Button(ui,roster,"Record species observation","Ghi nhận hồ sơ loài",46);
                livingTravel=Label(ui,roster,"—","—",14,Muted,TextAnchor.UpperLeft,80,false);
                nextTravel=Button(ui,roster,"Next habitat","Sinh cảnh tiếp theo",46);
                moveDragon=Button(ui,roster,"Move to habitat","Chuyển sinh cảnh",46);
                storeDragon=Button(ui,roster,"Move to storage","Chuyển vào kho",46);
                foreach(var button in new[]{nextDragon,observeDragon,nextTravel,moveDragon,storeDragon})button.interactable=false;
                Label(ui,roster,"Only catalog models with a safe spawn are shown. Stage fallback and missing models are reported.","Chỉ hiển thị model đúng loài có điểm đặt an toàn. Có báo model thiếu hoặc thay thế độ tuổi.",14,Muted,TextAnchor.MiddleLeft,80);
                guide=Page(host,"Guide");
                tutorialStatus=Label(ui,guide,"Loading tutorial","Đang tải hướng dẫn",16,Gold,TextAnchor.MiddleLeft,54,false);
                guideFeedback=Label(ui,guide,"—","—",14,Muted,TextAnchor.MiddleLeft,42,false);
                Label(ui,guide,"Observe, feed and record biology first. Survey and unlock habitats, then buy and claim the Starter Pack.","Quan sát, cho ăn và ghi nhận sinh học trước. Khảo sát, mở khóa sinh cảnh rồi mua và nhận gói Starter.",14,Paper,TextAnchor.MiddleLeft,88);
                surveyRocks=Button(ui,guide,"Survey warm rocks","Khảo sát bãi đá ấm",46);
                unlockRocks=Button(ui,guide,"Unlock warm rocks","Mở khóa bãi đá ấm",46);
                buy=Button(ui,guide,"Buy Starter Pack · 100 G","Mua Starter · 100 G",46);
                claim=Button(ui,guide,"Claim to meadow","Nhận vào đồng cỏ",46);
                store=Button(ui,guide,"Claim to storage","Nhận vào kho",46);
                surveyPond=Button(ui,guide,"Survey pondbank","Khảo sát bờ hồ",46);
                unlockPond=Button(ui,guide,"Unlock pondbank","Mở khóa bờ hồ",46);
                hatchery=Page(host,"Eggs");
                hatcheryStatus=Label(ui,hatchery,"Loading hatchery","Đang tải tổ ấp",16,Gold,TextAnchor.MiddleLeft,62,false);
                hatcheryFeedback=Label(ui,hatchery,"—","—",14,Muted,TextAnchor.MiddleLeft,44,false);
                Label(ui,hatchery,"Egg packs • paid in gold","Gói trứng • mua bằng vàng",17,Gold,TextAnchor.MiddleLeft,34);
                standard=Button(ui,hatchery,"Starter · "+PackCatalog.Get("starter").PriceG+" G","Starter · "+PackCatalog.Get("starter").PriceG+" G",46);
                elemental=Button(ui,hatchery,"Elemental · "+PackCatalog.Get("elemental").PriceG+" G","Elemental · "+PackCatalog.Get("elemental").PriceG+" G",46);
                mystic=Button(ui,hatchery,"Mystic · "+PackCatalog.Get("mystic").PriceG+" G","Mystic · "+PackCatalog.Get("mystic").PriceG+" G",46);
                astral=Button(ui,hatchery,"Astral · "+PackCatalog.Get("astral").PriceG+" G","Astral · "+PackCatalog.Get("astral").PriceG+" G",46);
                packMeadow=Button(ui,hatchery,"Claim pack to meadow","Nhận gói vào đồng cỏ",46);
                packStorage=Button(ui,hatchery,"Claim pack to storage","Nhận gói vào kho",46);
                Label(ui,hatchery,"Breeding nest","Tổ ấp lai tạo",17,Gold,TextAnchor.MiddleLeft,34);
                parentsStatus=Label(ui,hatchery,"Choose two adults","Chọn hai rồng trưởng thành",15,Paper,TextAnchor.MiddleLeft,94,false);
                var parentRow=ActionRow(hatchery);
                selectA=Button(ui,parentRow,"Select A","Chọn A",48);
                selectB=Button(ui,parentRow,"Select B","Chọn B",48);
                breed=Button(ui,hatchery,"Start breeding","Bắt đầu lai tạo",46);
                tap=Button(ui,hatchery,"Tap egg","Chạm trứng",46);
                breedMeadow=Button(ui,hatchery,"Claim nest to meadow","Nhận rồng lai vào đồng cỏ",46);
                breedStorage=Button(ui,hatchery,"Claim nest to storage","Nhận rồng lai vào kho",46);
                Label(ui,hatchery,"No ads. Breeding eligibility includes health checks.","Không quảng cáo. Điều kiện lai bao gồm kiểm tra sức khỏe.",13,Muted,TextAnchor.MiddleLeft,64);

                management=Page(host,"Management");
                Label(ui,management,"Sanctuary overview","Tổng quan khu bảo tồn",18,Gold,TextAnchor.MiddleLeft,36);
                managementSummary=Label(ui,management,"Loading","Đang tải",15,Paper,TextAnchor.UpperLeft,110,false);
                managementFeedback=Label(ui,management,"—","—",14,Muted,TextAnchor.MiddleLeft,60,false);
                Label(ui,management,"Health & care","Sức khỏe & chăm sóc",18,Gold,TextAnchor.MiddleLeft,36);
                healthStatus=Label(ui,management,"Loading","Đang tải",15,Paper,TextAnchor.UpperLeft,180,false);
                healthNext=Button(ui,management,"Next dragon","Rồng tiếp theo",46);
                healthShelter=Button(ui,management,"Move to shelter","Đưa vào nơi trú",46);
                careCaseStatus=Label(ui,management,"Loading","Đang tải",15,Paper,TextAnchor.UpperLeft,155,false);
                healthAssess=Button(ui,management,"Assess case","Nhận định ca",46);
                carePlanStatus=Label(ui,management,"—","—",14,Paper,TextAnchor.UpperLeft,100,false);
                healthNextPlan=Button(ui,management,"Next care plan","Kế hoạch tiếp theo",46);
                healthApplyPlan=Button(ui,management,"Apply care plan","Chọn kế hoạch",46);
                healthStart=Button(ui,management,"Start care","Bắt đầu chăm sóc",46);
                healthReview=Button(ui,management,"Review recovery","Lượng giá phục hồi",46);
                healthDischarge=Button(ui,management,"Discharge to storage","Xuất viện về kho",46);
                Label(ui,management,"World progress","Tiến độ thế giới",18,Gold,TextAnchor.MiddleLeft,36);
                managementWorld=Label(ui,management,"Loading","Đang tải",15,Paper,TextAnchor.UpperLeft,240,false);
                unlockOcean=Button(ui,management,"Unlock Ocean","Mở Thủy Vực",46);
                unlockSky=Button(ui,management,"Unlock Sky","Mở Thiên Giới",46);
                Label(ui,management,"World unlocking does not load a new 3D scene. Quest gameplay is pending.","Mở khóa thế giới chưa chuyển cảnh 3D. Nội dung nhiệm vụ đang chờ.",14,Muted,TextAnchor.MiddleLeft,66);
                Label(ui,management,"Upgrades","Nâng cấp",18,Gold,TextAnchor.MiddleLeft,36);
                foreach(var def in EconomyTables.Upgrades)
                {
                    upgradeStatus.Add(Label(ui,management,"—","—",15,Paper,TextAnchor.MiddleLeft,50,false));
                    upgradeButtons.Add(Button(ui,management,"Buy next level","Mua cấp tiếp",46));
                }
                Label(ui,management,"Capacity upgrade increases the production budget, not physical habitat slots.","Nâng cấp sức chứa tăng ngân sách sản xuất, không tăng chỗ ở vật lý.",14,Muted,TextAnchor.MiddleLeft,66);
                Label(ui,management,"Expedition","Thám hiểm",18,Gold,TextAnchor.MiddleLeft,36);
                managementExpedition=Label(ui,management,"Loading","Đang tải",15,Paper,TextAnchor.UpperLeft,105,false);
                claimExpedition=Button(ui,management,"Claim expedition rewards","Nhận thưởng thám hiểm",46);
                Label(ui,management,"Starting expeditions is deferred until health checks and living-dragon presentation are integrated.","Chưa mở nút khởi hành cho đến khi tích hợp kiểm tra sức khỏe và hiển thị rồng.",14,Muted,TextAnchor.MiddleLeft,80);
                Label(ui,management,"Materials","Vật liệu",18,Gold,TextAnchor.MiddleLeft,36);
                managementMaterials=Label(ui,management,"Loading","Đang tải",15,Paper,TextAnchor.UpperLeft,130,false);
                Label(ui,management,"Codex","Bách khoa",18,Gold,TextAnchor.MiddleLeft,36);
                managementCodex=Label(ui,management,"Loading","Đang tải",15,Paper,TextAnchor.UpperLeft,500,false);
            }
            else Label(ui,roster,integrated?"One founder dragon in this session. Additional residents await gameplay integration.":"Only the current preview dragon is available. The full roster awaits Core Domain integration.",integrated?"Một rồng khởi tạo trong phiên này. Thêm rồng sẽ có khi nối gameplay.":"Hiện chỉ có rồng thử nghiệm. Danh sách rồng thật sẽ được nối với Core Domain.",14,Muted,TextAnchor.MiddleLeft,90);

            var controller=dragon.AddComponent<DragonCarePreview>();
            controller.Configure(animator,turntable,status,feed,pet,sleep,inspect,resume);
            if(integrated)
            {
                var session=dragon.AddComponent<SanctuaryDomainPreview>();
                var fruitVisual=CreateFruit();
                session.Configure(gold,progress,notice,fruitVisual);
                controller.ConfigureDomain(session);
                if(playable)
                {
                    var hostGo=new GameObject("Dravio Session Host");
                    var persistence=hostGo.AddComponent<DravioSessionHost>();
                    var locationData=AssetDatabase.LoadAssetAtPath<SanctuaryLocationData>(OriginSanctuaryEnvironmentBuilder.LocationsAssetPath);
                    if(locationData==null)throw new InvalidOperationException("Playable location data missing.");
                    session.ConfigurePersistent(persistence,locationData,saveStatus,tutorialStatus,rosterStatus,guideFeedback);
                    var visualRoot=new GameObject("Living Sanctuary Presentation");
                    var manager=visualRoot.AddComponent<DragonVisualManager>();
                    manager.catalog=AssetDatabase.LoadAssetAtPath<DragonVisualCatalog>("Assets/Dravio/Character/Data/DragonVisualCatalog.asset");
                    var living=visualRoot.AddComponent<LivingSanctuaryPresentation>();
                    living.Configure(persistence,locationData,manager,controller,session,fruitVisual,
                        selectedTitle,status,progress,notice,livingProfile,livingTravel,
                        feed,pet,sleep,inspect,resume,rosterInspect,nextDragon,observeDragon,nextTravel,moveDragon,storeDragon);
                    session.ConfigureLiving(living);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(saveButton.onClick,session.SaveGame);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(bioRecord.onClick,session.ViewBiology);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(surveyRocks.onClick,session.SurveyRocks);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(unlockRocks.onClick,session.UnlockRocks);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(buy.onClick,session.BuyStarter);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(claim.onClick,session.ClaimMeadow);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(store.onClick,session.ClaimStorage);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(surveyPond.onClick,session.SurveyPond);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(unlockPond.onClick,session.UnlockPond);
                    session.ConfigureHatchery(hatcheryStatus,parentsStatus,hatcheryFeedback);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(standard.onClick,session.BuyStandardStarter);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(elemental.onClick,session.BuyElemental);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(mystic.onClick,session.BuyMystic);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(astral.onClick,session.BuyAstral);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(packMeadow.onClick,session.ClaimMeadow);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(packStorage.onClick,session.ClaimStorage);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(selectA.onClick,session.SelectParentA);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(selectB.onClick,session.SelectParentB);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(breed.onClick,session.StartBreeding);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(tap.onClick,session.TapBreedingEgg);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(breedMeadow.onClick,session.ClaimBreedMeadow);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(breedStorage.onClick,session.ClaimBreedStorage);
                    session.ConfigureManagement(managementSummary,managementWorld,managementCodex,managementMaterials,
                        managementExpedition,managementFeedback,unlockOcean,unlockSky,claimExpedition,
                        upgradeStatus.ToArray(),upgradeButtons.ToArray());
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(unlockOcean.onClick,session.UnlockOcean);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(unlockSky.onClick,session.UnlockSky);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(claimExpedition.onClick,session.ClaimExpeditionReward);
                    for(int i=0;i<upgradeButtons.Count;i++)
                        UnityEditor.Events.UnityEventTools.AddStringPersistentListener(upgradeButtons[i].onClick,session.BuySanctuaryUpgrade,EconomyTables.Upgrades[i].Id);
                    session.ConfigureHealth(healthStatus,careCaseStatus,carePlanStatus,healthNext,healthShelter,healthAssess,
                        healthNextPlan,healthApplyPlan,healthStart,healthReview,healthDischarge);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthNext.onClick,session.SelectNextHealthDragon);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthShelter.onClick,session.ShelterHealthDragon);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthAssess.onClick,session.AssessHealthCase);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthNextPlan.onClick,session.SelectNextHealthPlan);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthApplyPlan.onClick,session.ApplyHealthPlan);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthStart.onClick,session.StartHealthCare);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthReview.onClick,session.ReviewHealthCase);
                    UnityEditor.Events.UnityEventTools.AddPersistentListener(healthDischarge.onClick,session.DischargeHealthCase);
                    foreach(var button in new[]{healthNext,healthShelter,healthAssess,healthNextPlan,healthApplyPlan,healthStart,healthReview,healthDischarge})
                        button.interactable=false;
                    unlockOcean.interactable=unlockSky.interactable=claimExpedition.interactable=false;
                    foreach(var button in upgradeButtons)button.interactable=false;
                }
            }
            var runtime=canvasGo.AddComponent<FantasyPreviewUI>();
            // persistent callbacks survive editor scene serialization.
            UnityEditor.Events.UnityEventTools.AddPersistentListener(rosterInspect.onClick,controller.Inspect);
            var pages=new List<GameObject>{care.parent.parent.gameObject,info.parent.parent.gameObject,roster.parent.parent.gameObject};
            if(playable)
            {
                pages.Add(guide.parent.parent.gameObject);
                pages.Add(hatchery.parent.parent.gameObject);
                pages.Add(management.parent.parent.gameObject);
            }
            runtime.Configure(safe,panel,pages.ToArray(),
                tabs.ToArray(),lang,hide,hide.GetComponentInChildren<Text>(),ui.copy.ToArray(),controller);
        }

        static GameObject CreateFruit()
        {
            var fruit=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fruit.name="Session fruit marker";
            Object.DestroyImmediate(fruit.GetComponent<Collider>());
            fruit.transform.localScale=new Vector3(.25f,.24f,.25f);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("URP Lit missing for fruit.");
            var mat=new Material(shader){name="Preview fruit"};
            mat.SetColor("_BaseColor",new Color(.86f,.19f,.08f));
            mat.SetFloat("_Smoothness",.2f);
            const string folder="Assets/Dravio/Art/Imported/Materials";
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(folder+"/SessionFruit.mat"));
            fruit.GetComponent<Renderer>().sharedMaterial=mat;
            fruit.SetActive(false);
            return fruit;
        }

        static RectTransform ActionRow(Transform parent)
        {
            var row=RectNode("Touch actions",parent);
            var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;
            layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight=56;
            return row;
        }

        static void EnsureEventSystem()
        {
            var e=Object.FindFirstObjectByType<EventSystem>();
            if(e==null)e=new GameObject("Fantasy EventSystem",typeof(EventSystem)).GetComponent<EventSystem>();
            foreach(var m in e.GetComponents<BaseInputModule>())
                if(!(m is InputSystemUIInputModule))m.enabled=false;
            var input=e.GetComponent<InputSystemUIInputModule>();
            if(input==null)input=e.gameObject.AddComponent<InputSystemUIInputModule>();
            input.enabled=true;
            if(input.actionsAsset==null)input.AssignDefaultActions();
        }

        static RectTransform RectNode(string name,Transform parent)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            return go.GetComponent<RectTransform>();
        }
        static RectTransform Surface(string name,Transform parent,Color c)
        {
            var r=RectNode(name,parent);r.gameObject.AddComponent<Image>().color=c;return r;
        }
        static void Stretch(RectTransform r,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
        {r.anchorMin=min;r.anchorMax=max;r.offsetMin=offsetMin;r.offsetMax=offsetMax;}

        static RectTransform Page(Transform host,string name)
        {
            var page=RectNode(name,host);
            Stretch(page,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            var scroll=page.gameObject.AddComponent<ScrollRect>();
            var viewport=Surface("Viewport",page,new Color(.13f,.10f,.07f,.20f));
            Stretch(viewport,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            var body=RectNode("Content",viewport);
            body.anchorMin=new Vector2(0,1);body.anchorMax=new Vector2(1,1);
            body.pivot=new Vector2(.5f,1);body.anchoredPosition=Vector2.zero;body.sizeDelta=Vector2.zero;
            var v=body.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding=new RectOffset(7,7,5,9);v.spacing=6;v.childControlWidth=true;v.childControlHeight=true;
            v.childForceExpandHeight=false;v.childForceExpandWidth=true;
            var fit=body.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport=viewport;scroll.content=body;scroll.horizontal=false;
            scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=24;
            return body;
        }
        static Text Label(Layout ui,Transform parent,string en,string vi,int size,Color color,TextAnchor align,float height,bool localize=true)
        {
            var r=RectNode("Text",parent);var text=r.gameObject.AddComponent<Text>();
            text.font=ui.font;text.text=vi;text.fontSize=size;text.color=color;text.alignment=align;
            text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Truncate;
            text.resizeTextForBestFit=true;text.resizeTextMinSize=12;text.resizeTextMaxSize=size;
            r.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
            if(localize)ui.copy.Add(new FantasyPreviewUI.Copy{label=text,en=en,vi=vi});
            return text;
        }
        static Button Button(Layout ui,Transform parent,string en,string vi,float height,float width=-1)
        {
            var r=Surface(en,parent,Color.white);var b=r.gameObject.AddComponent<Button>();b.targetGraphic=r.GetComponent<Image>();
            var colors=b.colors;colors.normalColor=Gold;colors.highlightedColor=new Color(1,.90f,.65f);
            colors.pressedColor=new Color(.66f,.54f,.34f);colors.selectedColor=colors.highlightedColor;b.colors=colors;
            var nav=b.navigation;nav.mode=Navigation.Mode.None;b.navigation=nav;
            var size=r.gameObject.AddComponent<LayoutElement>();size.preferredHeight=height;size.preferredWidth=width;
            if(width<0)size.flexibleWidth=1;
            var label=Label(ui,r,en,vi,16,Ink,TextAnchor.MiddleCenter,height);
            Stretch(label.rectTransform,Vector2.zero,Vector2.one,new Vector2(5,2),new Vector2(-5,-2));
            return b;
        }
        static void Fact(Layout ui,Transform parent,string en,string vi,string valueEn,string valueVi)
            =>Label(ui,parent,en+"  •  "+valueEn,vi+"  •  "+valueVi,15,Paper,TextAnchor.MiddleLeft,48);
        static string Join(string[] values,bool vi)=>string.Join(", ",values.Select(v=>vi?Translate(v):v));
        static string Translate(string value)=>value switch {
            "lung"=>"phổi","walk"=>"đi bộ","scale"=>"vảy","day"=>"ban ngày",
            "fruit"=>"trái cây","aether"=>"Aether","mild"=>"ôn hòa","warm"=>"ấm","hot"=>"nóng",
            "origin_meadow"=>"Đồng cỏ Khởi Nguyên","origin_warm_rocks"=>"Bãi đá ấm Khởi Nguyên",
            _=>value
        };
    }
}
