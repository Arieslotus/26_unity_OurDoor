#!/usr/bin/env sh
set -eu

server_root=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)

if [ -z "${SKYNET_ROOT:-}" ]; then
    echo "SKYNET_ROOT is required. Example: SKYNET_ROOT=/absolute/path/to/skynet $0" >&2
    exit 1
fi

skynet_bin="$SKYNET_ROOT/skynet"
if [ ! -x "$skynet_bin" ]; then
    echo "Skynet executable not found: $skynet_bin" >&2
    exit 1
fi

export SKYNET_ROOT
export LXY_SERVER_ROOT="$server_root"

exec "$skynet_bin" "$server_root/config"
