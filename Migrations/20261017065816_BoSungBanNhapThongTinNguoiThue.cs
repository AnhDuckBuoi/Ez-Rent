using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EZ_Rent.Migrations
{
    /// <inheritdoc />
    public partial class BoSungBanNhapThongTinNguoiThue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractTenantDrafts",
                columns: table => new
                {
                    ContractTenantDraftId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RentalContractId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Revision = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TemplateVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ContractStamp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    PermanentAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DocumentKind = table.Column<int>(type: "int", nullable: true),
                    DocumentNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DocumentIssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    DocumentIssuedAt = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    LandlordName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    LandlordPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RoomAddress = table.Column<string>(type: "nvarchar(700)", maxLength: 700, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MonthlyRent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElectricityPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ContractTerms = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTenantDrafts", x => x.ContractTenantDraftId);
                    table.ForeignKey(
                        name: "FK_ContractTenantDrafts_RentalContracts_RentalContractId",
                        column: x => x.RentalContractId,
                        principalTable: "RentalContracts",
                        principalColumn: "RentalContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractTenantDrafts_RentalContractId_TenantId_Revision",
                table: "ContractTenantDrafts",
                columns: new[] { "RentalContractId", "TenantId", "Revision" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractTenantDrafts");
        }
    }
}
