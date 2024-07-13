import type { ModuleRegistry } from 'cs2/modding';
import type React from 'react';
import { featureSingleton } from '../internals/index.js';
import { createMasterScreenExtension } from './master-screen-extension.js';

const cooperativePreloadingFeature = featureSingleton(
    'cooperativePreloading',
    1,
    () => installCooperativePreloading
);

/**
 * Registers the cooperative preloading feature on the frontend, responsible
 * for disabling menu buttons and displaying spinners.
 * Only the mod that provides the most recent version of UDK will actually
 * set up the feature, it will be a no-op for other mods.
 */
export function register(moduleRegistry: ModuleRegistry): void {
    const [cooperativePreloading, inCharge] = cooperativePreloadingFeature();

    if (inCharge) {
        cooperativePreloading(moduleRegistry);
    }
}

function installCooperativePreloading(moduleRegistry: ModuleRegistry) {
    const MasterScreenExtension = createMasterScreenExtension();

    moduleRegistry.extend(
        'game-ui/menu/components/shared/master-screen/master-screen.tsx',
        'MasterScreen',
        MasterScreen => (props: React.PropsWithChildren) => {
            return (
                <MasterScreenExtension original={MasterScreen} props={props} />
            );
        }
    );
}
