import * as path from 'node:path';
import MiniCssExtractPlugin from 'mini-css-extract-plugin';
import mod from './mod.json' with { type: 'json' };

const userDataPath = process.env.CSII_USERDATAPATH;

if (!userDataPath) {
    throw 'CSII_USERDATAPATH environment variable is not set, ensure the CSII Modding Toolchain is installed correctly';
}

const outputDir = `${userDataPath}\\Mods\\${mod.id}`;

// biome-ignore lint/style/noDefaultExport: per api contract
export default {
    mode: 'production',
    stats: 'none',
    entry: {
        [mod.id]: path.resolve(import.meta.dirname, 'src/index.tsx')
    },
    externalsType: 'window',
    externals: {
        react: 'React',
        'react-dom': 'ReactDOM',
        'cs2/modding': 'cs2/modding',
        'cs2/api': 'cs2/api',
        'cs2/bindings': 'cs2/bindings',
        'cs2/l10n': 'cs2/l10n',
        'cs2/ui': 'cs2/ui',
        'cs2/input': 'cs2/input',
        'cs2/utils': 'cs2/utils',
        'cohtml/cohtml': 'cohtml/cohtml'
    },
    module: {
        rules: [
            {
                test: /\.tsx?$/,
                use: 'ts-loader',
                exclude: /node_modules/
            },
            {
                test: /\.s?css$/,
                include: path.join(import.meta.dirname, 'src'),
                use: [
                    MiniCssExtractPlugin.loader,
                    {
                        loader: 'css-loader',
                        options: {
                            url: true,
                            importLoaders: 1,
                            modules: {
                                auto: true,
                                exportLocalsConvention: 'camelCase',
                                localIdentName: '[local]_[hash:base64:3]'
                            }
                        }
                    },
                    'sass-loader'
                ]
            },
            {
                test: /\.(png|jpe?g|gif|svg)$/i,
                type: 'asset/resource',
                generator: {
                    filename: 'images/[name][ext][query]'
                }
            }
        ]
    },
    resolve: {
        extensions: ['.ts', '.tsx']
    },
    output: {
        path: path.resolve(import.meta.dirname, outputDir),
        library: {
            type: 'module'
        },
        publicPath: `coui://ui-mods/`
    },
    experiments: {
        outputModule: true
    },
    plugins: [
        new MiniCssExtractPlugin(),
        {
            apply(compiler) {
                let runCount = 0;
                compiler.hooks.done.tap('AfterDonePlugin', stats => {
                    console.info(stats.toString({ colors: true }));
                    console.info(
                        `\n🔨 ${runCount++ ? 'Updated' : 'Built'} ${mod.id}`
                    );
                    console.info(`   \x1b[90m${outputDir}\x1b[0m\n`);
                });
            }
        }
    ]
};
