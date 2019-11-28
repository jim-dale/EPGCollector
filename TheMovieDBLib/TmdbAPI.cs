////////////////////////////////////////////////////////////////////////////////// 
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

using System;
using System.Text;
using System.Net;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading;
using System.Runtime.Serialization.Json;
using System.Runtime.Serialization;
using System.Reflection;

namespace TheMovieDB
{
    /// <summary>
    /// The class that contains the Tmdb low level calls.
    /// </summary>
    public class TmdbAPI
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

        /// <summary>
        /// The async delegate.
        /// </summary>
        /// <param name="sender">The sender object.</param>
        /// <param name="e">The event parameters.</param>
        public delegate void TmdbAsyncHandler(object sender, TmdbAsyncEventArgs e);
        /// <summary>
        /// The process delegate.
        /// </summary>
        /// <param name="jsonString">The JSON text to be processed.</param>
        /// <returns>The deserialized object.</returns>
        public delegate object ProcessJsonString(string jsonString);

        /// <summary>
        /// Get the web response keys.
        /// </summary>
        public StringDictionary ResponseKeys { get; private set; }
        
        /// <summary>
        /// Get the minimum access time.
        /// </summary>
        public int MinimumAccessTime { get; private set; }
        /// <summary>
        /// Get the total number of requests.
        /// </summary>
        public int TotalRequestCount { get; private set; }
        /// <summary>
        /// Get the total request time.
        /// </summary>
        public TimeSpan? TotalRequestTime { get; private set; }
        /// <summary>
        /// Get the total number of delays.
        /// </summary>
        public int TotalDelays { get; private set; }
        /// <summary>
        /// Get the total delay time.
        /// </summary>
        public int TotalDelayTime { get; private set; }
        /// <summary>
        /// Get the total time between requests.
        /// </summary>
        public TimeSpan? TotalTimeBetweenRequests { get; private set; }
        /// <summary>
        /// Get the minimum time between requests.
        /// </summary>
        public TimeSpan? MinimumTimeBetweenRequests { get; private set; }
        /// <summary>
        /// Get the maximum time between requests.
        /// </summary>
        public TimeSpan? MaximumTimeBetweenRequests { get; private set; }

        private const string configurationUrl = "http://api.themoviedb.org/3/configuration?api_key={0}";

        private const string movieSearchUrl = "http://api.themoviedb.org/3/search/movie?api_key={0}&query={1}&page={2}&include_adult=true";
        private const string movieGetInfoUrl = "http://api.themoviedb.org/3/movie/{1}?api_key={0}";
        private const string movieGetCastUrl = "http://api.themoviedb.org/3/movie/{1}/casts?api_key={0}";
        private const string movieGetAlternativeTitlesUrl = "http://api.themoviedb.org/3/movie/{1}/alternative_titles?api_key={0}";
        private const string movieGetImagesUrl = "http://api.themoviedb.org/3/movie/{1}/images?api_key={0}";
        private const string movieGetKeywordsUrl = "http://api.themoviedb.org/3/movie/{1}/keywords?api_key={0}";
        private const string movieGetReleaseInfoUrl = "http://api.themoviedb.org/3/movie/{1}/releases?api_key={0}";
        private const string movieGetTrailersUrl = "http://api.themoviedb.org/3/movie/{1}/trailers?api_key={0}";
        private const string movieGetTranslationsUrl = "http://api.themoviedb.org/3/movie/{1}/translations?api_key={0}";

        private const string personSearchUrl = "http://api.themoviedb.org/3/search/person?api_key={0}&query={1}&page={2}&include_adult=true";
        private const string personGetInfoUrl = "http://api.themoviedb.org/3/person/{1}?api_key={0}";
        private const string personGetCreditsUrl = "http://api.themoviedb.org/3/person/{1}/credits?api_key={0}";
        private const string personGetImagesUrl = "http://api.themoviedb.org/3/person/{1}/images?api_key={0}";

        private const string collectionGetInfoUrl = "http://api.themoviedb.org/3/collection/{1}?api_key={0}";

