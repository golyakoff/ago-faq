using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ago.Faq.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class Stage22AddModuleSiteRegistrationRotation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "previous_credential",
            table: "module_site_registrations",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "previous_credential_expires_at",
            table: "module_site_registrations",
            type: "timestamptz",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "previous_credential",
            table: "module_site_registrations");

        migrationBuilder.DropColumn(
            name: "previous_credential_expires_at",
            table: "module_site_registrations");
    }
}
