using System;
using System.IO;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace ApocaChaseCamera
{
    internal static class Program
    {
        private static int checks;
        private static GameObject player,car,third; private static Camera camera,eye;
        private static PlayMakerFSM inCar,health,ads,bino;
        private static void Check(bool pass,string why){checks++;if(!pass)throw new Exception(why);}
        private static void Near(float a,float b,float tolerance,string why){Check(Math.Abs(a-b)<tolerance,why+": "+a+" vs "+b);}
        private static T Field<T>(string name){return (T)typeof(ChaseView).GetField(name,BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);}
        private static GameObject Child(string name,GameObject parent){GameObject go=new GameObject(name);go.transform.Parent(parent.transform);return go;}
        private static void Frame(float dt){Time.unscaledTime+=dt;Time.frameCount++;ChaseView.Tick();}
        private static void Render(){ChaseView.PreCull(camera);}
        private static void Finish(){ChaseView.PostRender(camera);}
        private static void Setup(string name,Vector3 trigger,Vector3 anchor)
        {
            ChaseView.Reset();CameraBinding.Reset();GameObject.Registry.Clear();
            car=new GameObject(name); GameObject drive=Child("DriveTrigger",car);drive.transform.localPosition=trigger;
            third=Child("3rdCamera",drive);third.transform.localPosition=anchor;
            camera=Child("Camera",third).Add(new Camera());
            player=Child("Player",Child("sitPos",car));
            inCar=player.Add(new PlayMakerFSM {FsmName="InCar",ActiveStateName="InCar"});
            health=player.Add(new PlayMakerFSM {FsmName="Health",ActiveStateName="Idle"});
            GameObject holder=Child("PlayerCameraHolder",player);GameObject eyeGo=Child("PlayerCamera",holder);eye=eyeGo.Add(new Camera());eye.enabled=false;
            ads=eyeGo.Add(new PlayMakerFSM {FsmName="AimDownSIghts_Hold",ActiveStateName="idle"});
            bino=Child("Binocular Anim",Child("ItemAnim",eyeGo)).Add(new PlayMakerFSM {FsmName="Animation",ActiveStateName="off"});
            Physics.Hits=new RaycastHit[0];Physics.Overlaps=new Collider[0];Input.X=Input.Y=0;
            Physics.GroundProbe=null;Terrain.activeTerrains=new Terrain[0];
            Plugin.Enabled.Value=true;Apocasetter.GameMenu.Paused=false;Apocasetter.InputBlocker.Active=false;Application.isFocused=true;Time.timeScale=1;
        }
        private static Vector3 Parse(string[] values,int start){return new Vector3(float.Parse(values[start],CultureInfo.InvariantCulture),float.Parse(values[start+1],CultureInfo.InvariantCulture),float.Parse(values[start+2],CultureInfo.InvariantCulture));}
        public static void Main(string[] args)
        {
            // Real prefab camera anchors extracted read-only from game assets.
            int vehicles=0;
            foreach(string line in File.ReadAllLines(args[0]))
            {
                string[] v=line.Split(',');Setup(v[0],Parse(v,1),Parse(v,4));Frame(1f/60f);Render();
                Check(CameraBinding.Car==car.transform,v[0]+" car binding");Check(CameraBinding.NativeCamera==camera,v[0]+" native camera binding");
                Vector3 expected=car.transform.TransformPoint(CameraBinding.LocalAnchor);
                Near(Field<Vector3>("previousRaw").y,expected.y,.0001f,v[0]+" anchor");
                Check(camera.worldToCameraMatrix.token!=101,v[0]+" rendered chase view");Finish();
                Check(camera.worldToCameraMatrix.token==101 && camera.cullingMatrix.token==202,v[0]+" restored camera matrices");vehicles++;
                Check(camera.ViewResets==1 && camera.CullResets==1,v[0]+" Unity automatic matrices restored rather than frozen snapshot");
            }
            Check(vehicles==14,"all 14 stock prefab fixtures");
            Setup("ModdedVehicleUsingNormalDriveTrigger",new Vector3(0,0,0),new Vector3(0,.3f,0));Frame(.016f);Render();
            Check(camera.worldToCameraMatrix.token!=101,"unlisted modded vehicle works through standard binding");Finish();
            Vector3 oldPlayer=player.transform.position,oldCar=car.transform.position,oldCamera=camera.transform.position;
            car.transform.rotation=Quaternion.Euler(25,20,35);Frame(.016f);oldCamera=camera.transform.position;oldPlayer=player.transform.position;Render();
            Near(Field<Quaternion>("rotation").euler.z,0,.0001f,"camera roll removed");
            Near(Field<Quaternion>("rotation").euler.x,8,.0001f,"vehicle pitch does not tilt view");
            Near(Vector3.Distance(oldCamera,camera.transform.position),0,.0001f,"rendering does not move game camera");
            Near(Vector3.Distance(oldPlayer,player.transform.position),0,.0001f,"rendering does not move player");
            Near(Vector3.Distance(oldCar,car.transform.position),0,.0001f,"rendering does not move car");
            Near(camera.fieldOfView,60,.0001f,"no FOV pumping");Check(inCar.ActiveStateName=="InCar","FSM untouched");Finish();
            Input.X=4;Frame(.016f);Render();float orbit=Field<float>("orbit");Near(orbit,8,.0001f,"mouse look immediate");
            Finish();Render();Near(Field<float>("orbit"),orbit,.0001f,"multiple renders do not double input");Finish();Input.X=0;
            Frame(.5f);Render();Near(Field<float>("orbit"),orbit,.0001f,"recenter delay respected");Finish();
            for(int i=0;i<300;i++){Frame(1f/60);Render();Finish();}Check(Math.Abs(Field<float>("orbit"))<.2f,"camera returns behind car");
            float before=Field<Vector3>("previousRaw").y;car.transform.position=car.transform.position+new Vector3(100,100,100);Frame(.016f);Render();
            Near(Field<float>("vertical"),before+100,.001f,"floating origin or teleport resets history");Finish();
            car.transform.position=car.transform.position+new Vector3(50,0,0);Frame(.016f);Render();
            Near(Field<Vector3>("position").x,car.transform.TransformPoint(CameraBinding.LocalAnchor).x+(Field<Quaternion>("rotation")*new Vector3(0,0,-6.5f)).x,.001f,"camera keeps up horizontally");Finish();
            Collider own=car.Add(new Collider());Collider wall=new GameObject("Wall").Add(new Collider());
            Physics.Hits=new[]{new RaycastHit{collider=own,distance=.1f}};Frame(.016f);Render();Near(Field<float>("distance"),6.5f,.01f,"own vehicle ignored");Finish();
            Physics.Hits=new[]{new RaycastHit{collider=wall,distance=2f}};Frame(.016f);Render();Check(Field<float>("distance")<2,"wall contraction immediate");Finish();
            Physics.Hits=new RaycastHit[0];Frame(.016f);Render();Check(Field<float>("distance")>1.92f&&Field<float>("distance")<3,"wall recovery gentle");Finish();
            Physics.Overlaps=new[]{wall};Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"no custom view inside unresolved solid");Finish();Physics.Overlaps=new Collider[0];
            inCar.ActiveStateName="OutCar";Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"exit restores original view");inCar.ActiveStateName="InCar";
            third.activeSelf=false;eye.enabled=true;Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"first-person view untouched");third.activeSelf=true;eye.enabled=false;
            Frame(.016f);Render();Apocasetter.GameMenu.Paused=true;Frame(.016f);Check(camera.worldToCameraMatrix.token==101,"pause restores pending render");Apocasetter.GameMenu.Paused=false;
            Plugin.Enabled.Value=false;Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"disable restores native view");Plugin.Enabled.Value=true;
            Apocasetter.InputBlocker.Active=true;Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"modal blocks camera input");Apocasetter.InputBlocker.Active=false;
            Application.isFocused=false;Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"unfocused camera reset");Application.isFocused=true;
            ads.ActiveStateName="ads";Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"ADS preserved");ads.ActiveStateName="idle";
            bino.ActiveStateName="animOn";Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"binocular raise preserved");bino.ActiveStateName="off";
            health.FsmVariables.Health.Value=.1f;Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"death view preserved");health.FsmVariables.Health.Value=100;
            Plugin.Height.Value=4;Frame(.016f);Render();float high=Field<Vector3>("position").y;Finish();Plugin.Height.Value=2;Frame(.016f);Render();Near(high-Field<Vector3>("position").y,2,.001f,"height slider applies live");Finish();Plugin.Height.Value=1.7f;
            NumericalChecks();
            ElevationChecks();
            RecenterChecks();
            QueryChecks();
            // Exercise production optional bridge against a declared interface
            // double. This verifies reflection/guards, not the real mod runtime.
            HarmonyLib.AccessTools.FindTypes=true;Time.unscaledTime+=3;ApocaplayerBridge.Discover();Apocaplayer.ThirdPerson.On=true;
            Check(ApocaplayerBridge.CruisingView,"optional bridge unarmed cruise");
            Apocaplayer.Game.Weapon="Rifle";Check(!ApocaplayerBridge.CruisingView,"bridge hands weapon view back");Apocaplayer.Game.Weapon="";
            Apocaplayer.ThirdPerson.Peek=true;Check(!ApocaplayerBridge.CruisingView,"bridge binocular guard");Apocaplayer.ThirdPerson.Peek=false;
            Apocaplayer.ThirdPerson.AimZoom=true;Check(!ApocaplayerBridge.CruisingView,"bridge aim guard");Apocaplayer.ThirdPerson.AimZoom=false;
            third.activeSelf=false;eye.enabled=true;Frame(.016f);Check(CameraBinding.SelectedCamera()==eye,"optional mod camera selected");
            MethodInfo prefix=typeof(ApocaplayerBridge).GetMethod("BeforePreCull",BindingFlags.Static|BindingFlags.NonPublic);
            Check(!(bool)prefix.Invoke(null,new object[]{eye}),"successful bridge replaces original cruise render");Check(eye.worldToCameraMatrix.token!=101,"bridge render override");Check(Apocaplayer.ThirdPerson.HasView,"bridge publishes render view");
            Check(Apocaplayer.OcclusionCutaway.Calls==0,"disabled cutaway remains disabled");ChaseView.PostRender(eye);
            Apocaplayer.Plugin.OcclusionPrototype.Value=true;Frame(.016f);prefix.Invoke(null,new object[]{eye});Check(Apocaplayer.OcclusionCutaway.Calls==1,"enabled cutaway follows new view");ChaseView.PostRender(eye);
            checks += HudChecks.Run();
            checks += RuntimeRegressionChecks.Run();
            checks += BindingRegressionChecks.Run();
            checks += HudRegressionChecks.Run();
            checks += CompassRegressionChecks.Run();
            Console.WriteLine("PASS: "+checks+" checks; 14 real prefab fixtures; render lifecycle simulated; real Unity gameplay still needs testing.");
        }
        private static void NumericalChecks()
        {
            float[] results=new float[3];int[] rates={30,60,144};
            for(int r=0;r<rates.Length;r++)
            {
                int hz=rates[r];float state=0,rawEnergy=0,filteredEnergy=0;
                for(int i=0;i<hz*10;i++)
                {
                    float raw=.4f*(float)Math.Sin(2*Math.PI*3*i/hz);
                    state=CameraMath.FollowHeight(state,raw,.35f,.6f,1f/hz);
                    if(i>hz*2){rawEnergy+=raw*raw;filteredEnergy+=state*state;}
                }
                results[r]=(float)Math.Sqrt(filteredEnergy/rawEnergy);
                Check(results[r]<.2f,"3Hz road bumps reduced at "+hz+" FPS");
                float step=0;for(int i=0;i<hz;i++)step=CameraMath.Follow(step,1,.35f,1f/hz);
                Near(step,1f-(float)Math.Exp(-1/.35),.00002f,"one-second response independent of FPS "+hz);
            }
            Check(Math.Abs(results[0]-results[2])<.01f,"bump damping consistent across FPS");
            Near(CameraMath.Follow(9,12,0,.016f),12,.00001f,"zero smoothing bypass");
            float yaw=179;for(int i=0;i<60;i++)yaw=CameraMath.FollowAngle(yaw,-179,.25f,1f/60);
            Check(Math.Abs(CameraMath.Wrap(yaw-(-179)))<.05f,"heading follows across +/-180 without full spin");
            Near(CameraMath.SafeDistance(8,2,.45f,.016f),2,.00001f,"hard obstacle has no smoothing delay");
            Check(CameraMath.NeedsReset(100,.016f)&&CameraMath.NeedsReset(0,2),"teleport and long frame reset");
            Console.WriteLine("3Hz bump amplitude remaining at 30/60/144 FPS: "+String.Join(", ",Array.ConvertAll(results,v=>v.ToString("P1",CultureInfo.InvariantCulture))));
        }
        private static void ElevationChecks()
        {
            // Reproduce the reported problem: a fast climb followed by a fast
            // descent while horizontal camera tracking remains immediate.
            foreach(int hz in new[]{30,60,144})
            {
                Setup("ElevationRegression",new Vector3(0,0,0),new Vector3(0,.3f,0));Frame(1f/hz);Render();Finish();
                for(int i=0;i<hz*2;i++)
                {
                    car.transform.position=car.transform.position+new Vector3(0,12f/hz,20f/hz);Frame(1f/hz);Render();
                    Check(Math.Abs(Field<float>("vertical")-Field<Vector3>("previousRaw").y)<=.6001f,"steep climb stays close to vehicle at "+hz+" FPS");Finish();
                }
                for(int i=0;i<hz*2;i++)
                {
                    car.transform.position=car.transform.position+new Vector3(0,-12f/hz,20f/hz);Frame(1f/hz);Render();
                    Check(Math.Abs(Field<float>("vertical")-Field<Vector3>("previousRaw").y)<=.6001f,"steep descent does not leave camera floating at "+hz+" FPS");Finish();
                }
            }
            foreach(float boom in new[]{6.5f,15f})
            {
                Setup("TerrainRegression",new Vector3(0,0,0),new Vector3(0,.3f,0));car.transform.position=new Vector3(0,1,0);
                Terrain terrain=new GameObject("Terrain").Add(new Terrain());terrain.transform.position=new Vector3(-500,-500,-500);
                terrain.Height=p=>-p.z*.8f;Terrain.activeTerrains=new[]{terrain};Plugin.Distance.Value=boom;Frame(.016f);Render();
                Vector3 view=Field<Vector3>("position");
                Check(camera.worldToCameraMatrix.token!=101,"terrain correction retains custom chase view");
                Check(view.y>=terrain.Height(view)+.82f,"camera clears rising terrain even when physics casts miss it at boom "+boom);
                Check(Field<float>("distance")<boom,"terrain retracts boom rather than throwing view upward");
                Near(view.y,1.3f+Plugin.Height.Value+(Field<Quaternion>("rotation")*new Vector3(0,0,-Field<float>("distance"))).y,.001f,"terrain does not add artificial height");Finish();
                terrain.Height=p=>-100;Frame(.016f);Render();Check(Field<float>("distance")<boom,"crest clearance returns distance gradually");Finish();
            }
            Plugin.Distance.Value=6.5f;
            Setup("RoadMeshRegression",new Vector3(0,0,0),new Vector3(0,.3f,0));car.transform.position=new Vector3(0,1,0);
            Collider road=new GameObject("RoadMesh").Add(new Collider());
            Physics.GroundProbe=p=>{
                float ground=-p.z*.8f;
                if(ground>p.y||ground<p.y-12)return new RaycastHit[0];
                return new[]{new RaycastHit{collider=road,normal=new Vector3(0,.78087f,.624695f),point=new Vector3(p.x,ground,p.z)}};
            };
            Frame(.016f);Render();Vector3 meshView=Field<Vector3>("position");Check(meshView.y>=-meshView.z*.8f+.82f,"mesh roads get ground clearance");Finish();
            Setup("HeightLagDisabled",new Vector3(0,0,0),new Vector3(0,.3f,0));Plugin.MaxHeightLag.Value=0;Frame(.016f);Render();Finish();
            car.transform.position=new Vector3(0,-5,0);Frame(.016f);Render();Near(Field<float>("vertical"),Field<Vector3>("previousRaw").y,.0001f,"zero height lag follows drop immediately");Finish();Plugin.MaxHeightLag.Value=.6f;
            Setup("BridgeRegression",new Vector3(0,0,0),new Vector3(0,.3f,0));
            // Downward probes start only clearance metres above the camera, so
            // an overhead bridge does not become a false floor.
            float probeRise=0;
            Physics.GroundProbe=p=>{probeRise=Math.Max(probeRise,p.y-(Field<Vector3>("previousRaw").y+Plugin.Height.Value+.91f));return new RaycastHit[0];};
            Frame(.016f);Render();Check(probeRise<1.1f,"ground probe does not start far above overhead structures");Finish();
            Setup("BridgeReady",new Vector3(0,0,0),new Vector3(0,.3f,0));Frame(.016f);Render();Finish();
            Console.WriteLine("Elevation regression: climbs/drops at 30/60/144 FPS bounded to 0.6 m; terrain and road clearance checked at short and maximum boom lengths.");
        }
        private static void RecenterChecks()
        {
            Setup("RecenterRegression",new Vector3(0,0,0),new Vector3(0,.3f,0));Frame(.016f);Render();Finish();
            Input.X=50;Input.Y=-10;Frame(.016f);Render();Finish();Input.X=Input.Y=0;
            float looked=Field<float>("orbit"),pitched=Field<float>("pitchOrbit");
            car.transform.position=new Vector3(1000,0,1000);Frame(.016f);Render();
            Near(Field<float>("orbit"),looked,.0001f,"floating origin preserves look-around direction");
            Near(Field<float>("pitchOrbit"),pitched,.0001f,"floating origin preserves look-around pitch");Finish();
            Frame(1.25f);Render();Near(Field<float>("orbit"),looked,.0001f,"frame hitch cannot snap orbit behind vehicle");Finish();
            Apocasetter.GameMenu.Paused=true;Frame(.016f);Time.unscaledTime+=3;Apocasetter.GameMenu.Paused=false;Frame(.016f);Render();
            Near(Field<float>("orbit"),looked,.0001f,"pause/resume preserves orbit");Finish();
            Apocasetter.InputBlocker.Active=true;Frame(.016f);Time.unscaledTime+=2;Apocasetter.InputBlocker.Active=false;Frame(.016f);Render();
            Near(Field<float>("orbit"),looked,.0001f,"settings modal preserves orbit");Finish();
            Application.isFocused=false;Frame(.016f);Time.unscaledTime+=2;Application.isFocused=true;Frame(.016f);Render();
            Near(Field<float>("orbit"),looked,.0001f,"focus return preserves orbit");Finish();
            float last=looked;
            for(int i=0;i<420;i++)
            {
                Frame(1f/60);Render();float current=Field<float>("orbit");
                Check(Math.Abs(CameraMath.Wrap(current-last))<=120f/60+.001f,"return speed bounded on every render");
                if(i<30)Near(current,looked,.0001f,"recenter delay survives interruption");last=current;Finish();
            }
            Check(Math.Abs(last)<.1f,"eased recenter eventually returns behind car");
            float[] end=new float[3];int[] rates={30,60,144};
            for(int j=0;j<rates.Length;j++)
            {
                float angle=170,blend=0,dt=1f/rates[j];
                for(int i=0;i<rates[j]*2;i++)
                {
                    blend=CameraMath.Follow(blend,1,.2f,dt);
                    float next=CameraMath.RecenterAngle(angle,.7f,blend,dt);
                    Check(Math.Abs(CameraMath.Wrap(next-angle))<=120f*dt+.001f,"large orbit returns without a snap at "+rates[j]+" FPS");angle=next;
                }
                end[j]=angle;
            }
            Check(Math.Abs(end[0]-end[2])<.6f,"recenter timing consistent across frame rates");
            Setup("BridgeReady",new Vector3(0,0,0),new Vector3(0,.3f,0));Frame(.016f);Render();Finish();
            Console.WriteLine("Recenter regression: look direction preserved across origin shifts, hitches, pause, settings, and focus changes; eased return capped at 120 deg/s.");
        }
        private static void QueryChecks()
        {
            Setup("BufferedCameraQueries",new Vector3(0,0,0),new Vector3(0,.3f,0));
            Physics.AllocatingQueries=Physics.BufferedQueries=0;
            for(int i=0;i<600;i++){Frame(1f/60);Render();Finish();}
            Check(Physics.AllocatingQueries==0&&Physics.BufferedQueries==2400,"Ten seconds of clear camera frames use four buffered queries each without duplicated clearance probes");
            Collider own=car.Add(new Collider()),wall=new GameObject("Crowded wall").Add(new Collider());
            RaycastHit[] many=new RaycastHit[129];for(int i=0;i<128;i++)many[i]=new RaycastHit{collider=own,distance=.1f};many[128]=new RaycastHit{collider=wall,distance=2};
            Physics.Hits=many;Frame(.016f);Render();Check(Field<float>("distance")<2&&Physics.AllocatingQueries==0,"A growing sweep buffer finds the wall after 128 own-vehicle parts without an allocating physics fallback");Finish();
            Physics.Hits=new RaycastHit[0];Collider[] overlaps=new Collider[129];for(int i=0;i<128;i++)overlaps[i]=own;overlaps[128]=wall;Physics.Overlaps=overlaps;
            Frame(.016f);Render();Check(camera.worldToCameraMatrix.token==101,"A saturated overlap cannot discard a wall and render the camera inside it");Finish();Physics.Overlaps=new Collider[0];
            Collider road=new GameObject("Crowded road").Add(new Collider());
            for(int i=0;i<128;i++)many[i]=new RaycastHit{collider=own,normal=Vector3.up,point=new Vector3()};many[128]=new RaycastHit{collider=road,normal=Vector3.up,point=new Vector3()};
            Physics.GroundProbe=p=>many;
            object support=typeof(ChaseView).GetMethod("GroundSupport",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{new Vector3(0,1,0)});
            Check(support!=null&&((RaycastHit)support).collider==road,"A saturated support ray still finds the real road beyond own colliders");
            Physics.GroundProbe=null;Frame(.016f);Render();Check(camera.worldToCameraMatrix.token!=101,"Cleared query buffers do not reuse stale crowded-scene collisions");Finish();
            for(int i=0;i<100;i++){Time.unscaledTime+=2;ApocaplayerBridge.Discover();}
            Check(HarmonyLib.AccessTools.TypeSearches==0,"Optional bridge discovery never calls the warning-producing Harmony missing-type search");
            object missing=typeof(ApocaplayerBridge).GetMethod("FindType",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{"Missing.OptionalCamera.Type"});
            Check(missing==null,"Quiet optional lookup accepts a genuinely absent type without requiring it");
        }
    }
}
