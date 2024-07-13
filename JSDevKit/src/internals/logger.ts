import { currentMod, version } from './common.js';

export function udkMsg(message: string): string {
    return `UDK[${version}/${currentMod.id}]: ${message}`;
}
