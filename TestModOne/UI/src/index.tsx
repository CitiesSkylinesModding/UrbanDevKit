import { cooperativePreloading } from '@csmodding/urbandevkit';
import type { ModRegistrar } from 'cs2/modding';

const register: ModRegistrar = moduleRegistry => {
    cooperativePreloading.register(moduleRegistry);
};

// biome-ignore lint/style/noDefaultExport: per api contract
export default register;
