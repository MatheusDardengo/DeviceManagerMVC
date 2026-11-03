-- Seeds a demo user and sample devices. Safe to run multiple times: rows are added only if missing.
USE [DeviceManager];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @now DATETIME2(7) = SYSUTCDATETIME();

-- Demo login: demo@devicemanager.local / Demo@12345.
-- Email is stored normalized (trimmed, lower-case); the hash is ASP.NET Core Identity's
-- PasswordHasher v3 format (PBKDF2-HMAC-SHA512), the same one the API verifies against.
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = N'demo@devicemanager.local')
    INSERT INTO dbo.Users (Email, PasswordHash, CreatedAtUtc)
    VALUES (N'demo@devicemanager.local',
            N'AQAAAAIAAYagAAAAEMug4R2Hs0QkbJnyyMEOz78hnKi4DxCJA4nS/QBDDEJwN2CSAPy7Dj8tAdaqWVBWZA==',
            @now);

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
