using PoC.Shared.Common;

namespace PoC.Materials.API.Extensions;

public static class PagedResponseExtensions
{
    public static PagedResponse<T> ToPagedResponse<T>(
        this PagedResult<T> result,
        HttpContext httpContext,
        LinkGenerator linkGenerator,
        string routeName,
        int limit,
        string? currentCursor)
    {
        var meta = new PaginationMeta(limit, result.Count, result.Cursor);
        
        var links = new List<Link>
        {
            new Link(
                "self",
                linkGenerator.GetUriByName(httpContext, routeName, new { limit, cursor = currentCursor }) ?? string.Empty,
                "GET")
        };

        if (!string.IsNullOrEmpty(result.Cursor))
        {
            links.Add(new Link(
                "next",
                linkGenerator.GetUriByName(httpContext, routeName, new { limit, cursor = result.Cursor }) ?? string.Empty,
                "GET"));
        }

        return new PagedResponse<T>(result.Items, meta, links);
    }
}
