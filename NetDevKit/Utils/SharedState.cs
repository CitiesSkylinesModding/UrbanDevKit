using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UrbanDevKit.Internals;

namespace UrbanDevKit.Utils;

/// <summary>
/// <para>
/// This class is used to share state across different assemblies that don't
/// know about each other but know about the SharedState API and what keys it
/// may contain.<br />
/// This can be used by mods to share data without linking to each other.
/// It is used internally by UDK to share state across different UDK versions.
/// <br />
/// In a normal time, with only one UDK assembly, we could store state in static
/// classes, but with multiple UDK assemblies of different versions, the
/// same-named static symbols will not resolve to the same types!
/// </para>
///
/// <para>
/// By the way, this also means that it makes no sense to put objects that are
/// specific to your assembly in the shared state, as the other assemblies
/// won't know about them (they could only work with those values with
/// reflection). So, only put objects that are known to all assemblies, like
/// .NET/Unity/Game types.
/// </para>
///
/// <para>
/// This class uses reflection to create a dynamic assembly on the fly which
/// contains a static field with a dictionary that allows sharing values
/// across any assemblies in the current AppDomain.<br />
/// This also means the dynamic assembly and how the dictionary is used MUST NOT
/// be subject to breaking changes unless absolutely necessary.
/// </para>
///
/// <para>
/// When performing any operation (even read a single value) on the shared
/// state, it's recommended to `lock() {}` the operations on <see cref="State"/>
/// unless you absolutely know what you're doing.<br />
/// It was considered to use a `ConcurrentDictionary`, but as the API consumer
/// can manipulate other complex structures inside the dictionary or perform
/// many operations in a row, it's better that they handle the synchronization
/// themselves.
/// </para>
/// </summary>
public static partial class SharedState {
    private const string SharedAssemblyName = "UrbanDevKitSharedState";

    private const string SharedClassName = "SharedState";

    private const string SharedFieldName = "state";

    /// <summary>
    /// Access to the shared state dictionary.
    /// </summary>
    public static readonly IDictionary<string, object?> State;

    private static readonly UDKLogger Log = new(nameof(SharedState));

    /// <summary>
    /// Initializes the shared state assembly, catching error and fallback to an
    /// assembly-local state if that fails.
    /// </summary>
    static SharedState() {
        SharedState.State =
            SharedState.GetSharedStateClass()
                    .GetField(
                        SharedState.SharedFieldName,
                        BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null) as
                IDictionary<string, object?> ??
            throw new NullReferenceException(
                "Unexpected null shared state dictionary.");
    }

    /// <summary>
    /// Get a typed value accessor for a key in the shared state dictionary.
    /// If you typically need to `lock()` your operations on the shared state,
    /// this is not needed for this method as it always locks the shared state
    /// itself to register initializer functions.
    /// </summary>
    /// <param name="namespace">
    /// Namespace of the key, ex. `nameof(MyMod)`.
    /// </param>
    /// <param name="key">
    /// Key name, ex. "MyKey".
    /// </param>
    /// <param name="initializer">
    /// Initializer function for the value when it is first accessed.
    /// As many assemblies can access the shared state, one of them will have to
    /// initialize the value, so one of them gets to say how the value is first
    /// initialized.<br />
    /// To mitigate compatibility issues and race conditions, the initializer
    /// is versioned with a PATCH version number (i.e. it MUST remain compatible
    /// with other and older implementations).<br />
    /// This give a better chance to the "best" initializer to be the one used.
    /// In order to play nice with this system, delay resolving
    /// <see cref="StateValueAccessor{TValue}.Value"/> until you really need it,
    /// so other assemblies have a chance to propose their initializers.
    /// </param>
    public static StateValueAccessor<TValue> GetValueAccessor<TValue>(
        string @namespace,
        string key,
        (ushort Version, Func<TValue> Function) initializer) {
        return StateValueAccessor<TValue>.For(@namespace, key, initializer);
    }

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
        var type = SharedState.CreateSharedStateClass();

        SharedState.Log.Verbose(
            $"Created {SharedState.SharedAssemblyName} dynamic assembly.");

        return type;
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
            typeof(IDictionary<string, object>),
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
