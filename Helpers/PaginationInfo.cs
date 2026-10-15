namespace EZ_Rent.Helpers
{
    public class PaginationInfo
    {
        public int PageIndex  { get; set; }
        public int TotalPages { get; set; }
        public int TotalItems { get; set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage     => PageIndex < TotalPages;

        public string Action     { get; set; } = "Index";
        public string Controller { get; set; } = "";
        public string? Area      { get; set; }
        public Dictionary<string, string?> RouteValues { get; set; } = new();

        // Helper tạo từ PaginatedList
        public static PaginationInfo From<T>(
            PaginatedList<T> list,
            string action,
            string controller,
            string? area = null,
            Dictionary<string, string?>? routeValues = null)
        {
            return new PaginationInfo
            {
                PageIndex   = list.PageIndex,
                TotalPages  = list.TotalPages,
                TotalItems  = list.TotalItems,
                Action      = action,
                Controller  = controller,
                Area        = area,
                RouteValues = routeValues ?? new()
            };
        }
    }
}