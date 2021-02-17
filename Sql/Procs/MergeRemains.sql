IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeRemains'
            AND type = 'P'
      )
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeRemains]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeRemains] AS
BEGIN
WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					SHOP_ID,
					PROD_ID,
					REP_DATE,
					DISC
				ORDER BY 
					SHOP_ID,
					PROD_ID,
					REP_DATE,
					DISC
		) rnk FROM [stage].[remains]
	)
DELETE FROM CTE
WHERE rnk > 1

	MERGE [dbo].[remains] AS target
	USING [stage].[remains] AS source
		ON (target.SHOP_ID = source.SHOP_ID 
		AND target.PROD_ID = source.PROD_ID
		AND target.REP_DATE = source.REP_DATE
		AND target.DISC = source.DISC)
	WHEN MATCHED
		THEN UPDATE
			SET
				BEGIN_QTY = source.BEGIN_QTY,     
				BEGIN_SUM = source.BEGIN_SUM,     
				END_QTY = source.END_QTY,     
				END_SUM = source.END_SUM,     
				BEG_PRICE = source.BEG_PRICE,     
				END_PRICE = source.END_PRICE
	WHEN NOT MATCHED
		THEN INSERT 
			VALUES
			(
			   source.[PROD_ID]
			  ,source.[REP_DATE]
			  ,source.[DISC]
			  ,source.[BEGIN_QTY]
			  ,source.[BEGIN_SUM]
			  ,source.[END_QTY]
			  ,source.[END_SUM]
			  ,source.[BEG_PRICE]
			  ,source.[END_PRICE]
			  ,source.[SHOP_ID]
			);
TRUNCATE TABLE [stage].[remains]
END') 