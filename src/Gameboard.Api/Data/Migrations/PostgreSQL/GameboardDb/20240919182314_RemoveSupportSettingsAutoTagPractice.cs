﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Gameboard.Api.Data.Migrations.PostgreSQL.GameboardDb
{
    /// <inheritdoc />
    public partial class RemoveSupportSettingsAutoTagPractice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AutoTagPracticeTicketsWith",
                table: "SupportSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AutoTagPracticeTicketsWith",
                table: "SupportSettings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);
        }
    }
}
