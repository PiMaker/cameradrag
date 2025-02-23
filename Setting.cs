using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace CameraDrag
{
    [FileLocation(nameof(CameraDrag))]
    public class Setting : ModSetting
    {
        public Setting(IMod mod) : base(mod)
        {
        }

        [SettingsUISection("Bindings")]
        public ButtonMode AllowLeftMouseButton { get; set; } = ButtonMode.Disabled;
        [SettingsUISection("Bindings")]
        public ButtonMode AllowRightMouseButton { get; set; } = ButtonMode.AllowDragWithShift;
        [SettingsUISection("Bindings")]
        public ButtonMode AllowMiddleMouseButton { get; set; } = ButtonMode.AllowDragWithShift;

        [SettingsUISection("Sensitivity")]
        [SettingsUISlider(min = 1f, max = 40f, step = 1f, scalarMultiplier = 1f )]
        public float Sensitivity { get; set; } = 5f;
        [SettingsUISection("Sensitivity")]
        [SettingsUISlider(min = 0f, max = 10f, step = 1f, scalarMultiplier = 1f)]
        public float Smoothing { get; set; } = 2f;

        public override void SetDefaults()
        {
            AllowLeftMouseButton = ButtonMode.Disabled;
            AllowRightMouseButton = ButtonMode.AllowDragWithShift;
            AllowMiddleMouseButton = ButtonMode.AllowDragWithShift;
            Sensitivity = 5f;
            Smoothing = 2f;
        }

        public enum ButtonMode
        {
            Disabled,
            AllowDrag,
            AllowDragWithShift,
            AllowDragWithCtrl,
        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "Camera Drag" },
                { m_Setting.GetBindingMapLocaleID(), "Camera Drag Settings" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AllowLeftMouseButton)), "Use Left Mouse Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AllowLeftMouseButton)), "Allow using the left mouse button to drag the camera. Optionally require Shift/Control to be pressed." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AllowRightMouseButton)), "Use Right Mouse Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AllowRightMouseButton)), "Allow using the right mouse button to drag the camera. Optionally require Shift/Control to be pressed." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.AllowMiddleMouseButton)), "Use Middle Mouse Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.AllowMiddleMouseButton)), "Allow using the middle mouse button to drag the camera. Optionally require Shift/Control to be pressed." },

                { m_Setting.GetEnumValueLocaleID(Setting.ButtonMode.Disabled), "Disabled" },
                { m_Setting.GetEnumValueLocaleID(Setting.ButtonMode.AllowDrag), "Always Enabled" },
                { m_Setting.GetEnumValueLocaleID(Setting.ButtonMode.AllowDragWithShift), "Require Shift" },
                { m_Setting.GetEnumValueLocaleID(Setting.ButtonMode.AllowDragWithCtrl), "Require Ctrl" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Sensitivity)), "Drag Sensitivity" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Sensitivity)), "How quickly the camera will move relative to your mouse. The default of 5 mostly keeps your cursor in the same place on the map." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Smoothing)), "Smoothing" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Smoothing)), "How much the camera will keep moving after you let go of the button. 0 means no smoothing." },

                { m_Setting.GetOptionGroupLocaleID("Bindings"), "Bindings" },
                { m_Setting.GetOptionGroupLocaleID("Sensitivity"), "Sensitivity / Smoothing" },
                { m_Setting.GetOptionGroupLocaleID("Invert"), "Inverted Movement" },
            };
        }

        public void Unload()
        {
        }
    }
}