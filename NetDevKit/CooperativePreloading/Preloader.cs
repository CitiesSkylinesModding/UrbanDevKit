using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Colossal.IO.AssetDatabase;
using Colossal.PSI.Common;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Assets;
using Game.SceneFlow;
using Game.UI;
using Game.UI.Menu;
using Unity.Entities;
using UrbanDevKit.Internals;
using UrbanDevKit.Utils;
using Hash128 = Colossal.Hash128;
using PortableOperation =
    System.Collections.Generic.IReadOnlyDictionary<string, string>;
using PortableOperationMap =
    System.Collections.Generic.List<System.Collections.Generic.
        IReadOnlyDictionary<string, string>>;

namespace UrbanDevKit.CooperativePreloading;

/// <summary>
/// A class that helps with cooperative preloading across multiple mods.
/// Multiple mods can register themselves as preloading, and UDK will ensure
/// that the user doesn't load a game until all mods are done preloading.
/// <br />
/// <a href="https://github.com/CitiesSkylinesModding/UrbanDevKit/wiki/Cooperative-Preloading">
/// Documentation
/// </a>
/// </summary>
public static class Preloader {
    /// <summary>
    /// Vanilla notification system. Lazy was not necessary in testing, but
    /// still, we'll play nice just in case.
    /// </summary>
    private static readonly Lazy<NotificationUISystem> NotificationUISystem =
        new(() =>
            World.All[0].GetOrCreateSystemManaged<NotificationUISystem>()
        );

    private static readonly UDKLogger Log = new(nameof(CooperativePreloading));

    /// <summary>
    /// List of operations that are local to the current UDK assembly, as
    /// different versions can be loaded at the same time.
    /// Shortly after mods loaded an all UDK assemblies are initialized, we
    /// empty this list and copy its contents to <see cref="SharedOperations"/>
    /// where all UDK assemblies put and share their own operations.
    /// </summary>
    private static readonly PortableOperationMap LocalOperations = [];

    /// <summary>
    /// <para>
    /// If the user tried to load a game using the launcher's Continue button
    /// or the CLI argument to load a specific save, this will contain the
    /// asset of the save game that was requested to be autoloaded.
    /// The initializer method is the one that prevents the autoloading.
    /// </para>
    /// <para>
    /// Initialized by the most recent UDK assembly, a frame after mods
    /// initialization via:
    /// => static ctor
    /// => <see cref="LateInitialize"/>
    /// => <see cref="CopyLocalToSharedOperations"/>
    /// => <see cref="OnSharedOperationsChanged"/>.
    /// </para>
    /// </summary>
    private static readonly SharedState.StateValueAccessor<IAssetData?>
        RequestedAutoLoad = SharedState.GetValueAccessor(
            nameof(UrbanDevKit),
            "CooperativePreloading.RequestedAutoLoad",
            (1, Preloader.PreventAutoLoad));

    /// <summary>
    /// <para>
    /// Just like <see cref="LocalOperations"/>, but shared across all UDK
    /// assemblies, the latest version of UDK winning the initialization.<br/>
    /// Also wrapped in a <see cref="ValueBinding{T}"/> to communicate the list
    /// of operations to the UI.
    /// </para>
    /// <para>
    /// Initialized by the most recent UDK assembly, a frame after mods
    /// initialization via:
    /// => static ctor
    /// => <see cref="LateInitialize"/>
    /// => <see cref="CopyLocalToSharedOperations"/>.
    /// </para>
    /// </summary>
    private static readonly SharedState.StateValueAccessor<
            ValueBinding<PortableOperationMap>>
        SharedOperations = SharedState.GetValueAccessor(
            nameof(UrbanDevKit),
            "CooperativePreloading.SharedOperations",
            (1, Preloader.CreateOperationsBinding));

