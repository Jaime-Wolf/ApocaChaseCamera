using System;
using System.Reflection;
using NWH.VehiclePhysics2;
using UnityEngine;
using UnityEngine.UI;

namespace ApocaChaseCamera
{
    internal static class HudChecks
    {
        private static int count;
        private static void Check(bool pass,string why){count++;if(!pass)throw new Exception(why);}
        private static void Near(float a,float b,string why){Check(Math.Abs(a-b)<.002f,why+": "+a+" vs "+b);}
        private static T Field<T>(string name){return (T)typeof(DrivingHud).GetField(name,BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);}
        private static GameObject Child(string name,GameObject parent,bool ui)
        {GameObject go=ui?new GameObject(name,typeof(RectTransform)):new GameObject(name);go.transform.SetParent(parent.transform,false);return go;}
        private static void Slot(GameObject inventory,string name,float x)
        {GameObject go=Child(name,inventory,true);RectTransform rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(70,80);rect.anchoredPosition=new Vector2(x,0);go.AddComponent<Image>();}
        private static void Tick(){Time.unscaledTime+=.11f;Time.frameCount++;DrivingHud.LateTick();}
        internal static int Run()
        {
            DrivingHud.Reset();ChaseView.Reset();CameraBinding.Reset();GameObject.Registry.Clear();
            Plugin.Enabled.Value=Plugin.HudEnabled.Value=true;Apocasetter.GameMenu.Paused=Apocasetter.InputBlocker.Active=false;Application.isFocused=true;Time.timeScale=1;
            GameObject car=new GameObject("HUDVehicle");VehicleController vehicle=car.AddComponent<VehicleController>();vehicle.SpeedSigned=20;vehicle.powertrain.engine.OutputRPM=3000;vehicle.powertrain.transmission.Gear=3;
            GameObject player=Child("Player",Child("sitPos",car,false),false);PlayMakerFSM seated=player.Add(new PlayMakerFSM{FsmName="InCar",ActiveStateName="InCar"});
            PlayMakerFSM health=player.Add(new PlayMakerFSM{FsmName="Health",ActiveStateName="Idle"});
            GameObject third=Child("3rdCamera",Child("DriveTrigger",car,false),false);Camera chase=Child("Camera",third,false).AddComponent<Camera>();
            Camera eye=Child("PlayerCamera",Child("PlayerCameraHolder",player,false),false).AddComponent<Camera>();eye.enabled=false;
            GameObject canvas=new GameObject("Canvas",typeof(RectTransform));canvas.GetComponent<RectTransform>().sizeDelta=new Vector2(1920,1080);
            GameObject inventory=Child("WeaponSlots_UI",canvas,true);inventory.GetComponent<RectTransform>().anchoredPosition=new Vector2(650,-425);
            for(int i=0;i<3;i++)Slot(inventory,"NativeSlot"+i,-80+i*80);
            Texture2D metal=new Texture2D(),glass=new Texture2D{width=256,height=256};
            Child("weapons_bg",Child("Design_UI",canvas,true),true).AddComponent<RawImage>().texture=metal;
            // An inactive native frame supplies artwork without affecting bounds.
            GameObject nativeRim=Child("Image_1",inventory,true);nativeRim.AddComponent<RawImage>().texture=glass;nativeRim.SetActive(false);
            Text native=Child("NutsLable",canvas,true).AddComponent<Text>();native.font=new Font();native.fontStyle=FontStyle.Bold;
            Outline shadow=native.gameObject.AddComponent<Outline>();shadow.effectDistance=new Vector2(2,-2);shadow.effectColor=new Color(0,0,0,.8f);shadow.useGraphicAlpha=true;
            Tick();RectTransform root=Field<RectTransform>("root");Check(root!=null&&root.gameObject.activeSelf,"HUD created only in third person");
            Check(Field<Text>("speed").text=="72","speed uses km/h");Check(Field<Text>("rpm").text=="3000","RPM comes from native engine");Check(Field<Text>("gear").text=="3 / 5","gear and forward count use native transmission");
            Near(Field<Image>("rpmFill").rectTransform.anchorMax.x,.5f,"RPM bar is actual proportional geometry");
            Check(Field<Text>("speed").font==native.font&&Field<Text>("speed").fontStyle==native.fontStyle,"native inventory font reused");
            Near(Field<Text>("speed").GetComponent<Outline>().effectDistance.x,2,"native outline reused");
            Check(root.GetComponent<Image>().sprite.texture==metal,"HUD uses game's rusty inventory plate");
            Check(root.GetComponent<Image>().type==Image.Type.Sliced,"rust frame retains corner proportions");
            foreach(Image image in root.GetComponentsInChildren<Image>(true))
                Check(image.sprite==null||image.sprite.texture!=glass,"equipped-item texture never used by HUD");
            Check(root.Find("RPM housing")!=null&&root.Find("Gear housing")!=null,"each reading has its own housing");
            Near(Field<GaugeFace>("speedDial").Fraction,.3f,"speed needle reads actual 72 km/h on 240 km/h dial");
            Near(Field<GaugeFace>("rpmDial").Fraction,.5f,"RPM needle follows engine redline fraction");
            glass.width=128;nativeRim.GetComponent<RawImage>().texture=new Texture2D{width=128,height=128};Tick();
            foreach(Image image in root.GetComponentsInChildren<Image>(true))
                Check(image.sprite==null||image.sprite.texture==metal,"weapon changes cannot appear as gauge artwork");
            foreach(GaugeFace dial in root.GetComponentsInChildren<GaugeFace>(true))
            {
                foreach(float fraction in new[]{0f,.5f,1f})
                {
                    dial.Fraction=fraction;VertexHelper mesh=new VertexHelper();
                    typeof(GaugeFace).GetMethod("OnPopulateMesh",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(dial,new object[]{mesh});
                    Check(mesh.vertices.Count>100&&mesh.triangles.Count>100,"gauge has real dial geometry");
                    Rect rect=dial.rectTransform.rect;bool valid=true;
                    foreach(Vector3 point in mesh.vertices)
                        valid &= !float.IsNaN(point.x)&&!float.IsNaN(point.y)&&point.x>=rect.xMin-1&&point.x<=rect.xMax+1&&point.y>=rect.yMin-1&&point.y<=rect.yMax+1;
                    foreach(int index in mesh.triangles)valid &= index>=0&&index<mesh.vertices.Count;
                    Check(valid,"dial mesh stays within its housing at each needle position");
                }
            }
            Tick();
            foreach(Graphic graphic in root.GetComponentsInChildren<Graphic>(true))Check(!graphic.raycastTarget,"HUD cannot intercept inventory clicks");
            Check(root.anchoredPosition.y>-385,"HUD placed above inventory");float originalX=root.anchoredPosition.x;
            for(int i=0;i<3;i++)Slot(inventory,"ExtraSlot_apocapocket"+i,-160-i*80);Tick();
            Check(root.anchoredPosition.x<originalX-80,"HUD follows six-slot inventory expansion");
            Plugin.HudGap.Value=50;Tick();Near(root.anchoredPosition.y,-335,"HUD height adjustable");Plugin.HudGap.Value=10;
            Plugin.HudScale.Value=1.5f;Tick();Near(root.sizeDelta.x,660,"HUD size adjustable");Plugin.HudScale.Value=1;
            vehicle.SpeedSigned=-10;vehicle.powertrain.transmission.Gear=-1;vehicle.powertrain.engine.OutputRPM=5700;Tick();
            Check(Field<Text>("speed").text=="36","reverse speed remains positive");Check(Field<Text>("gear").text=="R / 5","reverse display");
            Check(Field<Image>("rpmFill").color.r>.9f&&Field<Image>("rpmFill").color.g<.3f,"RPM warning near native redline");
            vehicle.powertrain.transmission.gears.Add(.5f);vehicle.powertrain.transmission.Gear=0;Tick();Check(Field<Text>("gear").text=="N / 6","gear count updates for changed gearing");
            vehicle.powertrain.engine=null;Tick();Check(Field<Text>("rpm").text=="0","missing engine handled");Near(Field<Image>("rpmFill").rectTransform.anchorMax.x,0,"missing engine clears gauge");
            int nativeGear=vehicle.powertrain.transmission.Gear;float speedBefore=vehicle.SpeedSigned;Tick();Near(vehicle.SpeedSigned,speedBefore,"HUD never changes physics");Check(vehicle.powertrain.transmission.Gear==nativeGear,"HUD never shifts gears");
            third.SetActive(false);eye.enabled=true;Apocaplayer.ThirdPerson.On=false;Tick();Check(!root.gameObject.activeSelf,"HUD hidden in first person");
            third.SetActive(true);eye.enabled=false;Tick();Check(root.gameObject.activeSelf,"HUD returns to third person");
            Apocasetter.GameMenu.Paused=true;Tick();Check(!root.gameObject.activeSelf,"HUD hidden while paused");Apocasetter.GameMenu.Paused=false;
            Apocasetter.InputBlocker.Active=true;Tick();Check(!root.gameObject.activeSelf,"HUD hidden in settings");Apocasetter.InputBlocker.Active=false;
            Plugin.HudEnabled.Value=false;Tick();Check(!root.gameObject.activeSelf,"HUD optional");Plugin.HudEnabled.Value=true;
            inventory.SetActive(false);Tick();Check(!root.gameObject.activeSelf,"HUD follows native inventory visibility");inventory.SetActive(true);
            seated.ActiveStateName="OutCar";Tick();Check(!root.gameObject.activeSelf,"HUD hides on vehicle exit");seated.ActiveStateName="InCar";
            health.FsmVariables.Health.Value=.1f;Tick();Check(!root.gameObject.activeSelf,"HUD hides on death");health.FsmVariables.Health.Value=100;
            // Optional Apocaplayer third person still gets readings with a drawn
            // weapon, although camera smoothing yields to that mod for aiming.
            third.SetActive(false);eye.enabled=true;Apocaplayer.ThirdPerson.On=true;Apocaplayer.Game.Weapon="Rifle";Tick();Check(root.gameObject.activeSelf,"HUD supports optional third-person weapon view");
            Apocaplayer.ThirdPerson.Peek=true;Tick();Check(!root.gameObject.activeSelf,"HUD hides for binoculars");Apocaplayer.ThirdPerson.Peek=false;Apocaplayer.Game.Weapon="";
            foreach(float scale in new[]{.65f,1f,1.75f})foreach(int width in new[]{800,1280,1920,3440})
            {
                float left=-width*.5f,right=width*.5f;HudLayout layout=HudMath.Layout(left,right,-540,540,right-500,right-100,-400,scale,10);
                Check(layout.CenterX-layout.Width*.5f>=left+11.99f&&layout.CenterX+layout.Width*.5f<=right-11.99f,"HUD stays inside horizontal canvas at "+width);
                Check(layout.Bottom+layout.Height<=528.01f&&layout.Bottom>=-528.01f,"HUD stays inside vertical canvas");
                Check(layout.Bottom>-400,"HUD remains above inventory");
            }
            Near(HudMath.RpmFraction(9000,6000),1,"gauge clamps above redline");Near(HudMath.RpmFraction(float.NaN,6000),0,"invalid RPM handled");Near(HudMath.Reading(float.PositiveInfinity),0,"invalid speed handled");
            Check(HudMath.Gear(-2,5)=="R2 / 5","multiple reverse gears named");
            Sprite ownMetal=Field<Sprite>("rustFrame");
            DrivingHud.Reset();Check(!GameObject.Registry.ContainsKey("ApocaChaseCamera.DrivingHUD"),"scene reset removes own HUD");
            Check(ownMetal.destroyed,"scene reset frees only owned sprite wrappers");
            Plugin.HudScale.Value=1;Plugin.HudGap.Value=10;
            Console.WriteLine("Driving HUD: "+count+" checks for live readings, native style, gauge geometry, expanded inventory, resolution/scale, and visibility.");
            return count;
        }
    }
}
