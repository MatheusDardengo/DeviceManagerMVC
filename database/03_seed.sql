-- Seeds sample devices. Safe to run multiple times: a (Name, Brand) pair is added only if missing.
USE [DeviceManager];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now DATETIME2(7) = SYSUTCDATETIME();

INSERT INTO dbo.Devices (Name, Brand, State, CreatedAtUtc, UpdatedAtUtc)
SELECT s.Name, s.Brand, s.State, @now, @now
FROM (VALUES
        (N'iPhone 15 Pro',       N'Apple',     1),
        (N'MacBook Pro 14',      N'Apple',     2),
        (N'Galaxy S24',          N'Samsung',   1),
        (N'Galaxy Tab S9',       N'Samsung',   3),
        (N'Pixel 8',             N'Google',    2),
        (N'ThinkPad X1 Carbon',  N'Lenovo',    1),
        (N'XPS 13',              N'Dell',      1),
        (N'Surface Pro 9',       N'Microsoft', 3)
     ) AS s (Name, Brand, State)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Devices d
    WHERE d.Name = s.Name AND d.Brand = s.Brand
);

COMMIT TRANSACTION;
GO
