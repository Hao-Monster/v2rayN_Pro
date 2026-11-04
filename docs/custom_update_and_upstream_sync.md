# Custom update and upstream synchronization

## Goals

- Never install an official v2rayN GUI package over the customized client.
- Continue receiving official Xray, Mihomo, sing-box, GeoData, and security fixes.
- Make every upstream integration reviewable, testable, and reversible.

## Repository model

- `upstream`: read-only remote for `2dust/v2rayN`.
- `origin`: `Hao-Monster/v2rayN_Pro`.
- `custom/main`: protected, releasable custom branch.
- `sync/upstream-X.Y.Z-custom.N`: temporary upstream integration branch.
- `feature/*` and `fix/*`: scoped custom development branches.

Published custom branches must not be rebased. Integrate an official stable tag
with a merge commit so the upstream boundary remains visible.

## Version model

`X.Y.Z-custom.N` means:

- `X.Y.Z`: integrated official v2rayN stable version.
- `N`: custom release revision on that base.

Example: `7.23.4-custom.2` is newer than `7.23.4-custom.1`, while
`7.24.0-custom.1` is newer than every `7.23.4-custom.N` release.

## Update channels

The v2rayN GUI checks releases from this fork. Core components retain their
official release repositories. A custom release contains:

- `v2rayN-windows-64.zip` for Windows installation and in-app update.
- `v2rayN-macos-arm64.zip` for macOS in-app update.
- `v2rayN-macos-arm64.dmg` for a new macOS installation.
- `SHA256SUMS.txt` for package verification.

## Upstream workflow

1. `monitor-upstream-release.yml` checks the latest official stable release.
2. It creates one issue when `.github/UPSTREAM_VERSION` is behind.
3. Create a synchronization branch from `custom/main`.
4. Fetch upstream tags and merge the selected stable tag.
5. Resolve conflicts while preserving both upstream security behavior and
   custom product behavior.
6. Run unit tests and both custom package workflows.
7. Merge the PR into `custom/main`.
8. Tag the merge commit as `X.Y.Z-custom.N`.
9. `release-custom.yml` builds, validates, and publishes all update assets.

## Manual commands

```bash
git fetch upstream --tags
git switch custom/main
git pull --ff-only origin custom/main
git switch -c sync/upstream-X.Y.Z-custom.N
git merge --no-ff X.Y.Z
```

After validation and merge:

```bash
git tag -a X.Y.Z-custom.N -m "v2rayN Pro X.Y.Z-custom.N"
git push origin X.Y.Z-custom.N
```

The tag push is the release trigger. Do not create a release from an unmerged
feature branch.
