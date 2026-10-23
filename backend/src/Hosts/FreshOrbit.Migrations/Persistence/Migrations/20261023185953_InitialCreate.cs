using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FreshOrbit.Migrations.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "customers");

            migrationBuilder.EnsureSchema(
                name: "inventory");

            migrationBuilder.EnsureSchema(
                name: "settings");

            migrationBuilder.EnsureSchema(
                name: "cart");

            migrationBuilder.EnsureSchema(
                name: "payments");

            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "locations");

            migrationBuilder.EnsureSchema(
                name: "ordering");

            migrationBuilder.EnsureSchema(
                name: "promotions");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.EnsureSchema(
                name: "delivery");

            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.EnsureSchema(
                name: "pricing");

            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,")
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "business_profile",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    trade_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    fssai_license_no = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    support_phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    support_email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_business_profile", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "carts",
                schema: "cart",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    coupon_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_carts", x => x.id);
                    table.CheckConstraint("ck_carts_owner", "customer_id IS NOT NULL OR guest_token_hash IS NOT NULL");
                    table.CheckConstraint("ck_carts_status", "status IN ('ACTIVE', 'CONVERTED', 'ABANDONED')");
                });

            migrationBuilder.CreateTable(
                name: "cash_settlements",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    rider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    expected_paise = table.Column<long>(type: "bigint", nullable: false),
                    received_paise = table.Column<long>(type: "bigint", nullable: false),
                    difference_paise = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    collections_count = table.Column<int>(type: "integer", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    settled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    dispute_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_settlements", x => x.id);
                    table.CheckConstraint("ck_cash_settlements_status", "status IN ('OPEN', 'SETTLED', 'DISPUTED')");
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    image_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cities",
                schema: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cities", x => x.id);
                    table.CheckConstraint("ck_cities_status", "status IN ('DRAFT', 'ACTIVE', 'PAUSED', 'RETIRED')");
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    resolution = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    refund_amount_paise = table.Column<long>(type: "bigint", nullable: true),
                    decision_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_complaints", x => x.id);
                    table.CheckConstraint("ck_complaints_kind", "kind IN ('MISSING', 'DAMAGED', 'QUALITY', 'WRONG_ITEM', 'LATE')");
                    table.CheckConstraint("ck_complaints_status", "status IN ('OPEN', 'APPROVED', 'REJECTED', 'RESOLVED')");
                });

            migrationBuilder.CreateTable(
                name: "coupons",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "citext", nullable: false),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    discount_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    discount_value = table.Column<long>(type: "bigint", nullable: false),
                    max_discount_paise = table.Column<long>(type: "bigint", nullable: true),
                    min_order_paise = table.Column<long>(type: "bigint", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    first_order_only = table.Column<bool>(type: "boolean", nullable: false),
                    per_customer_limit = table.Column<int>(type: "integer", nullable: true),
                    total_limit = table.Column<int>(type: "integer", nullable: true),
                    used_count = table.Column<int>(type: "integer", nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_listed = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupons", x => x.id);
                    table.CheckConstraint("ck_coupons_discount_type", "discount_type IN ('PERCENT', 'FLAT', 'FREE_DELIVERY')");
                    table.CheckConstraint("ck_coupons_status", "status IN ('ACTIVE', 'PAUSED', 'EXPIRED')");
                    table.CheckConstraint("ck_coupons_used_count_not_negative", "used_count >= 0");
                    table.CheckConstraint("ck_coupons_used_count_within_total_limit", "total_limit IS NULL OR used_count <= total_limit");
                });

            migrationBuilder.CreateTable(
                name: "customers",
                schema: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    marketing_opt_in = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    addresses_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dead_letters",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    error = table.Column<string>(type: "text", nullable: false),
                    failed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dead_letters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "device_tokens",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    push_token = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    platform = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_device_tokens", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    before = table.Column<string>(type: "jsonb", nullable: true),
                    after = table.Column<string>(type: "jsonb", nullable: true),
                    ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entries", x => x.id);
                    table.CheckConstraint("ck_entries_actor_type", "actor_type IN ('ANONYMOUS', 'CUSTOMER', 'STAFF', 'SYSTEM')");
                });

            migrationBuilder.CreateTable(
                name: "idempotency_keys",
                schema: "platform",
                columns: table => new
                {
                    scope = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    request_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    response = table.Column<string>(type: "jsonb", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_idempotency_keys", x => new { x.scope, x.key });
                });

            migrationBuilder.CreateTable(
                name: "inbox",
                schema: "platform",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handler = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox", x => new { x.event_id, x.handler });
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_number = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    seller_gstin = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    seller_legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    taxable_paise = table.Column<long>(type: "bigint", nullable: false),
                    tax_paise = table.Column<long>(type: "bigint", nullable: false),
                    total_paise = table.Column<long>(type: "bigint", nullable: false),
                    pdf_storage_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "low_stock_digests",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_low_stock_digests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    template_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    dedupe_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "number_series",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    period = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    pad_width = table.Column<int>(type: "integer", nullable: false),
                    next_value = table.Column<long>(type: "bigint", nullable: false),
                    last_issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_number_series", x => x.id);
                    table.CheckConstraint("ck_number_series_code", "code IN ('ORDER', 'INVOICE')");
                    table.CheckConstraint("ck_number_series_values", "next_value >= 1 AND pad_width BETWEEN 1 AND 10");
                });

            migrationBuilder.CreateTable(
                name: "order_policies",
                schema: "settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reservation_minutes = table.Column<int>(type: "integer", nullable: false),
                    max_overweight_bp = table.Column<int>(type: "integer", nullable: false),
                    cancel_allowed_until_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    cod_max_order_paise = table.Column<long>(type: "bigint", nullable: false),
                    enabled_payment_methods = table.Column<List<string>>(type: "text[]", nullable: false),
                    substitution_response_minutes = table.Column<int>(type: "integer", nullable: false),
                    complaint_window_hours = table.Column<int>(type: "integer", nullable: false),
                    max_delivery_attempts = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_policies", x => x.id);
                    table.CheckConstraint("ck_order_policies_cancel_status", "cancel_allowed_until_status IN ('PENDING_PAYMENT', 'CONFIRMED', 'PREPARING', 'PACKED')");
                    table.CheckConstraint("ck_order_policies_ranges", "reservation_minutes > 0 AND max_overweight_bp >= 0 AND cod_max_order_paise >= 0 AND substitution_response_minutes > 0 AND complaint_window_hours > 0 AND max_delivery_attempts > 0");
                });

            migrationBuilder.CreateTable(
                name: "orders",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    payment_method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    substitution_preference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    address_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    locality = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    city_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    state_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    pincode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    delivery_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    slot_end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    customer_notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    coupon_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    items_paise = table.Column<long>(type: "bigint", nullable: false),
                    preparation_paise = table.Column<long>(type: "bigint", nullable: false),
                    discount_paise = table.Column<long>(type: "bigint", nullable: false),
                    delivery_fee_paise = table.Column<long>(type: "bigint", nullable: false),
                    delivery_fee_tax_rate_bp = table.Column<int>(type: "integer", nullable: false),
                    tax_paise = table.Column<long>(type: "bigint", nullable: false),
                    total_paise = table.Column<long>(type: "bigint", nullable: false),
                    max_total_paise = table.Column<long>(type: "bigint", nullable: false),
                    final_items_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_preparation_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_discount_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_tax_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_total_paise = table.Column<long>(type: "bigint", nullable: true),
                    amount_due_paise = table.Column<long>(type: "bigint", nullable: false),
                    refund_due_paise = table.Column<long>(type: "bigint", nullable: false),
                    refund_due_weight_shortfall_paise = table.Column<long>(type: "bigint", nullable: false),
                    refund_due_short_supply_paise = table.Column<long>(type: "bigint", nullable: false),
                    cold_box_used = table.Column<bool>(type: "boolean", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    captured_paise = table.Column<long>(type: "bigint", nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    payment_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    preparing_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    packed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancel_reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_orders", x => x.id);
                    table.CheckConstraint("ck_orders_amounts", "total_paise >= 0 AND amount_due_paise >= 0 AND refund_due_paise >= 0");
                    table.CheckConstraint("ck_orders_payment_method", "payment_method IN ('COD', 'UPI_ON_DELIVERY', 'ONLINE')");
                    table.CheckConstraint("ck_orders_status", "status IN ('PENDING_PAYMENT', 'CONFIRMED', 'PREPARING', 'PACKED', 'OUT_FOR_DELIVERY', 'DELIVERED', 'PAYMENT_FAILED', 'CANCELLED', 'DELIVERY_FAILED', 'RETURNED')");
                    table.CheckConstraint("ck_orders_substitution_preference", "substitution_preference IN ('CALL_ME', 'AUTO_SIMILAR', 'NONE')");
                });

            migrationBuilder.CreateTable(
                name: "otp_challenges",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    purpose = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    request_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_otp_challenges", x => x.id);
                    table.CheckConstraint("ck_otp_challenges_purpose", "purpose IN ('LOGIN', 'VERIFY_PHONE')");
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount_paise = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_order_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider_payment_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    authorized_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    captured_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    collected_paise = table.Column<long>(type: "bigint", nullable: true),
                    collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    collected_by_rider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    collection_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    collection_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cash_settlement_id = table.Column<Guid>(type: "uuid", nullable: true),
                    order_delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_excess = table.Column<bool>(type: "boolean", nullable: false),
                    failure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    failure_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    refunded_paise = table.Column<long>(type: "bigint", nullable: false),
                    refund_pending_paise = table.Column<long>(type: "bigint", nullable: false),
                    refund_revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount", "amount_paise >= 0");
                    table.CheckConstraint("ck_payments_collection_mode", "collection_mode IS NULL OR collection_mode IN ('CASH', 'UPI')");
                    table.CheckConstraint("ck_payments_method", "method IN ('COD', 'UPI_ON_DELIVERY', 'ONLINE')");
                    table.CheckConstraint("ck_payments_provider", "provider IN ('CASH', 'UPI_ON_DELIVERY', 'FAKE', 'RAZORPAY')");
                    table.CheckConstraint("ck_payments_refunds", "refunded_paise >= 0 AND refund_pending_paise >= 0");
                    table.CheckConstraint("ck_payments_status", "status IN ('PENDING', 'AUTHORIZED', 'CAPTURED', 'COLLECTED', 'FAILED', 'CANCELLED')");
                });

            migrationBuilder.CreateTable(
                name: "permissions",
                schema: "identity",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_permissions", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "preferences",
                schema: "notifications",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preferences", x => new { x.user_id, x.category, x.channel });
                });

            migrationBuilder.CreateTable(
                name: "preparation_charges",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preparation_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preparation_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    charge_paise = table.Column<long>(type: "bigint", nullable: false),
                    charge_basis = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preparation_charges", x => x.id);
                    table.CheckConstraint("ck_preparation_charges_basis", "charge_basis IN ('PER_KG', 'PER_ITEM', 'FLAT')");
                    table.CheckConstraint("ck_preparation_charges_charge", "charge_paise BETWEEN 0 AND 5000000");
                    table.CheckConstraint("ck_preparation_charges_valid_range", "valid_to IS NULL OR valid_to > valid_from");
                });

            migrationBuilder.CreateTable(
                name: "preparation_methods",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    guidance_yield_bp = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_preparation_methods", x => x.id);
                    table.CheckConstraint("ck_preparation_methods_guidance_yield", "guidance_yield_bp BETWEEN 1 AND 10000");
                });

            migrationBuilder.CreateTable(
                name: "promotions",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    subtitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    image_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    link_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    link_target = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: true),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_promotions", x => x.id);
                    table.CheckConstraint("ck_promotions_link_type", "link_type IN ('CATEGORY', 'PRODUCT', 'COUPON')");
                });

            migrationBuilder.CreateTable(
                name: "reconciliation_issues",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reconciliation_issues", x => x.id);
                    table.CheckConstraint("ck_reconciliation_issues_kind", "kind IN ('AMOUNT_MISMATCH', 'MISSED_WEBHOOK', 'COLLECTION_DIFFERENCE', 'REFUND_CAPPED', 'REFUND_STUCK', 'DELIVERED_NOT_COLLECTED', 'UNSETTLED_CASH', 'SETTLEMENT_AGED', 'CASH_DIFFERENCE', 'WEBHOOK_ERROR', 'REFUND_METHOD_REQUIRED')");
                    table.CheckConstraint("ck_reconciliation_issues_status", "status IN ('OPEN', 'AUTO_RESOLVED', 'RESOLVED')");
                    table.CheckConstraint("ck_reconciliation_issues_subject_type", "subject_type IN ('PAYMENT', 'REFUND', 'SETTLEMENT', 'WEBHOOK_EVENT', 'ORDER')");
                });

            migrationBuilder.CreateTable(
                name: "refresh_state",
                schema: "reporting",
                columns: table => new
                {
                    view_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    refreshed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    source_watermark = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_state", x => x.view_name);
                });

            migrationBuilder.CreateTable(
                name: "riders",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_riders", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sample_records",
                schema: "platform",
                columns: table => new
                {
                    table_name = table.Column<string>(type: "character varying(130)", maxLength: 130, nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sample_records", x => new { x.table_name, x.record_id });
                });

            migrationBuilder.CreateTable(
                name: "slot_templates",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    cutoff_minutes = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_slot_templates", x => x.id);
                    table.CheckConstraint("ck_slot_templates_capacity", "capacity BETWEEN 1 AND 500");
                    table.CheckConstraint("ck_slot_templates_cutoff", "cutoff_minutes BETWEEN 0 AND 2880");
                    table.CheckConstraint("ck_slot_templates_day_of_week", "day_of_week BETWEEN 1 AND 7");
                    table.CheckConstraint("ck_slot_templates_times", "start_time < end_time");
                });

            migrationBuilder.CreateTable(
                name: "stock_items",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    on_hand = table.Column<int>(type: "integer", nullable: false),
                    reserved = table.Column<int>(type: "integer", nullable: false),
                    low_stock_threshold = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_items", x => x.id);
                    table.CheckConstraint("ck_stock_items_on_hand", "on_hand >= 0");
                    table.CheckConstraint("ck_stock_items_reserved", "reserved >= 0");
                    table.CheckConstraint("ck_stock_items_stock_unit", "stock_unit IN ('GRAM', 'PIECE')");
                    table.CheckConstraint("ck_stock_items_threshold", "low_stock_threshold >= 0");
                });

            migrationBuilder.CreateTable(
                name: "storage_units",
                schema: "inventory",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    min_c = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    max_c = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storage_units", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "tax_categories",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    applies_to = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tax_rules",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tax_category_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    rate_bp = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tax_rules", x => x.id);
                    table.CheckConstraint("ck_tax_rules_rate", "rate_bp BETWEEN 0 AND 2800");
                    table.CheckConstraint("ck_tax_rules_valid_range", "valid_to IS NULL OR valid_to > valid_from");
                });

            migrationBuilder.CreateTable(
                name: "templates",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    variables = table.Column<string[]>(type: "text[]", nullable: false),
                    dlt_template_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    email = table.Column<string>(type: "citext", maxLength: 254, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    user_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    phone_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failed_login_count = table.Column<int>(type: "integer", nullable: false),
                    anonymised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_phone_or_email", "phone IS NOT NULL OR email IS NOT NULL OR anonymised_at IS NOT NULL");
                    table.CheckConstraint("ck_users_status", "status IN ('ACTIVE', 'LOCKED', 'DISABLED')");
                    table.CheckConstraint("ck_users_user_type", "user_type IN ('CUSTOMER', 'STAFF')");
                });

            migrationBuilder.CreateTable(
                name: "variant_prices",
                schema: "pricing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_price_paise = table.Column<long>(type: "bigint", nullable: false),
                    compare_at_paise = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    valid_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    valid_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_prices", x => x.id);
                    table.CheckConstraint("ck_variant_prices_amounts", "unit_price_paise BETWEEN 1 AND 5000000 AND (compare_at_paise IS NULL OR compare_at_paise > unit_price_paise)");
                    table.CheckConstraint("ck_variant_prices_valid_range", "valid_to IS NULL OR valid_to > valid_from");
                });

            migrationBuilder.CreateTable(
                name: "webhook_events",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    provider_event_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    headers = table.Column<string>(type: "jsonb", nullable: false),
                    signature_valid = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_events", x => x.id);
                    table.CheckConstraint("ck_webhook_events_status", "status IN ('RECEIVED', 'PROCESSED', 'IGNORED', 'ERROR', 'REJECTED')");
                });

            migrationBuilder.CreateTable(
                name: "cart_items",
                schema: "cart",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cart_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preparation_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cart_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_cart_items_carts_cart_id",
                        column: x => x.cart_id,
                        principalSchema: "cart",
                        principalTable: "carts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    local_names = table.Column<string[]>(type: "text[]", nullable: false),
                    species_scientific_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    source_region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    storage_instructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    handling_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    hsn_code = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    tax_category_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    featured_rank = table.Column<int>(type: "integer", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_hsn_code", "hsn_code IS NULL OR hsn_code ~ '^[0-9]{4,8}$'");
                    table.CheckConstraint("ck_products_source_type", "source_type IN ('WILD', 'FARMED')");
                    table.CheckConstraint("ck_products_status", "status IN ('DRAFT', 'ACTIVE', 'ARCHIVED')");
                    table.ForeignKey(
                        name: "fk_products_categories_category_id",
                        column: x => x.category_id,
                        principalSchema: "catalog",
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "fulfillment_locations",
                schema: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    address_line = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    pincode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    has_cold_storage = table.Column<bool>(type: "boolean", nullable: false),
                    fssai_license_no = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fulfillment_locations", x => x.id);
                    table.CheckConstraint("ck_fulfillment_locations_status", "status IN ('DRAFT', 'ACTIVE', 'PAUSED')");
                    table.ForeignKey(
                        name: "fk_fulfillment_locations_cities_city_id",
                        column: x => x.city_id,
                        principalSchema: "locations",
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coupon_redemptions",
                schema: "promotions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discount_paise = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    redeemed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reversed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupon_redemptions", x => x.id);
                    table.CheckConstraint("ck_coupon_redemptions_reversal_reason", "reversal_reason IS NULL OR reversal_reason IN ('ORDER_CANCELLED', 'PAYMENT_FAILED')");
                    table.CheckConstraint("ck_coupon_redemptions_status", "status IN ('APPLIED', 'REVERSED')");
                    table.ForeignKey(
                        name: "fk_coupon_redemptions_coupons_coupon_id",
                        column: x => x.coupon_id,
                        principalSchema: "promotions",
                        principalTable: "coupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coupon_scopes",
                schema: "promotions",
                columns: table => new
                {
                    scope_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: false),
                    coupon_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coupon_scopes", x => new { x.coupon_id, x.scope_type, x.scope_id });
                    table.CheckConstraint("ck_coupon_scopes_scope_type", "scope_type IN ('CATEGORY', 'PRODUCT')");
                    table.ForeignKey(
                        name: "fk_coupon_scopes_coupons_coupon_id",
                        column: x => x.coupon_id,
                        principalSchema: "promotions",
                        principalTable: "coupons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "addresses",
                schema: "customers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    recipient_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    phone = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    landmark = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    locality = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    city_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    state_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    pincode = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_addresses", x => x.id);
                    table.CheckConstraint("ck_addresses_coordinates", "latitude IS NULL OR (latitude BETWEEN -90 AND 90 AND longitude BETWEEN -180 AND 180)");
                    table.CheckConstraint("ck_addresses_default_active", "NOT is_default OR is_active");
                    table.ForeignKey(
                        name: "fk_addresses_customer_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customers",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "wishlist_items",
                schema: "customers",
                columns: table => new
                {
                    customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wishlist_items", x => new { x.customer_id, x.product_id });
                    table.ForeignKey(
                        name: "fk_wishlist_items_customers_customer_id",
                        column: x => x.customer_id,
                        principalSchema: "customers",
                        principalTable: "customers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invoice_lines",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    hsn_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    unit_price_paise = table.Column<long>(type: "bigint", nullable: false),
                    taxable_paise = table.Column<long>(type: "bigint", nullable: false),
                    tax_rate_bp = table.Column<int>(type: "integer", nullable: false),
                    tax_paise = table.Column<long>(type: "bigint", nullable: false),
                    cgst_paise = table.Column<long>(type: "bigint", nullable: false),
                    sgst_paise = table.Column<long>(type: "bigint", nullable: false),
                    total_paise = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_invoice_lines", x => x.id);
                    table.ForeignKey(
                        name: "fk_invoice_lines_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "ordering",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_adjustments",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    amount_paise = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_by_actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_adjustments", x => x.id);
                    table.CheckConstraint("ck_order_adjustments_kind", "kind IN ('WEIGHT', 'SHORT_SUPPLY', 'SUBSTITUTION', 'GOODWILL')");
                    table.ForeignKey(
                        name: "fk_order_adjustments_order_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_items",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    variant_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    image_key = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    hsn_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    preparation_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    preparation_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    preparation_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    sell_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    stock_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    unit_price_paise = table.Column<long>(type: "bigint", nullable: false),
                    preparation_charge_paise = table.Column<long>(type: "bigint", nullable: false),
                    charge_basis = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tax_rate_bp = table.Column<int>(type: "integer", nullable: false),
                    yield_bp = table.Column<int>(type: "integer", nullable: false),
                    is_weight_variable = table.Column<bool>(type: "boolean", nullable: false),
                    ordered_quantity = table.Column<int>(type: "integer", nullable: false),
                    gross_quantity = table.Column<int>(type: "integer", nullable: false),
                    max_chargeable_quantity = table.Column<int>(type: "integer", nullable: false),
                    estimated_item_paise = table.Column<long>(type: "bigint", nullable: false),
                    estimated_preparation_paise = table.Column<long>(type: "bigint", nullable: false),
                    estimated_total_paise = table.Column<long>(type: "bigint", nullable: false),
                    max_line_total_paise = table.Column<long>(type: "bigint", nullable: false),
                    discount_paise = table.Column<long>(type: "bigint", nullable: false),
                    estimated_tax_paise = table.Column<long>(type: "bigint", nullable: false),
                    packed_quantity = table.Column<int>(type: "integer", nullable: true),
                    charged_quantity = table.Column<int>(type: "integer", nullable: true),
                    final_item_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_preparation_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_discount_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_tax_paise = table.Column<long>(type: "bigint", nullable: true),
                    final_total_paise = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    substituted_from_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_items", x => x.id);
                    table.CheckConstraint("ck_order_items_status", "status IN ('ACTIVE', 'SHORT_SUPPLIED', 'SUBSTITUTED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_order_items_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "order_status_history",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    to_status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_order_status_history", x => x.id);
                    table.ForeignKey(
                        name: "fk_order_status_history_order_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "substitutions",
                schema: "ordering",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_preparation_method_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proposed_preparation_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    proposed_name = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    proposed_quantity = table.Column<int>(type: "integer", nullable: false),
                    quantity_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    stock_unit = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    price_difference_paise = table.Column<long>(type: "bigint", nullable: false),
                    proposed_max_total_paise = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    proposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    response_actor_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    new_order_item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_substitutions", x => x.id);
                    table.CheckConstraint("ck_substitutions_status", "status IN ('PROPOSED', 'ACCEPTED', 'REJECTED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "fk_substitutions_orders_order_id",
                        column: x => x.order_id,
                        principalSchema: "ordering",
                        principalTable: "orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount_paise = table.Column<long>(type: "bigint", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    method = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    provider_refund_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: true),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    complaint_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    payee_upi_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    manual_reference = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    succeeded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    succeeded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refunds", x => x.id);
                    table.CheckConstraint("ck_refunds_amount", "amount_paise > 0");
                    table.CheckConstraint("ck_refunds_method", "method IN ('ORIGINAL', 'UPI', 'BANK_TRANSFER', 'CASH')");
                    table.CheckConstraint("ck_refunds_reason_code", "reason_code IN ('WEIGHT_SHORTFALL', 'CANCELLATION', 'COMPLAINT', 'SHORT_SUPPLY', 'GOODWILL', 'RETURN')");
                    table.CheckConstraint("ck_refunds_status", "status IN ('REQUESTED', 'APPROVED', 'PROCESSING', 'SUCCEEDED', 'FAILED')");
                    table.ForeignKey(
                        name: "fk_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "payments",
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permissions",
                schema: "identity",
                columns: table => new
                {
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_code = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_permissions", x => new { x.role_id, x.permission_code });
                    table.ForeignKey(
                        name: "fk_role_permissions_permissions_permission_code",
                        column: x => x.permission_code,
                        principalSchema: "identity",
                        principalTable: "permissions",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_role_permissions_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "delivery_slots",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    cutoff_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    booked_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    closed_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_slots", x => x.id);
                    table.CheckConstraint("ck_delivery_slots_booked_count", "booked_count >= 0 AND booked_count <= capacity");
                    table.CheckConstraint("ck_delivery_slots_capacity", "capacity BETWEEN 0 AND 500");
                    table.CheckConstraint("ck_delivery_slots_closed_reason", "closed_reason IS NULL OR closed_reason IN ('MANUAL', 'CLOSURE', 'TEMPLATE_INACTIVE')");
                    table.CheckConstraint("ck_delivery_slots_status", "status IN ('OPEN', 'CLOSED')");
                    table.CheckConstraint("ck_delivery_slots_times", "start_at < end_at AND cutoff_at <= start_at");
                    table.ForeignKey(
                        name: "fk_delivery_slots_slot_template_slot_template_id",
                        column: x => x.slot_template_id,
                        principalSchema: "delivery",
                        principalTable: "slot_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "batches",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    supplier_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    source_landing_center = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    caught_or_harvested_on = table.Column<DateOnly>(type: "date", nullable: true),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    received_quantity = table.Column<int>(type: "integer", nullable: false),
                    remaining_quantity = table.Column<int>(type: "integer", nullable: false),
                    best_before_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    receipt_temperature_c = table.Column<decimal>(type: "numeric(4,1)", nullable: true),
                    total_cost_paise = table.Column<long>(type: "bigint", nullable: false),
                    remaining_cost_paise = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_batches", x => x.id);
                    table.CheckConstraint("ck_batches_received_quantity", "received_quantity > 0");
                    table.CheckConstraint("ck_batches_remaining_quantity", "remaining_quantity >= 0");
                    table.CheckConstraint("ck_batches_status", "status IN ('ACTIVE', 'DEPLETED', 'EXPIRED', 'DISCARDED')");
                    table.CheckConstraint("ck_batches_total_cost", "total_cost_paise >= 0");
                    table.ForeignKey(
                        name: "fk_batches_stock_item_stock_item_id",
                        column: x => x.stock_item_id,
                        principalSchema: "inventory",
                        principalTable: "stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_reservations",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_reservations", x => x.id);
                    table.CheckConstraint("ck_stock_reservations_quantity", "quantity > 0");
                    table.CheckConstraint("ck_stock_reservations_status", "status IN ('ACTIVE', 'CONSUMED', 'RELEASED', 'EXPIRED')");
                    table.ForeignKey(
                        name: "fk_stock_reservations_stock_items_stock_item_id",
                        column: x => x.stock_item_id,
                        principalSchema: "inventory",
                        principalTable: "stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "cold_chain_logs",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    temperature_c = table.Column<decimal>(type: "numeric(4,1)", nullable: false),
                    is_out_of_range = table.Column<bool>(type: "boolean", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cold_chain_logs", x => x.id);
                    table.ForeignKey(
                        name: "fk_cold_chain_logs_storage_units_storage_unit",
                        column: x => x.storage_unit,
                        principalSchema: "inventory",
                        principalTable: "storage_units",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoke_reason = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    replaced_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.CheckConstraint("ck_refresh_tokens_revoke_reason", "revoke_reason IS NULL OR revoke_reason IN ('ROTATED', 'LOGOUT', 'FAMILY_REUSE', 'SESSIONS_REVOKED')");
                    table.ForeignKey(
                        name: "fk_refresh_tokens_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_members",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    all_locations = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_staff_members_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_images",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    alt_text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_images_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sell_unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    stock_unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    pack_size_grams = table.Column<int>(type: "integer", nullable: true),
                    avg_piece_grams = table.Column<int>(type: "integer", nullable: true),
                    min_order = table.Column<int>(type: "integer", nullable: false),
                    max_order = table.Column<int>(type: "integer", nullable: false),
                    order_step = table.Column<int>(type: "integer", nullable: false),
                    is_weight_variable = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_variants", x => x.id);
                    table.CheckConstraint("ck_product_variants_quantity_rules", "order_step > 0 AND min_order >= order_step AND min_order % order_step = 0 AND max_order >= min_order AND max_order % order_step = 0");
                    table.CheckConstraint("ck_product_variants_sell_unit", "sell_unit IN ('KG', 'PIECE', 'PACK')");
                    table.CheckConstraint("ck_product_variants_stock_unit", "stock_unit IN ('GRAM', 'PIECE')");
                    table.ForeignKey(
                        name: "fk_product_variants_products_product_id",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "location_closures",
                schema: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location_closures", x => x.id);
                    table.ForeignKey(
                        name: "fk_location_closures_fulfillment_locations_fulfillment_locatio",
                        column: x => x.fulfillment_location_id,
                        principalSchema: "locations",
                        principalTable: "fulfillment_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "operating_hours",
                schema: "locations",
                columns: table => new
                {
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    opens_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    closes_at = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operating_hours", x => new { x.fulfillment_location_id, x.day_of_week });
                    table.CheckConstraint("ck_operating_hours_window", "opens_at < closes_at");
                    table.ForeignKey(
                        name: "fk_operating_hours_fulfillment_location_fulfillment_location_id",
                        column: x => x.fulfillment_location_id,
                        principalSchema: "locations",
                        principalTable: "fulfillment_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_areas",
                schema: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    city_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    delivery_fee_paise = table.Column<long>(type: "bigint", nullable: false),
                    free_delivery_above_paise = table.Column<long>(type: "bigint", nullable: true),
                    min_order_paise = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_areas", x => x.id);
                    table.CheckConstraint("ck_service_areas_fees", "delivery_fee_paise >= 0 AND min_order_paise >= 0 AND (free_delivery_above_paise IS NULL OR free_delivery_above_paise >= min_order_paise)");
                    table.ForeignKey(
                        name: "fk_service_areas_cities_city_id",
                        column: x => x.city_id,
                        principalSchema: "locations",
                        principalTable: "cities",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_areas_fulfillment_locations_fulfillment_location_id",
                        column: x => x.fulfillment_location_id,
                        principalSchema: "locations",
                        principalTable: "fulfillment_locations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shipments",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    order_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delivery_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    placed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    packed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cold_box_used = table.Column<bool>(type: "boolean", nullable: false),
                    dispatch_temperature_c = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    temperature_warning = table.Column<bool>(type: "boolean", nullable: false),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivery_code = table.Column<string>(type: "character(4)", nullable: true),
                    delivery_code_failures = table.Column<int>(type: "integer", nullable: false),
                    cash_collected_paise = table.Column<long>(type: "bigint", nullable: true),
                    cash_mode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    upi_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cash_collected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shipments", x => x.id);
                    table.CheckConstraint("ck_shipments_cash", "cash_collected_paise IS NULL OR cash_collected_paise >= 0");
                    table.CheckConstraint("ck_shipments_cash_mode", "cash_mode IS NULL OR cash_mode IN ('CASH', 'UPI')");
                    table.CheckConstraint("ck_shipments_code_failures", "delivery_code_failures >= 0");
                    table.CheckConstraint("ck_shipments_status", "status IN ('PENDING', 'ASSIGNED', 'OUT_FOR_DELIVERY', 'DELIVERED', 'FAILED', 'RETURNED', 'CANCELLED')");
                    table.ForeignKey(
                        name: "fk_shipments_delivery_slots_delivery_slot_id",
                        column: x => x.delivery_slot_id,
                        principalSchema: "delivery",
                        principalTable: "delivery_slots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shipments_riders_rider_id",
                        column: x => x.rider_id,
                        principalSchema: "delivery",
                        principalTable: "riders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "stock_movements",
                schema: "inventory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    cost_paise = table.Column<long>(type: "bigint", nullable: false),
                    reason_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    reference_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stock_movements", x => x.id);
                    table.CheckConstraint("ck_stock_movements_kind", "kind IN ('RECEIPT', 'SALE', 'WASTE', 'ADJUSTMENT_IN', 'ADJUSTMENT_OUT', 'RETURN', 'COUNT_CORRECTION')");
                    table.CheckConstraint("ck_stock_movements_quantity", "quantity <> 0");
                    table.CheckConstraint("ck_stock_movements_reference_type", "reference_type IN ('ORDER_ITEM', 'BATCH', 'MANUAL')");
                    table.ForeignKey(
                        name: "fk_stock_movements_batches_batch_id",
                        column: x => x.batch_id,
                        principalSchema: "inventory",
                        principalTable: "batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_stock_movements_stock_items_stock_item_id",
                        column: x => x.stock_item_id,
                        principalSchema: "inventory",
                        principalTable: "stock_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "staff_location_access",
                schema: "identity",
                columns: table => new
                {
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fulfillment_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_location_access", x => new { x.staff_member_id, x.fulfillment_location_id });
                    table.ForeignKey(
                        name: "fk_staff_location_access_staff_member_staff_member_id",
                        column: x => x.staff_member_id,
                        principalSchema: "identity",
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_roles",
                schema: "identity",
                columns: table => new
                {
                    staff_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_staff_roles", x => new { x.staff_member_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_staff_roles_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identity",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_staff_roles_staff_members_staff_member_id",
                        column: x => x.staff_member_id,
                        principalSchema: "identity",
                        principalTable: "staff_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variant_preparations",
                schema: "catalog",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preparation_method_id = table.Column<Guid>(type: "uuid", nullable: false),
                    yield_bp = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_preparations", x => new { x.variant_id, x.preparation_method_id });
                    table.CheckConstraint("ck_variant_preparations_yield", "yield_bp BETWEEN 1 AND 10000");
                    table.ForeignKey(
                        name: "fk_variant_preparations_preparation_methods_preparation_method",
                        column: x => x.preparation_method_id,
                        principalSchema: "catalog",
                        principalTable: "preparation_methods",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_variant_preparations_product_variants_variant_id",
                        column: x => x.variant_id,
                        principalSchema: "catalog",
                        principalTable: "product_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pincodes",
                schema: "locations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_area_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pincode = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pincodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_pincodes_service_area_service_area_id",
                        column: x => x.service_area_id,
                        principalSchema: "locations",
                        principalTable: "service_areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "delivery_attempts",
                schema: "delivery",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shipment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_no = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    modified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    modified_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delivery_attempts", x => x.id);
                    table.CheckConstraint("ck_delivery_attempts_outcome", "outcome IN ('DELIVERED', 'CUSTOMER_UNAVAILABLE', 'ADDRESS_NOT_FOUND', 'REFUSED', 'OTHER')");
                    table.ForeignKey(
                        name: "fk_delivery_attempts_shipment_shipment_id",
                        column: x => x.shipment_id,
                        principalSchema: "delivery",
                        principalTable: "shipments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_addresses_customer_id_is_active",
                schema: "customers",
                table: "addresses",
                columns: new[] { "customer_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "ux_addresses_one_default",
                schema: "customers",
                table: "addresses",
                column: "customer_id",
                unique: true,
                filter: "is_default AND is_active");

            migrationBuilder.CreateIndex(
                name: "ix_batches_best_before_active",
                schema: "inventory",
                table: "batches",
                column: "best_before_at",
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_batches_stock_item_id",
                schema: "inventory",
                table: "batches",
                column: "stock_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_batches_variant_best_before_active",
                schema: "inventory",
                table: "batches",
                columns: new[] { "variant_id", "best_before_at" },
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ux_batches_location_code",
                schema: "inventory",
                table: "batches",
                columns: new[] { "fulfillment_location_id", "batch_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cart_items_cart_id_variant_id_preparation_method_id",
                schema: "cart",
                table: "cart_items",
                columns: new[] { "cart_id", "variant_id", "preparation_method_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_carts_customer_id",
                schema: "cart",
                table: "carts",
                column: "customer_id",
                unique: true,
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_carts_expires_at",
                schema: "cart",
                table: "carts",
                column: "expires_at",
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_carts_guest_token_hash",
                schema: "cart",
                table: "carts",
                column: "guest_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cash_settlements_fulfillment_location_id_settlement_date",
                schema: "payments",
                table: "cash_settlements",
                columns: new[] { "fulfillment_location_id", "settlement_date" });

            migrationBuilder.CreateIndex(
                name: "ix_cash_settlements_rider_id_settlement_date",
                schema: "payments",
                table: "cash_settlements",
                columns: new[] { "rider_id", "settlement_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id",
                schema: "catalog",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ux_categories_slug",
                schema: "catalog",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_cities_code",
                schema: "locations",
                table: "cities",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cold_chain_logs_fulfillment_location_id_recorded_at",
                schema: "inventory",
                table: "cold_chain_logs",
                columns: new[] { "fulfillment_location_id", "recorded_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_cold_chain_logs_storage_unit",
                schema: "inventory",
                table: "cold_chain_logs",
                column: "storage_unit");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_order_id",
                schema: "ordering",
                table: "complaints",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_status_raised_at",
                schema: "ordering",
                table: "complaints",
                columns: new[] { "status", "raised_at" });

            migrationBuilder.CreateIndex(
                name: "ux_complaints_open_per_line",
                schema: "ordering",
                table: "complaints",
                columns: new[] { "order_id", "order_item_id" },
                unique: true,
                filter: "status IN ('OPEN', 'APPROVED')")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemptions_coupon_id_customer_id",
                schema: "promotions",
                table: "coupon_redemptions",
                columns: new[] { "coupon_id", "customer_id" });

            migrationBuilder.CreateIndex(
                name: "ix_coupon_redemptions_order_id",
                schema: "promotions",
                table: "coupon_redemptions",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coupons_code",
                schema: "promotions",
                table: "coupons",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_coupons_valid_to",
                schema: "promotions",
                table: "coupons",
                column: "valid_to",
                filter: "status IN ('ACTIVE', 'PAUSED')");

            migrationBuilder.CreateIndex(
                name: "ix_customers_user_id",
                schema: "customers",
                table: "customers",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dead_letters_event_id",
                schema: "platform",
                table: "dead_letters",
                column: "event_id");

            migrationBuilder.CreateIndex(
                name: "ix_delivery_attempts_shipment_id_attempt_no",
                schema: "delivery",
                table: "delivery_attempts",
                columns: new[] { "shipment_id", "attempt_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_slots_fulfillment_location_id_slot_date",
                schema: "delivery",
                table: "delivery_slots",
                columns: new[] { "fulfillment_location_id", "slot_date" });

            migrationBuilder.CreateIndex(
                name: "ix_delivery_slots_fulfillment_location_id_start_at",
                schema: "delivery",
                table: "delivery_slots",
                columns: new[] { "fulfillment_location_id", "start_at" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_delivery_slots_slot_template_id",
                schema: "delivery",
                table: "delivery_slots",
                column: "slot_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_device_tokens_push_token",
                schema: "notifications",
                table: "device_tokens",
                column: "push_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_device_tokens_user_id",
                schema: "notifications",
                table: "device_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_entries_actor_id",
                schema: "audit",
                table: "entries",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_entries_entity_type_entity_id",
                schema: "audit",
                table: "entries",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_entries_occurred_at",
                schema: "audit",
                table: "entries",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_fulfillment_locations_city_id",
                schema: "locations",
                table: "fulfillment_locations",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ux_fulfillment_locations_code",
                schema: "locations",
                table: "fulfillment_locations",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_idempotency_keys_expires_at",
                schema: "platform",
                table: "idempotency_keys",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_invoice_lines_invoice_id_line_no",
                schema: "ordering",
                table: "invoice_lines",
                columns: new[] { "invoice_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_invoice_number",
                schema: "ordering",
                table: "invoices",
                column: "invoice_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_invoices_order_id",
                schema: "ordering",
                table: "invoices",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_location_closures_location_date",
                schema: "locations",
                table: "location_closures",
                columns: new[] { "fulfillment_location_id", "closed_on" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_low_stock_digests_location_date",
                schema: "inventory",
                table: "low_stock_digests",
                columns: new[] { "fulfillment_location_id", "local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_dedupe_key",
                schema: "notifications",
                table: "messages",
                column: "dedupe_key",
                unique: true,
                filter: "dedupe_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_messages_queued",
                schema: "notifications",
                table: "messages",
                column: "queued_at",
                filter: "status = 'QUEUED'");

            migrationBuilder.CreateIndex(
                name: "ix_messages_user_id_queued_at",
                schema: "notifications",
                table: "messages",
                columns: new[] { "user_id", "queued_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_number_series_code_period",
                schema: "settings",
                table: "number_series",
                columns: new[] { "code", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_adjustments_order_id",
                schema: "ordering",
                table: "order_adjustments",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_items_order_id_line_no",
                schema: "ordering",
                table: "order_items",
                columns: new[] { "order_id", "line_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_order_items_variant_id",
                schema: "ordering",
                table: "order_items",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_order_policies_city_id",
                schema: "settings",
                table: "order_policies",
                column: "city_id",
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_order_status_history_order_id_at",
                schema: "ordering",
                table: "order_status_history",
                columns: new[] { "order_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_city_id",
                schema: "ordering",
                table: "orders",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id_idempotency_key",
                schema: "ordering",
                table: "orders",
                columns: new[] { "customer_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_customer_id_placed_at",
                schema: "ordering",
                table: "orders",
                columns: new[] { "customer_id", "placed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_orders_delivery_slot_id",
                schema: "ordering",
                table: "orders",
                column: "delivery_slot_id");

            migrationBuilder.CreateIndex(
                name: "ix_orders_fulfillment_location_id_status_slot_start_at",
                schema: "ordering",
                table: "orders",
                columns: new[] { "fulfillment_location_id", "status", "slot_start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_orders_order_number",
                schema: "ordering",
                table: "orders",
                column: "order_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_payment_due_at",
                schema: "ordering",
                table: "orders",
                column: "payment_due_at",
                filter: "status = 'PENDING_PAYMENT'");

            migrationBuilder.CreateIndex(
                name: "ix_otp_challenges_phone_requested_at",
                schema: "identity",
                table: "otp_challenges",
                columns: new[] { "phone", "requested_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_otp_challenges_request_ip_requested_at",
                schema: "identity",
                table: "otp_challenges",
                columns: new[] { "request_ip", "requested_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_occurred_at",
                schema: "platform",
                table: "outbox",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_collected_by_rider_id_collected_at",
                schema: "payments",
                table: "payments",
                columns: new[] { "collected_by_rider_id", "collected_at" },
                filter: "status = 'COLLECTED' AND cash_settlement_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_fulfillment_location_id_status",
                schema: "payments",
                table: "payments",
                columns: new[] { "fulfillment_location_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_payments_order_id",
                schema: "payments",
                table: "payments",
                column: "order_id",
                unique: true,
                filter: "provider IN ('CASH', 'UPI_ON_DELIVERY')");

            migrationBuilder.CreateIndex(
                name: "ix_payments_order_id1",
                schema: "payments",
                table: "payments",
                column: "order_id",
                unique: true,
                filter: "status IN ('CAPTURED', 'COLLECTED') AND NOT is_excess");

            migrationBuilder.CreateIndex(
                name: "ix_payments_provider_payment_id",
                schema: "payments",
                table: "payments",
                column: "provider_payment_id",
                unique: true,
                filter: "provider_payment_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_provider_provider_order_id",
                schema: "payments",
                table: "payments",
                columns: new[] { "provider", "provider_order_id" },
                unique: true,
                filter: "provider_order_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_status_method",
                schema: "payments",
                table: "payments",
                columns: new[] { "status", "method" },
                filter: "status IN ('PENDING', 'AUTHORIZED')");

            migrationBuilder.CreateIndex(
                name: "ix_pincodes_service_area_id",
                schema: "locations",
                table: "pincodes",
                column: "service_area_id");

            migrationBuilder.CreateIndex(
                name: "ux_pincodes_active_pincode",
                schema: "locations",
                table: "pincodes",
                column: "pincode",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_preparation_charges_fulfillment_location_id_variant_id_prep",
                schema: "pricing",
                table: "preparation_charges",
                columns: new[] { "fulfillment_location_id", "variant_id", "preparation_method_id", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ux_preparation_methods_code",
                schema: "catalog",
                table: "preparation_methods",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_images_product_id_sort_order",
                schema: "catalog",
                table: "product_images",
                columns: new[] { "product_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_product_id",
                schema: "catalog",
                table: "product_variants",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_sku",
                schema: "catalog",
                table: "product_variants",
                column: "sku",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_category_id",
                schema: "catalog",
                table: "products",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ux_products_slug",
                schema: "catalog",
                table: "products",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_promotions_city_id_starts_at",
                schema: "promotions",
                table: "promotions",
                columns: new[] { "city_id", "starts_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_issues_kind_subject_id",
                schema: "payments",
                table: "reconciliation_issues",
                columns: new[] { "kind", "subject_id" },
                unique: true,
                filter: "status = 'OPEN'");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_issues_status_detected_at",
                schema: "payments",
                table: "reconciliation_issues",
                columns: new[] { "status", "detected_at" });

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                schema: "identity",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_user_id",
                schema: "identity",
                table: "refresh_tokens",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_fulfillment_location_id_status",
                schema: "payments",
                table: "refunds",
                columns: new[] { "fulfillment_location_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_refunds_idempotency_key",
                schema: "payments",
                table: "refunds",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refunds_order_id",
                schema: "payments",
                table: "refunds",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_payment_id",
                schema: "payments",
                table: "refunds",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_provider_refund_id",
                schema: "payments",
                table: "refunds",
                column: "provider_refund_id",
                unique: true,
                filter: "provider_refund_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_refunds_source_reference",
                schema: "payments",
                table: "refunds",
                column: "source_reference",
                unique: true,
                filter: "source_reference IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_riders_fulfillment_location_id",
                schema: "delivery",
                table: "riders",
                column: "fulfillment_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_riders_phone",
                schema: "delivery",
                table: "riders",
                column: "phone",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_role_permissions_permission_code",
                schema: "identity",
                table: "role_permissions",
                column: "permission_code");

            migrationBuilder.CreateIndex(
                name: "ix_roles_code",
                schema: "identity",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_service_areas_city_id",
                schema: "locations",
                table: "service_areas",
                column: "city_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_areas_fulfillment_location_id",
                schema: "locations",
                table: "service_areas",
                column: "fulfillment_location_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_delivery_slot_id",
                schema: "delivery",
                table: "shipments",
                column: "delivery_slot_id");

            migrationBuilder.CreateIndex(
                name: "ix_shipments_fulfillment_location_id_status",
                schema: "delivery",
                table: "shipments",
                columns: new[] { "fulfillment_location_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_shipments_order_id",
                schema: "delivery",
                table: "shipments",
                column: "order_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_shipments_rider_id_status",
                schema: "delivery",
                table: "shipments",
                columns: new[] { "rider_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_slot_templates_fulfillment_location_id_day_of_week",
                schema: "delivery",
                table: "slot_templates",
                columns: new[] { "fulfillment_location_id", "day_of_week" });

            migrationBuilder.CreateIndex(
                name: "ix_staff_members_user_id",
                schema: "identity",
                table: "staff_members",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_staff_roles_role_id",
                schema: "identity",
                table: "staff_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ux_stock_items_location_variant",
                schema: "inventory",
                table: "stock_items",
                columns: new[] { "fulfillment_location_id", "variant_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_batch_id",
                schema: "inventory",
                table: "stock_movements",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_sale_reference",
                schema: "inventory",
                table: "stock_movements",
                column: "reference_id",
                filter: "kind = 'SALE'");

            migrationBuilder.CreateIndex(
                name: "ix_stock_movements_stock_item_id_occurred_at",
                schema: "inventory",
                table: "stock_movements",
                columns: new[] { "stock_item_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_expires_active",
                schema: "inventory",
                table: "stock_reservations",
                column: "expires_at",
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_order_id",
                schema: "inventory",
                table: "stock_reservations",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_stock_reservations_stock_item_id",
                schema: "inventory",
                table: "stock_reservations",
                column: "stock_item_id");

            migrationBuilder.CreateIndex(
                name: "ux_stock_reservations_active_order_item",
                schema: "inventory",
                table: "stock_reservations",
                column: "order_item_id",
                unique: true,
                filter: "status = 'ACTIVE'");

            migrationBuilder.CreateIndex(
                name: "ix_substitutions_expires_at",
                schema: "ordering",
                table: "substitutions",
                column: "expires_at",
                filter: "status = 'PROPOSED'");

            migrationBuilder.CreateIndex(
                name: "ix_substitutions_order_id",
                schema: "ordering",
                table: "substitutions",
                column: "order_id");

            migrationBuilder.CreateIndex(
                name: "ix_substitutions_order_item_id",
                schema: "ordering",
                table: "substitutions",
                column: "order_item_id",
                unique: true,
                filter: "status = 'PROPOSED'");

            migrationBuilder.CreateIndex(
                name: "ux_tax_categories_code",
                schema: "pricing",
                table: "tax_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tax_rules_tax_category_code_valid_from",
                schema: "pricing",
                table: "tax_rules",
                columns: new[] { "tax_category_code", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ix_templates_code_channel",
                schema: "notifications",
                table: "templates",
                columns: new[] { "code", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                schema: "identity",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_phone",
                schema: "identity",
                table: "users",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_variant_preparations_preparation_method_id",
                schema: "catalog",
                table: "variant_preparations",
                column: "preparation_method_id");

            migrationBuilder.CreateIndex(
                name: "ix_variant_prices_fulfillment_location_id_variant_id_valid_from",
                schema: "pricing",
                table: "variant_prices",
                columns: new[] { "fulfillment_location_id", "variant_id", "valid_from" });

            migrationBuilder.CreateIndex(
                name: "ix_variant_prices_variant_id",
                schema: "pricing",
                table: "variant_prices",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_provider_provider_event_id",
                schema: "payments",
                table: "webhook_events",
                columns: new[] { "provider", "provider_event_id" },
                unique: true,
                filter: "signature_valid");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_received_at",
                schema: "payments",
                table: "webhook_events",
                column: "received_at");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_events_status",
                schema: "payments",
                table: "webhook_events",
                column: "status",
                filter: "status = 'ERROR'");

            // Cross-schema foreign keys (docs/team/integration.md): SQL, because EF does not model cross-module references.
            foreach (var sql in CrossSchemaForeignKeys.AddSql)
            {
                migrationBuilder.Sql(sql);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cross-schema foreign keys first, so the tables they join can be dropped in any order.
            foreach (var sql in CrossSchemaForeignKeys.DropSql)
            {
                migrationBuilder.Sql(sql);
            }

            migrationBuilder.DropTable(
                name: "addresses",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "business_profile",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "cart_items",
                schema: "cart");

            migrationBuilder.DropTable(
                name: "cash_settlements",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "cold_chain_logs",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "complaints",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "coupon_redemptions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "coupon_scopes",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "dead_letters",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "delivery_attempts",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "device_tokens",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "entries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "idempotency_keys",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "inbox",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "invoice_lines",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "location_closures",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "low_stock_digests",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "number_series",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "operating_hours",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "order_adjustments",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_items",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "order_policies",
                schema: "settings");

            migrationBuilder.DropTable(
                name: "order_status_history",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "otp_challenges",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "outbox",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "pincodes",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "preparation_charges",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "product_images",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "promotions",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "reconciliation_issues",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "refresh_state",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "refunds",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "role_permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "sample_records",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "staff_location_access",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "staff_roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "stock_movements",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "stock_reservations",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "substitutions",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "tax_categories",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "tax_rules",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "templates",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "variant_preparations",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "variant_prices",
                schema: "pricing");

            migrationBuilder.DropTable(
                name: "webhook_events",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "wishlist_items",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "carts",
                schema: "cart");

            migrationBuilder.DropTable(
                name: "storage_units",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "coupons",
                schema: "promotions");

            migrationBuilder.DropTable(
                name: "shipments",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "service_areas",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "payments");

            migrationBuilder.DropTable(
                name: "permissions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "staff_members",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "batches",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "orders",
                schema: "ordering");

            migrationBuilder.DropTable(
                name: "preparation_methods",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_variants",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "customers",
                schema: "customers");

            migrationBuilder.DropTable(
                name: "delivery_slots",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "riders",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "fulfillment_locations",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "users",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "stock_items",
                schema: "inventory");

            migrationBuilder.DropTable(
                name: "products",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "slot_templates",
                schema: "delivery");

            migrationBuilder.DropTable(
                name: "cities",
                schema: "locations");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "catalog");
        }
    }
}
