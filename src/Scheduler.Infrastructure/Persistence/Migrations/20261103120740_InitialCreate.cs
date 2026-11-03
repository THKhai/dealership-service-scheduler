using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Scheduler.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dealership",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dealership", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_type",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    required_bay_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_type", x => x.id);
                    table.CheckConstraint("ck_service_type_duration", "duration_minutes > 0");
                });

            migrationBuilder.CreateTable(
                name: "vehicle",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vin = table.Column<string>(type: "character varying(17)", maxLength: 17, nullable: false),
                    make = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicle", x => x.id);
                    table.ForeignKey(
                        name: "fk_vehicle_customer_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opening_hours",
                columns: table => new
                {
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    open_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    close_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_hours", x => new { x.dealership_id, x.day_of_week });
                    table.CheckConstraint("ck_opening_hours_range", "close_time > open_time");
                    table.ForeignKey(
                        name: "fk_opening_hours_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealership",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_bay",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    bay_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_bay", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_bay_dealership_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealership",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technician",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technician", x => x.id);
                    table.ForeignKey(
                        name: "fk_technician_dealership_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealership",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "appointment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dealership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_bay_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointment", x => x.id);
                    table.CheckConstraint("ck_appointment_range", "end_utc > start_utc");
                    table.ForeignKey(
                        name: "fk_appointment_customers_customer_id",
                        column: x => x.customer_id,
                        principalTable: "customer",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointment_dealerships_dealership_id",
                        column: x => x.dealership_id,
                        principalTable: "dealership",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointment_service_bays_service_bay_id",
                        column: x => x.service_bay_id,
                        principalTable: "service_bay",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointment_service_types_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "service_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointment_technicians_technician_id",
                        column: x => x.technician_id,
                        principalTable: "technician",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_appointment_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicle",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "technician_skill",
                columns: table => new
                {
                    technician_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_type_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technician_skill", x => new { x.technician_id, x.service_type_id });
                    table.ForeignKey(
                        name: "fk_technician_skill_service_type_service_type_id",
                        column: x => x.service_type_id,
                        principalTable: "service_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_technician_skill_technician_technician_id",
                        column: x => x.technician_id,
                        principalTable: "technician",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "dealership",
                columns: new[] { "id", "name", "time_zone" },
                values: new object[] { new Guid("d0000000-0000-0000-0000-000000000001"), "Keyloop Motors District 1", "Asia/Ho_Chi_Minh" });

            migrationBuilder.InsertData(
                table: "service_type",
                columns: new[] { "id", "code", "duration_minutes", "name", "required_bay_type" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), "oil-change", 45, "Oil change", "General" },
                    { new Guid("50000000-0000-0000-0000-000000000002"), "brake-service", 90, "Brake service", "Lift" },
                    { new Guid("50000000-0000-0000-0000-000000000003"), "ev-battery-check", 60, "EV battery check", "EV" }
                });

            migrationBuilder.InsertData(
                table: "opening_hours",
                columns: new[] { "day_of_week", "dealership_id", "close_time", "open_time" },
                values: new object[,]
                {
                    { 1, new Guid("d0000000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { 2, new Guid("d0000000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { 3, new Guid("d0000000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { 4, new Guid("d0000000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { 5, new Guid("d0000000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) },
                    { 6, new Guid("d0000000-0000-0000-0000-000000000001"), new TimeOnly(17, 0, 0), new TimeOnly(8, 0, 0) }
                });

            migrationBuilder.InsertData(
                table: "service_bay",
                columns: new[] { "id", "bay_type", "dealership_id", "is_active", "name" },
                values: new object[,]
                {
                    { new Guid("b0000000-0000-0000-0000-000000000001"), "Lift", new Guid("d0000000-0000-0000-0000-000000000001"), true, "Bay 1" },
                    { new Guid("b0000000-0000-0000-0000-000000000002"), "Lift", new Guid("d0000000-0000-0000-0000-000000000001"), true, "Bay 2" },
                    { new Guid("b0000000-0000-0000-0000-000000000003"), "General", new Guid("d0000000-0000-0000-0000-000000000001"), true, "Bay 3" },
                    { new Guid("b0000000-0000-0000-0000-000000000004"), "EV", new Guid("d0000000-0000-0000-0000-000000000001"), true, "Bay 4" }
                });

            migrationBuilder.InsertData(
                table: "technician",
                columns: new[] { "id", "dealership_id", "is_active", "name" },
                values: new object[,]
                {
                    { new Guid("70000000-0000-0000-0000-000000000001"), new Guid("d0000000-0000-0000-0000-000000000001"), true, "An" },
                    { new Guid("70000000-0000-0000-0000-000000000002"), new Guid("d0000000-0000-0000-0000-000000000001"), true, "Binh" },
                    { new Guid("70000000-0000-0000-0000-000000000003"), new Guid("d0000000-0000-0000-0000-000000000001"), true, "Chi" }
                });

            migrationBuilder.InsertData(
                table: "technician_skill",
                columns: new[] { "service_type_id", "technician_id" },
                values: new object[,]
                {
                    { new Guid("50000000-0000-0000-0000-000000000001"), new Guid("70000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000002"), new Guid("70000000-0000-0000-0000-000000000001") },
                    { new Guid("50000000-0000-0000-0000-000000000002"), new Guid("70000000-0000-0000-0000-000000000002") },
                    { new Guid("50000000-0000-0000-0000-000000000003"), new Guid("70000000-0000-0000-0000-000000000002") },
                    { new Guid("50000000-0000-0000-0000-000000000001"), new Guid("70000000-0000-0000-0000-000000000003") },
                    { new Guid("50000000-0000-0000-0000-000000000002"), new Guid("70000000-0000-0000-0000-000000000003") },
                    { new Guid("50000000-0000-0000-0000-000000000003"), new Guid("70000000-0000-0000-0000-000000000003") }
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_customer_id",
                table: "appointment",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_dealership_id_start_utc",
                table: "appointment",
                columns: new[] { "dealership_id", "start_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_appointment_idempotency_key",
                table: "appointment",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointment_service_bay_id",
                table: "appointment",
                column: "service_bay_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_service_type_id",
                table: "appointment",
                column: "service_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_technician_id",
                table: "appointment",
                column: "technician_id");

            migrationBuilder.CreateIndex(
                name: "ix_appointment_vehicle_id",
                table: "appointment",
                column: "vehicle_id");

            migrationBuilder.CreateIndex(
                name: "ix_customer_email",
                table: "customer",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_bay_dealership_id",
                table: "service_bay",
                column: "dealership_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_type_code",
                table: "service_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technician_dealership_id",
                table: "technician",
                column: "dealership_id");

            migrationBuilder.CreateIndex(
                name: "ix_technician_skill_service_type_id",
                table: "technician_skill",
                column: "service_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_customer_id",
                table: "vehicle",
                column: "customer_id");

            migrationBuilder.CreateIndex(
                name: "ix_vehicle_vin",
                table: "vehicle",
                column: "vin",
                unique: true);

            // --- Double-booking protection (section 8.3). EF Core cannot model these. ---
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // Half-open range [start, end): back-to-back appointments do not overlap (A-05).
            migrationBuilder.Sql("""
                ALTER TABLE appointment
                  ADD COLUMN slot tstzrange
                  GENERATED ALWAYS AS (tstzrange(start_utc, end_utc, '[)')) STORED;
                """);

            // Only Confirmed appointments hold resources (A-08).
            migrationBuilder.Sql("""
                ALTER TABLE appointment ADD CONSTRAINT ex_appointment_bay
                  EXCLUDE USING gist (service_bay_id WITH =, slot WITH &&) WHERE (status = 'Confirmed');
                ALTER TABLE appointment ADD CONSTRAINT ex_appointment_technician
                  EXCLUDE USING gist (technician_id WITH =, slot WITH &&) WHERE (status = 'Confirmed');
                ALTER TABLE appointment ADD CONSTRAINT ex_appointment_vehicle
                  EXCLUDE USING gist (vehicle_id WITH =, slot WITH &&) WHERE (status = 'Confirmed');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE appointment DROP CONSTRAINT IF EXISTS ex_appointment_vehicle;
                ALTER TABLE appointment DROP CONSTRAINT IF EXISTS ex_appointment_technician;
                ALTER TABLE appointment DROP CONSTRAINT IF EXISTS ex_appointment_bay;
                ALTER TABLE appointment DROP COLUMN IF EXISTS slot;
                """);

            migrationBuilder.DropTable(
                name: "appointment");

            migrationBuilder.DropTable(
                name: "opening_hours");

            migrationBuilder.DropTable(
                name: "technician_skill");

            migrationBuilder.DropTable(
                name: "service_bay");

            migrationBuilder.DropTable(
                name: "vehicle");

            migrationBuilder.DropTable(
                name: "service_type");

            migrationBuilder.DropTable(
                name: "technician");

            migrationBuilder.DropTable(
                name: "customer");

            migrationBuilder.DropTable(
                name: "dealership");
        }
    }
}
