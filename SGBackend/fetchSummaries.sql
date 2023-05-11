select m."Title", ps."TotalSeconds", ps."LastListened"
from "PlaybackSummaries" ps inner join "Media" m on m."Id" = ps."MediumId"
where ps."UserId" = '547869ef-d571-48ee-b744-5a20f4669e97' 