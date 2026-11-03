# KydlCore - test coverage (spec 12)

| Feature / requirement | Unit | Integration | TestLab | Manual | Status |
|---|---|---|---|---|---|
| Loads, own in-game checks, no errors | - | Manifest, Architecture | selftest | - | Automated |
| Hub opens / closes, F6 key, Escape, input released | HubModel, WindowStack, KeyCaptureRules | - | hub-open-close, hub-key-f6 | KydlCore-Manual hub-look (theme, real keys) | Automated + Manual |
| Mods + tabs in the hub, open straight to a mod tab | KyHub, TabPaging | - | hub-lists-ported-mods, hub-opens-a-mod-tab | - | Automated |
| Settings pages (controls, hot reload, ServerSync read-only) | ConfigControlMap | Settings | - | - | Partly (game side: Gap) |
| Public API stable (mods do not break) | - | PublicApi (approved file) | ecosystem run | - | Automated |
| KY-Items registry (legacy sources) | KyItems (model) | - | ky-items | - | Automated |
| KY-Items contributions: ids, duplicates, withdraw | KyContributionRules | - | ky-items-contribute | - | Automated |
| KY-Items RegisterInGame (Jotunn adds prefab / recipe) | - | - | - | - | Gap (first mod that uses it) |
| No EnableAll, VersionCheckOnly + Patch | - | Architecture | - | - | Automated |
| Multiplayer (server without KydlCore, exact version) | - | - | - | real 2-player | Gap |
