using Microsoft.EntityFrameworkCore;
using OvoGrowthOS.Api.Data;
using OvoGrowthOS.Api.Mail;
using OvoGrowthOS.Domain;

namespace OvoGrowthOS.Api.Notifications;

public sealed record NotificationContent(string Title, string Href);

public sealed class NotificationService(AppDbContext db, SmtpSettingsProvider provider)
{
    public static bool Staff(UserAccount user) => user.Role is "Admin" or "Partner" or "Analyst";
    public static bool Manager(UserAccount user) => user.Role is "Admin" or "Partner";
    public static Task<int> LockUser(AppDbContext db, Guid id, CancellationToken ct = default) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"UserAccounts\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    public async Task Refresh(Guid userId, DateTimeOffset now, CancellationToken ct = default)
    {
        var settings = await provider.GetAsync(ct);
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (tx is not null) await LockUser(db, userId, ct);
        var user = await db.UserAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive || user.InvitationPending) return;
        var pref = await db.NotificationPreferences.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (pref is null) { pref = new NotificationPreference { UserId = userId, StartedAt = now }; db.Add(pref); }
        var since = pref.StartedAt > now.AddDays(-30) ? pref.StartedAt : now.AddDays(-30);
        var existing = (await db.UserNotifications.Where(x => x.UserId == userId).Select(x => x.EventKey).ToListAsync(ct)).ToHashSet();
        void Add(NotificationKind kind, Guid? source, string key, DateTimeOffset happened, DateOnly? day = null, bool emailAllowed = true)
        {
            if (!existing.Add(key)) return;
            db.Add(new UserNotification { UserId = userId, Kind = kind, SourceId = source, EventKey = key, Day = day,
                CreatedAt = now, Email = user.Email, AccountVersion = user.TokenVersion,
                EmailStatus = emailAllowed && settings.Ready && pref.WantsEmail(kind) && happened >= now.AddHours(-24) ? MailDeliveryStatus.Pending : null });
        }
        var today = TeamWork.Today(now);
        if (Staff(user))
        {
            var tasks = await db.WorkTasks.AsNoTracking().Where(x => x.AssigneeId == userId && x.CompletedAt == null).ToListAsync(ct);
            if (tasks.Count > 0 && now.ToOffset(TimeSpan.FromHours(3)).Hour >= 9)
                Add(NotificationKind.DailyTasks, null, $"daily:{today:yyyy-MM-dd}", now, today);
            foreach (var task in tasks.Where(x => x.DueOn <= today.AddDays(1)))
                Add(NotificationKind.TaskDue, task.Id, $"due:{task.Id}:{task.DueOn:yyyy-MM-dd}", now, task.DueOn);
            var promiseFrom = today.AddDays(-60);
            var promises = await db.CollectionPromises.AsNoTracking()
                .Where(x => x.OwnerId == userId && !x.IsCancelled && x.PromisedOn >= promiseFrom && x.PromisedOn <= today.AddDays(1))
                .ToListAsync(ct);
            foreach (var promise in promises)
            {
                var promisePeriod = await db.MonthlyPerformances.AsNoTracking()
                    .Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
                    .Include(x => x.Collection)!.ThenInclude(x => x!.Promise)
                    .SingleOrDefaultAsync(x => x.Id == promise.MonthlyPerformanceId, ct);
                if (promisePeriod is null) continue;
                var reminder = CollectionPromises.Reminder(CollectionPromises.Balance(promisePeriod, today), today);
                if (reminder is null) continue;
                var prefix = reminder == CollectionPromises.ReminderOverdue ? "promise-overdue" : "promise-due";
                Add(NotificationKind.PromiseReminder, promise.Id, $"{prefix}:{promise.Id}:{promise.PromisedOn:yyyy-MM-dd}", now, today);
            }
        }
        if (Manager(user))
        {
            var trNow = now.ToOffset(TimeSpan.FromHours(3));
            var diff = ((int)trNow.DayOfWeek + 6) % 7;
            var lastMonday = trNow.Date.AddDays(-diff).AddHours(9);
            if (trNow >= lastMonday)
            {
                var iso = trNow.Date;
                var week = $"digest:{System.Globalization.ISOWeek.GetYear(iso)}-W{System.Globalization.ISOWeek.GetWeekOfYear(iso):00}";
                Add(NotificationKind.WeeklyDigest, null, week, now, today);
            }
        }
        var brandId = await db.PortalAccesses.Where(x => x.UserId == userId).Select(x => (Guid?)x.BrandId).SingleOrDefaultAsync(ct);
        if (user.Role == "BrandClient" && brandId.HasValue)
        {
            var policy = await db.BrandMailPolicies.AsNoTracking().SingleOrDefaultAsync(x => x.BrandId == brandId, ct);
            var emailAllowed = policy?.ReportEmailEnabled == true;
            var scheduled = policy?.ScheduledReportEnabled == true;
            var reports = await db.PortalReports.AsNoTracking().Where(x => x.BrandId == brandId && x.RevokedAt == null && x.PublishedAt >= since).ToListAsync(ct);
            foreach (var report in reports)
            {
                var owns = ReportMailSchedule.OwnsEmail(scheduled, policy?.ScheduledSendDay ?? 5, policy?.ScheduledSendHour ?? 9, now, report.Year, report.Month);
                Add(NotificationKind.PortalReport, report.Id, $"report:{report.Id}", report.PublishedAt, emailAllowed: emailAllowed && !owns);
            }
        }
        if (Manager(user) || user.Role == "BrandClient" && brandId.HasValue)
        {
            var questions = await db.PortalQuestions.AsNoTracking().Where(x =>
                user.Role == "BrandClient" ? x.UserId == userId && x.BrandId == brandId :
                x.OwnerId == userId || x.OwnerId == null && user.Role == "Admin").ToListAsync(ct);
            var activeReports = (await db.PortalReports.Where(x => x.RevokedAt == null).Select(x => x.Id).ToListAsync(ct)).ToHashSet();
            var ids = questions.Where(x => activeReports.Contains(x.ReportId)).Select(x => x.Id).ToArray();
            foreach (var q in questions.Where(x => activeReports.Contains(x.ReportId)))
            {
                if (Manager(user) && q.CreatedAt >= since) Add(NotificationKind.PortalQuestion, q.Id, $"question:{q.Id}", q.CreatedAt);
                if (user.Role == "BrandClient" && q.AnsweredAt is { } answered && answered >= since)
                    Add(NotificationKind.PortalReply, q.Id, $"answer:{q.Id}", answered);
            }
            var messages = await db.PortalMessages.AsNoTracking().Where(x => ids.Contains(x.QuestionId) && x.CreatedAt >= since && x.AuthorId != userId).ToListAsync(ct);
            foreach (var message in messages.Where(x => x.FromStaff == (user.Role == "BrandClient")))
                Add(message.FromStaff ? NotificationKind.PortalReply : NotificationKind.PortalQuestion, message.QuestionId, $"message:{message.Id}", message.CreatedAt);
        }
        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    // Resolve again at read/send time. No stored message, report snapshot or financial text is exposed.
    public Task<NotificationContent?> Resolve(UserNotification n, UserAccount user, DateTimeOffset now, bool lockSource = false, CancellationToken ct = default) =>
        ResolveCore(n, user, now, lockSource, new ResolveCache(db, user.Id, ct), ct);

