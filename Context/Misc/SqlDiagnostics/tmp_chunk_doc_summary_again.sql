SELECT c."BusinessEntityId",
       e."Name",
       COUNT(*) AS chunk_count,
       SUM(c."CharCount") AS total_chars,
       ROUND(AVG(c."CharCount")::numeric,2) AS avg_chars_per_chunk,
       MIN(c."CharCount") AS min_chars_per_chunk,
       MAX(c."CharCount") AS max_chars_per_chunk
FROM public."BusinessEntityDataChunks" c
LEFT JOIN public."BusinessEntities" e ON e."Id" = c."BusinessEntityId"
GROUP BY c."BusinessEntityId", e."Name"
ORDER BY chunk_count DESC;
