using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameServer.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountsTableAndDropDiscordUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Player_DiscordUserId",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "DiscordUserId",
                table: "Player");

            migrationBuilder.Sql("DELETE FROM \"BattleResult\";");
            migrationBuilder.Sql("DELETE FROM \"Relic\";");
            migrationBuilder.Sql("DELETE FROM \"PlayerUnlockedCard\";");
            migrationBuilder.Sql("DELETE FROM \"Pet\";");
            migrationBuilder.Sql("DELETE FROM \"Player\";");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "Player",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Account",
                columns: table => new
                {
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Username = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Account", x => x.AccountId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Player_AccountId",
                table: "Player",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Account_Username",
                table: "Account",
                column: "Username",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Player_Account_AccountId",
                table: "Player",
                column: "AccountId",
                principalTable: "Account",
                principalColumn: "AccountId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Player_Account_AccountId",
                table: "Player");

            migrationBuilder.DropTable(
                name: "Account");

            migrationBuilder.DropIndex(
                name: "IX_Player_AccountId",
                table: "Player");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "Player");

            migrationBuilder.AddColumn<string>(
                name: "DiscordUserId",
                table: "Player",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Player_DiscordUserId",
                table: "Player",
                column: "DiscordUserId",
                unique: true);
        }
    }
}
