IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeSalesByDataBase'
            AND type = 'P'
      )
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeSalesByDataBase]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeSalesByDataBase] AS
BEGIN
	MERGE [dbo].[sales_by_database] AS target
	USING [stage].[sales_by_database] AS source
		ON (target.SHOP_ID = source.SHOP_ID 
		AND target.PROD_ID = source.PROD_ID
		AND target.REP_DATE = source.REP_DATE
		AND target.DISC = source.DISC)
	WHEN MATCHED
		THEN UPDATE
			SET
				PRICE = source.PRICE,     
				QTY = source.QTY,     
				SUM = source.SUM,     
				DISC_PRICE = source.DISC_PRICE,     
				DISC_SUM = source.DISC_SUM
	WHEN NOT MATCHED
		THEN INSERT 
			VALUES
			(
			   source.[PROD_ID]
			  ,source.[REP_DATE]
			  ,source.[PRICE]
			  ,source.[QTY]
			  ,source.[SUM]
		      ,source.[DISC]
			  ,source.[DISC_PRICE]
              ,source.[DISC_SUM]
			  ,source.[SHOP_ID]
			);
TRUNCATE TABLE [stage].[sales_by_database]
END')