<h1 align="center">:cityscape: UrbanDevKit :hammer_and_wrench:</h1>

<p align="center">
    Common libraries and community patterns for faster Cities: Skylines 2 mods development.
</p>

<p align="center">
    <a href="https://discord.gg/SsshDVq2Zj">
        <img alt="npm version" src="https://img.shields.io/badge/Discord-Cities:_Skylines_Modding-5865f2?logo=discord&logoColor=white&style=flat-square">
    </a>
</p>

> Reference maintainer(s): @toverux

## Usage

@todo

## Development

**These are instructions for modifying and building the UrbanDevKit modding libraries,
not the usage documentation of the libraries which is described above.**

### Installation

- Standard CS2 modding toolchain;
- [Bun](https://bun.sh) as a replacement for Node in the build toolchain.
- Recommended: enable `--developerMode --uiDeveloperMode` as game launch options.

Here's an example of game launch command that also skips the launcher in Steam:

```sh
"C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2.exe" %command% --developerMode --uiDeveloperMode
```

## Code style

### TypeScript

TypeScript code is formatted and linted by [Biome](https://biomejs.dev).
Run `bun check` to check for linting errors, format files and autofix simple issues.

You can also use Biome directly with `bun biome`.

The formatter and linter should run as a pre-commit hook if you have it installed,
which should be done automatically when running `bun i` (otherwise run `bun lefthook install`).

I'd suggest to use a Biome plugin for your editor to ease development.

If a rule seems out of place for this project, you can either disable/reconfigure
it in the `biome.json` file or disable it with an annotation comment, but these
should be justified and concerted.

### C#

For C#, prefer using Rider, as code style and linting settings are saved in the project.
Reformat your code before committing (CTRL+ALT+L with Rider).

At the very least, please ensure your IDE has `.editorconfig` support enabled.

### Commit messages

Commits must follow the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0) specification and more
specifically the Angular one.

Scope can be one or more of the following:
- `jsdk`: for changes in UrbanDevKit's UI libraries;
- `netdk`: for changes in UrbanDevKit's .NET libraries;
- `testmod`: for changes in the test mods (don't forget there is also the `test()` commit type);
- Propose new scopes if needed!
