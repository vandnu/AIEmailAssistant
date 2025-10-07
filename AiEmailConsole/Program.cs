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

        var payload = JsonSerializer.Serialize(new { email_text = email });
        var reply = await RunPythonAsync("../ai_engine.py", payload);

        Console.WriteLine("\n--- Forslag fra AI ---\n");
        Console.WriteLine(reply);

        Console.Write("\nGodkend og send? (J/N): ");
        var confirm = Console.ReadLine()?.Trim().ToLower();
        if (confirm == "j" || confirm == "ja")
            Console.WriteLine("Svar er godkendt og klar til afsendelse.");
        else
            Console.WriteLine("Annulleret.");
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