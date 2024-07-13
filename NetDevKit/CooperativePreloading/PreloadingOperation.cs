using System;
using System.Threading.Tasks;

namespace UrbanDevKit.CooperativePreloading;

public enum OperationState {
    Pending,

    Running,

    Done,

    Failed
}

/// <summary>
/// Base class for preloading operations.
/// See the generic <see cref="PreloadingOperation{TStartReturn}"/> and its
/// subclasses for specific implementations.
/// </summary>
public abstract class PreloadingOperation {
    /// <summary>
    /// Triggered when the operation fails, whether via
    /// <see cref="IDisposableWithException.Dispose(System.Exception?)"/> or
    /// via a wrapped task/coroutine.
    /// </summary>
    public event Action<Exception>? OnException;

    /// <summary>
    /// Not null if <see cref="State"/> is <see cref="OperationState.Failed"/>.
    /// </summary>
    public Exception? Exception { get; private set; }

    /// <summary>
    /// Current state of the operation.
    /// </summary>
    public OperationState State {
        get => this.stateValue;
        private set {
            this.stateValue = value;
            Preloader.SignalStateChange(this);
        }
    }

    /// <summary>
    /// Time in milliseconds that the operation took from "Running" to "Done" or
    /// "Failed".
    /// 0 if the operation is still pending or running.
    /// </summary>
    public int DurationMilliseconds { get; internal set; }

    /// <summary>
    /// Whether the operation has an exception handler attached via
    /// <see cref="PreloadingOperation{TStartReturn}.CatchExceptions"/>.
    /// </summary>
    internal bool HasExceptionHandler => this.OnException is not null;

    /// <summary>
    /// A GUID that uniquely identifies this operation.
    /// </summary>
    internal string Id { get; }

    internal string ModName { get; }

    internal string OperationName { get; }

    private OperationState stateValue;

    private int startMilliseconds;

    internal PreloadingOperation(string modName, string operationName) {
        this.Id = Guid.NewGuid().ToString();

        this.ModName = modName;

        this.OperationName = operationName;

        this.State = OperationState.Pending;
    }

    protected void SetRunning() {
        if (this.State is OperationState.Running) {
            throw new InvalidOperationException(
                $"Cannot {nameof(PreloadingOperation<object>.Start)}() " +
                $"a preloading operation that is already running.");
        }

        this.startMilliseconds = Environment.TickCount;
        this.State = OperationState.Running;
    }

    protected void SetDone() {
        this.DurationMilliseconds =
            Environment.TickCount - this.startMilliseconds;

        this.State = OperationState.Done;
    }

    protected void SetFailed(Exception ex) {
        this.DurationMilliseconds =
            Environment.TickCount - this.startMilliseconds;

        this.Exception = ex;
        this.State = OperationState.Failed;

        this.OnException?.Invoke(ex);
    }
}

/// <summary>
/// Typed preloading operation that can be started and run.
/// </summary>
public abstract class PreloadingOperation<TStartReturn>(
    string modName,
    string operationName) : PreloadingOperation(modName, operationName) {
    /// <summary>
    /// Starts the operation.
    /// For a Disposable-based operation, returns the disposable object and just
    /// marks the operation as Running.
    /// For a Task or coroutine operation, runs the wrapped Task/coroutine
    /// without blocking, and returns a Task if you wish to follow progress.
    /// </summary>
    public TStartReturn Start() {
        this.SetRunning();

        return this.Run();
    }

    /// <summary>
    /// Chainable utility method to attach an exception handler on
    /// <see cref="PreloadingOperation.OnException"/> to the operation when
    /// creating it.
    /// </summary>
    public PreloadingOperation<TStartReturn> CatchExceptions(
        Action<Exception> onException) {
        this.OnException += onException;

        return this;
    }

    protected abstract TStartReturn Run();
}

public interface IDisposableWithException : IDisposable {
    public void Dispose(Exception? ex);
}

/// <summary>
/// Preloading operation that allows to work with the <see cref="IDisposable"/>
/// pattern, with the ability to fail the operation by calling the
/// <see cref="IDisposableWithException.Dispose(System.Exception?)"/> overload.
/// </summary>
public sealed class DisposablePreloadingOperation(
    string modName,
    string operationName)
    : PreloadingOperation<IDisposableWithException>(modName, operationName) {
    protected override IDisposableWithException Run() {
        return new Disposable(this);
    }

    public class Disposable(
        DisposablePreloadingOperation operation) : IDisposableWithException {
        public void Dispose() {
            operation.SetDone();
        }

        public void Dispose(Exception? ex) {
            if (ex is not null) {
                operation.SetFailed(ex);
            }
            else {
                operation.SetDone();
            }
        }
    }
}

/// <summary>
/// Preloading operation that runs a Task or Task-wrapped coroutine.
/// </summary>
public sealed class TaskPreloadingOperation(
    string modName,
    string operationName,
    Func<Task> taskFn,
    bool ensureThreadPool) : PreloadingOperation<Task>(modName, operationName) {
    protected override Task Run() {
        var task = ensureThreadPool ? Task.Run(taskFn) : taskFn();

        return task.ContinueWith(_ => {
            if (task.IsFaulted) {
                var ex = task.Exception!.GetBaseException();

                this.SetFailed(ex);

                throw ex;
            }

            this.SetDone();
        }, TaskContinuationOptions.ExecuteSynchronously);
    }
}