    /// <summary>
    /// <para>
    /// A method to be used by all UDK assemblies to signal changes to the
    /// shared operations list. Closest we have to an Action event...
    /// </para>
    /// <para>
    /// Initialized by the most recent UDK assembly, a frame after mods
    /// initialization via:
    /// => static ctor
    /// => <see cref="LateInitialize"/>
    /// => <see cref="CopyLocalToSharedOperations"/>.
    /// </para>
    /// </summary>
    private static readonly SharedState.StateValueAccessor<Action>
        SharedOperationsChanged = SharedState.GetValueAccessor<Action>(
            nameof(UrbanDevKit),
            "CooperativePreloading.SharedOperationsChanged",
            (1, () => Preloader.OnSharedOperationsChanged));

    /// <summary>
    /// Static constructor that registers <see cref="LateInitialize"/> to be
    /// executed once in the next frame.
    /// </summary>
    static Preloader() {
        GameManager.instance.RegisterUpdater(Preloader.LateInitialize);
    }

    /// <summary>
    /// <para>
    /// Create a new preloading operation whose lifecycle is managed manually by
    /// the mod author using <see cref="PreloadingOperation{TStartReturn}.Start"/>
    /// and <see cref="IDisposableWithException.Dispose(Exception?)"/>.
    /// It is the most flexible solution for complex preloading flows.
    /// As it exposed a <see cref="IDisposable"/> it can be used in a
    /// <c>using(operation.Start()) { ... }</c> block.
    /// </para>
    /// <para>
    /// To signal an error, pass an exception to
    /// <see cref="IDisposableWithException.Dispose(Exception?)"/>, which will:
    /// <list type="bullet">
    /// <item>Cause the preloading operation to fail,</item>
    /// <item>Be displayed in the preloading notification,</item>
    /// <item>Be logged,</item>
    /// <item>
    /// Show a popup error dialog to the user, unless the mod author uses
    /// <see cref="PreloadingOperation{TStartReturn}.CatchExceptions"/> to
    /// define a custom error handler.
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="modName">
    /// Mod name, use `nameof(YourModNamespace)` or the actual name.
    /// Displayed to the user in the preloading progress notification.
    /// </param>
    /// <param name="operationName">
    /// Descriptive name of the operation, not currently displayed to the user.
    /// </param>
    public static PreloadingOperation<IDisposableWithException>
        RegisterPreloader(
            string modName,
            string operationName) {
        return new DisposablePreloadingOperation(modName, operationName);
    }

    /// <summary>
    /// <para>
    /// Create a new preloading operation whose lifecycle is partially managed
    /// by UDK. The mod author provides a <see cref="Task{TResult}"/> factory
    /// that UDK will execute and watch for errors and completion, upon call of
    /// <see cref="PreloadingOperation{Task}.Start"/> which is non-blocking.<br/>
    /// The task is executed by default on the thread pool, see
    /// <paramref name="ensureThreadPool"/>.
    /// </para>
    /// <para>
    /// Any error thrown by the task will:
    /// <list type="bullet">
    /// <item>Cause the preloading operation to fail,</item>
    /// <item>Be displayed in the preloading notification,</item>
    /// <item>Be logged,</item>
    /// <item>
    /// Show a popup error dialog to the user, unless the mod author uses
    /// <see cref="PreloadingOperation{TStartReturn}.CatchExceptions"/> to
    /// define a custom error handler.
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="modName">
    /// Mod name, use <c>nameof(YourModNamespace)</c> or the actual name.
    /// Displayed to the user in the preloading progress notification.
    /// </param>
    /// <param name="operationName">
    /// Descriptive name of the operation, not currently displayed to the user.
    /// </param>
    /// <param name="task">
    /// Task factory, ex <c>() => Task.Delay(1000).</c>
    /// </param>
    /// <param name="ensureThreadPool">
    /// Tasks allow you to offload work to the thread pool easily, contrarily to
    /// Unity's coroutines. By default, UDK will run the task on the thread
    /// pool using <see cref="Task.Run(Action)"/>, but you can set false to
    /// execute the task factory on the current thread, which will run the task
    /// on the current thread (ex. the main thread) unless your code itself
    /// does something to run its task on a different thread.
    /// </param>
    public static PreloadingOperation<Task> RegisterPreloader(
        string modName,
        string operationName,
        Func<Task> task,
        bool ensureThreadPool = true) {
        return new TaskPreloadingOperation(
            modName, operationName, task, ensureThreadPool);
    }

