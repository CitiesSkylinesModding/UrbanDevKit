using System;

namespace UrbanDevKit.Utils;

public static partial class SharedState {
    /// <summary>
    /// Helper class to initialize, read and write a state key.
    /// Most of the time, you'll want to `lock(<see cref="SharedState.State"/>)`
    /// your read and write operations.
    /// </summary>
    public sealed class StateValueAccessor<TValue> {
        /// <summary>
        /// Key to the value in the shared state dictionary.
        /// </summary>
        private readonly string key;

        private readonly string initializerKey;

        /// <summary>
        /// Gets whether the key is initialized in the shared state, that is if
        /// a mod already read or wrote
        /// <see cref="StateValueAccessor{TValue}.Value"/>.
        /// </summary>
        public bool IsInitialized => SharedState.State.ContainsKey(this.key);

        /// <summary>
        /// Gets the value of the key in the shared state.<br />
        /// If the key was not initialized yet, the initializer function is
        /// run and the value set before it is returned, making the operation
        /// non-atomic.
        /// </summary>
        public TValue Value {
            get {
                this.MaybeInitialize();

                return (TValue)SharedState.State[this.key]!;
            }
            set => SharedState.State[this.key] = value;
        }

        /// <summary>
        /// Behaviour is explained on the public API,
        /// <see cref="SharedState.GetValueAccessor{TValue}"/>.
        /// </summary>
        internal static StateValueAccessor<TValue> For(
            string @namespace,
            string key,
            (ushort Version, Func<TValue> Function) initializer) {
            var accessor = new StateValueAccessor<TValue>(@namespace, key);

            ushort lastInitializerVersion;

            lock (SharedState.State) {
                // Install the initializer if it's not there yet.
                if (!SharedState.State.TryGetValue(
                        accessor.initializerKey, out var initializerObj)) {
                    SharedState.State.Add(accessor.initializerKey, initializer);

                    lastInitializerVersion = initializer.Version;
                }

                // If an initializer was already defined for this key...
                else {
                    var existingInitializer =
                        ((ushort Version, Func<TValue> Function))
                        initializerObj!;

                    // ...and the new initializer is newer...
                    if (initializer.Version >= existingInitializer.Version) {
                        // ...replace the old initializer with the new one.
                        SharedState.State[accessor.initializerKey] =
                            initializer;

                        lastInitializerVersion = initializer.Version;
                    }

                    // ...otherwise, keep the old initializer.
                    else {
                        lastInitializerVersion = existingInitializer.Version;
                    }
                }
            }

            SharedState.Log.Verbose(
                $"Created {nameof(StateValueAccessor<TValue>)} for \"{accessor.key}\" " +
                $"(got initializer ver. {initializer.Version}, " +
                $"use ver. {lastInitializerVersion}).");

            return accessor;
        }

        internal StateValueAccessor(string @namespace, string key) {
            this.key = $"{@namespace}::{key}";
            this.initializerKey = $"{this.key}__Initializer";
        }

        /// <summary>
        /// Initializes the shared value with the initializer function.
        /// Similar to accessing <see cref="StateValueAccessor{TValue}.Value"/>,
        /// but for cases where property access is not desired.
        /// </summary>
        /// <returns>False if the property was already initialized.</returns>
        public bool MaybeInitialize() {
            if (this.IsInitialized) {
                return false;
            }

            // Otherwise, initialize the value and return it.
            var (version, function) =
                ((ushort, Func<TValue>))
                SharedState.State[this.initializerKey]!;

            var value = (TValue)function()!;

            SharedState.State[this.key] = value;

            SharedState.Log.Verbose(
                $"Initialized state value \"{this.key}\" " +
                $"with initializer version {version}.");

            return true;
        }

        /// <summary>
        /// Removes the key from the shared state.
        /// </summary>
        public bool Remove() {
            return SharedState.State.Remove(this.key);
        }
    }
}
