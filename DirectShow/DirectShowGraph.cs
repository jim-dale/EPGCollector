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
using System.Threading;
using DirectShowAPI;
using DomainObjects;

namespace DirectShow
{
    /// <summary>
    /// The class that describes a DirectShow graph
    /// </summary>
    public abstract class DirectShowGraph
    {
        /// <summary>Get the full assembly version number</summary>
        public static string AssemblyVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version.ToString();
            }
        }

        /// <summary>Get the capture builder for the graph</summary>
        public ICaptureGraphBuilder2 CaptureGraphBuilder { get; private set; }
        /// <summary>Get the graph builder for the graph</summary>
        public IFilterGraph2 GraphBuilder { get; private set; }
        /// <summary>Get the media control for the graph</summary>
        public IMediaControl MediaControl { get; private set; }
        /// <summary>Get the component name for the graph</summary>
        public string ComponentName { get; }

        /// <summary>Get or set the flag that forces tracing</summary>
        public static bool ForceTrace { get; set; }

        /// <summary>
        /// Initialize a new instance of the DirectShowGraph class
        /// </summary>
        /// <param name="componentName">The name of the component owning the graph</param>
        protected DirectShowGraph(string componentName)
        {
            ComponentName = componentName;
        }

        /// <summary>
        /// Decompose the graph and release resources.
        /// </summary>
        public virtual void Dispose()
        {
            MediaControl.GetState(0, out FilterState filterState);
            if (filterState != FilterState.Stopped)
            {
                LogMessage("Stopping graph");
                int hr = MediaControl.Stop();
                DsError.ThrowExceptionForHR(hr);
            }

            LogMessage("Removing filters");
            FilterGraphTools.RemoveAllFilters(GraphBuilder);

            LogMessage("Releasing Graph Builder");
            GraphBuilder = null;

            LogMessage("Releasing Capture Builder");
            CaptureGraphBuilder = null;
        }

        /// <summary>Start the graph</summary>
        public virtual bool Play()
        {
            LogMessage("Graph playing");
            int hr = MediaControl.Run();
            return hr >= 0;
        }

        /// <summary>Stop the graph</summary>
        public virtual void Stop()
        {
            LogMessage("Graph stopped");
            int hr = MediaControl.Stop();
            DsError.ThrowExceptionForHR(hr);
        }

        /// <summary>
        /// Pause or restart the graph.
        /// </summary>
        /// <param name="pause">True to pause the graph; false otherwise.</param>
        public void Pause(bool pause)
        {
            int hr;

            if (pause)
            {
                hr = MediaControl.Pause();
                Thread.Sleep(250);
            }
            else
            {
                hr = MediaControl.Run();
            }
            DsError.ThrowExceptionForHR(hr);
        }

        /// <summary>
        /// Build the graph.
        /// </summary>
        protected virtual void BuildGraph()
        {
            CaptureGraphBuilder = (ICaptureGraphBuilder2)new CaptureGraphBuilder2();
            GraphBuilder = (IFilterGraph2)new FilterGraph();
            CaptureGraphBuilder.SetFiltergraph(GraphBuilder);

            MediaControl = GraphBuilder as IMediaControl;
        }

        internal IPin FindPin(IBaseFilter filter, string name)
        {
            int hr = filter.EnumPins(out IEnumPins enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new IPin[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryPinInfo(out PinInfo pinInfo);
                if (hr != 0)
                {
                    _ = filter.QueryFilterInfo(out FilterInfo filterInfo);
                    throw new InvalidOperationException("The pin '" + name + "' could not be located for filter '" + filterInfo.achName + "'");
                }

                if (pinInfo.name.StartsWith(name))
                {
                    return current;
                }
            }

            return null;
        }

        internal IPin FindPin(IBaseFilter filter, PinDirection direction)
        {
            int hr = filter.EnumPins(out IEnumPins enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new IPin[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryDirection(out PinDirection pinDirection);
                DsError.ThrowExceptionForHR(hr);

                if (pinDirection == direction)
                {
                    return current;
                }
            }

            return null;
        }

        internal IPin FindPin(IBaseFilter filter, Guid mediaType, PinDirection direction)
        {
            int hr = filter.EnumPins(out IEnumPins enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new IPin[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryDirection(out PinDirection pinDirection);
                DsError.ThrowExceptionForHR(hr);

                if (pinDirection == direction)
                {
                    bool mediaReply = CheckMediaType(current, mediaType);
                    if (mediaReply)
                    {
                        return current;
                    }
                }
            }

            return null;
        }

        internal IPin FindPin(IBaseFilter filter, Guid mediaType, Guid mediaSubType, PinDirection direction)
        {
            int hr = filter.EnumPins(out IEnumPins enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new IPin[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryDirection(out PinDirection pinDirection);
                DsError.ThrowExceptionForHR(hr);

                if (pinDirection == direction)
                {
                    if (CheckMediaTypes(current, mediaType, mediaSubType))
                    {
                        return current;
                    }
                }
            }

            return null;
        }

        internal static IPin LocatePin(IBaseFilter filter, PinDirection direction)
        {
            int hr = filter.EnumPins(out IEnumPins enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new IPin[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryDirection(out PinDirection pinDirection);
                DsError.ThrowExceptionForHR(hr);

                if (pinDirection == direction)
                {
                    return current;
                }
            }

            return null;
        }

        internal static IPin LocatePin(IBaseFilter filter, Guid mediaType, Guid mediaSubType, PinDirection direction)
        {
            int hr = filter.EnumPins(out IEnumPins enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new IPin[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryDirection(out PinDirection pinDirection);
                DsError.ThrowExceptionForHR(hr);

                if (pinDirection == direction)
                {
                    if (CheckMediaTypes(current, mediaType, mediaSubType))
                    {
                        return current;
                    }
                }
            }

            return null;
        }

        private static bool CheckMediaType(IPin pin, Guid mediaType)
        {
            int hr = pin.EnumMediaTypes(out IEnumMediaTypes enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new AMMediaType[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                if (current.majorType == mediaType)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CheckMediaTypes(IPin pin, Guid mediaType, Guid mediaSubType)
        {
            int hr = pin.EnumMediaTypes(out IEnumMediaTypes enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new AMMediaType[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                if (current != null && current.majorType == mediaType && current.subType == mediaSubType)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Log the filters in the graph</summary>
        protected void LogFilters()
        {
            LogMessage("Logging filters");

            int hr = GraphBuilder.EnumFilters(out IEnumFilters enumerator);
            DsError.ThrowExceptionForHR(hr);

            var values = new IBaseFilter[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryFilterInfo(out FilterInfo filterInfo);
                DsError.ThrowExceptionForHR(hr);

                LogMessage("Filter: " + filterInfo.achName);
                LogPins(current);
            }

            LogMessage("All filters logged");
        }

        private void LogPins(IBaseFilter filter)
        {
            int hr = filter.EnumPins(out IEnumPins enumerator);
            DsError.ThrowExceptionForHR(hr);

            if (enumerator == null)
                return;

            var values = new IPin[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                hr = current.QueryPinInfo(out PinInfo pinInfo);
                DsError.ThrowExceptionForHR(hr);

                _ = current.ConnectedTo(out IPin connectedPin);
                if (connectedPin != null)
                {
                    hr = connectedPin.QueryPinInfo(out PinInfo connectedPinInfo);
                    DsError.ThrowExceptionForHR(hr);

                    hr = connectedPinInfo.filter.QueryFilterInfo(out FilterInfo filterInfo);
                    DsError.ThrowExceptionForHR(hr);

                    LogMessage("Pin: " + pinInfo.name + " Connected to: " + filterInfo.achName);
                    LogMediaTypes(current);
                }
                else
                {
                    LogMessage("Pin: " + pinInfo.name + " not connected");
                    LogMediaTypes(current);
                }
            }
        }

        private void LogMediaTypes(IPin pin)
        {
            int hr = pin.EnumMediaTypes(out IEnumMediaTypes enumerator);
            if (hr != 0)
            {
                LogMessage("Media types cannot be determined at this time (not connected yet?)");
                return;
            }

            AMMediaType connectedMediaType = new AMMediaType();
            _ = pin.ConnectionMediaType(connectedMediaType);

            var values = new AMMediaType[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                var current = values[0];

                // Is this still required?
                hr = pin.QueryPinInfo(out PinInfo pinInfo);
                DsError.ThrowExceptionForHR(hr);

                var majorType = GetMediaMajorTypeAsString(current.majorType);
                var subType = GetMediaSubTypeAsString(current.subType);
                var connectedComment = string.Empty;

                if (current.majorType == connectedMediaType.majorType && current.subType == connectedMediaType.subType)
                {
                    connectedComment = "** Connected **";
                }
                LogMessage("Media type: " + majorType + " ; " + subType + " " + current.fixedSizeSamples + " " + current.sampleSize + " " + connectedComment);
            }
        }

        internal string GetMediaMajorTypeAsString(Guid value)
        {
            if (value == MediaType.AnalogAudio)
                return "Analog Audio";
            if (value == MediaType.AnalogVideo)
                return "Analog Video";
            if (value == MediaType.Audio)
                return "Audio";
            if (value == MediaType.AuxLine21Data)
                return "Aux Line21 Data";
            if (value == MediaType.DTVCCData)
                return "DTV CC Data";
            if (value == MediaType.File)
                return "File";
            if (value == MediaType.Interleaved)
                return "Interleaved";
            if (value == MediaType.LMRT)
                return "LMRT";
            if (value == MediaType.Midi)
                return "Midi";
            if (value == MediaType.Mpeg2Sections)
                return "MPEG2 Sections";
            if (value == MediaType.MSTVCaption)
                return "MSTV Caption";
            if (value == MediaType.ScriptCommand)
                return "Script Command";
            if (value == MediaType.Stream)
                return "Stream";
            if (value == MediaType.Timecode)
                return "Timecode";
            if (value == MediaType.URLStream)
                return "URL Stream";
            if (value == MediaType.VBI)
                return "VBI";
            if (value == MediaType.Video)
                return "Video";

            return value.ToString();
        }

        internal string GetMediaSubTypeAsString(Guid value)
        {
            if (value == MediaSubType.A2B10G10R10)
                return "A2B10G10R10";
            if (value == MediaSubType.A2R10G10B10)
                return "A2B10G10B10";
            if (value == MediaSubType.AI44)
                return "AI44";
            if (value == MediaSubType.AIFF)
                return "AIFF";
            if (value == MediaSubType.AnalogVideo_NTSC_M)
                return "Analog Video NTSC M";
            if (value == MediaSubType.AnalogVideo_PAL_B)
                return "Analog Video PAL B";
            if (value == MediaSubType.AnalogVideo_PAL_D)
                return "Analog Video PAL D";
            if (value == MediaSubType.AnalogVideo_PAL_G)
                return "Analog Video PAL G";
            if (value == MediaSubType.AnalogVideo_PAL_H)
                return "Analog Video PAL H";
            if (value == MediaSubType.AnalogVideo_PAL_I)
                return "Analog Video PAL I";
            if (value == MediaSubType.AnalogVideo_PAL_M)
                return "Analog Video PAL M";
            if (value == MediaSubType.AnalogVideo_PAL_N)
                return "Analog Video PAL N";
            if (value == MediaSubType.AnalogVideo_PAL_N_COMBO)
                return "Analog Video PAL N Combo";
            if (value == MediaSubType.AnalogVideo_SECAM_B)
                return "Analog Video Secam B";
            if (value == MediaSubType.AnalogVideo_SECAM_D)
                return "Analog Video Secam D";
            if (value == MediaSubType.AnalogVideo_SECAM_G)
                return "Analog Video Secam G";
            if (value == MediaSubType.AnalogVideo_SECAM_H)
                return "Analog Video Secam H";
            if (value == MediaSubType.AnalogVideo_SECAM_K)
                return "Analog Video Secam K";
            if (value == MediaSubType.AnalogVideo_SECAM_K1)
                return "Analog Video Secam K1";
            if (value == MediaSubType.AnalogVideo_SECAM_L)
                return "Analog Video Secam L";
            if (value == MediaSubType.ARGB1555)
                return "ARGB1555";
            if (value == MediaSubType.ARGB1555_D3D_DX7_RT)
                return "ARGB1555 D3D DX7 RT";
            if (value == MediaSubType.ARGB1555_D3D_DX9_RT)
                return "ARGB1555 D3D DX9 RT";
            if (value == MediaSubType.ARGB32)
                return "ARGB32";
            if (value == MediaSubType.ARGB32_D3D_DX7_RT)
                return "ARGB32 D3D DX7 RT";
            if (value == MediaSubType.ARGB32_D3D_DX9_RT)
                return "ARGB32 D3D DX9 RT";
            if (value == MediaSubType.ARGB4444)
                return "ARGB4444";
            if (value == MediaSubType.ARGB4444_D3D_DX7_RT)
                return "ARGB4444 D3D DX7 RT";
            if (value == MediaSubType.ARGB4444_D3D_DX9_RT)
                return "ARGB4444 D3D DX9 RT";
            if (value == MediaSubType.Asf)
                return "ASF";
            if (value == MediaSubType.AtscSI)
                return "ATSC SI";
            if (value == MediaSubType.AU)
                return "AU";
            if (value == MediaSubType.Avi)
                return "AVI";
            if (value == MediaSubType.AYUV)
                return "AYUV";
            if (value == MediaSubType.CFCC)
                return "CFCC";
            if (value == MediaSubType.CLPL)
                return "ARGB 1555";
            if (value == MediaSubType.CLJR)
                return "CLPL";
            if (value == MediaSubType.CPLA)
                return "CPLA";
            if (value == MediaSubType.Data708_608)
                return "Data 708/608";
            if (value == MediaSubType.DOLBY_AC3_SPDIF)
                return "Dolby AC3 S/P DIF";
            if (value == MediaSubType.DolbyAC3)
                return "Dolby AC3";
            if (value == MediaSubType.DRM_Audio)
                return "DRM Audio";
            if (value == MediaSubType.DssAudio)
                return "DSS Audio";
            if (value == MediaSubType.DssVideo)
                return "DSS Video";
            if (value == MediaSubType.DtvCcData)
                return "DTV CC Data";
            if (value == MediaSubType.dv25)
                return "DV25";
            if (value == MediaSubType.dv50)
                return "DV50";
            if (value == MediaSubType.DvbSI)
                return "DVB SI";
            if (value == MediaSubType.DVCS)
                return "DVCS";
            if (value == MediaSubType.dvh1)
                return "DV H1";
            if (value == MediaSubType.dvhd)
                return "DV HD";
            if (value == MediaSubType.DVSD)
                return "DV SD";
            if (value == MediaSubType.dvsl)
                return "DV SL";
            if (value == MediaSubType.H264)
                return "H264";
            if (value == MediaSubType.I420)
                return "I420";
            if (value == MediaSubType.IA44)
                return "IA44";
            if (value == MediaSubType.IEEE_FLOAT)
                return "IEEE FLOAT";
            if (value == MediaSubType.IF09)
                return "IF09";
            if (value == MediaSubType.IJPG)
                return "IJPG";
            if (value == MediaSubType.IMC1)
                return "IMC1";
            if (value == MediaSubType.IMC2)
                return "IMC2";
            if (value == MediaSubType.IMC3)
                return "IMC3";
            if (value == MediaSubType.IMC4)
                return "IMC4";
            if (value == MediaSubType.IYUV)
                return "IYUV";
            if (value == MediaSubType.Line21_BytePair)
                return "Line21 Byte Pair";
            if (value == MediaSubType.Line21_GOPPacket)
                return "Line21 GOP Packet";
            if (value == MediaSubType.Line21_VBIRawData)
                return "Line21 VBI Raw Data";
            if (value == MediaSubType.MDVF)
                return "MDVF";
            if (value == MediaSubType.MJPG)
                return "MJPG";
            if (value == MediaSubType.MPEG1Audio)
                return "MPEG1 Audio";
            if (value == MediaSubType.MPEG1AudioPayload)
                return "MPEG1 Audio Payload";
            if (value == MediaSubType.MPEG1Packet)
                return "MPEG1 Packet";
            if (value == MediaSubType.MPEG1Payload)
                return "MPEG1 Payload";
            if (value == MediaSubType.MPEG1System)
                return "MPEG1 System";
            if (value == MediaSubType.MPEG1SystemStream)
                return "MPEG1 System Stream";
            if (value == MediaSubType.MPEG1Video)
                return "MPEG1 Video";
            if (value == MediaSubType.MPEG1VideoCD)
                return "MPEG1 Video CD";
            if (value == MediaSubType.Mpeg2Audio)
                return "MPEG2 Audio";
            if (value == MediaSubType.Mpeg2Data)
                return "MPEG2 Data";
            if (value == MediaSubType.Mpeg2Program)
                return "MPEG2 Program";
            if (value == MediaSubType.Mpeg2Transport)
                return "MPEG2 Transport";
            if (value == MediaSubType.Mpeg2TransportStride)
                return "MPEG2 Transport Stride";
            if (value == MediaSubType.Mpeg2Video)
                return "MPEG2 Video";
            if (value == MediaSubType.None)
                return "None";
            if (value == MediaSubType.Null)
                return "Null";
            if (value == MediaSubType.NV12)
                return "NV12";
            if (value == MediaSubType.NV24)
                return "NV24";
            if (value == MediaSubType.Overlay)
                return "Overlay";
            if (value == MediaSubType.PCM)
                return "PCM";
            if (value == MediaSubType.PCMAudio_Obsolete)
                return "PCM Audio Obsolete";
            if (value == MediaSubType.PLUM)
                return "PLUM";
            if (value == MediaSubType.QTJpeg)
                return "QT JPEG";
            if (value == MediaSubType.QTMovie)
                return "QT Movie";
            if (value == MediaSubType.QTRle)
                return "QT RLE";
            if (value == MediaSubType.QTRpza)
                return "QT RPZA";
            if (value == MediaSubType.QTSmc)
                return "QT SMC";
            if (value == MediaSubType.RAW_SPORT)
                return "RAW SPORT";
            if (value == MediaSubType.RGB1)
                return "RGB1";
            if (value == MediaSubType.RGB16_D3D_DX7_RT)
                return "RGB16 D3D DX7 RT";
            if (value == MediaSubType.RGB16_D3D_DX9_RT)
                return "RGB16 D3D DX9 RT";
            if (value == MediaSubType.RGB24)
                return "RGB24";
            if (value == MediaSubType.RGB32)
                return "RGB32";
            if (value == MediaSubType.RGB32_D3D_DX7_RT)
                return "RGB32 D3D DX7 RT";
            if (value == MediaSubType.RGB32_D3D_DX9_RT)
                return "RGB32 D3D DX9 RT";
            if (value == MediaSubType.RGB4)
                return "RGB4";
            if (value == MediaSubType.RGB555)
                return "RGB555";
            if (value == MediaSubType.RGB565)
                return "RGB565";
            if (value == MediaSubType.RGB8)
                return "RGB8";
            if (value == MediaSubType.S340)
                return "S340";
            if (value == MediaSubType.S342)
                return "S342";
            if (value == MediaSubType.SPDIF_TAG_241h)
                return "S/P DIF TAG 241H";
            if (value == MediaSubType.TELETEXT)
                return "Teletext";
            if (value == MediaSubType.TVMJ)
                return "TVMJ";
            if (value == MediaSubType.UYVY)
                return "UYVY";
            if (value == MediaSubType.VideoImage)
                return "Video Image";
            if (value == MediaSubType.VPS)
                return "VPS";
            if (value == MediaSubType.VPVBI)
                return "VP VBI";
            if (value == MediaSubType.VPVideo)
                return "VP Video";
            if (value == MediaSubType.WAKE)
                return "WAKE";
            if (value == MediaSubType.WAVE)
                return "WAVE";
            if (value == MediaSubType.WebStream)
                return "Web Stream";
            if (value == MediaSubType.WSS)
                return "WSS";
            if (value == MediaSubType.Y211)
                return "Y211";
            if (value == MediaSubType.Y411)
                return "Y411";
            if (value == MediaSubType.Y41P)
                return "Y41P";
            if (value == MediaSubType.YUY2)
                return "YUY2";
            if (value == MediaSubType.YUYV)
                return "YUYV";
            if (value == MediaSubType.YV12)
                return "YV12";
            if (value == MediaSubType.YVU9)
                return "YVU9";
            if (value == MediaSubType.YVYU)
                return "YVYU";

            return value.ToString();
        }

        /// <summary>Log a BDA message</summary>
        /// <param name="message">The message to be logged</param>
        protected void LogMessage(string message)
        {
            if (TraceEntry.IsDefined(TraceName.Bda) || ForceTrace)
            {
                Logger.Instance.Write(ComponentName + " " + message);
            }
        }
    }
}
