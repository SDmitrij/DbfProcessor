IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeDayNight'
            AND type = 'P')
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeDayNight]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeDayNight] AS
BEGIN
WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					SHOP_ID,
					DATE
				ORDER BY 
					SHOP_ID,
					DATE

		) rnk FROM [stage].[day_night]
	)
DELETE FROM CTE
WHERE rnk > 1
BEGIN TRY
	BEGIN TRANSACTION
		MERGE [dbo].[day_night] AS target
		USING [stage].[day_night] AS source
			ON (target.SHOP_ID = source.SHOP_ID
			AND target.DATE = source.DATE)
		WHEN MATCHED
			THEN UPDATE
				SET
					SUM_R_D = source.SUM_R_D,
					SUM_G_D = source.SUM_G_D,
					AM_R_D = source.AM_R_D,
					AM_CH_D = source.AM_CH_D,
					AM_P_D = source.AM_P_D,
					WEIGHT_D = source.WEIGHT_D,
					ALLDAY = source.ALLDAY,
					SUM_R_N = source.SUM_R_N,
					SUM_G_N = source.SUM_G_N,
					AM_R_N = source.AM_R_N,
					AM_CH_N = source.AM_CH_N,
					AM_P_N = source.AM_P_N,
					WEIGHT_N = source.WEIGHT_N 

		WHEN NOT MATCHED
			THEN INSERT 
				VALUES
				(
					source.DATE,
					source.SHOP_ID,
					source.SUM_R_D,
					source.SUM_G_D,
					source.AM_R_D,
					source.AM_CH_D,
					source.AM_P_D,
					source.WEIGHT_D,
					source.ALLDAY,
					source.SUM_R_N,
					source.SUM_G_N,
					source.AM_R_N,
					source.AM_CH_N,
					source.AM_P_N,
					source.WEIGHT_N
				);
		TRUNCATE TABLE [stage].[day_night]
	COMMIT TRANSACTION
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION
	INSERT INTO [service].[stage_errors] (STAGE_PROC, PROBLEM, DATE_TIME) VALUES (''sp_MergeDayNight'',
		ERROR_MESSAGE(), CURRENT_TIMESTAMP)
END CATCH
END')