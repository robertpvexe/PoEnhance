#!/usr/bin/env bash
# Idempotent Cloud Agent setup for PoEnhance.
#
# PoEnhance targets .NET 10. PoEnhance.App / PoEnhance.App.Tests are WPF and
# target net10.0-windows, so they only build and run on Windows. This script
# provisions the .NET 10 SDK and prepares the cross-platform projects
# (GameData, Core, DataImport, DataTool and their test projects), which build,
# test and run on the Linux Cloud Agent.
set -euo pipefail

DOTNET_INSTALL_DIR="/usr/local/dotnet"
DOTNET_CHANNEL="10.0"

log() { printf '\n=== %s ===\n' "$1"; }

log "Provisioning .NET ${DOTNET_CHANNEL} SDK"
if [ ! -x "${DOTNET_INSTALL_DIR}/dotnet" ]; then
  tmp_script="$(mktemp)"
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "${tmp_script}"
  chmod +x "${tmp_script}"
  sudo mkdir -p "${DOTNET_INSTALL_DIR}"
  sudo "${tmp_script}" --channel "${DOTNET_CHANNEL}" --install-dir "${DOTNET_INSTALL_DIR}"
  rm -f "${tmp_script}"
else
  echo "SDK already present at ${DOTNET_INSTALL_DIR}; skipping download."
fi

# Make `dotnet` available on PATH for every shell (login and non-login).
sudo ln -sf "${DOTNET_INSTALL_DIR}/dotnet" /usr/local/bin/dotnet

# Expose DOTNET_ROOT and disable first-run noise for interactive shells.
sudo tee /etc/profile.d/dotnet.sh >/dev/null <<EOF
export DOTNET_ROOT="${DOTNET_INSTALL_DIR}"
export PATH="\$PATH:${DOTNET_INSTALL_DIR}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
EOF

export DOTNET_ROOT="${DOTNET_INSTALL_DIR}"
export PATH="${PATH}:${DOTNET_INSTALL_DIR}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

dotnet --info | head -5

# Cross-platform projects. Building the four test projects transitively
# restores and builds every cross-platform library (GameData, Core,
# DataImport, DataTool). The WPF App is intentionally excluded on Linux.
CROSS_PLATFORM_TEST_PROJECTS=(
  "PoEnhance.GameData.Tests/PoEnhance.GameData.Tests.csproj"
  "PoEnhance.Core.Tests/PoEnhance.Core.Tests.csproj"
  "PoEnhance.DataImport.Tests/PoEnhance.DataImport.Tests.csproj"
  "PoEnhance.DataTool.Tests/PoEnhance.DataTool.Tests.csproj"
)

log "Restoring cross-platform projects"
for project in "${CROSS_PLATFORM_TEST_PROJECTS[@]}"; do
  dotnet restore "${project}"
done

log "Building cross-platform projects (Release)"
for project in "${CROSS_PLATFORM_TEST_PROJECTS[@]}"; do
  dotnet build "${project}" -c Release --no-restore
done

log "PoEnhance Cloud Agent setup complete"
