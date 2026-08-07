# syntax=docker/dockerfile:1

# ---- ビルド ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 先に csproj だけを入れて restore し、ソース変更でキャッシュが飛ばないようにする。
COPY Directory.Build.props Tendo.sln ./
COPY src/Tendo.Bot/Tendo.Bot.csproj src/Tendo.Bot/
COPY src/Tendo.Game/Tendo.Game.csproj src/Tendo.Game/
COPY src/Tendo.Data/Tendo.Data.csproj src/Tendo.Data/
COPY tests/Tendo.Bot.Tests/Tendo.Bot.Tests.csproj tests/Tendo.Bot.Tests/
COPY tests/Tendo.Game.Tests/Tendo.Game.Tests.csproj tests/Tendo.Game.Tests/
COPY tests/Tendo.Data.Tests/Tendo.Data.Tests.csproj tests/Tendo.Data.Tests/
RUN dotnet restore src/Tendo.Bot/Tendo.Bot.csproj

# マスターデータ (data/*.json) は csproj が参照しているので一緒に入れる。
COPY src/ src/
COPY data/ data/

RUN dotnet publish src/Tendo.Bot/Tendo.Bot.csproj -c Release -o /app --no-restore

# ---- 実行 ----
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# 日本語の表示名やフィールド名を扱うので invariant globalization にはしない。
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# root で動かす必要はない。
RUN useradd --create-home --shell /usr/sbin/nologin tendo
USER tendo

COPY --from=build /app ./

# トークンと接続文字列は環境変数で渡すこと (イメージには焼かない)。
#   docker run -e TENDO_Discord__Token=... -e TENDO_Database__ConnectionString=... tendo
ENTRYPOINT ["dotnet", "Tendo.Bot.dll"]
