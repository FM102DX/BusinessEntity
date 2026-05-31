SELECT "Id", "BusinessEntityId", "SortOrder", LEFT("Data", 220) AS data_head, LEFT(COALESCE("PlainText",''), 120) AS plaintext_head
FROM "BusinessEntityDataChunks"
ORDER BY "BusinessEntityId", "SortOrder"
LIMIT 10;
