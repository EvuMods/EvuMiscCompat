# EvuMiscCompat

Compatibility fixes that sit beside other mods.

## Features

- With [Dual Wield](https://thunderstore.io/c/valheim/p/Smoothbrain/DualWield/) 1.0.12 and [Trinkets Unbound](https://github.com/Jello22/TrinketsUnbound) 0.3.0 both installed, a second one-handed weapon equips in the offhand again. Either mod alone is left as it is. A second trinket still equips.

## Installation

Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/). Copy `EvuMiscCompat.dll` and `EvuMiscCompat.Core.dll` into `BepInEx/plugins`. Both files have to sit in the same folder. Install the mod on the client and on a dedicated server.

Dual Wield and Trinkets Unbound are optional. The offhand repair runs only when Dual Wield is loaded. There are no settings.

## Build

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md). `make verify` builds the plugin and runs the tests.

## License

[MIT](LICENSE)
