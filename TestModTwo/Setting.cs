using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;

namespace TestModTwo;

[FileLocation($"ModsSettings/{nameof(TestModTwo)}/{nameof(TestModTwo)}")]
public sealed class Setting(IMod mod) : ModSetting(mod) {
    [SettingsUIButton]
    public bool Button {
        // ReSharper disable once ValueParameterNotUsed
        set => Mod.Log.Info("Button clicked");
    }

    public override void SetDefaults() {
        // noop
    }
}

public class LocaleEn(Setting setting) : IDictionarySource {
    public IEnumerable<KeyValuePair<string, string>> ReadEntries(
        IList<IDictionaryEntryError> errors,
        Dictionary<string, int> indexCounts) =>
        new Dictionary<string, string> {
            { setting.GetSettingsLocaleID(), "Test Mod Two" },
            { setting.GetOptionLabelLocaleID(nameof(Setting.Button)), "Button" }, {
                setting.GetOptionDescLocaleID(nameof(Setting.Button)),
                $"Simple single button. It should be bool property with only setter or use [{nameof(SettingsUIButtonAttribute)}] to make button from bool property with setter and getter"
            }
        };

    public void Unload() {
    }
}
