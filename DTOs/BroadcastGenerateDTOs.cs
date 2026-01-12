using System;
using News_Back_end.Models.SQLServer;

namespace News_Back_end.DTOs
{
 public class BroadcastGenerateRequestDTO
 {
 // Free-form prompt text that the AI will use to generate title/subject/body
 public string Prompt { get; set; } = string.Empty;

 // Desired channel for context (optional)
 public BroadcastChannel? Channel { get; set; }

 // Desired language code (e.g., en, zh) - optional
 public string? Language { get; set; }
 }

 public class BroadcastGenerateResultDTO
 {
 public string Title { get; set; } = string.Empty;
 public string Subject { get; set; } = string.Empty;
 public string Body { get; set; } = string.Empty;
 }
}
