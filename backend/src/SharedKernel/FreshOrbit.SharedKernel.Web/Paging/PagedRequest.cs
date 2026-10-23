using FluentValidation;

namespace FreshOrbit.SharedKernel.Web.Paging;

/// <summary>Query parameters <c>page</c> (1-based) and <c>pageSize</c> (1–100). Bind with <c>[AsParameters]</c>.</summary>
public sealed record PagedRequest(int Page = 1, int PageSize = PagedRequest.DefaultPageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;
}

/// <summary>A page of a list: <c>{ items, page, pageSize, totalCount }</c>.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public sealed class PagedRequestValidator : AbstractValidator<PagedRequest>
{
    public PagedRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrorCode("PAGE_OUT_OF_RANGE");
        RuleFor(x => x.PageSize).InclusiveBetween(1, PagedRequest.MaxPageSize).WithErrorCode("PAGE_SIZE_OUT_OF_RANGE");
    }
}
