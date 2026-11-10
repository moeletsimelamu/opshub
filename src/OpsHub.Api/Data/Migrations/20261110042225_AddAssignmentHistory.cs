using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpsHub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TicketAssignmentHistory",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TicketId = table.Column<int>(type: "INTEGER", nullable: false),
                    FromAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ChangedById = table.Column<Guid>(type: "TEXT", nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TicketAssignmentHistory", x => x.Id);
                    table.CheckConstraint("CK_TicketAssignmentHistory_Changes", "\"FromAgentId\" IS NOT NULL OR \"ToAgentId\" IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_TicketAssignmentHistory_AspNetUsers_ChangedById",
                        column: x => x.ChangedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketAssignmentHistory_AspNetUsers_FromAgentId",
                        column: x => x.FromAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketAssignmentHistory_AspNetUsers_ToAgentId",
                        column: x => x.ToAgentId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TicketAssignmentHistory_Tickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "Tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentHistory_ChangedById",
                table: "TicketAssignmentHistory",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentHistory_FromAgentId",
                table: "TicketAssignmentHistory",
                column: "FromAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentHistory_TicketId_ChangedAt",
                table: "TicketAssignmentHistory",
                columns: new[] { "TicketId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TicketAssignmentHistory_ToAgentId",
                table: "TicketAssignmentHistory",
                column: "ToAgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TicketAssignmentHistory");
        }
    }
}
