# EvuMiscCompat

Compatibility fixes that sit beside other mods.

## Features

- With [Dual Wield](https://thunderstore.io/c/valheim/p/Smoothbrain/DualWield/) 1.0.12 and [Trinkets Unbound](https://github.com/Jello22/TrinketsUnbound) 0.3.0 both installed, a second one-handed weapon equips in the offhand again. Either mod alone is left as it is. A second trinket still equips.
- With [Venture Location Reset](https://thunderstore.io/c/valheim/p/VentureValheim/Venture_Location_Reset/) installed, a player can type `resetdungeon` in chat to force-reset the nearest dungeon. The reset is refused while a player is inside the area. `Enabled` in the config turns this command off. Admins still have Venture Location Reset's `resetlocations` console command.

## Installation

Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/). Copy `EvuMiscCompat.dll` and `EvuMiscCompat.Core.dll` into `BepInEx/plugins`. Both files have to sit in the same folder. Install the mod on the client and on a dedicated server.

Dual Wield, Trinkets Unbound, and Venture Location Reset are optional. The offhand repair runs only when Dual Wield is loaded. The dungeon reset command runs only when Venture Location Reset is loaded. Settings for that command are in `BepInEx/config/evu.evumisccompat.cfg`. See [docs/CONFIGURATION.md](docs/CONFIGURATION.md).

## Build

See [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md). `make verify` builds the plugin and runs the tests.

## License

[MIT](LICENSE)
