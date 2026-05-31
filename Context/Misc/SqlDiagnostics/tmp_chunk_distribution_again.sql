SELECT CASE
         WHEN "CharCount" < 1000 THEN '<1000'
         WHEN "CharCount" < 2000 THEN '1000-1999'
         WHEN "CharCount" < 4000 THEN '2000-3999'
         WHEN "CharCount" < 8000 THEN '4000-7999'
         WHEN "CharCount" < 12000 THEN '8000-11999'
         WHEN "CharCount" < 16000 THEN '12000-15999'
         ELSE '16000+'
       END AS bucket,
       COUNT(*) AS chunks,
       MIN("CharCount") AS min_chars,
       MAX("CharCount") AS max_chars,
       ROUND(AVG("CharCount")::numeric,2) AS avg_chars
FROM public."BusinessEntityDataChunks"
GROUP BY 1
ORDER BY MIN("CharCount");
