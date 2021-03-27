rem Make sure dotnet-ef is installed: dotnet tool install --global dotnet-ef

dotnet ef migrations remove --context SQLServerDataContext
dotnet ef migrations remove --context SQLiteDataContext

rem TODO: add for other DB types