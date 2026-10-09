using System;
using System.Reflection;
using UnityEngine;

namespace ApocaChaseCamera
{
    // These execute production camera code against the declared Unity doubles.
    // PhysX geometry, numerical render matrices and frame times still require
    // a real-game drive; the matrix token double must depend on position.
    internal static class RuntimeRegressionChecks
    {
        private static int checks;
        private static GameObject car;
        private static Camera camera;

        private static void Check(bool value, string why)
        { checks++; if (!value) throw new Exception("Runtime regression: " + why); }

        private static T Field<T>(string name)
        { return (T)typeof(ChaseView).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null); }

        private static GameObject Child(string name, GameObject parent)
        { GameObject result = new GameObject(name); result.transform.Parent(parent.transform); return result; }

        private static void Frame(float dt)
        { Time.unscaledTime += dt; Time.frameCount++; ChaseView.Tick(); }

        private static void Render()
        { ChaseView.PreCull(camera); }

        private static void Finish()
        { ChaseView.PostRender(camera); }

        private static void Setup(string name)
        {
            ChaseView.Reset(); CameraBinding.Reset(); GameObject.Registry.Clear();
            car = new GameObject(name);
            GameObject drive = Child("DriveTrigger", car);
            GameObject third = Child("3rdCamera", drive);
            third.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            camera = Child("Camera", third).Add(new Camera());
            GameObject player = Child("Player", Child("Seat", car));
            player.Add(new PlayMakerFSM { FsmName = "InCar", ActiveStateName = "InCar" });
            player.Add(new PlayMakerFSM { FsmName = "Health", ActiveStateName = "Idle" });
            Camera first = Child("PlayerCamera", Child("PlayerCameraHolder", player)).Add(new Camera());
            first.enabled = false;
            Physics.Hits = new RaycastHit[0]; Physics.Overlaps = new Collider[0]; Physics.GroundProbe = null;
            Terrain.activeTerrains = new Terrain[0]; Input.X = Input.Y = 0f;
            Plugin.Enabled.Value = true; Plugin.Recenter.Value = false;
            Plugin.Distance.Value = 6.5f; Plugin.Side.Value = 0f; Plugin.Pitch.Value = 8f;
            Apocasetter.GameMenu.InGame = true; Apocasetter.GameMenu.Paused = false;
            Apocasetter.InputBlocker.Active = false; Application.isFocused = true; Time.timeScale = 1f;
        }

        internal static int Run()
        {
            checks = 0;
            bool enabled = Plugin.Enabled.Value, recenter = Plugin.Recenter.Value;
            float pitch = Plugin.Pitch.Value, distance = Plugin.Distance.Value, side = Plugin.Side.Value;
            try
            {
                LivePitch(); InterruptedRender(); CollisionWork(); GrowingQueries();
                Console.WriteLine("Camera runtime regressions: live pitch bounds, interrupted renders, collision budgets and retained buffers passed.");
                return checks;
            }
            finally
            {
                ChaseView.Reset(); CameraBinding.Reset();
                Input.X = Input.Y = 0f; Physics.Hits = new RaycastHit[0];
                Physics.Overlaps = new Collider[0]; Physics.GroundProbe = null;
                Plugin.Enabled.Value = enabled; Plugin.Recenter.Value = recenter;
                Plugin.Pitch.Value = pitch; Plugin.Distance.Value = distance; Plugin.Side.Value = side;
            }
        }

