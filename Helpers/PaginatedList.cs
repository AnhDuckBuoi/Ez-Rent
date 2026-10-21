using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Helpers
{
    public class PaginatedList<T> : List<T>
    {
        public int PageIndex  { get; set; }
        public int TotalPages { get; set; }
        public int PageSize   { get; set; }
        public int TotalItems { get; set; }

        public bool HasPreviousPage => PageIndex > 1;
        public bool HasNextPage     => PageIndex < TotalPages;

        public PaginatedList(List<T> items, int count, int pageIndex, int pageSize)
        {
            PageIndex  = pageIndex;
            PageSize   = pageSize;
            TotalItems = count;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            AddRange(items);
        }

        public static async Task<PaginatedList<T>> CreateAsync(
            IQueryable<T> source,
            int pageIndex,
            int pageSize)
        {
            var count = await source.CountAsync();
            var items = await source
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedList<T>(items, count, pageIndex, pageSize);
        }
    }
}