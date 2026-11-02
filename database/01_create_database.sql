-- Creates the DeviceManager database. Safe to run multiple times.
IF DB_ID(N'DeviceManager') IS NULL
BEGIN
    CREATE DATABASE [DeviceManager];
END
GO