        private static void LivePitch()
        {
            Setup("LivePitchLimits"); Frame(1f / 60f); Render(); Finish();
            Input.Y = -100f; Frame(1f / 60f); Render(); Finish(); Input.Y = 0f;
            Check(Math.Abs(Field<Quaternion>("rotation").euler.x - 65f) < 0.001f, "mouse orbit reaches upper bound");
            Plugin.Pitch.Value = 35f;
            Apocasetter.InputBlocker.Active = true; Frame(1f / 60f);
            Apocasetter.InputBlocker.Active = false; Frame(1f / 60f); Render(); Finish();
            Check(Math.Abs(Field<Quaternion>("rotation").euler.x - 65f) < 0.001f, "base-pitch edit during menu keeps upper bound");
            Input.Y = 100f; Frame(1f / 60f); Render(); Finish(); Input.Y = 0f;
            Check(Math.Abs(Field<Quaternion>("rotation").euler.x + 35f) < 0.001f, "mouse orbit reaches lower bound");
            Plugin.Pitch.Value = -10f; Frame(1f / 60f); Render(); Finish();
            Check(Math.Abs(Field<Quaternion>("rotation").euler.x + 35f) < 0.001f, "base-pitch edit without input keeps lower bound");
        }

        private static void InterruptedRender()
        {
            Setup("InterruptedRenderRecovery"); Frame(1f / 60f); Render();
            int firstMatrix = camera.worldToCameraMatrix.token;
            int resets = camera.ViewResets;
            // No Finish: the previous render has lost its post-render callback.
            car.transform.position = new Vector3(10f, 0f, 0f); Frame(1f / 60f);
            Check(camera.ViewResets == resets + 1 && Field<int>("renderedFrame") == -1,
                "next Update restores an unfinished prior render");
            Render();
            Check(camera.worldToCameraMatrix.token != firstMatrix,
                "next view matrix follows the newly moved vehicle");
            int currentMatrix = camera.worldToCameraMatrix.token; resets = camera.ViewResets;
            Check(ChaseView.Apply(camera), "same-frame repeated Apply succeeds");
            Check(camera.ViewResets == resets && camera.worldToCameraMatrix.token == currentMatrix,
                "same-frame Apply leaves the active render intact");
            // Other mods can invoke Apply before this runner receives Update.
            car.transform.position = new Vector3(20f, 0f, 0f);
            Time.unscaledTime += 1f / 60f; Time.frameCount++;
            Check(ChaseView.Apply(camera), "new-frame Apply succeeds without Tick");
            Check(camera.ViewResets == resets + 1 && camera.worldToCameraMatrix.token != currentMatrix,
                "direct new-frame Apply restores and rebuilds the old render");
            Finish();
            Check(Field<int>("renderedFrame") == -1 && camera.worldToCameraMatrix.token == 101,
                "normal completion returns ownership to Unity automatic matrices");
        }

        private static void CollisionWork()
        {
            Setup("CollisionProbeBudget"); Physics.BufferedQueries = Physics.AllocatingQueries = 0;
            Frame(1f / 60f); Render();
            Check(Physics.BufferedQueries == 4 && Physics.AllocatingQueries == 0,
                "clear view uses one sweep, one support ray and one endpoint validation");
            Finish();
            Collider wall = new GameObject("Inescapable obstruction").Add(new Collider());
            Physics.Hits = new[] { new RaycastHit { collider = wall, distance = 0.2f } };
            Physics.Overlaps = new[] { wall }; Physics.BufferedQueries = 0;
            Frame(1f / 60f); Render();
            Check(!Field<bool>("clearPose") && Field<float>("distance") == 0f,
                "unresolved pivot stays in the native view");
            Check(Physics.BufferedQueries == 6,
                "short blocked boom stops after checking the zero-distance candidate");
            Physics.Hits = new RaycastHit[0]; Physics.BufferedQueries = 0;
            Frame(0f); Render();
            Check(Physics.BufferedQueries == 4,
                "already-zero boom does not probe the same blocked pivot repeatedly");
        }

