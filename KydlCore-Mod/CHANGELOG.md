# Changelog

## 1.1.0
- New for mod authors: the flight recorder. `FlightRecorder.Track(component, label, facts)` keeps the last two minutes of a creature or object (position, speed, health, AI target, network owner, whether its zone is loaded, the mod's own facts); `FlightRecorder.Dump(reason)`, `Note`, `Untrack`, `LastDumpPath` and `TrackedCount` complete it. It is always on, in memory only.
- It writes a report file to `BepInEx/cache/KydlCore/flight/` only when something goes wrong: a failed DrakesTestLab test, a CHECK OFF, an error of a KydlDev mod, or a mod's own `FlightRecorder.Dump`. At most 30 files per game start; files older than 3 days and everything beyond the newest 100 are removed at game start. Attach such a file to a bug report.
- New for mod authors: `ModLog.State`, `ModLog.Decision`, `ModLog.Every` and `ModLog.IsDue` (debug lines as before, and always on the flight recorder's timeline, even with the debug log off).
- New settings in the `[Diagnostics]` section (hub Settings tab): `FlightSampleSeconds` (1 s), `FlightWindowSeconds` (120 s), `FlightMaxDumps` (30), `FlightKeepDays` (3), `FlightKeepFiles` (100). They only size the recorder; it cannot be switched off.
- Nothing was removed or renamed: mods built for KydlCore 1.0.0 keep working. A server that runs 1.1.0 needs the same version on every player who has KydlCore (exact match).

## 1.0.0
- Tested in game with the full TestLab suite (7 tests): 3 green runs in a row on 2026-11-05, plus the ecosystem run with all 12 KydlDev mods loaded together.
- New for mod authors: PieceLearning (learn a build piece with the game's normal "new piece" message, check known pieces / items).
- Settings pages: a setting name that is too long is cut with "..." instead of running under its slider, long text values
  (webhook URLs) stay inside their box, and sliders are about 10 % shorter with a gap to the name. New mod icon.
- KydlDev spec layout; KY-Items: Jotunn-free registry with namespaced contributions (KyItems.Contribute / Withdraw / FindById), duplicates rejected; KyHub.HasTab; KydlCore can no longer be switched off (no EnableAll); exact network version, joins servers without KydlCore (VersionCheckOnly). Checked in game on 2026-11-04 (clean start and quit with all mods loaded, no errors from this mod).
- First version (project created 2026-10-29). Checked in game on 2026-11-04 (clean start and quit with all mods loaded, no errors from this mod).
