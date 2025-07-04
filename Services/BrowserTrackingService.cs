using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows.Automation;

namespace chronos_screentime.Services
{
    public class BrowserTrackingService
    {
        public class BrowserInfo
        {
            public string Domain { get; set; } = string.Empty;
            public string FullUrl { get; set; } = string.Empty;
            public string BrowserName { get; set; } = string.Empty;
            public bool IsValid { get; set; } = false;
        }

        public BrowserInfo? GetCurrentBrowserInfo(string processName)
        {
            try
            {
                if (!IsSupportedBrowser(processName))
                    return null;

                return processName.ToLower() switch
                {
                    "msedge" => GetEdgeBrowserInfo(),
                    "chrome" => GetChromeBrowserInfo(),
                    "firefox" => GetFirefoxBrowserInfo(),
                    _ => null
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting browser info: {ex.Message}");
                return null;
            }
        }

        private static bool IsSupportedBrowser(string processName)
        {
            var supportedBrowsers = new[] { "msedge", "chrome", "firefox" };
            return Array.Exists(supportedBrowsers, browser => 
                browser.Equals(processName, StringComparison.OrdinalIgnoreCase));
        }

        private BrowserInfo? GetEdgeBrowserInfo()
        {
            try
            {
                // Find the Edge process
                var edgeProcesses = Process.GetProcessesByName("msedge");
                if (edgeProcesses.Length == 0)
                    return null;

                foreach (var process in edgeProcesses)
                {
                    try
                    {
                        var windowHandle = process.MainWindowHandle;
                        if (windowHandle == IntPtr.Zero)
                            continue;

                        var element = AutomationElement.FromHandle(windowHandle);
                        if (element == null)
                            continue;

                        // Try to find the URL/address bar
                        var urlElement = FindUrlElement(element);
                        if (urlElement != null)
                        {
                            var url = GetUrlFromElement(urlElement);
                            if (!string.IsNullOrEmpty(url) && IsValidUrl(url))
                            {
                                return new BrowserInfo
                                {
                                    Domain = ExtractDomain(url),
                                    FullUrl = url,
                                    BrowserName = "Microsoft Edge",
                                    IsValid = true
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing Edge window: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetEdgeBrowserInfo: {ex.Message}");
            }

            return null;
        }

        private BrowserInfo? GetChromeBrowserInfo()
        {
            try
            {
                // Similar implementation for Chrome
                var chromeProcesses = Process.GetProcessesByName("chrome");
                if (chromeProcesses.Length == 0)
                    return null;

                foreach (var process in chromeProcesses)
                {
                    try
                    {
                        var windowHandle = process.MainWindowHandle;
                        if (windowHandle == IntPtr.Zero)
                            continue;

                        var element = AutomationElement.FromHandle(windowHandle);
                        if (element == null)
                            continue;

                        var urlElement = FindUrlElement(element);
                        if (urlElement != null)
                        {
                            var url = GetUrlFromElement(urlElement);
                            if (!string.IsNullOrEmpty(url) && IsValidUrl(url))
                            {
                                return new BrowserInfo
                                {
                                    Domain = ExtractDomain(url),
                                    FullUrl = url,
                                    BrowserName = "Google Chrome",
                                    IsValid = true
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing Chrome window: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetChromeBrowserInfo: {ex.Message}");
            }

            return null;
        }

        private BrowserInfo? GetFirefoxBrowserInfo()
        {
            try
            {
                // Similar implementation for Firefox
                var firefoxProcesses = Process.GetProcessesByName("firefox");
                if (firefoxProcesses.Length == 0)
                    return null;

                foreach (var process in firefoxProcesses)
                {
                    try
                    {
                        var windowHandle = process.MainWindowHandle;
                        if (windowHandle == IntPtr.Zero)
                            continue;

                        var element = AutomationElement.FromHandle(windowHandle);
                        if (element == null)
                            continue;

                        var urlElement = FindUrlElement(element);
                        if (urlElement != null)
                        {
                            var url = GetUrlFromElement(urlElement);
                            if (!string.IsNullOrEmpty(url) && IsValidUrl(url))
                            {
                                return new BrowserInfo
                                {
                                    Domain = ExtractDomain(url),
                                    FullUrl = url,
                                    BrowserName = "Mozilla Firefox",
                                    IsValid = true
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing Firefox window: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetFirefoxBrowserInfo: {ex.Message}");
            }

            return null;
        }

        private AutomationElement? FindUrlElement(AutomationElement root)
        {
            try
            {
                // Try multiple strategies to find the URL bar
                
                // Strategy 1: Find by ControlType.Edit and common names
                var condition = new AndCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new OrCondition(
                        new PropertyCondition(AutomationElement.NameProperty, "Address and search bar"),
                        new PropertyCondition(AutomationElement.NameProperty, "Address bar"),
                        new PropertyCondition(AutomationElement.NameProperty, "Search or enter address"),
                        new PropertyCondition(AutomationElement.AutomationIdProperty, "addressEditBox")
                    )
                );

                var urlElement = root.FindFirst(TreeScope.Descendants, condition);
                if (urlElement != null)
                    return urlElement;

                // Strategy 2: Look for elements with URL-like patterns in their value
                var editCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit);
                var editElements = root.FindAll(TreeScope.Descendants, editCondition);

                foreach (AutomationElement element in editElements)
                {
                    try
                    {
                        var value = GetUrlFromElement(element);
                        if (!string.IsNullOrEmpty(value) && IsValidUrl(value))
                        {
                            return element;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }

                // Strategy 3: Look in document elements (for some browsers)
                var documentCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document);
                var documentElements = root.FindAll(TreeScope.Descendants, documentCondition);

                foreach (AutomationElement element in documentElements)
                {
                    try
                    {
                        var name = element.Current.Name;
                        if (!string.IsNullOrEmpty(name) && IsValidUrl(name))
                        {
                            return element;
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding URL element: {ex.Message}");
            }

            return null;
        }

        private string GetUrlFromElement(AutomationElement element)
        {
            try
            {
                // Try to get value using different patterns
                
                // Try ValuePattern first
                if (element.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePattern))
                {
                    var value = ((ValuePattern)valuePattern).Current.Value;
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }

                // Try Name property
                var name = element.Current.Name;
                if (!string.IsNullOrEmpty(name))
                    return name;

                // Try AutomationElement properties as fallback
                try
                {
                    var helpText = element.Current.HelpText;
                    if (!string.IsNullOrEmpty(helpText) && IsValidUrl(helpText))
                        return helpText;
                }
                catch
                {
                    // Ignore if property is not supported
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting URL from element: {ex.Message}");
            }

            return string.Empty;
        }

        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            // Basic URL validation
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                   (url.Contains(".") && !url.Contains(" ") && url.Length > 3);
        }

        private static string ExtractDomain(string url)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url))
                    return string.Empty;

                // Clean up the URL
                url = url.Trim();

                // If URL doesn't start with a protocol, assume https
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                    !url.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://" + url;
                }

                // Parse the URI
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    var host = uri.Host.ToLower();
                    
                    // Remove www. prefix if present
                    if (host.StartsWith("www."))
                    {
                        host = host[4..];
                    }

                    return host;
                }

                // Fallback: use regex to extract domain
                var regex = new Regex(@"(?:https?://)?(?:www\.)?([^/\s]+)", RegexOptions.IgnoreCase);
                var match = regex.Match(url);
                if (match.Success && match.Groups.Count > 1)
                {
                    var domain = match.Groups[1].Value.ToLower();
                    if (domain.StartsWith("www."))
                    {
                        domain = domain[4..];
                    }
                    return domain;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting domain from URL '{url}': {ex.Message}");
            }

            return string.Empty;
        }

        public static string NormalizeDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain))
                return string.Empty;

            domain = domain.ToLower().Trim();
            
            // Remove www. prefix
            if (domain.StartsWith("www."))
            {
                domain = domain[4..];
            }

            // Remove trailing slash
            if (domain.EndsWith("/"))
            {
                domain = domain[..^1];
            }

            return domain;
        }
    }
} 