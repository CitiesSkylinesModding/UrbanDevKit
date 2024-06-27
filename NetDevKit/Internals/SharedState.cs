using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace UrbanDevKit.Internals;

/// <summary>
/// This class is used to share state across multiple UrbanDevKit assemblies
/// of a different major version.
///
/// In a normal time, with only one assembly, we could store state in static
/// classes, but with multiple assemblies, the same-named static symbols will
/// not resolve to the same types!
///
/// This class uses reflection to create a dynamic assembly on the fly which
/// contains a static field with a dictionary that allows sharing values
/// across any assemblies in the current AppDomain.
///
/// This also means the dynamic assembly and how the dictionary is used MUST NOT
/// be subject to breaking changes unless absolutely necessary.
///
/// ** Implementation details: **
/// When performing multiple operations on the shared state, it's recommended to
/// `lock() {}` the operations on <see cref="State"/>.
/// It was considered to use a ConcurrentDictionary, but as the API consumer
/// can manipulate other complex structures inside the dictionary, it's better
/// that they handle the synchronization themselves.
/// </summary>
internal static class SharedState {
    private const string SharedAssemblyName = "UrbanDevKitSharedState";

    private const string SharedClassName = "SharedState";

    private const string SharedFieldName = "state";

    /// <summary>
    /// Access to the shared state dictionary.
    /// </summary>
    internal static readonly Dictionary<string, object?> State =
        (SharedState.GetSharedStateClass()
            .GetField(
                SharedState.SharedFieldName,
                BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null) as Dictionary<string, object?>)!;

    /// <summary>
    /// Get a typed value accessor for a key in the shared state dictionary.
    /// </summary>
    internal static StateAccessor<TValue> GetValueAccessor<TValue>(
        string key,
        Func<TValue> initializer) {
        return new StateAccessor<TValue>(key, initializer);
    }

    private static readonly UDKLogger Log = new(nameof(SharedState));

    /// <summary>
    /// Gets the shared state class type and creates it if it doesn't exist.
    /// </summary>
    private static Type GetSharedStateClass() {
        // Try to find the shared state assembly.
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                assembly.GetName().Name == SharedState.SharedAssemblyName);

        // If it was already created by another UDK assembly, return the shared
        // state class type.
        if (assembly is not null) {
            SharedState.Log.Verbose(
                $"Assembly {SharedState.SharedAssemblyName} was already created.");

            return assembly.GetType(SharedState.SharedClassName);
        }

        // Otherwise, create the shared state assembly.
        SharedState.Log.Verbose(
            $"Creating {SharedState.SharedAssemblyName} assembly...");

        return SharedState.CreateSharedStateClass();
    }

    /// <summary>
    /// Creates the shared state assembly and its static class.
    /// </summary>
    private static Type CreateSharedStateClass() {
        // Step 1: Create an Assembly and Module.
        var assemblyBuilder =
            AppDomain.CurrentDomain.DefineDynamicAssembly(
                new AssemblyName(SharedState.SharedAssemblyName),
                AssemblyBuilderAccess.Run);

        var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");

        // Step 2: Define a New Type.
        var typeBuilder = moduleBuilder.DefineType(
            SharedState.SharedClassName,
            TypeAttributes.Public);

        // Step 3: Add the state field to the Type.
        typeBuilder.DefineField(
            SharedState.SharedFieldName,
            typeof(Dictionary<string, object>),
            FieldAttributes.Public | FieldAttributes.Static);

        // Step 4: Create the Type.
        var type = typeBuilder.CreateType();

        // Step 5: Set the state dictionary.
        typeBuilder.GetField(
                SharedState.SharedFieldName,
                BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, new Dictionary<string, object>());

        return type;
    }

    /// <summary>
    /// Helper class to initialize, read and write a state key.
    /// <param name="key">A key of <see cref="SharedState.State"/></param>
    /// <param name="initializer">
    /// Function returning the value to initialize the key with, if it's missing
    /// in the state.
    /// </param>
    /// </summary>
    internal class StateAccessor<TValue>(string key, Func<TValue> initializer) {
        internal bool HasValue => SharedState.State.ContainsKey(key);

        internal TValue Value {
            get {
                if (SharedState.State.TryGetValue(key, out var value)) {
                    return (TValue)value!;
                }

                SharedState.State[key] = value = initializer();

                return (TValue)value!;
            }
            set => SharedState.State[key] = value;
        }
    }
}
