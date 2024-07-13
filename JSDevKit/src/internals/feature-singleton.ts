import { type ModDefinition, currentMod } from './common.js';
import { udkMsg } from './logger.js';

declare global {
    // noinspection JSUnusedGlobalSymbols
    interface Window {
        // biome-ignore lint/style/useNamingConvention: namespace
        UrbanDevKit?: UrbanDevKit;
    }

    interface UrbanDevKit {
        singletonFeatures?: Record<
            string,
            {
                version: number;
                mod: ModDefinition;
                implementation: () => unknown;
                value?: unknown;
            }
        >;
    }
}

/**
 * Register a versioned singleton feature that can be shared across multiple
 * UDK versions.
 * The most recent version of the feature wins when the singleton is resolved.
 * This means that a feature of a given name must be backwards compatible with
 * previous versions, otherwise it should change name (add a braking change
 * version number directly in the name), making the version number this function
 * takes as an argument only a feature/patch version number.
 *
 * This is the frontend equivalent of UDK's .NET SharedState feature.
 *
 * @return A function to resolve the feature singleton.
 *         The first element of the tuple is the feature instance.
 *         The second element of the tuple is a boolean indicating if the
 *         feature was resolved with the implementation provided by the current
 *         mod, allowing for mods to conditionally execute code based on whether
 *         they are the ones providing the implementation or not.
 */
export function featureSingleton<TFeature extends {}>(
    name: string,
    version: number,
    implementation: () => TFeature
): () => [TFeature, boolean] {
    window.UrbanDevKit ??= {};
    window.UrbanDevKit.singletonFeatures ??= {};

    const existing = window.UrbanDevKit.singletonFeatures[name];

    if (existing && 'value' in existing) {
        console.warn(
            udkMsg(
                `Feature singleton "${name}" already resolved with version ${existing.version} (from mod ${existing.mod.id}).`
            )
        );
    } else if (!existing || existing.version <= version) {
        window.UrbanDevKit.singletonFeatures[name] = {
            version,
            mod: currentMod,
            implementation
        };

        console.debug(
            udkMsg(
                `Registered feature singleton "${name}" with version ${version}.`
            )
        );
    } else {
        console.debug(
            udkMsg(
                `Feature singleton "${name}" already registered with higher version (ver. ${existing.version} from mod ${existing.mod.id}).`
            )
        );
    }

    // Late binding to allow other mods to register possible new versions.
    return (resolveFeature<TFeature>).bind(null, name);
}

function resolveFeature<TFeature>(name: string): [TFeature, boolean] {
    // biome-ignore lint/style/noNonNullAssertion: cannot be null if call is legit
    const feature = window.UrbanDevKit!.singletonFeatures![name]!;

    if (!('value' in feature)) {
        console.debug(
            udkMsg(
                `Resolving feature singleton "${name}" with version ${feature.version} (from mod ${feature.mod.id}).`
            )
        );

        feature.value = feature.implementation();
    }

    return [feature.value as TFeature, feature.mod == currentMod];
}
