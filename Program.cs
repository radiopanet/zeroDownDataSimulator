using System;
using Npgsql;


string connString = "Host=localhost;Username=postgres;Password=123456;Database=zero_down_db";

using var conn = new NpgsqlConnection(connString);
conn.Open();

Random rand = new Random();

while(true) // continuous simulation loop
{
    for(int parameterId = 0; parameterId <= 9; parameterId++)
    {
        // Fetch threshold info for this parameter
        using var thresholdCmd = new NpgsqlCommand(
            "SELECT equipment_id, threshold_min, threshold_max FROM parameters WHERE parameter_id=@pid", conn);

        thresholdCmd.Parameters.AddWithValue("pid", parameterId);

        using var reader = thresholdCmd.ExecuteReader();
        if(!reader.Read()) continue;

        int equipmentId = reader.GetInt32(0);
        decimal? thresholdMin = reader.IsDBNull(1) ? null : reader.GetDecimal(1);
        decimal? thresholdMax = reader.IsDBNull(2) ? null : reader.GetDecimal(2);
        reader.Close();

        //Generate random value (adjust ranges per parameter for realism)
        double value = rand.NextDouble() * 120;   

        //Insert into readings
        using var insertCmd = new NpgsqlCommand(
            "INSERT INTO readings (equipment_id, parameter_id, reading_value, recorded_at) " +
                    "VALUES (@eid, @pid, @val, NOW())",
                    conn);

        insertCmd.Parameters.AddWithValue("eid", equipmentId);
        insertCmd.Parameters.AddWithValue("pid", parameterId);
        insertCmd.Parameters.AddWithValue("val", value);
        insertCmd.ExecuteNonQuery();
        
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Inserted reading for parameter {parameterId}: {value}");

         // Check thresholds and insert alert if breached
        bool alertTriggered = false;
        string severity = "Warning";
        string message = "";  

        if (thresholdMin.HasValue && value < (double)thresholdMin.Value)
        {
            alertTriggered = true;
            severity = "Critical";
            message = $"Value {value} below minimum threshold {thresholdMin}";
        }
        else if (thresholdMax.HasValue && value > (double)thresholdMax.Value)
        {
            alertTriggered = true;
            severity = "Critical";
            message = $"Value {value} above maximum threshold {thresholdMax}";
        }

        if (alertTriggered)
        {
            using var alertCmd = new NpgsqlCommand(
                    "INSERT INTO alerts (equipment_id, parameter_id, severity, message, triggered_at, resolved) " +
                    "VALUES (@eid, @pid, @sev, @msg, NOW(), FALSE)",
                    conn);
            alertCmd.Parameters.AddWithValue("eid", equipmentId);
            alertCmd.Parameters.AddWithValue("pid", parameterId);
            alertCmd.Parameters.AddWithValue("sev", severity);
            alertCmd.Parameters.AddWithValue("msg", message);
            alertCmd.ExecuteNonQuery();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ALERT: {message}");
        }

    }
    Thread.Sleep(5000);
}