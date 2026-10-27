using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GamingTournament.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeleteUserRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoachRequests_AspNetUsers_PlayerId",
                table: "CoachRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamJoinRequests_AspNetUsers_PlayerId",
                table: "TeamJoinRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_CoachRequests_AspNetUsers_PlayerId",
                table: "CoachRequests",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamJoinRequests_AspNetUsers_PlayerId",
                table: "TeamJoinRequests",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CoachRequests_AspNetUsers_PlayerId",
                table: "CoachRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamJoinRequests_AspNetUsers_PlayerId",
                table: "TeamJoinRequests");

            migrationBuilder.AddForeignKey(
                name: "FK_CoachRequests_AspNetUsers_PlayerId",
                table: "CoachRequests",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamJoinRequests_AspNetUsers_PlayerId",
                table: "TeamJoinRequests",
                column: "PlayerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
