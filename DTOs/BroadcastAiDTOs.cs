using System.Collections.Generic;

namespace News_Back_end.DTOs
{
 /// <summary>
 /// AI-generated insights for broadcast creation/editing
 /// </summary>
 public class BroadcastAiInsightsDTO
 {
 // Timing recommendations
 public string? BestSendTime { get; set; } // ISO time
 public string? BestSendTimeLocal { get; set; } // Human-readable e.g. "Tuesday 3:40 AM"
 public string? TimingRationale { get; set; } // Why this time is recommended

 // Engagement predictions
 public double? PredictedOpenRate { get; set; } // 0..100 percent
 public double? PredictedClickRate { get; set; } // 0..100 percent
 public string? EngagementScore { get; set; } // Low|Medium|High
 public string? EngagementRationale { get; set; } // Why this score

 // Subject line feedback
 public string? OptimalSubjectLength { get; set; } // e.g. "40-50 chars"
 public string? SubjectLineFeedback { get; set; } // Specific feedback on current subject
 public List<string>? SubjectLineSuggestions { get; set; } // Alternative subject lines

 // Content feedback
 public string? BodyLengthFeedback { get; set; } // e.g. "Too long - consider trimming"
 public string? ToneFeedback { get; set; } // e.g. "Professional but could be more engaging"
 public List<string>? ContentImprovements { get; set; } // Specific content suggestions

 // Audience insights
 public int? EstimatedRecipientsCount { get; set; }
 public string? AudienceMatchQuality { get; set; } // Poor|Fair|Good|Excellent
 public string? AudienceInsight { get; set; } // e.g. "This audience prefers shorter emails"
 }

 /// <summary>
 /// Suggested article to include in the broadcast
 /// </summary>
 public class SuggestedArticleDTO
 {
 public int PublicationDraftId { get; set; }
 public string? Title { get; set; }
 public string? HeroImageUrl { get; set; }
 public double? RelevanceScore { get; set; } // 0-100
 public string? WhySuggested { get; set; } // e.g. "High engagement with this audience segment"
 public List<string>? MatchingTopics { get; set; } // Topics that match audience interests
 }

 /// <summary>
 /// Actionable recommendation for improving the broadcast
 /// </summary>
 public class BroadcastAiRecommendationDTO
 {
 public string? Priority { get; set; } // High|Medium|Low
 public string? Category { get; set; } // Content|Timing|Audience|Subject|Articles
 public string? Title { get; set; }
 public string? Why { get; set; }
 public List<string>? Actions { get; set; }
 public List<string>? MetricsReferenced { get; set; }
 public double? ExpectedImpact { get; set; } // Estimated % improvement
 }

 /// <summary>
 /// Complete AI analysis result for a broadcast
 /// </summary>
 public class BroadcastAiResultDTO
 {
 // Overall assessment
 public string? Summary { get; set; }
 public string? OverallScore { get; set; } // A|B|C|D|F or 1-100
 public string? ReadinessStatus { get; set; } // Ready|NeedsWork|NotRecommended

 // Detailed insights
 public BroadcastAiInsightsDTO? Insights { get; set; }

 // Prioritized recommendations
 public List<BroadcastAiRecommendationDTO>? Recommendations { get; set; }

 // Suggested articles to add (based on audience and past performance)
 public List<SuggestedArticleDTO>? SuggestedArticles { get; set; }

 // Quick wins - simple changes that would improve performance
 public List<string>? QuickWins { get; set; }

 // Warnings - issues that should be addressed before sending
 public List<string>? Warnings { get; set; }

 // Raw fallback content when parsing fails
 public string? Raw { get; set; }

 // Metadata
 public DateTime? GeneratedAt { get; set; }
 public string? ModelVersion { get; set; }
 }

 /// <summary>
 /// Request DTO for getting AI insights during broadcast creation
 /// Allows passing draft content for real-time feedback
 /// </summary>
 public class BroadcastAiInsightsRequestDTO
 {
 // Existing broadcast ID (optional - for editing existing broadcast)
 public int? BroadcastId { get; set; }

 // Draft content (for real-time feedback during creation)
 public string? DraftTitle { get; set; }
 public string? DraftSubject { get; set; }
 public string? DraftBody { get; set; }

 // Selected articles
 public List<int>? SelectedArticleIds { get; set; }

 // Targeting
 public List<int>? SelectedInterestTagIds { get; set; }
 public List<int>? SelectedIndustryTagIds { get; set; }

 // Scheduling
 public DateTimeOffset? PlannedSendTime { get; set; }

 // What kind of feedback is requested
 public bool IncludeSubjectSuggestions { get; set; } = true;
 public bool IncludeArticleSuggestions { get; set; } = true;
 public bool IncludeTimingAnalysis { get; set; } = true;
 public bool IncludeContentFeedback { get; set; } = true;
 }

 /// <summary>
 /// Lightweight insights for real-time preview during editing
 /// (Faster than full analysis - uses cached/computed data where possible)
 /// </summary>
 public class BroadcastQuickInsightsDTO
 {
 // Subject line analysis
 public int SubjectCharCount { get; set; }
 public string? SubjectLengthStatus { get; set; } // TooShort|Good|TooLong
 public string? SubjectTip { get; set; }

 // Body analysis
 public int BodyWordCount { get; set; }
 public string? BodyLengthStatus { get; set; } // TooShort|Good|TooLong

 // Audience preview
 public int EstimatedRecipients { get; set; }
 public string? AudienceDescription { get; set; } // e.g. "Technology professionals in HK"

 // Timing
 public string? SuggestedSendTime { get; set; }
 public bool IsPlannedTimeOptimal { get; set; }

 // Article selection
 public int SelectedArticleCount { get; set; }
 public string? ArticleSelectionTip { get; set; } // e.g. "Consider adding 1-2 more articles"

 // Overall readiness
 public int ReadinessPercent { get; set; } // 0-100
 public List<string>? MissingItems { get; set; } // e.g. ["Subject line", "At least one article"]
 }
}
