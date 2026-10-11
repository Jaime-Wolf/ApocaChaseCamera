using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

// Handwritten origin-event doubles. They model a shared translation only;
// the game's implementations are inspected separately and not copied here.
internal static class OriginShiftFixture
{
    internal static void Move(Vector3 delta)
    {
        foreach (GameObject item in new List<GameObject>(GameObject.Registry.Values))
            if (item != null && item.transform.parent == null) item.transform.position += delta;
    }
}
public class FloatingOrigin
{
    public Vector3 Delta;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LateUpdate() { OriginShiftFixture.Move(Delta); }
}
public class FloatingOriginAdvanced
{
    public Vector3 Delta;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LateUpdate() { OriginShiftFixture.Move(Delta); }
}
public class FloatingOriginNew
{
    public Vector3 Delta;
    [MethodImpl(MethodImplOptions.NoInlining)]
    public void LateUpdate() { OriginShiftFixture.Move(Delta); }
}
