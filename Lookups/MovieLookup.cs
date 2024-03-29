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

using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Xml;
using System.Globalization;
using System.Text;

using DomainObjects;

using TheMovieDB;

namespace Lookups
{
    internal class MovieLookup
    {
        private const string DefaultFileName = "Movie Database.xml";

        internal int WebLookups { get; private set; }
        internal int InStoreLookups { get; private set; }

        internal Collection<string> UnusedPosters { get; private set; }

        internal bool Initialized { get; private set; }

        private int noData;
        private int outstanding;

        private Collection<MovieEntry> movies;

        private int webExceptionCount;
        private bool webException;
        private int webErrors;
        private int duplicatesDeleted;
        private int thresholdExclusions;

        private DateTime startTime;

        private TmdbAPI apiInstance;

        private int referencedPosters = 0;
        private int unmatchedDeleted = 0;
        private int unmatchedNotDeleted = 0;

        internal MovieLookup()
        {
            try
            {
                apiInstance = new TmdbAPI("b5410cd85abf11ab7e32d6addd5d5963");
                Initialized = true;
            }
            catch (Exception ex)
            {
                Logger.Instance.Write("<e> An exception of type " + ex.GetType().Name + " has occured while connecting to TMDB");
                Logger.Instance.Write("<e> " + ex.Message);
                Logger.Instance.Write("<e> No movie metadata available");
                return;
            }

            LoadMovieDatabase();

            startTime = DateTime.Now;
        }