        private static void GrowingQueries()
        {
            Setup("RetainedCollisionBuffers");
            Collider own = car.Add(new Collider()); Collider wall = new GameObject("Late external wall").Add(new Collider());
            RaycastHit[] hits = new RaycastHit[129]; Collider[] colliders = new Collider[129];
            for (int i = 0; i < 128; i++)
            { hits[i] = new RaycastHit { collider = own, distance = 0.1f }; colliders[i] = own; }
            hits[128] = new RaycastHit { collider = wall, distance = 2f }; colliders[128] = wall;
            Physics.Hits = hits; Physics.Overlaps = colliders; Physics.GroundProbe = delegate(Vector3 origin) { return hits; };
            Physics.AllocatingQueries = Physics.BufferedQueries = 0;
            int count;
            RaycastHit[] sweep = CameraQueries.Sweep(new Vector3(), 0.3f, new Vector3(0f, 0f, -1f), 6.5f, out count);
            Check(count == 129 && sweep[128].collider == wall && Physics.AllocatingQueries == 0,
                "growing sweep keeps a late external hit without allocating all-results API");
            int queries = Physics.BufferedQueries;
            Check(System.Object.ReferenceEquals(sweep, CameraQueries.Sweep(new Vector3(), 0.3f, new Vector3(0f, 0f, -1f), 6.5f, out count)) &&
                Physics.BufferedQueries == queries + 1, "next dense sweep reuses its grown capacity in one query");
            RaycastHit[] ray = CameraQueries.Ray(new Vector3(), new Vector3(0f, -1f, 0f), 20f, out count);
            Check(count == 129 && ray[128].collider == wall && Physics.AllocatingQueries == 0,
                "growing support ray keeps its late hit");
            Collider[] overlap = CameraQueries.Overlap(new Vector3(), 0.3f, out count);
            Check(count == 129 && overlap[128] == wall && Physics.AllocatingQueries == 0,
                "growing overlap keeps its late collider");
            CameraQueries.Clear();
            Check(sweep[128].collider == null && ray[128].collider == null && overlap[128] == null,
                "reset releases retained collider references");
            Check(System.Object.ReferenceEquals(sweep, CameraQueries.Sweep(new Vector3(), 0.3f, new Vector3(0f, 0f, -1f), 6.5f, out count)),
                "reset retains grown buffer capacity");

            // Growth is bounded, but complete geometry must never be discarded.
            RaycastHit[] crowded = new RaycastHit[4097]; Collider[] crowdedOverlap = new Collider[4097];
            for (int i = 0; i < 4096; i++)
            { crowded[i] = new RaycastHit { collider = own, distance = 0.1f }; crowdedOverlap[i] = own; }
            crowded[4096] = new RaycastHit { collider = wall, distance = 2f }; crowdedOverlap[4096] = wall;
            Physics.Hits = crowded; Physics.Overlaps = crowdedOverlap;
            Physics.GroundProbe = delegate(Vector3 origin) { return crowded; }; Physics.AllocatingQueries = 0;
            RaycastHit[] complete = CameraQueries.Sweep(new Vector3(), 0.3f, new Vector3(0f, 0f, -1f), 6.5f, out count);
            Check(count == 4097 && complete[4096].collider == wall && Physics.AllocatingQueries == 1,
                "sweep at growth limit preserves the complete hit list");
            complete = CameraQueries.Ray(new Vector3(), new Vector3(0f, -1f, 0f), 20f, out count);
            Check(count == 4097 && complete[4096].collider == wall && Physics.AllocatingQueries == 2,
                "ray at growth limit preserves the complete hit list");
            Collider[] all = CameraQueries.Overlap(new Vector3(), 0.3f, out count);
            Check(count == 4097 && all[4096] == wall && Physics.AllocatingQueries == 3,
                "overlap at growth limit preserves the complete collider list");
            Physics.Hits = new RaycastHit[0]; Physics.Overlaps = new Collider[0]; Physics.GroundProbe = null;
            CameraQueries.Clear();
            sweep = CameraQueries.Sweep(new Vector3(), 0.3f, new Vector3(0f, 0f, -1f), 6.5f, out count);
            Check(count == 0 && sweep.Length == 4096 && sweep[4095].collider == null,
                "cleared saturated buffer is bounded and cannot retain stale hits");
        }
    }
}
