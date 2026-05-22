#!/bin/bash

set -e

REPO_DIR="/app/spotifyPlayerRepo"

echo "Starting redis..."
redis-server --bind 0.0.0.0 --daemonize yes

if [ ! -d "$REPO_DIR" ]; then
    echo "Cloning repository..."
    git clone https://github.com/addeswe/spotifyPlayer "$REPO_DIR"
fi

echo "Configuring and starting nginx..."
cp /app/nginx.conf /etc/nginx/nginx.conf
nginx

echo "Starting spotifyToken..."
cd "$REPO_DIR/spotifyToken"

dotnet restore
dotnet run &

echo "Starting spotifyPlayer..."
cd "$REPO_DIR/spotifyPlayer"

dotnet restore
dotnet run &

echo "Services started."

wait
