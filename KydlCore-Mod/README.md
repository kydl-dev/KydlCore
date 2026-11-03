# KydlCore

Shared runtime for all KydlDev mods: one UI root and theme, the KY hub window (F6; the one menu of every KydlDev mod) and the KY-Items category and registry.

## Features
- **KY hub (F6):** one window for every KydlDev mod. Row 1 lists the installed mods, row 2 their tabs (settings, the mod's own pages). Escape closes it; your character does not move while it is open.
- **Settings pages:** every KydlDev mod's player settings as switches, sliders, drop-downs and key pickers, with live reload (a change applies at once).
- **KY-Items:** the hammer category of all KydlDev build pieces and a tab listing every item and piece the installed KydlDev mods add (shown only when such a mod is installed).
- **Shared look:** dark panels, gold accents, one font for every KydlDev menu.

## How to use
- Press **F6** in game (changeable: `[General] HubKey`).
- KydlCore has no gameplay of its own: install it together with the KydlDev mods that need it (your mod manager does this for you).
- KydlCore is always on: it has no on / off switch, because every KydlDev mod relies on it.

## Configuration
`BepInEx/config/com.kydldev.KydlCore.cfg` (or the hub's Settings tab):

| Section | Setting | Default | What it does |
|---------|---------|---------|--------------|
| General | HubKey | F6 | Opens / closes the KY hub (None = no key) |
| DebugLog | Enable | false | Detailed lines in the BepInEx log for bug reports |

Edits to the file are picked up while the game runs.

## Multiplayer
KydlCore can be installed on your game only: you can join a server without it. When the server has it too, both must have exactly the same version.
The KydlDev gameplay mods themselves must be on every player and the server (each says so in its own description).

## For mod makers (KydlDev mods)
Hub tabs: `KyHub.AddTab / AddPage / RemoveTab / HasTab / ConfigEntries`. KY-Items: `KyItems.Contribute(new KyContribution { Id = "<modid>:<item>", ... })`
(duplicate ids are rejected), `KyItems.Withdraw(id)`, `KyItems.All / FindById / Changed`.

## Bug reports
Turn on `[DebugLog] Enable`, reproduce the problem, and attach `BepInEx/LogOutput.log`.
