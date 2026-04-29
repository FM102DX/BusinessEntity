SELECT "SortOrder",
       "CharCount",
       "BlockCount",
       ROUND(CASE WHEN "BlockCount" > 0 THEN "CharCount"::numeric / "BlockCount" ELSE 0 END, 2) AS avg_chars_per_block
FROM public."BusinessEntityDataChunks"
WHERE "BusinessEntityId" = 'd5c25353-80a9-4d93-9726-acb23b34e059'
ORDER BY "SortOrder"
LIMIT 20;