        private void LoadMovieDatabase()
        {
            movies = new Collection<MovieEntry>();

            if (RunParameters.Instance.LookupReload)
            {
                ClearExistingData();
                return;
            }

            var path = Path.Combine(RunParameters.DataDirectory, DefaultFileName);
            if (File.Exists(path) == false)
            {
                return;
            }

            Logger.Instance.Write("Loading movie database from " + path);
            int notFoundCount = 0;

            try
            {
                using (var reader = XmlReader.Create(path, new XmlReaderSettings { IgnoreWhitespace = true }))
                {
                    while (reader.EOF == false)
                    {
                        reader.Read();
                        if (reader.IsStartElement())
                        {
                            switch (reader.Name.ToLowerInvariant())
                            {
                                case "movie":
                                    var movie = new MovieEntry(reader.GetAttribute("title"), reader.GetAttribute("metaDataTitle"));
                                    movie.Status = reader.GetAttribute("status");
                                    movie.Load(reader.ReadSubtree());

                                    movies.Add(movie);
                                    if (movie.Status == "notfound")
                                    {
                                        notFoundCount++;
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
            catch (XmlException ex)
            {
                Logger.Instance.Write("Failed to load file " + path);
                Logger.Instance.Write("Data exception: " + ex.Message);
            }
            catch (IOException ex)
            {
                Logger.Instance.Write("Failed to load file " + path);
                Logger.Instance.Write("I/O exception: " + ex.Message);
            }

            Logger.Instance.Write("Loaded " + movies.Count + " movies (not found = " + notFoundCount + ")");
        }

        private void AddMovie(MovieEntry newEntry)
        {
            foreach (var item in movies)
            {
                if (item.Title == newEntry.Title && item.MetaDataTitle == newEntry.MetaDataTitle)
                {
                    if (TraceEntry.IsDefined(TraceName.Lookups))
                    {
                        Logger.Instance.Write("Duplicate movie database entry ignored for " + newEntry.ToString());
                    }

                    duplicatesDeleted++;
                    return;
                }
            }

            movies.Add(newEntry);
        }

        private void ClearExistingData()
        {
            Logger.Instance.Write("Clearing existing movie data");

            var path = Path.Combine(RunParameters.DataDirectory, DefaultFileName);

            try
            {
                File.Delete(path);
                Logger.Instance.Write("Movie database deleted");
            }
            catch (IOException ex)
            {
                Logger.Instance.Write("Failed to delete " + path);
                Logger.Instance.Write("I/O exception: " + ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Instance.Write("Failed to delete " + path);
                Logger.Instance.Write("I/O exception: " + ex.Message);
            }

            if (Directory.Exists(RunParameters.ImagePath))
            {
                var directory = new DirectoryInfo(RunParameters.ImagePath);
                var files = directory.GetFiles("*.jpg", SearchOption.TopDirectoryOnly);

                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file.FullName);
                    }
                    catch (IOException e)
                    {
                        Logger.Instance.Write("Failed to delete " + file.FullName);
                        Logger.Instance.Write("I/O exception: " + e.Message);
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Logger.Instance.Write("Failed to delete " + file.FullName);
                        Logger.Instance.Write("I/O exception: " + e.Message);
                    }
                }
            }

            LookupController.ClearPosterDirectory(RunParameters.ImagePath);
            LookupController.ClearPosterDirectory(Path.Combine(RunParameters.ImagePath, "Movies"));

            Logger.Instance.Write("Existing movie data cleared");
        }

        internal LookupController.LookupReply Process(EPGEntry epgEntry)
        {
            bool isMovie = CheckForMovie(epgEntry);
            if (!isMovie)
                return LookupController.LookupReply.NotMovie;

            if (epgEntry.EventName == null || epgEntry.ShortDescription == null)
                return LookupController.LookupReply.NoData;

            if (TraceEntry.IsDefined(TraceName.LookupName))
            {
                TraceEntry traceEntry = TraceEntry.FindEntry(TraceName.LookupName, true);

                if (traceEntry != null && traceEntry.StringParameterSet)
                {
                    if (epgEntry.EventName != traceEntry.StringParameter)
                        return LookupController.LookupReply.NoData;
                }
            }

            var searchTitle = GetSearchNameAndDate(epgEntry.EventName, out string searchDate);
            if (epgEntry.Date != null)
                searchDate = epgEntry.Date;

            if (TraceEntry.IsDefined(TraceName.Lookups))
            {
                Logger.Instance.Write("Processing movie " + epgEntry.EventName +
                    (searchDate != null ? " from " + searchDate : string.Empty));
            }
            if (RunParameters.Instance.LookupNotMovie != null)
            {
                string lowerCaseTitle = searchTitle.ToLowerInvariant();

                foreach (var notMovie in RunParameters.Instance.LookupNotMovie)
                {
                    if (notMovie.ToLowerInvariant() == lowerCaseTitle)
                    {
                        if (TraceEntry.IsDefined(TraceName.Lookups))
                            Logger.Instance.Write("The programme " + searchTitle + " is defined by the user as not a movie");
                        return LookupController.LookupReply.NotMovie;
                    }
                }
            }

            var existingMovie = FindMovie(searchTitle, searchDate);
            if (existingMovie != null)
            {
                if (TraceEntry.IsDefined(TraceName.Lookups))
                    Logger.Instance.Write("Entry found in store for " + epgEntry.EventName + " - status is " + existingMovie.Status);

                if (existingMovie.Found)
                {
                    ProcessMovie(epgEntry, existingMovie);
                    InStoreLookups++;
                    return LookupController.LookupReply.InStore;
                }
                else
                {
                    if (existingMovie.UsedThisTime || !RunParameters.Instance.LookupNotFound)
                    {
                        existingMovie.UsedThisTime = true;
                        existingMovie.DateLastUsed = DateTime.Now;
                        noData++;
                        return LookupController.LookupReply.NoData;
                    }
                }

            }

            if (DateTime.Now >= startTime.AddMinutes(RunParameters.Instance.LookupTimeLimit))
            {
                if (TraceEntry.IsDefined(TraceName.Lookups))
                    Logger.Instance.Write("Lookups timed out - entry ignored");

                outstanding++;
                return LookupController.LookupReply.NotProcessed;
            }

            if (webException)
            {
                if (TraceEntry.IsDefined(TraceName.Lookups))
                    Logger.Instance.Write("Web exception limit reached (" + RunParameters.Instance.LookupErrorLimit + ") - entry ignored");

                outstanding++;
                return LookupController.LookupReply.NotProcessed;
            }

            try
            {
                var results = TmdbMovie.Search(apiInstance, searchTitle);
                webExceptionCount = 0;

                if (results.TotalResults == 0)
                {
                    if (existingMovie == null)
                        movies.Add(MovieEntry.CreateNotFoundEntry(searchTitle));
                    else
                    {
                        existingMovie.UsedThisTime = true;
                        existingMovie.DateLastUsed = DateTime.Now;
                    }

                    if (TraceEntry.IsDefined(TraceName.Lookups))
                        Logger.Instance.Write("No results for " + epgEntry.EventName);

                    noData++;
                    return LookupController.LookupReply.NoData;
                }

                if (TraceEntry.IsDefined(TraceName.Lookups))
                    Logger.Instance.Write("Retrieved " + results.TotalResults + " matches");

                var movie = FindMovieEntry(results.Movies, searchTitle, searchDate);
                if (movie != null)
                {
                    if (TraceEntry.IsDefined(TraceName.Lookups))
                        Logger.Instance.Write("Found " + movie.Title +
                            " from " + movie.ReleaseDate.Year);

                    var newMovie = CreateMovieEntry(searchTitle, movie, epgEntry.EventName);
                    if (newMovie != null)
                    {
                        ProcessMovie(epgEntry, newMovie);
                        WebLookups++;
                        return LookupController.LookupReply.WebLookup;
                    }
                    else
                    {
                        noData++;
                        return LookupController.LookupReply.NoData;
                    }
                }

                movies.Add(MovieEntry.CreateNotFoundEntry(searchTitle));

                if (TraceEntry.IsDefined(TraceName.Lookups))
                    Logger.Instance.Write("No matching result for " + epgEntry.EventName);

                noData++;
                return LookupController.LookupReply.NoData;
            }
            catch (WebException ex)
            {
                webErrors++;
                webExceptionCount++;

                if (webExceptionCount < RunParameters.Instance.LookupErrorLimit)
                {
                    Logger.Instance.Write("<e> Movie lookup has encountered an error when searching for '" + searchTitle + "'");
                    Logger.Instance.Write("<e> " + ex.Message);

                    if (DebugEntry.IsDefined(DebugName.LogResponseKeys) && apiInstance.ResponseKeys != null)
                    {
                        foreach (DictionaryEntry dictionaryEntry in apiInstance.ResponseKeys)
                        {
                            Logger.Instance.Write("Response key: " + dictionaryEntry.Key + " value: " + dictionaryEntry.Value);
                        }
                    }
                }
                else
                    webException = true;

                noData++;
                return LookupController.LookupReply.NoData;
            }
        }

        private bool CheckForMovie(EPGEntry epgEntry)
        {
            if (!RunParameters.Instance.LookupIgnoreCategories && epgEntry.EventCategory != null && epgEntry.EventCategory.GetDescription(EventCategoryMode.Xmltv).ToLowerInvariant().Contains("movie"))
                return true;

            if (RunParameters.Instance.LookupMoviePhrases != null && epgEntry.EventName != null)
            {
                foreach (string phrase in RunParameters.Instance.LookupMoviePhrases)
                {
                    if (epgEntry.EventName.ToLowerInvariant().Contains(phrase.ToLowerInvariant()))
                        return true;
                }
            }

            if (RunParameters.Instance.MovieLowTime != 0 || RunParameters.Instance.MovieHighTime != 0)
            {
                if (epgEntry.Duration.TotalMinutes >= RunParameters.Instance.MovieLowTime && epgEntry.Duration.TotalMinutes <= RunParameters.Instance.MovieHighTime)
                    return true;
            }

            return false;
        }

        private string GetSearchNameAndDate(string eventName, out string searchDate)
        {
            searchDate = null;
            string searchName = eventName;

            if (eventName == null)
                return (searchName);

            int yearStart = eventName.IndexOf("(");
            if (yearStart != -1)
            {
                int yearEnd = eventName.IndexOf(")", yearStart);
                if (yearEnd != -1)
                {
                    if (yearEnd - yearStart == 5)
                    {
                        try
                        {
                            int year = Int32.Parse(eventName.Substring(yearStart + 1, 4));

                            if (year > 1900 && year < 2150)
                            {
                                searchDate = eventName.Substring(yearStart + 1, 4);
                                searchName = eventName.Remove(yearStart, 6).Trim();
                            }
                        }
                        catch (FormatException) { searchDate = null; }
                        catch (OverflowException) { searchDate = null; }
                    }
                }
            }

            if (RunParameters.Instance.LookupIgnoredPhrases != null)
            {
                foreach (string phrase in RunParameters.Instance.LookupIgnoredPhrases)
                    searchName = searchName.Replace(phrase.Trim(), "");
            }

            return (searchName.Trim());
        }

        private MovieEntry FindMovie(string title, string date)
        {
            if (title == null)
                return null;

            string noCaseTitle = title.ToLowerInvariant();

            foreach (var movie in movies)
            {
                if (movie.Title != null && movie.Title.ToLowerInvariant() == noCaseTitle)
                {
                    if (date == null)
                        return movie;
                    else
                    {
                        if (CheckYear(movie.ReleaseDate, date))
                            return movie;
                    }
                }
            }

            foreach (var movie in movies)
            {
                if (movie.Title != null && movie.Title.ToLowerInvariant() == noCaseTitle)
                {
                    if (movie.ReleaseDate == null)
                        return movie;
                }
            }

            return null;
        }

        private TmdbMovie FindMovieEntry(TmdbMovie[] movies, string title, string date)
        {
            string noCaseTitle = title.ToLowerInvariant();
            Collection<TmdbMovie> selectedList = new Collection<TmdbMovie>();

            foreach (TmdbMovie movie in movies)
            {
                if (movie != null && movie.Title != null)
                {
                    if (date == null)
                    {
                        if (movie.Title.ToLowerInvariant() == noCaseTitle)
                            return (movie);
                        else
                            selectedList.Add(movie);
                    }
                    else
                    {
                        if (CheckYear(movie.ReleaseDate.Year, date))
                        {
                            if (movie.Title.ToLowerInvariant() == noCaseTitle)
                                return (movie);
                            else
                                selectedList.Add(movie);
                        }
                    }
                }
            }

            if (selectedList.Count == 0)
                return (null);

            switch (RunParameters.Instance.LookupMatching)
            {
                case MatchMethod.Exact:
                    return (null);
                case MatchMethod.Contains:
                    TmdbMovie matchedEntry = null;

                    foreach (TmdbMovie movie in selectedList)
                    {
                        if (movie.Title.ToLowerInvariant().Contains(noCaseTitle))
                        {
                            if (matchedEntry == null)
                                matchedEntry = movie;
                            else
                            {
                                int lengthDiff1 = title.Length - matchedEntry.Title.Length;
                                if (lengthDiff1 < 0)
                                    lengthDiff1 *= -1;

                                int lengthDiff2 = title.Length - movie.Title.Length;
                                if (lengthDiff2 < 0)
                                    lengthDiff2 *= -1;

                                if (lengthDiff2 < lengthDiff1)
                                    matchedEntry = movie;
                            }
                        }
                    }

                    return (matchedEntry);

                case MatchMethod.Nearest:

                    Collection<string> titleList = new Collection<string>();

                    foreach (TmdbMovie movie in selectedList)
                    {
                        titleList.Add(movie.Title);

                        if (TraceEntry.IsDefined(TraceName.Lookups))
                            Logger.Instance.Write("Added to match list " + movie.Title + " from " + movie.ReleaseDate.Year);
                    }

                    MatchResult matchResult = LookupController.FindBestMatch(title, titleList);
                    if (RunParameters.Instance.LookupMatchThreshold == 0)
                        return (selectedList[matchResult.Index]);
                    else
                    {
                        if (RunParameters.Instance.LookupMatchThreshold >= matchResult.Rating)
                            return (selectedList[matchResult.Index]);
                        else
                        {
                            if (TraceEntry.IsDefined(TraceName.Lookups))
                                Logger.Instance.Write("Excluded nearest result - result rating was " + matchResult.Rating + " threshold is " + RunParameters.Instance.LookupMatchThreshold);
                            thresholdExclusions++;
                            return null;
                        }
                    }
                default:
                    return (null);
            }
        }

        private bool CheckYear(string movieYearStr, string yearStr)
        {
            if (movieYearStr == null)
                return false;

            if (int.TryParse(movieYearStr, out int year))
            {
                return CheckYear(year, yearStr);
            }

            return false;
        }

        private bool CheckYear(int movieYear, string yearStr)
        {
            if (yearStr == null)
                return true;

            if (int.TryParse(yearStr, out int year))
            {
                return (movieYear >= year - 1 && movieYear <= year + 1);
            }

            return false;
        }

        private MovieEntry CreateMovieEntry(string title, TmdbMovie movie, string originalTitle)
        {
            string posterPath = null;

            try
            {
                movie.LoadAllData(apiInstance);

                var movieEntry = new MovieEntry(title, movie.Title);
                movieEntry.OriginalTitle = originalTitle;
                movieEntry.Overview = movie.Overview;
                movieEntry.Cast = movie.Cast.CastNames;
                movieEntry.Directors = movie.Cast.DirectorNames;
                movieEntry.Producers = movie.Cast.ProducerNames;
                movieEntry.Writers = movie.Cast.WriterNames;
                movieEntry.StarRating = LookupController.GetStarRating(movie.VoteAverage);

                if (movie.Genres != null && movie.Genres.Length > 0)
                    movieEntry.Genre = movie.Genres[0].Name;

                movieEntry.ReleaseDate = movie.ReleaseDate.Year.ToString();

                if (RunParameters.Instance.DownloadMovieThumbnail != LookupImageType.None)
                {
                    string imageDirectory = (RunParameters.Instance.LookupImagesInBase ?
                        RunParameters.ImagePath :
                        Path.Combine(RunParameters.ImagePath, "Movies"));

                    if (Directory.Exists(imageDirectory) == false)
                    {
                        Directory.CreateDirectory(imageDirectory);
                    }

                    Guid imageGuid = Guid.NewGuid();

                    posterPath = (RunParameters.Instance.LookupImageNameTitle ?
                        Path.Combine(imageDirectory, RunParameters.GetLegalFileName(title, ' ') + ".jpg") :
                        Path.Combine(imageDirectory, imageGuid + ".jpg"));

                    bool imageLoaded = movie.GetPosterImage(apiInstance, posterPath);
                    if (imageLoaded)
                    {
                        movieEntry.Poster = imageGuid;
                        if (TraceEntry.IsDefined(TraceName.Lookups))
                            Logger.Instance.Write("Image downloaded to " + imageGuid);
                    }
                }

                webExceptionCount = 0;
                movies.Add(movieEntry);

                return movieEntry;
            }
            catch (Exception ex)
            {
                Logger.Instance.Write("<e> Movie lookup has encountered an exception of type " + ex.GetType().Name +
                    " when loading a poster image for '" + title + "'");
                Logger.Instance.Write("<e> " + ex.Message);

                if (DebugEntry.IsDefined(DebugName.LogResponseKeys) && apiInstance.ResponseKeys != null)
                {
                    foreach (DictionaryEntry dictionaryEntry in apiInstance.ResponseKeys)
                    {
                        Logger.Instance.Write("Response key: " + dictionaryEntry.Key + " value: " + dictionaryEntry.Value);
                    }
                }

                if (DebugEntry.IsDefined(DebugName.StackTrace))
                    Logger.Instance.Write("<e> " + ex.StackTrace);

                webErrors++;
                webExceptionCount++;

                if (webExceptionCount >= RunParameters.Instance.LookupErrorLimit)
                    webException = true;

                if (posterPath != null && File.Exists(posterPath))
                {
                    File.Delete(posterPath);
                }
                return null;
            }
        }

        private void ProcessMovie(EPGEntry epgEntry, MovieEntry movieEntry)
        {
            epgEntry.MetaDataTitle = movieEntry.MetaDataTitle;

            if (epgEntry.ShortDescription == null)
                epgEntry.ShortDescription = movieEntry.Overview;

            if (movieEntry.Cast != null && movieEntry.Cast.Count != 0)
                epgEntry.Cast = movieEntry.Cast;
            if (movieEntry.Producers != null && movieEntry.Producers.Count != 0)
                epgEntry.Producers = movieEntry.Producers;
            if (movieEntry.Directors != null && movieEntry.Directors.Count != 0)
                epgEntry.Directors = movieEntry.Directors;
            if (movieEntry.Writers != null && movieEntry.Writers.Count != 0)
                epgEntry.Writers = movieEntry.Writers;
            if (movieEntry.StarRating != null)
                epgEntry.StarRating = movieEntry.StarRating;

            CreateEventCategory(epgEntry, movieEntry);

            if (movieEntry.Poster != null)
                epgEntry.Poster = movieEntry.Poster;

            if (epgEntry.Date == null)
                epgEntry.Date = movieEntry.ReleaseDate;

            movieEntry.UsedThisTime = true;
            movieEntry.DateLastUsed = DateTime.Now;
        }

        private void CreateEventCategory(EPGEntry epgEntry, MovieEntry movieEntry)
        {
            if (string.IsNullOrWhiteSpace(movieEntry.Genre))
                return;
            if (!RunParameters.Instance.LookupIgnoreCategories && epgEntry.EventCategory != null)
                return;

            epgEntry.EventCategory = new EventCategorySpec("Movies," + movieEntry.Genre + ",isMovie");
        }

        internal void CreateMovieDatabase()
        {
            string databasePath = Path.Combine(RunParameters.DataDirectory, "Movie Database.xml");
            Logger.Instance.Write("Creating movie database " + databasePath);

            FileStream fileStream = new FileStream(databasePath, FileMode.Create);

            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.NewLineOnAttributes = false;
            settings.CloseOutput = true;


            int outputCount = 0;
            int outputNotFoundCount = 0;
            int entriesDeleted = 0;
            int postersDeleted = 0;

            var outputEntries = new Collection<MovieEntry>();

            try
            {
                XmlWriter writer = XmlWriter.Create(fileStream, settings);

                writer.WriteStartElement("movies");

                foreach (MovieEntry movieEntry in movies)
                {
                    if (movieEntry.UsedThisTime || (movieEntry.DateLastUsed != null && movieEntry.DateLastUsed.Value.AddDays(10) > DateTime.Now))
                    {
                        outputEntries.Add(movieEntry);
                        movieEntry.Unload(writer);

                        outputCount++;
                        if (movieEntry.Status == "notfound")
                            outputNotFoundCount++;
                    }
                    else
                    {
                        entriesDeleted++;

                        if (movieEntry.Poster.HasValue)
                        {
                            string posterPath = Path.Combine(RunParameters.ImagePath, "Movies", movieEntry.Poster + ".jpg");

                            try
                            {
                                File.Delete(posterPath);
                                postersDeleted++;
                            }
                            catch (IOException ex)
                            {
                                Logger.Instance.Write("<e>Failed to delete movie poster " + posterPath);
                                Logger.Instance.Write("<e> " + ex.Message);

                                posterPath = RunParameters.ImagePath;

                                try
                                {
                                    File.Delete(posterPath);
                                    postersDeleted++;
                                }
                                catch (IOException inner)
                                {
                                    Logger.Instance.Write("<e>Failed to delete movie poster " + posterPath);
                                    Logger.Instance.Write("<e> " + inner.Message);
                                }
                            }
                        }
                    }
                }

                writer.WriteEndElement();

                writer.Close();
            }
            catch (XmlException ex)
            {
                Logger.Instance.Write("Failed to unload Movie database");
                Logger.Instance.Write("Data exception: " + ex.Message);
                throw;
            }
            catch (IOException ex)
            {
                Logger.Instance.Write("Failed to unload Movie database");
                Logger.Instance.Write("I/O exception: " + ex.Message);
                throw;
            }

            Logger.Instance.Write("Output " + outputCount + " movie database entries (not found = " + outputNotFoundCount + ")");
            Logger.Instance.Write("Deleted " + duplicatesDeleted + " duplicate movie database entries");
            Logger.Instance.Write("Deleted " + entriesDeleted + " movie database entries no longer in use");
            Logger.Instance.Write("Deleted " + postersDeleted + " movie posters no longer in use");

            if (!RunParameters.Instance.LookupImagesInBase)
                CleanPosterDirectory(Path.Combine(RunParameters.ImagePath, "Movies"), outputEntries);
            else
                UnusedPosters = CollectUnusedPosters(RunParameters.ImagePath, outputEntries);

            Logger.Instance.Write("Referenced movie posters = " + referencedPosters);
            Logger.Instance.Write("Unreferenced movie posters deleted = " + unmatchedDeleted);
            Logger.Instance.Write("Unreferenced movie posters not deleted = " + unmatchedNotDeleted);
        }

        private void CleanPosterDirectory(string posterDirectory, Collection<MovieEntry> outputEntries)
        {
            if (Directory.Exists(posterDirectory) == false)
            {
                return;
            }

            var posterFiles = Directory.GetFiles(posterDirectory, "*.jpg", SearchOption.TopDirectoryOnly);
            foreach (string posterName in posterFiles)
            {
                bool found = false;

                FileInfo fileInfo = new FileInfo(posterName);

                foreach (MovieEntry seriesEntry in outputEntries)
                {
                    if (seriesEntry.Poster != null && seriesEntry.Poster.HasValue)
                    {
                        if (seriesEntry.Poster.Value + ".jpg" == fileInfo.Name)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(seriesEntry.OriginalTitle))
                    {
                        if (RunParameters.GetLegalFileName(seriesEntry.OriginalTitle, ' ') + ".jpg" == fileInfo.Name)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    try
                    {
                        File.Delete(fileInfo.FullName);
                        Logger.Instance.Write("Unreferenced movie poster deleted - " + posterName);
                        unmatchedDeleted++;
                    }
                    catch (IOException e)
                    {
                        Logger.Instance.Write("<e> Failed to delete unreferenced movie poster - " + posterName);
                        Logger.Instance.Write("<e> " + e.Message);
                        unmatchedNotDeleted++;
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        Logger.Instance.Write("<e> Failed to delete unreferenced movie poster - " + posterName);
                        Logger.Instance.Write("<e> " + e.Message);
                        unmatchedNotDeleted++;
                    }
                }
                else
                    referencedPosters++;
            }
        }

        private Collection<string> CollectUnusedPosters(string posterDirectory, Collection<MovieEntry> outputEntries)
        {
            Collection<string> unusedPosters = new Collection<string>();

            if (!Directory.Exists(posterDirectory))
                return (unusedPosters);

            var posterFiles = Directory.GetFiles(posterDirectory, "*.jpg", SearchOption.TopDirectoryOnly);

            foreach (string posterName in posterFiles)
            {
                bool found = false;

                var fileInfo = new FileInfo(posterName);

                foreach (var seriesEntry in outputEntries)
                {
                    if (seriesEntry.Poster != null && seriesEntry.Poster.HasValue)
                    {
                        if (seriesEntry.Poster.Value + ".jpg" == fileInfo.Name)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(seriesEntry.OriginalTitle))
                    {
                        if (RunParameters.GetLegalFileName(seriesEntry.OriginalTitle, ' ') + ".jpg" == fileInfo.Name)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                    unusedPosters.Add(fileInfo.Name);
                else
                    referencedPosters++;
            }

            return (unusedPosters);
        }

        internal void LogStats()
        {
            Logger.Instance.Write("Movie statistics: Web lookups = " + WebLookups +
                    " In-store lookups = " + InStoreLookups +
                    " No data = " + noData +
                    " Outstanding = " + outstanding +
                    " Threshold exclusions = " + thresholdExclusions);

            if (apiInstance.TotalRequestCount != 0)
            {
                Logger.Instance.Write("Movie statistics: Total web requests = " + apiInstance.TotalRequestCount +
                    " Total web request time = " + string.Format("{0:hh\\:mm\\:ss}", apiInstance.TotalRequestTime) +
                    " Average web request time = " + (apiInstance.TotalRequestTime.Value.TotalSeconds / apiInstance.TotalRequestCount).ToString("0.000") + " secs");

                Logger.Instance.Write("Movie statistics: Average time between requests = " + (apiInstance.TotalTimeBetweenRequests.Value.TotalSeconds / apiInstance.TotalRequestCount).ToString("0.000") + " secs" +
                    " Minimum time between requests = " + (apiInstance.MinimumTimeBetweenRequests != null ? (apiInstance.MinimumTimeBetweenRequests.Value.TotalMilliseconds / 1000).ToString("0.000") + " secs" : "n/a") +
                    " Maximum time between requests = " + (apiInstance.MaximumTimeBetweenRequests != null ? (apiInstance.MaximumTimeBetweenRequests.Value.TotalMilliseconds / 1000).ToString("0.000") + " secs" : "n/a"));

                if (apiInstance.TotalDelays != 0)
                    Logger.Instance.Write("Movie statistics: Total request delays = " + apiInstance.TotalDelays +
                        " Total request delay time = " + ((decimal)apiInstance.TotalDelayTime / 1000).ToString("0.000") + " secs" +
                        " Average request delay time = " + ((((decimal)apiInstance.TotalDelayTime) / apiInstance.TotalDelays) / 1000).ToString("0.000") + " secs");
                else
                    Logger.Instance.Write("Movie statistics: Total request delays = 0" +
                    " Total request delay time = 0.000 secs" +
                    " Average request delay time = 0.000 secs");
            }

            Logger.Instance.Write("Movie statistics: Web errors = " + webErrors +
                " default timeout = " + apiInstance.DefaultTimeout +
                " actual timeout = " + apiInstance.ActualTimeout);
        }

        private class MovieEntry
        {
            internal string Title { get; set; }
            internal string MetaDataTitle { get; set; }
            internal string Overview { get; set; }
            internal Collection<string> Cast { get; set; }
            internal Collection<string> Directors { get; set; }
            internal Collection<string> Producers { get; set; }
            internal Collection<string> Writers { get; set; }
            internal string StarRating { get; set; }
            internal Guid? Poster { get; set; }
            internal string Genre { get; set; }
            internal bool UsedThisTime { get; set; }
            internal string Status { get; set; }
            internal string ReleaseDate { get; set; }
            internal DateTime? DateLastUsed { get; set; }

            internal string OriginalTitle { get; set; }

            internal bool Found { get { return (Status != "notfound"); } }

            private MovieEntry()
            {
                Status = "found";
                DateLastUsed = DateTime.Now.AddDays(-1);
            }

            internal MovieEntry(string title, string metaDataTitle) : this()
            {
                Title = title;
                OriginalTitle = title;
                MetaDataTitle = metaDataTitle;
            }

            internal void Load(XmlReader reader)
            {
                while (!reader.EOF)
                {
                    reader.Read();
                    if (reader.IsStartElement())
                    {
                        switch (reader.Name.ToLowerInvariant())
                        {
                            case "overview":
                                Overview = reader.ReadString();
                                break;
                            case "actor":
                                if (Cast == null)
                                    Cast = new Collection<string>();
                                Cast.Add(reader.ReadString());
                                break;
                            case "producer":
                                if (Producers == null)
                                    Producers = new Collection<string>();
                                Producers.Add(reader.ReadString());
                                break;
                            case "director":
                                if (Directors == null)
                                    Directors = new Collection<string>();
                                Directors.Add(reader.ReadString());
                                break;
                            case "writer":
                                if (Writers == null)
                                    Writers = new Collection<string>();
                                Writers.Add(reader.ReadString());
                                break;
                            case "starrating":
                                StarRating = reader.ReadString();
                                break;
                            case "genre":
                                Genre = reader.ReadString();
                                break;
                            case "poster":
                                Poster = Guid.Parse(reader.ReadString());
                                break;
                            case "releasedate":
                                ReleaseDate = reader.ReadString();
                                break;
                            case "datelastused":
                                DateLastUsed = DateTime.Parse(reader.ReadString(), CultureInfo.InvariantCulture);
                                break;
                            case "originaltitle":
                                OriginalTitle = reader.ReadString();
                                break;
                            default:
                                break;
                        }
                    }
                }
            }

            internal void Unload(XmlWriter xmlWriter)
            {
                xmlWriter.WriteStartElement("movie");

                xmlWriter.WriteAttributeString("title", Title);
                xmlWriter.WriteAttributeString("metaDataTitle", MetaDataTitle);
                xmlWriter.WriteAttributeString("status", Status);

                if (Overview != null)
                    xmlWriter.WriteElementString("overview", Overview);

                if (Cast != null && Cast.Count != 0)
                {
                    xmlWriter.WriteStartElement("cast");
                    foreach (string actor in Cast)
                    {
                        xmlWriter.WriteElementString("actor", actor);
                    }
                    xmlWriter.WriteEndElement();
                }

                if (Producers != null && Producers.Count != 0)
                {
                    xmlWriter.WriteStartElement("producers");
                    foreach (string producer in Producers)
                    {
                        xmlWriter.WriteElementString("producer", producer);
                    }
                    xmlWriter.WriteEndElement();
                }

                if (Directors != null && Directors.Count != 0)
                {
                    xmlWriter.WriteStartElement("directors");
                    foreach (string director in Directors)
                    {
                        xmlWriter.WriteElementString("director", director);
                    }
                    xmlWriter.WriteEndElement();
                }

                if (Writers != null && Writers.Count != 0)
                {
                    xmlWriter.WriteStartElement("writers");
                    foreach (string writer in Writers)
                    {
                        xmlWriter.WriteElementString("writer", writer);
                    }
                    xmlWriter.WriteEndElement();
                }

                if (StarRating != null)
                    xmlWriter.WriteElementString("starrating", StarRating);

                if (Genre != null)
                    xmlWriter.WriteElementString("genre", Genre);

                if (Poster.HasValue)
                    xmlWriter.WriteElementString("poster", Poster.ToString());

                if (ReleaseDate != null)
                    xmlWriter.WriteElementString("releaseDate", ReleaseDate);

                if (DateLastUsed != null)
                    xmlWriter.WriteElementString("dateLastUsed", DateLastUsed.Value.ToString(CultureInfo.InvariantCulture));

                if (!string.IsNullOrWhiteSpace(OriginalTitle) && OriginalTitle != Title)
                    xmlWriter.WriteElementString("originalTitle", OriginalTitle);

                xmlWriter.WriteEndElement();
            }

            internal static MovieEntry CreateNotFoundEntry(string title)
            {
                var result = new MovieEntry(title, string.Empty)
                {
                    Status = "notfound",
                    UsedThisTime = true
                };

                return result;
            }

            public override string ToString()
            {
                return ("Title: " + Title + " Metadata title: " + MetaDataTitle);
            }
        }
    }
}
