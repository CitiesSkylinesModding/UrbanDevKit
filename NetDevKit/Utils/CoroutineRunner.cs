using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using System.Threading.Tasks;
using Object = UnityEngine.Object;

namespace UrbanDevKit.Utils;

/// <summary>
/// A simple helper class to run Unity coroutines from anywhere and convert them
/// to <see cref="Task"/>
/// </summary>
public static class CoroutineRunner {
    private static readonly MonoBehaviour CoroutineHolder;

    static CoroutineRunner() {
        CoroutineRunner.CoroutineHolder =
            new GameObject(nameof(CoroutineRunner))
                .AddComponent<MonoComponent>();
    }

    /// <summary>
    /// Run a coroutine from anywhere with a MonoBehaviour created by this class.
    /// Same signature as <see cref="MonoBehaviour.StartCoroutine(IEnumerator)"/>
    /// </summary>
    public static Coroutine Start(IEnumerator coroutine) =>
        CoroutineRunner.CoroutineHolder.StartCoroutine(coroutine);

    /// <summary>
    /// Same signature as <see cref="MonoBehaviour.StopCoroutine(Coroutine)"/>
    /// </summary>
    public static void Stop(Coroutine coroutine) =>
        CoroutineRunner.CoroutineHolder.StopCoroutine(coroutine);

    /// <summary>
    /// Wraps a coroutine in a <see cref="Task"/>.
    /// The task will complete with the last value the coroutine yielded.
    /// The task will throw if the coroutine throws an exception.
    /// The task can be cancelled and the coroutine will be stopped.
    /// </summary>
    public static Task AsTask(
        this IEnumerator coroutine,
        CancellationToken cancellationToken = default) {
        var tcs = new TaskCompletionSource<object?>();

        CoroutineRunner.CoroutineHolder.StartCoroutine(RunCoroutine());

        return tcs.Task;

        IEnumerator RunCoroutine() {
            // We can't use `yield return` in a try-catch block, so that's why
            // the control flow appears a bit more convoluted than it should be.
            while (true) {
                object? current;

                try {
                    if (cancellationToken.IsCancellationRequested) {
                        tcs.SetCanceled();

                        yield break;
                    }

                    if (!coroutine.MoveNext()) {
                        tcs.SetResult(coroutine.Current);

                        yield break;
                    }

                    current = coroutine.Current;
                }
                catch (Exception ex) {
                    if (!cancellationToken.IsCancellationRequested) {
                        tcs.SetException(ex);
                    }

                    yield break;
                }

                yield return current;
            }
        }
    }

    public class MonoComponent : MonoBehaviour {
        private void Awake() {
            Object.DontDestroyOnLoad(this.transform.gameObject);
        }
    }
}
