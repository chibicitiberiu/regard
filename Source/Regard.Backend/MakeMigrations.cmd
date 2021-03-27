rem Make sure dotnet-ef is installed: dotnet tool install --global dotnet-ef

mkdir Migrations\SqlServer
dotnet ef migrations add %1 --context SQLServerDataContext --output-dir Migrations\SqlServer

mkdir Migrations\SQLite
dotnet ef migrations add %1 --context SQLiteDataContext --output-dir Migrations\SQLite

rem TODO: add for other DB types