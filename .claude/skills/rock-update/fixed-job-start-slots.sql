SET NOCOUNT ON; SET ANSI_WARNINGS OFF;
-- Fixed daily/weekly start times of active jobs (read-only): which clock slots to keep a planned restart or upgrade away from.
-- From Rock Request 8463 (2026-11-11). Every-N-minute and hourly-range jobs are not listed - they catch their next tick.
IF OBJECT_ID('tempdb..#C') IS NOT NULL DROP TABLE #C;
SELECT j.Id, j.Name, j.CronExpression,
       MAX(CASE WHEN s.ordinal = 1 THEN s.value END) AS Sec, MAX(CASE WHEN s.ordinal = 2 THEN s.value END) AS Mins,
       MAX(CASE WHEN s.ordinal = 3 THEN s.value END) AS Hrs, MAX(CASE WHEN s.ordinal = 6 THEN s.value END) AS Dow,
       MAX(CASE WHEN s.ordinal = 4 THEN s.value END) AS Dom
INTO #C FROM ServiceJob j CROSS APPLY STRING_SPLIT(LTRIM(RTRIM(j.CronExpression)), ' ', 1) s
WHERE j.IsActive = 1 GROUP BY j.Id, j.Name, j.CronExpression;

SELECT COUNT(*) AS ActiveJobs,
       SUM(CASE WHEN TRY_CAST(Hrs AS int) IS NOT NULL AND TRY_CAST(Mins AS int) IS NOT NULL THEN 1 ELSE 0 END) AS FixedTimeJobs
FROM #C;

-- slots with fixed starts, busiest first
SELECT RIGHT('0' + Hrs, 2) + ':' + RIGHT('0' + Mins, 2) AS Slot, COUNT(*) AS Jobs,
       STRING_AGG(CAST(Id AS varchar(10)) + CASE WHEN Dow NOT IN ('*','?') THEN ' (' + Dow + ')' WHEN Dom NOT IN ('*','?','1/1') THEN ' (day ' + Dom + ')' ELSE '' END, ', ') WITHIN GROUP (ORDER BY Id) AS JobIds
FROM #C WHERE TRY_CAST(Hrs AS int) IS NOT NULL AND TRY_CAST(Mins AS int) IS NOT NULL
GROUP BY Hrs, Mins ORDER BY COUNT(*) DESC, TRY_CAST(Hrs AS int), TRY_CAST(Mins AS int);

-- fixed starts by hour (all 24)
SELECT TRY_CAST(Hrs AS int) AS HourET, COUNT(*) AS FixedStarts
FROM #C WHERE TRY_CAST(Hrs AS int) IS NOT NULL AND TRY_CAST(Mins AS int) IS NOT NULL
GROUP BY TRY_CAST(Hrs AS int) ORDER BY 1;
