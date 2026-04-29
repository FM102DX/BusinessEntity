SELECT "SortOrder",
       "CharCount",
       "BlockCount",
       ROUND(CASE WHEN "BlockCount" > 0 THEN "CharCount"::numeric / "BlockCount" ELSE 0 END, 2) AS avg_chars_per_block
FROM public."BusinessEntityDataChunks"
WHERE "BusinessEntityId" = '9f61e955-4590-4cac-87d5-856850b7695a'
ORDER BY "CharCount" DESC
LIMIT 15;
