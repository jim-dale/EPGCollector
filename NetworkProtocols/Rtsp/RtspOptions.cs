﻿////////////////////////////////////////////////////////////////////////////////// 
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

using System.IO;
using System.Net.Sockets;

namespace NetworkProtocols.Rtsp
{
    /// <summary>
    /// The class that describes an RTSP Options message.
    /// </summary>
    public class RtspOptions : RtspMessageBase
    {
        /// <summary>
        /// Initialize a new instance of the RtspOptions class.
        /// </summary>
        public RtspOptions() { }

        /// <summary>
        /// Send an Options request and receive and process the response. 
        /// </summary>
        /// <param name="hostAddress">The server address.</param>
        /// <param name="hostPort">The server port.</param>
        /// <param name="cseq">The sequence number of the message.</param>
        /// <param name="sessionId">The session identifier.</param>
        /// <param name="streamId">The stream identifier.</param>
        /// <param name="noSendPort">True to not send the port number in the OPTIONS message; false otherwise.</param>
        /// <returns>An ErrorSpec instance if an error occurs; null otherwise.</returns>
        public ErrorSpec Process(string hostAddress, int hostPort, int cseq, string sessionId, int streamId, bool noSendPort)
        {
            MemoryStream outputStream = new MemoryStream();
            StreamWriter streamWriter = new StreamWriter(outputStream);
            streamWriter.NewLine = "\r\n";

            if (!noSendPort)
                streamWriter.WriteLine("OPTIONS rtsp://" + hostAddress + ":" + hostPort + "/stream=" + streamId  + " RTSP/1.0");
            else
                streamWriter.WriteLine("OPTIONS rtsp://" + hostAddress + "/stream=" + streamId + " RTSP/1.0");

            streamWriter.WriteLine("CSeq:" + cseq);
            if (sessionId != null)
                streamWriter.WriteLine("Session:" + sessionId);
            streamWriter.WriteLine("Connection:close");
            streamWriter.WriteLine(string.Empty);
            streamWriter.Close();

            NetworkLogger.Instance.Write("Sending OPTIONS message to " + hostAddress + " port " + hostPort);

            TcpClient tcpClient = new TcpClient(hostAddress, hostPort);
            NetworkStream networkStream = tcpClient.GetStream();

            byte[] requestMessage = outputStream.ToArray();
            NetworkLogger.Instance.LogReply("RTSP OPTIONS request: ", requestMessage, requestMessage.Length);
            networkStream.Write(requestMessage, 0, requestMessage.Length);

            byte[] responseMessage = new byte[1024];
            int byteCount = networkStream.Read(responseMessage, 0, 1024);            

            networkStream.Close();
            tcpClient.Close();

            NetworkLogger.Instance.LogReply("RTSP OPTIONS response: ", responseMessage, byteCount);  

            MemoryStream replyStream = new MemoryStream(responseMessage, 0, byteCount);
            StreamReader streamReader = new StreamReader(replyStream);

            ErrorSpec error = StatusCode.Process(streamReader, RtspProtocolId, RtspProtocolVersion);
            if (error != null)
                return (error);

            while (!streamReader.EndOfStream)
            {
                string line = streamReader.ReadLine();
                error = base.ProcessLine(line, cseq);
                if (error != null)
                {
                    streamReader.Close();
                    return (error);
                }
            }

            streamReader.Close();

            return (null);
        }
    }
}
