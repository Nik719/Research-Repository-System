using Newtonsoft.Json.Linq;
using RestSharp;
using System.Text.RegularExpressions;
using WebApplication1.Models;

namespace WebApplication1.Service
{
    public class ScraperService
    {
        private readonly string _apiKey = "42b5a9a232599ef75ffd652db1e0fd240a1bed7c5f84aac62cc319c73b1b095d";
        private readonly ILogger<ScraperService> _logger;

        public ScraperService(ILogger<ScraperService> logger)
        {
            _logger = logger;
        }

        public List<ResearchWork> ScrapeResearchDataByAuthor(string authorName, int maxPages = 5, int numResultsPerPage = 10)
        {
            return ScrapeResearchData("author", authorName, maxPages, numResultsPerPage);
        }

        public List<ResearchWork> ScrapeResearchDataByTitle(string title, int maxPages = 5, int numResultsPerPage = 10)
        {
            return ScrapeResearchData("title", title, maxPages, numResultsPerPage);
        }

        private List<ResearchWork> ScrapeResearchData(string searchType, string query, int maxPages, int numResultsPerPage)
        {
            var allResearchWorks = new List<ResearchWork>();

            try
            {
                var client = new RestClient("https://serpapi.com/search");

                for (int page = 1; page <= maxPages; page++)
                {
                    var request = new RestRequest
                    {
                        Method = Method.Get
                    };

                    request.AddParameter("engine", "google_scholar");
                    request.AddParameter("q", query);
                    request.AddParameter("api_key", _apiKey);
                    request.AddParameter("num", numResultsPerPage);
                    request.AddParameter("start", (page - 1) * numResultsPerPage);

                    _logger.LogInformation("Sending request to SerpAPI. Query: {Query}, Search Type: {SearchType}, Page: {Page}", query, searchType, page);

                    var response = client.Execute(request);

                    if (!response.IsSuccessful)
                    {
                        _logger.LogError("Failed to retrieve data. Status Code: {StatusCode}, Content: {Content}", response.StatusCode, response.Content);
                        throw new Exception("Failed to retrieve data from SerpAPI.");
                    }

                    _logger.LogInformation("Received JSON response for page {Page}: {JsonResponse}", page, response.Content);

                    var jsonData = JObject.Parse(response.Content);
                    var results = jsonData["organic_results"];

                    if (results == null || !results.HasValues)
                    {
                        _logger.LogWarning("No results found in the JSON response. Query: {Query}, Page: {Page}", query, page);
                        break;
                    }

                    foreach (var result in results)
                    {
                        try
                        {
                            var authors = result["publication_info"]?["authors"];
                            string authorNames = authors != null
                                ? string.Join(", ", authors.Select(author => author["name"]?.ToString() ?? "Unknown"))
                                : "Unknown";

                            string summary = result["publication_info"]?["summary"]?.ToString() ?? string.Empty;
                            DateTime? publicationDate = ExtractPublicationDate(summary);

                            var researchWork = new ResearchWork
                            {
                                Title = result["title"]?.ToString() ?? "No title available",
                                AuthorName = authorNames,
                                Abstract = result["snippet"]?.ToString() ?? "No abstract available",
                                SourceUrl = result["link"]?.ToString(),
                                PublicationDate = publicationDate
                            };

                            allResearchWorks.Add(researchWork);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing result on page {Page}", page);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while scraping data. Query: {Query}, Search Type: {SearchType}", query, searchType);
                throw;
            }

            return allResearchWorks;
        }

        public List<ResearchWork> FilterByDate(List<ResearchWork> researchWorks, DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue)
            {
                researchWorks = researchWorks.Where(work => work.PublicationDate >= startDate).ToList();
            }

            if (endDate.HasValue)
            {
                researchWorks = researchWorks.Where(work => work.PublicationDate <= endDate).ToList();
            }

            return researchWorks;
        }

        private DateTime? ExtractPublicationDate(string summary)
        {
            string pattern = @"\b(\d{4})\b";
            var match = Regex.Match(summary, pattern);

            if (match.Success)
            {
                if (int.TryParse(match.Value, out int year))
                {
                    return new DateTime(year, 1, 1);
                }
            }

            return null;
        }
    }
}