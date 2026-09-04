using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Regard.Backend.Common.Utils;
using Regard.Backend.Configuration;
using Regard.Backend.DB;
using Regard.Backend.Jobs;
using Regard.Backend.Model;
using Regard.Backend.Services;
using Regard.Common.API.Admin;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Regard.Backend.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = UserRoles.Admin)]
    public class AdminController : ControllerBase
    {
        private const long BytesPerGb = 1024L * 1024L * 1024L;
        private const long MbPerGb = 1024L;

        private readonly UserManager<UserAccount> userManager;
        private readonly RoleManager<IdentityRole> roleManager;
        private readonly IOptionManager optionManager;
        private readonly UserQuotaService quotaService;
        private readonly RegardScheduler scheduler;
        private readonly ApiResponseFactory responseFactory;
        private readonly Microsoft.Extensions.Configuration.IConfiguration configuration;
        private readonly Regard.Backend.Common.Services.IYoutubeDlService ytdlService;
        private readonly DatabaseBackupService backupService;
        private readonly DataContext dataContext;
        private readonly LogFileReader logReader;

        public AdminController(UserManager<UserAccount> userManager,
                               RoleManager<IdentityRole> roleManager,
                               IOptionManager optionManager,
                               UserQuotaService quotaService,
                               RegardScheduler scheduler,
                               ApiResponseFactory responseFactory,
                               Microsoft.Extensions.Configuration.IConfiguration configuration,
                               Regard.Backend.Common.Services.IYoutubeDlService ytdlService,
                               DatabaseBackupService backupService,
                               DataContext dataContext,
                               LogFileReader logReader)
        {
            this.userManager = userManager;
            this.roleManager = roleManager;
            this.optionManager = optionManager;
            this.quotaService = quotaService;
            this.scheduler = scheduler;
            this.responseFactory = responseFactory;
            this.configuration = configuration;
            this.ytdlService = ytdlService;
            this.backupService = backupService;
            this.dataContext = dataContext;
            this.logReader = logReader;
        }

        /// <summary>Fixed on-disk location of the uploaded yt-dlp cookies.txt (null if DataDirectory unset).</summary>
        private string CookiesPath()
        {
            var dataDir = configuration["DataDirectory"];
            return string.IsNullOrEmpty(dataDir) ? null : System.IO.Path.Combine(dataDir, "cookies.txt");
        }

        // ---- Server settings ---------------------------------------------------------------

        [HttpGet]
        [Route("settings")]
        public IActionResult GetServerSettings()
        {
            int countQuota = optionManager.GetGlobal(Options.User_CountQuota);
            long sizeQuotaMb = optionManager.GetGlobal(Options.User_SizeQuota);

            var settings = new ApiServerSettings
            {
                AllowRegistrations = optionManager.GetGlobal(Options.Server_AllowRegistrations),
                DefaultVideoQuota = countQuota >= 0 ? countQuota : (int?)null,
                DefaultStorageQuotaGb = sizeQuotaMb >= 0 ? sizeQuotaMb / (double)MbPerGb : (double?)null,
                JobHistoryRetentionDays = optionManager.GetGlobal(Options.Server_JobHistoryRetentionDays),
                MaintenanceEnabled = optionManager.GetGlobal(Options.Server_Maintenance_Enabled),
                MaintenanceIntervalHours = optionManager.GetGlobal(Options.Server_Maintenance_IntervalHours),
                YtdlLogRetentionDays = optionManager.GetGlobal(Options.Server_Maintenance_YtdlLogRetentionDays),
                BackupEnabled = optionManager.GetGlobal(Options.Server_Backup_Enabled),
                BackupKeepCount = optionManager.GetGlobal(Options.Server_Backup_KeepCount),
                ReturnYouTubeDislikeEnabled = optionManager.GetGlobal(Options.ReturnYouTubeDislike_Enabled),
                ThrottleEnabled = optionManager.GetGlobal(Options.Server_Throttle_Enabled),
                SleepRequests = optionManager.GetGlobal(Options.Server_Ytdl_SleepRequests),
                SleepInterval = optionManager.GetGlobal(Options.Server_Ytdl_SleepInterval),
                MaxSleepInterval = optionManager.GetGlobal(Options.Server_Ytdl_MaxSleepInterval),
                LimitRate = optionManager.GetGlobal(Options.Server_Ytdl_LimitRate),
                Impersonate = optionManager.GetGlobal(Options.Server_Ytdl_Impersonate),
                ImpersonateTargets = ytdlService.ImpersonateTargets.ToArray(),
                CookiesConfigured = CookiesPath() is string cp && System.IO.File.Exists(cp),
                DownloadMinSeconds = optionManager.GetGlobal(Options.Server_Throttle_DownloadMinSeconds),
                DownloadMaxSeconds = optionManager.GetGlobal(Options.Server_Throttle_DownloadMaxSeconds),
                ExtractMinSeconds = optionManager.GetGlobal(Options.Server_Throttle_ExtractMinSeconds),
                ExtractMaxSeconds = optionManager.GetGlobal(Options.Server_Throttle_ExtractMaxSeconds),
                MaxPerHour = optionManager.GetGlobal(Options.Server_Throttle_MaxPerHour),
                MaxPerDay = optionManager.GetGlobal(Options.Server_Throttle_MaxPerDay),
                PerHostConcurrency = optionManager.GetGlobal(Options.Server_Throttle_PerHostConcurrency),
                MaxParallelJobs = configuration.GetValue("REGARD_MAX_PARALLEL_JOBS", 1),
            };
            return Ok(responseFactory.Success(settings));
        }

        [HttpPost]
        [Route("settings")]
        public IActionResult SaveServerSettings([FromBody] ApiServerSettings request)
        {
            optionManager.SetGlobal(Options.Server_AllowRegistrations, request.AllowRegistrations);
            optionManager.SetGlobal(Options.User_CountQuota, request.DefaultVideoQuota ?? -1);
            optionManager.SetGlobal(Options.User_SizeQuota,
                request.DefaultStorageQuotaGb.HasValue ? (long)(request.DefaultStorageQuotaGb.Value * MbPerGb) : -1);
            optionManager.SetGlobal(Options.Server_JobHistoryRetentionDays, request.JobHistoryRetentionDays);
            optionManager.SetGlobal(Options.Server_Maintenance_Enabled, request.MaintenanceEnabled);
            optionManager.SetGlobal(Options.Server_Maintenance_IntervalHours, request.MaintenanceIntervalHours);
            optionManager.SetGlobal(Options.Server_Maintenance_YtdlLogRetentionDays, request.YtdlLogRetentionDays);
            optionManager.SetGlobal(Options.Server_Backup_Enabled, request.BackupEnabled);
            optionManager.SetGlobal(Options.Server_Backup_KeepCount, request.BackupKeepCount);

            optionManager.SetGlobal(Options.ReturnYouTubeDislike_Enabled, request.ReturnYouTubeDislikeEnabled);
            optionManager.SetGlobal(Options.Server_Throttle_Enabled, request.ThrottleEnabled);
            optionManager.SetGlobal(Options.Server_Ytdl_SleepRequests, request.SleepRequests);
            optionManager.SetGlobal(Options.Server_Ytdl_SleepInterval, request.SleepInterval);
            optionManager.SetGlobal(Options.Server_Ytdl_MaxSleepInterval, request.MaxSleepInterval);
            optionManager.SetGlobal(Options.Server_Ytdl_LimitRate, request.LimitRate ?? "");
            optionManager.SetGlobal(Options.Server_Ytdl_Impersonate, request.Impersonate ?? "");
            optionManager.SetGlobal(Options.Server_Throttle_DownloadMinSeconds, request.DownloadMinSeconds);
            optionManager.SetGlobal(Options.Server_Throttle_DownloadMaxSeconds, request.DownloadMaxSeconds);
            optionManager.SetGlobal(Options.Server_Throttle_ExtractMinSeconds, request.ExtractMinSeconds);
            optionManager.SetGlobal(Options.Server_Throttle_ExtractMaxSeconds, request.ExtractMaxSeconds);
            optionManager.SetGlobal(Options.Server_Throttle_MaxPerHour, request.MaxPerHour);
            optionManager.SetGlobal(Options.Server_Throttle_MaxPerDay, request.MaxPerDay);
            optionManager.SetGlobal(Options.Server_Throttle_PerHostConcurrency, request.PerHostConcurrency);

            // Cookies file: null = leave as-is; "" = remove; non-empty = replace (atomic temp+move).
            if (request.CookiesFileContent != null)
            {
                var path = CookiesPath();
                if (path == null)
                    return BadRequest(responseFactory.Error("Server DataDirectory is not configured; cannot store cookies."));

                if (request.CookiesFileContent.Length == 0)
                {
                    if (System.IO.File.Exists(path))
                        System.IO.File.Delete(path);
                    optionManager.SetGlobal(Options.Server_Ytdl_CookiesFile, "");
                }
                else
                {
                    var tmp = path + ".tmp";
                    System.IO.File.WriteAllText(tmp, request.CookiesFileContent);
                    System.IO.File.Move(tmp, path, overwrite: true);
                    optionManager.SetGlobal(Options.Server_Ytdl_CookiesFile, path);
                }
            }

            return Ok(responseFactory.Success());
        }

        // ---- Database maintenance ----------------------------------------------------------

        [HttpGet]
        [Route("maintenance")]
        public IActionResult GetMaintenanceStatus()
        {
            var status = backupService.GetStatus();

            DateTimeOffset? lastSweep = null;
            var stored = optionManager.GetGlobal(Options.Server_Maintenance_LastRunUtc);
            if (!string.IsNullOrWhiteSpace(stored)
                && DateTimeOffset.TryParse(stored, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                lastSweep = parsed;

            return Ok(responseFactory.Success(new ApiMaintenanceStatus
            {
                Supported = status.Supported,
                BackupDirectory = status.Directory,
                DatabaseBytes = status.DatabaseBytes,
                WalBytes = status.WalBytes,
                ReclaimableBytes = status.ReclaimableBytes,
                FreeBytes = status.FreeBytes,
                BackupCount = status.Count,
                BackupTotalBytes = status.TotalBytes,
                LatestBackupUtc = status.LatestUtc,
                LastSweepUtc = lastSweep,
            }));
        }

        /// <summary>
        /// Takes a snapshot now, then applies retention. Runs inline rather than queueing a job:
        /// VACUUM INTO is a read transaction that takes well under a second on a database of this size
        /// and does not block writers, and an admin pressing a button wants to be told what happened
        /// rather than to go and look in the job log.
        /// </summary>
        [HttpPost]
        [Route("maintenance/backup")]
        public async Task<IActionResult> BackupNow()
        {
            var outcome = await backupService.CreateBackupAsync();

            if (!outcome.Succeeded)
            {
                // A skip is an expected, explainable state (SQL Server, no database yet, not enough
                // disk), so report it as a message rather than a server error.
                return outcome.Skipped
                    ? Ok(responseFactory.Success(message: $"No backup taken — {outcome.Reason}"))
                    : BadRequest(responseFactory.Error($"Backup failed: {outcome.Reason}"));
            }

            int removed = backupService.ApplyRetention(
                optionManager.GetGlobal(Options.Server_Backup_KeepCount));

            var message = $"Backup created: {outcome.Describe()}"
                        + (removed > 0 ? $"; removed {removed} older snapshot(s)" : "");
            return Ok(responseFactory.Success(message: message));
        }

        /// <summary>
        /// Compacts the database in place, returning free pages to the filesystem.
        ///
        /// A button rather than part of the nightly sweep, on purpose. VACUUM rewrites the whole file
        /// and holds an exclusive lock while it does; every other writer blocks for busy_timeout (30 s)
        /// and then *fails*, which on this database has previously meant unrelated endpoints returning
        /// bare 500s until a restart. That is an acceptable risk when a person chose it and is watching,
        /// and not one worth taking unattended at 3am to reclaim a few hundred KB.
        ///
        /// The follow-up checkpoint is not optional: under WAL the size reduction commits through the
        /// log, so without it the file on disk does not actually shrink and the UI would report success
        /// while nothing appeared to happen.
        /// </summary>
        [HttpPost]
        [Route("maintenance/compact")]
        public async Task<IActionResult> CompactNow()
        {
            var status = backupService.GetStatus();
            if (!status.Supported)
                return Ok(responseFactory.Success(message: "Compacting is only available on SQLite."));

            long before = status.DatabaseBytes;
            long reclaimable = status.ReclaimableBytes ?? 0;

            try
            {
                await dataContext.Database.ExecuteSqlRawAsync("VACUUM;");
                await dataContext.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);");
            }
            catch (Exception ex)
            {
                return BadRequest(responseFactory.Error($"Compacting failed: {ex.Message}"));
            }

            long after = backupService.GetStatus().DatabaseBytes;
            long freed = before - after;

            return Ok(responseFactory.Success(message: freed > 0
                ? $"Database compacted, freeing {DatabaseBackupService.Bytes(freed)}."
                : $"Database compacted (about {DatabaseBackupService.Bytes(reclaimable)} was reusable; the file did not shrink)."));
        }

        /// <summary>
        /// Runs the housekeeping sweep now instead of waiting for its schedule. This queues a normal
        /// job run; if the scheduled sweep happens to be running already, Quartz serialises the two
        /// (MaintenanceJob is [DisallowConcurrentExecution]) and this one starts when that finishes.
        /// </summary>
        [HttpPost]
        [Route("maintenance/run")]
        public async Task<IActionResult> RunMaintenanceNow()
        {
            await scheduler.Schedule<MaintenanceJob>(
                name: "Database maintenance (manual)",
                userId: userManager.GetUserId(User),
                retryCount: 0);

            return Ok(responseFactory.Success(message: "Maintenance sweep queued."));
        }

        // ---- Server logs -------------------------------------------------------------------
        //
        // Everything here inherits the class-level [Authorize(Roles = Admin)]. Note that no route below
        // ever builds a path from a client string: a requested file name is resolved by looking it up in
        // a directory listing, so a traversal attempt is simply not in the list and 404s.

        [HttpGet]
        [Route("logs/files")]
        public IActionResult GetLogFiles()
        {
            var files = logReader.ListAppLogs()
                .Select(f => new ApiLogFile { Name = f.Name, Bytes = f.Bytes, LastWriteUtc = f.LastWriteUtc })
                .ToArray();

            return Ok(responseFactory.Success(files));
        }

        [HttpGet]
        [Route("logs/entries")]
        public IActionResult GetLogEntries([FromQuery] string file = null,
                                           [FromQuery] int minSeverity = 0,
                                           [FromQuery] string search = null,
                                           [FromQuery] int skip = 0,
                                           [FromQuery] int take = 100)
        {
            // Same clamping shape as JobsController: a client cannot ask for an unbounded page.
            take = take <= 0 ? 100 : Math.Min(take, 500);
            skip = Math.Max(0, skip);

            var name = string.IsNullOrWhiteSpace(file) ? logReader.DefaultAppLogName() : file;
            if (name == null)
                return Ok(responseFactory.Success(new ApiLogPage()));   // no logs on disk yet

            var path = logReader.ResolveAppLog(name);
            if (path == null)
                return NotFound(responseFactory.Error("No such log file."));

            LogPage page;
            try
            {
                page = logReader.Query(path, new LogQuery
                {
                    MinSeverity = (LogSeverity)Math.Clamp(minSeverity, (int)LogSeverity.Trace, (int)LogSeverity.Unknown),
                    Search = search,
                    Skip = skip,
                    Take = take,
                });
            }
            catch (IOException ex)
            {
                return BadRequest(responseFactory.Error("Could not read the log file.", ex.Message));
            }

            return Ok(responseFactory.Success(new ApiLogPage
            {
                File = name,
                TotalMatched = page.TotalMatched,
                Entries = page.Entries.Select(ToApi).ToArray(),
            }));
        }

        [HttpGet]
        [Route("logs/download")]
        public IActionResult DownloadLog([FromQuery] string file)
        {
            var path = logReader.ResolveAppLog(file);
            if (path == null)
                return NotFound(responseFactory.Error("No such log file."));

            // FileStreamResult over a shared handle rather than PhysicalFile: the current day's file is
            // held open for writing by NLog, and PhysicalFile would open it without sharing.
            return File(LogFileReader.OpenShared(path).BaseStream, "text/plain", file);
        }

        private static ApiLogEntry ToApi(LogEntry entry) => new ApiLogEntry
        {
            Timestamp = entry.Timestamp,
            Level = entry.Level,
            Severity = (int)entry.Severity,
            Logger = entry.Logger,
            Callsite = entry.Callsite,
            RequestUrl = entry.RequestUrl,
            Message = entry.Message,
            Detail = entry.Detail,
        };

        // ---- User management ---------------------------------------------------------------

        [HttpGet]
        [Route("users")]
        public async Task<IActionResult> GetUsers()
        {
            var result = new List<ApiAdminUser>();
            foreach (var user in userManager.Users.ToList())
            {
                var usage = quotaService.GetUsage(user.Id);

                int? countOverride = optionManager.GetForUserNoResolve(Options.User_CountQuota, user.Id, out int cv) ? cv : (int?)null;
                double? sizeOverrideGb = optionManager.GetForUserNoResolve(Options.User_SizeQuota, user.Id, out long sv) ? sv / (double)MbPerGb : (double?)null;

                result.Add(new ApiAdminUser
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    IsAdmin = await userManager.IsInRoleAsync(user, UserRoles.Admin),
                    IsDisabled = await userManager.IsLockedOutAsync(user),
                    VideoCount = usage.Count,
                    UsedBytes = usage.Bytes,
                    VideoQuotaOverride = countOverride >= 0 ? countOverride : null,
                    StorageQuotaOverrideGb = sizeOverrideGb,
                });
            }
            return Ok(responseFactory.Success(result));
        }

        [HttpPost]
        [Route("users/role")]
        public async Task<IActionResult> SetUserRole([FromBody] SetUserRoleRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (!request.IsAdmin)
            {
                // Guard: never demote yourself or the last remaining admin.
                if (user.Id == userManager.GetUserId(User))
                    return BadRequest(responseFactory.Error("You can't remove your own admin role."));
                if (await IsLastAdmin(user))
                    return BadRequest(responseFactory.Error("Can't remove the last administrator."));

                await userManager.RemoveFromRoleAsync(user, UserRoles.Admin);
            }
            else
            {
                if (!await roleManager.RoleExistsAsync(UserRoles.Admin))
                    await roleManager.CreateAsync(new IdentityRole(UserRoles.Admin));
                if (!await userManager.IsInRoleAsync(user, UserRoles.Admin))
                    await userManager.AddToRoleAsync(user, UserRoles.Admin);
            }
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("users/enabled")]
        public async Task<IActionResult> SetUserEnabled([FromBody] SetUserEnabledRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (!request.Enabled && user.Id == userManager.GetUserId(User))
                return BadRequest(responseFactory.Error("You can't disable your own account."));

            if (request.Enabled)
            {
                await userManager.SetLockoutEndDateAsync(user, null);
            }
            else
            {
                await userManager.SetLockoutEnabledAsync(user, true);
                await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("users/quota")]
        public async Task<IActionResult> SetUserQuota([FromBody] SetUserQuotaRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (request.VideoQuota.HasValue)
                optionManager.SetForUser(Options.User_CountQuota, user.Id, request.VideoQuota.Value);
            else
                optionManager.UnsetForUser(Options.User_CountQuota, user.Id);

            if (request.StorageQuotaGb.HasValue)
                optionManager.SetForUser(Options.User_SizeQuota, user.Id, (long)(request.StorageQuotaGb.Value * MbPerGb));
            else
                optionManager.UnsetForUser(Options.User_SizeQuota, user.Id);

            return Ok(responseFactory.Success());
        }

        [HttpPost]
        [Route("users/delete")]
        public async Task<IActionResult> DeleteUser([FromBody] DeleteUserRequest request)
        {
            var user = await userManager.FindByIdAsync(request.UserId);
            if (user == null)
                return BadRequest(responseFactory.Error("User does not exist."));

            if (user.Id == userManager.GetUserId(User))
                return BadRequest(responseFactory.Error("You can't delete your own account."));
            if (await IsLastAdmin(user))
                return BadRequest(responseFactory.Error("Can't delete the last administrator."));

            await DeleteUserJob.Schedule(scheduler, userManager.GetUserId(User), user.Id);
            return Ok(responseFactory.Success());
        }

        /// <summary>True if the given user is an admin and no other admin exists.</summary>
        private async Task<bool> IsLastAdmin(UserAccount user)
        {
            if (!await userManager.IsInRoleAsync(user, UserRoles.Admin))
                return false;
            var admins = await userManager.GetUsersInRoleAsync(UserRoles.Admin);
            return admins.Count(a => a.Id != user.Id) == 0;
        }
    }
}
