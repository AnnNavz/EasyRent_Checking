IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526114410_init'
)
BEGIN
    CREATE TABLE [Driver] (
        [DriverId] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Address] nvarchar(255) NOT NULL,
        [ContactNo] nvarchar(11) NOT NULL,
        [LicenseNo] nvarchar(30) NOT NULL,
        [ExpiryDate] date NOT NULL,
        [ImagePath] nvarchar(255) NULL,
        CONSTRAINT [PK_Driver] PRIMARY KEY ([DriverId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260526114410_init'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260526114410_init', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527063247_vehicle'
)
BEGIN
    CREATE TABLE [Vehicle] (
        [VehicleId] int NOT NULL IDENTITY,
        [Model] nvarchar(100) NOT NULL,
        [PlateNumber] nvarchar(20) NOT NULL,
        [Brand] nvarchar(50) NOT NULL,
        [Color] nvarchar(30) NOT NULL,
        [Type] int NOT NULL,
        [Status] int NOT NULL,
        [ImagePath] nvarchar(255) NULL,
        CONSTRAINT [PK_Vehicle] PRIMARY KEY ([VehicleId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527063247_vehicle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527063247_vehicle', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527071113_vehicleUpdate'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [RegistrationDate] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527071113_vehicleUpdate'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [RegistrationExpiry] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260527071113_vehicleUpdate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260527071113_vehicleUpdate', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722162800_update_vehicles'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [BasePrice] decimal(18,2) NOT NULL DEFAULT 800.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722162800_update_vehicles'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [PassengersCount] int NOT NULL DEFAULT 5;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722162800_update_vehicles'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [Description] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722162800_update_vehicles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722162800_update_vehicles', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723101500_add_reservation'
)
BEGIN
    CREATE TABLE [Reservation] (
        [ReservationID] int NOT NULL IDENTITY,
        [VehicleId] int NOT NULL,
        [CustomerName] nvarchar(100) NOT NULL,
        [ContactInfo] nvarchar(100) NOT NULL,
        [PickupLoc] nvarchar(255) NOT NULL,
        [DropoffLoc] nvarchar(255) NOT NULL,
        [PickupDate] date NOT NULL,
        [ReturnDate] date NOT NULL,
        [PickupTime] time NOT NULL,
        [ReturnTime] time NOT NULL,
        [PassengerCount] int NOT NULL,
        [SpNotes] nvarchar(1000) NULL,
        [Discount] int NOT NULL,
        [ImagePath] nvarchar(255) NULL,
        [PaymentMethod] nvarchar(20) NULL,
        CONSTRAINT [PK_Reservation] PRIMARY KEY ([ReservationID]),
        CONSTRAINT [FK_Reservation_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723101500_add_reservation'
)
BEGIN
    CREATE INDEX [IX_Reservation_VehicleId] ON [Reservation] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260723101500_add_reservation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260723101500_add_reservation', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724071910_remove_reservation_payment_method'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'PaymentMethod');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [PaymentMethod];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724071910_remove_reservation_payment_method'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'ContactInfo');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Reservation] ALTER COLUMN [ContactInfo] nvarchar(11) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724071910_remove_reservation_payment_method'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724071910_remove_reservation_payment_method', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724081013_add_reservation_status'
)
BEGIN
    ALTER TABLE [Reservation] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724081013_add_reservation_status'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724081013_add_reservation_status', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    ALTER TABLE [Reservation] ADD [AmountSent] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    ALTER TABLE [Reservation] ADD [PayerAccountName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    ALTER TABLE [Reservation] ADD [PaymentChannel] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    ALTER TABLE [Reservation] ADD [PaymentDateTime] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    ALTER TABLE [Reservation] ADD [PaymentNotes] nvarchar(1000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    ALTER TABLE [Reservation] ADD [PaymentProofPath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    ALTER TABLE [Reservation] ADD [PaymentReference] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724082047_add_reservation_payment_confirmation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724082047_add_reservation_payment_confirmation', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724084255_add_reservation_soft_lock'
)
BEGIN
    ALTER TABLE [Reservation] ADD [CreatedAtUtc] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724084255_add_reservation_soft_lock'
)
BEGIN
    ALTER TABLE [Reservation] ADD [LockedUntilUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260724084255_add_reservation_soft_lock'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260724084255_add_reservation_soft_lock', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726130515_add_driver_license_images'
)
BEGIN
    ALTER TABLE [Driver] ADD [FrontLicenseImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726130515_add_driver_license_images'
)
BEGIN
    ALTER TABLE [Driver] ADD [BackLicenseImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726130515_add_driver_license_images'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726130515_add_driver_license_images', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726142843_add_reservation_is_draft'
)
BEGIN
    ALTER TABLE [Reservation] ADD [IsDraft] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260726142843_add_reservation_is_draft'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260726142843_add_reservation_is_draft', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801045852_remove_reservation'
)
BEGIN
    DROP TABLE [Reservation];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801045852_remove_reservation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801045852_remove_reservation', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801050735_add_vehicle_succeeding_fee'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [SucceedingFee] decimal(18,2) NOT NULL DEFAULT 400.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801050735_add_vehicle_succeeding_fee'
)
BEGIN
    UPDATE Vehicle SET SucceedingFee = ROUND(BasePrice / 2.0, 2) WHERE SucceedingFee = 400
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801050735_add_vehicle_succeeding_fee'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801050735_add_vehicle_succeeding_fee', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801053045_readd_reservation'
)
BEGIN
    CREATE TABLE [Reservation] (
        [ReservationId] int NOT NULL IDENTITY,
        [CustomerName] nvarchar(100) NOT NULL,
        [ContactNumber] nvarchar(max) NOT NULL,
        [PickupLocation] nvarchar(200) NOT NULL,
        [DropoffLocation] nvarchar(200) NOT NULL,
        [PickupDate] date NOT NULL,
        [ReturnDate] date NOT NULL,
        [PickupTime] time NOT NULL,
        [ReturnTime] time NOT NULL,
        [PassengerCount] int NOT NULL,
        [Notes] nvarchar(500) NULL,
        [Discount] bit NOT NULL,
        [DiscountImagePath] nvarchar(255) NULL,
        CONSTRAINT [PK_Reservation] PRIMARY KEY ([ReservationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801053045_readd_reservation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801053045_readd_reservation', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801054800_update_reservation_status'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'Discount');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [Reservation] ALTER COLUMN [Discount] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801054800_update_reservation_status'
)
BEGIN
    ALTER TABLE [Reservation] ADD [ReservationStatus] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801054800_update_reservation_status'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801054800_update_reservation_status', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801055725_add_reservation_vehicle'
)
BEGIN
    ALTER TABLE [Reservation] ADD [VehicleId] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801055725_add_reservation_vehicle'
)
BEGIN
    CREATE INDEX [IX_Reservation_VehicleId] ON [Reservation] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801055725_add_reservation_vehicle'
)
BEGIN
    ALTER TABLE [Reservation] ADD CONSTRAINT [FK_Reservation_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801055725_add_reservation_vehicle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801055725_add_reservation_vehicle', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802131220_add_customer'
)
BEGIN
    CREATE TABLE [Customer] (
        [CustomerId] int NOT NULL IDENTITY,
        [FullName] nvarchar(100) NOT NULL,
        [ContactNumber] nvarchar(max) NOT NULL,
        [Email] nvarchar(150) NOT NULL,
        [Password] nvarchar(100) NOT NULL,
        [ValidIDtype] nvarchar(max) NOT NULL,
        [ValidIDImagePath] nvarchar(255) NULL,
        CONSTRAINT [PK_Customer] PRIMARY KEY ([CustomerId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802131220_add_customer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802131220_add_customer', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802135832_add_customer_status'
)
BEGIN
    ALTER TABLE [Customer] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260802135832_add_customer_status'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260802135832_add_customer_status', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805060413_add_payment_account_date_submitted'
)
BEGIN
    CREATE TABLE [Payment] (
        [PaymentId] int NOT NULL IDENTITY,
        [ReservationId] int NOT NULL,
        [PaymentChannel] nvarchar(20) NOT NULL,
        [PaymentMethod] nvarchar(30) NOT NULL,
        [PaymentType] nvarchar(30) NOT NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [AmountPaid] decimal(18,2) NOT NULL,
        [AccountName] nvarchar(100) NOT NULL,
        [TransactionReference] nvarchar(100) NULL,
        [PaymentDate] datetime2 NOT NULL,
        [ReceiptImagePath] nvarchar(255) NULL,
        [PaymentStatus] nvarchar(30) NOT NULL,
        [PaymentSubmitted] datetime2 NOT NULL,
        [PaymentNotes] nvarchar(1000) NULL,
        CONSTRAINT [PK_Payment] PRIMARY KEY ([PaymentId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805060413_add_payment_account_date_submitted'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260805060413_add_payment_account_date_submitted', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    CREATE TABLE [ReservationDetails] (
        [ReservationDetailsID] int NOT NULL IDENTITY,
        [ReservationID] int NOT NULL,
        [VehicleId] int NOT NULL,
        [PickupLocation] nvarchar(200) NOT NULL,
        [DropoffLocation] nvarchar(200) NOT NULL,
        [PickupDate] date NOT NULL,
        [ReturnDate] date NOT NULL,
        [PickupTime] time NOT NULL,
        [ReturnTime] time NOT NULL,
        [PassengerCount] int NOT NULL,
        [Discount] int NOT NULL,
        [DiscountImagePath] nvarchar(255) NULL,
        CONSTRAINT [PK_ReservationDetails] PRIMARY KEY ([ReservationDetailsID]),
        CONSTRAINT [FK_ReservationDetails_Reservation_ReservationID] FOREIGN KEY ([ReservationID]) REFERENCES [Reservation] ([ReservationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ReservationDetails_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN

    INSERT INTO [ReservationDetails]
    (
        [ReservationID],
        [VehicleId],
        [PickupLocation],
        [DropoffLocation],
        [PickupDate],
        [ReturnDate],
        [PickupTime],
        [ReturnTime],
        [PassengerCount],
        [Discount],
        [DiscountImagePath]
    )
    SELECT
        [ReservationId],
        [VehicleId],
        [PickupLocation],
        [DropoffLocation],
        [PickupDate],
        [ReturnDate],
        [PickupTime],
        [ReturnTime],
        [PassengerCount],
        [Discount],
        [DiscountImagePath]
    FROM [Reservation];

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    ALTER TABLE [Reservation] DROP CONSTRAINT [FK_Reservation_Vehicle_VehicleId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DROP INDEX [IX_Reservation_VehicleId] ON [Reservation];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'Discount');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [Discount];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'DiscountImagePath');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [DiscountImagePath];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'DropoffLocation');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [DropoffLocation];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'PassengerCount');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [PassengerCount];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'PickupDate');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [PickupDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'PickupLocation');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [PickupLocation];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'PickupTime');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [PickupTime];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'ReturnDate');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [ReturnDate];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'ReturnTime');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [ReturnTime];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Reservation]') AND [c].[name] = N'VehicleId');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [Reservation] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [Reservation] DROP COLUMN [VehicleId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ReservationDetails_ReservationID] ON [ReservationDetails] ([ReservationID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    CREATE INDEX [IX_ReservationDetails_VehicleId] ON [ReservationDetails] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260805072249_split_reservation_details'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260805072249_split_reservation_details', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806032054_split_user_customer_admin_profiles'
)
BEGIN
    DROP TABLE [Customer];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806032054_split_user_customer_admin_profiles'
)
BEGIN
    CREATE TABLE [Users] (
        [UserId] int NOT NULL IDENTITY,
        [Email] nvarchar(150) NOT NULL,
        [PasswordHash] nvarchar(100) NOT NULL,
        [Role] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([UserId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806032054_split_user_customer_admin_profiles'
)
BEGIN
    CREATE TABLE [AdminProfiles] (
        [AdminId] int NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [EmployeeCode] nvarchar(50) NOT NULL,
        CONSTRAINT [PK_AdminProfiles] PRIMARY KEY ([AdminId]),
        CONSTRAINT [FK_AdminProfiles_Users_AdminId] FOREIGN KEY ([AdminId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806032054_split_user_customer_admin_profiles'
)
BEGIN
    CREATE TABLE [CustomerProfiles] (
        [CustomerId] int NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [ContactNumber] nvarchar(max) NOT NULL,
        [ValidIDtype] nvarchar(max) NOT NULL,
        [ValidIDImagePath] nvarchar(255) NULL,
        [Status] int NOT NULL,
        CONSTRAINT [PK_CustomerProfiles] PRIMARY KEY ([CustomerId]),
        CONSTRAINT [FK_CustomerProfiles_Users_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806032054_split_user_customer_admin_profiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AdminProfiles_EmployeeCode] ON [AdminProfiles] ([EmployeeCode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806032054_split_user_customer_admin_profiles'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806032054_split_user_customer_admin_profiles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806032054_split_user_customer_admin_profiles', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806034144_remove_admin_employee_code'
)
BEGIN
    DROP INDEX [IX_AdminProfiles_EmployeeCode] ON [AdminProfiles];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806034144_remove_admin_employee_code'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AdminProfiles]') AND [c].[name] = N'EmployeeCode');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [AdminProfiles] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [AdminProfiles] DROP COLUMN [EmployeeCode];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806034144_remove_admin_employee_code'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806034144_remove_admin_employee_code', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806040022_add_email_verification'
)
BEGIN
    ALTER TABLE [Users] ADD [EmailConfirmed] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806040022_add_email_verification'
)
BEGIN
    ALTER TABLE [Users] ADD [EmailVerificationToken] nvarchar(128) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806040022_add_email_verification'
)
BEGIN
    ALTER TABLE [Users] ADD [EmailVerificationTokenExpires] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806040022_add_email_verification'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806040022_add_email_verification', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payment]') AND [c].[name] = N'BaseAmount');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [Payment] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [Payment] DROP COLUMN [BaseAmount];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payment]') AND [c].[name] = N'DiscountAmount');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [Payment] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [Payment] DROP COLUMN [DiscountAmount];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payment]') AND [c].[name] = N'PaymentChannel');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [Payment] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [Payment] DROP COLUMN [PaymentChannel];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payment]') AND [c].[name] = N'PaymentSubmitted');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Payment] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [Payment] DROP COLUMN [PaymentSubmitted];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payment]') AND [c].[name] = N'AccountName');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Payment] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [Payment] ALTER COLUMN [AccountName] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    CREATE INDEX [IX_Payment_ReservationId] ON [Payment] ([ReservationId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    ALTER TABLE [Payment] ADD CONSTRAINT [FK_Payment_Reservation_ReservationId] FOREIGN KEY ([ReservationId]) REFERENCES [Reservation] ([ReservationId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806143524_lean_payment_model'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806143524_lean_payment_model', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806180412_payment_status_enum'
)
BEGIN
    ALTER TABLE [Payment] ADD [PaymentStatusEnum] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806180412_payment_status_enum'
)
BEGIN
    UPDATE Payment
    SET PaymentStatusEnum = CASE
        WHEN PaymentStatus = 'Approved' THEN 1
        WHEN PaymentStatus = 'Rejected' THEN 2
        ELSE 0
    END
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806180412_payment_status_enum'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payment]') AND [c].[name] = N'PaymentStatus');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [Payment] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [Payment] DROP COLUMN [PaymentStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806180412_payment_status_enum'
)
BEGIN
    EXEC sp_rename N'[Payment].[PaymentStatusEnum]', N'PaymentStatus', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806180412_payment_status_enum'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806180412_payment_status_enum', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806183659_add_transit_table'
)
BEGIN
    CREATE TABLE [Transit] (
        [TransitID] int NOT NULL IDENTITY,
        [ReservationID] int NOT NULL,
        [DriverID] int NOT NULL,
        [VehicleID] int NOT NULL,
        [DepartureTime] time NOT NULL,
        [ReturnTime] time NOT NULL,
        [FuelLevelStart] int NOT NULL,
        [FuelLevelEnd] int NULL,
        [VehicleConditionStart] nvarchar(500) NULL,
        [PostTripImagePath] nvarchar(255) NULL,
        [Remarks] nvarchar(1000) NULL,
        [TripStatus] int NOT NULL,
        CONSTRAINT [PK_Transit] PRIMARY KEY ([TransitID]),
        CONSTRAINT [FK_Transit_Driver_DriverID] FOREIGN KEY ([DriverID]) REFERENCES [Driver] ([DriverId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Transit_Reservation_ReservationID] FOREIGN KEY ([ReservationID]) REFERENCES [Reservation] ([ReservationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_Transit_Vehicle_VehicleID] FOREIGN KEY ([VehicleID]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806183659_add_transit_table'
)
BEGIN
    CREATE INDEX [IX_Transit_DriverID] ON [Transit] ([DriverID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806183659_add_transit_table'
)
BEGIN
    CREATE INDEX [IX_Transit_ReservationID] ON [Transit] ([ReservationID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806183659_add_transit_table'
)
BEGIN
    CREATE INDEX [IX_Transit_VehicleID] ON [Transit] ([VehicleID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806183659_add_transit_table'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806183659_add_transit_table', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] DROP CONSTRAINT [FK_Transit_Driver_DriverID];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] DROP CONSTRAINT [FK_Transit_Reservation_ReservationID];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] DROP CONSTRAINT [FK_Transit_Vehicle_VehicleID];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    DROP INDEX [IX_Transit_ReservationID] ON [Transit];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transit]') AND [c].[name] = N'ReturnTime');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [Transit] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [Transit] ALTER COLUMN [ReturnTime] time NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transit]') AND [c].[name] = N'FuelLevelStart');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [Transit] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [Transit] ALTER COLUMN [FuelLevelStart] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transit]') AND [c].[name] = N'DriverID');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [Transit] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [Transit] ALTER COLUMN [DriverID] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Transit]') AND [c].[name] = N'DepartureTime');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [Transit] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [Transit] ALTER COLUMN [DepartureTime] time NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] ADD [PreTripImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] ADD [VehicleConditionEnd] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Transit_ReservationID] ON [Transit] ([ReservationID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] ADD CONSTRAINT [FK_Transit_Driver_DriverID] FOREIGN KEY ([DriverID]) REFERENCES [Driver] ([DriverId]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] ADD CONSTRAINT [FK_Transit_Reservation_ReservationID] FOREIGN KEY ([ReservationID]) REFERENCES [Reservation] ([ReservationId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    ALTER TABLE [Transit] ADD CONSTRAINT [FK_Transit_Vehicle_VehicleID] FOREIGN KEY ([VehicleID]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260806190439_transit_dispatch_flow'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260806190439_transit_dispatch_flow', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260808121402_remove_payment_status'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Payment]') AND [c].[name] = N'PaymentStatus');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [Payment] DROP CONSTRAINT [' + @var24 + '];');
    ALTER TABLE [Payment] DROP COLUMN [PaymentStatus];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260808121402_remove_payment_status'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260808121402_remove_payment_status', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [Payment] DROP CONSTRAINT [FK_Payment_Reservation_ReservationId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [Transit] DROP CONSTRAINT [FK_Transit_Reservation_ReservationID];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [ReservationDetails] DROP CONSTRAINT [FK_ReservationDetails_Reservation_ReservationID];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [ReservationDetails] DROP CONSTRAINT [FK_ReservationDetails_Vehicle_VehicleId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[Reservation]', N'Rental';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[Rental].[ReservationId]', N'RentalId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[Rental].[ReservationStatus]', N'RentalStatus', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [RentalOption] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[ReservationDetails]', N'RentalDetails';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[RentalDetails].[ReservationDetailsID]', N'RentalDetailsID', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[RentalDetails].[ReservationID]', N'RentalID', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[RentalDetails].[IX_ReservationDetails_ReservationID]', N'IX_RentalDetails_RentalID', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[RentalDetails].[IX_ReservationDetails_VehicleId]', N'IX_RentalDetails_VehicleId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[Payment].[ReservationId]', N'RentalId', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[Payment].[IX_Payment_ReservationId]', N'IX_Payment_RentalId', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[Transit].[ReservationID]', N'RentalID', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    EXEC sp_rename N'[Transit].[IX_Transit_ReservationID]', N'IX_Transit_RentalID', N'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [RentalDetails] ADD CONSTRAINT [FK_RentalDetails_Rental_RentalID] FOREIGN KEY ([RentalID]) REFERENCES [Rental] ([RentalId]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [RentalDetails] ADD CONSTRAINT [FK_RentalDetails_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [Payment] ADD CONSTRAINT [FK_Payment_Rental_RentalId] FOREIGN KEY ([RentalId]) REFERENCES [Rental] ([RentalId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    ALTER TABLE [Transit] ADD CONSTRAINT [FK_Transit_Rental_RentalID] FOREIGN KEY ([RentalID]) REFERENCES [Rental] ([RentalId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811140841_rename_reservation_to_rental'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811140841_rename_reservation_to_rental', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811152856_add_rental_payment_due_and_expired'
)
BEGIN
    ALTER TABLE [Rental] ADD [PaymentDueAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260811152856_add_rental_payment_due_and_expired'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260811152856_add_rental_payment_due_and_expired', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812073200_add_rental_customer_id'
)
BEGIN
    ALTER TABLE [Rental] ADD [CustomerId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812073200_add_rental_customer_id'
)
BEGIN
    UPDATE r
    SET r.CustomerId = c.CustomerId
    FROM Rental r
    INNER JOIN CustomerProfiles c ON r.ContactNumber = c.ContactNumber
    WHERE r.CustomerId IS NULL
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812073200_add_rental_customer_id'
)
BEGIN
    CREATE INDEX [IX_Rental_CustomerId] ON [Rental] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812073200_add_rental_customer_id'
)
BEGIN
    ALTER TABLE [Rental] ADD CONSTRAINT [FK_Rental_CustomerProfiles_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [CustomerProfiles] ([CustomerId]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812073200_add_rental_customer_id'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812073200_add_rental_customer_id', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812075301_add_feedback'
)
BEGIN
    CREATE TABLE [Feedback] (
        [FeedbackId] int NOT NULL IDENTITY,
        [TransitID] int NOT NULL,
        [CustomerId] int NOT NULL,
        [Rating] int NOT NULL,
        [Comment] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Feedback] PRIMARY KEY ([FeedbackId]),
        CONSTRAINT [FK_Feedback_CustomerProfiles_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [CustomerProfiles] ([CustomerId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Feedback_Transit_TransitID] FOREIGN KEY ([TransitID]) REFERENCES [Transit] ([TransitID]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812075301_add_feedback'
)
BEGIN
    CREATE INDEX [IX_Feedback_CustomerId] ON [Feedback] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812075301_add_feedback'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Feedback_TransitID] ON [Feedback] ([TransitID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260812075301_add_feedback'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260812075301_add_feedback', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814153642_add_account_lockout'
)
BEGIN
    ALTER TABLE [Users] ADD [AccessFailedCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814153642_add_account_lockout'
)
BEGIN
    ALTER TABLE [Users] ADD [LockoutEndUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814153642_add_account_lockout'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260814153642_add_account_lockout', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814163745_add_password_reset'
)
BEGIN
    ALTER TABLE [Users] ADD [PasswordResetToken] nvarchar(128) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814163745_add_password_reset'
)
BEGIN
    ALTER TABLE [Users] ADD [PasswordResetTokenExpires] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814163745_add_password_reset'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260814163745_add_password_reset', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814165506_add_rental_cancellation_fee'
)
BEGIN
    ALTER TABLE [Rental] ADD [CancellationFee] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814165506_add_rental_cancellation_fee'
)
BEGIN
    ALTER TABLE [Rental] ADD [CancelledAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260814165506_add_rental_cancellation_fee'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260814165506_add_rental_cancellation_fee', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817082249_add_driver_is_active'
)
BEGIN
    ALTER TABLE [Driver] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817082249_add_driver_is_active'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260817082249_add_driver_is_active', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817082345_set_existing_drivers_active'
)
BEGIN
    UPDATE [Driver] SET [IsActive] = 1 WHERE [IsActive] = 0
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817082345_set_existing_drivers_active'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260817082345_set_existing_drivers_active', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817084048_add_driver_created_at'
)
BEGIN
    ALTER TABLE [Driver] ADD [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE());
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817084048_add_driver_created_at'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260817084048_add_driver_created_at', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817084832_add_maintenance_log'
)
BEGIN
    CREATE TABLE [MaintenanceLog] (
        [MaintenanceLogId] int NOT NULL IDENTITY,
        [VehicleId] int NOT NULL,
        [Type] int NOT NULL,
        [Status] int NOT NULL,
        [ScheduledDate] date NOT NULL,
        [StartedAt] datetime2 NULL,
        [CompletedAt] datetime2 NULL,
        [Description] nvarchar(500) NULL,
        [WorkDone] nvarchar(1000) NULL,
        [Cost] decimal(18,2) NULL,
        [ImagePath] nvarchar(255) NULL,
        [Remarks] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_MaintenanceLog] PRIMARY KEY ([MaintenanceLogId]),
        CONSTRAINT [FK_MaintenanceLog_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817084832_add_maintenance_log'
)
BEGIN
    CREATE INDEX [IX_MaintenanceLog_VehicleId] ON [MaintenanceLog] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260817084832_add_maintenance_log'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260817084832_add_maintenance_log', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [Odometer] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    ALTER TABLE [MaintenanceLog] ADD [MaintenancePlanId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    ALTER TABLE [MaintenanceLog] ADD [Odometer] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN

    UPDATE MaintenanceLog SET [Type] = CASE [Type]
        WHEN 0 THEN 0
        WHEN 1 THEN 2
        WHEN 2 THEN 1
        WHEN 3 THEN 0
        WHEN 4 THEN 0
        WHEN 5 THEN 0
        WHEN 6 THEN 3
        WHEN 7 THEN 3
        WHEN 8 THEN 3
        WHEN 9 THEN 0
        ELSE 0
    END;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    CREATE TABLE [MaintenancePlan] (
        [MaintenancePlanId] int NOT NULL IDENTITY,
        [VehicleId] int NOT NULL,
        [Type] int NOT NULL,
        [Trigger] int NOT NULL,
        [IntervalKilometers] int NULL,
        [IntervalMonths] int NULL,
        [LastCompletedDate] date NULL,
        [LastOdometer] int NULL,
        [NextDueDate] date NULL,
        [NextDueOdometer] int NULL,
        CONSTRAINT [PK_MaintenancePlan] PRIMARY KEY ([MaintenancePlanId]),
        CONSTRAINT [FK_MaintenancePlan_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    CREATE INDEX [IX_MaintenanceLog_MaintenancePlanId] ON [MaintenanceLog] ([MaintenancePlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MaintenancePlan_VehicleId_Type] ON [MaintenancePlan] ([VehicleId], [Type]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN

    INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
    SELECT VehicleId, 0, 0, 5000, NULL, NULL, Odometer + 5000 FROM Vehicle;

    INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
    SELECT VehicleId, 1, 1, NULL, 1, DATEADD(month, 1, CAST(RegistrationDate AS date)), NULL FROM Vehicle;

    INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
    SELECT VehicleId, 2, 0, 10000, NULL, NULL, Odometer + 10000 FROM Vehicle;

    INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
    SELECT VehicleId, 3, 1, NULL, 6, DATEADD(month, 6, CAST(RegistrationDate AS date)), NULL FROM Vehicle;

    INSERT INTO MaintenancePlan (VehicleId, [Type], [Trigger], IntervalKilometers, IntervalMonths, NextDueDate, NextDueOdometer)
    SELECT VehicleId, 4, 0, 15000, NULL, NULL, Odometer + 15000 FROM Vehicle;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    ALTER TABLE [MaintenanceLog] ADD CONSTRAINT [FK_MaintenanceLog_MaintenancePlan_MaintenancePlanId] FOREIGN KEY ([MaintenancePlanId]) REFERENCES [MaintenancePlan] ([MaintenancePlanId]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818051129_add_pms_maintenance_plans'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260818051129_add_pms_maintenance_plans', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818233257_add_transit_odometer'
)
BEGIN
    ALTER TABLE [Transit] ADD [OdometerEnd] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818233257_add_transit_odometer'
)
BEGIN
    ALTER TABLE [Transit] ADD [OdometerStart] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260818233257_add_transit_odometer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260818233257_add_transit_odometer', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    ALTER TABLE [Feedback] ADD [DriverCourtesy] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    ALTER TABLE [Feedback] ADD [DriverDriving] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    ALTER TABLE [Feedback] ADD [DriverProfessionalism] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    ALTER TABLE [Feedback] ADD [VehicleComfort] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    ALTER TABLE [Feedback] ADD [VehiclePerformance] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    ALTER TABLE [Feedback] ADD [VehicleSafety] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    UPDATE Feedback
    SET VehicleComfort = Rating,
        VehiclePerformance = Rating,
        VehicleSafety = Rating
    WHERE VehicleComfort = 0 AND VehiclePerformance = 0 AND VehicleSafety = 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260819085129_add_feedback_category_ratings'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260819085129_add_feedback_category_ratings', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821061411_add_incident_report'
)
BEGIN
    CREATE TABLE [IncidentReport] (
        [IncidentReportId] int NOT NULL IDENTITY,
        [VehicleId] int NOT NULL,
        [TransitID] int NULL,
        [DriverID] int NULL,
        [Type] int NOT NULL,
        [Severity] int NOT NULL,
        [Status] int NOT NULL,
        [OccurredAt] datetime2 NOT NULL,
        [Location] nvarchar(200) NOT NULL,
        [Description] nvarchar(500) NOT NULL,
        [Odometer] int NULL,
        [IsUndrivable] bit NOT NULL,
        [ImagePath] nvarchar(255) NULL,
        [RepairStartedAt] datetime2 NULL,
        [WorkDone] nvarchar(1000) NULL,
        [Cost] decimal(18,2) NULL,
        [Remarks] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [ClosedAt] datetime2 NULL,
        CONSTRAINT [PK_IncidentReport] PRIMARY KEY ([IncidentReportId]),
        CONSTRAINT [FK_IncidentReport_Driver_DriverID] FOREIGN KEY ([DriverID]) REFERENCES [Driver] ([DriverId]) ON DELETE SET NULL,
        CONSTRAINT [FK_IncidentReport_Transit_TransitID] FOREIGN KEY ([TransitID]) REFERENCES [Transit] ([TransitID]) ON DELETE SET NULL,
        CONSTRAINT [FK_IncidentReport_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821061411_add_incident_report'
)
BEGIN
    CREATE INDEX [IX_IncidentReport_DriverID] ON [IncidentReport] ([DriverID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821061411_add_incident_report'
)
BEGIN
    CREATE INDEX [IX_IncidentReport_Status] ON [IncidentReport] ([Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821061411_add_incident_report'
)
BEGIN
    CREATE INDEX [IX_IncidentReport_TransitID] ON [IncidentReport] ([TransitID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821061411_add_incident_report'
)
BEGIN
    CREATE INDEX [IX_IncidentReport_VehicleId] ON [IncidentReport] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821061411_add_incident_report'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821061411_add_incident_report', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823232634_add_rental_fare_totals'
)
BEGIN
    ALTER TABLE [Rental] ADD [SucceedingFeeTotal] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823232634_add_rental_fare_totals'
)
BEGIN
    ALTER TABLE [Rental] ADD [TotalAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823232634_add_rental_fare_totals'
)
BEGIN

    UPDATE r
    SET r.TotalAmount = p.TotalAmount
    FROM Rental r
    INNER JOIN (
        SELECT RentalId, TotalAmount,
               ROW_NUMBER() OVER (PARTITION BY RentalId ORDER BY PaymentId DESC) AS rn
        FROM Payment
    ) p ON p.RentalId = r.RentalId AND p.rn = 1
    WHERE r.TotalAmount = 0 AND p.TotalAmount > 0;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260823232634_add_rental_fare_totals'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260823232634_add_rental_fare_totals', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824062000_remove_incident_odometer'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[IncidentReport]') AND [c].[name] = N'Odometer');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [IncidentReport] DROP CONSTRAINT [' + @var25 + '];');
    ALTER TABLE [IncidentReport] DROP COLUMN [Odometer];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824062000_remove_incident_odometer'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824062000_remove_incident_odometer', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824140000_vehicle_type_as_string'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [TypeName] nvarchar(30) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824140000_vehicle_type_as_string'
)
BEGIN

    UPDATE Vehicle SET TypeName = CASE [Type]
        WHEN 0 THEN N'SUV'
        WHEN 1 THEN N'Van'
        ELSE N'SUV'
    END;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824140000_vehicle_type_as_string'
)
BEGIN
    DECLARE @var26 sysname;
    SELECT @var26 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vehicle]') AND [c].[name] = N'Type');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [Vehicle] DROP CONSTRAINT [' + @var26 + '];');
    ALTER TABLE [Vehicle] DROP COLUMN [Type];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824140000_vehicle_type_as_string'
)
BEGIN
    EXEC sp_rename N'[Vehicle].[TypeName]', N'Type', N'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824140000_vehicle_type_as_string'
)
BEGIN
    DECLARE @var27 sysname;
    SELECT @var27 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Vehicle]') AND [c].[name] = N'Type');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [Vehicle] DROP CONSTRAINT [' + @var27 + '];');
    EXEC(N'UPDATE [Vehicle] SET [Type] = N''SUV'' WHERE [Type] IS NULL');
    ALTER TABLE [Vehicle] ALTER COLUMN [Type] nvarchar(30) NOT NULL;
    ALTER TABLE [Vehicle] ADD DEFAULT N'SUV' FOR [Type];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824140000_vehicle_type_as_string'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824140000_vehicle_type_as_string', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825210000_add_customer_id_front_back'
)
BEGIN
    ALTER TABLE [CustomerProfiles] ADD [FrontValidIDImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825210000_add_customer_id_front_back'
)
BEGIN
    ALTER TABLE [CustomerProfiles] ADD [BackValidIDImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825210000_add_customer_id_front_back'
)
BEGIN

    UPDATE CustomerProfiles
    SET FrontValidIDImagePath = ValidIDImagePath
    WHERE ValidIDImagePath IS NOT NULL AND ValidIDImagePath <> N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825210000_add_customer_id_front_back'
)
BEGIN
    DECLARE @var28 sysname;
    SELECT @var28 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'ValidIDImagePath');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var28 + '];');
    ALTER TABLE [CustomerProfiles] DROP COLUMN [ValidIDImagePath];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825210000_add_customer_id_front_back'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825210000_add_customer_id_front_back', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825234000_add_vehicle_favorite'
)
BEGIN
    CREATE TABLE [VehicleFavorite] (
        [FavoriteId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [VehicleId] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_VehicleFavorite] PRIMARY KEY ([FavoriteId]),
        CONSTRAINT [FK_VehicleFavorite_CustomerProfiles_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [CustomerProfiles] ([CustomerId]) ON DELETE CASCADE,
        CONSTRAINT [FK_VehicleFavorite_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825234000_add_vehicle_favorite'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_VehicleFavorite_CustomerId_VehicleId] ON [VehicleFavorite] ([CustomerId], [VehicleId]) WHERE [CustomerId] IS NOT NULL AND [VehicleId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825234000_add_vehicle_favorite'
)
BEGIN
    CREATE INDEX [IX_VehicleFavorite_VehicleId] ON [VehicleFavorite] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825234000_add_vehicle_favorite'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825234000_add_vehicle_favorite', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826002000_add_login_email_otp'
)
BEGIN
    ALTER TABLE [Users] ADD [LoginOtpExpiresUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826002000_add_login_email_otp'
)
BEGIN
    ALTER TABLE [Users] ADD [LoginOtpFailedCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826002000_add_login_email_otp'
)
BEGIN
    ALTER TABLE [Users] ADD [LoginOtpHash] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826002000_add_login_email_otp'
)
BEGIN
    ALTER TABLE [Users] ADD [LoginOtpSentAtUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826002000_add_login_email_otp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826002000_add_login_email_otp', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826004500_add_login_mfa_enabled'
)
BEGIN
    ALTER TABLE [Users] ADD [LoginMfaEnabled] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826004500_add_login_mfa_enabled'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826004500_add_login_mfa_enabled', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826081500_add_customer_self_deactivated'
)
BEGIN
    ALTER TABLE [CustomerProfiles] ADD [IsSelfDeactivated] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826081500_add_customer_self_deactivated'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826081500_add_customer_self_deactivated', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826093000_tighten_contact_and_id_lengths'
)
BEGIN
    DECLARE @var29 sysname;
    SELECT @var29 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Driver]') AND [c].[name] = N'ContactNo');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [Driver] DROP CONSTRAINT [' + @var29 + '];');
    ALTER TABLE [Driver] ALTER COLUMN [ContactNo] nvarchar(20) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826093000_tighten_contact_and_id_lengths'
)
BEGIN
    DECLARE @var30 sysname;
    SELECT @var30 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'ContactNumber');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var30 + '];');
    ALTER TABLE [CustomerProfiles] ALTER COLUMN [ContactNumber] nvarchar(20) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826093000_tighten_contact_and_id_lengths'
)
BEGIN
    DECLARE @var31 sysname;
    SELECT @var31 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'ValidIDtype');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var31 + '];');
    ALTER TABLE [CustomerProfiles] ALTER COLUMN [ValidIDtype] nvarchar(30) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826093000_tighten_contact_and_id_lengths'
)
BEGIN
    DECLARE @var32 sysname;
    SELECT @var32 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Rental]') AND [c].[name] = N'ContactNumber');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [Rental] DROP CONSTRAINT [' + @var32 + '];');
    ALTER TABLE [Rental] ALTER COLUMN [ContactNumber] nvarchar(20) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826093000_tighten_contact_and_id_lengths'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826093000_tighten_contact_and_id_lengths', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826120000_add_system_log'
)
BEGIN
    CREATE TABLE [SystemLog] (
        [SystemLogId] int NOT NULL IDENTITY,
        [ActorUserId] int NULL,
        [ActorName] nvarchar(100) NOT NULL,
        [Action] int NOT NULL,
        [Category] int NOT NULL,
        [EntityType] nvarchar(40) NULL,
        [EntityId] int NULL,
        [Summary] nvarchar(500) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_SystemLog] PRIMARY KEY ([SystemLogId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826120000_add_system_log'
)
BEGIN
    CREATE INDEX [IX_SystemLog_CreatedAt] ON [SystemLog] ([CreatedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826120000_add_system_log'
)
BEGIN
    CREATE INDEX [IX_SystemLog_Category_Action] ON [SystemLog] ([Category], [Action]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260826120000_add_system_log'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260826120000_add_system_log', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    DROP INDEX [IX_Transit_RentalID] ON [Transit];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    ALTER TABLE [Transit] ADD [RentalVehicleId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    CREATE TABLE [RentalVehicle] (
        [RentalVehicleId] int NOT NULL IDENTITY,
        [RentalId] int NOT NULL,
        [VehicleId] int NOT NULL,
        [LineBaseAmount] decimal(18,2) NOT NULL,
        [LineSucceedingFeeTotal] decimal(18,2) NOT NULL,
        [LineTotalAmount] decimal(18,2) NOT NULL,
        [SortOrder] int NOT NULL,
        CONSTRAINT [PK_RentalVehicle] PRIMARY KEY ([RentalVehicleId]),
        CONSTRAINT [FK_RentalVehicle_Rental_RentalId] FOREIGN KEY ([RentalId]) REFERENCES [Rental] ([RentalId]) ON DELETE CASCADE,
        CONSTRAINT [FK_RentalVehicle_Vehicle_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicle] ([VehicleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Transit_RentalID_VehicleID] ON [Transit] ([RentalID], [VehicleID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    CREATE INDEX [IX_Transit_RentalVehicleId] ON [Transit] ([RentalVehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RentalVehicle_RentalId_VehicleId] ON [RentalVehicle] ([RentalId], [VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    CREATE INDEX [IX_RentalVehicle_VehicleId] ON [RentalVehicle] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    INSERT INTO RentalVehicle (RentalId, VehicleId, LineBaseAmount, LineSucceedingFeeTotal, LineTotalAmount, SortOrder)
    SELECT d.RentalID, d.VehicleId,
           ISNULL(r.TotalAmount, 0) - ISNULL(r.SucceedingFeeTotal, 0),
           ISNULL(r.SucceedingFeeTotal, 0),
           ISNULL(r.TotalAmount, 0),
           0
    FROM RentalDetails d
    INNER JOIN Rental r ON r.RentalId = d.RentalID
    WHERE NOT EXISTS (
        SELECT 1 FROM RentalVehicle rv
        WHERE rv.RentalId = d.RentalID AND rv.VehicleId = d.VehicleId
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    UPDATE t
    SET t.RentalVehicleId = rv.RentalVehicleId
    FROM Transit t
    INNER JOIN RentalVehicle rv
        ON rv.RentalId = t.RentalID AND rv.VehicleId = t.VehicleID;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    ALTER TABLE [Transit] ADD CONSTRAINT [FK_Transit_RentalVehicle_RentalVehicleId] FOREIGN KEY ([RentalVehicleId]) REFERENCES [RentalVehicle] ([RentalVehicleId]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827150432_add_rental_vehicle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827150432_add_rental_vehicle', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827164000_add_admin_self_deactivated'
)
BEGIN
    ALTER TABLE [AdminProfiles] ADD [IsSelfDeactivated] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827164000_add_admin_self_deactivated'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827164000_add_admin_self_deactivated', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827213000_add_profile_images'
)
BEGIN
    ALTER TABLE [CustomerProfiles] ADD [ProfileImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827213000_add_profile_images'
)
BEGIN
    ALTER TABLE [AdminProfiles] ADD [ProfileImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827213000_add_profile_images'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827213000_add_profile_images', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828150000_add_vehicle_is_active'
)
BEGIN
    ALTER TABLE [Vehicle] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828150000_add_vehicle_is_active'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828150000_add_vehicle_is_active', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828180000_add_pending_vehicle_ids'
)
BEGIN
    ALTER TABLE [Rental] ADD [PendingVehicleIdsJson] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828180000_add_pending_vehicle_ids'
)
BEGIN
    ;WITH PendingLines AS (
        SELECT rv.RentalId,
               '[' + STRING_AGG(CAST(rv.VehicleId AS nvarchar(11)), ',') WITHIN GROUP (ORDER BY rv.SortOrder, rv.RentalVehicleId) + ']' AS JsonIds
        FROM RentalVehicle rv
        INNER JOIN Rental r ON r.RentalId = rv.RentalId
        WHERE r.RentalStatus = 0
        GROUP BY rv.RentalId
    )
    UPDATE r
    SET PendingVehicleIdsJson = p.JsonIds
    FROM Rental r
    INNER JOIN PendingLines p ON p.RentalId = r.RentalId;

    DELETE rv
    FROM RentalVehicle rv
    INNER JOIN Rental r ON r.RentalId = rv.RentalId
    WHERE r.RentalStatus = 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260828180000_add_pending_vehicle_ids'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260828180000_add_pending_vehicle_ids', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829100000_add_rental_resolution'
)
BEGIN
    ALTER TABLE [Rental] ADD [CompanyCancellationReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829100000_add_rental_resolution'
)
BEGIN
    ALTER TABLE [Rental] ADD [RefundAmount] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829100000_add_rental_resolution'
)
BEGIN
    ALTER TABLE [Rental] ADD [RefundedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829100000_add_rental_resolution'
)
BEGIN
    ALTER TABLE [Rental] ADD [RefundReceiptImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829100000_add_rental_resolution'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829100000_add_rental_resolution', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829120000_add_transit_trip_photo_paths'
)
BEGIN
    ALTER TABLE [Transit] ADD [PostTripImagePathsJson] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829120000_add_transit_trip_photo_paths'
)
BEGIN
    ALTER TABLE [Transit] ADD [PreTripImagePathsJson] nvarchar(2000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829120000_add_transit_trip_photo_paths'
)
BEGIN
    UPDATE Transit
    SET PreTripImagePathsJson = CONCAT('["', REPLACE(PreTripImagePath, '"', '\"'), '"]')
    WHERE PreTripImagePath IS NOT NULL AND LTRIM(RTRIM(PreTripImagePath)) <> '';

    UPDATE Transit
    SET PostTripImagePathsJson = CONCAT('["', REPLACE(PostTripImagePath, '"', '\"'), '"]')
    WHERE PostTripImagePath IS NOT NULL AND LTRIM(RTRIM(PostTripImagePath)) <> '';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260829120000_add_transit_trip_photo_paths'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260829120000_add_transit_trip_photo_paths', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831043125_hatdog'
)
BEGIN

    UPDATE Rental SET ContactNumber = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ContactNumber)), N'+', N''), N' ', N''), N'-', N'');
    UPDATE Rental SET ContactNumber = N'0' + SUBSTRING(ContactNumber, 3, 10)
    WHERE ContactNumber LIKE N'63%' AND LEN(ContactNumber) = 12;
    UPDATE Rental SET ContactNumber = RIGHT(ContactNumber, 11) WHERE LEN(ContactNumber) > 11;
    UPDATE Rental SET ContactNumber = N'09000000000'
    WHERE ContactNumber IS NULL OR LTRIM(RTRIM(ContactNumber)) = N'' OR LEN(ContactNumber) < 11;

    UPDATE CustomerProfiles SET ContactNumber = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ContactNumber)), N'+', N''), N' ', N''), N'-', N'');
    UPDATE CustomerProfiles SET ContactNumber = N'0' + SUBSTRING(ContactNumber, 3, 10)
    WHERE ContactNumber LIKE N'63%' AND LEN(ContactNumber) = 12;
    UPDATE CustomerProfiles SET ContactNumber = RIGHT(ContactNumber, 11) WHERE LEN(ContactNumber) > 11;
    UPDATE CustomerProfiles SET ContactNumber = N'09000000000'
    WHERE ContactNumber IS NULL OR LTRIM(RTRIM(ContactNumber)) = N'' OR LEN(ContactNumber) < 11;

    UPDATE Driver SET ContactNo = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ContactNo)), N'+', N''), N' ', N''), N'-', N'');
    UPDATE Driver SET ContactNo = N'0' + SUBSTRING(ContactNo, 3, 10)
    WHERE ContactNo LIKE N'63%' AND LEN(ContactNo) = 12;
    UPDATE Driver SET ContactNo = RIGHT(ContactNo, 11) WHERE LEN(ContactNo) > 11;
    UPDATE Driver SET ContactNo = N'09000000000'
    WHERE ContactNo IS NULL OR LTRIM(RTRIM(ContactNo)) = N'' OR LEN(ContactNo) < 11;

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831043125_hatdog'
)
BEGIN
    DECLARE @var33 sysname;
    SELECT @var33 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Rental]') AND [c].[name] = N'ContactNumber');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [Rental] DROP CONSTRAINT [' + @var33 + '];');
    ALTER TABLE [Rental] ALTER COLUMN [ContactNumber] nvarchar(11) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831043125_hatdog'
)
BEGIN
    DECLARE @var34 sysname;
    SELECT @var34 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Driver]') AND [c].[name] = N'ContactNo');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [Driver] DROP CONSTRAINT [' + @var34 + '];');
    ALTER TABLE [Driver] ALTER COLUMN [ContactNo] nvarchar(11) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831043125_hatdog'
)
BEGIN
    DECLARE @var35 sysname;
    SELECT @var35 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'ContactNumber');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var35 + '];');
    ALTER TABLE [CustomerProfiles] ALTER COLUMN [ContactNumber] nvarchar(11) NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831043125_hatdog'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831043125_hatdog', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831043238_refundCancellation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831043238_refundCancellation', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831130000_add_customer_cancellation_refund'
)
BEGIN
    ALTER TABLE [Rental] ADD [CustomerCancellationReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831130000_add_customer_cancellation_refund'
)
BEGIN
    ALTER TABLE [Rental] ADD [RefundRequestedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831130000_add_customer_cancellation_refund'
)
BEGIN
    ALTER TABLE [Rental] ADD [RefundRequestedAmount] decimal(18,2) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831130000_add_customer_cancellation_refund'
)
BEGIN
    ALTER TABLE [Rental] ADD [CancellationFeePaidAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831130000_add_customer_cancellation_refund'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831130000_add_customer_cancellation_refund', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831140000_add_refund_rejection'
)
BEGIN
    ALTER TABLE [Rental] ADD [RefundRejectedAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831140000_add_refund_rejection'
)
BEGIN
    ALTER TABLE [Rental] ADD [RefundRejectionReason] nvarchar(500) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831140000_add_refund_rejection'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831140000_add_refund_rejection', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831150000_add_transit_issue_links'
)
BEGIN
    CREATE TABLE [TransitIssueLink] (
        [TransitIssueLinkId] int NOT NULL IDENTITY,
        [TransitID] int NOT NULL,
        [Source] int NOT NULL,
        [IncidentReportId] int NULL,
        [MaintenanceLogId] int NULL,
        [BlocksTrip] bit NOT NULL,
        [LinkedAt] datetime2 NOT NULL,
        [ResolvedAt] datetime2 NULL,
        [ResolutionAction] nvarchar(50) NULL,
        [ResolutionNotes] nvarchar(500) NULL,
        CONSTRAINT [PK_TransitIssueLink] PRIMARY KEY ([TransitIssueLinkId]),
        CONSTRAINT [FK_TransitIssueLink_IncidentReport_IncidentReportId] FOREIGN KEY ([IncidentReportId]) REFERENCES [IncidentReport] ([IncidentReportId]),
        CONSTRAINT [FK_TransitIssueLink_MaintenanceLog_MaintenanceLogId] FOREIGN KEY ([MaintenanceLogId]) REFERENCES [MaintenanceLog] ([MaintenanceLogId]),
        CONSTRAINT [FK_TransitIssueLink_Transit_TransitID] FOREIGN KEY ([TransitID]) REFERENCES [Transit] ([TransitID]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831150000_add_transit_issue_links'
)
BEGIN
    CREATE INDEX [IX_TransitIssueLink_IncidentReportId] ON [TransitIssueLink] ([IncidentReportId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831150000_add_transit_issue_links'
)
BEGIN
    CREATE INDEX [IX_TransitIssueLink_MaintenanceLogId] ON [TransitIssueLink] ([MaintenanceLogId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831150000_add_transit_issue_links'
)
BEGIN
    CREATE INDEX [IX_TransitIssueLink_TransitID] ON [TransitIssueLink] ([TransitID]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260831150000_add_transit_issue_links'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260831150000_add_transit_issue_links', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [Discount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [DiscountImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [DropoffLocation] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [PassengerCount] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [PickupDate] date NOT NULL DEFAULT '0001-01-01';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [PickupLocation] nvarchar(200) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [PickupTime] time NOT NULL DEFAULT '00:00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [ReturnDate] date NOT NULL DEFAULT '0001-01-01';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    ALTER TABLE [Rental] ADD [ReturnTime] time NOT NULL DEFAULT '00:00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    UPDATE r
    SET
        r.PickupLocation = d.PickupLocation,
        r.DropoffLocation = d.DropoffLocation,
        r.PickupDate = d.PickupDate,
        r.ReturnDate = d.ReturnDate,
        r.PickupTime = d.PickupTime,
        r.ReturnTime = d.ReturnTime,
        r.PassengerCount = d.PassengerCount,
        r.Discount = d.Discount,
        r.DiscountImagePath = d.DiscountImagePath
    FROM Rental r
    INNER JOIN RentalDetails d ON d.RentalID = r.RentalId;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    DROP TABLE [RentalDetails];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260910140116_merge_rental_details_into_rental'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260910140116_merge_rental_details_into_rental', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911150000_add_staff_profile'
)
BEGIN
    CREATE TABLE [StaffProfiles] (
        [StaffId] int NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [ProfileImagePath] nvarchar(255) NULL,
        [IsSelfDeactivated] bit NOT NULL,
        CONSTRAINT [PK_StaffProfiles] PRIMARY KEY ([StaffId]),
        CONSTRAINT [FK_StaffProfiles_Users_StaffId] FOREIGN KEY ([StaffId]) REFERENCES [Users] ([UserId]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911150000_add_staff_profile'
)
BEGIN
    INSERT INTO StaffProfiles (StaffId, FullName, ProfileImagePath, IsSelfDeactivated)
    SELECT ap.AdminId, ap.FullName, ap.ProfileImagePath, ap.IsSelfDeactivated
    FROM AdminProfiles ap
    INNER JOIN Users u ON u.UserId = ap.AdminId
    WHERE u.Role = 2;

    DELETE ap
    FROM AdminProfiles ap
    INNER JOIN Users u ON u.UserId = ap.AdminId
    WHERE u.Role = 2;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911150000_add_staff_profile'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911150000_add_staff_profile', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    ALTER TABLE [AdminProfiles] ADD [ContactNumber] nvarchar(11) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    ALTER TABLE [AdminProfiles] ADD [Address] nvarchar(255) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    ALTER TABLE [AdminProfiles] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    UPDATE p
    SET p.CreatedAt = u.CreatedAt
    FROM AdminProfiles p
    INNER JOIN Users u ON u.UserId = p.AdminId
    WHERE p.CreatedAt = '0001-01-01T00:00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    ALTER TABLE [StaffProfiles] ADD [ContactNumber] nvarchar(11) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    ALTER TABLE [StaffProfiles] ADD [Address] nvarchar(255) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    ALTER TABLE [StaffProfiles] ADD [CreatedAt] datetime2 NOT NULL DEFAULT '0001-01-01T00:00:00.0000000';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    UPDATE p
    SET p.CreatedAt = u.CreatedAt
    FROM StaffProfiles p
    INNER JOIN Users u ON u.UserId = p.StaffId
    WHERE p.CreatedAt = '0001-01-01T00:00:00';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911170000_add_admin_staff_contact_address'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911170000_add_admin_staff_contact_address', N'8.0.29');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    ALTER TABLE [Users] ADD [FullName] nvarchar(100) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    ALTER TABLE [Users] ADD [ContactNumber] nvarchar(11) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    ALTER TABLE [Users] ADD [Address] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    ALTER TABLE [Users] ADD [ProfileImagePath] nvarchar(255) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    ALTER TABLE [Users] ADD [Status] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    ALTER TABLE [Users] ADD [IsSelfDeactivated] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN

    UPDATE u SET
        u.FullName = c.FullName,
        u.ContactNumber = c.ContactNumber,
        u.ProfileImagePath = c.ProfileImagePath,
        u.Status = c.Status,
        u.IsSelfDeactivated = c.IsSelfDeactivated
    FROM Users u
    INNER JOIN CustomerProfiles c ON c.CustomerId = u.UserId;

    UPDATE u SET
        u.FullName = a.FullName,
        u.ContactNumber = a.ContactNumber,
        u.Address = a.Address,
        u.ProfileImagePath = a.ProfileImagePath,
        u.Status = 0,
        u.IsSelfDeactivated = a.IsSelfDeactivated
    FROM Users u
    INNER JOIN AdminProfiles a ON a.AdminId = u.UserId;

    UPDATE u SET
        u.FullName = s.FullName,
        u.ContactNumber = s.ContactNumber,
        u.Address = s.Address,
        u.ProfileImagePath = s.ProfileImagePath,
        u.Status = 0,
        u.IsSelfDeactivated = s.IsSelfDeactivated
    FROM Users u
    INNER JOIN StaffProfiles s ON s.StaffId = u.UserId;

    UPDATE Users SET FullName = LEFT(Email, 100) WHERE FullName = '';

END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    DROP TABLE [AdminProfiles];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    DROP TABLE [StaffProfiles];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    DECLARE @var36 sysname;
    SELECT @var36 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'ContactNumber');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var36 + '];');
    ALTER TABLE [CustomerProfiles] DROP COLUMN [ContactNumber];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    DECLARE @var37 sysname;
    SELECT @var37 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'FullName');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var37 + '];');
    ALTER TABLE [CustomerProfiles] DROP COLUMN [FullName];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    DECLARE @var38 sysname;
    SELECT @var38 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'IsSelfDeactivated');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var38 + '];');
    ALTER TABLE [CustomerProfiles] DROP COLUMN [IsSelfDeactivated];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    DECLARE @var39 sysname;
    SELECT @var39 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'ProfileImagePath');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var39 + '];');
    ALTER TABLE [CustomerProfiles] DROP COLUMN [ProfileImagePath];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    DECLARE @var40 sysname;
    SELECT @var40 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerProfiles]') AND [c].[name] = N'Status');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [CustomerProfiles] DROP CONSTRAINT [' + @var40 + '];');
    ALTER TABLE [CustomerProfiles] DROP COLUMN [Status];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260925020241_merge_profiles_into_user'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260925020241_merge_profiles_into_user', N'8.0.29');
END;
GO

COMMIT;
GO

