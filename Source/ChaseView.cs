using System;
using UnityEngine;

namespace ApocaChaseCamera
{
    internal static class ChaseView
    {
        private static bool ready, allowed, clearPose, suspended;
        private static Camera selected, rendered;
        private static Vector3 previousRaw, position;
        private static Quaternion rotation;
        private static float vertical, heading, orbit, pitchOrbit, distance, lastMove, lastTime;
        private static float mouseX, mouseY, nextError;
        private static float suspendTime, recenterBlend;
        private static int poseFrame = -1;
        private static int renderedFrame = -1;

        internal static void Reset()
        {
            Restore(); CompassView.Reset(); CameraQueries.Clear(); ready = false; allowed = false; clearPose = false; selected = null;
            orbit = 0f; pitchOrbit = 0f; mouseX = 0f; mouseY = 0f; poseFrame = -1;
            suspended = false; recenterBlend = 0f;
        }

        private static void Suspend()
        {
            Restore(); CompassView.Release(); allowed = false; mouseX = 0f; mouseY = 0f;
            if (!suspended) { suspended = true; suspendTime = Time.unscaledTime; }
        }

        internal static void Tick()
        {
            try
            {
                CompassView.Release();
                // Recover a render interrupted before onPostRender. Same-frame
                // callbacks may repeat, but an old view must not leak forward.
                if (renderedFrame >= 0 && renderedFrame != Time.frameCount) Restore();
                ApocaplayerBridge.Discover();
                if (!Plugin.Enabled.Value || !Apocasetter.GameMenu.InGame) { Reset(); return; }
                if (!Application.isFocused || Apocasetter.GameMenu.Paused ||
                    Apocasetter.InputBlocker.Active || Time.timeScale <= 0f) { Suspend(); return; }
                if (!CameraBinding.Resolve()) { Reset(); return; }
                if (CameraBinding.SpecialView()) { Suspend(); return; }
                Camera camera = CameraBinding.SelectedCamera();
                if (camera == null) { Suspend(); return; }
                if (selected != camera) { Reset(); selected = camera; }
                allowed = true;
                if (suspended)
                {
                    lastMove += Mathf.Max(0f, Time.unscaledTime - suspendTime);
                    lastTime = Time.unscaledTime; suspended = false; recenterBlend = 0f;
                    return;
                }
                // Unity mouse axes are frame deltas: do not multiply by deltaTime.
                mouseX = Input.GetAxisRaw("Mouse X"); mouseY = Input.GetAxisRaw("Mouse Y");
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void BuildPose()
        {
            if (poseFrame == Time.frameCount) return;
            poseFrame = Time.frameCount;
            Transform car = CameraBinding.Car;
            Vector3 raw = car.TransformPoint(CameraBinding.LocalAnchor);
            float now = Time.unscaledTime;
            float dt = now - lastTime;
            Vector3 flat = Vector3.ProjectOnPlane(car.forward, Vector3.up);
            float targetHeading = flat.sqrMagnitude > 0.001f ?
                Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg : heading;
            if (!ready)
            {
                vertical = raw.y; heading = targetHeading; orbit = 0f; pitchOrbit = 0f;
                distance = Plugin.Distance.Value; lastMove = now; dt = 0f; ready = true;
            }
            else if (CameraMath.NeedsReset(Vector3.Distance(raw, previousRaw), dt))
            {
                // A floating-origin shift or frame stall invalidates position
                // history, not the user's chosen look direction.
                vertical = raw.y;
                float shortened = CameraMath.Clamp(dt, 0f, 1f / 30f);
                lastMove += Mathf.Max(0f, dt - shortened);
                dt = shortened; recenterBlend = 0f;
            }
            vertical = CameraMath.FollowHeight(vertical, raw.y, Plugin.BumpTime.Value,
                Plugin.MaxHeightLag.Value, dt);
            heading = CameraMath.FollowAngle(heading, targetHeading, Plugin.TurnTime.Value, dt);
            if (Math.Abs(mouseX) + Math.Abs(mouseY) > 0.002f)
            {
                orbit = CameraMath.Wrap(orbit + mouseX * Plugin.Sensitivity.Value);
                pitchOrbit -= mouseY * Plugin.Sensitivity.Value;
                lastMove = now;
                recenterBlend = 0f;
            }
            else if (Plugin.Recenter.Value && now - lastMove >= Plugin.RecenterDelay.Value)
            {
                recenterBlend = CameraMath.Follow(recenterBlend, 1f, 0.2f, dt);
                orbit = CameraMath.RecenterAngle(orbit, Plugin.RecenterTime.Value, recenterBlend, dt);
                pitchOrbit = CameraMath.RecenterAngle(pitchOrbit, Plugin.RecenterTime.Value, recenterBlend, dt);
            }
            // The base pitch is adjustable while orbit is preserved through a
            // settings-menu pause. Keep the combined angle within its limits.
            pitchOrbit = CameraMath.Clamp(pitchOrbit, -35f - Plugin.Pitch.Value, 65f - Plugin.Pitch.Value);
            previousRaw = raw; lastTime = now;
            // Horizontal translation follows immediately: fast cars cannot run
            // ahead of the camera. Only vertical bumps and heading are damped.
            Vector3 pivot = new Vector3(raw.x, vertical + Plugin.Height.Value, raw.z);
            rotation = Quaternion.Euler(Plugin.Pitch.Value + pitchOrbit, heading + orbit, 0f);
            Vector3 offset = rotation * new Vector3(Plugin.Side.Value, 0f, -Plugin.Distance.Value);
            float desired = offset.magnitude;
            Vector3 direction = offset / desired;
            float radius = Mathf.Max(0.22f, selected.nearClipPlane *
                Mathf.Tan(selected.fieldOfView * Mathf.Deg2Rad * 0.5f) * Mathf.Sqrt(1f + selected.aspect * selected.aspect));
            float clear = desired;
            Terrain[] terrains = Terrain.activeTerrains;
            RaycastHit? support = GroundSupport(raw);
            int sweepCount;RaycastHit[] sweep=CameraQueries.Sweep(pivot,radius,direction,desired,out sweepCount);
            for(int i=0;i<sweepCount;i++)
            {
                RaycastHit hit=sweep[i];
                if (!CameraBinding.OwnCollider(hit.collider)) clear = Mathf.Min(clear, Mathf.Max(0f, hit.distance - 0.08f));
            }
            distance = CameraMath.SafeDistance(distance, clear, Plugin.RecoveryTime.Value, dt);
            Vector3 candidate = pivot + direction * distance;
            // A collider can engulf the start of a sphere cast. Check the end as
            // well and search inward instead of accepting a position in a wall.
            // Terrain overlaps and sphere casts can miss a camera already
            // beneath a heightfield. Sample its surface explicitly, and bring
            // the boom inward rather than raising the camera above the car.
            float step = Mathf.Max(Mathf.Max(radius, 0.25f), desired / 24f);
            bool blocked = Blocked(candidate, radius, terrains, support);
            for (int attempt = 0; attempt < 24 && blocked; attempt++)
            {
                float shorter = Mathf.Max(0f, distance - step);
                if (shorter >= distance) break;
                distance = shorter;
                candidate = pivot + direction * distance;
                blocked = Blocked(candidate, radius, terrains, support);
            }
            clearPose = !blocked;
            position = candidate;
        }

        private static RaycastHit? GroundSupport(Vector3 raw)
        {
            RaycastHit? nearest = null;
            int count;RaycastHit[] hits=CameraQueries.Ray(raw+Vector3.up*0.5f,new Vector3(0,-1,0),20,out count);
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=hits[i];
                if (!CameraBinding.OwnCollider(hit.collider) && hit.normal.y > 0.25f &&
                    (!nearest.HasValue || hit.point.y > nearest.Value.point.y)) nearest = hit;
            }
            return nearest;
        }

        private static bool Blocked(Vector3 point, float radius, Terrain[] terrains, RaycastHit? support)
        {
            float clearance = radius + Plugin.GroundClearance.Value;
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 start = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (point.x < start.x || point.z < start.z || point.x > start.x + size.x || point.z > start.z + size.z) continue;
                float ground = start.y + terrain.SampleHeight(point);
                if (point.y < ground + clearance) return true;
            }
            // Project the supporting road's slope to the camera. That lets a
            // downward ray start above a road even when the camera has gone
            // below its surface, without starting above distant roofs/bridges.
            float floor = point.y;
            if (support.HasValue)
            {
                RaycastHit road = support.Value;
                floor = road.point.y - (road.normal.x * (point.x - road.point.x) +
                    road.normal.z * (point.z - road.point.z)) / road.normal.y;
            }
            Vector3 probe = new Vector3(point.x, Mathf.Max(point.y, floor) + clearance, point.z);
            int count;RaycastHit[] hits=CameraQueries.Ray(probe,new Vector3(0,-1,0),clearance+12,out count);
            for(int i=0;i<count;i++)
            {
                RaycastHit hit=hits[i];
                if (!CameraBinding.OwnCollider(hit.collider) && hit.normal.y > 0.25f &&
                    (!support.HasValue || hit.point.y <= floor + 2f) &&
                    point.y < hit.point.y + clearance) return true;
            }
            Collider[] colliders=CameraQueries.Overlap(point,radius,out count);
            for(int i=0;i<count;i++) if (!CameraBinding.OwnCollider(colliders[i])) return true;
            return false;
        }

