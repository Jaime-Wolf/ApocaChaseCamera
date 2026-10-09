using System;
using UnityEngine;
namespace ApocaChaseCamera
{
    internal static class CameraQueries
    {
        private const int MaxBufferSize=4096;
        private static RaycastHit[] sweep=new RaycastHit[128],ray=new RaycastHit[128];
        private static Collider[] overlap=new Collider[128];
        private static int sweepUsed,rayUsed,overlapUsed;
        internal static RaycastHit[] Sweep(Vector3 origin,float radius,Vector3 direction,float length,out int count)
        {
            for(;;)
            {
                count=Physics.SphereCastNonAlloc(origin,radius,direction,sweep,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                sweepUsed=Math.Max(sweepUsed,count);
                if(count<sweep.Length)return sweep;
                if(sweep.Length>=MaxBufferSize)break;
                sweep=new RaycastHit[Math.Min(MaxBufferSize,sweep.Length*2)];sweepUsed=0;
            }
            // A full buffer may omit a closer wall behind the truck's own parts.
            RaycastHit[] complete=Physics.SphereCastAll(origin,radius,direction,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            count=complete.Length;return complete;
        }
        internal static RaycastHit[] Ray(Vector3 origin,Vector3 direction,float length,out int count)
        {
            for(;;)
            {
                count=Physics.RaycastNonAlloc(origin,direction,ray,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                rayUsed=Math.Max(rayUsed,count);
                if(count<ray.Length)return ray;
                if(ray.Length>=MaxBufferSize)break;
                ray=new RaycastHit[Math.Min(MaxBufferSize,ray.Length*2)];rayUsed=0;
            }
            RaycastHit[] complete=Physics.RaycastAll(origin,direction,length,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            count=complete.Length;return complete;
        }
        internal static Collider[] Overlap(Vector3 point,float radius,out int count)
        {
            for(;;)
            {
                count=Physics.OverlapSphereNonAlloc(point,radius,overlap,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                overlapUsed=Math.Max(overlapUsed,count);
                if(count<overlap.Length)return overlap;
                if(overlap.Length>=MaxBufferSize)break;
                overlap=new Collider[Math.Min(MaxBufferSize,overlap.Length*2)];overlapUsed=0;
            }
            Collider[] complete=Physics.OverlapSphere(point,radius,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            count=complete.Length;return complete;
        }
        internal static void Clear()
        {
            // Retain capacity across vehicles while releasing collider refs.
            // Repeated resets in an inactive view have no populated range.
            if(sweepUsed>0)Array.Clear(sweep,0,sweepUsed);
            if(rayUsed>0)Array.Clear(ray,0,rayUsed);
            if(overlapUsed>0)Array.Clear(overlap,0,overlapUsed);
            sweepUsed=rayUsed=overlapUsed=0;
        }
    }
}