    // One cache for a whole page: shared lookups run once instead of once per notification.
    public async Task<List<NotificationContent?>> ResolveMany(IReadOnlyList<UserNotification> rows, UserAccount user, DateTimeOffset now, CancellationToken ct = default)
    {
        var cache = new ResolveCache(db, user.Id, ct);
        if (user.IsActive && !user.InvitationPending)
        {
            if (Staff(user) && rows.Any(x => x.Kind == NotificationKind.DailyTasks)) await cache.PrefetchDailyDates();
            if (Staff(user) && rows.Any(x => x.Kind == NotificationKind.TaskDue))
                await cache.PrefetchTasks(rows.Where(x => x.Kind == NotificationKind.TaskDue).Select(x => x.SourceId));
            var others = rows.Where(x => x.Kind is not (NotificationKind.DailyTasks or NotificationKind.TaskDue)).ToList();
            if (others.Count > 0)
            {
                var brand = await cache.Brand();
                var reports = others.Where(x => x.Kind == NotificationKind.PortalReport).Select(x => x.SourceId).OfType<Guid>().ToList();
                if (reports.Count > 0 && brand.HasValue) await cache.PrefetchReports(reports.Select(x => (x, brand.Value)));
                var questions = others.Where(x => x.Kind != NotificationKind.PortalReport).Select(x => x.SourceId).ToList();
                if (questions.Count > 0)
                {
                    await cache.PrefetchQuestions(questions);
                    await cache.PrefetchReportsFor(cache.LoadedQuestions());
                }
            }
        }
        var result = new List<NotificationContent?>(rows.Count);
        foreach (var n in rows) result.Add(await ResolveCore(n, user, now, false, cache, ct));
        return result;
    }

