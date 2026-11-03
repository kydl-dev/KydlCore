# KydlCore.Core - shared pure contracts of every KydlDev mod (MIGRATION_DECISIONS A9)

Part of KydlCore.dll (public types, namespace `KydlCore.Core`; mods reference KydlCore) and LINKED into each mod's unit-test
project (`tests\Unit`). Pure C#: no UnityEngine, HarmonyLib, BepInEx, Jotunn, KydlCore.Common/.Ui or game types - the guard test
`KydlCoreContracts_ArePure` in every mod's integration tests checks it. Logging = an injected `Action<string>` trace (ModLog.Verbose in game).

| Folder | Types |
|---|---|
| Results | `ResponseStatus` (Success / Rejected / Ignored / Failed), `OperationResponse<T>`, `ValidationResult` |
| Pipeline | `OperationPipeline<TIn,TOut>` (validations in order, then one deciding step), `IOperationStep<TIn,TOut>` |
| Commands | `ICommand<TResult>`, `ICommandHandler<TCommand,TResult>`, `CommandDispatcher` (requests a player / UI / timer makes) |
| Features | `IFeature`, `FeatureRegistry` (A4: each feature on / off live with `EnableAll` + its own `Enable`, Start/Stop its hub UI) |
| Time | `IClock` (game time without `UnityEngine.Time`; `ManualClock` for tests) |
| Identity | `PlayerId`, `IOwnership` (owner-gated executors) |

A change here changes every mod on its next build: say so in the handoff. A bug is fixed HERE (PROJECT_RULES 16).
