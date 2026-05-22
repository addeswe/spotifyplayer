@ECHO OFF
set SPOTIFY_CLIENT_ID=din_client_id
set SPOTIFY_CLIENT_SECRET=din_client_secret
cd docker
docker compose up --build