    private async Task<NotificationContent?> ResolveCore(UserNotification n, UserAccount user, DateTimeOffset now, bool lockSource, ResolveCache cache, CancellationToken ct)
    {
        if (!user.IsActive || user.InvitationPending || n.UserId != user.Id) return null;
        if (n.Kind == NotificationKind.DailyTasks)
        {
            if (!Staff(user) || n.Day != TeamWork.Today(now)) return null;
            var dates = await cache.DailyDates();
            var today = TeamWork.Today(now);
            return dates.Length > 0 ? new($"Günlük görev özeti: {dates.Length} açık, {dates.Count(x => x == today)} bugün, {dates.Count(x => x < today)} gecikmiş", "/work") : null;
        }
        if (n.Kind == NotificationKind.TaskDue)
        {
            if (!Staff(user)) return null;
            if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"WorkTasks\" WHERE \"Id\" = {n.SourceId} FOR SHARE", ct);
            var task = await cache.TaskFor(n.SourceId);
            return task is not null && task.DueOn == n.Day && task.DueOn <= TeamWork.Today(now).AddDays(1)
                ? new($"Görevinizin son tarihi: {task.DueOn:dd.MM.yyyy}", $"/brands/{task.BrandId}#team-work") : null;
        }
        if (n.Kind == NotificationKind.WeeklyDigest)
            return Staff(user) ? new("Haftalık yönetim özeti", "/reports") : null;
        if (n.Kind == NotificationKind.PromiseReminder)
        {
            if (!Staff(user)) return null;
            if (lockSource && db.Database.IsRelational())
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"CollectionPromises\" WHERE \"Id\" = {n.SourceId} FOR SHARE", ct);
            var promise = await db.CollectionPromises.AsNoTracking().SingleOrDefaultAsync(x => x.Id == n.SourceId, ct);
            if (promise is null || promise.IsCancelled || promise.OwnerId != user.Id) return null;
            var promisePeriod = await db.MonthlyPerformances.AsNoTracking()
                .Include(x => x.Brand).Include(x => x.Deal)
                .Include(x => x.Collection)!.ThenInclude(x => x!.Payments)
                .Include(x => x.Collection)!.ThenInclude(x => x!.Promise)
                .SingleOrDefaultAsync(x => x.Id == promise.MonthlyPerformanceId, ct);
            if (promisePeriod is null) return null;
            var balance = CollectionPromises.Balance(promisePeriod, TeamWork.Today(now));
            var reminder = CollectionPromises.Reminder(balance, TeamWork.Today(now));
            if (reminder is null || balance is null) return null;
            var overdueRow = n.EventKey.StartsWith("promise-overdue:", StringComparison.Ordinal);
            if (overdueRow != (reminder == CollectionPromises.ReminderOverdue)) return null;
            var name = promisePeriod.Brand?.Name ?? "Marka";
            var currency = promisePeriod.Collection?.Currency ?? promisePeriod.Deal?.Currency ?? "";
            var remaining = balance.Remaining.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
            return overdueRow
                ? new($"Ödeme sözü gecikti: {name} ({remaining} {currency} kaldı)", "/commissions/planning")
                : new($"Yarın ödeme sözü var: {name} ({remaining} {currency})", "/commissions/planning");
        }
        var brand = await cache.Brand();
        if (n.Kind == NotificationKind.PortalReport)
        {
            if (user.Role != "BrandClient" || brand is null) return null;
            if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalReports\" WHERE \"Id\" = {n.SourceId} FOR SHARE", ct);
            return await cache.ReportActive(n.SourceId ?? Guid.Empty, brand.Value)
                ? new("Markanız için yeni bir rapor paylaşıldı", "/portal") : null;
        }
        if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalQuestions\" WHERE \"Id\" = {n.SourceId} FOR SHARE", ct);
        var question = await cache.QuestionFor(n.SourceId);
        if (question is null) return null;
        if (lockSource && db.Database.IsRelational()) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM growth.\"PortalReports\" WHERE \"Id\" = {question.ReportId} FOR SHARE", ct);
        if (!await cache.QuestionReportActive(question)) return null;
        if (n.Kind == NotificationKind.PortalReply)
            return user.Role == "BrandClient" && question.UserId == user.Id && question.BrandId == brand
                ? new("Sorunuza yeni bir yanıt geldi", "/portal") : null;
        return Manager(user) && (question.OwnerId == user.Id || question.OwnerId is null && user.Role == "Admin")
            ? new("Müşteri konuşmasında yeni bir mesaj var", "/portal-management") : null;
    }

    // Memoised lookups: a single Resolve call fills them one by one, ResolveMany fills them in bulk.
    private sealed class ResolveCache(AppDbContext db, Guid userId, CancellationToken ct)
    {
        private bool _brandLoaded; private Guid? _brand;
        private bool _dailyLoaded; private DateOnly[] _daily = [];
        private readonly Dictionary<Guid, WorkTask?> _tasks = new();
        private readonly HashSet<Guid> _knownTasks = [];
        private readonly Dictionary<Guid, PortalQuestion?> _questions = new();
        private readonly HashSet<Guid> _knownQuestions = [];
        private readonly Dictionary<(Guid ReportId, Guid BrandId), bool> _reports = new();
        private readonly HashSet<(Guid ReportId, Guid BrandId)> _knownReports = [];

        public async Task<Guid?> Brand()
        {
            if (!_brandLoaded)
            {
                _brand = await db.PortalAccesses.Where(x => x.UserId == userId).Select(x => (Guid?)x.BrandId).SingleOrDefaultAsync(ct);
                _brandLoaded = true;
            }
            return _brand;
        }

        public async Task<DateOnly[]> DailyDates()
        {
            if (!_dailyLoaded)
            {
                _daily = await db.WorkTasks.Where(x => x.AssigneeId == userId && x.CompletedAt == null).Select(x => x.DueOn).ToArrayAsync(ct);
                _dailyLoaded = true;
            }
            return _daily;
        }

        public Task PrefetchDailyDates() => DailyDates();

        public async Task<WorkTask?> TaskFor(Guid? id)
        {
            if (id is null) return null;
            if (_knownTasks.Contains(id.Value)) return _tasks[id.Value];
            var task = await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.AssigneeId == userId && x.CompletedAt == null, ct);
            _tasks[id.Value] = task; _knownTasks.Add(id.Value);
            return task;
        }

        public async Task PrefetchTasks(IEnumerable<Guid?> ids)
        {
            var wanted = ids.OfType<Guid>().Distinct().Where(x => !_knownTasks.Contains(x)).ToList();
            if (wanted.Count == 0) return;
            var found = await db.WorkTasks.AsNoTracking().Where(x => wanted.Contains(x.Id) && x.AssigneeId == userId && x.CompletedAt == null).ToListAsync(ct);
            foreach (var id in wanted) { _tasks[id] = found.FirstOrDefault(x => x.Id == id); _knownTasks.Add(id); }
        }

        public async Task<PortalQuestion?> QuestionFor(Guid? id)
        {
            if (id is null) return null;
            if (_knownQuestions.Contains(id.Value)) return _questions[id.Value];
            var question = await db.PortalQuestions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
            _questions[id.Value] = question; _knownQuestions.Add(id.Value);
            return question;
        }

        public async Task PrefetchQuestions(IEnumerable<Guid?> ids)
        {
            var wanted = ids.OfType<Guid>().Distinct().Where(x => !_knownQuestions.Contains(x)).ToList();
            if (wanted.Count == 0) return;
            var found = await db.PortalQuestions.AsNoTracking().Where(x => wanted.Contains(x.Id)).ToListAsync(ct);
            foreach (var id in wanted) { _questions[id] = found.FirstOrDefault(x => x.Id == id); _knownQuestions.Add(id); }
        }

        public IReadOnlyList<PortalQuestion> LoadedQuestions() => _questions.Values.OfType<PortalQuestion>().ToList();

        public async Task<bool> ReportActive(Guid reportId, Guid brandId)
        {
            var key = (reportId, brandId);
            if (_knownReports.Contains(key)) return _reports[key];
            var active = await db.PortalReports.AsNoTracking().AnyAsync(x => x.Id == reportId && x.BrandId == brandId && x.RevokedAt == null, ct);
            _reports[key] = active; _knownReports.Add(key);
            return active;
        }

        public async Task PrefetchReports(IEnumerable<(Guid ReportId, Guid BrandId)> pairs)
        {
            var wanted = pairs.Distinct().Where(x => !_knownReports.Contains(x)).ToList();
            if (wanted.Count == 0) return;
            var ids = wanted.Select(x => x.ReportId).ToList();
            var active = await db.PortalReports.AsNoTracking().Where(x => ids.Contains(x.Id) && x.RevokedAt == null).Select(x => new { x.Id, x.BrandId }).ToListAsync(ct);
            foreach (var pair in wanted) { _reports[pair] = active.Any(x => x.Id == pair.ReportId && x.BrandId == pair.BrandId); _knownReports.Add(pair); }
        }

        public Task PrefetchReportsFor(IReadOnlyList<PortalQuestion> questions) =>
            PrefetchReports(questions.Select(x => (x.ReportId, x.BrandId)));

        public Task<bool> QuestionReportActive(PortalQuestion question) => ReportActive(question.ReportId, question.BrandId);
    }
}
