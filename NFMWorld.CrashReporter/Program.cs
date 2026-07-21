using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using NFMWorld.CrashReporter;
using NFMWorld.Sentry;

var dsn = args[0];
var release = args[1];
var eventId = args[2];
var errorInfo = args[3];

SentrySdk.Init(options =>
{
    options.Dsn = dsn;
    options.Debug = true;
    options.TracesSampleRate = 0;
    options.Release = release;
});

var errorMessage = $"NFM World has encountered an unexpected error ({errorInfo}).";
var errorDetails = "Please help us improve by submitting a crash report with details about what you were doing when the error occurred.";

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    var report = await CrashReportDialog.ShowWindowsDialog(
        errorMessage,
        errorDetails
    );

    if (report.Submitted)
    {
        SentrySdk.CaptureFeedback(new SentryFeedback
        {
            Comments = report.Description,
            Email = report.Email,
            Name = report.UserName,
            EventId = SentryEventId.Parse(eventId)
        });
    }
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    var report = await CrashReportDialog.ShowMacDialog(
        errorMessage,
        errorDetails
    );

    if (report.Submitted)
    {
        SentrySdk.CaptureFeedback(new SentryFeedback
        {
            Comments = report.Description,
            Email = report.Email,
            Name = report.UserName,
            EventId = SentryEventId.Parse(eventId)
        });
    }
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    var report = await CrashReportDialog.ShowLinuxDialog(
        errorMessage,
        errorDetails
    );

    if (report.Submitted)
    {
        SentrySdk.CaptureFeedback(new SentryFeedback
        {
            Comments = report.Description,
            Email = report.Email,
            Name = report.UserName,
            EventId = SentryEventId.Parse(eventId)
        });
    }
}

await SentrySdk.FlushAsync(TimeSpan.FromSeconds(60));

namespace NFMWorld.CrashReporter
{
    internal class CrashReportResult
    {
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Description { get; set; } = "";
        public bool Submitted { get; set; }
    }

    internal class CrashReportDialog
    {
        // Windows implementation using Win32 TaskDialog
        public static async Task<CrashReportResult> ShowWindowsDialog(string errorMessage, string errorDetails)
        {
            var result = new CrashReportResult();
            
            // Create a temporary PowerShell script for the dialog
            string script = CreateWindowsInputDialogScript(errorMessage, errorDetails);
            string scriptPath = Path.GetTempFileName() + ".ps1";
            
            try
            {
                File.WriteAllText(scriptPath, script, Encoding.UTF8);
                
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi)!;
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                ParseOutput(output, result);
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                Console.WriteLine("Error report dialog failed. Please report this error manually:");
                Console.WriteLine(errorMessage);
                Console.WriteLine(errorDetails);
            }
            finally
            {
                if (File.Exists(scriptPath))
                    File.Delete(scriptPath);
            }

            return result;
        }

