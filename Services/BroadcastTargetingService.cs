using Microsoft.EntityFrameworkCore;
using News_Back_end.DTOs;
using News_Back_end.Models.SQLServer;

namespace News_Back_end.Services
{
    /// <summary>
  /// Service to handle broadcast targeting based on actual member interest/industry tags
    /// Maps BroadcastAudience to real member data
    /// </summary>
    public interface IBroadcastTargetingService
    {
      /// <summary>
        /// Get available tags for UI selection
     /// </summary>
        Task<BroadcastTagOptionsDTO> GetAvailableTagsAsync();

  /// <summary>
   /// Get members that match the targeting criteria
        /// </summary>
        Task<List<Member>> GetTargetedMembersAsync(BroadcastTargetingDTO targeting);

        /// <summary>
        /// Preview the recipients who would receive a broadcast
        /// </summary>
        Task<BroadcastTargetPreviewDTO> PreviewTargetedMembersAsync(BroadcastTargetingDTO targeting, int sampleSize = 10);

   /// <summary>
        /// Get tag options with member counts (for UI statistics)
        /// </summary>
        Task<BroadcastTagOptionsDTO> GetTagOptionsWithCountsAsync();

        /// <summary>
    /// Convert legacy BroadcastAudience enum to tag-based targeting
        /// </summary>
        Task<BroadcastTargetingDTO> ConvertFromLegacyAudienceAsync(BroadcastAudience audience);
    }

    public class BroadcastTargetingService : IBroadcastTargetingService
    {
        private readonly MyDBContext _db;

     public BroadcastTargetingService(MyDBContext db)
      {
            _db = db;
        }

        /// <summary>
        /// Get all available tags for broadcast targeting
        /// </summary>
     public async Task<BroadcastTagOptionsDTO> GetAvailableTagsAsync()
        {
    var interestTags = await _db.InterestTags
      .Select(it => new TagOptionDTO
                {
  Id = it.InterestTagId,
         NameEN = it.NameEN,
   NameZH = it.NameZH,
   MemberCount = it.Members.Count
           })
 .ToListAsync();

 var industryTags = await _db.IndustryTags
  .Select(it => new TagOptionDTO
                {
  Id = it.IndustryTagId,
            NameEN = it.NameEN,
      NameZH = it.NameZH,
        MemberCount = it.Members.Count
       })
      .ToListAsync();

            return new BroadcastTagOptionsDTO
            {
          InterestTags = interestTags,
   IndustryTags = industryTags
    };
        }

      /// <summary>
        /// Get tag options with member counts (for UI dropdown/selection)
    /// </summary>
        public async Task<BroadcastTagOptionsDTO> GetTagOptionsWithCountsAsync()
        {
            return await GetAvailableTagsAsync();
        }

