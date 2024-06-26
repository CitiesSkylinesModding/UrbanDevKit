let mod: ModDefinition | undefined;

declare const UDK_VERSION: string;

export interface ModDefinition {
    readonly id: string;
    readonly author: string;
}

/**
 * Registers the mod with the Urban Development Kit, to improve debug messages.
 *
 * @example
 * import mod from '../mod.json';
 *
 * const register: ModRegistrar = moduleRegistry => {
 *     registerMod(mod);
 * };
 */
export function registerMod(modJson: ModDefinition): void {
    mod = modJson;

    console.info(udkMsg(`Registered mod (author: ${mod.author})`));
}

export function getMod(): ModDefinition | undefined {
    return mod;
}

export function udkMsg(message: string): string {
    return `UDK[${UDK_VERSION}/${mod?.id || 'Unknown'}]: ${message}`;
}
