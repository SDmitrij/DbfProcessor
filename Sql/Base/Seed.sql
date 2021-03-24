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
		[ID] INT NOT NULL PRIMARY KEY IDENTITY(1, 1),
		[PACK_NAME] CHAR(50) NOT NULL,
		[DBF_NAME] CHAR(50) NOT NULL,
		[BULKED] BIT NOT NULL,
		[TIME] SMALLDATETIME
	)')
END

IF NOT EXISTS(SELECT * FROM information_schema.tables WHERE table_schema = 'service' AND table_name = 'stage_errors')
BEGIN
	EXEC('CREATE TABLE [service].[stage_errors]
	(
		[ID] INT PRIMARY KEY IDENTITY(1,1),
		[STAGE_PROC] CHAR(100),
		[PROBLEM] NVARCHAR(MAX),
		[DATE_TIME] SMALLDATETIME
	)')
END

IF EXISTS 
(SELECT * FROM sys.objects 
WHERE object_id = OBJECT_ID(N'[dbo].[fn_CheckBulked]') 
AND type in (N'FN', N'IF', N'TF', N'FS', N'FT'))
BEGIN
	EXEC('DROP FUNCTION [dbo].[fn_CheckBulked]');
END
EXEC('CREATE FUNCTION [dbo].[fn_CheckBulked]
(
	@dbfName CHAR(50),
	@packName CHAR(50)
)
RETURNS INT
AS
BEGIN
	DECLARE @res INT
	IF EXISTS (SELECT [ID] FROM [service].[sync_info] WHERE 
	[DBF_NAME] = @dbfName
	AND [PACK_NAME] = @packName 
	AND [BULKED] = 1)
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
	@bulked BIT,
	@time SMALLDATETIME
AS
BEGIN
	IF EXISTS (SELECT * FROM [service].[sync_info] WHERE [PACK_NAME] = @packName AND [DBF_NAME] = @dbfName)
	BEGIN
		UPDATE [service].[sync_info]
		SET 
			[BULKED] = @bulked,
			[TIME] = @time
		WHERE [PACK_NAME] = @packName AND [DBF_NAME] = @dbfName
	END
	ELSE
	BEGIN
		INSERT INTO [service].[sync_info] ([PACK_NAME], [DBF_NAME], [BULKED], [TIME])
		VALUES (@packName, @dbfName, @bulked, @time)
	END
END')