using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Input;
using Game.Modding;
using Game.SceneFlow;
using UnityEngine.InputSystem;
using HarmonyLib;
using Unity.Mathematics;
using UnityEngine;
using Game.Simulation;
using Unity.Entities;
using Colossal.Mathematics;

namespace CameraDrag
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(CameraDrag)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private const string HARMONY_ID = "at.pimaker.cameradrag";

        private const float DECAY_EPS = 0.0001f;
        private const float DELTA_MULT = -0.0001f;
        private const float SMOOTHING_DIV = 0.085f;

        public static Mod Instance { get; private set; }

        private Harmony harmony;
        private Setting settings;
        private ProxyActionMap cameraMap;
        private TerrainSystem terrainSystem;

        private bool lastFrameDragging;
        private bool blockDraggingFromUI;
        private float2 delta;
        private float2 lastGrabPosition;
        private float3 worldTerrainPosAtStartDrag;

        // accessed from CameraControllerPatch
        public float2 Delta => delta;
        public bool Dragging => lastFrameDragging;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Instance = this;
            log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                log.Info($"Current mod asset at {asset.path}");

            settings = new Setting(this);
            settings.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(settings));

            settings.RegisterKeyBindings();

            AssetDatabase.global.LoadSettings(nameof(CameraDrag), settings);

            // apply patches in CameraControllerPatch
            harmony = new Harmony(HARMONY_ID);
            harmony.PatchAll(typeof(CameraControllerPatch).Assembly);
            foreach (var method in harmony.GetPatchedMethods())
                log.Info($"Patched method: {method}");
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (settings != null)
            {
                settings.UnregisterInOptionsUI();
                settings = null;
            }
            if (harmony != null)
            {
                harmony.UnpatchAll(HARMONY_ID);
                harmony = null;
            }
            Instance = null;
        }

        private bool SetupDependencies()
        {
            // find the main camera action map
            if (cameraMap == null)
            {
                cameraMap = InputManager.instance?.FindActionMap("Camera");
                if (cameraMap == null) return false;
                log.Info("Found Camera action map");
            }

            // find the terrain system
            if (terrainSystem == null)
            {
                terrainSystem = World.DefaultGameObjectInjectionWorld?.GetOrCreateSystemManaged<TerrainSystem>();
                if (terrainSystem == null) return false;
                log.Info("Found TerrainSystem");
            }

            return true;
        }

        public void UpdateMod()
        {
            // always run decay so we eventually stop
            if (math.any(delta != 0f))
            {
                if (Mathf.Approximately(settings.Smoothing, 0f) || math.all(delta < DECAY_EPS) && math.all(delta > -DECAY_EPS))
                {
                    // stop, zero exactly
                    delta = float2.zero;
                }
                else
                {
                    // smooth decay
                    delta = math.lerp(delta, 0f, (1f / (settings.Smoothing * SMOOTHING_DIV)) * Time.deltaTime);
                }
            }

            // not using mouse, abort
            if (InputManager.instance.activeControlScheme != InputManager.ControlScheme.KeyboardAndMouse)
            {
                lastFrameDragging = false;
                blockDraggingFromUI = false;
                return;
            }

            // find and assign dependencies
            if (!SetupDependencies())
            {
                lastFrameDragging = false;
                blockDraggingFromUI = false;
                return;
            }

            // left or right?
            var button = settings.UseLeftMouseButton ? Mouse.current.leftButton : Mouse.current.rightButton;

            // don't drag when any of WASD is pressed
            var wasdPressed = false;
            foreach (var action in cameraMap.actions.Values)
            {
                if (action.IsPressed())
                {
                    wasdPressed = true;
                    break;
                }
            }

            // main enable logic
            if (!blockDraggingFromUI && !wasdPressed && (lastFrameDragging || (
                (InputManager.instance.controlOverWorld && InputManager.instance.mouseOnScreen) &&
                (!settings.RequireCtrl || Keyboard.current.ctrlKey.isPressed) &&
                (!settings.RequireShift || Keyboard.current.shiftKey.isPressed)
            )))
            {
                if (button.isPressed)
                {
                    // skip starting frame to init mouse position, otherwise calculate world-space camera movemente delta
                    var currentMousePosition = ((float3)InputManager.instance.mousePosition).xy;
                    if (lastFrameDragging)
                    {
                        if (math.any(worldTerrainPosAtStartDrag != float3.zero))
                        {
                            // world-grab mode
                            var worldTerrainPlane = new Plane(Vector3.up, worldTerrainPosAtStartDrag);
                            var ray = Camera.main.ScreenPointToRay(InputManager.instance.mousePosition);
                            if (worldTerrainPlane.Raycast(ray, out float enter))
                            {
                                var newWorldPlaneHitPos = ray.GetPoint(enter);
                                var newMousePosition = new float2(newWorldPlaneHitPos.x, newWorldPlaneHitPos.z);
                                delta = newMousePosition - lastGrabPosition;
                                log.Info($"worldTerrainPosAtStartDrag={worldTerrainPosAtStartDrag}, delta={delta}");
                                lastGrabPosition = newMousePosition;
                            }
                        }
                        else
                        {
                            // approximation mode
                            delta = (currentMousePosition - lastGrabPosition) * DELTA_MULT * settings.Sensitivity;
                            lastGrabPosition = currentMousePosition;
                        }
                    }
                    else
                    {
                        // first frame
                        var terrainData = terrainSystem.GetHeightData();
                        var startPoint = Camera.main.ScreenToWorldPoint(new Vector3(currentMousePosition.x, currentMousePosition.y, -20_000f));
                        var endPoint = Camera.main.ScreenToWorldPoint(new Vector3(currentMousePosition.x, currentMousePosition.y, 60_000f));
                        var raycastSegment = new Line3.Segment(startPoint, endPoint);
                        var terrainHit = TerrainUtils.Raycast(ref terrainData, raycastSegment, true, out float terrainHitDistance, out float3 _);
                        if (terrainHit)
                        {
                            worldTerrainPosAtStartDrag = raycastSegment.a + raycastSegment.ab * terrainHitDistance;
                            lastGrabPosition = worldTerrainPosAtStartDrag.xz;
                            log.Info($"GRAB START worldTerrainPosAtStartDrag={worldTerrainPosAtStartDrag}, lastGrabPosition={lastGrabPosition}");
                        }
                        else
                        {
                            worldTerrainPosAtStartDrag = float3.zero;
                            lastGrabPosition = currentMousePosition;
                        }
                    }
                    lastFrameDragging = true;
                    return;
                }
            }
            else
            {
                // prevent drag started above UI, even when moving away
                if (!InputManager.instance.controlOverWorld)
                    blockDraggingFromUI = true;
                else if (!button.isPressed)
                    blockDraggingFromUI = false;
            }


            // nothing burger
            lastFrameDragging = false;
            return;
        }

        private static float2 XY(Vector3 input) => new float2(input.x, input.y);
    }
}
