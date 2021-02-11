IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeRevaluationDocuments'
            AND type = 'P'
      )
BEGIN
	EXEC('DROP PROCEDURE [dbo].[sp_MergeRevaluationDocuments]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeRevaluationDocuments] AS
BEGIN
	WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					SHOP_ID,
					DOC_ID,
					DOC_DATE,
					PROD_ID

				ORDER BY 
					SHOP_ID,
					DOC_ID,
					DOC_DATE,
					PROD_ID
					

		) rnk FROM [stage].[revaluation_documents]
	)
DELETE FROM CTE
WHERE rnk > 1

	MERGE [dbo].[revaluation_documents] AS target
	USING [stage].[revaluation_documents] AS source
		ON (target.SHOP_ID = source.SHOP_ID 
		AND target.DOC_ID = source.DOC_ID
		AND target.DOC_DATE = source.DOC_DATE
		AND target.PROD_ID = source.PROD_ID)
	WHEN MATCHED
	THEN UPDATE
		SET
			QTY = source.QTY,
			OLD_PRICE = source.OLD_PRICE,
			NEW_PRICE = source.NEW_PRICE,
			EVENT = source.EVENT,
			COMMENT = source.COMMENT,
			CS = source.CS

	WHEN NOT MATCHED
		THEN INSERT 
			VALUES
			(
			   source.[SHOP_ID]
			  ,source.[DOC_ID]
			  ,source.[DOC_DATE]
              ,source.[PROD_ID]
              ,source.[QTY]
              ,source.[OLD_PRICE]
              ,source.[NEW_PRICE]
              ,source.[EVENT]
              ,source.[COMMENT]
              ,source.[CS]
			);
TRUNCATE TABLE [stage].[revaluation_documents]
END')