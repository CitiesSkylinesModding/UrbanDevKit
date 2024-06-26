using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using JetBrains.Annotations;

namespace TestModOne;

[UsedImplicitly]
public sealed class Mod : IMod {
    public static readonly ILog Log = LogManager
        .GetLogger(nameof(TestModOne))
        .SetShowsErrorsInUI(true);

    private Setting? setting;

    public void OnLoad(UpdateSystem updateSystem) {
        Mod.Log.Info(nameof(Mod.OnLoad));

        if (GameManager.instance.modManager.TryGetExecutableAsset(
                this, out var asset)) {
            Mod.Log.Info($"Current mod asset at {asset.path}");
        }

        this.setting = new Setting(this);
        this.setting.RegisterInOptionsUI();

        GameManager.instance.localizationManager.AddSource(
            "en-US", new LocaleEn(this.setting));

        AssetDatabase.global.LoadSettings(
            nameof(TestModOne), this.setting, new Setting(this));
    }

    public void OnDispose() {
        Mod.Log.Info(nameof(Mod.OnDispose));

        if (this.setting is not null) {
            this.setting.UnregisterInOptionsUI();
            this.setting = null;
        }
    }
}
