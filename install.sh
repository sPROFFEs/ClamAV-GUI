#!/usr/bin/env bash
set -e

REPO="sPROFFEs/ClamAV-GUI"
GITHUB_API="https://api.github.com/repos/${REPO}/releases"

# Colors for terminal output
BOLD="\033[1m"
GREEN="\033[32m"
BLUE="\033[34m"
YELLOW="\033[33m"
RED="\033[31m"
RESET="\033[0m"

echo -e "${BOLD}${BLUE}=== ClamAV GUI Installer ===${RESET}"

# 1. Detect OS
OS="$(uname -s)"
ARCH="$(uname -m)"

case "${OS}" in
    Linux)
        OS_TAG="linux"
        ;;
    Darwin)
        OS_TAG="osx"
        ;;
    *)
        echo -e "${RED}Unsupported Operating System: ${OS}${RESET}"
        echo "For Windows, run the PowerShell one-liner:"
        echo "  irm https://raw.githubusercontent.com/${REPO}/main/install.ps1 | iex"
        exit 1
        ;;
esac

# 2. Detect Architecture
case "${ARCH}" in
    x86_64|amd64)
        ARCH_TAG="x64"
        ;;
    aarch64|arm64)
        ARCH_TAG="arm64"
        ;;
    *)
        echo -e "${RED}Unsupported architecture: ${ARCH}${RESET}"
        exit 1
        ;;
esac

ASSET_PATTERN="ClamAV-GUI-.*-${OS_TAG}-${ARCH_TAG}\.tar\.gz"

# 3. Find latest release asset URL
echo -e "Detecting latest release for ${BOLD}${OS_TAG}-${ARCH_TAG}${RESET}..."

RELEASE_JSON=$(curl -sSL -H "Accept: application/vnd.github.v3+json" "${GITHUB_API}" || true)

DOWNLOAD_URL=$(echo "${RELEASE_JSON}" | grep -o "https://github.com/${REPO}/releases/download/[^\"]*${OS_TAG}-${ARCH_TAG}\.tar\.gz" | head -n 1 || true)

if [ -z "${DOWNLOAD_URL}" ]; then
    # Fallback to current release tag
    DOWNLOAD_URL="https://github.com/${REPO}/releases/download/v2.0.0-beta.1/ClamAV-GUI-v2.0.0-beta-${OS_TAG}-${ARCH_TAG}.tar.gz"
fi

echo -e "Downloading from: ${BLUE}${DOWNLOAD_URL}${RESET}"

# 4. Create temporary directory
TMP_DIR=$(mktemp -d)
cleanup() {
    rm -rf "${TMP_DIR}"
}
trap cleanup EXIT

ARCHIVE_PATH="${TMP_DIR}/clamav-gui.tar.gz"
curl -sSL -o "${ARCHIVE_PATH}" "${DOWNLOAD_URL}"

# 5. Determine installation paths
INSTALL_DIR="${HOME}/.local/share/clamav-gui"
BIN_DIR="${HOME}/.local/bin"

mkdir -p "${INSTALL_DIR}"
mkdir -p "${BIN_DIR}"

echo -e "Extracting files to ${INSTALL_DIR}..."
tar -xzf "${ARCHIVE_PATH}" -C "${INSTALL_DIR}"
chmod +x "${INSTALL_DIR}/ClamAVGui.App"

# 6. Create binary symlink
ln -sf "${INSTALL_DIR}/ClamAVGui.App" "${BIN_DIR}/clamav-gui"

# 7. Desktop entry & Icon registration
if [ "${OS_TAG}" = "linux" ]; then
    APPS_DIR="${HOME}/.local/share/applications"
    ICONS_DIR="${HOME}/.local/share/icons/hicolor/256x256/apps"
    mkdir -p "${APPS_DIR}"
    mkdir -p "${ICONS_DIR}"

    if [ -f "${INSTALL_DIR}/Assets/icon.png" ]; then
        cp "${INSTALL_DIR}/Assets/icon.png" "${ICONS_DIR}/clamav-gui.png"
    fi

    DESKTOP_FILE="${APPS_DIR}/clamav-gui.desktop"
    cat > "${DESKTOP_FILE}" <<EOF
[Desktop Entry]
Type=Application
Version=1.0
Name=ClamAV GUI
GenericName=Antivirus Scanner
Comment=Cross-platform graphical interface for ClamAV Antivirus
Exec="${INSTALL_DIR}/ClamAVGui.App" %F
Icon=clamav-gui
Terminal=false
Categories=Utility;Security;System;
Keywords=antivirus;clamav;virus;scanner;malware;
StartupNotify=true
EOF
    chmod +x "${DESKTOP_FILE}"

    # Update desktop database if tool exists
    if command -v update-desktop-database >/dev/null 2>&1; then
        update-desktop-database "${APPS_DIR}" >/dev/null 2>&1 || true
    fi
elif [ "${OS_TAG}" = "osx" ]; then
    # macOS App Bundle Shortcut in ~/Applications
    MAC_APP_DIR="${HOME}/Applications/ClamAV GUI.app/Contents/MacOS"
    mkdir -p "${MAC_APP_DIR}"
    
    cat > "${HOME}/Applications/ClamAV GUI.app/Contents/MacOS/ClamAV GUI" <<EOF
#!/usr/bin/env bash
exec "${INSTALL_DIR}/ClamAVGui.App" "\$@"
EOF
    chmod +x "${HOME}/Applications/ClamAV GUI.app/Contents/MacOS/ClamAV GUI"
fi

echo
echo -e "${GREEN}${BOLD}✓ ClamAV GUI successfully installed!${RESET}"
echo -e "You can launch it from your application menu or by running:"
echo -e "  ${BOLD}clamav-gui${RESET} (or ${INSTALL_DIR}/ClamAVGui.App)"

if [[ ":$PATH:" != *":$HOME/.local/bin:"* ]]; then
    echo
    echo -e "${YELLOW}Note: '${HOME}/.local/bin' is not in your current PATH.${RESET}"
    echo "Add it to your shell config (~/.bashrc or ~/.zshrc):"
    echo "  export PATH=\"\$HOME/.local/bin:\$PATH\""
fi
