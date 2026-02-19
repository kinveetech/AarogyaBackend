FROM mcr.microsoft.com/devcontainers/dotnet:1-9.0-bookworm

RUN rm -f /etc/apt/sources.list.d/yarn.list \
    && apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
      awscli \
      postgresql-client \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*
