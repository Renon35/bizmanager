using HtmlAgilityPack;
using System.Web;

namespace BizManager.Services;

public class ProductImageScraperService(HttpClient httpClient, IWebHostEnvironment env, ILogger<ProductImageScraperService> logger)
{
    public async Task<string?> ScrapeImageAsync(string brandDomain, string productCode)
    {
        try
        {
            // 1. DuckDuckGo Search Query
            string query = HttpUtility.UrlEncode($"site:{brandDomain} {productCode}");
            string searchUrl = $"https://html.duckduckgo.com/html/?q={query}";
            
            // Add custom Headers for DuckDuckGo
            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.5");

            // Fetch search results
            var searchResponse = await httpClient.SendAsync(request);
            if (!searchResponse.IsSuccessStatusCode)
            {
                logger.LogWarning($"DuckDuckGo search failed. Status: {searchResponse.StatusCode}");
                return null;
            }

            var searchHtml = await searchResponse.Content.ReadAsStringAsync();
            var searchDoc = new HtmlDocument();
            searchDoc.LoadHtml(searchHtml);
            
            await File.WriteAllTextAsync("wwwroot/search.html", searchHtml);

            string? productUrl = null;
            
            // DuckDuckGo HTML puts results in 'a.result__url' or general links
            var linkNodes = searchDoc.DocumentNode.SelectNodes("//a[@href]");

            if (linkNodes != null)
            {
                foreach (var link in linkNodes)
                {
                    string href = link.GetAttributeValue("href", string.Empty);
                    
                    if (href.Contains("uddg="))
                    {
                        // Extract original URL from DuckDuckGo redirect
                        try 
                        {
                            string uddgQuery = href.Split("uddg=")[1].Split("&")[0];
                            href = HttpUtility.UrlDecode(uddgQuery);
                        } 
                        catch { /* Ignore parse errors */ }
                    }
                    
                    if (href.StartsWith("//"))
                    {
                        href = "https:" + href;
                    }
                    
                    // Must belong to the brand and not be a DuckDuckGo internal URL
                    if (href.Contains(brandDomain) && !href.Contains("duckduckgo.com"))
                    {
                        productUrl = href;
                        break;
                    }
                }
            }
            
            // Fallback: search for porland.com inside raw HTML
            if (string.IsNullOrEmpty(productUrl))
            {
                var regex = new System.Text.RegularExpressions.Regex($@"https?://[a-zA-Z0-9.-]*{brandDomain.Replace(".", @"\.")}[^\""\'\s<>]+");
                var match = regex.Match(searchHtml);
                if (match.Success)
                {
                    productUrl = match.Value;
                }
            }

            if (string.IsNullOrEmpty(productUrl))
            {
                logger.LogWarning($"No valid product URL found on Google for domain {brandDomain} and code {productCode}. Using fallback direct URL strategy.");
                // Fallback: Best guess strategy for typical product URLs
                productUrl = $"https://www.{brandDomain}/arama?q={productCode}";
            }

            // 3. Visit that page
            var pageResponse = await httpClient.GetAsync(productUrl);
            if (!pageResponse.IsSuccessStatusCode)
            {
                logger.LogWarning($"Product page fetch failed. Status: {pageResponse.StatusCode}");
                return null;
            }

            var pageHtml = await pageResponse.Content.ReadAsStringAsync();
            var pageDoc = new HtmlDocument();
            pageDoc.LoadHtml(pageHtml);

            // 4. Extract first product image prioritizing og:image
            string? imageUrl = null;
            var ogImageNode = pageDoc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            if (ogImageNode != null)
            {
                imageUrl = ogImageNode.GetAttributeValue("content", string.Empty);
            }
            else
            {
                // Fallback: grab first reasonable img tag
                var imgNodes = pageDoc.DocumentNode.SelectNodes("//img[@src]");
                if (imgNodes != null)
                {
                    foreach(var img in imgNodes)
                    {
                        string src = img.GetAttributeValue("src", string.Empty);
                        // Skip suspicious non-product images
                        if (src.Contains("base64") || src.Contains("logo", StringComparison.OrdinalIgnoreCase) || src.Contains("icon", StringComparison.OrdinalIgnoreCase)) continue;
                        
                        if (src.StartsWith("//")) src = "https:" + src;
                        else if (src.StartsWith("/")) src = $"https://{brandDomain}{src}";
                        else if (!src.StartsWith("http")) src = $"https://{brandDomain}/{src}";
                        
                        imageUrl = src;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(imageUrl))
            {
                 logger.LogWarning($"No image found on product page {productUrl}.");
                 return null;
            }

            imageUrl = HttpUtility.HtmlDecode(imageUrl);

            // 5. Download and save locally
            var imageResponse = await httpClient.GetAsync(imageUrl);
            if (!imageResponse.IsSuccessStatusCode) return null;

            var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();
            if (imageBytes.Length == 0) return null;

            string fileName = $"{productCode}.jpg";
            fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars())); // Sanity check

            string uploadsFolder = Path.Combine(env.WebRootPath, "uploads", "products");
            Directory.CreateDirectory(uploadsFolder);
            
            string filePath = Path.Combine(uploadsFolder, fileName);
            await File.WriteAllBytesAsync(filePath, imageBytes);

            return $"/uploads/products/{fileName}";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to scrape image for {productCode} on {brandDomain}.");
            return null;
        }
    }
}