        private const string companyGetInfoUrl = "http://api.themoviedb.org/3/company/{1}?api_key={0}";
        private const string companyMoviesUrl = "http://api.themoviedb.org/3/company/{1}/movies?api_key={0}&page={2}";

        /// <summary>
        /// Get the configuration record.
        /// </summary>
        public TmdbConfiguration Configuration
        {
            get
            {
                if (configuration == null)
                    configuration = getConfiguration();

                return configuration;
            }
        }

        /// <summary>Placeholder for missing property</summary>
        public string DefaultTimeout { get; set; }
        /// <summary>Placeholder for missing property</summary>
        public string ActualTimeout { get; set; }

        private string apiKey;

        private TmdbConfiguration configuration;

        private WebClient webClient;

        private TmdbAsyncHandler asyncHandler;

        private string searchTitle;
        private Collection<TmdbMovie> movies;

        private string searchName;
        private Collection<TmdbPerson> people;

        private int searchIdentity;
        private Collection<TmdbCompanyMovie> companyMovies;

        private DateTime? lastAccessTime;        

        private TmdbAPI() { }

        /// <summary>
        /// Initialize a new instance of the TmdbAPI class.
        /// </summary>
        /// <param name="apiKey">The API key.</param>
        public TmdbAPI(string apiKey) 
        {
            this.apiKey = apiKey;
            
            MinimumAccessTime = 260;
            TotalRequestTime = new TimeSpan();
            TotalTimeBetweenRequests = new TimeSpan();
        }

        /// <summary>
        /// Get all movies matching a search string..
        /// </summary>
        /// <param name="title">The title or partial title of the movie to search for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovieSearchResults GetMovies(string title, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            movies = new Collection<TmdbMovie>();            

            int pageNumber = 0;
            TmdbMovieSearchResults pageResults = null;
            int totalPages = -1;

