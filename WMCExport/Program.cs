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
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WMCUtility
{
    class Program
    {
        static int Main(string[] args)
        {
            int exitCode = 1;

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            using (var serviceProvider = serviceCollection.BuildServiceProvider())
            {
                var commandLine = Environment.CommandLine;

                var logger = serviceProvider.GetService<ILogger<Program>>();
                logger.LogInformation("Windows Media Center Utility build: {Version}", AssemblyVersion);
                logger.LogInformation("Windows Media Center Utility command line: {CommandLine}", commandLine);

                Type commandType = ParseCommandLine(args);
                if (commandType == default)
                {
                    logger.LogError("Windows Media Center Utility started with invalid command line: {CommandLine}", commandLine);
                }
                else
                {
                    var command = (ICommand)serviceProvider.GetRequiredService(commandType);

                    exitCode = command.Run();
                }
            }

            return exitCode;
        }

        private static Type ParseCommandLine(string[] args)
        {
            Type commandType = default;

            foreach (var arg in args)
            {
                var commandName = arg.ToUpperInvariant();

                switch (commandName)
                {
                    case "EXPORTDATA":
                        commandType = typeof(ExportMcedbCommand);
                        break;
                    case "DISABLEGUIDELOADER":
                        commandType = typeof(DisableGuideLoaderCommand);
                        break;
                    case "ENABLEGUIDELOADER":
                        commandType = typeof(EnableGuideLoaderCommand);
                        break;
                    default:
                        break;
                }
            }

            return commandType;
        }

        private static string AssemblyVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version.ToString();
            }
        }

        private static void ConfigureServices(ServiceCollection services)
        {
            services.AddLogging(configure => configure.AddConsole())
                .AddTransient<ExportMcedbCommand>()
                .AddTransient<EnableGuideLoaderCommand>()
                .AddTransient<DisableGuideLoaderCommand>();
        }
    }
}
