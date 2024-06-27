using System;
using Colossal.Logging;

namespace UrbanDevKit.Internals;

internal class UDKLogger(string scope) {
    private static readonly ILog Log = LogManager
        .GetLogger(nameof(UrbanDevKit))
        .SetEffectiveness(Level.All)
        .SetShowsErrorsInUI(true);

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

    internal void Error(string message) {
        UDKLogger.Log.Error(this.Format(message));
    }

    internal void Error(Exception exception, string message) {
        UDKLogger.Log.Error(exception, this.Format(message));
    }

    private string Format(string message) {
        return $"[{UDKLogger.AssemblyName}] [{scope}] {message}";
    }
}
