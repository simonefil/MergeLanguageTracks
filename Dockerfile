# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION=1.0.0
WORKDIR /src
RUN dotnet tool install -g Microsoft.Web.LibraryManager.Cli
ENV PATH="$PATH:/root/.dotnet/tools"
COPY RemuxForge.Core/ ./RemuxForge.Core/
COPY RemuxForge.Web/ ./RemuxForge.Web/
RUN cd RemuxForge.Web && libman restore && cd ..
RUN dotnet publish RemuxForge.Web/RemuxForge.Web.csproj -c Release -p:Version=${VERSION} -o /app

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ENV LANG=C.UTF-8
RUN apt-get update && apt-get install -y --no-install-recommends \
    mkvtoolnix mediainfo curl xz-utils \
    && curl -L -o /tmp/ffmpeg.tar.xz https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-linux64-gpl.tar.xz \
    && tar -xf /tmp/ffmpeg.tar.xz -C /tmp \
    && cp /tmp/ffmpeg-master-latest-linux64-gpl/bin/ffmpeg /usr/local/bin/ \
    && cp /tmp/ffmpeg-master-latest-linux64-gpl/bin/ffprobe /usr/local/bin/ \
    && rm -rf /tmp/ffmpeg* \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app .
ENV REMUXFORGE_PORT=5000
ENV REMUXFORGE_DATA_DIR=/data
EXPOSE 5000
ENTRYPOINT ["dotnet", "RemuxForge.Web.dll"]
