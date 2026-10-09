#if REAL_APOCAPLAYER
// External services/Unity are simulated; ThirdPerson.cs itself is compiled
// unchanged from an independently supplied Apocaplayer checkout.
using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public partial struct Vector3
    {
        public static Vector3 zero { get { return new Vector3(); } }
        public static Vector3 forward { get { return new Vector3(0,0,1); } }
        public static Vector3 right { get { return new Vector3(1,0,0); } }
        public Vector3 normalized { get { return this/Math.Max(.00001f,magnitude); } }
        public void Normalize(){this=normalized;}
        public static Vector3 Cross(Vector3 a,Vector3 b){return new Vector3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);}
        public static float Dot(Vector3 a,Vector3 b){return a.x*b.x+a.y*b.y+a.z*b.z;}
    }
    public partial struct Vector2
    {
        public static Vector2 operator -(Vector2 a,Vector2 b){return new Vector2(a.x-b.x,a.y-b.y);}
        public static implicit operator Vector3(Vector2 a){return new Vector3(a.x,a.y,0);}
        public string ToString(string format){return x.ToString(format)+","+y.ToString(format);}
    }
    public partial struct Quaternion
    {
        public static Quaternion identity {get{return Euler(0,0,0);}}
        public static Quaternion LookRotation(Vector3 forward,Vector3 up)
        {return Euler(Mathf.Atan2(-forward.y,Mathf.Sqrt(forward.x*forward.x+forward.z*forward.z))*Mathf.Rad2Deg,Mathf.Atan2(forward.x,forward.z)*Mathf.Rad2Deg,0);}
        public static Quaternion Slerp(Quaternion a,Quaternion b,float k)
        {
            var q=System.Numerics.Quaternion.Slerp(new System.Numerics.Quaternion(a.x,a.y,a.z,a.w),new System.Numerics.Quaternion(b.x,b.y,b.z,b.w),k);
            var v=new Quaternion{x=q.X,y=q.Y,z=q.Z,w=q.W};var f=v*Vector3.forward;
            v.euler=new Vector3(Mathf.Atan2(-f.y,Mathf.Sqrt(f.x*f.x+f.z*f.z))*Mathf.Rad2Deg,Mathf.Atan2(f.x,f.z)*Mathf.Rad2Deg,0);return v;
        }
    }
    public partial struct Matrix4x4
    {
        internal static Matrix4x4 Numeric(System.Numerics.Matrix4x4 value){return new Matrix4x4{numeric=value};}
        public float m11 {get{return numeric.M22;}}
        public static Matrix4x4 Perspective(float fov,float aspect,float near,float far)
        {
            // OpenGL-style column projection, stored transposed for System.Numerics row vectors.
            float y=1f/(float)Math.Tan(fov*Math.PI/360);
            return Numeric(new System.Numerics.Matrix4x4(y/aspect,0,0,0,0,y,0,0,0,0,-(far+near)/(far-near),-1,0,0,-2*far*near/(far-near),0));
        }
        public static Vector4 operator *(Matrix4x4 a,Vector4 b)
        {var v=System.Numerics.Vector4.Transform(new System.Numerics.Vector4(b.x,b.y,b.z,b.w),a.numeric);return new Vector4(v.X,v.Y,v.Z,v.W);}
    }
    public partial class Camera
    {
        public float farClipPlane=1000f;
        public bool usePhysicalProperties;
        public static event Action<Camera> onPreCull;
        public static void Render(Camera camera){if(onPreCull!=null)onPreCull(camera);}
        public void ResetProjectionMatrix(){projectionMatrix=Matrix4x4.Perspective(fieldOfView,aspect,nearClipPlane,farClipPlane);}
    }
    public enum KeyCode {None,LeftAlt}
    public static partial class Input
    {
        public static bool ObserveHeld,ObserveDown,MiddleHeld,MiddleDown,ChangeCamera;
        public static float GetAxis(string name){return GetAxisRaw(name);}
        public static bool GetKey(KeyCode key){return key!=KeyCode.None&&ObserveHeld;}
        public static bool GetKeyDown(KeyCode key){return key!=KeyCode.None&&ObserveDown;}
        public static bool GetMouseButton(int key){return MiddleHeld;}
        public static bool GetMouseButtonDown(int key){return MiddleDown;}
        public static bool GetButtonDown(string name){return ChangeCamera;}
    }
    public static partial class Mathf
    {
        public static float Clamp(float v,float a,float b){return Math.Max(a,Math.Min(b,v));}
        public static float Clamp01(float v){return Clamp(v,0,1);}
        public static float Abs(float v){return Math.Abs(v);}
        public static float Exp(float v){return (float)Math.Exp(v);}
        public static float Pow(float a,float b){return (float)Math.Pow(a,b);}
        public static float Lerp(float a,float b,float k){return a+(b-a)*Clamp01(k);}
        public static float DeltaAngle(float a,float b){return ApocaChaseCamera.CameraMath.Wrap(b-a);}
        public static float LerpAngle(float a,float b,float k){return a+DeltaAngle(a,b)*Clamp01(k);}
        public static float MoveTowards(float a,float b,float d){return Math.Abs(b-a)<=d?b:a+Math.Sign(b-a)*d;}
    }
    public static partial class Time {public static float unscaledDeltaTime=1f/60f,deltaTime=1f/60f;}
    public static class Screen {public static int width=1920,height=1080;}
    public static class Resources
    {public static T[] FindObjectsOfTypeAll<T>() where T:Component{var result=new List<T>();foreach(var go in GameObject.Registry.Values)result.AddRange(go.GetComponents<T>());return result.ToArray();}}
    public struct Scene {public bool IsValid(){return true;}}
    public enum RenderMode {ScreenSpaceOverlay,ScreenSpaceCamera}
    public class Canvas:Component {public Canvas rootCanvas {get{return this;}}public RenderMode renderMode;public Camera worldCamera;}
    public static class RectTransformUtility
    {public static bool ScreenPointToLocalPointInRectangle(RectTransform parent,Vector2 screen,Camera camera,out Vector2 local){local=screen;return true;}}
    public static partial class Physics
    {
        public static Vector3 LastRayOrigin,LastRayDirection;
        public static bool Raycast(Vector3 p,Vector3 d,out RaycastHit hit,float length,int mask,QueryTriggerInteraction q){hit=new RaycastHit();return false;}
    }
}
namespace HutongGames.PlayMaker
{
    public class FsmBool {public bool Value;}
    public class FsmString {public string Value;}
}
namespace HutongGames.PlayMaker.Actions
{
    public class ActivateGameObject:HutongGames.PlayMaker.FsmStateAction
    {public HutongGames.PlayMaker.FsmBool activate;public HutongGames.PlayMaker.Fsm Fsm;public HutongGames.PlayMaker.FsmOwnerDefault gameObject;}
    public class GetButtonDown:HutongGames.PlayMaker.FsmStateAction
    {public HutongGames.PlayMaker.Fsm Fsm;public HutongGames.PlayMaker.FsmString buttonName;}
}
namespace InsaneSystems.InputManager
{public static class InputController{public static bool GetKeyActionIsDown(string action){return UnityEngine.Input.ChangeCamera;}}}
namespace Apocaplayer
{
    public static class Plugin
    {
        public static BepInEx.Configuration.ConfigEntry<bool> Enabled=Bool(true),ThirdPersonOnFoot=Bool(true),
            EnableMMB=Bool(false),ToggleMiddleMouse=Bool(false),DynamicCrosshair=Bool(true),OcclusionPrototype=Bool(false),OcclusionInVehicle=Bool(false);
        public static BepInEx.Configuration.ConfigEntry<UnityEngine.KeyCode> ObserveKey=new BepInEx.Configuration.ConfigEntry<UnityEngine.KeyCode>(UnityEngine.KeyCode.LeftAlt);
        public static BepInEx.Configuration.ConfigEntry<float> ThirdCarHeight=Float(.5f),ThirdHeight=Float(.1f),ThirdShoulder=Float(.3f),ThirdDistance=Float(2.4f),ThirdCarDistance=Float(6f),OrbitSpeed=Float(3f),CarCameraForward=Float(.15f);
        public static ApocaChaseCamera.TestLog Log=new ApocaChaseCamera.TestLog{ThrowWarnings=false};
        private static BepInEx.Configuration.ConfigEntry<bool> Bool(bool v){return new BepInEx.Configuration.ConfigEntry<bool>(v);}
        private static BepInEx.Configuration.ConfigEntry<float> Float(float v){return new BepInEx.Configuration.ConfigEntry<float>(v);}
        public static void Verbose(string message){}
    }
    public static class Game
    {
        public static bool Ready {get{return true;}}
        public static bool InCar {get{return InCarFlag;}}
        public static bool Dead {get{return DeadFlag;}}
        public static bool Paused {get{return UnityEngine.Time.timeScale<.01f;}}
        public static bool FirstPersonCameraOn {get{return Cam!=null&&Cam.isActiveAndEnabled;}}
        public static string DrawnWeapon {get{return Weapon;}}
        public static bool InCarFlag=true,DeadFlag,Binoculars,AimDownSights,CaveFlag;
        public static string Weapon="";
        public static UnityEngine.GameObject Player;
        public static UnityEngine.Transform PlayerCamera,CameraHolder,CarRoot;
        public static UnityEngine.Camera Cam;
    }
    public static class Props{public enum Kind{None,Rifle,Pistol}public static Kind KindOf(string w){return string.IsNullOrEmpty(w)?Kind.None:Kind.Rifle;}}
    public static class Perf{public static long Now{get{return 0;}}public const int Camera=0;public static void Add(int slot,long elapsed){}}
    public static class Runner{public static bool RagdollVisible;public static UnityEngine.Vector3 DeathFocus;}
    public static class Cave{public static bool Inside;public static void Tick(bool want){Inside=want&&Game.CaveFlag;}}
    public static class OcclusionCutaway
    {
        public static bool Available {get{return ShaderReady;}}public static bool ShaderReady=true;
        public static int Calls,Stops,Creates;public static bool Active;
        public static void Prepare(UnityEngine.Camera c,UnityEngine.Vector3 p,UnityEngine.Quaternion r,UnityEngine.Transform car){Calls++;if(!Active){Creates++;Active=true;}}
        public static void Stop(){Stops++;Active=false;}
    }
    public static class ModAPI
    {public static bool PlayerTraversalActive,PlayerTraversalCameraActive;public static bool TryPlayerTraversalEye(out UnityEngine.Vector3 eye){eye=new UnityEngine.Vector3();return false;}}
    public class ClimbingController{public static ClimbingController Instance;public bool TryJumpClimb(){return false;}}
}
#endif
