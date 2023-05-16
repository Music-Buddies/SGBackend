SELECT "Stats"."LatestFetch", "User"."Name" 
FROM public."User"
inner join "Stats" on "Stats"."Id" = "User"."StatsId"
