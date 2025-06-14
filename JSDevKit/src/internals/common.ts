import modJson from 'mod.json';
import packageJson from '../../package.json' with { type: 'json' };

export const currentMod = modJson;

export const version = packageJson.version;

export interface ModDefinition {
    readonly id: string;
    readonly author: string;
}
