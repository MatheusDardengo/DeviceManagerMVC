-- Creates the DeviceManager schema. Safe to run multiple times.
USE [DeviceManager];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* ---------- Users ---------- */
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id            INT            IDENTITY(1,1) NOT NULL,
        Email         NVARCHAR(256)  NOT NULL,
        PasswordHash  NVARCHAR(512)  NOT NULL,
        CreatedAtUtc  DATETIME2(7)   NOT NULL,

        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Users_Email UNIQUE (Email),
        CONSTRAINT CK_Users_Email_NotBlank CHECK (LEN(TRIM(Email)) > 0)
    );
END
GO

/* ---------- RefreshTokens ---------- */
IF OBJECT_ID(N'dbo.RefreshTokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshTokens
    (
        Id                   INT               IDENTITY(1,1) NOT NULL,
        UserId               INT               NOT NULL,
        TokenHash            BINARY(32)        NOT NULL,   -- SHA-256 of the raw token
        FamilyId             UNIQUEIDENTIFIER  NOT NULL,   -- shared by every rotation of one login
        CreatedAtUtc         DATETIME2(7)      NOT NULL,
        ExpiresAtUtc         DATETIME2(7)      NOT NULL,
        RevokedAtUtc         DATETIME2(7)      NULL,
        ReplacedByTokenHash  BINARY(32)        NULL,

        CONSTRAINT PK_RefreshTokens PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_RefreshTokens_TokenHash UNIQUE (TokenHash),
        CONSTRAINT FK_RefreshTokens_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id) ON DELETE CASCADE,
        CONSTRAINT CK_RefreshTokens_Expiry CHECK (ExpiresAtUtc > CreatedAtUtc)
    );

    CREATE NONCLUSTERED INDEX IX_RefreshTokens_FamilyId ON dbo.RefreshTokens (FamilyId);
    CREATE NONCLUSTERED INDEX IX_RefreshTokens_UserId   ON dbo.RefreshTokens (UserId);
END
GO

/* ---------- Devices ---------- */
IF OBJECT_ID(N'dbo.Devices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Devices
    (
        Id            INT            IDENTITY(1,1) NOT NULL,
        Name          NVARCHAR(100)  NOT NULL,
        Brand         NVARCHAR(100)  NOT NULL,
        State         TINYINT        NOT NULL,   -- 1 Available, 2 InUse, 3 Inactive
        CreatedAtUtc  DATETIME2(7)   NOT NULL,
        UpdatedAtUtc  DATETIME2(7)   NOT NULL,

        CONSTRAINT PK_Devices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Devices_State          CHECK (State IN (1, 2, 3)),
        CONSTRAINT CK_Devices_Name_NotBlank  CHECK (LEN(TRIM(Name))  > 0),
        CONSTRAINT CK_Devices_Brand_NotBlank CHECK (LEN(TRIM(Brand)) > 0),
        CONSTRAINT CK_Devices_UpdatedAfterCreated CHECK (UpdatedAtUtc >= CreatedAtUtc)
    );

    CREATE NONCLUSTERED INDEX IX_Devices_Brand ON dbo.Devices (Brand);
    CREATE NONCLUSTERED INDEX IX_Devices_State ON dbo.Devices (State);
END
GO
