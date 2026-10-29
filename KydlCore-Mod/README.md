# KydlCore

Shared runtime for all KydlDev mods: one UI root and theme, the KY hub window (F6; the one menu of every KydlDev mod) and the KY-Items category and registry.

## Features
- (fill in)

## How to use
- Build pieces of this mod are in the hammer's **KY-Items** category.

## Configuration
`BepInEx/config/com.kydldev.KydlCore.cfg` (or the in-game config manager):

| Section | Setting | Default | What it does |
|---------|---------|---------|--------------|
| General | EnableAll | true | Switches the whole mod on / off while playing |
| DebugLog | Enable | false | Detailed lines in the BepInEx log for bug reports |

Edits to the file are picked up while the game runs.

## Multiplayer
Every player needs the mod (same minor version).

## Bug reports
Turn on `[DebugLog] Enable`, reproduce the problem, and attach `BepInEx/LogOutput.log`.
