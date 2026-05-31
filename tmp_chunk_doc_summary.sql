SELECT c."BusinessEntityId", e."Name", COUNT(*) AS chunk_count, SUM(c."CharCount") AS total_chars, SUM(c."BlockCount") AS total_blocks, ROUND(AVG(c."CharCount")::numeric, 2) AS avg_chars_per_chunk
FROM "BusinessEntityDataChunks" c
LEFT JOIN "BusinessEntities" e ON e."Id" = c."BusinessEntityId"
GROUP BY c."BusinessEntityId", e."Name"
ORDER BY chunk_count DESC;
