# Configuration

EvuMiscCompat writes `BepInEx/config/evu.evumisccompat.cfg` when the plugin loads. The offhand repair has no settings. The `Location reset` section controls the player chat command, and only when [Venture Location Reset](https://thunderstore.io/c/valheim/p/VentureValheim/Venture_Location_Reset/) is installed.

On a server that has this mod, clients use the server's values for every entry below. A client's own file does not override them. In singleplayer, and on the listen host, the local file is the source. A multiplayer client whose server does not have this mod cannot use the command.

## Location reset

| Entry | Default | Purpose |
| --- | --- | --- |
| Enabled | true | Turns the `resetdungeon` chat command on or off. Off leaves resets to Venture Location Reset, including the admin `resetlocations` console command. |
| Cooldown minutes | 10 | How long a player must wait after a force reset before the next one. `0` disables the limit. A refused reset does not start the timer. |
| Require confirmation | On nearby activity | `Always` asks before every reset. `On nearby activity` asks when a grave, a player-built piece, or a terrain change is inside the area that would be wiped. `Never` skips the dialog. A player inside that area blocks the reset in every mode. |
| Search radius | 20 | How close the player must be to a dungeon entrance, in meters. Clamped from 1 to 100. |

The wipe radius is the dungeon's own exterior radius, not this search radius. Sky interiors use the dungeon generator's size. See the readme for what the command refuses to reset.
