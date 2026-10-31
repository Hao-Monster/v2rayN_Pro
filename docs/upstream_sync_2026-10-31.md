# Upstream integration 2026-10-31

- Product baseline: `6180812c578dab938f6fba5868d4d23aaa648c06` (`sync/upstream-7.24.4-custom.1`, includes the earlier dashboard/network-recovery work).
- Target: official stable `7.25.6`, commit `1883178b5f1e222b6c6db2782e7a3d989b7cc18f`.
- Integration branch: `sync/upstream-7.25.6-custom.1`; version `7.25.6-custom.1`.
- Published history is retained. `upstream` push URL is disabled; rerere is enabled locally.

## Resolution

Adopt upstream ReactiveUI source generators/RxVoid, cancellation-aware download and speed-test APIs, and TUnit test runner. Retain the custom subscription response metadata/HWID headers, onboarding, network recovery, home dashboard, static/GIF backgrounds, CFST workflows, country/region display and macOS TUN cleanup. Subscription editors expose new official request-header/core fields alongside the custom identity handling. Explicit subscription headers override generated defaults.

## Validation

- Windows x64 Release build passed.
- macOS ARM64 Release cross-build passed.
- ServiceLib tests: 165 passed, 0 failed, 0 skipped. Includes response metadata/header precedence regression. The HTTP test fixture now tolerates its own listener-close cancellation race.
- No installed client was replaced, and no release was published.

## Not run

Native Windows/macOS VPN, system-proxy/TUN, actual subscription-server and CFST network acceptance; macOS signing/notarization and runtime. Cross-building does not verify those behaviors. Use `docs/upstream_upgrade_checklist.md` before promotion/release.
