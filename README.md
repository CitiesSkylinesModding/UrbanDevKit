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
- [Bun](https://bun.sh) as the package manager and partial replacement for Node in the build toolchain.
- Recommended: enable `--developerMode --uiDeveloperMode` as game launch options.

Here's an example of game launch command that also skips the launcher in Steam:

```sh
"C:\Program Files (x86)\Steam\steamapps\common\Cities Skylines II\Cities2.exe" %command% --developerMode --uiDeveloperMode
```

### Building

In development:
- For the UI part: Run `bun dev` to watch for changes in JSDevKit and the two test mods.
- For the .NET part: Hit Build in your favorite IDE.<br>
  This will also build the test mods, but a current limitation is that the JSDevKit
  should be built before, be it with `bun dev:jsdevkit` or `bun build:jsdevkit`.

For a complete build:
- Run `bun build` to build everything UI-related.<br>
  Unlike `bun dev:*`, this will also run the Biome linter and type checking.
- Hit Build in your favorite IDE for the .NET part.

### Publishing

We will keep the npm and NuGet packages versions in sync, even if one of those
projects had no changes.<br>
This will be easier for API consumers to ensure they have properly synchronized SDKs.

We will also use strict [semantic versioning](https://semver.org).

The first time, ensure you are logged in to npm with `npm login` and
NuGet ([download](https://www.nuget.org/downloads)) with `nuget setApiKey YOUR_API_KEY`.

1. The first thing to do is to build the projects.<br>
   The JavaScript SDK is always built in production mode. Run `bun run build`.<br>
   For .NET, ensure you switch to Release mode. Hit Build in your favorite IDE.
2. Launch the game and ensure the test mods are working as expected.
3. Update `NetDevKit/NetDevKit.csproj#Version` and `JSDevKit/package.json#version`
   with the new version number.
4. **If** there is a breaking change, update `NetDevKit/NetDevKit.csproj#AssemblyName`
   (ex. `UrbanDevKitV1` to `UrbanDevKitV2`) so mods can load different versions of the SDK.
5. Update `CHANGELOG.md` with the new version and describe additions and changes.
6. Commit the changes.
7. Run `bun release` to publish the new version to npm and NuGet (@todo).
8. Tag the commit with the new version number.
9. Push the commit and the tag to the repository.
10. Create a GitHub release from the tag, and copy the changelog entry in the description.

The process is a bit manual for now, but at least it gives us control,
and does not necessitate a thousand deps and config files (I tried).<br>
Contributions to automate this in a reasonably simple way are welcome!

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
