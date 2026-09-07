# Multi-user data-isolation test pass (2026-09-07)

First real exercise of multiple accounts. Driven through the live API (dev SQLite backend) with three
accounts: **admin** (owns the existing data — subs 1-4/24, folder 1 "Tech"), and two fresh non-admin
users **B** (`bob_iso`) and **C** (`carol_iso`) created via self-registration. Script:
`scratchpad/multiuser_test.py` (26 assertions, all green after the fix below).

## What was tested and passed (26/26)

**Read isolation** — a non-admin sees only their own data:
- `subscription/list` / `subscriptionfolder/list` return only the caller's rows; B never sees admin's or
  C's subs/folders.
- `video/list` by `subscriptionId`, by `ids`, and by `subscriptionFolderId` all return empty for another
  user's ids (the folder path 400s). `video/view/{id}` on another user's video → 404.
- **Even the admin's** normal `subscription/list` is user-scoped (`GetAll(user)` filters `UserId`), so
  admin-sees-all applies to jobs/notifications only, not to subscription/folder/video listing. Good.

**Write isolation** — a non-admin cannot mutate another user's data:
- `subscription/edit` and `subscriptionfolder/edit` on admin's id → 400, name unchanged.
- `subscription/synchronize` on admin's id → 404.
- `video/mark_watched` on admin's video → no effect on the owner's `isWatched`.

**Delete isolation** — see the fix below; all four IDOR probes now leave the victim's data intact, while
a user can still delete their own subscription and folder.

## Bug found and FIXED — cross-user delete IDOR (data loss)

`SubscriptionManager.Delete` / `DeleteFolders` handed the **client-supplied ids straight through** to the
file-deletion jobs and, in one branch, to a delete-by-id with no owner filter:

- `Delete(user, ids, deleteFiles:true)` → `DeleteSubscriptionFilesJob.Schedule(scheduler, ids, true)`.
  The job's `AddAdditionalVideos` selects videos to delete by `SubscriptionIds.Contains(...)` with **no
  user check**, and then `DeleteInternal(firstSub.User, ...)` removes the rows for the owner of the
  *first id*. So user A calling `POST /api/subscription/delete` with **B's** subscription id and
  `deleteDownloadedFiles:true` would delete B's downloaded files **and** B's subscription/video rows.
- `DeleteFolders(user, ids, recursive:true, deleteFiles:true)` → `DeleteSubscriptionFolderFilesJob` with
  the same unscoped-id shape.
- `DeleteFolders(..., recursive:false)` deleted folders with `foldersToDelete = Where(ids.Contains(x.Id))`
  — **no `UserId` filter** — so A could delete B's folder row and orphan B's subscriptions.

The scoped `DeleteInternal` / `DeleteFoldersInternal` (used for the no-files path) were already safe; only
the file-deletion and non-recursive-folder paths trusted the ids.

**Fix** (`SubscriptionManager.cs`): intersect the incoming ids with the caller's owned rows at the top of
`Delete` and `DeleteFolders`, before any branch runs, and no-op on an empty result. One choke point
covers every downstream path (both jobs and the non-recursive delete). Verified: B can no longer delete
admin's or C's subs/folders even with `deleteDownloadedFiles:true`, and can still delete its own.

## Known issue CONFIRMED (not fixed here) — jobs/notifications leak across users

`GET /api/jobs` as non-admin B returned **all 25 job rows, every one `userId: null`** — byte-identical to
what admin sees. Root cause (already in `BACKLOG.md` / CLAUDE.md): `SynchronizeJob`, `DownloadVideoJob`,
`ReprocessVideoJob`, and `RefreshVideoMetadataJob` are scheduled with no `userId`, so `JobInfo.UserId`
is null, which `JobsController.VisibleJobs` (`UserId == user.Id || UserId == null`) shows to everyone and
`NotificationService.Send` broadcasts to `Clients.All`. Today the visible rows are all legitimately
system-wide maintenance jobs, but a user-initiated **sync** or **download** row carries the subscription
/ video name in `job.Name`, so it would leak those names (and a bell card) to every non-admin.

The item-4 fix (`ShouldPostTerminalFailure` gating the terminal-failure card on `job.Notify`) is in place
and unit-tested; it stops `Notify==false` background jobs from broadcasting failures, but does not change
that `Notify==true` user jobs still have `UserId==null`.

**Recommended fix (own change):** populate `JobInfo.UserId` from the initiating user on the user-scoped
Schedule calls (per-sub/per-folder sync, download, reprocess, refresh-video-metadata), leaving the
recurring/global maintenance jobs null. The notification routing and `VisibleJobs` already key on
`UserId`, so this closes the leak without touching that infrastructure. Left for a dedicated change
because it alters what every user sees in their bell / job log.

## Minor integrity gaps (noted, not fixed)

- `MoveSubscription` / `MoveFolder` validate the moved item's ownership but **not** the destination
  `ParentFolderId`'s — a user could parent their own sub/folder under another user's folder id. Integrity
  only (no read/delete of the other user's data); low priority.