    /// <summary>
    /// <para>
    /// Create a new preloading operation whose lifecycle is partially managed
    /// by UDK. The mod author provides a coroutine that UDK will execute and
    /// watch for errors and completion, upon call of
    /// <see cref="PreloadingOperation{Task}.Start"/> which is non-blocking.<br/>
    /// It is recommended to use
    /// <see cref="RegisterPreloader(string, string, Func{Task}, bool)"/>
    /// instead, as coroutines are less flexible and run on the main thread.<br/>
    /// As a side note, the coroutine passed to this method will be wrapped in
    /// a Task and use the task overload of this method, but run the task on the
    /// main thread.
    /// </para>
    /// <para>
    /// Any error thrown by the coroutine will:
    /// <list type="bullet">
    /// <item>Cause the preloading operation to fail,</item>
    /// <item>Be displayed in the preloading notification,</item>
    /// <item>Be logged,</item>
    /// <item>
    /// Show a popup error dialog to the user, unless the mod author uses
    /// <see cref="PreloadingOperation{TStartReturn}.CatchExceptions"/> to
    /// define a custom error handler.
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="modName">
    /// Mod name, use <c>nameof(YourModNamespace)</c> or the actual name.
    /// Displayed to the user in the preloading progress notification.
    /// </param>
    /// <param name="operationName">
    /// Descriptive name of the operation, not currently displayed to the user.
    /// </param>
    /// <param name="coroutine">
    /// Coroutine method, ex.
    /// <c>RegisterPreloader(string, string, Mod.Coroutine())</c>.
    /// Note that due to how <see cref="IEnumerator"/>-returning methods work,
    /// no code is executed until <see cref="PreloadingOperation{Task}.Start"/>
    /// is called, so yes you should call the method in <c>RegisterPreloader</c>
    /// itself.
    /// </param>
    public static PreloadingOperation<Task> RegisterPreloader(
        string modName,
        string operationName,
        IEnumerator coroutine) {
        return new TaskPreloadingOperation(
            modName, operationName, () => coroutine.RunAsTask(), false);
    }

    /// <summary>
    /// Called by <see cref="PreloadingOperation"/> when its state changes, to
    /// signal the change to the system.
    /// </summary>
    internal static void SignalStateChange(PreloadingOperation operation) {
        // Operation state change logging.
        if (operation.State == OperationState.Failed) {
            Preloader.Log.Error(
                operation.Exception!,
                $"Error while preloading mod \"{operation.ModName}\", " +
                $"task \"{operation.OperationName}\". " +
                "The mod might be in an inconsistent state.",
                inUI: !operation.HasExceptionHandler);
        }
        else {
            Preloader.Log.Info(
                $"Mod \"{operation.ModName}\", {operation.State} task " +
                $"\"{operation.OperationName}\"" +
                (operation.DurationMilliseconds > 0
                    ? $" in {operation.DurationMilliseconds}ms."
                    : "."));
        }

        // Update the list of operations.
        lock (SharedState.State) {
            // If the shared operations list is initialized, update this one.
            if (Preloader.SharedOperations.IsInitialized) {
                // Update list...
                var binding = Preloader.SharedOperations.Value;

                binding.value.RemoveAll(op => op["id"] == operation.Id);

                var portableOp = operation.ToPortable();
                binding.value.Add(portableOp);

                // ...then trigger UI update and notify the most recent UDK
                // assembly that the list changed.
                binding.TriggerUpdate();
                Preloader.SharedOperationsChanged.Value();
            }

            // Otherwise, update the assembly-local list.
            else {
                Preloader.LocalOperations.RemoveAll(
                    op => op["id"] == operation.Id);

                var portableOp = operation.ToPortable();
                Preloader.LocalOperations.Add(portableOp);
            }
        }
    }

    /// <summary>
    /// Copy the local operations to the shared operations list.
    /// Executed once in the next frame after all mods, i.e. all UDK assemblies
    /// are loaded and registered, so the most recent version of the UDK
    /// assembly will win the initialization of shared state values.
    /// </summary>
    private static void LateInitialize() {
        lock (SharedState.State) {
            Preloader.CopyLocalToSharedOperations();
        }
    }

