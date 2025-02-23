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
        public bool UseLeftMouseButton { get; set; } = false;
        [SettingsUISection("Bindings")]
        public bool RequireShift { get; set; } = false;
        [SettingsUISection("Bindings")]
        public bool RequireCtrl { get; set; } = false;

        [SettingsUISection("Sensitivity")]
        [SettingsUISlider(min = 1f, max = 40f, step = 1f, scalarMultiplier = 1f )]
        public float Sensitivity { get; set; } = 5f;
        [SettingsUISection("Sensitivity")]
        [SettingsUISlider(min = 0f, max = 10f, step = 1f, scalarMultiplier = 1f)]
        public float Smoothing { get; set; } = 2f;

        public override void SetDefaults()
        {
            UseLeftMouseButton = false;
            RequireShift = false;
            RequireCtrl = false;
            Sensitivity = 5f;
            Smoothing = 2f;
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

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.UseLeftMouseButton)), "Use Left Mouse Button" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.UseLeftMouseButton)), "Use the left mouse button to drag the camera. Default is right. Note that the left button may interfere with object selection." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RequireShift)), "Require Shift Key" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RequireShift)), "Require the Shift key to be held to drag the camera." },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.RequireCtrl)), "Require Control Key" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.RequireCtrl)), "Require the Control key to be held to drag the camera." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.Sensitivity)), "Drag Sensitivity" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.Sensitivity)), "How quickly the camera will move relative to your mouse." },
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
