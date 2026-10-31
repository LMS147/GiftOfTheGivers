using System.ComponentModel.DataAnnotations;
namespace GiftOfTheGivers.Helpers.Models;

public class StaffAuditEntry
{
    public int Id { get; set; }
    [StringLength(450)] public string? ActorUserId { get; set; }
    [Required, StringLength(256)] public string ActorName { get; set; } = "Staff";
    [Required, StringLength(50)] public string Action { get; set; } = "";
    [Required, StringLength(50)] public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    [Required, StringLength(1000)] public string Summary { get; set; } = "";
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