    /// <summary>
    /// Prevents autoload of a save game if some mods are preloading and
    /// initializes the <see cref="RequestedAutoLoad"/> shared state value.
    /// </summary>
    /// <returns></returns>
    private static IAssetData? PreventAutoLoad() {
        var gm = GameManager.instance;

        // If there are no operations, there's no need to prevent autoloading.
        if (Preloader.SharedOperations.Value.value.Count == 0) {
            return null;
        }

        // The game mode at this point should be:
        // - MainMenu if the user just launched the game normally,
        // - Other if the user clicked Continue in the launcher (autoload).
        //   However, it seems that it will also be Other when the user launches
        //   the game to the main menu BUT there is a cloud data conflict in the
        //   playset.
        if (gm.gameMode != GameMode.MainMenu && gm.gameMode != GameMode.Other) {
            Preloader.ShowUnexpectedLoadingSequenceStateError();

            return null;
        }

        // It is important to catch exceptions here, to not brick the process if
        // something goes wrong, which could happen if some game internals
        // change, so if it fails we display an error, but let the process
        // continue.
        try {
            IAssetData? saveGame = null;

            // startGame is a CLI argument to load a specific save game by GUID.
            if (gm.configuration.startGame.isValid) {
                AssetDatabase.global.TryGetAsset(
                    gm.configuration.startGame, out saveGame);
            }

            // continuelastsave is a CLI argument to load the last save game.
            if (gm.configuration.continuelastsave) {
                saveGame = gm.settings.userState.lastSaveGameMetadata;
            }

            // Inhibit --continuelastsave.
            gm.configuration.continuelastsave = false;

            // Same thing for startGame, although a little less obvious.
            // In GameManager.Initialize(), the boot sequence is roughly like
            // that (for the part that interests us - semi pseudocode):
            // ```
            // InitializeModManager(); // loads mods
            //
            // MoreCode();
            //
            // if (!configuration.startGame.isValid && !configuration.continuelastsave)
            //     MainMenu();
            //
            // MoreCode();
            //
            // var success = true;
            // if (configuration.startGame.isValid)
            //     success = AutoLoad(configuration.startGame);
            // else if (configuration.continuelastsave)
            //     success = AutoLoad(settings.userState.lastSaveGameMetadata);
            // if (!success)
            //     MainMenu();
            // ```
            //
            // Ideally we would like to make the first condition not true, but
            // mods are asynchronously loaded and initialize *after* this first
            // condition, so UDK's PreventAutoLoad() executes too late.
            // (Except in the case where there is a PDX Cloud conflict, in which
            // case mods are loaded sooner, but we can't rely on that lol).
            // So now we need to make AutoLoad() fail so it sets the success
            // flag to false, causing the last condition to show the main menu.
            // For this, we set a GUID that is not an existing valid save game
            // GUID. This way we force the second condition to be true (as the
            // GUID is technically isValid, i.e. not 0-0-0-0), but AutoLoad()
            // will fail to find the save game and our main menu will be shown.
            gm.configuration.startGame = new Hash128('n', 'o', 'p', 'e');

            if (saveGame is null) {
                return null;
            }

            Preloader.Log.Info(
                $"Prevented auto load of save \"{saveGame.name}\" " +
                $"(guid={saveGame.id}) because some mods are preloading.");

            return saveGame;
        }
        // Something in the game internals changed, show an error warning about
        // autoloading and preloading but let the process continue.
        catch (Exception ex) {
            Preloader.Log.Error(
                ex,
                "Failed to prevent auto load of a save game to let mods " +
                "execute their preloading operations. " +
                "If you see this message, please report it on the Cities " +
                "Skylines: Modding Discord or at " +
                "github.com/CitiesSkylinesModding/UrbanDevKit, and do not " +
                "use the launcher's \"Continue\" button.");

            return null;
        }
    }

