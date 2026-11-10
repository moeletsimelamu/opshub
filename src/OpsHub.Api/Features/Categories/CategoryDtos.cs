using System.ComponentModel.DataAnnotations;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Features.Categories;

/// <summary>A category as administrators see it, with how many tickets use it.</summary>
public sealed record CategoryAdminResponse(int Id, string Name, bool IsActive, int TicketCount);

public sealed record CreateCategoryRequest
{
    [Required, MaxLength(Category.NameMaxLength)]
    public string Name { get; init; } = string.Empty;
}

public sealed record UpdateCategoryRequest
{
    [Required, MaxLength(Category.NameMaxLength)]
    public string Name { get; init; } = string.Empty;

    /// <summary>False hides the category from the ticket forms; tickets that use it keep it.</summary>
    [Required]
    public bool? IsActive { get; init; }
}
