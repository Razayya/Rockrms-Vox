/* MISSED-START CHECK after a planned restart or upgrade (read-only; safe to run any time).
   From Rock Request 8463 (2026-11-11): six 7:00 am jobs silently skipped during the 19.7 upgrade restart on 11/3.
   Run it SAME DAY, once per outage window - some jobs only look at "today" (see rock-update/SKILL.md gotcha 6).

   Rock does not replay a job whose start time falls while Rock is down (Quartz RAM job store, misfire = do nothing:
   Rock/Model/Core/ServiceJob/ServiceJobService.cs BuildQuartzTrigger, RockWeb/web.config quartz.jobStore.type). So after
   Rock comes back, run this with the outage window and Run Now whatever it lists (if that job still needs to run today).

   Set the window below in ROCK TIME (Eastern), from just before Rock went down to just after it answered again.
   Times in ServiceJobHistory are Rock time too. Same-day windows only.

   Result 1 - fixed-time jobs whose schedule falls in the window and that did not start in it. Read from each job's
              schedule (seconds/minutes/hours fields that are plain numbers). DayMatch says whether today is one of its days:
              'yes', or 'check' when the day fields are something this script doesn't decode (read the schedule).
   Result 2 - cross-check from history: jobs that normally start in this clock window (same weekday last week, or on at
              least 2 of the 6 days before) but did not start in it this time. Catches schedules Result 1 can't decode.
   Result 3 - jobs that run many times a day (every few minutes / hourly). These just catch the next run; nothing to do.
*/
SET NOCOUNT ON;
DECLARE @From datetime = '2026-11-03 06:59:00';   -- <- SET: outage start (Rock time). Example = the 11/3 upgrade restart
DECLARE @To   datetime = '2026-11-03 07:03:00';   -- <- SET: Rock answering again (Rock time)

DECLARE @T1 time(0) = CAST(@From AS time(0)), @T2 time(0) = CAST(@To AS time(0));
DECLARE @Dow char(3) = UPPER(LEFT(DATENAME(weekday, @From), 3));      -- e.g. TUE
DECLARE @DowNum int = CHARINDEX(@Dow, 'SUNMONTUEWEDTHUFRISAT') / 3 + 1; -- SUN=1 .. SAT=7 (Quartz numbering)
DECLARE @Day int = DAY(@From);

IF CAST(@From AS date) <> CAST(@To AS date) OR @To <= @From
BEGIN RAISERROR('Use a same-day window with @To after @From.', 16, 1); RETURN; END

/* each active job's schedule split into its fields */
IF OBJECT_ID('tempdb..#Cron') IS NOT NULL DROP TABLE #Cron;
SELECT j.Id, j.Name, j.CronExpression,
       MAX(CASE WHEN s.ordinal = 1 THEN s.value END) AS Sec,
       MAX(CASE WHEN s.ordinal = 2 THEN s.value END) AS Mins,
       MAX(CASE WHEN s.ordinal = 3 THEN s.value END) AS Hrs,
       MAX(CASE WHEN s.ordinal = 4 THEN s.value END) AS Dom,
       MAX(CASE WHEN s.ordinal = 5 THEN s.value END) AS Mon,
       MAX(CASE WHEN s.ordinal = 6 THEN s.value END) AS Dow
INTO #Cron
FROM ServiceJob j
CROSS APPLY STRING_SPLIT(LTRIM(RTRIM(j.CronExpression)), ' ', 1) s
WHERE j.IsActive = 1
GROUP BY j.Id, j.Name, j.CronExpression;

/* how often each job ran over the 7 days before the window (to spot frequent jobs) */
IF OBJECT_ID('tempdb..#Freq') IS NOT NULL DROP TABLE #Freq;
SELECT h.ServiceJobId AS Id, COUNT(*) / 7.0 AS RunsPerDay
INTO #Freq
FROM ServiceJobHistory h
WHERE h.StartDateTime >= DATEADD(day, -7, CAST(@From AS date)) AND h.StartDateTime < CAST(@From AS date)
GROUP BY h.ServiceJobId;

IF OBJECT_ID('tempdb..#Started') IS NOT NULL DROP TABLE #Started;
SELECT DISTINCT h.ServiceJobId AS Id INTO #Started
FROM ServiceJobHistory h WHERE h.StartDateTime >= @From AND h.StartDateTime < @To;

