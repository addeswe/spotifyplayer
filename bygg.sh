#!/bin/bash

export SPOTIFY_CLIENT_ID=din_client_id
export SPOTIFY_CLIENT_SECRET=din_client_secret
cd docker
docker compose up --build
