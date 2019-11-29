////////////////////////////////////////////////////////////////////////////////// 
//                                                                              //
//      Copyright (C) 2005-2016 nzsjb                                           //
//                                                                              //
//  This Program is free software; you can redistribute it and/or modify        //
//  it under the terms of the GNU General Public License as published by        //
//  the Free Software Foundation; either version 2, or (at your option)         //
//  any later version.                                                          //
//                                                                              //
//  This Program is distributed in the hope that it will be useful,             //
//  but WITHOUT ANY WARRANTY; without even the implied warranty of              //
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the                //
//  GNU General Public License for more details.                                //
//                                                                              //
//  You should have received a copy of the GNU General Public License           //
//  along with GNU Make; see the file COPYING.  If not, write to                //
//  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA.       //
//  http://www.gnu.org/copyleft/gpl.html                                        //
//                                                                              //  
//////////////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using System.IO;

namespace DomainObjects
{
    /// <summary>
    /// The class that describes the WMC utility function.
    /// </summary>
    public static class WMCUtility
    {
        private const string AppName = "WMCUtility.exe";

        /// <summary>
        /// Run the utility functions
        /// </summary>
        /// <param name="description">The description of the function</param>
        /// <param name="arguments">The parameters to the function</param>
        /// <returns></returns>
        public static string Run(string description, string arguments)
        {
            Logger.Instance.Write("Running Windows Media Centre Utility to " + description);


            if (Environment.OSVersion.Version.Major < 6)
                return ("Windows Media Centre Utility cannot run on this version of Windows (" + Environment.OSVersion + ")");

            FileVersionInfo fileVersionInfo;

            try
            {
                fileVersionInfo = FileVersionInfo.GetVersionInfo(Path.Combine(Environment.GetEnvironmentVariable("windir"), Path.Combine("ehome", "mcepg.dll")));
                if (fileVersionInfo == null)
                    return ("Windows Media Centre Utility cannot run because the file mcepg.dll has no file version number");

                Logger.Instance.Write("The file version number for mcepg.dll is " + fileVersionInfo.FileVersion);
            }
            catch (FileNotFoundException)
            {
                return ("Windows Media Centre Utility cannot run - can't find Windows Media Centre file mcepg.dll");
            }

            using (var process = new Process())
            {
                process.StartInfo.FileName = AppName;
                process.StartInfo.WorkingDirectory = Environment.CurrentDirectory;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.EnableRaisingEvents = true;
                if (arguments != null)
                {
                    process.StartInfo.Arguments = arguments;
                }
                process.OutputDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Logger.Instance.Write(e.Data);
                    }
                });
                process.ErrorDataReceived += new DataReceivedEventHandler((sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Logger.Instance.Write(e.Data);
                    }
                });

                try
                {
                    process.Start();

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    Logger.Instance.Write("Windows Media Centre Utility has completed: exit code " + process.ExitCode);
                    if (process.ExitCode == 0)
                        return null;
                    else
                        return "Windows Media Centre failed: reply code " + process.ExitCode;
                }
                catch (Exception e)
                {
                    Logger.Instance.Write("<e> Failed to run the Windows Media Centre Utility");
                    Logger.Instance.Write("<e> " + e.Message);

                    return "Failed to run Windows Media Centre Utility due to an exception";
                }
            }
        }
    }
}
