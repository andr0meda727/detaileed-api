using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudioLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city",
                schema: "identity",
                table: "studio",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                schema: "identity",
                table: "studio",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                schema: "identity",
                table: "studio",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_studio_latitude",
                schema: "identity",
                table: "studio",
                sql: "latitude BETWEEN -90 AND 90");

            migrationBuilder.AddCheckConstraint(
                name: "CK_studio_location_pair",
                schema: "identity",
                table: "studio",
                sql: "(latitude IS NULL) = (longitude IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_studio_longitude",
                schema: "identity",
                table: "studio",
                sql: "longitude BETWEEN -180 AND 180");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_studio_latitude",
                schema: "identity",
                table: "studio");

            migrationBuilder.DropCheckConstraint(
                name: "CK_studio_location_pair",
                schema: "identity",
                table: "studio");

            migrationBuilder.DropCheckConstraint(
                name: "CK_studio_longitude",
                schema: "identity",
                table: "studio");

            migrationBuilder.DropColumn(
                name: "city",
                schema: "identity",
                table: "studio");

            migrationBuilder.DropColumn(
                name: "latitude",
                schema: "identity",
                table: "studio");

            migrationBuilder.DropColumn(
                name: "longitude",
                schema: "identity",
                table: "studio");
        }
    }
}
