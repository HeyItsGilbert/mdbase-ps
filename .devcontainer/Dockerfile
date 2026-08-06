FROM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl git \
    && curl --fail --silent --show-error --location \
        https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
        --output /tmp/packages-microsoft-prod.deb \
    && dpkg --install /tmp/packages-microsoft-prod.deb \
    && rm /tmp/packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install --yes --no-install-recommends powershell \
    && rm -rf /var/lib/apt/lists/*

RUN useradd --create-home --shell /bin/bash vscode \
    && mkdir --parents /workspaces/mdbase-ps \
    && chown --recursive vscode:vscode /workspaces

WORKDIR /workspaces/mdbase-ps
USER vscode
