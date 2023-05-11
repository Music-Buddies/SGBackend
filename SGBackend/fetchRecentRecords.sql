select m."Title", pr."PlayedAt", pr."PlayedSeconds", pr."UserId"
from "PlaybackRecords" pr inner join "Media" m on m."Id" = pr."MediumId"
where pr."UserId" = '547869ef-d571-48ee-b744-5a20f4669e97'
order by pr."PlayedAt" desc
