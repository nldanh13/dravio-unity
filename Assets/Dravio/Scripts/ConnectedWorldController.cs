using System;
using System.Collections;
using System.Linq;
using Dravio.EnvironmentArt;
using Dravio.Platform;
using Dravio.Platform.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Dravio
{
    // One persistent *loaded* shell scene, one host. Additive environment scenes contain no gameplay authority.
    [DisallowMultipleComponent]
    public sealed class ConnectedWorldController : MonoBehaviour
    {
        [SerializeField] DravioSessionHost host;
        [SerializeField] LivingSanctuaryPresentation presentation;
        [SerializeField] SanctuaryDomainPreview adapter;
        [SerializeField] GameObject[] originEnvironmentRoots;
        [SerializeField] Text viewStatus;
        [SerializeField] Button originButton,oceanButton,skyButton;
        Scene shell,remote;
        bool[] rootStates;
        bool switching,configured;
        string current=ConnectedWorldPolicy.Origin,feedback="";
        float hour,nextRefresh;
        AudioListener ownedListener;
        Camera listenerCamera;
        public void Configure(DravioSessionHost h,LivingSanctuaryPresentation p,SanctuaryDomainPreview a,
            GameObject[] roots,Text status,Button origin,Button ocean,Button sky)
        {host=h;presentation=p;adapter=a;originEnvironmentRoots=roots;viewStatus=status;originButton=origin;oceanButton=ocean;skyButton=sky;}
        // Keep a single fallback listener in the persistent shell, not a camera root
        // that is disabled during additive world transitions. No scene regeneration needed.
        void Awake()
        {
            if(gameObject.scene.path==ConnectedWorldPolicy.OriginScene)
                RefreshAudioListener();
        }
        void RefreshAudioListener()
        {
            var other=UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)
                .Any(x=>x!=ownedListener && x.isActiveAndEnabled);
            if(other)
            {
                if(ownedListener!=null)ownedListener.enabled=false;
                listenerCamera=null;
                return;
            }
            if(ownedListener==null)
            {
                var root=new GameObject("Dravio Runtime Audio Listener");
                root.transform.SetParent(transform,false);
                ownedListener=root.AddComponent<AudioListener>();
            }
            listenerCamera=Camera.main;
            if(listenerCamera!=null)
                ownedListener.transform.SetPositionAndRotation(listenerCamera.transform.position,listenerCamera.transform.rotation);
            ownedListener.enabled=true;
        }
        void LateUpdate()
        {
            if(ownedListener==null||!ownedListener.enabled)return;
            if(listenerCamera==null||!listenerCamera.isActiveAndEnabled)listenerCamera=Camera.main;
            if(listenerCamera!=null)
                ownedListener.transform.SetPositionAndRotation(listenerCamera.transform.position,listenerCamera.transform.rotation);
        }
        void Start()
        {
            shell=gameObject.scene;
            configured=host!=null && presentation!=null && adapter!=null && viewStatus!=null &&
                originButton!=null && oceanButton!=null && skyButton!=null && originEnvironmentRoots!=null &&
                originEnvironmentRoots.Length>0 && shell.path==ConnectedWorldPolicy.OriginScene &&
                SceneManager.sceneCount==1 && host.gameObject.scene==shell &&
                originEnvironmentRoots.All(x=>x!=null && x.scene==shell && x.transform.parent==null &&
                    x.GetComponentInChildren<DravioSessionHost>(true)==null &&
                    x.GetComponentInChildren<LivingSanctuaryPresentation>(true)==null &&
                    x.GetComponentInChildren<SanctuaryDomainPreview>(true)==null &&
                    x.GetComponentInChildren<ConnectedWorldController>(true)==null &&
                    x.GetComponentInChildren<EventSystem>(true)==null);
            if(configured)
            {
                var env=LivingEnvironment.Active;var camera=Camera.main;
                configured=env!=null && env.locations!=null && env.locations.zoneId==ConnectedWorldPolicy.Origin &&
                    camera!=null && camera.orthographic && originEnvironmentRoots.Contains(env.transform.root.gameObject) &&
                    originEnvironmentRoots.Contains(camera.transform.root.gameObject) &&
                    UnityEngine.Object.FindObjectsByType<DravioSessionHost>(FindObjectsInactive.Include).Length==1;
            }
            if(!configured){feedback="World shell configuration invalid / Cấu hình thế giới chưa hợp lệ";Refresh();return;}
            RefreshAudioListener();
            rootStates=new bool[originEnvironmentRoots.Length];
            originButton.onClick.AddListener(()=>Visit(ConnectedWorldPolicy.Origin));
            oceanButton.onClick.AddListener(()=>Visit(ConnectedWorldPolicy.Ocean));
            skyButton.onClick.AddListener(()=>Visit(ConnectedWorldPolicy.Sky));
            Refresh();
        }
        bool Ready=>configured && presentation.WorldViewAvailable && host.Session!=null &&
            host.Session.Status==SessionStatus.Ready && !host.Session.Hidden;
        static bool Installed(string zone)=>zone==ConnectedWorldPolicy.Origin ||
            SceneUtility.GetBuildIndexByScenePath(ConnectedWorldPolicy.ScenePath(zone))>=0;
        bool CanEnter(string zone)=>Ready && ConnectedWorldPolicy.CanVisit(host.Session.State,zone) && Installed(zone);
        void Update(){if(Time.unscaledTime>=nextRefresh){nextRefresh=Time.unscaledTime+.25f;Refresh();}}
        void Refresh()
        {
            if(viewStatus==null)return;
            if(originButton!=null)originButton.interactable=configured && !switching && current!=ConnectedWorldPolicy.Origin;
            if(oceanButton!=null)oceanButton.interactable=!switching && current!=ConnectedWorldPolicy.Ocean && CanEnter(ConnectedWorldPolicy.Ocean);
            if(skyButton!=null)skyButton.interactable=!switching && current!=ConnectedWorldPolicy.Sky && CanEnter(ConnectedWorldPolicy.Sky);
            string info=(switching?"Loading / Đang tải":current)+"\n"+
                "Ocean / Thủy Vực: "+ZoneStatus(ConnectedWorldPolicy.Ocean)+"\n"+
                "Sky / Thiên Giới: "+ZoneStatus(ConnectedWorldPolicy.Sky);
            viewStatus.text=info+(string.IsNullOrEmpty(feedback)?"":"\n"+feedback);
        }
        string ZoneStatus(string zone)=>!ConnectedWorldPolicy.CanVisit(host?.Session?.State,zone)?"Locked / Khóa":
            !Installed(zone)?"Scene missing / Thiếu scene":"Available / Đã mở";
        public void Visit(string zone)
        {
            if(!configured||switching||zone==current||ConnectedWorldPolicy.ScenePath(zone)==null)return;
            if(zone!=ConnectedWorldPolicy.Origin && !CanEnter(zone))return;
            StartCoroutine(Change(zone));
        }
        IEnumerator Change(string zone)
        {
            switching=true;feedback="";Refresh();
            adapter.SetWorldSwitching(true);
            presentation.SuspendWorldView(); // Clear visual claims, not canonical food or residents.
            var living=LivingEnvironment.Active;hour=living!=null?living.Hour:13f;
            if(current==ConnectedWorldPolicy.Origin)
            {
                for(int i=0;i<originEnvironmentRoots.Length;i++)
                {rootStates[i]=originEnvironmentRoots[i].activeSelf;originEnvironmentRoots[i].SetActive(false);}
            }
            if(remote.IsValid() && remote.isLoaded)
            {
                SceneManager.SetActiveScene(shell);
                foreach(var root in remote.GetRootGameObjects())root.SetActive(false);
                RefreshAudioListener();
                var unload=SceneManager.UnloadSceneAsync(remote);
                if(unload!=null)yield return unload;
                if(remote.isLoaded)
                {
                    // Keep malformed/stuck scene disabled, fail closed for further loads.
                    feedback="Unload failed / Không thể dỡ scene";RestoreOrigin();
                    configured=false;yield break;
                }
                remote=default;
            }
            RefreshAudioListener();
            if(zone==ConnectedWorldPolicy.Origin){RestoreOrigin();yield break;}
            // Recheck entitlement after unload; no scene load if canonical state changed.
            if(!CanEnter(zone)){feedback="World unavailable / Thế giới chưa sẵn sàng";RestoreOrigin();yield break;}
            AsyncOperation load=null;
            try{load=SceneManager.LoadSceneAsync(SceneUtility.GetBuildIndexByScenePath(ConnectedWorldPolicy.ScenePath(zone)),LoadSceneMode.Additive);}
            catch(Exception e){Debug.LogException(e);}
            if(load==null){feedback="Load failed / Không tải được scene";RestoreOrigin();yield break;}
            yield return load;
            remote=SceneManager.GetSceneByPath(ConnectedWorldPolicy.ScenePath(zone));
            RefreshAudioListener();
            bool bound=false;
            try{bound=CanEnter(zone)&&TryBind(remote,zone);}
            catch(Exception e){Debug.LogException(e);}
            if(!bound)
            {
                feedback="World validation failed / Scene không hợp lệ";
                if(remote.IsValid() && remote.isLoaded)
                {
                    SceneManager.SetActiveScene(shell);
                    foreach(var root in remote.GetRootGameObjects())root.SetActive(false);
                    var unload=SceneManager.UnloadSceneAsync(remote);if(unload!=null)yield return unload;
                    if(remote.isLoaded){RestoreOrigin();configured=false;yield break;}
                }
                remote=default;RestoreOrigin();yield break;
            }
            current=zone;Finish();
        }
        bool TryBind(Scene scene,string zone)
        {
            if(!scene.IsValid()||!scene.isLoaded)return false;
            var roots=scene.GetRootGameObjects();
            if(roots.Any(x=>x.GetComponentInChildren<DravioSessionHost>(true)!=null ||
                x.GetComponentInChildren<EventSystem>(true)!=null ||
                x.GetComponentInChildren<LivingSanctuaryPresentation>(true)!=null ||
                x.GetComponentInChildren<SanctuaryDomainPreview>(true)!=null ||
                x.GetComponentInChildren<Canvas>(true)!=null))return false;
            var environments=roots.SelectMany(x=>x.GetComponentsInChildren<LivingEnvironment>()).Where(x=>x.isActiveAndEnabled).ToArray();
            var cameras=roots.SelectMany(x=>x.GetComponentsInChildren<Camera>()).Where(x=>x.isActiveAndEnabled).ToArray();
            if(environments.Length!=1||cameras.Length!=1||environments[0].locations==null ||
                environments[0].locations.zoneId!=zone||!cameras[0].orthographic)return false;
            if(LivingEnvironment.Active!=environments[0] || !SceneManager.SetActiveScene(scene))return false;
            var env=environments[0];
            if(env.timeOfDay!=null)env.timeOfDay.SetHour(hour);
            var fit=cameras[0].GetComponent<SanctuaryCameraFit>();if(fit!=null)fit.enabled=true;
            return presentation.BindWorldView(env.locations,env.interactions,cameras[0]);
        }
        void RestoreOrigin()
        {
            try
            {
                SceneManager.SetActiveScene(shell);
                for(int i=0;i<originEnvironmentRoots.Length;i++)if(originEnvironmentRoots[i]!=null)originEnvironmentRoots[i].SetActive(rootStates[i]);
                var env=LivingEnvironment.Active;
                if(env!=null && env.timeOfDay!=null)env.timeOfDay.SetHour(hour);
                current=ConnectedWorldPolicy.Origin;
                if(env==null||!presentation.BindWorldView(env.locations,env.interactions,Camera.main))
                {feedback="Origin binding failed / Không thể khôi phục Khởi Nguyên";configured=false;}
            }
            catch(Exception e){Debug.LogException(e);configured=false;feedback="Origin restore failed / Lỗi khôi phục";}
            finally{Finish();}
        }
        void Finish(){RefreshAudioListener();switching=false;adapter.SetWorldSwitching(false);Refresh();}
    }
}