        /// <summary>
        /// Get members that match the targeting criteria
    /// Supports filtering by tags, country, and membership type
  /// </summary>
        public async Task<List<Member>> GetTargetedMembersAsync(BroadcastTargetingDTO targeting)
        {
  if (targeting == null)
    throw new ArgumentNullException(nameof(targeting));

            var query = _db.Members
       .Include(m => m.IndustryTags)
  .Include(m => m.Interests)
    .Where(m => m.PreferredChannel == Channels.Email || m.PreferredChannel == Channels.Both)
            .Where(m => !string.IsNullOrEmpty(m.Email))
            .AsQueryable();

  // Filter by interest tags (if any selected)
       if (targeting.SelectedInterestTagIds?.Any() == true)
        {
                if (targeting.UseOrLogic)
  {
            // Members who have ANY of the selected interest tags
          query = query.Where(m => m.Interests.Any(it => targeting.SelectedInterestTagIds.Contains(it.InterestTagId)));
      }
         else
          {
 // Members who have ALL of the selected interest tags
          var tagCount = targeting.SelectedInterestTagIds.Count;
          query = query.Where(m => m.Interests
        .Count(it => targeting.SelectedInterestTagIds.Contains(it.InterestTagId)) == tagCount);
       }
            }

            // Filter by industry tags (if any selected)
    if (targeting.SelectedIndustryTagIds?.Any() == true)
        {
        if (targeting.UseOrLogic)
                {
            // Members who have ANY of the selected industry tags
        query = query.Where(m => m.IndustryTags.Any(it => targeting.SelectedIndustryTagIds.Contains(it.IndustryTagId)));
          }
  else
                {
   // Members who have ALL of the selected industry tags
    var tagCount = targeting.SelectedIndustryTagIds.Count;
  query = query.Where(m => m.IndustryTags
           .Count(it => targeting.SelectedIndustryTagIds.Contains(it.IndustryTagId)) == tagCount);
          }
    }

  // Filter by country (if any specified)
          if (targeting.TargetCountries?.Any() == true)
            {
        var countries = targeting.TargetCountries
              .Where(c => Enum.TryParse<Countries>(c, out _))
         .Select(c => Enum.Parse<Countries>(c))
   .ToList();

         if (countries.Any())
          {
                    query = query.Where(m => countries.Contains(m.Country));
   }
     }

  // Filter by membership type (if any specified)
    if (targeting.TargetMembershipTypes?.Any() == true)
            {
    var types = targeting.TargetMembershipTypes
        .Where(t => Enum.TryParse<Types>(t, out _))
        .Select(t => Enum.Parse<Types>(t))
             .ToList();

      if (types.Any())
      {
       query = query.Where(m => types.Contains(m.MembershipType));
    }
      }

     return await query.ToListAsync();
    }

        /// <summary>
        /// Preview targeted members (returns sample of recipients)
        /// </summary>
        public async Task<BroadcastTargetPreviewDTO> PreviewTargetedMembersAsync(BroadcastTargetingDTO targeting, int sampleSize = 10)
        {
         var allMatched = await GetTargetedMembersAsync(targeting);

            // Get sample for preview
       var sample = allMatched.Take(sampleSize).ToList();

            return new BroadcastTargetPreviewDTO
            {
   TotalMembersMatched = allMatched.Count,
  SampleSize = Math.Min(sampleSize, allMatched.Count),
          SampleMembers = sample.Select(m => new MemberPreviewDTO
                {
     MemberId = m.MemberId,
         CompanyName = m.CompanyName,
       ContactPerson = m.ContactPerson,
     Email = m.Email,
     InterestTagNames = m.Interests.Select(it => it.NameEN).ToList(),
        IndustryTagNames = m.IndustryTags.Select(it => it.NameEN).ToList()
     }).ToList()
            };
 }

        /// <summary>
        /// Convert legacy BroadcastAudience enum values to modern tag-based targeting
        /// This bridges old broadcasts with new tag system
        /// </summary>
   public async Task<BroadcastTargetingDTO> ConvertFromLegacyAudienceAsync(BroadcastAudience audience)
        {
            // Map enum values to interest tag names
         var tagNameMap = new Dictionary<BroadcastAudience, string[]>
            {
                { BroadcastAudience.Technology, new[] { "Technology", "Tech" } },
          { BroadcastAudience.Business, new[] { "Business", "Commerce" } },
       { BroadcastAudience.Sports, new[] { "Sports", "Fitness" } },
                { BroadcastAudience.Entertainment, new[] { "Entertainment", "Media" } },
       { BroadcastAudience.Politics, new[] { "Politics", "Government" } }
        };

        var targeting = new BroadcastTargetingDTO
   {
    UseOrLogic = true,
   SelectedInterestTagIds = new List<int>(),
        SelectedIndustryTagIds = new List<int>()
       };

 if (audience == BroadcastAudience.All)
            {
           // No filtering - target all email-enabled members
     return targeting;
   }

            // Find tags matching the audience
   if (tagNameMap.TryGetValue(audience, out var tagNames))
 {
     var matchingInterestTags = await _db.InterestTags
        .Where(it => tagNames.Contains(it.NameEN) || tagNames.Contains(it.NameZH))
     .Select(it => it.InterestTagId)
      .ToListAsync();

 targeting.SelectedInterestTagIds = matchingInterestTags;
          }

            return targeting;
      }
    }
}
