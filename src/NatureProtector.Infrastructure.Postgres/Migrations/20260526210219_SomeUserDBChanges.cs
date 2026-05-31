using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NatureProtector.Infrastructure.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class SomeUserDBChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_roles_RolesId",
                schema: "user_base",
                table: "user_roles");

            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_users_UserId1",
                schema: "user_base",
                table: "user_roles");

            migrationBuilder.DropIndex(
                name: "IX_user_roles_RolesId",
                schema: "user_base",
                table: "user_roles");

            migrationBuilder.DropColumn(
                name: "RolesId",
                schema: "user_base",
                table: "user_roles");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId1",
                schema: "user_base",
                table: "user_roles",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_users_UserId1",
                schema: "user_base",
                table: "user_roles",
                column: "UserId1",
                principalSchema: "user_base",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_roles_users_UserId1",
                schema: "user_base",
                table: "user_roles");

            migrationBuilder.AlterColumn<Guid>(
                name: "UserId1",
                schema: "user_base",
                table: "user_roles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<short>(
                name: "RolesId",
                schema: "user_base",
                table: "user_roles",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_RolesId",
                schema: "user_base",
                table: "user_roles",
                column: "RolesId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_roles_RolesId",
                schema: "user_base",
                table: "user_roles",
                column: "RolesId",
                principalSchema: "user_base",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_user_roles_users_UserId1",
                schema: "user_base",
                table: "user_roles",
                column: "UserId1",
                principalSchema: "user_base",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
