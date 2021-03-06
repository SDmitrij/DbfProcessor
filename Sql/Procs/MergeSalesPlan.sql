IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeSalesPlan'
            AND type = 'P'
      )
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeSalesPlan]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeSalesPlan] AS
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
		) rnk FROM [stage].[sales_plan]
	)
DELETE FROM CTE
WHERE rnk > 1
BEGIN TRY
	BEGIN TRANSACTION
		MERGE [dbo].[sales_plan] AS target
		USING [stage].[sales_plan] AS source
			ON (target.SHOP_ID = source.SHOP_ID 
			AND target.DATE = source.DATE)
		WHEN NOT MATCHED
			THEN INSERT 
				VALUES
				(
				   source.[DATE]
				  ,source.[SHOP_ID]
				  ,source.[AM_HOURS]
				  ,source.[AM_DAY]
				  ,source.[AM_NIGHT]
				);
		TRUNCATE TABLE [stage].[sales_plan]
	COMMIT TRANSACTION
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION
	INSERT INTO [service].[stage_errors] (STAGE_PROC, PROBLEM, DATE_TIME) VALUES (''sp_MergeSalesPlan'',
		ERROR_MESSAGE(), CURRENT_TIMESTAMP)
END CATCH
END')

