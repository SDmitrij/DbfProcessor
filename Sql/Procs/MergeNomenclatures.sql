IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeNomenclatures'
            AND type = 'P')
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeNomenclatures]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeNomenclatures] AS
BEGIN
WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					G_ID,
					GO_CODE,
					GO_1CCODE
				ORDER BY 
					G_ID,
					GO_CODE,
					GO_1CCODE
		) rnk FROM [stage].[nomenclatures]
	)
DELETE FROM CTE
WHERE rnk > 1

	MERGE [dbo].[nomenclatures] AS target
	USING [stage].[nomenclatures] AS source
		ON (target.G_ID = source.G_ID
		AND target.GO_CODE = source.GO_CODE
		AND target.GO_1CCODE = source.GO_1CCODE)
	WHEN NOT MATCHED
		THEN INSERT 
			VALUES
			(
			  source.G_ID,
			  source.GO_CODE,
			  source.GO_1CCODE,
			  source.GO_NAME,
			  source.TM_NAME,
			  source.TM_1CCODE,
			  source.GO_EXP_DT,
			  source.GO_BRUTTO,
			  source.PRV_LABEL
			);
TRUNCATE TABLE [stage].[nomenclatures]
END')