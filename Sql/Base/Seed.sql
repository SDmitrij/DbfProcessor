IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = N'stage')
BEGIN
	EXEC('CREATE SCHEMA [stage]');
END

IF NOT EXISTS (SELECT name FROM sys.schemas WHERE name = N'service')
BEGIN
	EXEC('CREATE SCHEMA [service]');
END

IF NOT EXISTS(SELECT * FROM information_schema.tables WHERE table_schema = 'service' AND table_name = 'sync_info')
BEGIN
	EXEC('CREATE TABLE [service].[sync_info]
	(
		[Id] INT NOT NULL PRIMARY KEY IDENTITY(1, 1),
		[PackName] CHAR(50) NOT NULL,
		[DbfName] CHAR(50) NOT NULL,
		[Bulked] BIT NOT NULL
	)')
END

IF EXISTS 
(SELECT * FROM sys.objects 
WHERE object_id = OBJECT_ID(N'[dbo].[fn_NotBulkedDbfs]') 
AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	EXEC('DROP FUNCTION [dbo].[fn_NotBulkedDbfs]');
END
EXEC('CREATE FUNCTION [dbo].[fn_NotBulkedDbfs]
(
	@dbfName CHAR(50),
	@packName CHAR(50)
)
RETURNS INT
AS
BEGIN
	DECLARE @res INT
	IF EXISTS (SELECT [Id] FROM [service].[sync_info] WHERE 
	[DbfName] = @dbfName
	AND [PackName] = @packName 
	AND [Bulked] = 1)
		SET @res = 1;
	ELSE
		SET @res = 0;
	RETURN @res
END')

IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_InsertSyncInfo'
            AND type = 'P')
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_InsertSyncInfo]')
END
EXEC('CREATE PROCEDURE [dbo].[sp_InsertSyncInfo]
	@packName CHAR(50),
	@dbfName CHAR(50),
	@bulked BIT
AS
BEGIN
	IF EXISTS (SELECT * FROM [service].[sync_info] WHERE PackName = @packName AND DbfName = @dbfName)
	BEGIN
		UPDATE [service].[sync_info]
		SET [Bulked] = @bulked
		WHERE PackName = @packName AND DbfName = @dbfName
	END
	ELSE
	BEGIN
		INSERT INTO [service].[sync_info] ([PackName], [DbfName], [Bulked])
		VALUES (@packName, @dbfName, @bulked)
	END
END')