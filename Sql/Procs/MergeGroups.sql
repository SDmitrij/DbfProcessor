IF EXISTS (
        SELECT type_desc, type
        FROM sys.procedures WITH(NOLOCK)
        WHERE NAME = 'sp_MergeGroups'
            AND type = 'P')
BEGIN
     EXEC('DROP PROCEDURE [dbo].[sp_MergeGroups]')
END

EXEC('CREATE PROCEDURE [dbo].[sp_MergeGroups] AS
BEGIN
WITH CTE AS 
	(SELECT *, ROW_NUMBER() 
		OVER(
				PARTITION BY
					G_ID
				ORDER BY
					G_ID
		) rnk FROM [stage].[groups]
	)
DELETE FROM CTE
WHERE rnk > 1

	MERGE [dbo].[groups] AS target
	USING [stage].[groups] AS source
		ON (target.G_ID = source.G_ID)
	WHEN NOT MATCHED
		THEN INSERT 
			VALUES
			(
				source.DATE,
				source.G_ID,
				source.G_DIM,
				source.G_G1_DIM,
				source.G_G2_DIM,
				source.G_G3_DIM
			);
TRUNCATE TABLE [stage].[groups]
END')