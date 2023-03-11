SELECT CONVERT(sg.QRTZ_JOB_DETAILS.JOB_DATA USING utf8), TRIGGER_STATE, FROM_UNIXTIME((NEXT_FIRE_TIME/10000000.0) - (62135596800.0)) as Next_Fire_time 
FROM sg.QRTZ_TRIGGERS inner join sg.QRTZ_JOB_DETAILS on sg.QRTZ_TRIGGERS.JOB_NAME = sg.QRTZ_TRIGGERS.JOB_NAME;