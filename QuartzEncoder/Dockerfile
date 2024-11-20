#See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER root
RUN apt-get update -y && apt-get install -y curl ffmpeg
RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/bin/yt-dlp
RUN chmod a+rx /usr/bin/yt-dlp
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["QuartzEncoder/QuartzEncoder.csproj", "QuartzEncoder/"]
RUN dotnet restore "./QuartzEncoder/./QuartzEncoder.csproj"
COPY . .
WORKDIR "/src/QuartzEncoder"
RUN dotnet build "./QuartzEncoder.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./QuartzEncoder.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "QuartzEncoder.dll"]