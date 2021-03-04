IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeReceipts'
            AND type = 'P'
      )
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeReceipts]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeReceipts] AS
BEGIN
WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					SHOP_ID,
					CODE,
					PRICE,
					OPER,
					KASSA,
					DATE,
					TIME,
					BAR,
					DISC
				ORDER BY 
					SHOP_ID,
					CODE,
					PRICE,
					OPER,
					KASSA,
					DATE,
					TIME,
					BAR,
					DISC

		) rnk FROM [stage].[receipts]
	)
DELETE FROM CTE
WHERE rnk > 1
BEGIN TRY
	BEGIN TRANSACTION
		MERGE [dbo].[receipts] AS target
			USING [stage].[receipts] AS source
				ON (target.SHOP_ID = source.SHOP_ID 
				AND target.CODE = source.CODE
				AND target.KASSA = source.KASSA
				AND target.DATE = source.DATE
				AND target.TIME = source.TIME
				AND target.PRICE = source.PRICE
				AND target.OPER = source.OPER
				AND target.DISC = source.DISC
				AND target.BAR = source.BAR)
			WHEN NOT MATCHED
				THEN INSERT 
					VALUES
					(
					   source.[LINE_NUM]
					  ,source.[SHOP_ID]
					  ,source.[CODE]
					  ,source.[ID_MAG]
					  ,source.[PRICE]
					  ,source.[WEIGHT]
					  ,source.[OPER]
					  ,source.[KASSA]
					  ,source.[DATE]
					  ,source.[TIME]
					  ,source.[BAR]
					  ,source.[DEP]
					  ,source.[DISC]
					  ,source.[REC]
					  ,source.[Z]
					  ,source.[CLERK]
					  ,source.[CARD]
					  ,source.[PAYMENT]
					  ,source.[PAYTYPE]
					  ,source.[M_INPUT]
					  ,source.[CS]
					);
		TRUNCATE TABLE [stage].[receipts]
	COMMIT TRANSACTION
END TRY
BEGIN CATCH
	IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION
	INSERT INTO [service].[stage_errors] (STAGE_PROC, PROBLEM, DATE_TIME) VALUES (''sp_MergeReceipts'',
		ERROR_MESSAGE(), CURRENT_TIMESTAMP)
END CATCH
END')