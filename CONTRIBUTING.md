# Contributing

## Commits

This repository uses [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/). release-please reads them to open release pull requests and to update `CHANGELOG.md`.

- `feat:` a new player-facing behavior
- `fix:` a broken behavior
- `chore:` tooling, docs, or maintenance that should not ship as a feature

The game-update workflow uses exactly `fix: rebuild against Valheim <buildid>`. Leave that shape for that workflow.

## Checks

Run `make verify` before opening a pull request. Behavior changes need a unit test in `tests/EvuMiscCompat.Core.Tests`. Equip behavior still needs a pass in the Valheim client. See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

Do not commit `.refs/`, build output, or game binaries.
