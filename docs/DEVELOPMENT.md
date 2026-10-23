# Development

## Layout

- `src/EvuMiscCompat.Core` holds the EquipItem instruction-window rule and the player dungeon-reset rules. It targets `netstandard2.0` and does not reference Unity.
- `src/EvuMiscCompat` is the BepInEx plugin (`net48`). It patches `Humanoid.EquipItem` last and, when Dual Wield is loaded, puts `CheckDualOneHandedWeaponEquip` back on the one-handed compare if that call is missing. When Venture Location Reset is loaded, it also handles the `resetdungeon` chat command and calls that mod's `TryReset`.
- `tests/EvuMiscCompat.Core.Tests` covers the window and the reset rules. In-game checks are manual.

Trinkets Unbound 0.3.0 inserts `ldarg.0`, `ldarg.1`, and `call GetAssignmentType` between the item-type load and `ldc.i4.3` on that compare. Dual Wield 1.0.12 only recognizes the vanilla five-instruction shape, so its offhand call never lands and a second one-handed weapon stays in the right hand. This mod emits the same five instructions Dual Wield would have emitted, using Dual Wield's own method. Skill checks, the spear exclusion, and the exclusion list stay in Dual Wield.

Jotunn is downloaded with the other reference assemblies and is not a package dependency.

## Prerequisites

- .NET SDK 8. `make` uses `~/.dotnet` when that install exists, including when `/usr/bin/dotnet` is only a runtime.
- `make`, `curl`, `unzip`, and `python3`
- Either a local Valheim install (`VALHEIM_INSTALL` pointing at the game directory) or SteamCMD, which `scripts/fetch-refs.sh` downloads for you

The script also downloads the BepInEx pack version in `deps/bepinex-pack.version` and the Jotunn version in `deps/jotunn.version`. Assemblies land in `.refs/`, which is gitignored. `BepInEx.AssemblyPublicizer.MSBuild` publicizes `assembly_valheim.dll` during the plugin build.

Reference assemblies come from the Valheim dedicated server (Steam app 896660). `VALHEIM_INSTALL` copies from a local client instead.

## Commands

```sh
make fetch-refs
make build
make test
make verify
make package
make install
```

`make install` reads `GALE_PROFILE` from gitignored `.local.mk` and copies `dist/EvuMiscCompat/` into that profile.

`make verify` is the gate: fetch references if needed, build, and test. `make package` writes `dist/EvuMiscCompat-<version>.zip` in the Hexium layout: `manifest.json`, `icon.png`, `README.md`, `CHANGELOG.md`, `EvuMiscCompat.dll`, and `EvuMiscCompat.Core.dll` all at the zip root. `icon.png` must be 256×256. `version_number` in the packaged manifest is taken from `version.txt`. BepInExPack is not a manifest dependency; Hexium assumes it and strips that entry on upload.

The release workflow attaches that zip and the two raw DLLs to the GitHub release. Publishing that release also publishes the same version to Thunderstore. A release created from this workflow does not start other workflows by itself, because it uses `GITHUB_TOKEN`, so release-please dispatches the Thunderstore workflow with the new tag. A release published any other way starts that workflow directly. The manual Action remains: leave the tag empty to package the selected branch, or set a release tag such as `v0.1.0`. It publishes team `EvuMods` to the Valheim community with categories Mods, AI Generated, Client-side, Server-side, and the update slug from the `game_category` input (`deep-north-update` when a release starts it). NSFW is off. The service account token belongs in the `TCLI_AUTH_TOKEN` repository secret, not in the repo.

## Version

The release pull request is the only edit of `version.txt` and of `version_number` in `manifest.json`. release-please writes those, plus `CHANGELOG.md`, from the conventional commits since the last release. Merging that pull request tags `vX.Y.Z`. `make package` on that tag reads `version.txt`, so the zip is `EvuMiscCompat-X.Y.Z.zip` and the packaged manifest uses the same number. `Directory.Build.props` reads `version.txt` for the plugin version. A later hand edit of either file is overwritten on the next release.

## Game updates

`.valheim-buildid` is the dedicated-server build this tree was last verified against. The Valheim update workflow compares it with the public Steam build. A green `make verify` opens a pull request whose message is `fix: rebuild against Valheim <buildid>`. The pull request is merged with a non-conventional merge body so the changelog records that rebuild once. A failed build opens a GitHub issue and does not release.

A green compile means the referenced members and the core tests still hold. It does not play the game. Check dual wielding in a client after a game update that you care about.

For that chain to publish on its own, the release-please workflow dispatches itself after merging a release pull request that contains only those rebuild commits. GitHub does not start a new workflow from `GITHUB_TOKEN` alone, so the dispatch is explicit. See `.github/workflows/`.

## In-game check

There is no automated playtest here. After an equip change, confirm:

- Dual Wield alone still equips a second axe in the offhand.
- Dual Wield and Trinkets Unbound together equip a second axe in the offhand.
- Trinkets Unbound still equips a second trinket.
- A spear, or an item on Dual Wield's exclusion list, does not move to the offhand.

After a dungeon-reset change, with Venture Location Reset loaded, confirm:

- `resetdungeon` in chat resets the nearest dungeon and does not appear as a chat line.
- With `On nearby activity`, an empty door resets without a dialog, and a grave or a player-built piece asks first.
- With `Always`, an empty door asks too. With `Never`, a grave does not ask.
- A player inside the dungeon, or another player in the wipe area, refuses the reset in every confirmation mode.
- A second command inside the cooldown refuses.
- `Enabled` set to false turns the chat command off.
- The admin console command `resetlocations` is unchanged.