        private static string CreateWindowsInputDialogScript(string errorMessage, string errorDetails)
        {
            return
                """
                Add-Type -AssemblyName System.Windows.Forms
                Add-Type -AssemblyName System.Drawing

                $form = New-Object System.Windows.Forms.Form
                $form.Text = 'Crash Reporter'
                $form.Size = New-Object System.Drawing.Size(500,450)
                $form.StartPosition = 'CenterScreen'
                $form.FormBorderStyle = 'FixedDialog'
                $form.MaximizeBox = $false
                $form.MinimizeBox = $false

                # Error message
                $label = New-Object System.Windows.Forms.Label
                $label.Location = New-Object System.Drawing.Point(15,15)
                $label.Size = New-Object System.Drawing.Size(450,60)
                $label.Text = 'The application has encountered an error. Please help us improve by submitting a crash report.'
                $form.Controls.Add($label)

                # User Name
                $nameLabel = New-Object System.Windows.Forms.Label
                $nameLabel.Location = New-Object System.Drawing.Point(15,85)
                $nameLabel.Size = New-Object System.Drawing.Size(100,25)
                $nameLabel.Text = 'Your Name:'
                $form.Controls.Add($nameLabel)

                $nameBox = New-Object System.Windows.Forms.TextBox
                $nameBox.Location = New-Object System.Drawing.Point(120,85)
                $nameBox.Size = New-Object System.Drawing.Size(345,25)
                $form.Controls.Add($nameBox)

                # Email
                $emailLabel = New-Object System.Windows.Forms.Label
                $emailLabel.Location = New-Object System.Drawing.Point(15,120)
                $emailLabel.Size = New-Object System.Drawing.Size(100,25)
                $emailLabel.Text = 'Email:'
                $form.Controls.Add($emailLabel)

                $emailBox = New-Object System.Windows.Forms.TextBox
                $emailBox.Location = New-Object System.Drawing.Point(120,120)
                $emailBox.Size = New-Object System.Drawing.Size(345,25)
                $form.Controls.Add($emailBox)

                # Description
                $descLabel = New-Object System.Windows.Forms.Label
                $descLabel.Location = New-Object System.Drawing.Point(15,155)
                $descLabel.Size = New-Object System.Drawing.Size(450,25)
                $descLabel.Text = 'What were you doing when the error occurred?'
                $form.Controls.Add($descLabel)

                $descBox = New-Object System.Windows.Forms.TextBox
                $descBox.Location = New-Object System.Drawing.Point(15,180)
                $descBox.Size = New-Object System.Drawing.Size(450,130)
                $descBox.Multiline = $true
                $descBox.ScrollBars = 'Vertical'
                $form.Controls.Add($descBox)

                # Buttons
                $submitButton = New-Object System.Windows.Forms.Button
                $submitButton.Location = New-Object System.Drawing.Point(280,360)
                $submitButton.Size = New-Object System.Drawing.Size(90,25)
                $submitButton.Text = 'Submit'
                $submitButton.DialogResult = [System.Windows.Forms.DialogResult]::OK
                $form.Controls.Add($submitButton)

                $cancelButton = New-Object System.Windows.Forms.Button
                $cancelButton.Location = New-Object System.Drawing.Point(375,360)
                $cancelButton.Size = New-Object System.Drawing.Size(90,25)
                $cancelButton.Text = 'Cancel'
                $cancelButton.DialogResult = [System.Windows.Forms.DialogResult]::Cancel
                $form.Controls.Add($cancelButton)

                $form.AcceptButton = $submitButton
                $form.CancelButton = $cancelButton

                $result = $form.ShowDialog()

                if ($result -eq [System.Windows.Forms.DialogResult]::OK) {
                    Write-Output "SUBMITTED"
                    Write-Output "NAME:$($nameBox.Text)"
                    Write-Output "EMAIL:$($emailBox.Text)"
                    Write-Output "DESCRIPTION:$($descBox.Text)"
                } else {
                    Write-Output "CANCELLED"
                }
                """;
        }

        // Mac implementation using osascript
        public static async Task<CrashReportResult> ShowMacDialog(string errorMessage, string errorDetails)
        {
            var result = new CrashReportResult();

            try
            {
                // First ask if user wants to submit a report
                string askScript = 
                    $$"""
                      tell application "System Events"
                          display dialog "{{EscapeAppleScript(errorMessage)}}" & return & return & "Would you like to submit a crash report?" buttons {"Cancel", "Submit Report"} default button "Submit Report" with icon caution with title "Application Error"
                          if button returned of result is "Submit Report" then
                              return "continue"
                          else
                              return "cancel"
                          end if
                      end tell
                      """;

                string response = await RunOsascript(askScript);
                if (!response.Trim().Contains("continue"))
                {
                    result.Submitted = false;
                    return result;
                }

                // Get user name
                string nameScript =
                    """
                    tell application "System Events"
                        display dialog "Please enter your name:" default answer "" with title "Crash Reporter"
                        return text returned of result
                    end tell
                    """;

                result.UserName = await RunOsascript(nameScript);

                // Get email
                string emailScript = 
                    """
                    tell application "System Events"
                        display dialog "Please enter your email address:" default answer "" with title "Crash Reporter"
                        return text returned of result
                    end tell
                    """;

                result.Email = await RunOsascript(emailScript);

                // Get description
                string descScript = 
                    """
                    tell application "System Events"
                        display dialog "What were you doing when the error occurred?" default answer "" with title "Crash Reporter"
                        return text returned of result
                    end tell
                    """;

                result.Description = await RunOsascript(descScript);
                result.Submitted = !string.IsNullOrWhiteSpace(result.Description);
            }
            catch
            {
                result.Submitted = false;
            }

            return result;
        }

