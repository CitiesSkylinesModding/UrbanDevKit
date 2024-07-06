using System;
using System.Runtime.CompilerServices;
using Colossal.Logging;

namespace UrbanDevKit.Internals;

/// <summary>
/// A logger that prefixes messages with the UDK version and a feature scope.
/// </summary>
internal class UDKLogger(string scope) {
    private const bool ShowsErrorsInUI = true;

    private static readonly ILog Log = LogManager
        .GetLogger(nameof(UrbanDevKit))
        .SetEffectiveness(Level.All)
        .SetShowsErrorsInUI(UDKLogger.ShowsErrorsInUI);

    private static readonly string AssemblyName =
        typeof(UDKLogger).Assembly.GetName().Name;

    internal void Verbose(string message) {
        UDKLogger.Log.Verbose(this.Format(message));
    }

    internal void Info(string message) {
        UDKLogger.Log.Info(this.Format(message));
    }

    internal void Warn(string message) {
        UDKLogger.Log.Warn(this.Format(message));
    }

    internal void Error(string message, bool inUI = true) {
        UDKLogger.Log.showsErrorsInUI = inUI;
        UDKLogger.Log.Error(this.Format(message));
        UDKLogger.Log.showsErrorsInUI = UDKLogger.ShowsErrorsInUI;
    }

    internal void Error(Exception exception, string message, bool inUI = true) {
        UDKLogger.Log.showsErrorsInUI = inUI;
        UDKLogger.Log.Error(exception, this.Format(message));
        UDKLogger.Log.showsErrorsInUI = UDKLogger.ShowsErrorsInUI;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string Format(string message) {
        return $"[{UDKLogger.AssemblyName}] [{scope}] {message}";
    }
}
