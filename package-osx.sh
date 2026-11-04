#!/bin/bash

Arch="$1"
OutputPath="$2"
Version="$3"
CFST_VERSION="${CFST_VERSION:-v2.3.5}"
XRAY_VERSION="${XRAY_VERSION:-v26.7.11}"

set -euo pipefail

FileName="v2rayN-${Arch}.zip"
CoreDirectory="v2rayN-${Arch}"
CoreExtractRoot="core-package-${Arch}"
rm -rf "$CoreExtractRoot" "$FileName"
wget -nv -O "$FileName" "https://github.com/2dust/v2rayN-core-bin/raw/refs/heads/master/$FileName"
7z x -y -o"$CoreExtractRoot" "$FileName"
cp -rf "$CoreExtractRoot/$CoreDirectory/." "$OutputPath/"

case "$Arch" in
  macos-arm64)
    CFST_ARCH="arm64"
    XRAY_FILE="Xray-macos-arm64-v8a.zip"
    XRAY_ARCHIVE_SHA256="61f8f74d099098af710fa43613d9934d97b901dee909801d34f496cd463956d1"
    ;;
  macos-64)
    CFST_ARCH="amd64"
    XRAY_FILE="Xray-macos-64.zip"
    XRAY_ARCHIVE_SHA256="d8c116756d3a88a38a833a94bdf8bc801f69243ee888befcb56df8b4f1ec4878"
    ;;
  *)
    echo "Unsupported macOS architecture for CloudflareST: $Arch" >&2
    exit 1
    ;;
esac

# Xray v26.7.11 added Darwin process lookup. Older cores cannot safely run
# the process-based routing rules used by v2rayN's Xray TUN configuration.
XRAY_URL="https://github.com/XTLS/Xray-core/releases/download/${XRAY_VERSION}/${XRAY_FILE}"
XRAY_TEMP="xray-package-${CFST_ARCH}"
rm -rf "$XRAY_TEMP" "$XRAY_FILE"
mkdir -p "$XRAY_TEMP" "$OutputPath/bin/xray"
curl --fail --location --retry 3 --output "$XRAY_FILE" "$XRAY_URL"
echo "${XRAY_ARCHIVE_SHA256}  ${XRAY_FILE}" | shasum -a 256 --check
unzip -q -o "$XRAY_FILE" -d "$XRAY_TEMP"
cp -f "$XRAY_TEMP/xray" "$OutputPath/bin/xray/xray"
chmod 755 "$OutputPath/bin/xray/xray"
XRAY_VERSION_OUTPUT="$("$OutputPath/bin/xray/xray" version | head -n 1)"
echo "Bundled ${XRAY_VERSION_OUTPUT}"
if [[ "$XRAY_VERSION_OUTPUT" != *"${XRAY_VERSION#v}"* ]]; then
  echo "Unexpected Xray version: $XRAY_VERSION_OUTPUT" >&2
  exit 1
fi

CFST_FILE="cfst_darwin_${CFST_ARCH}.zip"
CFST_URL="https://github.com/XIU2/CloudflareSpeedTest/releases/download/${CFST_VERSION}/${CFST_FILE}"
mkdir -p "$OutputPath/bin/cfst"
curl --fail --location --retry 3 --output "$CFST_FILE" "$CFST_URL"
# Extract only runtime files. The upstream archive also contains filenames
# whose legacy encoding is not handled consistently by macOS unzip.
unzip -p "$CFST_FILE" cfst > "$OutputPath/bin/cfst/cfst"
unzip -p "$CFST_FILE" ip.txt > "$OutputPath/bin/cfst/ip.txt"
chmod 755 "$OutputPath/bin/cfst/cfst"

PackagePath="v2rayN-Package-${Arch}"
rm -rf "$PackagePath"
mkdir -p "$PackagePath/v2rayN.app/Contents/Resources"
cp -rf "$OutputPath" "$PackagePath/v2rayN.app/Contents/MacOS"
cp -f "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN.icns" "$PackagePath/v2rayN.app/Contents/Resources/AppIcon.icns"
echo "When this file exists, app will not store configs under this folder" > "$PackagePath/v2rayN.app/Contents/MacOS/NotStoreConfigHere.txt"
chmod +x "$PackagePath/v2rayN.app/Contents/MacOS/v2rayN"
chmod +x "$PackagePath/v2rayN.app/Contents/MacOS/bin/cfst/cfst"

RequiredFiles=(
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/Country.mmdb"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/geoip.dat"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/geoip.metadb"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/geoip-only-cn-private.dat"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/geosite.dat"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/mihomo/mihomo"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/sing_box/sing-box"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/xray/xray"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/cfst/cfst"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/cfst/ip.txt"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/srss/geoip-cn.srs"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/srss/geosite-cn.srs"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/srss/geosite-gfw.srs"
  "$PackagePath/v2rayN.app/Contents/MacOS/bin/srss/geosite-google.srs"
  "$PackagePath/v2rayN.app/Contents/MacOS/guiConfigs/default-workspace-background.gif"
)
for RequiredFile in "${RequiredFiles[@]}"; do
  if [[ ! -f "$RequiredFile" ]]; then
    echo "Missing required macOS runtime file: $RequiredFile" >&2
    exit 1
  fi
done

cat >"$PackagePath/v2rayN.app/Contents/Info.plist" <<-EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleLocalizations</key>
  <array>
    <string>zh-Hans</string>
    <string>zh-Hant</string>
    <string>en</string>
    <string>fa</string>
    <string>fr</string>
    <string>ru</string>
    <string>hu</string>
  </array>
  <key>CFBundleDisplayName</key>
  <string>v2rayN</string>
  <key>CFBundleExecutable</key>
  <string>v2rayN</string>
  <key>CFBundleIconFile</key>
  <string>AppIcon</string>
  <key>CFBundleIconName</key>
  <string>AppIcon</string>
  <key>CFBundleIdentifier</key>
  <string>2dust.v2rayN</string>
  <key>CFBundleName</key>
  <string>v2rayN</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>${Version}</string>
  <key>CSResourcesFileMapped</key>
  <true/>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>LSMinimumSystemVersion</key>
  <string>13.6</string>
</dict>
</plist>
EOF

if [[ -n "${APPLE_CODESIGN_IDENTITY:-}" ]]; then
  codesign --force --deep --options runtime --timestamp \
    --sign "$APPLE_CODESIGN_IDENTITY" "$PackagePath/v2rayN.app"
else
  # Re-sign the completed bundle so newly injected native tools have a coherent signature.
  codesign --force --deep --sign - --timestamp=none "$PackagePath/v2rayN.app"
fi
codesign --verify --deep --strict --verbose=2 "$PackagePath/v2rayN.app"

create-dmg \
    --volname "v2rayN Installer" \
    --window-size 700 420 \
    --icon-size 100 \
    --icon "v2rayN.app" 160 185 \
    --hide-extension "v2rayN.app" \
    --app-drop-link 500 185 \
    "v2rayN-${Arch}.dmg" \
    "$PackagePath/v2rayN.app"