        // Linux implementation using zenity or kdialog
        public static async Task<CrashReportResult> ShowLinuxDialog(string errorMessage, string errorDetails)
        {
            var result = new CrashReportResult();
            
            // Determine which dialog tool is available
            string? dialogTool = await FindLinuxDialogTool();
            if (string.IsNullOrEmpty(dialogTool))
                return result;

            try
            {
                // Ask if user wants to submit
                string askArgs = dialogTool == "zenity"
                    ? $"""
                       --question --title="Application Error" --text="{errorMessage}

                       Would you like to submit a crash report?" --ok-label="Submit Report" --cancel-label="Cancel"
                       """
                    : $"""
                       --title "Application Error" --yesno "{errorMessage}

                       Would you like to submit a crash report?"
                       """;

                var askProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = dialogTool,
                    Arguments = dialogTool == "zenity" ? askArgs : $"--yes-label \"Submit Report\" --no-label \"Cancel\" {askArgs}",
                    UseShellExecute = false
                });

                if (askProcess == null)
                {
                    result.Submitted = false;
                    return result;
                }

                await askProcess.WaitForExitAsync();
                if (askProcess.ExitCode != 0)
                {
                    result.Submitted = false;
                    return result;
                }

                // Get user name
                result.UserName = await RunLinuxInputDialog(dialogTool, "Please enter your name:", "Crash Reporter");
                
                // Get email
                result.Email = await RunLinuxInputDialog(dialogTool, "Please enter your email address:", "Crash Reporter");
                
                // Get description
                result.Description = await RunLinuxInputDialog(dialogTool, "What were you doing when the error occurred?", "Crash Reporter");
                
                result.Submitted = !string.IsNullOrWhiteSpace(result.Description);
                return result;
            }
            catch
            {
                result.Submitted = false;
                return result;
            }
        }

        private static async Task<string?> FindLinuxDialogTool()
        {
            try
            {
                var zenityProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "zenity",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });

                if (zenityProcess != null)
                {
                    string zenityPath = (await zenityProcess.StandardOutput.ReadToEndAsync()).Trim();
                    await zenityProcess.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(zenityPath))
                        return "zenity";
                }
            }
            catch { }

            try
            {
                var kdialogProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = "kdialog",
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                });

                if (kdialogProcess != null)
                {
                    string kdialogPath = (await kdialogProcess.StandardOutput.ReadToEndAsync()).Trim();
                    await kdialogProcess.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(kdialogPath))
                        return "kdialog";
                }
            }
            catch { }

            return null;
        }

        private static async Task<string> RunLinuxInputDialog(string tool, string prompt, string title)
        {
            string args = tool == "zenity"
                ? $"""
                   --entry --title="{title}" --text="{prompt}"
                   """
                : $"""
                   --title "{title}" --inputbox "{prompt}"
                   """;

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = tool,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true
            })!;
            string output = (await process.StandardOutput.ReadToEndAsync()).Trim();
            await process.WaitForExitAsync();

            return output;
        }

        // Helper methods
        private static string EscapeAppleScript(string text)
        {
            return text.Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r");
        }

        private static async Task<string> RunOsascript(string script)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = "osascript",
                Arguments = $"-e \"{script}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            })!;
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return output.Trim();
        }

        private static void ParseOutput(string output, CrashReportResult result)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                result.Submitted = false;
                return;
            }

            var lines = output.Split('\n');
            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (trimmedLine.StartsWith("SUBMITTED"))
                    result.Submitted = true;
                else if (trimmedLine.StartsWith("NAME:"))
                    result.UserName = trimmedLine[5..].Trim();
                else if (trimmedLine.StartsWith("EMAIL:"))
                    result.Email = trimmedLine[6..].Trim();
                else if (trimmedLine.StartsWith("DESCRIPTION:"))
                    result.Description = trimmedLine[12..].Trim();
            }
        }

        // Main entry point
        public static async Task<CrashReportResult> ShowDialog(string errorMessage, string? errorDetails = null)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return await ShowWindowsDialog(errorMessage, errorDetails);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return await ShowMacDialog(errorMessage, errorDetails);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return await ShowLinuxDialog(errorMessage, errorDetails);
            
            return new CrashReportResult { Submitted = false };
        }
    }
}