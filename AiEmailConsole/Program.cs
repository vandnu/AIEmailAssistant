using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Skriv en mail (test): ");
        var email = Console.ReadLine() ?? "";

        var payload = JsonSerializer.Serialize(new { email_text = email, feedback = "" });
        var reply = await RunPythonAsync("../ai_engine.py", payload);

        // Log originalt forslag
        int iteration = 0;
        System.IO.File.AppendAllText("log.txt",
            $"=== {DateTime.Now} | Iteration {iteration} ===\nEmail: {email}\nAI-svar: {reply}\nFeedback: {""}\n\n");

        Console.WriteLine("\n--- Forslag fra AI ---\n");
        Console.WriteLine(reply);
        
        string feedback ="";
        while (true)
        {
             Console.Write("\nGodkend og send? (J/N): ");
             var confirm = Console.ReadLine()?.Trim().ToLower();

             if (confirm == "j" || confirm == "ja")
             {
                Console.WriteLine("Svar er godkendt og klar til afsendelse.");
                break;
             }
             else if (confirm == "n" || confirm == "nej")
             {
                Console.Write("Hvad skal forbedres? ");
                feedback = Console.ReadLine() ?? "";

                var newPayload = JsonSerializer.Serialize(new { email_text = email, feedback = feedback });
                var newReply = await RunPythonAsync("../ai_engine.py", newPayload);

                // Log opdateret forslag
                iteration++;
                System.IO.File.AppendAllText("log.txt",
                    $"=== {DateTime.Now} | Iteration {iteration} ===\nEmail: {email}\nAI-svar: {newReply}\nFeedback: {feedback}\n\n");

                Console.WriteLine("\n--- Opdateret forslag fra AI ---\n");
                Console.WriteLine(newReply);
             }
             else
             {
                Console.WriteLine("Skriv J for ja eller N for nej.");
             }
        }
    }        

    static async Task<string> RunPythonAsync(string scriptPath, string inputJson)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python3",
            Arguments = scriptPath,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new Exception("Kunne ikke starte Python.");
        await process.StandardInput.WriteAsync(inputJson);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync();
        process.WaitForExit();

        try
        {
            var doc = JsonDocument.Parse(output);
            if (doc.RootElement.TryGetProperty("reply", out var r))
                return r.GetString() ?? "";
            if (doc.RootElement.TryGetProperty("error", out var e))
                return "Fejl: " + e.GetString();
        }
        catch
        {
            return "Kunne ikke parse output: " + output;
        }

        return "Ukendt fejl.";
    }
}