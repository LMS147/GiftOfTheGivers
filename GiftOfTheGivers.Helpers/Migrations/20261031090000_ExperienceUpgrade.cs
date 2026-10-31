using GiftOfTheGivers.Helpers.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace GiftOfTheGivers.Helpers.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261031090000_ExperienceUpgrade")]
public partial class ExperienceUpgrade : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("Category", "ReliefProjects", type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "Other");
        m.AddColumn<decimal>("TargetAmount", "ReliefProjects", type: "decimal(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m);
        m.AddColumn<bool>("IsActive", "ReliefProjects", type: "bit", nullable: false, defaultValue: true);
        m.AddColumn<string>("ImageUrl", "ReliefProjects", type: "nvarchar(500)", maxLength: 500, nullable: true);
        m.AddColumn<int>("ReliefProjectId", "Donations", type: "int", nullable: true);
        m.AddColumn<Guid>("SubmissionKey", "Donations", type: "uniqueidentifier", nullable: true);
        m.CreateIndex("IX_Donations_ReliefProjectId", "Donations", "ReliefProjectId");
        m.CreateIndex("IX_Donations_SubmissionKey", "Donations", "SubmissionKey", unique: true, filter: "[SubmissionKey] IS NOT NULL");
        m.AddForeignKey("FK_Donations_ReliefProjects_ReliefProjectId", "Donations", "ReliefProjectId", "ReliefProjects", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
        m.CreateTable("Notifications", columns: t => new
        {
            Id = t.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            UserId = t.Column<string>(maxLength: 450, nullable: true),
            Email = t.Column<string>(maxLength: 254, nullable: false),
            Subject = t.Column<string>(maxLength: 200, nullable: false),
            Body = t.Column<string>(maxLength: 2000, nullable: false),
            CreatedAtUtc = t.Column<DateTime>(nullable: false), ReadAtUtc = t.Column<DateTime>(nullable: true),
            SentAtUtc = t.Column<DateTime>(nullable: true), DeliveryAttempts = t.Column<int>(nullable: false),
            NextAttemptAtUtc = t.Column<DateTime>(nullable: true), LastDeliveryError = t.Column<string>(maxLength: 200, nullable: true)
        }, constraints: t => t.PrimaryKey("PK_Notifications", x => x.Id));
        m.CreateIndex("IX_Notifications_UserId_ReadAtUtc", "Notifications", new[] { "UserId", "ReadAtUtc" });
        m.CreateIndex("IX_Notifications_SentAtUtc_NextAttemptAtUtc", "Notifications", new[] { "SentAtUtc", "NextAttemptAtUtc" });
        m.CreateTable("StaffAuditEntries", columns: t => new
        {
            Id = t.Column<int>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
            ActorUserId = t.Column<string>(maxLength: 450, nullable: true), ActorName = t.Column<string>(maxLength: 256, nullable: false),
            Action = t.Column<string>(maxLength: 50, nullable: false), EntityType = t.Column<string>(maxLength: 50, nullable: false),
            EntityId = t.Column<int>(nullable: false), Summary = t.Column<string>(maxLength: 1000, nullable: false),
            OccurredAtUtc = t.Column<DateTime>(nullable: false)
        }, constraints: t => t.PrimaryKey("PK_StaffAuditEntries", x => x.Id));
        m.CreateIndex("IX_StaffAuditEntries_OccurredAtUtc", "StaffAuditEntries", "OccurredAtUtc");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("Notifications"); m.DropTable("StaffAuditEntries");
        m.DropForeignKey("FK_Donations_ReliefProjects_ReliefProjectId", "Donations");
        m.DropIndex("IX_Donations_ReliefProjectId", "Donations"); m.DropIndex("IX_Donations_SubmissionKey", "Donations");
        m.DropColumn("ReliefProjectId", "Donations"); m.DropColumn("SubmissionKey", "Donations");
        foreach (var column in new[] { "Category", "TargetAmount", "IsActive", "ImageUrl" }) m.DropColumn(column, "ReliefProjects");
    }
}
