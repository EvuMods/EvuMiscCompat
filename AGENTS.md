# Agent Instructions

These instructions apply to the EvuMiscCompat repository.

## Read Order

1. Read [README.md](README.md) for what the mod does.
2. Read [ROADMAP.md](ROADMAP.md) before adding a feature that is listed as later work.
3. Read [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) for the toolchain, project split, and verification.
4. Read [docs/CONFIGURATION.md](docs/CONFIGURATION.md) before adding settings.

## Product Boundary

EvuMiscCompat is a collection of compatibility fixes. The first fix restores Dual Wield's offhand equip when Trinkets Unbound 0.3.0 has shifted the `Humanoid.EquipItem` compare. The repair calls Dual Wield's `CheckDualOneHandedWeaponEquip`. It does not reimplement skill, spear, or exclusion rules.

The mod loads on a dedicated server and on every client. Dual Wield (`org.bepinex.plugins.dualwield`), Trinkets Unbound (`jello.trinketsunbound`), and Venture Location Reset (`com.orianaventure.mod.LocationReset`) are soft dependencies. Jotunn is not a dependency.

The player dungeon reset is the `resetdungeon` chat command. It runs only when Venture Location Reset is loaded, and `Location reset` / `Enabled` turns it off. The other settings for that command are in [docs/CONFIGURATION.md](docs/CONFIGURATION.md). On a server that has this mod, clients follow the server's values.

## Project Split

- `src/EvuMiscCompat.Core` is `netstandard2.0` and has no Unity types. The EquipItem window rule and the dungeon-reset command rules live here so they can be tested without the game.
- `src/EvuMiscCompat` is the `net48` BepInEx plugin. It patches `Humanoid.EquipItem` at `Priority.Last` and resolves Dual Wield's private method at runtime. The player dungeon reset resolves Venture Location Reset's `TryReset` the same way.
- `tests/EvuMiscCompat.Core.Tests` covers the core. In-game checks are manual.

## Verification

Run `make verify` after code changes. That fetches reference assemblies when needed, builds the solution, and runs the tests.

In-game installs are `make install`, which reads `GALE_PROFILE` from gitignored `.local.mk`.

Do not commit `.refs/`, `bin/`, `obj/`, or `dist/`. Valheim and BepInEx binaries stay out of git.

## Commits

Use [Conventional Commits](CONTRIBUTING.md). release-please opens release pull requests from those messages. A `fix: rebuild against Valheim <buildid>` commit is reserved for the game-update workflow.
