using System.Collections;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Debug;
using Game.Modding;
using Game.SceneFlow;
using JetBrains.Annotations;
using UnityEngine;
using UrbanDevKit.CooperativePreloading;
using Random = System.Random;

namespace TestModTwo;

[UsedImplicitly]
public sealed class Mod : IMod {
    public static readonly ILog Log = LogManager
        .GetLogger(nameof(TestModTwo))
        .SetShowsErrorsInUI(true);

    public static readonly PreloadingOperation<IDisposableWithException>
        DisposablePreloader =
            Preloader.RegisterPreloader(
                nameof(TestModTwo),
                "StartPreloadingOperations");

    public static readonly PreloadingOperation<Task> CoroutinePreloader =
        Preloader.RegisterPreloader(
            nameof(TestModTwo),
            nameof(Mod.PreloadCoroutine),
            Mod.PreloadCoroutine());

    public static readonly PreloadingOperation<Task> TaskPreloader =
        Preloader.RegisterPreloader(
            nameof(TestModTwo),
            nameof(Mod.PreloadTask),
            Mod.PreloadTask,
            ensureThreadPool: false);

    private Setting? setting;

    public void OnLoad(UpdateSystem updateSystem) {
        Mod.Log.Info(nameof(Mod.OnLoad));

        this.setting = new Setting(this);
        this.setting.RegisterInOptionsUI();

        GameManager.instance.localizationManager.AddSource(
            "en-US", new LocaleEn(this.setting));

        AssetDatabase.global.LoadSettings(
            nameof(TestModTwo), this.setting, new Setting(this));

        using (Mod.DisposablePreloader.Start()) {
            Mod.CoroutinePreloader.Start();
            Mod.TaskPreloader.Start();
        }
    }

    public void OnDispose() {
        Mod.Log.Info(nameof(Mod.OnDispose));

        if (this.setting is not null) {
            this.setting.UnregisterInOptionsUI();
            this.setting = null;
        }
    }

    private static IEnumerator PreloadCoroutine() {
        Mod.Log.Info(nameof(Mod.PreloadCoroutine));

        // Wait between 1 and 3 seconds
        yield return new WaitForSeconds(new Random().Next(6, 9));
    }

    private static async Task PreloadTask() {
        Assert.IsTrue(GameManager.instance.isMainThread);

        Mod.Log.Info(
            $"{nameof(Mod.PreloadTask)}, " +
            $"is main thread = {GameManager.instance.isMainThread}");

        // Wait between 1 and 3 seconds
        await Task.Delay(new Random().Next(6000, 9000));
    }
}
