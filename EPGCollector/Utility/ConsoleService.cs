using System;
using System.Diagnostics;
using DomainObjects;

namespace EPGCollector
{
    public partial class ConsoleService
    {
        public string Command { get; private set; }
        public string Arguments { get; private set; }

        public int ExitCode { get; set; }

        public void Reset()
        {
            ExitCode = 0;
        }

        public void Run(string command, params string[] args)
        {
            Command = Environment.ExpandEnvironmentVariables(command);
            Arguments = string.Join(" ", args);

            Run();
        }

        public void Run()
        {
            Reset();

            using (var process = new Process())
            {
                process.StartInfo.FileName = Command;
                process.StartInfo.Arguments = Arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.ErrorDialog = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;

                process.OutputDataReceived += (sender, eventArgs) =>
                {
                    if (eventArgs.Data != null)
                    {
                        Logger.Instance.Write(eventArgs.Data);
                    }
                };
                process.ErrorDataReceived += (sender, eventArgs) =>
                {
                    if (eventArgs.Data != null)
                    {
                        Logger.Instance.Write(eventArgs.Data);
                    }
                };

                if (process.Start() == false)
                {
                    Logger.Instance.Write($"Error starting {Command}");
                }
                else
                {
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    ExitCode = process.ExitCode;
                }
            }
        }
    }
}
