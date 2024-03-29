﻿////////////////////////////////////////////////////////////////////////////////// 
//                                                                              //
//      Copyright © 2005-2016 nzsjb                                             //
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

namespace TheMovieDB
{
    /// <summary>
    /// The class that encapsulates the delegates needed for generic web access.
    /// </summary>
    internal class TmdbDelegates
    {
        /// <summary>
        /// Get or set the async handler delegate.
        /// </summary>
        internal TmdbAPI.TmdbAsyncHandler AsyncHandler { get; set; }
        
        /// <summary>
        /// The JSON string handling delegate.
        /// </summary>
        internal TmdbAPI.ProcessJsonString JsonHandler { get; set; }

        private TmdbDelegates() { }

        /// <summary>
        /// Initialize a new instance of the TmdbDelegates class.
        /// </summary>
        /// <param name="asyncHandler">The async handler delegate.</param>
        /// <param name="jsonHandler">The JSON string handler delegate.</param>
        internal TmdbDelegates(TmdbAPI.TmdbAsyncHandler asyncHandler, TmdbAPI.ProcessJsonString jsonHandler)
        {
            this.AsyncHandler = asyncHandler;
            this.JsonHandler = jsonHandler;
        }
    }
}
