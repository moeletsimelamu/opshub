namespace OpsHub.Domain.Tickets;

public class Category
{
    public const int NameMaxLength = 60;

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Inactive categories stay on old tickets but cannot be chosen for new ones.</summary>
    public bool IsActive { get; set; } = true;

    public List<Ticket> Tickets { get; set; } = [];
}
