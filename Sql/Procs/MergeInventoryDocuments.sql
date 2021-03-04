IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeInventoryDocuments'
            AND type = 'P'
      )
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeInventoryDocuments]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeInventoryDocuments] AS
BEGIN
	WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					SHOP_ID,
					DOC_ID,
					DOC_TYPE,
					DATE,
					PROD_ID
				ORDER BY 
					SHOP_ID,
					DOC_ID,
					DOC_TYPE,
					DATE,
					PROD_ID

		) rnk FROM [stage].[inventory_documents]
	)
DELETE FROM CTE
WHERE rnk > 1
BEGIN TRY
	BEGIN TRANSACTION
		MERGE [dbo].[inventory_documents] AS target
		USING [stage].[inventory_documents] AS source
			ON (target.SHOP_ID = source.SHOP_ID 
			AND target.DOC_ID = source.DOC_ID
			AND target.DOC_TYPE = source.DOC_TYPE
			AND target.DATE = source.DATE
			AND target.PROD_ID = source.PROD_ID)
		WHEN MATCHED
			THEN UPDATE
				SET
					PRICE = source.PRICE,     
					QTY_CALC = source.QTY_CALC,     
					QTY_FACT = source.QTY_FACT,				
					CS = source.CS
		WHEN NOT MATCHED
			THEN INSERT 
				VALUES
				(
				   source.[SHOP_ID]
				  ,source.[DOC_ID]
				  ,source.[DOC_TYPE]
				  ,source.[DATE]
				  ,source.[PROD_ID]
				  ,source.[PRICE]
				  ,source.[QTY_CALC]
				  ,source.[QTY_FACT]
				  ,source.[RSN_ID]
				  ,source.[FIO]
				  ,source.[TAB_NUM]
				  ,source.[CS]
				  ,source.[ALC_CODE]
				);
		TRUNCATE TABLE [stage].[inventory_documents]
	COMMIT TRANSACTION
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION
	INSERT INTO [service].[stage_errors] (STAGE_PROC, PROBLEM, DATE_TIME) VALUES (''sp_MergeInventoryDocuments'',
		ERROR_MESSAGE(), CURRENT_TIMESTAMP)
END CATCH
END')