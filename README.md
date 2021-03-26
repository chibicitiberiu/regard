# Regard

Regard is a self hosted personal video management platform, which allows you to keep track of, download automatically and watch the content you want to see.



## Development setup

Required software:

* **Visual Studio 2019** with the ***ASP.NET and web development*** and ***.NET Core cross-platform development*** workloads installed
* **SQL Server** (any edition should work); **SQL Server Management Studio** is recommended for server management.
* **Python 3.x**
* [**Web Compiler**](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.WebCompiler) extension for Visual Studio, which compiles `.scss ` files
* **Entity Framework CLI** which can be installed by running the command: `dotnet tool install --global dotnet-ef`

Steps:

1. Clone the repository
2. Create a new database in SQL Server
3. Set the connection string in `Regard.Backend\appsettings.json`. At the moment, only SQL Server is supported.
4. (optional) Modify data and download directories in `Regard.Backend\appsettings.json`.
5. Run migrations, by running `Regard.Backend\MigrateSQLServer.cmd`
6. Open `Regard.sln` in Visual Studio
7. Set both the `Regard.Frontend` and `Regard.Backend` projects as startup projects; to do this, right clicking the solution, select *Set startup projects...*, select the *Multiple startup projects* option, and set the action for both projects to *Start*.
8. Run project.

