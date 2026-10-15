using DiscoRec;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EZ_Rent.Services
{
    public class RecommendationService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<RecommendationService> _logger;

        private const string MODEL_CACHE_KEY = "disco_recommender_model";
        private static readonly TimeSpan MODEL_CACHE_DURATION = TimeSpan.FromMinutes(30);

        // ⭐ Search trong vòng 24h sẽ override behavior
        private const int RECENT_SEARCH_HOURS = 24;

        public RecommendationService(
            AppDbContext context,
            IMemoryCache cache,
            ILogger<RecommendationService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        // ============================================================
        // LẤY GỢI Ý — HYBRID: Disco CF + Profile Post-filter
        // ============================================================
        public async Task<List<RentalPost>> GetRecommendationsAsync(string userId, int take = 6)
        {
            // Không login → fallback phòng mới
            if (string.IsNullOrEmpty(userId))
                return await GetFallbackAsync(take);

            // ⭐ Build profile TRƯỚC
            var profile = await BuildUserProfileAsync(userId);

            // ⭐ Log debug
            _logger.LogInformation(
                "[REC] user={UserId} | city={City} | district={District} | roomType={RoomType} | price={Min}-{Max}",
                userId,
                profile.PreferredCity ?? "(null)",
                profile.PreferredDistrict ?? "(null)",
                profile.PreferredRoomType?.ToString() ?? "(null)",
                profile.PriceMin,
                profile.PriceMax);

            // ⭐ Không có profile → fallback phòng mới
            if (string.IsNullOrEmpty(profile.PreferredCity) &&
                !profile.PreferredRoomType.HasValue)
            {
                _logger.LogInformation("[REC] user={UserId} không có profile → fallback", userId);
                return await GetFallbackAsync(take);
            }

            // ⭐ Có profile → dùng Disco + filter
            var model = await GetOrBuildModelAsync();

            if (model != null)
            {
                try
                {
                    var recommendedItems = model.UserRecs(userId, take * 5);
                    if (recommendedItems != null && recommendedItems.Any())
                    {
                        var recommendedIds = recommendedItems
                            .Select(r => int.Parse(r.Id))
                            .ToList();

                        var discoPosts = await _context.RentalPosts
                            .Include(p => p.Owner)
                            .Include(p => p.RoomImages)
                            .Where(p => recommendedIds.Contains(p.RentalPostId) &&
                                        p.ApprovalStatus == ApprovalStatus.Approved &&
                                        p.RoomStatus == RoomStatus.Available)
                            .ToListAsync();

                        // Filter theo profile
                        var filtered = recommendedIds
                            .Select(id => discoPosts.FirstOrDefault(p => p.RentalPostId == id))
                            .Where(p => p != null && MatchesProfile(p!, profile))
                            .Cast<RentalPost>()
                            .ToList();

                        if (filtered.Count >= take)
                        {
                            _logger.LogInformation(
                                "[REC] user={UserId} → Disco trả {Count} phòng khớp profile",
                                userId, filtered.Count);
                            return filtered.Take(take).ToList();
                        }

                        // Bù thêm từ pool cùng profile
                        var existingIds = filtered.Select(p => p.RentalPostId).ToHashSet();
                        var extra = await _context.RentalPosts
                            .Include(p => p.Owner)
                            .Include(p => p.RoomImages)
                            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved &&
                                        p.RoomStatus == RoomStatus.Available &&
                                        p.City == profile.PreferredCity &&
                                        p.Price >= profile.PriceMin * 0.5m &&
                                        p.Price <= profile.PriceMax * 1.5m &&
                                        !existingIds.Contains(p.RentalPostId))
                            .OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
                            .Take(take - filtered.Count)
                            .ToListAsync();

                        filtered.AddRange(extra);

                        _logger.LogInformation(
                            "[REC] user={UserId} → Disco + bù = {Count} phòng",
                            userId, filtered.Count);

                        return filtered.Take(take).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[REC] Disco failed for {UserId}", userId);
                }
            }

            // ⭐ Disco không có gợi ý → dùng PROFILE-BASED
            _logger.LogInformation("[REC] user={UserId} → profile-based fallback", userId);
            return await GetFallbackByProfileAsync(profile, take);
        }

        // ============================================================
        // BUILD USER PROFILE
        //    ⭐ Search trong 24h override Behavior
        // ============================================================
        private async Task<UserPreference> BuildUserProfileAsync(string userId)
        {
            // ===== 1. Behavior =====
            var favs = await _context.Favorites
                .Where(f => f.UserId == userId)
                .Select(f => f.RentalPost)
                .ToListAsync();

            var views = await _context.ViewingAppointments
                .Where(v => v.TenantId == userId &&
                            v.Status != ViewingAppointmentStatus.Cancelled &&
                            v.Status != ViewingAppointmentStatus.Rejected)
                .Select(v => v.RentalPost)
                .ToListAsync();

            var reqs = await _context.RentalRequests
                .Where(r => r.TenantId == userId &&
                            r.Status != RentalRequestStatus.Cancelled)
                .Select(r => r.RentalPost)
                .ToListAsync();

            var behavior = favs.Concat(views).Concat(reqs).ToList();

            // ===== 2. SearchHistory (20 lần gần nhất) =====
            var searches = await _context.SearchHistories
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.SearchedAt)
                .Take(20)
                .ToListAsync();

            // ===== 3. Cả 2 rỗng → return default =====
            if (behavior.Count == 0 && searches.Count == 0)
                return new UserPreference();

            var profile = new UserPreference();

            // ⭐ Kiểm tra search mới (trong 24h)
            var recentSearch = searches.FirstOrDefault(s =>
                (DateTime.UtcNow - s.SearchedAt).TotalHours < RECENT_SEARCH_HOURS);

            bool hasRecentSearch = recentSearch != null;

            // ============================================================
            // ===== 4. CITY =====
            // ⭐ Ưu tiên: search mới (24h) > behavior > search cũ
            // ============================================================
            if (hasRecentSearch && !string.IsNullOrEmpty(recentSearch!.City))
            {
                profile.PreferredCity = recentSearch.City;

                _logger.LogInformation(
                    "[Profile] user={UserId} → dùng City từ SEARCH MỚI: {City}",
                    userId, recentSearch.City);
            }
            else if (behavior.Count > 0)
            {
                profile.PreferredCity = behavior
                    .GroupBy(p => p.City)
                    .OrderByDescending(g => g.Count())
                    .First().Key;

                _logger.LogInformation(
                    "[Profile] user={UserId} → dùng City từ BEHAVIOR: {City}",
                    userId, profile.PreferredCity);
            }
            else if (searches.Any(s => !string.IsNullOrEmpty(s.City)))
            {
                profile.PreferredCity = searches
                    .Where(s => !string.IsNullOrEmpty(s.City))
                    .GroupBy(s => s.City!)
                    .OrderByDescending(g => g.Count())
                    .First().Key;
            }

            // ============================================================
            // ===== 5. DISTRICT (chỉ khi search mới) =====
            // ============================================================
            if (hasRecentSearch && !string.IsNullOrEmpty(recentSearch!.District))
            {
                profile.PreferredDistrict = recentSearch.District;
            }

            // ============================================================
            // ===== 6. PRICE RANGE =====
            // ⭐ Ưu tiên: search mới > behavior > search cũ
            // ============================================================
            if (hasRecentSearch &&
                (recentSearch!.MinPrice.HasValue || recentSearch.MaxPrice.HasValue))
            {
                profile.PriceMin = recentSearch.MinPrice ?? 1_000_000m;
                profile.PriceMax = recentSearch.MaxPrice ?? 20_000_000m;

                _logger.LogInformation(
                    "[Profile] user={UserId} → dùng Price từ SEARCH MỚI: {Min}-{Max}",
                    userId, profile.PriceMin, profile.PriceMax);
            }
            else if (behavior.Count > 0)
            {
                profile.PriceMin = (decimal)behavior.Min(p => (double)p.Price);
                profile.PriceMax = (decimal)behavior.Max(p => (double)p.Price);
                profile.AvgPrice = (decimal)behavior.Average(p => (double)p.Price);
            }
            else if (searches.Any())
            {
                var mins = searches.Where(s => s.MinPrice.HasValue)
                                   .Select(s => s.MinPrice!.Value).ToList();
                var maxs = searches.Where(s => s.MaxPrice.HasValue)
                                   .Select(s => s.MaxPrice!.Value).ToList();

                profile.PriceMin = mins.Any() ? mins.Min() : 1_000_000m;
                profile.PriceMax = maxs.Any() ? maxs.Max() : 20_000_000m;
            }

            // Fallback nếu giá vẫn chưa có
            if (profile.PriceMin <= 0) profile.PriceMin = 1_000_000m;
            if (profile.PriceMax <= 0) profile.PriceMax = 20_000_000m;

            // ============================================================
            // ===== 7. ROOM TYPE =====
            // ⭐ Ưu tiên: search mới > behavior > search cũ
            // ============================================================
            if (hasRecentSearch && recentSearch!.RoomType.HasValue)
            {
                profile.PreferredRoomType = recentSearch.RoomType.Value;
            }
            else if (behavior.Count > 0)
            {
                profile.PreferredRoomType = behavior
                    .GroupBy(p => p.RoomType)
                    .OrderByDescending(g => g.Count())
                    .First().Key;
            }
            else if (searches.Any(s => s.RoomType.HasValue))
            {
                profile.PreferredRoomType = searches
                    .Where(s => s.RoomType.HasValue)
                    .GroupBy(s => s.RoomType!.Value)
                    .OrderByDescending(g => g.Count())
                    .First().Key;
            }

            // ============================================================
            // ===== 8. AREA — chỉ từ behavior (nới ±50%) =====
            // ============================================================
            if (behavior.Count >= 3)
            {
                profile.MinArea = behavior.Min(p => p.Area) * 0.5;
                profile.MaxArea = behavior.Max(p => p.Area) * 1.5;
            }

            // ============================================================
            // ===== 9. AMENITIES — top 5 từ behavior =====
            // ============================================================
            if (behavior.Count > 0)
            {
                profile.PreferredAmenities = behavior
                    .Where(p => !string.IsNullOrEmpty(p.AmenitiesJson))
                    .SelectMany(p => ParseAmenities(p.AmenitiesJson))
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count())
                    .Take(5)
                    .Select(g => g.Key)
                    .ToList();
            }

            return profile;
        }

        // ============================================================
        // MATCH PROFILE — nới lỏng để tránh filter quá chặt
        // ============================================================
        private bool MatchesProfile(RentalPost post, UserPreference profile)
        {
            // Không có profile → không lọc
            if (string.IsNullOrEmpty(profile.PreferredCity) &&
                string.IsNullOrEmpty(profile.PreferredDistrict) &&
                !profile.PreferredRoomType.HasValue)
                return true;

            // ===== 1. CITY (bắt buộc nếu có) =====
            if (!string.IsNullOrEmpty(profile.PreferredCity) &&
                post.City != profile.PreferredCity)
                return false;

            // ===== 2. DISTRICT (chỉ check khi có) =====
            if (!string.IsNullOrEmpty(profile.PreferredDistrict) &&
                post.District != profile.PreferredDistrict)
                return false;

            // ===== 3. PRICE (nới ±50%) =====
            var lowerPrice = profile.PriceMin * 0.5m;
            var upperPrice = profile.PriceMax * 1.5m;
            if (post.Price < lowerPrice || post.Price > upperPrice)
                return false;

            // ===== 4. ROOM TYPE =====
            if (profile.PreferredRoomType.HasValue &&
                post.RoomType != profile.PreferredRoomType.Value)
                return false;

            // ⭐ BỎ check AREA — quá chặt, gây fail nhiều

            return true;
        }

        // ============================================================
        // FALLBACK THEO PROFILE — khi Disco không có gợi ý
        // ============================================================
        private async Task<List<RentalPost>> GetFallbackByProfileAsync(
            UserPreference profile, int take)
        {
            var query = _context.RentalPosts
                .Include(p => p.Owner)
                .Include(p => p.RoomImages)
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved &&
                            p.RoomStatus == RoomStatus.Available);

            // Filter City
            if (!string.IsNullOrEmpty(profile.PreferredCity))
                query = query.Where(p => p.City == profile.PreferredCity);

            // Filter District
            if (!string.IsNullOrEmpty(profile.PreferredDistrict))
                query = query.Where(p => p.District == profile.PreferredDistrict);

            // Filter Price (±50%)
            if (profile.PriceMax > 0 && profile.PriceMax < 20_000_000m)
            {
                var lower = profile.PriceMin * 0.5m;
                var upper = profile.PriceMax * 1.5m;
                query = query.Where(p => p.Price >= lower && p.Price <= upper);
            }

            // Filter RoomType
            if (profile.PreferredRoomType.HasValue)
                query = query.Where(p => p.RoomType == profile.PreferredRoomType.Value);

            // ⭐ BỎ filter Area — quá chặt

            var results = await query
                .OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
                .Take(take)
                .ToListAsync();

            // ⭐ Nếu < take → nới lỏng (bỏ RoomType, bỏ District)
            if (results.Count < take && !string.IsNullOrEmpty(profile.PreferredCity))
            {
                var existingIds = results.Select(r => r.RentalPostId).ToHashSet();

                var loosened = await _context.RentalPosts
                    .Include(p => p.Owner)
                    .Include(p => p.RoomImages)
                    .Where(p => p.ApprovalStatus == ApprovalStatus.Approved &&
                                p.RoomStatus == RoomStatus.Available &&
                                p.City == profile.PreferredCity &&
                                !existingIds.Contains(p.RentalPostId))
                    .OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
                    .Take(take - results.Count)
                    .ToListAsync();

                results.AddRange(loosened);
            }

            // ⭐ Cuối cùng vẫn thiếu → dùng fallback thường
            if (results.Count < take)
            {
                var existingIds = results.Select(r => r.RentalPostId).ToHashSet();

                var fallback = await _context.RentalPosts
                    .Include(p => p.Owner)
                    .Include(p => p.RoomImages)
                    .Where(p => p.ApprovalStatus == ApprovalStatus.Approved &&
                                p.RoomStatus == RoomStatus.Available &&
                                !existingIds.Contains(p.RentalPostId))
                    .OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
                    .Take(take - results.Count)
                    .ToListAsync();

                results.AddRange(fallback);
            }

            return results;
        }

        // ============================================================
        // BUILD DISCO MODEL
        // ============================================================
        private async Task<Recommender<string, string>?> GetOrBuildModelAsync()
        {
            if (_cache.TryGetValue(MODEL_CACHE_KEY, out Recommender<string, string>? cached))
                return cached;

            var interactions = await GetAllInteractionsAsync();

            if (interactions.Count < 10)
            {
                _logger.LogInformation("Not enough data ({Count} interactions)", interactions.Count);
                return null;
            }

            var dataset = new Dataset<string, string>();
            foreach (var (uid, iid) in interactions)
                dataset.Add(uid, iid);

            var recommender = Recommender<string, string>.FitImplicit(dataset);

            _cache.Set(MODEL_CACHE_KEY, recommender, MODEL_CACHE_DURATION);
            _logger.LogInformation("Built Disco model with {Count} interactions", interactions.Count);

            return recommender;
        }

        // ============================================================
        // LẤY TẤT CẢ TƯƠNG TÁC — dùng cho model + evaluator
        // ============================================================
        public static async Task<List<(string UserId, string ItemId)>>
            GetAllInteractionsAsync(AppDbContext context)
        {
            var result = new List<(string, string)>();

            var favs = await context.Favorites
                .Select(f => new { f.UserId, f.RentalPostId })
                .ToListAsync();
            result.AddRange(favs.Select(f => (f.UserId, f.RentalPostId.ToString())));

            var views = await context.ViewingAppointments
                .Where(v => v.Status != ViewingAppointmentStatus.Cancelled &&
                            v.Status != ViewingAppointmentStatus.Rejected)
                .Select(v => new { v.TenantId, v.RentalPostId })
                .ToListAsync();
            result.AddRange(views.Select(v => (v.TenantId, v.RentalPostId.ToString())));

            var reqs = await context.RentalRequests
                .Where(r => r.Status != RentalRequestStatus.Cancelled)
                .Select(r => new { r.TenantId, r.RentalPostId })
                .ToListAsync();
            result.AddRange(reqs.Select(r => (r.TenantId, r.RentalPostId.ToString())));

            var convs = await context.Conversations
                .Where(c => c.RentalPostId != null)
                .Select(c => new { c.InitiatorId, RentalPostId = c.RentalPostId!.Value })
                .ToListAsync();
            result.AddRange(convs.Select(c => (c.InitiatorId, c.RentalPostId.ToString())));

            return result.Distinct().ToList();
        }

        private Task<List<(string UserId, string ItemId)>> GetAllInteractionsAsync()
            => GetAllInteractionsAsync(_context);

        // ============================================================
        // FALLBACK — phòng mới nhất
        // ============================================================
        private async Task<List<RentalPost>> GetFallbackAsync(int take)
        {
            return await _context.RentalPosts
                .Include(p => p.Owner)
                .Include(p => p.RoomImages)
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved &&
                            p.RoomStatus == RoomStatus.Available)
                .OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
                .Take(take)
                .ToListAsync();
        }

        // ============================================================
        // HELPER — Parse Amenities JSON
        // ============================================================
        private List<string> ParseAmenities(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new();
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new();
            }
            catch
            {
                return new();
            }
        }

        // ============================================================
        // XÓA CACHE khi có tương tác mới
        // ============================================================
        public void InvalidateModel()
        {
            _cache.Remove(MODEL_CACHE_KEY);
            _logger.LogInformation("🔄 Disco model cache invalidated");
        }
    }

    // ============================================================
    // DTO — Profile sở thích user
    // ============================================================
    public class UserPreference
    {
        public string? PreferredCity { get; set; }
        public string? PreferredDistrict { get; set; }
        public RoomType? PreferredRoomType { get; set; }
        public decimal AvgPrice { get; set; }
        public decimal PriceMin { get; set; } = 1_000_000m;
        public decimal PriceMax { get; set; } = 20_000_000m;
        public double MinArea { get; set; }
        public double MaxArea { get; set; }
        public List<string> PreferredAmenities { get; set; } = new();
    }
}