    /// <summary>
    /// Initializer for <see cref="SharedOperations"/>.
    /// Needs to be run on .NET mods init, before the UI mods are initialized,
    /// or the binding will not work (even with Attach()).
    /// </summary>
    private static ValueBinding<PortableOperationMap>
        CreateOperationsBinding() {
        var binding = new ValueBinding<PortableOperationMap>(
            "udk.cooperativePreloading",
            "operations",
            [],
            new ListWriter<PortableOperation>(
                new ReadOnlyDictionaryWriter<string, string>()));

        binding.Attach(GameManager.instance.userInterface.view.View);

        return binding;
    }

    /// <summary>
    /// Copy the local operations to the shared operations list once every UDK
    /// assembly is loaded and the most recent version of UDK is able to
    /// initialize <see cref="SharedOperations"/>.
    /// </summary>
    private static void CopyLocalToSharedOperations() {
        var binding = Preloader.SharedOperations.Value;

        binding.value.AddRange(Preloader.LocalOperations);
        Preloader.LocalOperations.Clear();

        // Signal the change to the UI and to the UDK assembly managing the
        // shared list.
        binding.TriggerUpdate();
        Preloader.SharedOperationsChanged.Value();
    }

    /// <summary>
    /// Called only in the most recent UDK assembly, when the shared operations
    /// list changes.
    /// It updates the UI notification and, if all operations are done, it
    /// resumes the autoloading of the save game if any was requested.
    /// </summary>
    private static void OnSharedOperationsChanged() {
        const string identifier =
            $"{nameof(UrbanDevKit)}.{nameof(CooperativePreloading)}.{nameof(Preloader)}";

        var notifications = Preloader.NotificationUISystem.Value;

        lock (SharedState.State) {
            var operations = Preloader.SharedOperations.Value.value;

            var autoLoadedSaveGame = Preloader.RequestedAutoLoad.Value;

            var isThereAnyError = operations
                .Select(op => op["state"].ToState())
                .Any(state => state == OperationState.Failed);

            var doneOrFailedCount = operations
                .Select(op => op["state"].ToState())
                .Count(state =>
                    state is OperationState.Done or OperationState.Failed);

            var areAllOperationsFinished =
                doneOrFailedCount == operations.Count;

            var title = areAllOperationsFinished
                ? isThereAnyError
                    ? "Some mods failed to preload"
                    : "Mods preloaded successfully"
                : autoLoadedSaveGame is not null
                    ? $"Delayed auto load of “{autoLoadedSaveGame.name}”"
                    : "Please wait, some mods are preloading…";

            var text = autoLoadedSaveGame is not null
                ? isThereAnyError
                    ? "Auto loading was cancelled. " +
                      "Check for errors in notifications/logs."
                    : areAllOperationsFinished
                        ? "All mods preloaded, auto loading will resume now!"
                        : "Save game loading will resume as soon as they’re done."
                : isThereAnyError
                    ? "Check for errors in notifications, or logs."
                    : areAllOperationsFinished
                        ? "Ready to play!"
                        : "Waiting for: " + string.Join(", ", operations
                            .Where(op =>
                                op["state"].ToState() == OperationState.Running)
                            .Select(op => op["modName"])
                            .Distinct());

            // AddOrUpdateNotification updates all properties of a notification
            // of the given identifier, *except* the title and onClicked params.
            // As we change both, we need to reset the notification first.
            // Note this does *not* cause a flicker, as the notification is
            // removed and re-added synchronously, it just causes a little extra
            // work.
            notifications.RemoveNotification(identifier);

            notifications.AddOrUpdateNotification(
                identifier,
                title, text,
                progressState: areAllOperationsFinished
                    ? isThereAnyError
                        ? ProgressState.Failed
                        : ProgressState.Complete
                    : ProgressState.Progressing,
                progress:
                (int)((float)doneOrFailedCount / operations.Count * 100),
                onClicked: !(areAllOperationsFinished && isThereAnyError)
                    ? null
                    : () => notifications.RemoveNotification(identifier));

            // We're not done yet, wait for the next call...
            if (!areAllOperationsFinished) {
                return;
            }

            // We're done and there are no errors, remove the notification after
            // a short delay.
            if (!isThereAnyError) {
                notifications.RemoveNotification(identifier, 2);
            }

            // We're done and there are no errors, resume the autoloading if
            // there was any requested.
            if (!isThereAnyError && autoLoadedSaveGame is not null) {
                Preloader.RequestedAutoLoad.Value = null;
                Preloader.ResumeAutoLoad(autoLoadedSaveGame);
            }
        }
    }

