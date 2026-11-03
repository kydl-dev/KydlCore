# KydlCore tests (MIGRATION_DECISIONS A1, C15-C24)

| Folder | What | Run |
|---|---|---|
| `Unit/` | pure rules (Domain / Application / Execution are LINKED, not the DLL), NUnit + FsCheck | `dotnet test tests\Unit` (or `dotnet msbuild KydlCore.csproj -t:UnitTests`) |
| `Integration/` | the built DLL + package contracts (manifest, versions, Hexium, settings, patch targets, suite guards, architecture rules) AND command flows with fake capabilities | `dotnet test tests\Integration` (`-t:IntegrationTests`); release gates: `--filter TestCategory=ReleaseGate` |
| `TestLab/suites/KydlCore/` | DrakesTestLab suite: the real game (hooks in `src/Testing/TestHooks`) | `-t:TestLab` (= `Claude\lessons\scripts\lab-guarded.ps1 -Mod KydlCore`; the lab mod is switched on for the run and ALWAYS off afterwards) |
| `TestLab/suites/KydlCore-Manual/` | checkpoints a person answers (looks, real keys) | F9 > Tests in game |
| `COVERAGE.md` | requirement -> Unit / Integration / TestLab / Manual / Gap | update with every change |

Build the mod first (the tests read `assets\KydlCore.dll`). Lab runs: never build while the game runs; read `Claude\lessons\TESTLAB_PLAYBOOK.md`
and the current lab docs (`%APPDATA%\com.kesomannen.gale\valheim\profiles\test\BepInEx\config\DrakesTestLab\docs`). Every run is archived in
`E:\Projects\Valheim\_testlab\` (results + DLL hashes) and gets a TESTLOG line. Release: 3 green runs of this DLL + 1 ecosystem run (G16).
Everything in this folder stays out of the release zip.
