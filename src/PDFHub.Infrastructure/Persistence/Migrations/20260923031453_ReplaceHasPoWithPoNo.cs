using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDFHub.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceHasPoWithPoNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PoNo",
                table: "Drawings",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            // The old tick carried no number: keep the fact that a PO exists (PdfCodeRules.PoWithoutNumber).
            migrationBuilder.Sql("UPDATE Drawings SET PoNo = 'มี PO (ไม่ระบุเลขที่)' WHERE HasPo = 1;");

            migrationBuilder.DropColumn(
                name: "HasPo",
                table: "Drawings");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasPo",
                table: "Drawings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE Drawings SET HasPo = 1 WHERE PoNo IS NOT NULL;");

            migrationBuilder.DropColumn(
                name: "PoNo",
                table: "Drawings");
        }
    }
}
