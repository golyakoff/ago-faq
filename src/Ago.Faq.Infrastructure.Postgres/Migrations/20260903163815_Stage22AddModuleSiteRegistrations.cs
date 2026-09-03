using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ago.Faq.Infrastructure.Postgres.Migrations;

/// <inheritdoc />
public partial class Stage22AddModuleSiteRegistrations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "module_site_registrations",
            columns: table => new
            {
                site_id = table.Column<Guid>(type: "uuid", nullable: false),
                credential = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                registered_at = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_module_site_registrations", x => x.site_id);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "module_site_registrations");
    }
}
