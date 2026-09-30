-- Repair missing Vehicle.Type column on host database.
-- Safe to run more than once.

IF COL_LENGTH('Vehicle', 'Type') IS NULL
BEGIN
    IF COL_LENGTH('Vehicle', 'TypeName') IS NOT NULL
    BEGIN
        EXEC sp_rename N'Vehicle.TypeName', N'Type', N'COLUMN';
    END
    ELSE
    BEGIN
        ALTER TABLE [Vehicle] ADD [Type] nvarchar(30) NOT NULL CONSTRAINT DF_Vehicle_Type_Repair DEFAULT N'SUV';
    END
END;

IF COL_LENGTH('Vehicle', 'Type') IS NOT NULL
BEGIN
    UPDATE [Vehicle]
    SET [Type] = N'SUV'
    WHERE [Type] IS NULL OR LTRIM(RTRIM([Type])) = N'';

    DECLARE @typeConstraint sysname;
    SELECT @typeConstraint = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE [d].[parent_object_id] = OBJECT_ID(N'[Vehicle]') AND [c].[name] = N'Type';

    IF @typeConstraint IS NULL
    BEGIN
        ALTER TABLE [Vehicle] ADD CONSTRAINT DF_Vehicle_Type_Repair DEFAULT N'SUV' FOR [Type];
    END
END;
