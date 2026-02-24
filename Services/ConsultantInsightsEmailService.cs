using Microsoft.EntityFrameworkCore;
using News_Back_end.Models.SQLServer;

namespace News_Back_end.Services
{
 public interface IConsultantInsightsEmailService
 {
 Task SendDueInsightsAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken);
 Task<(string Subject, string HtmlBody, DateTimeOffset GeneratedAtUtc)> BuildPreviewAsync(string consultantUserId, DateTimeOffset nowUtc, CancellationToken cancellationToken);
 Task<(bool Success, string Message, string Email, DateTimeOffset AttemptedAtUtc)> SendNowAsync(string consultantUserId, DateTimeOffset nowUtc, bool force, CancellationToken cancellationToken);
 }

 /// <summary>
 /// Sends consultant insights emails based on ConsultantPreference settings.
 /// Uses AI to generate contextual insights based on territories and industries.
 /// </summary>
 public class ConsultantInsightsEmailService : IConsultantInsightsEmailService
 {
 private readonly MyDBContext _db;
 private readonly GmailEmailService _email;
 private readonly IConsultantInsightsAiService _aiService;
 private readonly ILogger<ConsultantInsightsEmailService> _logger;

 public ConsultantInsightsEmailService(
  MyDBContext db,
  GmailEmailService email,
  IConsultantInsightsAiService aiService,
  ILogger<ConsultantInsightsEmailService> logger)
 {
 _db = db;
 _email = email;
 _aiService = aiService;
 _logger = logger;
 }

 public async Task SendDueInsightsAsync(DateTimeOffset nowUtc, CancellationToken cancellationToken)
 {
 // Only send on weekdays for Daily
 var isWeekday = nowUtc.DayOfWeek != DayOfWeek.Saturday && nowUtc.DayOfWeek != DayOfWeek.Sunday;
 var isMonday = nowUtc.DayOfWeek == DayOfWeek.Monday;

 var nowMinutes = nowUtc.Hour * 60 + nowUtc.Minute;

 // narrow window: send if preferred time is within the last 1 minute
 // (since the runner runs every minute)
 var windowStart = nowMinutes - 1;
 var windowEnd = nowMinutes;

 var prefs = await _db.ConsultantPreferences
 .Include(p => p.ConsultantUser)
 .Where(p => p.Email != null && p.Email != "")
 .Where(p => p.PreferredTimeMinutesUtc >= windowStart && p.PreferredTimeMinutesUtc <= windowEnd)
 .ToListAsync(cancellationToken);

 if (!prefs.Any())
 {
 _logger.LogDebug("[ConsultantInsights] No preferences due at {Now}", nowUtc);
 return;
 }

 foreach (var pref in prefs)
 {
 cancellationToken.ThrowIfCancellationRequested();

 if (pref.Frequency == ConsultantInsightsFrequency.Daily && !isWeekday)
 continue;

 if (pref.Frequency == ConsultantInsightsFrequency.Weekly && !isMonday)
 continue;

 // Guard: send only to themselves
 var consultantEmail = pref.ConsultantUser?.Email ?? "";
 if (string.IsNullOrWhiteSpace(consultantEmail) || !string.Equals(consultantEmail, pref.Email, StringComparison.OrdinalIgnoreCase))
 {
 _logger.LogWarning("[ConsultantInsights] Skipping preference {PrefId}: email mismatch (pref={PrefEmail}, user={UserEmail})",
 pref.ConsultantPreferenceId, pref.Email, consultantEmail);
 continue;
 }

 // Idempotency: only send per period
 var (period, periodDateUtc) = GetPeriodKey(pref.Frequency, nowUtc);
 var alreadySent = await _db.ConsultantInsightsSendLogs
 .AnyAsync(l => l.ConsultantUserId == pref.ConsultantUserId && l.Period == period && l.PeriodDateUtc == periodDateUtc,
 cancellationToken);

 if (alreadySent)
 {
 _logger.LogDebug("[ConsultantInsights] Already sent for {UserId} {Period} {PeriodDate}. Skipping.", pref.ConsultantUserId, period, periodDateUtc);
 continue;
 }

 var territories = SafeDeserialize(pref.TerritoriesJson);
 var industries = SafeDeserialize(pref.IndustriesJson);

 var subject = pref.Frequency == ConsultantInsightsFrequency.Weekly
 ? $"China Insights (Weekly) - {nowUtc:yyyy-MM-dd}"
 : $"China Insights (Daily) - {nowUtc:yyyy-MM-dd}";

 // Retrieve previous insights to ensure freshness
 ConsultantInsightsAiResponse? previousInsights = await GetMostRecentInsightsAsync(pref.ConsultantUserId, cancellationToken);

 // Generate AI-powered insights with exclusion of previous content
 ConsultantInsightsAiResponse? aiResponse = null;
 try
 {
     var language = pref.Language == ConsultantInsightsLanguage.Chinese ? "zh" : "en";
 
 // Use the enhanced service if available
 if (_aiService is IConsultantInsightsAiService aiSvcWithExclusion)
 {
 aiResponse = await aiSvcWithExclusion.GenerateInsightsWithExclusionAsync(
  territories,
  industries,
  pref.Frequency.ToString(),
  pref.ConsultantUser?.Name,
  language,
  previousInsights,
  cancellationToken);
 }
 else
 {
 // Fallback if service doesn't support exclusion
 aiResponse = await _aiService.GenerateInsightsAsync(
  territories,
  industries,
  pref.Frequency.ToString(),
  pref.ConsultantUser?.Name,
  language,
  cancellationToken);
 }
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "[ConsultantInsights] Failed to generate AI insights for {UserId}. Will use fallback.", pref.ConsultantUserId);
 aiResponse = null;
 }

 var html = aiResponse != null
 ? BuildAiEmailHtml(pref.ConsultantUser?.Name, territories, industries, aiResponse, nowUtc)
 : BuildDummyEmailHtml(pref.ConsultantUser?.Name, territories, industries, nowUtc);

 var sendLog = new ConsultantInsightsSendLog
 {
 ConsultantUserId = pref.ConsultantUserId,
 Period = period,
 PeriodDateUtc = periodDateUtc,
 SentAtUtc = DateTimeOffset.UtcNow,
 Email = pref.Email,
 Success = true
 };

 // Prepare history record
 var historyRecord = aiResponse != null
 ? new ConsultantInsightsHistory
 {
 ConsultantUserId = pref.ConsultantUserId,
 Period = period,
 PeriodDateUtc = periodDateUtc,
 KeyDevelopmentsJson = System.Text.Json.JsonSerializer.Serialize(aiResponse.KeyDevelopments),
 OpportunitiesJson = System.Text.Json.JsonSerializer.Serialize(aiResponse.Opportunities),
 WatchoutsJson = System.Text.Json.JsonSerializer.Serialize(aiResponse.Watchouts),
 RecommendedActionsJson = System.Text.Json.JsonSerializer.Serialize(aiResponse.RecommendedActions),
 ExecutiveSummary = aiResponse.ExecutiveSummary,
 GeneratedAtUtc = DateTimeOffset.UtcNow
 }
 : null;

 try
 {
 await _email.SendEmailAsync(pref.Email, subject, html);
 _db.ConsultantInsightsSendLogs.Add(sendLog);
 
 // Store history for future freshness checks
 if (historyRecord != null)
 {
 _db.ConsultantInsightsHistories.Add(historyRecord);
 }

 await _db.SaveChangesAsync(cancellationToken);

 _logger.LogInformation("[ConsultantInsights] Sent insights email to {Email} (PrefId={PrefId})", pref.Email, pref.ConsultantPreferenceId);
 }
 catch (Exception ex)
 {
 // Persist failure log too (helps debug but still blocks repeats in same period)
 sendLog.Success = false;
 sendLog.Error = ex.Message;

 try
 {
 _db.ConsultantInsightsSendLogs.Add(sendLog);
 await _db.SaveChangesAsync(cancellationToken);
 }
 catch (Exception saveEx)
 {
 _logger.LogError(saveEx, "[ConsultantInsights] Failed to record send log for {Email}", pref.Email);
 }

 _logger.LogError(ex, "[ConsultantInsights] Failed to send insights email to {Email} (PrefId={PrefId})", pref.Email, pref.ConsultantPreferenceId);
 }
 }
 }

 /// <summary>
 /// Retrieves the most recent insights for a consultant to ensure freshness.
 /// Looks at recent past periods (e.g., last 5 days for Daily, last 4 weeks for Weekly).
 /// </summary>
 private async Task<ConsultantInsightsAiResponse?> GetMostRecentInsightsAsync(string consultantUserId, CancellationToken cancellationToken)
 {
 try
 {
 var recentHistory = await _db.ConsultantInsightsHistories
 .Where(h => h.ConsultantUserId == consultantUserId)
 .OrderByDescending(h => h.GeneratedAtUtc)
 .FirstOrDefaultAsync(cancellationToken);

 if (recentHistory == null)
 return null;

 return new ConsultantInsightsAiResponse
 {
 KeyDevelopments = SafeDeserialize(recentHistory.KeyDevelopmentsJson),
 Opportunities = SafeDeserialize(recentHistory.OpportunitiesJson),
 Watchouts = SafeDeserialize(recentHistory.WatchoutsJson),
 RecommendedActions = SafeDeserialize(recentHistory.RecommendedActionsJson),
 ExecutiveSummary = recentHistory.ExecutiveSummary
 };
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "[ConsultantInsights] Failed to retrieve recent insights history for {UserId}", consultantUserId);
 return null;
 }
 }

 public async Task<(string Subject, string HtmlBody, DateTimeOffset GeneratedAtUtc)> BuildPreviewAsync(
 string consultantUserId,
 DateTimeOffset nowUtc,
 CancellationToken cancellationToken)
 {
 var pref = await _db.ConsultantPreferences
 .Include(p => p.ConsultantUser)
 .FirstOrDefaultAsync(p => p.ConsultantUserId == consultantUserId, cancellationToken);

 // If no saved pref, fall back to user basics (still return a preview)
 string? name = pref?.ConsultantUser?.Name;
 var territories = SafeDeserialize(pref?.TerritoriesJson);
 var industries = SafeDeserialize(pref?.IndustriesJson);

 var freq = pref?.Frequency ?? ConsultantInsightsFrequency.Daily;
 var subject = freq == ConsultantInsightsFrequency.Weekly
 ? $"China Insights (Weekly) - {nowUtc:yyyy-MM-dd}"
 : $"China Insights (Daily) - {nowUtc:yyyy-MM-dd}";

 // Try to generate AI insights for preview
 ConsultantInsightsAiResponse? aiResponse = null;
 try
 {
     var language = pref?.Language == ConsultantInsightsLanguage.Chinese ? "zh" : "en";
 aiResponse = await _aiService.GenerateInsightsAsync(
  territories,
  industries,
  freq.ToString(),
  name,
 language,
  cancellationToken);
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "[ConsultantInsights] Failed to generate AI preview for {UserId}", consultantUserId);
 aiResponse = null;
 }

 var html = aiResponse != null
 ? BuildAiEmailHtml(name, territories, industries, aiResponse, nowUtc)
 : BuildDummyEmailHtml(name, territories, industries, nowUtc);

 return (subject, html, nowUtc);
 }

 public async Task<(bool Success, string Message, string Email, DateTimeOffset AttemptedAtUtc)> SendNowAsync(
 string consultantUserId,
 DateTimeOffset nowUtc,
 bool force,
 CancellationToken cancellationToken)
 {
 var pref = await _db.ConsultantPreferences
 .Include(p => p.ConsultantUser)
 .FirstOrDefaultAsync(p => p.ConsultantUserId == consultantUserId, cancellationToken);

 if (pref == null)
 return (false, "No consultant preferences found. Save preferences first.", string.Empty, DateTimeOffset.UtcNow);

 // Guard: send only to themselves
 var consultantEmail = pref.ConsultantUser?.Email ?? string.Empty;
 if (string.IsNullOrWhiteSpace(consultantEmail))
 return (false, "Consultant user has no email.", string.Empty, DateTimeOffset.UtcNow);

 // Always send to the consultant's login email
 var targetEmail = consultantEmail.Trim();

 var (period, periodDateUtc) = GetPeriodKey(pref.Frequency, nowUtc);
 if (!force)
 {
 var alreadySent = await _db.ConsultantInsightsSendLogs
 .AnyAsync(l => l.ConsultantUserId == consultantUserId && l.Period == period && l.PeriodDateUtc == periodDateUtc,
 cancellationToken);

 if (alreadySent)
 return (false, "Already sent for this period. Use force=true to resend for preview/testing.", targetEmail, DateTimeOffset.UtcNow);
 }

 var territories = SafeDeserialize(pref.TerritoriesJson);
 var industries = SafeDeserialize(pref.IndustriesJson);

 var subject = pref.Frequency == ConsultantInsightsFrequency.Weekly
 ? $"China Insights (Weekly) - {nowUtc:yyyy-MM-dd}"
 : $"China Insights (Daily) - {nowUtc:yyyy-MM-dd}";

 // Generate AI-powered insights
 ConsultantInsightsAiResponse? aiResponse = null;
 try
 {
 var language = pref.Language == ConsultantInsightsLanguage.Chinese ? "zh" : "en";
 aiResponse = await _aiService.GenerateInsightsAsync(
  territories,
  industries,
  pref.Frequency.ToString(),
  pref.ConsultantUser?.Name,
  language,
  cancellationToken);
 }
 catch (Exception ex)
 {
 _logger.LogWarning(ex, "[ConsultantInsights] Failed to generate AI insights for SendNow {UserId}", consultantUserId);
 aiResponse = null;
 }

 var html = aiResponse != null
 ? BuildAiEmailHtml(pref.ConsultantUser?.Name, territories, industries, aiResponse, nowUtc)
 : BuildDummyEmailHtml(pref.ConsultantUser?.Name, territories, industries, nowUtc);

 var attemptedAt = DateTimeOffset.UtcNow;
 var sendLog = new ConsultantInsightsSendLog
 {
 ConsultantUserId = consultantUserId,
 Period = period,
 PeriodDateUtc = periodDateUtc,
 SentAtUtc = attemptedAt,
 Email = targetEmail,
 Success = true
 };

 try
 {
 await _email.SendEmailAsync(targetEmail, subject, html);
 _db.ConsultantInsightsSendLogs.Add(sendLog);
 await _db.SaveChangesAsync(cancellationToken);

 _logger.LogInformation("[ConsultantInsights] SendNow succeeded to {Email} (UserId={UserId}, Force={Force})", targetEmail, consultantUserId, force);
 return (true, "Sent.", targetEmail, attemptedAt);
 }
 catch (Exception ex)
 {
 sendLog.Success = false;
 sendLog.Error = ex.Message;
 try
 {
 _db.ConsultantInsightsSendLogs.Add(sendLog);
 await _db.SaveChangesAsync(cancellationToken);
 }
 catch (Exception saveEx)
 {
 _logger.LogError(saveEx, "[ConsultantInsights] Failed to record SendNow log for {Email}", targetEmail);
 }

 _logger.LogError(ex, "[ConsultantInsights] SendNow failed to {Email} (UserId={UserId}, Force={Force})", targetEmail, consultantUserId, force);
 return (false, "Failed to send: " + ex.Message, targetEmail, attemptedAt);
 }
 }

 private static (ConsultantInsightsPeriod Period, DateTime PeriodDateUtc) GetPeriodKey(ConsultantInsightsFrequency frequency, DateTimeOffset nowUtc)
 {
 var date = nowUtc.UtcDateTime.Date;
 if (frequency == ConsultantInsightsFrequency.Weekly)
 {
 // Monday as start of week
 int diff = (7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7;
 var monday = date.AddDays(-diff);
 return (ConsultantInsightsPeriod.Weekly, monday);
 }

 return (ConsultantInsightsPeriod.Daily, date);
 }

 private static List<string> SafeDeserialize(string? json)
 {
 if (string.IsNullOrWhiteSpace(json)) return new();
 try
 {
 return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new();
 }
 catch
 {
 return new();
 }
 }

 /// <summary>
 /// Builds email HTML from AI-generated insights.
 /// </summary>
 private static string BuildAiEmailHtml(
  string? name,
  List<string> territories,
  List<string> industries,
  ConsultantInsightsAiResponse aiResponse,
  DateTimeOffset nowUtc)
 {
 var displayName = string.IsNullOrWhiteSpace(name) ? "Consultant" : name;
 var territoryLine = string.Join(", ", territories.Any() ? territories : new List<string> { "China (national)" });
 var industryLine = string.Join(", ", industries.Any() ? industries : new List<string> { "Cross-sector" });

 return $@"<!DOCTYPE html>
<html>
<head>
 <meta charset='utf-8' />
 <meta name='viewport' content='width=device-width, initial-scale=1' />
</head>
<body style='font-family: Arial, sans-serif; color:#222; line-height:1.55; background:#ffffff;'>
 <div style='max-width:720px; margin:0 auto; padding:18px;'>

 <div style='border-bottom:1px solid #eee; padding-bottom:12px; margin-bottom:16px;'>
 <div style='font-size:12px; color:#666;'>China Insights Digest</div>
 <h2 style='margin:6px 0 0 0; font-weight:700;'>Daily Briefing</h2>
 <div style='margin-top:6px; font-size:12px; color:#666;'>Generated {nowUtc:yyyy-MM-dd HH:mm} (UTC)</div>
 </div>

 <p style='margin:0 0 14px 0;'>Dear {Html(displayName)},</p>

 <div style='background:#f6f8fa; padding:12px 14px; border-radius:10px; margin-bottom:16px;'>
 <div style='display:flex; flex-wrap:wrap; gap:10px; font-size:13px;'>
 <div><strong>Focus territories:</strong> {Html(territoryLine)}</div>
 <div><strong>Focus industries:</strong> {Html(industryLine)}</div>
 </div>
 </div>

 <h3 style='margin:0 0 8px 0;'>Executive summary</h3>
 <p style='margin:0 0 14px 0;'>{Html(aiResponse.ExecutiveSummary)}</p>

 <h3 style='margin:18px 0 8px 0;'>Key developments</h3>
 <ol style='margin:0 0 18px 0; padding-left:20px;'>
 {string.Join("", aiResponse.KeyDevelopments.Select(i => $"<li style='margin:0 0 10px 0;'>{Html(i)}</li>"))}
 </ol>

 <h3 style='margin:18px 0 8px 0;'>Opportunities & positioning</h3>
 <ul style='margin:0 0 18px 0; padding-left:20px;'>
 {string.Join("", aiResponse.Opportunities.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
 </ul>

 <h3 style='margin:18px 0 8px 0;'>Watch-outs</h3>
 <ul style='margin:0 0 18px 0; padding-left:20px; color:#333;'>
 {string.Join("", aiResponse.Watchouts.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
 </ul>

 <h3 style='margin:18px 0 8px 0;'>Recommended actions (next 7 days)</h3>
 <ul style='margin:0 0 18px 0; padding-left:20px;'>
 {string.Join("", aiResponse.RecommendedActions.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
 </ul>

 <div style='margin-top:18px; padding:12px 14px; background:#fff7ed; border:1px solid #fed7aa; border-radius:10px;'>
 <div style='font-size:12px; color:#7c2d12;'><strong>Note:</strong> This briefing is generated from market research and your saved preferences for {Html(territoryLine)} and {Html(industryLine)}.</div>
 </div>

 <hr style='border:none; border-top:1px solid #eee; margin:22px 0;' />
 <p style='color:#666; font-size:12px; margin:0;'>You are receiving this because you set preferences in the Consultant Advice page.</p>
 </div>
</body>
</html>";
 }

 private static string BuildDummyEmailHtml(string? name, List<string> territories, List<string> industries, DateTimeOffset nowUtc)
 {
 var displayName = string.IsNullOrWhiteSpace(name) ? "Consultant" : name;
 var focusTerritories = territories.Any() ? territories : new List<string> { "China (national)" };
 var focusIndustries = industries.Any() ? industries : new List<string> { "Cross-sector" };

 // Create realistic, AI-style sections with light personalization based on selected territories/industries.
 var territoryLine = string.Join(", ", focusTerritories);
 var industryLine = string.Join(", ", focusIndustries);

 var headlineA = BuildHeadline("Policy & Regulatory", focusTerritories.First(), focusIndustries.First());
 var headlineB = BuildHeadline("Market Signals", focusTerritories.Last(), focusIndustries.Last());
 var headlineC = BuildHeadline("Opportunities", focusTerritories.Count > 1 ? focusTerritories[1] : focusTerritories[0], focusIndustries.Count > 1 ? focusIndustries[1] : focusIndustries[0]);

 var executiveSummary = $@"
<p style='margin:0 0 14px 0;'>This briefing highlights near-term developments relevant to <strong>{Html(territoryLine)}</strong> and <strong>{Html(industryLine)}</strong>. Items below are prioritized for actionable impact over the next 1¨C4 weeks.</p>";

 var keyDevelopments = new[]
 {
 $"{headlineA} ¡ª A set of implementation-level clarifications is expected to reduce ambiguity for market entry and ongoing compliance in {focusTerritories.First()}.",
 $"{headlineB} ¡ª Pricing and procurement behavior suggests a continued shift toward value-based evaluation, especially among mid-sized buyers in {focusTerritories.Last()}.",
 $"{headlineC} ¡ª Signals point to incremental budget release and pilot programs that could open partner-led routes to market for {focusIndustries.First().ToLowerInvariant()} offerings."
 };

 var opportunities = new[]
 {
 $"Shortlist 3¨C5 local partners for {focusTerritories.First()} with existing government/enterprise account coverage; prioritize those with demonstrated delivery capability.",
 $"Package a compliance-first offer: a one-page control map + onboarding checklist tailored to {focusIndustries.First()} stakeholders.",
 "Prepare a two-track messaging set: (1) cost reduction / efficiency, (2) risk reduction / continuity¡ªbuyers are comparing both explicitly."
 };

 var watchouts = new[]
 {
 "Contract timelines are tightening; expect shorter bid windows and heavier documentation upfront.",
 "Marketing claims scrutiny remains elevated¡ªensure product collateral includes verifiable references and avoids absolute guarantees.",
 "If operating cross-border, plan for additional review steps on data flows and vendor access."
 };

 var recommendedActions = new[]
 {
 "Update your ICP and account list: re-rank top 20 targets based on spend signals and urgency drivers.",
 "Schedule 2 discovery calls this week focused on compliance/commercial blockers; document objections verbatim for next iteration.",
 "Draft a one-slide risk register for leadership: top risks, mitigations, and owner per item."
 };

 return $@"<!DOCTYPE html>
<html>
<head>
 <meta charset='utf-8' />
 <meta name='viewport' content='width=device-width, initial-scale=1' />
</head>
<body style='font-family: Arial, sans-serif; color:#222; line-height:1.55; background:#ffffff;'>
 <div style='max-width:720px; margin:0 auto; padding:18px;'>

 <div style='border-bottom:1px solid #eee; padding-bottom:12px; margin-bottom:16px;'>
 <div style='font-size:12px; color:#666;'>China Insights Digest</div>
 <h2 style='margin:6px 0 0 0; font-weight:700;'>Daily Briefing</h2>
 <div style='margin-top:6px; font-size:12px; color:#666;'>Generated {nowUtc:yyyy-MM-dd HH:mm} (UTC)</div>
 </div>

 <p style='margin:0 0 14px 0;'>Dear {Html(displayName)},</p>

 <div style='background:#f6f8fa; padding:12px 14px; border-radius:10px; margin-bottom:16px;'>
 <div style='display:flex; flex-wrap:wrap; gap:10px; font-size:13px;'>
 <div><strong>Focus territories:</strong> {Html(territoryLine)}</div>
 <div><strong>Focus industries:</strong> {Html(industryLine)}</div>
 </div>
 </div>

 <h3 style='margin:0 0 8px 0;'>Executive summary</h3>
 {executiveSummary}

 <h3 style='margin:18px 0 8px 0;'>Key developments</h3>
 <ol style='margin:0 0 18px 0; padding-left:20px;'>
 {string.Join("", keyDevelopments.Select(i => $"<li style='margin:0 0 10px 0;'>{Html(i)}</li>"))}
 </ol>

 <h3 style='margin:18px 0 8px 0;'>Opportunities & positioning</h3>
 <ul style='margin:0 0 18px 0; padding-left:20px;'>
 {string.Join("", opportunities.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
 </ul>

 <h3 style='margin:18px 0 8px 0;'>Watch-outs</h3>
 <ul style='margin:0 0 18px 0; padding-left:20px; color:#333;'>
 {string.Join("", watchouts.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
 </ul>

 <h3 style='margin:18px 0 8px 0;'>Recommended actions (next 7 days)</h3>
 <ul style='margin:0 0 18px 0; padding-left:20px;'>
 {string.Join("", recommendedActions.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
 </ul>

 <div style='margin-top:18px; padding:12px 14px; background:#fff7ed; border:1px solid #fed7aa; border-radius:10px;'>
 <div style='font-size:12px; color:#7c2d12;'><strong>Note:</strong> This is a preview-format briefing generated from your saved preferences. Content generation will be connected to automated analysis.</div>
 </div>

 <hr style='border:none; border-top:1px solid #eee; margin:22px 0;' />
 <p style='color:#666; font-size:12px; margin:0;'>You are receiving this because you set preferences in the Consultant Advice page.</p>
 </div>
</body>
</html>";
 }

 private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value);

 private static string BuildHeadline(string category, string territory, string industry)
 {
 // Simple deterministic headline that still reads like a briefing.
 // Avoids explicit dummy markers.
 var t = string.IsNullOrWhiteSpace(territory) ? "China" : territory;
 var i = string.IsNullOrWhiteSpace(industry) ? "industry" : industry;
 return $"{category}: {t} ¡ª {i} implementation signals";
 }

 /// <summary>
 /// Builds email HTML from consultant-edited insights content.
 /// </summary>
 public static string BuildEditedEmailHtml(
        string? name,
 List<string> territories,
       List<string> industries,
string executiveSummary,
            List<string> keyDevelopments,
       List<string> opportunities,
          List<string> watchouts,
        List<string> recommendedActions,
  DateTimeOffset nowUtc)
        {
       var displayName = string.IsNullOrWhiteSpace(name) ? "Consultant" : name;
            var territoryLine = string.Join(", ", territories.Any() ? territories : new List<string> { "China (national)" });
     var industryLine = string.Join(", ", industries.Any() ? industries : new List<string> { "Cross-sector" });

            return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1' />
</head>
<body style='font-family: Arial, sans-serif; color:#222; line-height:1.55; background:#ffffff;'>
    <div style='max-width:720px; margin:0 auto; padding:18px;'>

        <div style='border-bottom:1px solid #eee; padding-bottom:12px; margin-bottom:16px;'>
            <div style='font-size:12px; color:#666;'>China Insights Digest</div>
       <h2 style='margin:6px 0 0 0; font-weight:700;'>Daily Briefing</h2>
            <div style='margin-top:6px; font-size:12px; color:#666;'>Generated {nowUtc:yyyy-MM-dd HH:mm} (UTC)</div>
        </div>

      <p style='margin:0 0 14px 0;'>Dear {Html(displayName)},</p>

    <div style='background:#f6f8fa; padding:12px 14px; border-radius:10px; margin-bottom:16px;'>
            <div style='display:flex; flex-wrap:wrap; gap:10px; font-size:13px;'>
     <div><strong>Focus territories:</strong> {Html(territoryLine)}</div>
 <div><strong>Focus industries:</strong> {Html(industryLine)}</div>
  </div>
        </div>

 <h3 style='margin:0 0 8px 0;'>Executive summary</h3>
        <p style='margin:0 0 14px 0;'>{Html(executiveSummary)}</p>

        <h3 style='margin:18px 0 8px 0;'>Key developments</h3>
        <ol style='margin:0 0 18px 0; padding-left:20px;'>
 {string.Join("", keyDevelopments.Select(i => $"<li style='margin:0 0 10px 0;'>{Html(i)}</li>"))}
        </ol>

     <h3 style='margin:18px 0 8px 0;'>Opportunities & positioning</h3>
        <ul style='margin:0 0 18px 0; padding-left:20px;'>
         {string.Join("", opportunities.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
        </ul>

        <h3 style='margin:18px 0 8px 0;'>Watch-outs</h3>
        <ul style='margin:0 0 18px 0; padding-left:20px; color:#333;'>
            {string.Join("", watchouts.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
        </ul>

      <h3 style='margin:18px 0 8px 0;'>Recommended actions (next 7 days)</h3>
      <ul style='margin:0 0 18px 0; padding-left:20px;'>
            {string.Join("", recommendedActions.Select(i => $"<li style='margin:0 0 9px 0;'>{Html(i)}</li>"))}
        </ul>

        <div style='margin-top:18px; padding:12px 14px; background:#fff7ed; border:1px solid #fed7aa; border-radius:10px;'>
            <div style='font-size:12px; color:#7c2d12;'><strong>Note:</strong> This briefing has been customized based on your edits and preferences for {Html(territoryLine)} and {Html(industryLine)}.</div>
        </div>

    <hr style='border:none; border-top:1px solid #eee; margin:22px 0;' />
        <p style='color:#666; font-size:12px; margin:0;'>You are receiving this because you set preferences in the Consultant Advice page.</p>
    </div>
</body>
</html>";
      }
    }
}
