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

namespace CameraDrag
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger(nameof(CameraDrag)).SetShowsErrorsInUI(false);
        private const string HARMONY_ID = "at.pimaker.cameradrag";

        private const float DECAY_EPS = 0.0001f;
        private const float DELTA_MULT = -0.0001f;
        private const float SMOOTHING_DIV = 0.085f;
        private const float BASE_SCREEN_WIDTH = 2560f;

        public static Mod Instance;

        private Harmony harmony;
        private Setting settings;
        private ProxyActionMap cameraMap;
        private ProxyAction cameraRotate;
        private CameraController cameraController;

        private bool lastFrameDragging;
        private bool blockDraggingFromUI;
        private float2 delta;
        private float2 lastMousePosition;

        // accessed from CameraControllerPatch
        public float2 Delta => delta;
        public bool Dragging => lastFrameDragging;
        public Vector2 ReadRotateValue() => lastFrameDragging ? Vector2.zero : cameraRotate.ReadValue<Vector2>();

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

        public void UpdateMod()
        {
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

            // find the main camera action map
            if (cameraMap == null)
            {
                cameraMap = InputManager.instance?.FindActionMap("Camera");
                if (cameraMap == null) return;
                log.Info("Found Camera action map");
            }

            // find the main camera rotate action
            if (cameraRotate == null)
            {
                cameraRotate = cameraMap.FindAction("Rotate");
                if (cameraRotate == null) return;
                log.Info("Found Camera rotate action");
            }

            // find the main camera controller
            if (cameraController == null)
            {
                if (!CameraController.TryGet(out cameraController)) return;
                if (cameraController == null) return;
                log.Info("Found Camera controller");
            }

            // are we clicking something allowed?
            var isPressed = false;
            if (IsButtonAllowed(settings.AllowLeftMouseButton) && Mouse.current.leftButton.isPressed)
                isPressed = true;
            else if (IsButtonAllowed(settings.AllowRightMouseButton) && Mouse.current.rightButton.isPressed)
                isPressed = true;
            else if (IsButtonAllowed(settings.AllowMiddleMouseButton) && Mouse.current.middleButton.isPressed)
                isPressed = true;

            // don't drag when any of WASD is pressed
            var wasdPressed = false;
            foreach (var action in cameraMap.actions.Values)
            {
                if (action == cameraRotate)
                    continue; // ignore rotate action

                if (action.IsPressed())
                {
                    wasdPressed = true;
                    break;
                }
            }

            // main enabling logic
            if (!blockDraggingFromUI && !wasdPressed && isPressed &&
                (lastFrameDragging || (InputManager.instance.controlOverWorld && InputManager.instance.mouseOnScreen)))
            {
                // skip starting frame to init mouse position, then set new delta
                var currentMousePosition = ((float3)InputManager.instance.mousePosition).xy;
                if (lastFrameDragging)
                {
                    var rotationMult = math.min(5f, math.tan(math.radians(90f - math.abs(cameraController.angle.y))));
                    var rotationMultAxis = 1f + new float2(rotationMult * 0.05f /* game feel */, rotationMult);
                    var screenMult = BASE_SCREEN_WIDTH / Mathf.Max(240f, Screen.width);
                    delta = (currentMousePosition - lastMousePosition) * DELTA_MULT * settings.Sensitivity * rotationMultAxis * screenMult;
                }
                lastMousePosition = currentMousePosition;
                lastFrameDragging = true;
                return;
            }
            else
            {
                // prevent drag started over UI, even when moving away
                if (!InputManager.instance.controlOverWorld)
                    blockDraggingFromUI = true;
                else if (!isPressed)
                    blockDraggingFromUI = false;
            }

            // nothing burger
            lastFrameDragging = false;
            return;
        }

        private static bool IsButtonAllowed(Setting.ButtonMode mode)
        {
            switch (mode)
            {
                case Setting.ButtonMode.AllowDrag:
                    return true;
                case Setting.ButtonMode.AllowDragWithShift:
                    return Keyboard.current.shiftKey.isPressed;
                case Setting.ButtonMode.AllowDragWithCtrl:
                    return Keyboard.current.ctrlKey.isPressed;
                case Setting.ButtonMode.Disabled:
                default:
                    return false;
            }
        }
    }
}