SELECT "sensor_id",
    CONCAT(
        'From ', 
        DATE_FORMAT(MIN("time"), '%Y-%m-%d %H:%i:%s'), 
        ' to ', 
        DATE_FORMAT(MAX("time"), '%Y-%m-%d %H:%i:%s')
    ) AS reading_range,
    MAX("time") - MIN("time") AS total_duration
FROM "accepted_readings"
WHERE "area_id" = 'b3f4fb84-bf17-5522-a5f3-70fd1212f381'
GROUP BY "sensor_id";