            if (completionHandler == null)
            {
                do
                {
                    pageNumber++;
                    pageResults = GetMovies(title, pageNumber, null);

                    foreach (TmdbMovie movie in pageResults.Movies)
                        movies.Add(movie);

                    if (totalPages == -1)
                        totalPages = pageResults.TotalPages;
                }
                while (pageNumber < totalPages);

                TmdbMovieSearchResults returnResults = new TmdbMovieSearchResults();
                returnResults.Movies = new TmdbMovie[movies.Count];

                for (int index = 0; index < movies.Count; index++)
                    returnResults.Movies[index] = movies[index];

                returnResults.TotalResults = returnResults.Movies.Length;
                returnResults.TotalPages = pageNumber;

                return returnResults;
            }
            else
            {
                asyncHandler = completionHandler;
                searchTitle = title;
                GetMovies(title, 1, new TmdbAPI.TmdbAsyncHandler(movieSearchCompleted));
                return null;
            }
        }

        private void movieSearchCompleted(object sender, TmdbAsyncEventArgs e)
        {
            TmdbMovieSearchResults pageResults = e.ReplyObject as TmdbMovieSearchResults;

            foreach (TmdbMovie movie in pageResults.Movies)
                movies.Add(movie);

            if (movies.Count < pageResults.TotalResults)
            {
                GetMovies(searchTitle, pageResults.Page + 1, new TmdbAPI.TmdbAsyncHandler(movieSearchCompleted));
                return;
            }

            TmdbMovieSearchResults returnResults = new TmdbMovieSearchResults();
            returnResults.Movies = new TmdbMovie[movies.Count];

            for (int index = 0; index < movies.Count; index++)
                returnResults.Movies[index] = movies[index];

            asyncHandler(null, new TmdbAsyncEventArgs(returnResults));
        }

        /// <summary>
        /// Get a page of movie titles given a search string.
        /// </summary>
        /// <param name="title">The title of the movie to search for.</param>
        /// <param name="page">The page number.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovieSearchResults GetMovies(string title, int page, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieSearchUrl, apiKey, escapeQueryString(title), page);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString  = new ProcessJsonString(movieSearchResponse);

            return (TmdbMovieSearchResults)getData(url, new TmdbDelegates(asyncHandler, processString));            
        }

        private object movieSearchResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbMovieSearchResults));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            try
            {
                return serializer.ReadObject(stream) as TmdbMovieSearchResults;
            }
            catch (SerializationException)
            {
                TmdbMovieSearchResults exceptionResults = new TmdbMovieSearchResults();
                exceptionResults.Movies = new TmdbMovie[0];
                return exceptionResults;
            }
        }
        
        /// <summary>
        /// Retrieve specific information about a movie. Things like overview, release date, cast data, genre's, YouTube trailer link, etc...
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovie GetMovieInfo(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetInfoUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieInfoResponse);

            return (TmdbMovie)getData(url, new TmdbDelegates(asyncHandler, processString));    
        }

        private object getMovieInfoResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbMovie));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbMovie; 
        }

        /// <summary>
        /// Retrieve alternative titles for a movie.
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbAlternativeTitles GetMovieAlternativeTitles(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetAlternativeTitlesUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieAlternativeTitlesResponse);

            return (TmdbAlternativeTitles)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getMovieAlternativeTitlesResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbAlternativeTitles));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbAlternativeTitles;
        }

        /// <summary>
        /// Retrieve cast information about a movie.
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbCast GetMovieCast(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetCastUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieCastResponse);

            return (TmdbCast)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getMovieCastResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbCast));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbCast; 
        }

        /// <summary>
        /// Retrieve images for a movie.
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovieImages GetMovieImages(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetImagesUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieImagesResponse);

            return (TmdbMovieImages)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getMovieImagesResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbMovieImages));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbMovieImages;
        }

        /// <summary>
        /// Retrieve keywords for a movie.
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovieKeywords GetMovieKeywords(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetKeywordsUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieKeywordsResponse);

            return (TmdbMovieKeywords)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getMovieKeywordsResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbMovieKeywords));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbMovieKeywords;
        }

        /// <summary>
        /// Retrieve release info for a movie.
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovieReleaseInfo GetMovieReleaseInfo(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetReleaseInfoUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieReleaseInfoResponse);

            return (TmdbMovieReleaseInfo)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getMovieReleaseInfoResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbMovieReleaseInfo));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbMovieReleaseInfo;
        }

        /// <summary>
        /// Retrieve trailers for a movie.
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovieTrailers GetMovieTrailers(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetTrailersUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieTrailersResponse);

            return (TmdbMovieTrailers)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getMovieTrailersResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbMovieTrailers));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbMovieTrailers;            
        }

        /// <summary>
        /// Retrieve translations for a movie.
        /// </summary>
        /// <param name="id">The ID of the TMDb movie you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbMovieTranslations GetMovieTranslations(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(movieGetTranslationsUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getMovieTranslationsResponse);

            return (TmdbMovieTranslations)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getMovieTranslationsResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbMovieTranslations));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbMovieTranslations;
        }

        /// <summary>
        /// Get all people matching a search string.
        /// </summary>
        /// <param name="name">The name or partial name of the person to search for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbPersonSearchResults GetPeople(string name, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            people = new Collection<TmdbPerson>();

            int pageNumber = 0;
            TmdbPersonSearchResults pageResults = null;

            if (completionHandler == null)
            {
                do
                {
                    pageNumber++;
                    pageResults = GetPeople(name, pageNumber, null);

                    foreach (TmdbPerson person in pageResults.People)
                        people.Add(person);
                }
                while (pageNumber < pageResults.TotalPages);

                TmdbPersonSearchResults returnResults = new TmdbPersonSearchResults();
                returnResults.People = new TmdbPerson[people.Count];

                for (int index = 0; index < people.Count; index++)
                    returnResults.People[index] = people[index];

                returnResults.TotalResults = returnResults.People.Length;
                returnResults.TotalPages = pageNumber;

                return returnResults;
            }
            else
            {
                asyncHandler = completionHandler;
                searchName = name;
                GetPeople(name, 1, new TmdbAPI.TmdbAsyncHandler(personSearchCompleted));
                return null;
            }
        }

        private void personSearchCompleted(object sender, TmdbAsyncEventArgs e)
        {
            TmdbPersonSearchResults pageResults = e.ReplyObject as TmdbPersonSearchResults;

            foreach (TmdbPerson person in pageResults.People)
                people.Add(person);

            if (people.Count < pageResults.TotalResults)
            {
                GetPeople(searchName, pageResults.Page + 1, new TmdbAPI.TmdbAsyncHandler(personSearchCompleted));
                return;
            }

            TmdbPersonSearchResults returnResults = new TmdbPersonSearchResults();
            returnResults.People = new TmdbPerson[people.Count];

            for (int index = 0; index < people.Count; index++)
                returnResults.People[index] = people[index];

            asyncHandler(null, new TmdbAsyncEventArgs(returnResults));
        }

        /// <summary>
        /// Search for a person.
        /// </summary>
        /// <param name="name">The name of the person you are searching for.</param>
        /// <param name="page">The page number.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbPersonSearchResults GetPeople(string name, int page, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(personSearchUrl, apiKey, escapeQueryString(name), page);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(personSearchResponse);

            return (TmdbPersonSearchResults)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object personSearchResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbPersonSearchResults));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbPersonSearchResults; 
        }

        /// <summary>
        /// Retrieve the full filmography.
        /// </summary>
        /// <param name="id">The ID of the TMDb person you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbPerson GetPersonInfo(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(personGetInfoUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getPersonInfoResponse);

            return (TmdbPerson)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getPersonInfoResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbPerson));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbPerson;
        }

        /// <summary>
        /// Retrieve the credits for a person.
        /// </summary>
        /// <param name="id">The ID of the TMDb person you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbPersonCredits GetPersonCredits(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(personGetCreditsUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getPersonCreditsResponse);

            return (TmdbPersonCredits)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getPersonCreditsResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbPersonCredits));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbPersonCredits;
        }

        /// <summary>
        /// Retrieve the images for a person.
        /// </summary>
        /// <param name="id">The ID of the TMDb person you are searching for.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbPersonImages GetPersonImages(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(personGetImagesUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getPersonImagesResponse);

            return (TmdbPersonImages)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getPersonImagesResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbPersonImages));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbPersonImages;
        }

        /// <summary>
        /// Get the information for a film collection.
        /// </summary>
        /// <param name="id">The ID of the collection.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>A TmdbCollectionInfo instance.</returns>
        public TmdbCollectionInfo GetCollectionInfo(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(collectionGetInfoUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getCollectionInfoResponse);

            return (TmdbCollectionInfo)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getCollectionInfoResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbCollectionInfo));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbCollectionInfo;
        }

        /// <summary>
        /// Get the information for a company.
        /// </summary>
        /// <param name="id">The ID of the company.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>A TmdbCompanyInfo instance.</returns>
        public TmdbCompanyInfo GetCompanyInfo(int id, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(companyGetInfoUrl, apiKey, id);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(getCompanyInfoResponse);

            return (TmdbCompanyInfo)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object getCompanyInfoResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbCompanyInfo));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbCompanyInfo;
        }

        /// <summary>
        /// Get all movies for a company.
        /// </summary>
        /// <param name="identity">The identity of the company.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns></returns>
        public TmdbCompanyMovies CompanyMovieSearch(int identity, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            companyMovies = new Collection<TmdbCompanyMovie>();

            int pageNumber = 0;
            TmdbCompanyMovies pageResults = null;

            if (completionHandler == null)
            {
                do
                {
                    pageNumber++;
                    pageResults = CompanyMovieSearch(identity, pageNumber, null);

                    foreach (TmdbCompanyMovie movie in pageResults.Movies)
                        companyMovies.Add(movie);
                }
                while (companyMovies.Count < pageResults.Total_Results);

                TmdbCompanyMovies returnResults = new TmdbCompanyMovies();
                returnResults.Movies = new TmdbCompanyMovie[companyMovies.Count];

                for (int index = 0; index < companyMovies.Count; index++)
                    returnResults.Movies[index] = companyMovies[index];

                return returnResults;
            }
            else
            {
                asyncHandler = completionHandler;
                searchIdentity = identity;
                CompanyMovieSearch(identity, 1, new TmdbAPI.TmdbAsyncHandler(companyMovieSearchCompleted));
                return null;
            }
        }

        private void companyMovieSearchCompleted(object sender, TmdbAsyncEventArgs e)
        {
            TmdbCompanyMovies pageResults = e.ReplyObject as TmdbCompanyMovies;

            foreach (TmdbCompanyMovie movie in pageResults.Movies)
                companyMovies.Add(movie);

            if (companyMovies.Count < pageResults.Total_Results)
            {
                CompanyMovieSearch(searchIdentity, pageResults.Page + 1, new TmdbAPI.TmdbAsyncHandler(companyMovieSearchCompleted));
                return;
            }

            TmdbCompanyMovies returnResults = new TmdbCompanyMovies();
            returnResults.Movies = new TmdbCompanyMovie[companyMovies.Count];

            for (int index = 0; index < companyMovies.Count; index++)
                returnResults.Movies[index] = companyMovies[index];

            asyncHandler(null, new TmdbAsyncEventArgs(returnResults));
        }

        /// <summary>
        /// Get a page of movie titles for a company.
        /// </summary>
        /// <param name="identity">The identity of the company to search for.</param>
        /// <param name="page">The page number.</param>
        /// <param name="completionHandler">The async completion handler. Can be null.</param>
        /// <returns>The results object.</returns>
        public TmdbCompanyMovies CompanyMovieSearch(int identity, int page, TmdbAsyncHandler completionHandler)
        {
            initializeFunction();

            string url = string.Format(companyMoviesUrl, apiKey, identity, page);
            TmdbAsyncHandler asyncHandler = completionHandler;
            ProcessJsonString processString = new ProcessJsonString(companyMoviesResponse);

            return (TmdbCompanyMovies)getData(url, new TmdbDelegates(asyncHandler, processString));
        }

        private object companyMoviesResponse(string jsonString)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbCompanyMovies));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbCompanyMovies;
        }

        /// <summary>
        /// Get an image.
        /// </summary>
        /// <param name="imageType">The type of image.</param>
        /// <param name="filePath">The source path.</param>
        /// <param name="size">The size of the image.</param>
        /// <param name="outputPath">The output path.</param>
        /// <returns>True if the image is downloaded; flase otherwise.</returns>
        public bool GetImage(ImageType imageType, string filePath, int size, string outputPath)
        {
            string url;

            switch (imageType)
            {
                case ImageType.Backdrop:
                    if (size != -1)
                        url = Configuration.Images.BaseUrl + Configuration.Images.BackdropSizes[size];
                    else
                        url = Configuration.Images.BaseUrl + Configuration.Images.OriginalSize;
                    break;
                case ImageType.Poster:
                    if (size != -1)
                        url = Configuration.Images.BaseUrl + Configuration.Images.PosterSizes[size];
                    else
                        url = Configuration.Images.BaseUrl + Configuration.Images.OriginalSize;
                    break;
                case ImageType.Profile:
                    if (size != -1)
                        url = Configuration.Images.BaseUrl + Configuration.Images.ProfileSizes[size];
                    else
                        url = Configuration.Images.BaseUrl + Configuration.Images.OriginalSize;
                    break;
                case ImageType.Logo:
                    url = Configuration.Images.BaseUrl;
                    break;
                default:
                    return false;
                    
            }

            string editedFilePath = (filePath.StartsWith("/") ? filePath.Substring(1) : filePath);
            string address;
            
            if (imageType != ImageType.Logo)
                address = url + @"/" + editedFilePath + "?api_key=" + apiKey;
            else
                address = editedFilePath + "?api_key=" + apiKey;
            
            WebClient webClient = new WebClient();

            try
            {
                checkRequestRate();
                
                webClient.DownloadFile(address, outputPath);

                TotalRequestCount++;
                TotalRequestTime += DateTime.Now - lastAccessTime.Value;
            }
            catch (WebException)
            {
                if (webClient.ResponseHeaders != null)
                {
                    ResponseKeys = new StringDictionary();

                    for (int index = 0; index < webClient.ResponseHeaders.Count; index++)
                    {
                        string header = webClient.ResponseHeaders.GetKey(index);
                        string[] values = webClient.ResponseHeaders.GetValues(index);

                        if (values != null)
                        {
                            foreach (string headerValue in values)
                                ResponseKeys.Add(header, headerValue);
                        }
                    }
                }

                webClient.Dispose();
                throw;
            }
            
            webClient.Dispose();

            return true;
        }

        private void initializeFunction()
        {
            if (apiKey == null)
                throw new InvalidOperationException("The API key has not been set");

            if (webClient != null && webClient.IsBusy)
                throw new InvalidOperationException("Request in progress");
        }

        private TmdbConfiguration getConfiguration()
        {
            if (apiKey == null)
                throw new InvalidOperationException("The API key has not been set");

            WebClient webClient = getWebClient();
            byte[] data = webClient.DownloadData(string.Format(configurationUrl, apiKey));

            string jsonString = Encoding.UTF8.GetString(data);

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(TmdbConfiguration));
            MemoryStream stream = new MemoryStream(Encoding.Unicode.GetBytes(jsonString));

            return serializer.ReadObject(stream) as TmdbConfiguration;
        }

        private string escapeQueryString(string inputString)
        {
            return Uri.EscapeDataString(inputString);
        }

        private object getData(string url, TmdbDelegates delegates)
        {
            WebClient webClient = getWebClient();

            if (delegates.AsyncHandler != null)
            {
                checkRequestRate();

                webClient.DownloadDataCompleted += new DownloadDataCompletedEventHandler(webClientDownloadDataCompleted);
                webClient.DownloadDataAsync(new Uri(url), delegates);
                return null;
            }
            else
            {
                try
                {
                    checkRequestRate();
                    
                    byte[] response = webClient.DownloadData(new Uri(url));

                    TotalRequestCount++;
                    TotalRequestTime += DateTime.Now - lastAccessTime.Value;

                    return delegates.JsonHandler(Encoding.UTF8.GetString(response));
                }
                catch (WebException)
                {
                    if (webClient.ResponseHeaders != null)
                    {
                        ResponseKeys = new StringDictionary();

                        for (int index = 0; index < webClient.ResponseHeaders.Count; index++)
                        {
                            string header = webClient.ResponseHeaders.GetKey(index);
                            string[] values = webClient.ResponseHeaders.GetValues(index);

                            if (values != null)
                            {
                                foreach (string headerValue in values)
                                    ResponseKeys.Add(header, headerValue);
                            }
                        }
                    }

                    throw;
                }
            }
        }

        private void webClientDownloadDataCompleted(object sender, DownloadDataCompletedEventArgs e)
        {
            TotalRequestCount++;
            TotalRequestTime += DateTime.Now - lastAccessTime.Value;

            TmdbDelegates delegates = e.UserState as TmdbDelegates;
            delegates.AsyncHandler(null, new TmdbAsyncEventArgs(delegates.JsonHandler(Encoding.UTF8.GetString(e.Result))));
        }

        private WebClient getWebClient()
        {
            if (webClient == null)
                webClient = new WebClient();

            webClient.Headers.Clear();
            webClient.Headers.Add("Accept", "application/json");            

            return webClient;
        }

        private void checkRequestRate()
        {
            if (lastAccessTime != null)
            {
                TimeSpan gap = DateTime.Now - lastAccessTime.Value;

                TotalTimeBetweenRequests += gap;
                if (MinimumTimeBetweenRequests == null || gap < MinimumTimeBetweenRequests.Value)
                    MinimumTimeBetweenRequests = gap;
                if (MaximumTimeBetweenRequests == null || gap > MaximumTimeBetweenRequests.Value)
                    MaximumTimeBetweenRequests = gap;

                if (gap.TotalMilliseconds < MinimumAccessTime)
                {
                    int waitTime = (int)(MinimumAccessTime - gap.TotalMilliseconds);

                    TotalDelays++;
                    TotalDelayTime+= waitTime;

                    Thread.Sleep(waitTime);
                }
            }

            lastAccessTime = DateTime.Now;
        }
    }
}
