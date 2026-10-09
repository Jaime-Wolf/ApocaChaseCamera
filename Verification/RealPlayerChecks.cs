#if REAL_APOCAPLAYER
using System;
using System.Reflection;
using UnityEngine;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using Player = Apocaplayer.ThirdPerson;
using PlayerPlugin = Apocaplayer.Plugin;
using PlayerGame = Apocaplayer.Game;
namespace ApocaChaseCamera
{
    internal static class RealPlayerChecks
    {
        private static int checks;
        private static GameObject car,player;
        private static Camera eye,native;
        private static PlayMakerFSM inCar,look;
        private static MouseLook lookX,lookY;
        private static Transform arrow;
        private static MethodInfo compute=typeof(Player).GetMethod("ComputeView",BindingFlags.NonPublic|BindingFlags.Static);
        private static T Field<T>(string name){return (T)typeof(Player).GetField(name,BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);}
        private static void Check(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
        private static void Near(float a,float b,string why){Check(Math.Abs(a-b)<.005f,why+": "+a+" vs "+b);}
        private static GameObject Child(string name,GameObject parent){var go=new GameObject(name);go.transform.SetParent(parent.transform,false);return go;}
        private static void Setup()
        {
            Player.Off();ApocaplayerBridge.Shutdown();ChaseView.SaveZoom(true);ChaseView.Reset();CameraBinding.Reset();
            foreach(var old in new System.Collections.Generic.List<GameObject>(GameObject.Registry.Values))UnityEngine.Object.Destroy(old);
            GameObject.Registry.Clear();
            car=new GameObject("Test car");var drive=Child("DriveTrigger",car);var third=Child("3rdCamera",drive);
            native=Child("Camera",third).AddComponent<Camera>();native.enabled=false;
            player=Child("Player",Child("Seat",car));
            inCar=player.Add(new PlayMakerFSM{FsmName="InCar",ActiveStateName="InCar"});
            player.Add(new PlayMakerFSM{FsmName="Health",ActiveStateName="Idle"});
            var holder=new GameObject("PlayerCameraHolder");eye=Child("PlayerCamera",holder).AddComponent<Camera>();eye.ResetProjectionMatrix();
            eye.gameObject.Add(new PlayMakerFSM{FsmName="AimDownSIghts_Hold",ActiveStateName="idle"});
            Child("Binocular Anim",Child("ItemAnim",eye.gameObject)).Add(new PlayMakerFSM{FsmName="Animation",ActiveStateName="off"});
            var canvas=new GameObject("Canvas",typeof(RectTransform));canvas.AddComponent<Canvas>();
            var point=new GameObject("MousePoint",typeof(RectTransform));point.transform.SetParent(canvas.transform,false);
            var crosshair=new GameObject("MouseCrosshair",typeof(RectTransform));crosshair.transform.SetParent(canvas.transform,false);
            arrow=Child("Compass Arrow",Child("Compass",canvas)).transform;GameObject.Registry["Canvas/Compass/Compass Arrow"]=arrow.gameObject;
            var yaw=new FsmFloat{Name="y",Value=123};
            player.Add(new PlayMakerFSM{FsmName="Compass UI",Fsm=new Fsm{CompassState=new FsmState{Actions=new FsmStateAction[]{
                new GetRotation{yAngle=yaw,space=Space.World},new FloatMultiply{floatVariable=yaw},
                new SetRotation{zAngle=yaw,space=Space.World,gameObject=new FsmOwnerDefault{Target=arrow.gameObject}}}}}});
            PlayerGame.Player=player;PlayerGame.Cam=eye;PlayerGame.PlayerCamera=eye.transform;PlayerGame.CameraHolder=holder.transform;PlayerGame.CarRoot=car.transform;
            PlayerGame.InCarFlag=true;PlayerGame.DeadFlag=PlayerGame.Binoculars=PlayerGame.AimDownSights=PlayerGame.CaveFlag=false;PlayerGame.Weapon="";
            PlayerPlugin.Enabled.Value=true;PlayerPlugin.DynamicCrosshair.Value=true;PlayerPlugin.OcclusionPrototype.Value=PlayerPlugin.OcclusionInVehicle.Value=false;
            PlayerPlugin.ThirdCarDistance.Value=6;PlayerPlugin.ThirdCarHeight.Value=.5f;
            Plugin.Enabled.Value=true;Plugin.Distance.Value=6.5f;Plugin.Height.Value=1.7f;Plugin.Pitch.Value=8;
            Plugin.Log.ThrowWarnings=false;Time.frameCount++;Time.unscaledTime+=1;
            Application.isFocused=true;Time.timeScale=1;Apocasetter.GameMenu.InGame=true;Apocasetter.GameMenu.Paused=false;Apocasetter.InputBlocker.Active=false;
            Physics.Hits=new RaycastHit[0];Physics.Overlaps=new Collider[0];Physics.GroundProbe=null;Terrain.activeTerrains=new Terrain[0];
            Input.X=Input.Y=0;Input.mouseScrollDelta=Vector2.zero;Input.ObserveHeld=Input.ObserveDown=Input.ChangeCamera=false;
            Apocaplayer.OcclusionCutaway.ShaderReady=true;Apocaplayer.OcclusionCutaway.Calls=Apocaplayer.OcclusionCutaway.Creates=0;Apocaplayer.OcclusionCutaway.Active=false;
            Player.On=true;ApocaplayerBridge.Discover();Check(ApocaplayerBridge.CruisingView,"bridge binds to the actual unchanged ThirdPerson class");
        }
        private static void Frame(bool chaseFirst=true)
        {
            Time.frameCount++;Time.unscaledTime+=1f/60f;
            if(chaseFirst){ChaseView.Tick();Player.Tick();}else{Player.Tick();ChaseView.Tick();}
            Player.LateCrosshair();CompassView.Sync();Camera.Render(eye);ChaseView.PostRender(eye);Player.EndOfFrame();
        }
        private static Vector2 Project(Vector3 point,Vector3 position,Quaternion rotation,float fov)
        {
            // Independent pinhole projection: rotate the point into the eye's local coordinates.
            Vector3 p=Quaternion.Inverse(rotation)*(point-position);
            float tan=(float)Math.Tan(fov*Math.PI/360);
            return new Vector2((p.x/(p.z*tan*eye.aspect)*.5f+.5f)*Screen.width,(p.y/(p.z*tan)*.5f+.5f)*Screen.height);
        }
        private static void CursorAndOwnership()
        {
            Setup();Frame();Input.X=20;Frame();Input.X=0;
            Vector3 p;Quaternion r;Check(ChaseView.TryPose(eye,out p,out r),"cruising pose available");
            Near(Vector3.Distance(Field<Vector3>("_vPos"),p),0,"actual private cursor cache uses ChaseCam position");
            Check(Player.HasCrosshair,"actual LateCrosshair produced a cursor");
            var expected=Project(eye.transform.position+eye.transform.forward*1000,p,r,eye.fieldOfView);
            Near(Player.CrosshairScreen.x,expected.x,"actual cursor horizontal projection follows visible view");
            Near(Player.CrosshairScreen.y,expected.y,"actual cursor vertical projection follows visible view");
            Near(GameObject.Find("MousePoint").transform.localPosition.x,expected.x-Screen.width*.5f,"actual cursor UI moves horizontally with the rendered view");
            Near(GameObject.Find("MousePoint").transform.localPosition.y,expected.y-Screen.height*.5f,"actual cursor UI moves vertically with the rendered view");
            Near(Vector3.Distance(eye.transform.position,Vector3.zero),0,"no camera transform movement");
            float before=Field<float>("_orbitYaw");Input.ObserveHeld=true;Input.X=4;Frame();
            Near(Field<float>("_orbitYaw"),0,"hidden observation yaw does not accumulate");
            Check(!Player.Orbiting&&Player.BeforeMouseLook(),"hidden orbit cannot suppress native mouse look");
            Input.X=0;Input.ObserveHeld=false;PlayerGame.Weapon="Rifle";Frame();
            Check(!ApocaplayerBridge.Driving(eye),"drawing a weapon yields ownership");
            float actualYaw,actualPitch;ApocaplayerBridge.Angles(Field<Quaternion>("_vRot"),out actualYaw,out actualPitch);
            Near(arrow.rotation.euler.z,-actualYaw,"armed compass follows actual current-frame rendered heading");
            PlayerGame.Weapon="";Frame(false);Check(ApocaplayerBridge.Driving(eye),"stowing returns to ChaseCam with reverse Update order");
            PlayerGame.Binoculars=true;Frame();Check(Player.Peek&&!ApocaplayerBridge.Driving(eye),"binoculars retain native control");
            PlayerGame.Binoculars=false;PlayerGame.Weapon="Rifle";PlayerGame.AimDownSights=true;Frame();
            Check(Player.AimZoom&&!ApocaplayerBridge.Driving(eye),"aiming retains native control");
            foreach(var fsm in eye.gameObject.GetComponents<PlayMakerFSM>())
                if(fsm.FsmName=="AimDownSIghts_Hold")fsm.ActiveStateName="ads";
            Input.ObserveHeld=true;Input.X=4;Frame();Input.X=0;
            ApocaplayerBridge.Angles(Field<Quaternion>("_vRot"),out actualYaw,out actualPitch);
            Near(arrow.rotation.euler.z,-actualYaw,"non-scoped ADS compass follows the still-active third-person view");
            PlayerGame.Weapon="Rifle scope";Frame();
            Check(Player.Peek&&!ApocaplayerBridge.ThirdPersonView,"scoped ADS retains the first-person special view");
            Near(arrow.rotation.euler.z,0,"scoped ADS releases the driving compass override");
            Input.ObserveHeld=false;
        }
        private static void BindLook(bool steering)
        {
            look=player.AddComponent<PlayMakerFSM>();look.FsmName="Look";look.ActiveStateName=steering?"MouseSteering":"Look";
            lookX=new MouseLook{axes=MouseLook.RotationAxes.MouseX,gameObject=new FsmOwnerDefault{Target=eye.gameObject}};
            lookY=new MouseLook{axes=MouseLook.RotationAxes.MouseY,gameObject=new FsmOwnerDefault{Target=eye.gameObject}};
            var direction=Child("Direction",player);
            look.Fsm.States[look.ActiveStateName]=new FsmState{Actions=new FsmStateAction[]{new MouseLook{gameObject=new FsmOwnerDefault{Target=direction}},lookX,lookY}};
        }
        private static void PassiveRecenterCursor()
        {
            foreach(float sign in new[]{-1f,1f})
            {
                Setup();BindLook(sign<0);Plugin.Recenter.Value=true;Plugin.RecenterDelay.Value=.1f;Frame();
                Input.X=sign*25;Input.Y=sign*7;lookX.OnUpdate();lookY.OnUpdate();Frame();Input.X=Input.Y=0;
                Quaternion lastManual=eye.transform.rotation;
                for(int i=0;i<180;i++)
                {
                    lookX.OnUpdate();lookY.OnUpdate();Frame(i%2==0);
                    Vector3 p;Quaternion r;Check(ChaseView.TryPose(eye,out p,out r),"recenter retains a valid cruising view");
                    if(i<3)Near(Vector3.Distance(eye.transform.forward,lastManual*Vector3.forward),0,"native aim waits for configured recenter delay");
                    if(i>10)Near(Vector3.Distance(eye.transform.forward,r*Vector3.forward),0,"passive recenter brings actual native aim along with view in either direction");
                    var expected=Project(eye.transform.position+eye.transform.forward*1000,p,r,eye.fieldOfView);
                    Near(Player.CrosshairScreen.x,expected.x,"recenter cursor uses updated aim and rendered horizontal view");
                    Near(Player.CrosshairScreen.y,expected.y,"recenter cursor uses updated aim and rendered vertical view");
                    Near(GameObject.Find("MousePoint").transform.localPosition.x,expected.x-Screen.width*.5f,"recenter UI follows actual interaction projection");
                }
                Check(Math.Abs(Player.CrosshairScreen.x-Screen.width*.5f)<5,"recenter returns cursor near center with natural eye parallax");
                Near(GameObject.Find("Direction").transform.rotation.eulerAngles.y,0,"direction/steering target stays untouched");
                Quaternion before=eye.transform.rotation;Input.X=.2f;lookX.OnUpdate();lookY.OnUpdate();Frame();Input.X=0;
                Check(Vector3.Distance(eye.transform.forward,before*Vector3.forward)<.02f,"manual look resumes without snapping to stale native yaw");
                Plugin.Recenter.Value=false;before=eye.transform.rotation;for(int i=0;i<60;i++)Frame();
                Near(Vector3.Distance(eye.transform.forward,before*Vector3.forward),0,"disabled recenter leaves actual aim alone");
            }
            // The eye is parented directly to a tilted vehicle seat in the real game.
            Setup();BindLook(false);Plugin.Recenter.Value=true;Plugin.RecenterDelay.Value=.1f;Frame();
            car.transform.rotation=Quaternion.Euler(12,70,9);eye.transform.SetParent(player.transform.parent,true);
            for(int i=0;i<120;i++){lookX.OnUpdate();lookY.OnUpdate();Frame();}
            var local=eye.transform.localEulerAngles;
            Near(CameraMath.Wrap(lookX.CachedYaw-local.y),0,"cached yaw uses seat-local coordinates");
            Near(CameraMath.Wrap(lookY.CachedPitch-local.x),0,"vertical input accumulator stores actual local pitch");
            lookX.OnUpdate();lookY.OnUpdate();
            // Native look resets local roll; yaw/pitch must still be preserved.
            var expectedLocal=Quaternion.Euler(local.x,local.y,0);
            Near(Vector3.Distance(eye.transform.forward,(eye.transform.parent.rotation*expectedLocal)*Vector3.forward),0,"native input retains synchronized local yaw/pitch");
            car.transform.rotation=Quaternion.Euler(5,110,-3);Frame();Vector3 turnPosition;Quaternion turnRotation;
            Check(ChaseView.TryPose(eye,out turnPosition,out turnRotation),"turning vehicle retains view");
            Near(Vector3.Distance(eye.transform.forward,turnRotation*Vector3.forward),0,"settled aim continues following vehicle heading smoothing");
            Plugin.RecenterDelay.Value=1.5f;
        }
        private static void RecenterOwnershipGuards()
        {
            Setup();BindLook(false);Plugin.Recenter.Value=true;Plugin.RecenterDelay.Value=0;
            var both=new MouseLook{axes=MouseLook.RotationAxes.MouseXAndY,gameObject=new FsmOwnerDefault{Target=eye.gameObject}};
            look.Fsm.States["Look"].Actions=new FsmStateAction[]{both};
            for(int i=0;i<30;i++)Frame();
            Near(CameraMath.Wrap(both.CachedYaw-eye.transform.localEulerAngles.y),0,"combined-axis action yaw synchronized");
            Near(CameraMath.Wrap(both.CachedPitch-eye.transform.localEulerAngles.x),0,"combined-axis action pitch uses its own sign convention");
            foreach(string mode in new[]{"first person","armed","paused","focus lost","modal","on foot"})
            {
                Setup();BindLook(false);Plugin.Recenter.Value=true;Plugin.RecenterDelay.Value=0;Frame();
                eye.transform.rotation=Quaternion.Euler(16,73,0);Quaternion before=eye.transform.rotation;
                if(mode=="first person")Player.On=false;
                if(mode=="armed")PlayerGame.Weapon="Rifle";
                if(mode=="paused")Apocasetter.GameMenu.Paused=true;
                if(mode=="focus lost")Application.isFocused=false;
                if(mode=="modal")Apocasetter.InputBlocker.Active=true;
                if(mode=="on foot"){inCar.ActiveStateName="OutCar";PlayerGame.InCarFlag=false;}
                if(mode=="disabled look")look.enabled=false;
                if(mode=="stopped look")look.ActiveStateName="LookStop";
                for(int i=0;i<15;i++)Frame();
                Near(Vector3.Distance(eye.transform.forward,before*Vector3.forward),0,"aim is untouched while "+mode);
            }
            Plugin.RecenterDelay.Value=1.5f;
        }
        private static void DormantAndLateNativeLook()
        {
            foreach(string mode in new[]{"disabled controller","empty active state","missing controller"})
            {
                Setup();Plugin.Recenter.Value=true;Plugin.RecenterDelay.Value=0;
                if(mode!="missing controller")BindLook(false);
                if(mode=="disabled controller")look.enabled=false;
                if(mode=="empty active state")
                {
                    look.ActiveStateName="LookStop";
                    look.Fsm.States["LookStop"]=new FsmState{Actions=new FsmStateAction[0]};
                }
                eye.transform.rotation=Quaternion.Euler(15,70,0);Frame();
                Vector3 p;Quaternion r;
                Check(ChaseView.TryPose(eye,out p,out r),"view remains owned with "+mode);
                Near(Vector3.Distance(eye.transform.forward,r*Vector3.forward),0,"native aim follows recenter with "+mode);
                Check(Math.Abs(Player.CrosshairScreen.x-Screen.width*.5f)<5,"actual Apocaplayer cursor returns with "+mode);
                if(mode!="missing controller")
                {
                    look.enabled=true;look.ActiveStateName="Look";
                    var before=eye.transform.forward;lookX.OnUpdate();lookY.OnUpdate();
                    Near(Vector3.Distance(before,eye.transform.forward),0,"resuming a dormant controller keeps the synchronized aim");
                }
            }
            Setup();BindLook(false);Plugin.Recenter.Value=true;Plugin.RecenterDelay.Value=0;Frame();
            Time.frameCount++;Time.unscaledTime+=1f/60f;ChaseView.Tick();Player.Tick();
            Vector3 position;Quaternion rotation;
            Check(ChaseView.TryPose(eye,out position,out rotation),"early pose consumer runs before native update");
            // Unlike the old fixture, exercise a native write AFTER the pose is cached.
            eye.transform.rotation=Quaternion.Euler(20,75,0);lookX.Seed(75,0);lookY.Seed(0,20);
            Player.LateCrosshair();
            Near(Vector3.Distance(eye.transform.forward,rotation*Vector3.forward),0,"cursor preparation repairs a late native aim overwrite even with a cached pose");
            var expected=Project(eye.transform.position+eye.transform.forward*1000,position,rotation,eye.fieldOfView);
            Near(Player.CrosshairScreen.x,expected.x,"late native overwrite cannot leave stale horizontal cursor projection");
            Near(Player.CrosshairScreen.y,expected.y,"late native overwrite cannot leave stale vertical cursor projection");
            var corrected=eye.transform.forward;lookX.OnUpdate();lookY.OnUpdate();
            Near(Vector3.Distance(eye.transform.forward,corrected),0,"late overwrite repair synchronizes native input caches too");
            Player.EndOfFrame();Plugin.RecenterDelay.Value=1.5f;
        }
        private static void CutawayAndCollision()
        {
            Setup();PlayerPlugin.OcclusionPrototype.Value=true;
            for(int i=0;i<6;i++)Frame(i%2==0);
            Check(Apocaplayer.OcclusionCutaway.Calls==0&&Apocaplayer.OcclusionCutaway.Creates==0,"disabled vehicle effect never prepares/recreates cutaway");
            PlayerPlugin.OcclusionInVehicle.Value=true;Frame();Check(Apocaplayer.OcclusionCutaway.Calls==1,"both toggles enable the cutaway");
            PlayerGame.CaveFlag=true;int calls=Apocaplayer.OcclusionCutaway.Calls;
            for(int i=0;i<6;i++)Frame(i%2==0);
            Check(Apocaplayer.OcclusionCutaway.Calls==calls,"cave frames do not recreate stopped effects");
            PlayerGame.CaveFlag=false;Frame();
            var building=new GameObject("Building");
            var childCollider=Child("Collider",building).AddComponent<Collider>();
            var childRenderer=Child("Mesh",building).AddComponent<Renderer>();
            int scans=GameObject.HierarchyArrayScans;
            Check(ApocaplayerBridge.CanFade(childCollider),"renderer on the collider owner is recognized");
            for(int i=0;i<120;i++){Time.unscaledTime+=1f/60f;ApocaplayerBridge.CanFade(childCollider);}
            Check(GameObject.HierarchyArrayScans==scans+1,"stable blocker binding performs one hierarchy scan across two seconds");
            childRenderer.enabled=false;
            Check(!ApocaplayerBridge.CanFade(childCollider),"cached blocker checks renderer visibility immediately");
            var wall=new GameObject("Renderable wall");var collider=wall.AddComponent<Collider>();wall.AddComponent<Renderer>();
            Physics.Hits=new[]{new RaycastHit{collider=collider,distance=2}};Frame();
            Vector3 p;Quaternion r;ChaseView.TryPose(eye,out p,out r);
            var pivot=car.transform.TransformPoint(CameraBinding.LocalAnchor)+Vector3.up*Plugin.Height.Value;
            Check(Vector3.Distance(p,pivot)>5,"fadeable wall preserves cruising distance");
            wall.GetComponent<Renderer>().forceRenderingOff=true;Frame();ChaseView.TryPose(eye,out p,out r);
            Check(Vector3.Distance(p,pivot)<2,"hidden/unfadeable renderer retains physical camera contraction");
            PlayerPlugin.OcclusionInVehicle.Value=false;Time.unscaledTime+=.2f;Frame();ChaseView.TryPose(eye,out p,out r);
            Check(Vector3.Distance(p,pivot)<2,"effect disabled restores collision mode");
            Physics.Overlaps=new[]{collider};Frame();Check(!ChaseView.TryPose(eye,out p,out r),"unresolved solid safely yields the view");
            Physics.Overlaps=new Collider[0];Physics.Hits=new RaycastHit[0];
            var terrain=new GameObject("Terrain").AddComponent<Terrain>();terrain.transform.position=new Vector3(-1000,0,-1000);terrain.terrainData.size=new Vector3(2000,1000,2000);terrain.Height=delegate{return 0f;};Terrain.activeTerrains=new[]{terrain};
            PlayerPlugin.OcclusionInVehicle.Value=true;Plugin.Pitch.Value=35;Plugin.Distance.Value=15;Frame();
            Check(!ApocaplayerBridge.CanFade(terrain.gameObject.AddComponent<Collider>()),"terrain is never a fadeable collider");
            Check(ChaseView.TryPose(eye,out p,out r)&&p.y>=Plugin.GroundClearance.Value+.22f,"enabled cutaway retains real camera ground clearance");
            Terrain.activeTerrains=new Terrain[0];Plugin.Pitch.Value=8;Plugin.Distance.Value=6.5f;
            var road=new GameObject("RoadMesh");var roadCollider=road.AddComponent<Collider>();road.AddComponent<Renderer>();
            Check(!ApocaplayerBridge.CanFade(roadCollider),"renderable mesh road stays solid even without Terrain component");
            Physics.Hits=new[]{new RaycastHit{collider=roadCollider,distance=2}};Frame();ChaseView.TryPose(eye,out p,out r);
            Check(Vector3.Distance(p,pivot)<2,"road sides contract the boom while cutaway is enabled");
            var floor=new GameObject("Unnamed ground");var floorCollider=floor.AddComponent<Collider>();floor.AddComponent<Renderer>();
            Physics.Hits=new[]{new RaycastHit{collider=floorCollider,distance=2,normal=Vector3.up}};Frame();ChaseView.TryPose(eye,out p,out r);
            Check(Vector3.Distance(p,pivot)<2,"upward ground hits stay solid regardless of object name");
            Physics.Hits=new RaycastHit[0];
            PlayerPlugin.OcclusionPrototype.Value=false;PlayerPlugin.OcclusionInVehicle.Value=true;calls=Apocaplayer.OcclusionCutaway.Calls;Frame();
            Check(Apocaplayer.OcclusionCutaway.Calls==calls,"main toggle off disables effect even when vehicle toggle remains on");
        }
        private static void ZoomAndGuards()
        {
            Setup();Input.mouseScrollDelta=new Vector2(0,1);Frame(false);Input.mouseScrollDelta=Vector2.zero;
            Near(PlayerPlugin.ThirdCarDistance.Value,6,"wheel leaves Apocaplayer driving setting untouched");
            Vector3 p;Quaternion r;ChaseView.TryPose(eye,out p,out r);
            Near(Vector3.Distance(p,car.transform.TransformPoint(CameraBinding.LocalAnchor)+Vector3.up*Plugin.Height.Value),5.72f,"wheel changes visible ChaseCam distance");
            Time.unscaledTime+=1.1f;Frame();Near(Plugin.Distance.Value,5.72f,"wheel persists ChaseCam distance after debounce");
            PlayerGame.Weapon="Rifle";Input.mouseScrollDelta=new Vector2(0,1);Frame();Input.mouseScrollDelta=Vector2.zero;
            Time.unscaledTime+=1.1f;Frame();Check(PlayerPlugin.ThirdCarDistance.Value<6,"armed view keeps Apocaplayer's original wheel behavior");
            PlayerGame.Weapon="";Frame();
            var replacement=new GameObject("Replacement owner camera").AddComponent<Camera>();
            PlayerGame.Cam=replacement;
            Check(!ApocaplayerBridge.Driving(eye),"a camera no longer owned by Apocaplayer is not intercepted");
            float replacementYaw;
            Check(!ApocaplayerBridge.CompassHeading(out replacementYaw),"replacement camera does not receive the old owner's heading");
            PlayerGame.Cam=eye;
            Apocasetter.InputBlocker.Active=true;Check(!ApocaplayerBridge.Driving(eye),"settings modal rejects takeover immediately");Apocasetter.InputBlocker.Active=false;
            Application.isFocused=false;Check(!ApocaplayerBridge.Driving(eye),"focus loss rejects takeover");Application.isFocused=true;
            PlayerGame.DeadFlag=true;Check(!ApocaplayerBridge.Driving(eye),"death rejects takeover");PlayerGame.DeadFlag=false;
            PlayerPlugin.Enabled.Value=false;Check(!ApocaplayerBridge.Driving(eye),"disabled player rejects takeover");PlayerPlugin.Enabled.Value=true;
            Plugin.Enabled.Value=false;Frame();Check(!ApocaplayerBridge.Driving(eye),"disabled ChaseCam yields original camera");Plugin.Enabled.Value=true;
            inCar.ActiveStateName="OutCar";PlayerGame.InCarFlag=false;Frame();Check(!ApocaplayerBridge.Driving(eye),"on-foot camera remains Apocaplayer's");
        }
        private static void LateSwitchesAndCleanup()
        {
            Setup();PlayerPlugin.OcclusionPrototype.Value=PlayerPlugin.OcclusionInVehicle.Value=true;
            Time.frameCount++;Time.unscaledTime+=1f/60f;ChaseView.Tick();Player.Tick();Player.LateCrosshair();
            Check(Field<bool>("_vCutaway"),"cursor initially caches an eligible cutaway pose");
            PlayerPlugin.OcclusionInVehicle.Value=false;Camera.Render(eye);
            Check(!Field<bool>("_vCutaway"),"late cutaway disable invalidates the shared cache before native fallback");
            Check(Apocaplayer.OcclusionCutaway.Calls==0,"late setting disable never prepares a cutaway");
            ChaseView.PostRender(eye);Player.EndOfFrame();
            Setup();Frame();
            ApocaplayerBridge.Refresh();
            Check(Field<int>("_viewFrame")==-1,"scene refresh invalidates the bridge's shared current-frame pose");
            Frame();
            for(int i=0;i<20;i++)
            {
                Time.frameCount++;Time.unscaledTime+=1f/60f;ChaseView.Tick();
                // The switch occurs after ChaseCam's Update, in the same frame.
                PlayerGame.Weapon=i%2==0?"Rifle":"";Player.Tick();Player.LateCrosshair();
                Camera.Render(eye);
                Vector3 published=Player.ViewPos;Quaternion rotation=Player.ViewRot;
                Vector2 expected=Project(eye.transform.position+eye.transform.forward*1000,published,rotation,Player.ViewFov);
                Near(Player.CrosshairScreen.x,expected.x,"late ownership switch keeps cursor aligned "+i);
                ChaseView.PostRender(eye);Player.EndOfFrame();
            }
            PlayerGame.Weapon="Rifle";PlayerGame.AimDownSights=true;
            Time.frameCount++;Time.unscaledTime+=.3f;Time.unscaledDeltaTime=.3f;ChaseView.Tick();Player.Tick();Player.LateCrosshair();Camera.Render(eye);
            Check(Field<bool>("_projSet"),"actual native aim owns a custom projection before interrupted cleanup");
            // Skip EndOfFrame: the next owner must release the interrupted native projection.
            PlayerGame.Weapon="";PlayerGame.AimDownSights=false;Time.unscaledDeltaTime=1f/60f;Frame();
            Check(!Field<bool>("_projSet"),"cruising recovers an interrupted native projection");
            Near(eye.projectionMatrix.m11,1f/(float)Math.Tan(eye.fieldOfView*Math.PI/360),"recovered projection matches normal visible FOV");
            var method=typeof(Player).GetMethod("Tick",BindingFlags.Public|BindingFlags.Static);
            string foreign="apoca.foreign-test";
            new HarmonyLib.Harmony(foreign).Patch(method,postfix:new HarmonyLib.HarmonyMethod(typeof(RealPlayerChecks),"ForeignPostfix"));
            ApocaplayerBridge.Shutdown();
            var info=HarmonyLib.Harmony.GetPatchInfo(method);
            Check(info!=null&&info.Owners.Contains(foreign),"shutdown preserves patches owned by other mods");
            Check(!info.Owners.Contains(Plugin.GUID+".apocaplayer"),"shutdown removes only ChaseCam compatibility hooks");
            new HarmonyLib.Harmony(foreign).UnpatchSelf();
            Setup();var enabled=typeof(ApocaplayerBridge).GetField("drawnWeapon",BindingFlags.NonPublic|BindingFlags.Static);
            enabled.SetValue(null,(Func<string>)delegate{throw new InvalidOperationException("injected interface failure");});
            Check(!ApocaplayerBridge.CruisingView,"interface failure yields safely");
            Check(!HarmonyLib.Harmony.GetPatchInfo(method).Owners.Contains(Plugin.GUID+".apocaplayer"),"interface failure removes compatibility hooks");
            ApocaplayerBridge.Refresh();ApocaplayerBridge.Discover();Check(!ApocaplayerBridge.ThirdPersonView,"failed interface is not repatched every frame");
        }
        private static void HolderTargetRecenter()
        {
            foreach(bool seatedEye in new[]{false,true})
            foreach(bool parentedHolder in new[]{false,true})
            {
                Setup();BindLook(false);Plugin.Recenter.Value=true;Plugin.RecenterDelay.Value=0;
                Transform holder=PlayerGame.CameraHolder;
                if(parentedHolder)holder.SetParent(player.transform,false);
                lookX.gameObject.Target=lookY.gameObject.Target=holder.gameObject;
                car.transform.rotation=Quaternion.Euler(8,40,-4);
                player.transform.rotation=car.transform.rotation;
                holder.rotation=Quaternion.Euler(10,110,0);
                var initial=holder.localEulerAngles;lookX.Seed(initial.y,0);lookY.Seed(0,initial.x);
                if(seatedEye)eye.transform.SetParent(player.transform.parent,true);
                eye.transform.rotation=holder.rotation;
                Vector3 originalPlayer=player.transform.forward,originalDirection=GameObject.Find("Direction").transform.forward;
                Frame();
                Near(CameraMath.Wrap(lookX.CachedYaw-holder.localEulerAngles.y),0,"holder yaw cache follows its own parent frame");
                Near(CameraMath.Wrap(lookY.CachedPitch-holder.localEulerAngles.x),0,"holder pitch cache uses native stored-angle convention");
                Near(Vector3.Distance(holder.forward,eye.transform.forward),0,"holder and cursor aim agree after passive recenter");
                Vector3 before=eye.transform.forward;
                Input.X=.2f;Input.Y=.1f;lookX.OnUpdate();lookY.OnUpdate();
                // Model native holder-driven aim/inheritance explicitly: the
                // lightweight Transform double does not propagate rotations.
                eye.transform.rotation=holder.rotation;Frame();Input.X=Input.Y=0;
                Check(Vector3.Distance(before,eye.transform.forward)<.03f,"small resumed input cannot restore stale holder aim");
                Near(Vector3.Distance(player.transform.forward,originalPlayer),0,"holder synchronization never turns the player");
                Near(Vector3.Distance(GameObject.Find("Direction").transform.forward,originalDirection),0,"holder synchronization never turns steering");
                PlayerGame.Weapon="Rifle";Frame();before=holder.forward;
                DrivingAim.Recenter(eye,Quaternion.Euler(0,-80,0));
                Near(Vector3.Distance(before,holder.forward),0,"armed view rejects holder synchronization");
                PlayerGame.Weapon="";Plugin.Recenter.Value=false;before=holder.forward;
                for(int i=0;i<20;i++)Frame();
                Near(Vector3.Distance(before,holder.forward),0,"disabled auto-recenter leaves holder untouched");
            }
            Plugin.RecenterDelay.Value=1.5f;
        }
        private static void ColdSeatedDiscovery()
        {
            Setup();eye.transform.SetParent(player.transform.parent,true);
            CameraBinding.Reset();ChaseView.Reset();Frame();
            Check(CameraBinding.FirstCamera==eye,"cold seated discovery binds the reparented native eye");
            Check(ApocaplayerBridge.Driving(eye),"cold seated discovery retains compatibility takeover");
            int finds=GameObject.FindCalls;
            for(int i=0;i<120;i++)CameraBinding.Resolve();
            Check(GameObject.FindCalls==finds,"stable seated discovery performs no repeated global camera searches");
            UnityEngine.Object.Destroy(eye.gameObject);
            eye=Child("PlayerCamera",player.transform.parent.gameObject).AddComponent<Camera>();PlayerGame.Cam=eye;
            Time.unscaledTime+=.51f;Frame();
            Check(CameraBinding.FirstCamera==eye&&ApocaplayerBridge.Driving(eye),"replaced seated eye is rediscovered at bounded retry");
            UnityEngine.Object.Destroy(eye.gameObject);
            new GameObject("PlayerCamera").AddComponent<Camera>();
            Time.unscaledTime+=.51f;CameraBinding.Resolve();
            Check(CameraBinding.FirstCamera==null,"unrelated scene camera with matching name is rejected");
        }
        private static void ForeignPostfix(){}
        public static void Main(string[] args)
        {
            string scenario=args.Length==0?"":args[0];
            if(scenario=="holder")HolderTargetRecenter();
            else if(scenario=="discovery")ColdSeatedDiscovery();
            else
            {
                if(scenario!="")throw new ArgumentException("Unknown scenario: "+scenario);
                CursorAndOwnership();PassiveRecenterCursor();RecenterOwnershipGuards();DormantAndLateNativeLook();
                HolderTargetRecenter();ColdSeatedDiscovery();CutawayAndCollision();ZoomAndGuards();LateSwitchesAndCleanup();
            }
            Player.Off();ApocaplayerBridge.Shutdown();
            Check(HarmonyLib.Harmony.GetAllPatchedMethods()!=null,"real Harmony lifecycle completed");
            Console.WriteLine("PASS: "+checks+" cross-mod checks using unchanged Apocaplayer ThirdPerson.cs and real Harmony; Unity/physics/effect dependencies simulated.");
        }
    }
}
#endif

