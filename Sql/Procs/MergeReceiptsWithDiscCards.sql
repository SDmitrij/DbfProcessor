IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeReceiptsWithDiscCards'
            AND type = 'P'
      )
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeReceiptsWithDiscCards]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeReceiptsWithDiscCards] AS
BEGIN
WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					DATE,
					TIME,
					SHOP_ID,
					REC,
					OPER,
					KASSA,
					PAYTYPE,
					CODE,
					DISC_PARAM
				ORDER BY 
					DATE,
					TIME,
					SHOP_ID,
					REC,
					OPER,
					KASSA,
					PAYTYPE,
					CODE,
					DISC_PARAM

		) rnk FROM [stage].[receipts_with_discount_cards]
	)
DELETE FROM CTE
WHERE rnk > 1

	MERGE [dbo].[receipts_with_discount_cards] AS target
		USING [stage].[receipts_with_discount_cards] AS source
			ON (target.DATE = source.DATE
			AND target.TIME = source.TIME
			AND target.SHOP_ID = source.SHOP_ID
			AND target.REC = source.REC
			AND target.OPER = source.OPER
			AND target.KASSA = source.KASSA
			AND target.PAYTYPE = source.PAYTYPE
			AND target.CODE = source.CODE
			AND target.DISC_PARAM = source.DISC_PARAM)
		WHEN NOT MATCHED
			THEN INSERT 
				VALUES
				(
				   source.DATE,
				   source.TIME,
				   source.SHOP_ID,
				   source.REC,
				   source.OPER,
				   source.KASSA,
				   source.PAYTYPE,
				   source.CODE,
				   source.QTY,
				   source.SUM,
				   source.DISC_SUM,
				   source.DISC_PARAM,
				   source.DISC_TYPE,
				   source.CARD_TYPE,
				   source.CARD_ID
				);
TRUNCATE TABLE [stage].[receipts_with_discount_cards]
END')