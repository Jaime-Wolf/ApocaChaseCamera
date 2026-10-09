using System;
using System.Collections.Generic;
using System.Reflection;

namespace BepInEx.Configuration { public class ConfigEntry<T> { public T Value; public ConfigEntry(T value) { Value=value; } } }
namespace Apocasetter
{
    public static class GameMenu { public static bool InGame=true, Paused; }
    public static class InputBlocker { public static bool Active; }
}
namespace HutongGames.PlayMaker
{
    public class FsmFloat { public float Value=100f; public string Name; public bool IsNone; }
    public class FsmOwnerDefault { public UnityEngine.GameObject Target; }
    public class FsmStateAction { public bool Enabled=true; public virtual void OnUpdate(){} public virtual void OnLateUpdate(){} }
    public class FsmState { public FsmStateAction[] Actions; }
    public class Fsm
    {
        public string Name, GameObjectName;
        public bool Initialized=true; public FsmState CompassState; public System.Collections.Generic.Dictionary<string,FsmState> States=new System.Collections.Generic.Dictionary<string,FsmState>();
        public FsmState GetState(string name){FsmState state;return name=="compass fps" ? CompassState : States.TryGetValue(name,out state)?state:null;}
        public UnityEngine.GameObject GetOwnerDefaultTarget(FsmOwnerDefault owner){return owner.Target;}
    }
    public class FsmVariables { public static int Searches; public FsmFloat Health=new FsmFloat(); public FsmFloat FindFsmFloat(string name) { Searches++; return Health; } }
}
// Small handwritten action doubles exercise the adapter contract. Game action
// implementations are loaded only from the installed game in production.
namespace HutongGames.PlayMaker.Actions
{
    public class MouseLook : HutongGames.PlayMaker.FsmStateAction
    {
        public enum RotationAxes {MouseXAndY,MouseX,MouseY}
        public RotationAxes axes; public HutongGames.PlayMaker.FsmOwnerDefault gameObject;
        private float rotationX,rotationY;
        public float CachedYaw {get{return rotationX;}} public float CachedPitch {get{return rotationY;}}
        public void Seed(float x,float y){rotationX=x;rotationY=y;}
        // Minimal input-cache double; not the game's implementation.
        public override void OnUpdate()
        {
            var t=gameObject.Target.transform;var e=t.localEulerAngles;
            if((int)axes!=2){rotationX+=UnityEngine.Input.X*2;e.y=rotationX;}
            // Native MouseY negates the incoming delta, not the stored angle.
            if((int)axes!=1){rotationY+=UnityEngine.Input.Y*2*((int)axes==2?-1:1);e.x=rotationY;}
            t.localRotation=UnityEngine.Quaternion.Euler(e.x,e.y,0);
        }
    }
    public class GetRotation : HutongGames.PlayMaker.FsmStateAction
    { public HutongGames.PlayMaker.FsmFloat yAngle; public UnityEngine.Space space; }
    public class FloatMultiply : HutongGames.PlayMaker.FsmStateAction
    {
        public HutongGames.PlayMaker.FsmFloat floatVariable;
        public float Factor=-1f; public static int Calls;
        public override void OnUpdate(){Calls++;floatVariable.Value*=Factor;}
    }
    public class SetRotation : HutongGames.PlayMaker.FsmStateAction
    {
        public HutongGames.PlayMaker.FsmFloat zAngle; public HutongGames.PlayMaker.FsmOwnerDefault gameObject;
        public UnityEngine.Space space; public bool lateUpdate; public static int Calls;
        public override void OnUpdate(){if(!lateUpdate)Draw();}
        public override void OnLateUpdate(){if(lateUpdate)Draw();}
        private void Draw(){Calls++;UnityEngine.Vector3 e=gameObject.Target.transform.eulerAngles;
            gameObject.Target.transform.rotation=UnityEngine.Quaternion.Euler(e.x,e.y,zAngle.Value);}
    }
}
public class PlayMakerFSM : UnityEngine.Component
{
    public string FsmName, ActiveStateName; public bool enabled=true;
    public HutongGames.PlayMaker.Fsm Fsm=new HutongGames.PlayMaker.Fsm();
    public HutongGames.PlayMaker.FsmVariables FsmVariables=new HutongGames.PlayMaker.FsmVariables();
}
namespace UnityEngine
{
    public enum Space {World,Self}
    public class Component : Object
    {
        public GameObject gameObject; public Transform transform { get { return gameObject.transform; } }
        public T GetComponent<T>() where T:Component {return gameObject.GetComponent<T>();}
        public T GetComponentInParent<T>() where T:Component {for(Transform t=transform;t!=null;t=t.parent){T c=t.GetComponent<T>();if(c!=null)return c;}return null;}
        public T[] GetComponents<T>() where T:Component {return gameObject.GetComponents<T>();}
        public T[] GetComponentsInChildren<T>(bool inactive) where T:Component {return gameObject.GetComponentsInChildren<T>(inactive);}
        public void GetComponentsInChildren<T>(bool inactive,List<T> result) where T:Component {gameObject.GetComponentsInChildren(inactive,result);}
    }
    public class GameObject : Object
    {
        public static Dictionary<string,GameObject> Registry=new Dictionary<string,GameObject>();
        public static int HierarchyArrayScans,HierarchyListScans,FindCalls;
        public string name; public int layer; public bool activeSelf=true; public Transform transform;
        #if REAL_APOCAPLAYER
        public Scene scene;
        #endif
        private List<Component> components=new List<Component>();
        public bool activeInHierarchy { get { return !destroyed && activeSelf && (transform.parent==null || transform.parent.gameObject.activeInHierarchy); } }
        public GameObject(string name,params Type[] types)
        {
            this.name=name;transform=new Transform(this);Registry[name]=this;
            foreach(Type type in types)if(type==typeof(RectTransform))transform=new RectTransform(this);
            components.Add(transform);
        }
        public static GameObject Find(string name) { FindCalls++; GameObject go; return Registry.TryGetValue(name,out go) && go.activeInHierarchy ? go : null; }
        public T Add<T>(T value) where T:Component { value.gameObject=this; components.Add(value); return value; }
        public T AddComponent<T>() where T:Component,new(){return Add(new T());}
        public void SetActive(bool active){activeSelf=active;}
        public T GetComponent<T>() where T:Component { foreach(Component c in components) if(c is T) return (T)c; return null; }
        public T[] GetComponents<T>() where T:Component { List<T> values=new List<T>(); foreach(Component c in components) if(c is T) values.Add((T)c); return values.ToArray(); }
        public T[] GetComponentsInChildren<T>(bool inactive) where T:Component
        {
            HierarchyArrayScans++;List<T> result=new List<T>();Collect(inactive,result);return result.ToArray();
        }
        public void GetComponentsInChildren<T>(bool inactive,List<T> result) where T:Component
        {HierarchyListScans++;result.Clear();Collect(inactive,result);}
        private void Collect<T>(bool inactive,List<T> result) where T:Component
        {if(destroyed||(!inactive&&!activeInHierarchy))return;foreach(Component component in components)if(component is T&&!component.destroyed)result.Add((T)component);foreach(Transform child in transform.children)child.gameObject.Collect(inactive,result);}
        internal void DestroyComponents(){foreach(Component component in components)component.destroyed=true;}
        internal void Remove(Component component){components.Remove(component);}
    }
    public class Transform : Component
    {
        public static int FindCalls, ChildComponentQueries;
        public Transform parent; public List<Transform> children=new List<Transform>();
        public Transform root {get{Transform value=this;while(value.parent!=null)value=value.parent;return value;}}
        public int childCount {get{return children.Count;}}
        public Transform GetChild(int index){return children[index];}
        public Vector3 localPosition; public Quaternion rotation=Quaternion.Euler(0,0,0);
        public Vector3 eulerAngles {get{return rotation.euler;}}
        public Quaternion localRotation {get{return parent==null?rotation:Quaternion.Inverse(parent.rotation)*rotation;}set{rotation=parent==null?value:parent.rotation*value;}}
        public Vector3 localEulerAngles {get{return localRotation.eulerAngles;}}
        public string name { get { return gameObject.name; } }
        public Transform(GameObject go) { gameObject=go; }
        public virtual Vector3 position { get { return parent==null ? localPosition : parent.TransformPoint(localPosition); } set { localPosition=parent==null ? value : parent.InverseTransformPoint(value); } }
        public Vector3 forward { get { return rotation * new Vector3(0,0,1); } }
        public void Parent(Transform value) { if(parent!=null)parent.children.Remove(this); parent=value; if(value!=null)value.children.Add(this); }
        public void SetParent(Transform value,bool stays){Parent(value);}
        public Vector3 TransformPoint(Vector3 value) { return position + rotation*value; }
        public Vector3 InverseTransformPoint(Vector3 value) { return Quaternion.Inverse(rotation)*(value-position); }
        public Transform Find(string path) { FindCalls++;string[] p=path.Split('/'); Transform node=this; foreach(string name in p) { Transform next=null; foreach(Transform child in node.children)if(child.name==name){next=child;break;} if(next==null)return null; node=next; }return node; }
        public bool IsChildOf(Transform other) { for(Transform t=this;t!=null;t=t.parent)if(t==other)return true;return false; }
        public T GetComponentInChildren<T>(bool inactive) where T:Component { ChildComponentQueries++;T result=GetComponent<T>();if(result!=null)return result;foreach(Transform c in children){if(!inactive&&!c.gameObject.activeInHierarchy)continue;result=c.GetComponentInChildren<T>(inactive);if(result!=null)return result;}return null; }
    }
    public partial struct Vector3
    {
        public float x,y,z; public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
        public static Vector3 up {get{return new Vector3(0,1,0);}} public static Vector3 one {get{return new Vector3(1,1,1);}}
        public float sqrMagnitude {get{return x*x+y*y+z*z;}} public float magnitude {get{return (float)Math.Sqrt(sqrMagnitude);}}
        public static Vector3 operator +(Vector3 a,Vector3 b){return new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
        public static Vector3 operator -(Vector3 a,Vector3 b){return new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);}
        public static Vector3 operator *(Vector3 a,float b){return new Vector3(a.x*b,a.y*b,a.z*b);}
        public static Vector3 operator /(Vector3 a,float b){return a*(1/b);}
        public static float Distance(Vector3 a,Vector3 b){return (a-b).magnitude;}
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n){return a-n*(a.x*n.x+a.y*n.y+a.z*n.z);}
    }
    public partial struct Vector2
    {
        public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero {get{return new Vector2(0,0);}}
    }
    public struct Vector4 {public float x,y,z,w;public Vector4(float a,float b,float c,float d){x=a;y=b;z=c;w=d;}}
    public class Texture : Object {}
    public class Texture2D:Texture {public int width=512,height=512;}
    public enum SpriteMeshType {FullRect}
    public class Sprite : Object
    {
        public Texture2D texture;public Rect rect;public Vector4 border;
        public static Sprite Create(Texture2D texture,Rect rect,Vector2 pivot,float ppu,uint extrude,SpriteMeshType mesh,Vector4 border)
        {return new Sprite{texture=texture,rect=rect,border=border};}
    }
    public struct Color
    {
        public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}
    }
    public struct Rect
    {
        public float xMin,xMax,yMin,yMax;public Rect(float left,float bottom,float width,float height){xMin=left;yMin=bottom;xMax=left+width;yMax=bottom+height;}
    }
    public class RectTransform : Transform
    {
        public static int WorldCornerQueries;
        public Vector2 anchorMin=new Vector2(.5f,.5f),anchorMax=new Vector2(.5f,.5f),pivot=new Vector2(.5f,.5f),anchoredPosition,sizeDelta,offsetMin,offsetMax;
        public RectTransform(GameObject go):base(go){}
        public Rect rect
        {
            get {RectTransform p=parent as RectTransform;float width=sizeDelta.x,height=sizeDelta.y;
                if(p!=null){Rect r=p.rect;width+=(r.xMax-r.xMin)*(anchorMax.x-anchorMin.x)+offsetMax.x-offsetMin.x;height+=(r.yMax-r.yMin)*(anchorMax.y-anchorMin.y)+offsetMax.y-offsetMin.y;}
                return new Rect(-width*pivot.x,-height*pivot.y,width,height);}
        }
        public override Vector3 position
        {
            get {RectTransform p=parent as RectTransform;if(p==null)return localPosition;Rect r=p.rect;
                return p.TransformPoint(new Vector3(r.xMin+(r.xMax-r.xMin)*(anchorMin.x+(anchorMax.x-anchorMin.x)*pivot.x)+anchoredPosition.x,
                    r.yMin+(r.yMax-r.yMin)*(anchorMin.y+(anchorMax.y-anchorMin.y)*pivot.y)+anchoredPosition.y,0));}
            set {base.position=value;}
        }
        public void GetWorldCorners(Vector3[] corners){WorldCornerQueries++;Rect r=rect;corners[0]=TransformPoint(new Vector3(r.xMin,r.yMin,0));corners[1]=TransformPoint(new Vector3(r.xMin,r.yMax,0));corners[2]=TransformPoint(new Vector3(r.xMax,r.yMax,0));corners[3]=TransformPoint(new Vector3(r.xMax,r.yMin,0));}
    }
    // Model Unity's destroyed-object null behavior for lifecycle regressions.
    public class Object
    {
        public bool destroyed;
        public static bool operator ==(Object a,Object b){bool an=ReferenceEquals(a,null)||(!ReferenceEquals(a,null)&&a.destroyed),bn=ReferenceEquals(b,null)||(!ReferenceEquals(b,null)&&b.destroyed);return an||bn?an&&bn:ReferenceEquals(a,b);}
        public static bool operator !=(Object a,Object b){return !(a==b);}
        public static implicit operator bool(Object value){return value!=null;}
        public override bool Equals(object value){return ReferenceEquals(this,value);}
        public override int GetHashCode(){return base.GetHashCode();}
        public static void Destroy(Object value)
        {
            if(value==null)return;GameObject go=value as GameObject;
            if(go!=null){foreach(Transform child in go.transform.children.ToArray())Destroy(child.gameObject);go.SetActive(false);if(go.transform.parent!=null)go.transform.Parent(null);GameObject current;if(GameObject.Registry.TryGetValue(go.name,out current)&&ReferenceEquals(current,go))GameObject.Registry.Remove(go.name);go.DestroyComponents();}
            Component component=value as Component;if(component!=null)component.gameObject.Remove(component);value.destroyed=true;
        }
    }
    public class Font : Object {}
    public enum FontStyle {Normal,Bold}
    public enum TextAnchor {MiddleCenter}
    public partial struct Quaternion
    {
        public float x,y,z,w; public Vector3 euler;
        public static Quaternion Euler(float x,float y,float z)
        {
            double a=x*Math.PI/360,b=y*Math.PI/360,c=z*Math.PI/360;
            return new Quaternion {x=(float)(Math.Cos(b)*Math.Sin(a)*Math.Cos(c)+Math.Sin(b)*Math.Cos(a)*Math.Sin(c)),
                y=(float)(Math.Sin(b)*Math.Cos(a)*Math.Cos(c)-Math.Cos(b)*Math.Sin(a)*Math.Sin(c)),
                z=(float)(Math.Cos(b)*Math.Cos(a)*Math.Sin(c)-Math.Sin(b)*Math.Sin(a)*Math.Cos(c)),
                w=(float)(Math.Cos(b)*Math.Cos(a)*Math.Cos(c)+Math.Sin(b)*Math.Sin(a)*Math.Sin(c)), euler=new Vector3(x,y,z)};
        }
        public Vector3 eulerAngles {get{
            double sx=Math.Max(-1,Math.Min(1,2*(w*x-y*z)));
            return new Vector3((float)(Math.Asin(sx)*180/Math.PI),(float)(Math.Atan2(2*(w*y+x*z),1-2*(x*x+y*y))*180/Math.PI),(float)(Math.Atan2(2*(w*z+x*y),1-2*(x*x+z*z))*180/Math.PI));
        }}
        public static Quaternion operator *(Quaternion a,Quaternion b)
        {
            var q=new Quaternion{x=a.w*b.x+a.x*b.w+a.y*b.z-a.z*b.y,y=a.w*b.y-a.x*b.z+a.y*b.w+a.z*b.x,z=a.w*b.z+a.x*b.y-a.y*b.x+a.z*b.w,w=a.w*b.w-a.x*b.x-a.y*b.y-a.z*b.z};
            q.euler=q.eulerAngles;return q;
        }
        public static Quaternion Inverse(Quaternion a){return new Quaternion{x=-a.x,y=-a.y,z=-a.z,w=a.w};}
        public static Vector3 operator *(Quaternion q,Vector3 v)
        {
            Vector3 u=new Vector3(q.x,q.y,q.z);
            float dot=u.x*v.x+u.y*v.y+u.z*v.z;
            Vector3 cross=new Vector3(u.y*v.z-u.z*v.y,u.z*v.x-u.x*v.z,u.x*v.y-u.y*v.x);
            return u*(2*dot)+v*(q.w*q.w-u.sqrMagnitude)+cross*(2*q.w);
        }
    }
    // A matrix token tracks ownership/restoration; numerical render-matrix
    // multiplication is Unity's responsibility and is not simulated here.
    public partial struct Matrix4x4
    {
        #if REAL_APOCAPLAYER
        internal System.Numerics.Matrix4x4 numeric;
        #endif
        public int token; public Matrix4x4(int token){this.token=token;
            #if REAL_APOCAPLAYER
            numeric=System.Numerics.Matrix4x4.Identity;
            #endif
        }
        public Matrix4x4 inverse {get{
            #if REAL_APOCAPLAYER
            System.Numerics.Matrix4x4 v;System.Numerics.Matrix4x4.Invert(numeric,out v);return Numeric(v);
            #else
            return new Matrix4x4(-token);
            #endif
        }}
        public static Matrix4x4 Scale(Vector3 a){
            #if REAL_APOCAPLAYER
            return Numeric(System.Numerics.Matrix4x4.CreateScale(a.x,a.y,a.z));
            #else
            return new Matrix4x4(10);
            #endif
        }
        public static int TrsCalls;
        public static Matrix4x4 TRS(Vector3 p,Quaternion r,Vector3 s){TrsCalls++;
            #if REAL_APOCAPLAYER
            return Numeric(System.Numerics.Matrix4x4.CreateScale(s.x,s.y,s.z)*System.Numerics.Matrix4x4.CreateFromQuaternion(new System.Numerics.Quaternion(r.x,r.y,r.z,r.w))*System.Numerics.Matrix4x4.CreateTranslation(p.x,p.y,p.z));
            #else
            return new Matrix4x4(20+(int)Math.Round(p.x*1000f)+(int)Math.Round(p.y*100f)+(int)Math.Round(p.z*10f));
            #endif
        }
        public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b){
            #if REAL_APOCAPLAYER
            return Numeric(b.numeric*a.numeric);
            #else
            return new Matrix4x4(a.token*31+b.token);
            #endif
        }
    }
    public partial class Camera : Component
    {
        public bool enabled=true; public bool isActiveAndEnabled {get{return enabled&&gameObject.activeInHierarchy;}}
        public float fieldOfView=60,aspect=1.777f,nearClipPlane=0.3f;
        public Matrix4x4 worldToCameraMatrix=new Matrix4x4(101),cullingMatrix=new Matrix4x4(202),projectionMatrix=new Matrix4x4(303);
        public int ViewResets, CullResets;
        public void ResetWorldToCameraMatrix(){ViewResets++;worldToCameraMatrix=new Matrix4x4(101);}
        public void ResetCullingMatrix(){CullResets++;cullingMatrix=new Matrix4x4(202);}
        #if !REAL_APOCAPLAYER
        public void ResetProjectionMatrix(){projectionMatrix=new Matrix4x4(303);}
        #endif
    }
    public struct Bounds {public void Expand(float amount){} public bool Intersects(Bounds other){return true;}}
    public class Rigidbody : Component { }
    public class LODGroup : Component { }
    public class Renderer : Component {public bool enabled=true,forceRenderingOff; public Bounds bounds;}
    public class TerrainCollider : Collider {}
    public class Collider : Component {public string name {get{return gameObject.name;}}public Rigidbody attachedRigidbody;public Bounds bounds;}
    public struct RaycastHit {public Collider collider; public float distance; public Vector3 normal,point;}
    public class TerrainData { public Vector3 size=new Vector3(1000,1000,1000); }
    public class Terrain : Component
    {
        public static Terrain[] activeTerrains=new Terrain[0];
        public TerrainData terrainData=new TerrainData();
        public Func<Vector3,float> Height;
        public float SampleHeight(Vector3 point){return Height(point)-transform.position.y;}
    }
    public enum QueryTriggerInteraction {Ignore,UseGlobal}
    public static partial class Physics
    {
        public const int DefaultRaycastLayers=-5;
        public static RaycastHit[] Hits=new RaycastHit[0]; public static Collider[] Overlaps=new Collider[0];
        public static Func<Vector3,RaycastHit[]> GroundProbe;
        public static int AllocatingQueries,BufferedQueries;
        public static int SphereCastNonAlloc(Vector3 p,float r,Vector3 d,RaycastHit[] buffer,float l,int m,QueryTriggerInteraction q){BufferedQueries++;int count=Math.Min(Hits.Length,buffer.Length);Array.Copy(Hits,buffer,count);return count;}
        public static int RaycastNonAlloc(Vector3 p,Vector3 d,RaycastHit[] buffer,float l,int m,QueryTriggerInteraction q){BufferedQueries++;
            #if REAL_APOCAPLAYER
            LastRayOrigin=p;LastRayDirection=d;
            #endif
            RaycastHit[] hits=GroundProbe==null?new RaycastHit[0]:GroundProbe(p);int count=Math.Min(hits.Length,buffer.Length);Array.Copy(hits,buffer,count);return count;}
        public static int OverlapSphereNonAlloc(Vector3 p,float r,Collider[] buffer,int m,QueryTriggerInteraction q){BufferedQueries++;int count=Math.Min(Overlaps.Length,buffer.Length);Array.Copy(Overlaps,buffer,count);return count;}
        public static RaycastHit[] SphereCastAll(Vector3 p,float r,Vector3 d,float l,int m,QueryTriggerInteraction q){AllocatingQueries++;return Hits;}
        public static Collider[] OverlapSphere(Vector3 p,float r,int m,QueryTriggerInteraction q){AllocatingQueries++;return Overlaps;}
        public static RaycastHit[] RaycastAll(Vector3 p,Vector3 d,float length,int m,QueryTriggerInteraction q)
        {AllocatingQueries++;return GroundProbe==null?new RaycastHit[0]:GroundProbe(p);}
    }
    public static partial class Mathf
    {
        public const float Rad2Deg=57.2957795f,Deg2Rad=0.0174532925f;
        public static float Min(float a,float b){return Math.Min(a,b);}public static float Max(float a,float b){return Math.Max(a,b);}
        public static int Max(int a,int b){return Math.Max(a,b);}public static int RoundToInt(float value){return (int)Math.Round(value);}
        public static float Atan2(float a,float b){return (float)Math.Atan2(a,b);}public static float Tan(float a){return (float)Math.Tan(a);}public static float Sqrt(float a){return (float)Math.Sqrt(a);}
    }
    public static partial class Time {public static float unscaledTime,timeScale=1;public static int frameCount;}
    public static class Application {public static bool isFocused=true;}
    public static partial class Input {public static float X,Y;public static Vector2 mouseScrollDelta;public static float GetAxisRaw(string name){return name=="Mouse X"?X:Y;}}
}
namespace UnityEngine.UI
{
    public class Graphic:UnityEngine.Component
    {
        public bool enabled=true,raycastTarget=true;public UnityEngine.Color color=new UnityEngine.Color(1,1,1,1);
        public UnityEngine.RectTransform rectTransform {get{return GetComponent<UnityEngine.RectTransform>();}}
    }
    public enum HorizontalWrapMode {Overflow}
    public enum VerticalWrapMode {Truncate}
    public class Text:Graphic {public UnityEngine.Font font;public UnityEngine.FontStyle fontStyle;public int fontSize;public UnityEngine.TextAnchor alignment;public bool supportRichText;public HorizontalWrapMode horizontalOverflow;public VerticalWrapMode verticalOverflow;public string text="";}
    public class Image:Graphic {public enum Type{Simple,Sliced}public Sprite sprite;public Type type;}
    public class MaskableGraphic:Graphic {public void SetVerticesDirty(){} protected virtual void OnPopulateMesh(VertexHelper mesh){} }
    public class VertexHelper
    {
        public List<UnityEngine.Vector3> vertices=new List<UnityEngine.Vector3>();public List<int> triangles=new List<int>();public List<UnityEngine.Color> colors=new List<UnityEngine.Color>();
        public int currentVertCount {get{return vertices.Count;}}
        public void Clear(){vertices.Clear();triangles.Clear();colors.Clear();}
        public void AddVert(UnityEngine.Vector3 position,UnityEngine.Color color,UnityEngine.Vector2 uv){vertices.Add(position);colors.Add(color);}
        public void AddTriangle(int a,int b,int c){triangles.Add(a);triangles.Add(b);triangles.Add(c);}
    }
    public class RawImage:Graphic {public UnityEngine.Texture texture;}
    public class Shadow:UnityEngine.Component {public UnityEngine.Color effectColor;public UnityEngine.Vector2 effectDistance;public bool useGraphicAlpha;}
    public class Outline:Shadow {}
}
namespace NWH.VehiclePhysics2
{
    public class VehicleController:UnityEngine.Component {public float SpeedSigned;public Powertrain.Powertrain powertrain=new Powertrain.Powertrain();}
}
namespace NWH.VehiclePhysics2.Powertrain
{
    public class Powertrain {public EngineComponent engine=new EngineComponent();public TransmissionComponent transmission=new TransmissionComponent();}
    public class EngineComponent {public float OutputRPM,revLimiterRPM=6000;}
    public class TransmissionComponent {public int Gear;public List<float> gears=new List<float>{-2,0,4,3,2,1,.8f};}
}
#if !REAL_HARMONY
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static bool FindTypes;
        public static int TypeSearches;public static Type TypeByName(string name){TypeSearches++;return FindTypes?Assembly.GetExecutingAssembly().GetType(name):null;}
        public static FieldInfo Field(Type t,string name){return t.GetField(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);}
        public static PropertyInfo Property(Type t,string name){return t.GetProperty(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);}
        public static MethodInfo Method(Type t,string name,Type[] args){return t.GetMethod(name,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static,null,args,null);}
    }
    public class HarmonyMethod {public HarmonyMethod(Type t,string name) {}}
    public class Harmony {public Harmony(string id){}public void Patch(MethodInfo method,HarmonyMethod prefix=null,HarmonyMethod postfix=null){}public void UnpatchSelf(){}}
}
#endif
#if !REAL_APOCAPLAYER
namespace Apocaplayer
{
    #pragma warning disable 0414, 0169
    public static class ThirdPerson
    {
        public static bool On,Peek,AimZoom,HasView; public static float ViewFov;
        public static UnityEngine.Vector3 ViewPos; public static UnityEngine.Quaternion ViewRot;
        public static bool Orbiting;
        private static UnityEngine.Vector3 _vPos;
        private static UnityEngine.Quaternion _vRot;
        private static UnityEngine.Transform _vCar;
        private static float _vFov,_gameFov,_orbitYaw,_orbitPitch,_dist;
        private static int _viewFrame=-1;
        private static bool _vCutaway,_projSet,_fovSet;
        private static void ComputeView(UnityEngine.Camera camera){_vRot=camera.transform.rotation;_viewFrame=UnityEngine.Time.frameCount;}
        private static void Zoom(){}
        public static void Tick(){}
        private static void PreCull(UnityEngine.Camera camera){}
    }
    #pragma warning restore 0414, 0169
    public static class Game
    {
        public static UnityEngine.Camera Cam;
        public static string Weapon=""; public static string DrawnWeapon {get{return Weapon;}}
        public static bool Ready {get{return true;}}public static bool Dead {get{return false;}}
        public static bool InCar {get{return ApocaChaseCamera.CameraBinding.Car!=null;}}
    }
    public static class Plugin
    {
        public static BepInEx.Configuration.ConfigEntry<bool> Enabled=new BepInEx.Configuration.ConfigEntry<bool>(true),
            OcclusionPrototype=new BepInEx.Configuration.ConfigEntry<bool>(false),OcclusionInVehicle=new BepInEx.Configuration.ConfigEntry<bool>(false);
        public static BepInEx.Configuration.ConfigEntry<float> ThirdCarHeight=new BepInEx.Configuration.ConfigEntry<float>(.5f);
    }
    public static class Cave {public static bool Inside;}
    public static class OcclusionCutaway
    {
        public static int Calls;public static bool Available {get{return true;}}
        public static void Prepare(UnityEngine.Camera c,UnityEngine.Vector3 p,UnityEngine.Quaternion r,UnityEngine.Transform t){Calls++;}
    }
}
#endif
namespace ApocaChaseCamera
{
    public class TestLog {public int Warnings;public bool ThrowWarnings=true;public void LogInfo(string value){}public void LogWarning(string value){Warnings++;if(ThrowWarnings)throw new Exception(value);}}
    internal static class Plugin
    {
        public const string GUID="test";public static TestLog Log=new TestLog();
        public static BepInEx.Configuration.ConfigEntry<bool> Enabled=new BepInEx.Configuration.ConfigEntry<bool>(true), Recenter=new BepInEx.Configuration.ConfigEntry<bool>(true);
        public static BepInEx.Configuration.ConfigEntry<bool> HudEnabled=new BepInEx.Configuration.ConfigEntry<bool>(true);
        public static BepInEx.Configuration.ConfigEntry<float> HudScale=Float(1),HudGap=Float(10);
        public static BepInEx.Configuration.ConfigEntry<float> Height=Float(1.7f),Distance=Float(6.5f),Side=Float(0),Pitch=Float(8),BumpTime=Float(.35f),TurnTime=Float(.25f),RecenterDelay=Float(1.5f),RecenterTime=Float(.7f),Sensitivity=Float(2),RecoveryTime=Float(.45f),MaxHeightLag=Float(.6f),GroundClearance=Float(.6f);
        private static BepInEx.Configuration.ConfigEntry<float> Float(float value){return new BepInEx.Configuration.ConfigEntry<float>(value);}
    }
}