/* Result 1: fixed-time jobs due in the window */
WITH fixed AS (
  SELECT c.*,
         TIMEFROMPARTS(TRY_CAST(c.Hrs AS int), TRY_CAST(c.Mins AS int), TRY_CAST(c.Sec AS int), 0, 0) AS DueTime,
         CASE
           WHEN c.Mon NOT IN ('*', '?', '1/1') THEN 'check'
           WHEN c.Dom NOT IN ('*', '?', '1/1') AND TRY_CAST(c.Dom AS int) IS NULL THEN 'check'
           WHEN TRY_CAST(c.Dom AS int) IS NOT NULL AND TRY_CAST(c.Dom AS int) <> @Day THEN 'no'
           WHEN c.Dow IN ('*', '?') THEN 'yes'
           WHEN c.Dow = 'MON-FRI' THEN CASE WHEN @DowNum BETWEEN 2 AND 6 THEN 'yes' ELSE 'no' END
           WHEN c.Dow LIKE '%[A-Z]%' AND c.Dow NOT LIKE '%-%' AND c.Dow NOT LIKE '%#%' AND c.Dow NOT LIKE '%L%'
                THEN CASE WHEN ',' + c.Dow + ',' LIKE '%,' + @Dow + ',%' THEN 'yes' ELSE 'no' END
           WHEN TRY_CAST(c.Dow AS int) IS NOT NULL THEN CASE WHEN TRY_CAST(c.Dow AS int) = @DowNum THEN 'yes' ELSE 'no' END
           ELSE 'check'
         END AS DayMatch
  FROM #Cron c
  WHERE TRY_CAST(c.Sec AS int) BETWEEN 0 AND 59 AND TRY_CAST(c.Mins AS int) BETWEEN 0 AND 59 AND TRY_CAST(c.Hrs AS int) BETWEEN 0 AND 23
)
SELECT 'MISSED (schedule)' AS Result, f.Id AS Job, LEFT(f.Name, 50) AS Name, CONVERT(varchar(8), f.DueTime, 108) AS DueAt,
       f.CronExpression, f.DayMatch,
       (SELECT CONVERT(varchar(16), MIN(h.StartDateTime), 120) FROM ServiceJobHistory h
         WHERE h.ServiceJobId = f.Id AND h.StartDateTime >= @To) AS NextStartAfter
FROM fixed f
WHERE f.DueTime >= @T1 AND f.DueTime < @T2 AND f.DayMatch IN ('yes', 'check')
  AND NOT EXISTS (SELECT 1 FROM #Started s WHERE s.Id = f.Id)
ORDER BY f.DueTime, f.Id;

/* Result 2: history cross-check */
WITH prior AS (
  SELECT h.ServiceJobId AS Id,
         COUNT(DISTINCT CAST(h.StartDateTime AS date)) AS PriorDaysInClockWindow,
         MAX(CASE WHEN CAST(h.StartDateTime AS date) = DATEADD(day, -7, CAST(@From AS date)) THEN 1 ELSE 0 END) AS SameWeekdayLastWeek
  FROM ServiceJobHistory h
  WHERE h.StartDateTime >= DATEADD(day, -7, CAST(@From AS date)) AND h.StartDateTime < CAST(@From AS date)
    AND CAST(h.StartDateTime AS time(0)) >= @T1 AND CAST(h.StartDateTime AS time(0)) < @T2
  GROUP BY h.ServiceJobId
)
SELECT 'MISSED (history)' AS Result, p.Id AS Job, LEFT(j.Name, 50) AS Name, j.CronExpression, p.PriorDaysInClockWindow, p.SameWeekdayLastWeek
FROM prior p JOIN ServiceJob j ON j.Id = p.Id AND j.IsActive = 1
LEFT JOIN #Freq fq ON fq.Id = p.Id
WHERE (p.SameWeekdayLastWeek = 1 OR p.PriorDaysInClockWindow >= 2)
  AND ISNULL(fq.RunsPerDay, 0) < 6
  AND NOT EXISTS (SELECT 1 FROM #Started s WHERE s.Id = p.Id)
ORDER BY p.Id;

/* Result 3: frequent jobs - they resume on their next run by themselves */
SELECT 'frequent - next run catches up' AS Result, COUNT(*) AS Jobs,
       STRING_AGG(CAST(j.Id AS varchar(10)), ', ') WITHIN GROUP (ORDER BY j.Id) AS JobIds
FROM #Freq fq JOIN ServiceJob j ON j.Id = fq.Id AND j.IsActive = 1
WHERE fq.RunsPerDay >= 6;

SELECT 'window' AS Info, CONVERT(varchar(19), @From, 120) AS [From], CONVERT(varchar(19), @To, 120) AS [To], @Dow AS Weekday,
       (SELECT COUNT(*) FROM #Started) AS JobsThatStartedInWindow;