    /// <summary>
    /// Resumes the autoloading of a save game that was prevented by the system
    /// to let mods execute preloading operations.
    /// </summary>
    private static void ResumeAutoLoad(IAssetData asset) {
        var gameMode = GameManager.instance.gameMode;

        // Unexpected state, we don't know what to do, log an error and do
        // nothing.
        if (gameMode != GameMode.MainMenu) {
            Preloader.Log.Error(
                $"Unexpected game mode when entering {nameof(Preloader.ResumeAutoLoad)}, " +
                $"expected {GameMode.MainMenu}, got {gameMode}.",
                inUI: false);

            return;
        }

        // Does the same thing as GameManager.AutoLoad().
        GameManager.instance.RunOnMainThread(() => {
            GameManager.instance.Load(
                GameMode.Game,
                asset is MapData or MapMetadata
                    ? Purpose.NewGame
                    : Purpose.LoadGame,
                asset);
        });
    }

    /// <summary>
    /// Converts a <see cref="PreloadingOperation"/> to a "portable operation",
    /// which is a dictionary so that all UDK assemblies can share data as they
    /// all have a different version of the <see cref="PreloadingOperation"/>
    /// type.<br/>
    /// Also, a dictionary is natively serializable so this is also what the UI
    /// will receive through the <see cref="SharedOperations"/> binding.
    /// </summary>
    private static PortableOperation ToPortable(
        this PreloadingOperation operation) {
        return new Dictionary<string, string> {
            ["id"] = operation.Id,
            ["modName"] = operation.ModName,
            ["operationName"] = operation.OperationName,
            ["state"] = operation.State.ToString()
        };
    }

    /// <summary>
    /// Converts a string to an <see cref="OperationState"/>.
    /// </summary>
    private static OperationState ToState(this string stateString) {
        Enum.TryParse<OperationState>(stateString, out var state);

        return state;
    }

    /// <summary>
    /// Extracted error handling for unexpected conditions in
    /// <see cref="PreventAutoLoad"/>.
    /// </summary>
    private static void ShowUnexpectedLoadingSequenceStateError() {
        var ex = new Exception(
            $"Unexpected game mode when entering {nameof(Preloader.PreventAutoLoad)}, " +
            $"Expected {GameMode.MainMenu} or {GameMode.Other}, " +
            $"got {GameManager.instance.gameMode}.");

        Preloader.Log.Error(ex, "Loading sequence issue.", inUI: false);

        ErrorDialogManager.ShowErrorDialog(new ErrorDialog {
            localizedTitle = "Do not use launcher's Continue button!",
            localizedMessage =
                "This is a message from mods. Some mods require preloading " +
                "data before you launch a game. " +
                "It seems we were unable to prevent auto-loading of your " +
                "save in time for this, and you may experience issues. " +
                "We recommend closing the game and restarting.",
            errorDetails = ex.ToString(),
            actions = ErrorDialog.Actions.Quit
        });
    }

    /// <summary>
    /// The native writer for dictionaries declares itself for
    /// <see cref="IDictionary{TKey,TValue}"/> even though it does not write to
    /// the dictionary, so it could have used
    /// <see cref="IReadOnlyDictionary{TKey,TValue}"/>. This class fixes that.
    /// </summary>
    private class ReadOnlyDictionaryWriter<TK, TV> :
        DictionaryWriter<TK, TV>,
        IWriter<IReadOnlyDictionary<TK, TV>> {
        public void Write(
            IJsonWriter writer,
            IReadOnlyDictionary<TK, TV> value) {
            this.Write(writer, (IDictionary<TK, TV>)value);
        }
    }
}
