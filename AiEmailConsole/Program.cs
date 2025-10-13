using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Search;
using MimeKit;

class Program
{
    static async Task Main()
    {
        string emailAddress = "testeraiapp218@gmail.com";
        string appPassword = Environment.GetEnvironmentVariable("EMAIL_APP_PASSWORD") ?? "";

        using var imap = new ImapClient();
        imap.Connect("imap.gmail.com", 993, true);
        imap.Authenticate(emailAddress, appPassword);

        var inbox = imap.Inbox;
        inbox.Open(MailKit.FolderAccess.ReadWrite);

        Console.WriteLine("Live-mail assistent kører... Tryk Ctrl+C for at stoppe.");

        while (true)
        {
            Console.WriteLine("Tjekker efter nye mails ...");
            inbox.Check();
            var uids = inbox.Search(SearchQuery.NotSeen);
            foreach (var uid in uids)
            {
                var message = inbox.GetMessage(uid);

                // Vis afsender og emne
                var from = message.From.ToString();
                var subject = message.Subject ?? "(intet emne)";
                Console.WriteLine($"Mail fra: {from}");
                Console.WriteLine($"    Emne: {subject}");
                Console.WriteLine(new string('-', 50));


                string emailText = message.TextBody ?? "";
                string feedback = "";

                var payload = JsonSerializer.Serialize(new { email_text = emailText, feedback });
                var reply = await RunPythonAsync("../ai_engine.py", payload);

                Console.WriteLine("\n--- Ny mail ---");
                Console.WriteLine(emailText);

                Console.WriteLine("\n--- Forslag fra AI ---\n");
                Console.WriteLine(reply);

                bool skipMail = false;

                while (true)
                {
                    Console.Write("\nGodkend og send? (J/N/Spring over): ");
                    var confirm = Console.ReadLine()?.Trim().ToLower();

                    if (confirm == "j" || confirm == "ja") break;
                    else if (confirm == "n" || confirm == "nej")
                    {
                        Console.Write("Hvad skal forbedres? ");
                        feedback = Console.ReadLine() ?? "";
                        payload = JsonSerializer.Serialize(new { email_text = emailText, feedback });
                        reply = await RunPythonAsync("../ai_engine.py", payload);
                        Console.WriteLine("\n--- Opdateret forslag fra AI ---\n");
                        Console.WriteLine(reply);
                    }
                    else if (confirm == "spring over")
                    {
                        Console.WriteLine("Mail springes over.");
                        skipMail = true;
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Skriv J for ja, N for nej eller spring over.");
                    }
                }

                // Send svar via SMTP
                if(!skipMail)
                {
                var response = new MimeMessage();
                response.From.Add(MailboxAddress.Parse(emailAddress));
                response.To.Add(message.From.Mailboxes.First());
                response.Subject = "Re: " + message.Subject;
                response.Body = new TextPart("plain") { Text = reply };

                using var smtp = new SmtpClient();
                smtp.Connect("smtp.gmail.com", 587, false);
                smtp.Authenticate(emailAddress, appPassword);
                smtp.Send(response);
                smtp.Disconnect(true);
                }
                else
                {
                    Console.WriteLine("Mail blev ikke sendt.");
                }

                // Markér som læst
                inbox.AddFlags(uid, MessageFlags.Seen, true);

                // Log
                var logEntry = $"=== {DateTime.Now} === \nFra: {message.From}\nEmne: {message.Subject}\n";
                logEntry += skipMail
                    ? "Mail blev sprunget over.\n\n"
                    : $"Email: {emailText}\nAI-svar: {reply}\nFeedback: {feedback}\n\n";
                System.IO.File.AppendAllText("log.txt", logEntry);
            }

            // Vent 30 sekunder før næste tjek
            await Task.Delay(30000);
        }

        // Denne linje sker først når programmet stoppes manuelt
        // imap.Disconnect(true);
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