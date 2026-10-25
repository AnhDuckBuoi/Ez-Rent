using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EZ_Rent.Migrations
{
    /// <inheritdoc />
    public partial class LuuTaiLieuHopDongVaAnhCongTo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentDocumentVersion",
                table: "RentalContracts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContractDocuments",
                columns: table => new
                {
                    ContractDocumentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RentalContractId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ByteLength = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TenantAcknowledgedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TenantAcknowledgedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractDocuments", x => x.ContractDocumentId);
                    table.ForeignKey(
                        name: "FK_ContractDocuments_RentalContracts_RentalContractId",
                        column: x => x.RentalContractId,
                        principalTable: "RentalContracts",
                        principalColumn: "RentalContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceMeterEvidence",
                columns: table => new
                {
                    InvoiceMeterEvidenceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RentalPostId = table.Column<int>(type: "int", nullable: false),
                    Period = table.Column<string>(type: "nvarchar(7)", maxLength: 7, nullable: false),
                    ReadingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PrevElectricIndex = table.Column<double>(type: "float", nullable: false),
                    CurrElectricIndex = table.Column<double>(type: "float", nullable: false),
                    ElectricUsage = table.Column<double>(type: "float", nullable: false),
                    ElectricityPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrevWaterIndex = table.Column<double>(type: "float", nullable: false),
                    CurrWaterIndex = table.Column<double>(type: "float", nullable: false),
                    WaterUsage = table.Column<double>(type: "float", nullable: false),
                    WaterPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElectricStorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    WaterStorageKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ElectricContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    WaterContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ElectricSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WaterSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CapturedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceMeterEvidence", x => x.InvoiceMeterEvidenceId);
                    table.ForeignKey(
                        name: "FK_InvoiceMeterEvidence_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "InvoiceId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractDocuments_RentalContractId_Version",
                table: "ContractDocuments",
                columns: new[] { "RentalContractId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceMeterEvidence_InvoiceId",
                table: "InvoiceMeterEvidence",
                column: "InvoiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractDocuments");

            migrationBuilder.DropTable(
                name: "InvoiceMeterEvidence");

            migrationBuilder.DropColumn(
                name: "CurrentDocumentVersion",
                table: "RentalContracts");
        }
    }
}
