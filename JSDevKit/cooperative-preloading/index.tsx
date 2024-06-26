import type { ModuleRegistry } from 'cs2/modding';

export function registerCooperativePreloading(
    moduleRegistry: ModuleRegistry
): void {
    console.info('UDK: Got module registry:', moduleRegistry);
}
