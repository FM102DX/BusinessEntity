WITH block_stats AS (
    SELECT c."Id", c."BusinessEntityId", c."SortOrder", c."CharCount", c."BlockCount",
           ROUND(AVG(LENGTH(COALESCE(block->>'html','')))::numeric, 2) AS avg_html_chars_per_block,
           COUNT(*) FILTER (WHERE COALESCE(block->>'html','') <> '') AS html_blocks
    FROM "BusinessEntityDataChunks" c
    CROSS JOIN LATERAL jsonb_array_elements((c."Data"::jsonb -> 'payload' -> 'blocks')) AS block
    GROUP BY c."Id", c."BusinessEntityId", c."SortOrder", c."CharCount", c."BlockCount"
)
SELECT bs."BusinessEntityId", e."Name", bs."SortOrder", bs."CharCount", bs."BlockCount", bs.html_blocks, bs.avg_html_chars_per_block
FROM block_stats bs
LEFT JOIN "BusinessEntities" e ON e."Id" = bs."BusinessEntityId"
ORDER BY e."Name", bs."SortOrder"
LIMIT 120;
