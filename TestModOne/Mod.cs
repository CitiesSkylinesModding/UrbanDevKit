using System;
using System.Collections;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Colossal.Serialization.Entities;
using Game;
using Game.Modding;
using Game.SceneFlow;
using JetBrains.Annotations;
using Unity.Assertions;
using UnityEngine;
using UrbanDevKit.CooperativePreloading;
using Random = System.Random;

namespace TestModOne;

[UsedImplicitly]
public sealed class Mod : IMod {
    public static readonly ILog Log = LogManager
        .GetLogger(nameof(TestModOne))
        .SetShowsErrorsInUI(true);

    public static readonly PreloadingOperation<IDisposableWithException>
        DisposablePreloader =
            Preloader.RegisterPreloader(
                    nameof(TestModOne),
                    "WrapPreloadingOperations")
                .CatchExceptions(Mod.NotifyFailedPreloading);

    public static readonly PreloadingOperation<Task> CoroutinePreloader =
        Preloader.RegisterPreloader(
                nameof(TestModOne),
                nameof(Mod.PreloadCoroutine),
                Mod.PreloadCoroutine())
            .CatchExceptions(Mod.NotifyFailedPreloading);

    public static readonly PreloadingOperation<Task> TaskPreloader =
        Preloader.RegisterPreloader(
                nameof(TestModOne),
                nameof(Mod.PreloadTask),
                Mod.PreloadTask)
            .CatchExceptions(Mod.NotifyFailedPreloading);

    private Setting? setting;

    public void OnLoad(UpdateSystem updateSystem) {
        Mod.Log.Info(nameof(Mod.OnLoad));

        this.setting = new Setting(this);
        this.setting.RegisterInOptionsUI();

        GameManager.instance.localizationManager.AddSource(
            "en-US", new LocaleEn(this.setting));

        AssetDatabase.global.LoadSettings(
            nameof(TestModOne), this.setting, new Setting(this));

        GameManager.instance.onGameLoadingComplete += OnGameLoadingComplete;

        return;

        async void OnGameLoadingComplete(Purpose purpose, GameMode mode) {
            if (
                mode != GameMode.MainMenu ||
                Mod.DisposablePreloader.State == OperationState.Running) {
                return;
            }

            var disposable = Mod.DisposablePreloader.Start();

            await Task.WhenAll(
                    Mod.CoroutinePreloader.Start(),
                    Mod.TaskPreloader.Start())
                .ContinueWith(task => disposable.Dispose(task.Exception));
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
        yield return new WaitForSeconds(new Random().Next(3, 6));

        throw new Exception("Fail PreloadCoroutine");
    }

    private static async Task PreloadTask() {
        Assert.IsFalse(GameManager.instance.isMainThread);

        Mod.Log.Info(
            $"{nameof(Mod.PreloadTask)}, " +
            $"is main thread = {GameManager.instance.isMainThread}");

        // Wait between 1 and 3 seconds
        await Task.Delay(new Random().Next(3000, 6000));

        throw new Exception("Fail PreloadTask");
    }

    private static void NotifyFailedPreloading(Exception ex) {
        Mod.Log.showsErrorsInUI = false;

        Mod.Log.Warn(
            $"{nameof(TestModOne)} here! I failed a preloading operation " +
            $"(this is just a demo, all is fine).");

        Mod.Log.showsErrorsInUI = true;
    }
}
