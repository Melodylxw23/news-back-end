using System.Collections.Generic;
using News_Back_end.Models.SQLServer;
using News_Back_end.DTOs;

namespace News_Back_end.DTOs
{
    // Renamed to avoid Swagger schema id collision with Controllers.SourcesController.FetchDto
    public class FetchRequestDto
    {
        public List<int>? SourceIds { get; set; }

        // Optional per-request override for description/summary settings.
        // If provided, these settings are used for the fetch run only and are not persisted.
        // Use DTO to avoid requiring full Source / enum values in request payload
        public SourceDescriptionSettingDto? SourceSettingOverride { get; set; }

        // When true, bypass duplicate check and force saving articles (useful for re-import/debug)
        public bool Force { get; set; } = false;
    }
}
