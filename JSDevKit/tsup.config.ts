import * as fs from 'node:fs/promises';
import * as path from 'node:path';
import * as util from 'node:util';
import Bun from 'bun';
import chalk from 'chalk';
import { defineConfig } from 'tsup';
import * as packageJson from './package.json';

// biome-ignore lint/style/noDefaultExport: per api contract
export default defineConfig({
    tsconfig: p('tsconfig.json'),
    entry: [p('index.ts'), p('cooperative-preloading/index.tsx')],
    outDir: p('dist'),
    clean: true,
    define: {
        // biome-ignore lint/style/useNamingConvention: compile-time constant
        UDK_VERSION: JSON.stringify(packageJson.version)
    },
    platform: 'browser',
    format: 'esm',
    target: 'es2022',
    sourcemap: true,
    treeshake: true,
    external: ['react', 'cs2'],
    async onSuccess() {
        // For some reason tsup silences non-Error instance errors in onSuccess
        // (ex. I/O errors), and just exits with code 1, so we will wrap our
        // code to always throw Error instances.
        try {
            await onSuccess();
        } catch (err) {
            if (!(err instanceof Error)) {
                throw new Error(
                    `Non-Error thrown in onSuccess: ${util.inspect(err)}`
                );
            }

            throw err;
        }
    }
});

async function onSuccess(): Promise<void> {
    // Quite a "lot" of custom code to make a perfect bundle!
    // Some tsup features don't work as we'd like (dts files), and we need to do
    // some more custom work.

    const udkPrefix = chalk.blueBright('UDK ');

    // Generate .d.ts files with tsc.
    // TSUP has a { dts: true } option, but it triggers after onSuccess and
    // causes bun to not terminate, experimentalDts does not but I find its
    // output to be quite terrible.
    console.info(`${udkPrefix}${chalk.bold('Generating .d.ts files')}`);

    const tscExitCode = await Bun.spawn(['tsc', '-p', p('tsconfig.json')], {
        stdio: ['inherit', 'inherit', 'inherit'],
        windowsHide: true
    }).exited;

    if (tscExitCode != 0) {
        process.exit(tscExitCode);
    }

    // Copying source files (.ts, etc) to dist is good for DX, first so
    // that the source files are available in the dist directory, and second
    // IDEs like JetBrains's can pick up the .ts file and redirect from the
    // .d.ts file when going to definition.
    console.info(`${udkPrefix}${chalk.bold('Copying source files')}`);

    const exportedPaths = ['./index.ts', ...Object.keys(packageJson.exports)]
        .filter(exportPath => exportPath != '.')
        .map(exportPath => path.resolve(import.meta.dir, exportPath));

    const filesToCopyGlob = new Bun.Glob(p('**/*.{ts,tsx}'));

    for await (let filePath of filesToCopyGlob.scan()) {
        filePath = path.normalize(filePath);

        const fileIsInExportedPath = exportedPaths.some(exportedPath =>
            filePath.startsWith(exportedPath)
        );

        if (fileIsInExportedPath) {
            const dest = path.join(
                import.meta.dir,
                'dist',
                path.relative(import.meta.dir, filePath)
            );

            console.info(`${udkPrefix}Creating ${dest}`);

            await fs.mkdir(path.dirname(dest), { recursive: true });
            await fs.copyFile(filePath, dest);
        }
    }

    // Delete a few files that are undesired in the output.
    console.info(`${udkPrefix}${chalk.bold('Deleting undesired files')}`);

    for await (let filePath of ['tsup.config.d.ts']) {
        filePath = path.join(import.meta.dir, 'dist', filePath);

        console.info(`${udkPrefix}Deleting ${filePath}`);

        await fs.unlink(filePath);
    }
}

/**
 * Gets the path of a file in the directory tsup.config.ts is in.
 * The returned path uses Unix path separators because Tsup doesn't like Windows
 * one, even on Windows.
 */
function p(filePath: string): string {
    return `${import.meta.dir}/${filePath}`.replaceAll('\\', '/');
}
