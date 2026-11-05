using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PricingExceptions.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PricingExceptionRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MortgageApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AssignedReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedReviewerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentRevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingExceptionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingExceptionRequests_Users_AssignedReviewerId",
                        column: x => x.AssignedReviewerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PricingExceptionRequests_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IdempotencyRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationScope = table.Column<string>(type: "text", nullable: false),
                    Key = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "text", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "integer", nullable: false),
                    ResponseJson = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_PricingExceptionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PricingExceptionRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_IdempotencyRecords_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RequestRevisions",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    DiscountBasisPoints = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false),
                    SubmittedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestRevisions", x => new { x.RequestId, x.RevisionNumber });
                    table.ForeignKey(
                        name: "FK_RequestRevisions_PricingExceptionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PricingExceptionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequestRevisions_Users_SubmittedByUserId",
                        column: x => x.SubmittedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoryEntries_PricingExceptionRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "PricingExceptionRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoryEntries_RequestRevisions_RequestId_RevisionNumber",
                        columns: x => new { x.RequestId, x.RevisionNumber },
                        principalTable: "RequestRevisions",
                        principalColumns: new[] { "RequestId", "RevisionNumber" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoryEntries_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ReviewReason = table.Column<string>(type: "text", nullable: true),
                    ReviewedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerName = table.Column<string>(type: "text", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_RequestRevisions_RequestId_RevisionNumber",
                        columns: x => new { x.RequestId, x.RevisionNumber },
                        principalTable: "RequestRevisions",
                        principalColumns: new[] { "RequestId", "RevisionNumber" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_ActorUserId",
                table: "HistoryEntries",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoryEntries_RequestId_RevisionNumber",
                table: "HistoryEntries",
                columns: new[] { "RequestId", "RevisionNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_ActorUserId",
                table: "IdempotencyRecords",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_RequestId",
                table: "IdempotencyRecords",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingExceptionRequests_AssignedReviewerId",
                table: "PricingExceptionRequests",
                column: "AssignedReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingExceptionRequests_CreatedByUserId",
                table: "PricingExceptionRequests",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestRevisions_SubmittedByUserId",
                table: "RequestRevisions",
                column: "SubmittedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_RequestId_RevisionNumber",
                table: "ReviewDecisions",
                columns: new[] { "RequestId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewedByUserId",
                table: "ReviewDecisions",
                column: "ReviewedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoryEntries");

            migrationBuilder.DropTable(
                name: "IdempotencyRecords");

            migrationBuilder.DropTable(
                name: "ReviewDecisions");

            migrationBuilder.DropTable(
                name: "RequestRevisions");

            migrationBuilder.DropTable(
                name: "PricingExceptionRequests");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
