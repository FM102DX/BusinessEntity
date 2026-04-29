WITH blocks AS (
    SELECT c."BusinessEntityId", e."Name", c."SortOrder", c."CharCount", c."BlockCount", LENGTH(COALESCE(block->>'html','')) AS html_len
    FROM "BusinessEntityDataChunks" c
    LEFT JOIN "BusinessEntities" e ON e."Id" = c."BusinessEntityId"
    CROSS JOIN LATERAL jsonb_array_elements((c."Data"::jsonb -> 'payload' -> 'blocks')) AS block
)
SELECT "BusinessEntityId", "Name",
       COUNT(DISTINCT "SortOrder") AS chunk_count,
       MIN("CharCount") AS min_chars_per_chunk,
       MAX("CharCount") AS max_chars_per_chunk,
       ROUND(AVG("CharCount")::numeric, 2) AS avg_chars_per_chunk_repeated,
       ROUND(AVG(html_len)::numeric, 2) AS avg_html_chars_per_block,
       MIN(html_len) AS min_html_chars_per_block,
       MAX(html_len) AS max_html_chars_per_block
FROM blocks
GROUP BY "BusinessEntityId", "Name";
