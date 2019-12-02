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
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml;
using System.IO;
using System.Diagnostics;
using DomainObjects;

namespace DVBLogicPlugin
{
    internal class PluginMonitor
    {
        internal int MonitorIdentity { get; }

        private readonly string _directory;
        private State _pluginStatus = State.unknown;
        private string _frequency;
        private Process _collectionProcess;
        private string _runReference;
        private Mutex _cancelMutex;

        private enum State
        {
            unknown = 0,
            inProgress,
            finishedError,
            finishSuccess,
            finishAborted
        };

        internal PluginMonitor(int monitorIdentity, string directory)
        {
            MonitorIdentity = monitorIdentity;
            _directory = directory;
            _pluginStatus = State.inProgress;            
        }

        internal bool StartScan(IntPtr scanInfo)
        {
            byte[] scanData = new byte[256];

            byte scanByte = 0xff;
            int index = 0;

            while (scanByte != 0x00)
            {
                scanByte = Marshal.ReadByte(scanInfo, index);
                if (scanByte != 0x00)
                {
                    scanData[index] = scanByte;
                    index++;
                }
            }

            MemoryStream memoryStream = new MemoryStream(scanData, 0, index);
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.CloseInput = true;
            settings.IgnoreWhitespace = true;
            XmlReader reader = XmlReader.Create(memoryStream, settings);

            try
            {
                while (!reader.EOF)
                {
                    reader.Read();
                    if (reader.IsStartElement())
                    {
                        switch (reader.Name)
                        {
                            case "frequency":
                                _frequency = reader.ReadString();
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
            catch (XmlException e)
            {
                Logger.Instance.Write("<E> Failed to parse scan info");
                Logger.Instance.Write("<E> Data exception: " + e.Message);
                return (false);
            }
            catch (IOException e)
            {
                Logger.Instance.Write("<E> Failed to parse scan info");
                Logger.Instance.Write("<E> I/O exception: " + e.Message);
                return (false);
            }

            reader.Close();

            string actualFileName = Path.Combine(_directory, _frequency.ToString()) + ".ini";
            Logger.Instance.Write("Running collection with parameters from " + actualFileName);

            if (!File.Exists(actualFileName))
            {
                Logger.Instance.Write("<e> The collection parameters do not exist - collection will be abandoned");
                _pluginStatus = State.finishedError;
                return (false);
            }

            _runReference = Process.GetCurrentProcess().Id + "-" + MonitorIdentity;

            string cancellationName = "EPG Collector Cancellation Mutex " + _runReference;
            Logger.Instance.Write("Cancellation mutex name is " + cancellationName);
            _cancelMutex = new Mutex(true, cancellationName); 

            _collectionProcess = new Process();

            _collectionProcess.StartInfo.FileName = Path.Combine(RunParameters.BaseDirectory, "EPGCollector.exe");
            _collectionProcess.StartInfo.WorkingDirectory = RunParameters.BaseDirectory;
            _collectionProcess.StartInfo.Arguments = @"/ini=" + '"' + actualFileName + '"' + " /plugin=" + _runReference;
            _collectionProcess.StartInfo.UseShellExecute = false;
            _collectionProcess.StartInfo.CreateNoWindow = true;
            _collectionProcess.EnableRaisingEvents = true;
            _collectionProcess.Exited += new EventHandler(collectionProcessExited);

            _collectionProcess.Start();

            _pluginStatus = State.inProgress;

            return (true);
        }

        private void collectionProcessExited(object sender, EventArgs e)
        {
            int exitCode = _collectionProcess.ExitCode;
            Logger.Instance.Write("Plugin notified that collection has completed with code " + exitCode);

            _collectionProcess.Close();
            _collectionProcess = null;

            _cancelMutex.Close();
            _cancelMutex = null;

            if (exitCode == 0)
                _pluginStatus = State.finishSuccess;
            else
                _pluginStatus = State.finishedError;
        }

        internal bool StopScan()
        {
            if (_cancelMutex != null)
            {
                Logger.Instance.Write("Plugin is releasing cancellation mutex");
                _cancelMutex.ReleaseMutex();
            }
            
            _pluginStatus = State.finishAborted;

            return (true);
        }

        internal int GetScanStatus()
        {
            return ((int)_pluginStatus);
        }

        internal int GetEPGData(IntPtr buffer, int bufferSize)
        {
            string fileName = Path.Combine(_directory, "EPG Collector Plugin.xml");
            FileInfo fileInfo = new FileInfo(fileName);
            int fileLength = 0;
            if (fileInfo.Exists)
                fileLength = (int)fileInfo.Length;

            if (bufferSize == 0)
            {
                if (fileLength != 0)
                {
                    Logger.Instance.Write("Replying with buffer size = " + (fileLength + 1));
                    return (fileLength + 1);
                }
                else
                {
                    Logger.Instance.Write("Replying with buffer size 0 for non-buffered output");
                    fileInfo = null;
                    GC.Collect();
                    return (0);
                }
            }
            else
            {
                Logger.Instance.Write("Passing " + (fileLength + 1) + " bytes");

                FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
                bool eof = false;
                byte[] readBuffer = new byte[1024 * 1024];

                do
                {
                    int readCount = fileStream.Read(readBuffer, 0, 1024 * 1024);
                    if (readCount != 0)
                    {
                        Marshal.Copy(readBuffer, 0, buffer, readCount);
                        buffer = new IntPtr(buffer.ToInt64() + readCount);
                    }
                    eof = (readCount != 1024 * 1024);
                }
                while (!eof);

                fileStream.Close();
                fileInfo.Delete();

                Marshal.WriteByte(buffer, 0x00);
                Logger.Instance.Write("Data transfer complete");

                return (fileLength + 1);
            }
        }
    }
}
