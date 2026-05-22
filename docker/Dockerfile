FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    git \
    nginx \
    redis-server \
    curl \
    dos2unix \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY start.sh /app/start.sh
COPY nginx.conf /app/nginx.conf

RUN dos2unix /app/start.sh && chmod +x /app/start.sh

EXPOSE 8080 5000

RUN mkdir -p /var/run/nginx

CMD ["bash", "/app/start.sh"]