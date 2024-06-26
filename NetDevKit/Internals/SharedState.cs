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
/// </summary>
internal static class SharedState {
    private const string SharedAssemblyName = "UrbanDevKitSharedState";

    private const string SharedClassName = "SharedState";

    private const string SharedFieldName = "state";

    /// <summary>
    /// Access to the shared state dictionary.
    /// </summary>
    private static readonly Dictionary<string, object?> State =
        (SharedState.GetSharedStateClass()
            .GetField(
                SharedState.SharedFieldName,
                BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null) as Dictionary<string, object?>)!;

    /// <summary>Like the Dictionary method, but thread-safe.</summary>
    public static bool ContainsKey(string key) {
        lock (SharedState.State) {
            return SharedState.State.ContainsKey(key);
        }
    }

    /// <summary>Like the Dictionary method, but thread-safe.</summary>
    public static bool TryGetValue<TValue>(string key, out TValue? value) {
        lock (SharedState.State) {
            if (SharedState.State.TryGetValue(key, out var obj) &&
                obj is TValue objValue) {
                value = objValue;

                return true;
            }

            value = default;

            return false;
        }
    }

    /// <summary>
    /// Like <see cref="Dictionary{TKey,TValue}.this"/> accessor, but
    /// thread-safe.
    /// </summary>
    public static void SetValue<TValue>(string key, TValue value) {
        lock (SharedState.State) {
            SharedState.State[key] = value;
        }
    }

    /// <summary>
    /// Gets the shared state class type and creates it if it doesn't exist.
    /// </summary>
    private static Type GetSharedStateClass() {
        var udkVersion = Assembly.GetExecutingAssembly().GetName().Name;

        // Try to find the shared state assembly.
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(assembly =>
                assembly.GetName().Name == SharedState.SharedAssemblyName);

        // If it was already created by another UDK assembly, return the shared
        // state class type.
        if (assembly is not null) {
            UrbanDevKit.Log.Debug(
                $"[{udkVersion}] Assembly {SharedState.SharedAssemblyName} was already created.");

            return assembly.GetType(SharedState.SharedClassName);
        }

        // Otherwise, create the shared state assembly.
        UrbanDevKit.Log.Debug(
            $"[{udkVersion}] Creating {SharedState.SharedAssemblyName} assembly...");

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
}
