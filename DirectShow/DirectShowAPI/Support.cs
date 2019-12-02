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
using System.Collections;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace DirectShowAPI
{
    /// <summary>
    /// A collection of methods to do common DirectShow tasks.
    /// </summary>
    public sealed class FilterGraphTools
    {
        private FilterGraphTools() { }

        /// <summary>
        /// Add a filter to a DirectShow Graph using its CLSID.
        /// </summary>
        /// <param name="graphBuilder">The IGraphBuilder interface of the graph.</param>
        /// <param name="clsid">A valid CLSID. This object must implement IBaseFilter.</param>
        /// <param name="name">The name used in the graph or null.</param>
        /// <returns>An instance of the filter if the method successfully created it or null if not.</returns>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static IBaseFilter AddFilterFromClsid(IGraphBuilder graphBuilder, Guid clsid, string name)
        {
            if (graphBuilder == null)
                throw new ArgumentNullException("graphBuilder");

            IBaseFilter result;
            try
            {
                Type type = Type.GetTypeFromCLSID(clsid);

                result = (IBaseFilter)Activator.CreateInstance(type);

                int hr = graphBuilder.AddFilter(result, name);
                DsError.ThrowExceptionForHR(hr);
            }
            catch (Exception)
            {
                result = null;
            }

            return result;
        }

        /// <summary>
        /// Remove and release all filters from a DirectShow Graph.
        /// </summary>
        /// <param name="graphBuilder">The IGraphBuilder interface of the graph.</param>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static void RemoveAllFilters(IGraphBuilder graphBuilder)
        {
            if (graphBuilder == null)
                throw new ArgumentNullException(nameof(graphBuilder));

            int hr = graphBuilder.EnumFilters(out IEnumFilters enumerator);
            DsError.ThrowExceptionForHR(hr);

            ArrayList filtersArray = new ArrayList();

            var values = new IBaseFilter[1];
            while (enumerator.Next(values.Length, values, IntPtr.Zero) == 0)
            {
                filtersArray.Add(values[0]);
            }

            foreach (IBaseFilter filter in filtersArray)
            {
                hr = graphBuilder.RemoveFilter(filter);
            }
        }

        /// <summary>
        /// Check if a COM Object is available.
        /// </summary>
        /// <param name="clsid">The CLSID of the object.</param>
        /// <returns>True if the object is available; false otherwise.</returns>
        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        public static bool IsThisComObjectInstalled(Guid clsid)
        {
            bool result = false;

            try
            {
                Type type = Type.GetTypeFromCLSID(clsid);
                object o = Activator.CreateInstance(type);
                result = true;
            }
            catch
            {
            }

            return result;
        }
    }
}
