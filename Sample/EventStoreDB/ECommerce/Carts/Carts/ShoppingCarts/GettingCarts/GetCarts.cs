using Core.Queries;
using Marten;
using Marten.Pagination;

namespace Carts.ShoppingCarts.GettingCarts;

public record GetCarts(
    int PageNumber,
    int PageSize
)
{
    public static GetCarts Create(int? pageNumber = 1, int? pageSize = 20)
    {
        if (pageNumber is null or <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (pageSize is null or <= 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        return new GetCarts(pageNumber.Value, pageSize.Value);
    }
}

internal class HandleGetCarts(IQuerySession querySession):
    IQueryHandler<GetCarts, IPagedList<ShoppingCartShortInfo>>
{
    public Task<IPagedList<ShoppingCartShortInfo>> Handle(GetCarts request, CancellationToken cancellationToken)
    {
        var (pageNumber, pageSize) = request;

        return querySession.Query<ShoppingCartShortInfo>()
            .ToPagedListAsync(pageNumber, pageSize, cancellationToken);
    }
}