        internal static void PreCull(Camera camera)
        {
            // With Apocaplayer, its prefix delegates the cruising view to us.
            if (ApocaplayerBridge.Owns(camera)) return;
            Apply(camera);
        }

        internal static bool CompassHeading(out float yaw)
        {
            yaw = 0f;
            // A native camera switch can happen after our Update. Do not use
            // the previous view's pose when the active camera has changed.
            if (!allowed || selected == null || !selected.isActiveAndEnabled ||
                selected != CameraBinding.SelectedCamera() || CameraBinding.Car == null) return false;
            try
            {
                BuildPose();
                if (!clearPose) return false;
                yaw = heading + orbit;
                return true;
            }
            catch (Exception ex) { Fail(ex); return false; }
        }

        internal static bool Apply(Camera camera)
        {
            if (!allowed || camera != selected || CameraBinding.Car == null) return false;
            try
            {
                // Apply can also be reached by another camera mod's render
                // hook before this runner receives its next Update.
                if (renderedFrame >= 0 && renderedFrame != Time.frameCount) Restore();
                BuildPose();
                if (!clearPose) return false;
                if (rendered == camera) return true;
                Restore();
                rendered = camera;
                renderedFrame = Time.frameCount;
                Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) *
                    Matrix4x4.TRS(position, rotation, Vector3.one).inverse;
                camera.worldToCameraMatrix = view;
                camera.cullingMatrix = camera.projectionMatrix * view;
                if (!ApocaplayerBridge.Publish(camera, position, rotation)) { Restore(); return false; }
                CompassView.Apply(heading + orbit);
                return true;
            }
            catch (Exception ex) { Fail(ex); return false; }
        }

        internal static void PostRender(Camera camera) { if (rendered == camera) Restore(); }
        private static void Restore()
        {
            // Assigning a captured default matrix would leave Unity in custom
            // matrix mode and freeze its normal view when the car later moves.
            if (rendered != null) { rendered.ResetWorldToCameraMatrix(); rendered.ResetCullingMatrix(); }
            rendered = null;
            renderedFrame = -1;
        }
        private static void Fail(Exception ex)
        {
            Reset();
            if (Time.unscaledTime < nextError) return;
            nextError = Time.unscaledTime + 10f;
            Plugin.Log.LogWarning("Camera override suspended: " + ex);
        }
    }
}
