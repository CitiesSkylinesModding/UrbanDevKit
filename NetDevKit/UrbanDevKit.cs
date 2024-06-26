﻿using System.Reflection;
using Colossal.Logging;
using UnityEngine.Scripting;

namespace UrbanDevKit;

[Preserve]
internal static class UrbanDevKit {
    public static readonly ILog Log = LogManager
        .GetLogger(nameof(UrbanDevKit))
        .SetEffectiveness(Level.All)
        .SetShowsErrorsInUI(true);

    [Preserve]
    static UrbanDevKit() {
        // Multiple UrbanDevKit assemblies can be loaded: they will have
        // different names suffixed with their major version as this is the only
        // way to load multiple incompatible UrbanDevKit assemblies.
        // We log the loaded assembly for debug and testing purposes, as this
        // static constructor will be called for each assembly, this is useful
        // to track which UDKs are loaded.
        UrbanDevKit.Log.Info(
            $"Loaded assembly {Assembly.GetExecutingAssembly().FullName}");
    }
}