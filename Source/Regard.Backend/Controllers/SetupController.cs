using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MoreLinq;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Jellyfin;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Response;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SetupController : ControllerBase
    {
        private readonly UserManager<UserAccount> userManager;
        private readonly IOptionManager optionManager;
        private readonly ApiResponseFactory responseFactory;
        private readonly DataContext dataContext;
        private readonly StorageManager storageManager;
        private readonly IConfiguration configuration;
        private readonly IJellyfinClient jellyfinClient;

        public SetupController(UserManager<UserAccount> userManager, IOptionManager optionManager,
            ApiResponseFactory responseFactory, DataContext dataContext, StorageManager storageManager,
            IConfiguration configuration, IJellyfinClient jellyfinClient)
        {
            this.userManager = userManager;
            this.optionManager = optionManager;
            this.responseFactory = responseFactory;
            this.dataContext = dataContext;
            this.storageManager = storageManager;
            this.configuration = configuration;
            this.jellyfinClient = jellyfinClient;
        }

        [HttpGet]
        [Route("server_status")]
        public async Task<IActionResult> ServerStatus()
        {
            var users = await userManager.GetUsersInRoleAsync(UserRoles.User);
            var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);

            return Ok(responseFactory.Success(new ServerStatusResponse()
            {
                Initialized = optionManager.GetGlobal(Options.Server_Initialized),
                HaveUsers = users.Count > 0,
                HaveAdmin = admins.Count > 0
            }));
        }

        [HttpPost]
        [Route("initialize")]
        public async Task<IActionResult> ServerInitialize()
        {
            List<string> errors = new List<string>();

            // Check we have an admin user
            var users = await userManager.GetUsersInRoleAsync(UserRoles.User);
            if (users.Count == 0)
                errors.Add("No user registered!");

            var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);
            if (admins.Count == 0) 
                errors.Add("No administrator users are present!");

            // Complete setup
            if (errors.Count == 0)
            {
                optionManager.SetGlobal(Options.Server_Initialized, true);
                return Ok(responseFactory.Success());
            }
            else
            {
                string allErrors = errors.Aggregate("Some errors have been encountered", (x, y) => x + "\n* " + y);
                return StatusCode(StatusCodes.Status405MethodNotAllowed, responseFactory.Error(allErrors));
            }

        }

        /// <summary>
        /// Runs first-time-setup sanity checks (DB, storage, downloader dependencies, Jellyfin).
        /// Only available before setup is completed, to avoid leaking configuration afterwards.
        /// </summary>
        [HttpGet]
        [Route("checks")]
        public async Task<IActionResult> Checks()
        {
            if (optionManager.GetGlobal(Options.Server_Initialized))
                return StatusCode(StatusCodes.Status403Forbidden, responseFactory.Error("Setup has already been completed."));

            var checks = new List<SetupCheckResult>();

            // Database
            bool db;
            try { db = await dataContext.Database.CanConnectAsync(); } catch { db = false; }
            checks.Add(new SetupCheckResult
            {
                Name = "Database",
                Status = db ? SetupCheckStatus.Ok : SetupCheckStatus.Error,
                Message = db ? "Connected to the database." : "Could not connect to the database."
            });

            // Data directory (writable)
            var (dataOk, dataErr) = TestWritable(storageManager.DataDirectory);
            checks.Add(new SetupCheckResult
            {
                Name = "Data directory",
                Status = dataOk ? SetupCheckStatus.Ok : SetupCheckStatus.Error,
                Message = dataOk ? $"Writable ({storageManager.DataDirectory})." : $"Not writable: {dataErr}"
            });

            // Download directory (must be absolute, and writable)
            var dl = storageManager.DownloadDirectory ?? "";
            if (!Path.IsPathFullyQualified(dl))
            {
                checks.Add(new SetupCheckResult
                {
                    Name = "Download directory",
                    Status = SetupCheckStatus.Error,
                    Message = $"Must be an absolute path (currently '{dl}'). Downloads and Jellyfin path-matching require it."
                });
            }
            else
            {
                var (dlOk, dlErr) = TestWritable(dl);
                checks.Add(new SetupCheckResult
                {
                    Name = "Download directory",
                    Status = dlOk ? SetupCheckStatus.Ok : SetupCheckStatus.Error,
                    Message = dlOk ? $"Writable ({dl})." : $"Not writable: {dlErr}"
                });
            }

            // python3 (required to run yt-dlp)
            bool py = await CommandSucceeds("python3", "--version");
            checks.Add(new SetupCheckResult
            {
                Name = "python3",
                Status = py ? SetupCheckStatus.Ok : SetupCheckStatus.Error,
                Message = py ? "Found on PATH." : "Not found on PATH (required to run yt-dlp)."
            });

            // ffmpeg (needed for merges + thumbnail conversion)
            bool ff = await CommandSucceeds("ffmpeg", "-version");
            checks.Add(new SetupCheckResult
            {
                Name = "ffmpeg",
                Status = ff ? SetupCheckStatus.Ok : SetupCheckStatus.Warning,
                Message = ff ? "Found on PATH." : "Not found on PATH; video merging and thumbnail conversion will fail."
            });

            // Jellyfin (optional)
            var jfUrl = configuration["Jellyfin:BaseUrl"];
            if (string.IsNullOrWhiteSpace(jfUrl))
            {
                checks.Add(new SetupCheckResult
                {
                    Name = "Jellyfin",
                    Status = SetupCheckStatus.Warning,
                    Message = "Not configured (optional) — Jellyfin watched-sync is disabled."
                });
            }
            else
            {
                bool jf = false;
                try { jf = await jellyfinClient.TestConnectionAsync(); } catch { }
                checks.Add(new SetupCheckResult
                {
                    Name = "Jellyfin",
                    Status = jf ? SetupCheckStatus.Ok : SetupCheckStatus.Warning,
                    Message = jf ? $"Reachable at {jfUrl}." : $"Could not reach Jellyfin at {jfUrl} (check the URL and API key)."
                });
            }

            var response = new SetupChecksResponse
            {
                Checks = checks,
                HasErrors = checks.Any(c => c.Status == SetupCheckStatus.Error)
            };
            return Ok(responseFactory.Success(response));
        }

        private static (bool ok, string error) TestWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var probe = Path.Combine(dir, ".regard-write-test-" + Guid.NewGuid().ToString("N"));
                System.IO.File.WriteAllText(probe, "ok");
                System.IO.File.Delete(probe);
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        private static async Task<bool> CommandSucceeds(string fileName, string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo(fileName, arguments)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };
                using var process = Process.Start(psi);
                if (process == null)
                    return false;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await process.WaitForExitAsync(cts.Token);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
