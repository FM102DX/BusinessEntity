SELECT "SortOrder",
       "CharCount",
       "BlockCount",
       ROUND(CASE WHEN "BlockCount" > 0 THEN "CharCount"::numeric / "BlockCount" ELSE 0 END, 2) AS avg_chars_per_block
FROM public."BusinessEntityDataChunks"
WHERE "BusinessEntityId" = '418feb2d-0819-472f-8ac8-4c9ca3858773'
ORDER BY "CharCount" DESC
LIMIT 15;
