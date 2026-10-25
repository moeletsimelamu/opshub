using System.ComponentModel.DataAnnotations;
using OpsHub.Api.Common;
using OpsHub.Domain.Tickets;

namespace OpsHub.Api.Features.Tickets;

// ---------- Responses ----------

public sealed record PersonSummary(Guid Id, string DisplayName);

public sealed record CategorySummary(int Id, string Name);

/// <summary>One row in the ticket list: only what the table shows.</summary>
public sealed record TicketListItem(
    int Id,
    string Reference,
    string Title,
    TicketStatus Status,
    TicketPriority Priority,
    CategorySummary Category,
    PersonSummary Requester,
    PersonSummary? AssignedAgent,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// What the current user may do with this ticket, so the UI shows only controls that will
/// work. The API enforces every one of these rules again; this is a convenience, not security.
/// </summary>
public sealed record TicketPermissions(
    bool CanEdit,
    bool CanEditPriority,
    bool CanAssign,
    bool CanComment,
    bool CanAddInternalComment,
    IReadOnlyList<TicketStatus> AllowedStatuses);

public sealed record TicketDetails(
    int Id,
    string Reference,
    string Title,
    string Description,
    TicketStatus Status,
    TicketPriority Priority,
    CategorySummary Category,
    PersonSummary Requester,
    PersonSummary? AssignedAgent,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? ResolvedAt,
    Guid Version,
    TicketPermissions Permissions);

public sealed record CommentResponse(
    int Id,
    string Body,
    bool IsInternal,
    PersonSummary Author,
    bool AuthorIsStaff,
    DateTime CreatedAt);

public sealed record StatusHistoryResponse(
    int Id,
    TicketStatus? FromStatus,
    TicketStatus ToStatus,
    PersonSummary ChangedBy,
    string? Note,
    DateTime ChangedAt);

public sealed record AgentSummary(Guid Id, string DisplayName, int ActiveTicketCount);

// ---------- Requests ----------

/// <summary>
/// GET /api/tickets?status=Open&amp;status=InProgress&amp;priority=High&amp;categoryId=2
/// &amp;assigneeId=...&amp;unassigned=true&amp;search=vpn&amp;sortBy=priority&amp;sortDirection=desc&amp;page=1&amp;pageSize=20
/// </summary>
public sealed class TicketListQuery : PageQuery
{
    /// <summary>Repeat the parameter to match several statuses.</summary>
    public IReadOnlyList<TicketStatus>? Status { get; set; }

    public IReadOnlyList<TicketPriority>? Priority { get; set; }

    public int? CategoryId { get; set; }

    public Guid? AssigneeId { get; set; }

    /// <summary>true = only tickets nobody is assigned to (the triage queue).</summary>
    public bool? Unassigned { get; set; }

    /// <summary>Matches title or description, or a reference such as "OPS-000012" or "12".</summary>
    [MaxLength(100)]
    public string? Search { get; set; }

    [AllowedValues(null, "updatedAt", "createdAt", "priority", "status", "title", "id")]
    public string? SortBy { get; set; }

    [AllowedValues(null, "asc", "desc")]
    public string? SortDirection { get; set; }
}

public sealed record CreateTicketRequest
{
    [Required, MinLength(5), MaxLength(Ticket.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required, MinLength(10), MaxLength(Ticket.DescriptionMaxLength)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; init; }

    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; init; } = TicketPriority.Medium;
}

public sealed record UpdateTicketRequest
{
    [Required, MinLength(5), MaxLength(Ticket.TitleMaxLength)]
    public string Title { get; init; } = string.Empty;

    [Required, MinLength(10), MaxLength(Ticket.DescriptionMaxLength)]
    public string Description { get; init; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Choose a category.")]
    public int CategoryId { get; init; }

    [EnumDataType(typeof(TicketPriority))]
    public TicketPriority Priority { get; init; }

    /// <summary>The version the client last saw (from TicketDetails). Stale versions get 409.</summary>
    [Required]
    public Guid? Version { get; init; }
}

public sealed record AssignTicketRequest
{
    /// <summary>The agent to assign, or null to unassign.</summary>
    public Guid? AgentId { get; init; }

    [Required]
    public Guid? Version { get; init; }
}

public sealed record ChangeStatusRequest
{
    [Required, EnumDataType(typeof(TicketStatus))]
    public TicketStatus? Status { get; init; }

    [MaxLength(TicketStatusHistory.NoteMaxLength)]
    public string? Note { get; init; }

    [Required]
    public Guid? Version { get; init; }
}

public sealed record AddCommentRequest
{
    [Required, MaxLength(TicketComment.BodyMaxLength)]
    public string Body { get; init; } = string.Empty;

    /// <summary>Internal notes are visible to support staff only.</summary>
    public bool IsInternal { get; init; }
}
