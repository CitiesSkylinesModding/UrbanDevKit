import { registerMod } from '@csmodding/urbandevkit';
import { registerCooperativePreloading } from '@csmodding/urbandevkit/cooperative-preloading';
import type { ModRegistrar } from 'cs2/modding';
import mod from '../mod.json';

const register: ModRegistrar = moduleRegistry => {
    registerMod(mod);
    registerCooperativePreloading(moduleRegistry);
};

// biome-ignore lint/style/noDefaultExport: per api contract
export default register;
