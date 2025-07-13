using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace chronos_screentime.Services
{
    public class CategoryService
    {
        private readonly string _categoriesFilePath;
        private readonly Dictionary<string, string> _appCategories; // AppName -> Category
        private readonly Dictionary<string, string> _websiteCategories; // Domain -> Category
        private readonly List<string> _customCategories;
        private readonly List<string> _defaultCategories;
        private readonly Dictionary<string, List<string>> _categoryTemplates;
        private readonly Dictionary<string, List<string>> _websiteTemplates;

        public event EventHandler? CategoriesChanged;

        public CategoryService()
        {
            _appCategories = new Dictionary<string, string>();
            _websiteCategories = new Dictionary<string, string>();
            _customCategories = new List<string>();
            
            // Default categories that come with the app
            _defaultCategories = new List<string>
            {
                "WebBrowsing",
                "Development", 
                "Gaming",
                "Communication",
                "Productivity",
                "Entertainment",
                "Social Media",
                "Education",
                "Utilities",
                "Uncategorized"
            };

            // Initialize category templates with popular apps and websites
            _categoryTemplates = InitializeCategoryTemplates();
            _websiteTemplates = InitializeWebsiteTemplates();

            _categoriesFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChronosScreenTime",
                "categories.json"
            );

            LoadCategories();
        }

        private Dictionary<string, List<string>> InitializeCategoryTemplates()
        {
            return new Dictionary<string, List<string>>
            {
                ["Development"] = new List<string>
                {
                    "Visual Studio", "VS Code", "Visual Studio Code", "IntelliJ IDEA", "PyCharm", "WebStorm", "PhpStorm", "Android Studio",
                    "Eclipse", "NetBeans", "Sublime Text", "Atom", "Notepad++", "Brackets", "Cursor", "JetBrains", "Xcode",
                    "Unity", "Unreal Engine", "Blender", "Maya", "3ds Max", "Cinema 4D", "Houdini", "ZBrush",
                    "Git", "GitHub Desktop", "SourceTree", "TortoiseGit", "GitKraken", "Bitbucket", "GitLab",
                    "Docker", "Docker Desktop", "Kubernetes", "Postman", "Insomnia", "Fiddler", "Charles",
                    "Node.js", "npm", "yarn", "Python", "Java", "C#", "C++", "JavaScript", "TypeScript",
                    "Terminal", "Command Prompt", "PowerShell", "WSL", "Ubuntu", "Linux", "Bash", "Zsh"
                },
                ["Gaming"] = new List<string>
                {
                    "Steam", "Epic Games Launcher", "Origin", "Uplay", "Battle.net", "GOG Galaxy", "Xbox", "PlayStation",
                    "Valorant", "League of Legends", "Dota 2", "Counter-Strike", "CS:GO", "CS2", "Overwatch", "Fortnite",
                    "Minecraft", "Roblox", "Among Us", "Fall Guys", "PUBG", "Apex Legends", "Call of Duty", "FIFA",
                    "Grand Theft Auto", "GTA V", "GTA Online", "Red Dead Redemption", "The Witcher", "Skyrim", "Fallout",
                    "World of Warcraft", "Final Fantasy", "Assassin's Creed", "Far Cry", "Watch Dogs", "Cyberpunk",
                    "Discord", "TeamSpeak", "Mumble", "OBS Studio", "Streamlabs", "XSplit", "NVIDIA GeForce Experience",
                    "AMD Radeon Software", "MSI Afterburner", "EVGA Precision", "Razer Synapse", "Logitech G HUB"
                },
                ["WebBrowsing"] = new List<string>
                {
                    "Chrome", "Google Chrome", "Firefox", "Mozilla Firefox", "Edge", "Microsoft Edge", "Safari", "Opera",
                    "Brave", "Vivaldi", "Chromium", "Internet Explorer", "IE", "Tor Browser", "DuckDuckGo", "Maxthon",
                    "Avast Secure Browser", "AVG Secure Browser", "Norton Secure Browser", "Kaspersky Secure Browser"
                },
                ["Communication"] = new List<string>
                {
                    "Discord", "Slack", "Microsoft Teams", "Zoom", "Skype", "WhatsApp", "Telegram", "Signal",
                    "Viber", "Line", "WeChat", "QQ", "Hangouts", "Google Meet", "Webex", "GoToMeeting",
                    "TeamViewer", "AnyDesk", "VNC", "Remote Desktop", "RDP", "Chrome Remote Desktop",
                    "Outlook", "Thunderbird", "Gmail", "Yahoo Mail", "ProtonMail", "Mail", "Calendar"
                },
                ["Productivity"] = new List<string>
                {
                    "Microsoft Word", "Word", "Microsoft Excel", "Excel", "Microsoft PowerPoint", "PowerPoint",
                    "Microsoft Office", "Office", "LibreOffice", "OpenOffice", "Google Docs", "Google Sheets",
                    "Google Slides", "Notion", "Evernote", "OneNote", "Microsoft OneNote", "Obsidian", "Roam Research",
                    "Trello", "Asana", "Monday.com", "ClickUp", "Notion", "Airtable", "Monday", "Basecamp",
                    "Jira", "Confluence", "Linear", "Figma", "Adobe XD", "Sketch", "InVision", "Miro",
                    "Lucidchart", "Draw.io", "Visio", "Microsoft Visio", "Canva", "Adobe Creative Suite"
                },
                ["Entertainment"] = new List<string>
                {
                    "Netflix", "Disney+", "Amazon Prime", "Hulu", "HBO Max", "YouTube", "Vimeo", "Twitch",
                    "Spotify", "Apple Music", "Amazon Music", "YouTube Music", "Tidal", "Deezer", "Pandora",
                    "VLC", "Windows Media Player", "iTunes", "QuickTime", "RealPlayer", "Winamp", "Foobar2000",
                    "Kodi", "Plex", "Emby", "Jellyfin", "MediaMonkey", "MusicBee", "AIMP", "Clementine"
                },
                ["Social Media"] = new List<string>
                {
                    "Facebook", "Instagram", "Twitter", "TikTok", "Snapchat", "LinkedIn", "Reddit", "Pinterest",
                    "YouTube", "Twitch", "Discord", "Telegram", "WhatsApp", "WeChat", "QQ", "Viber",
                    "Tumblr", "Flickr", "500px", "Behance", "Dribbble", "DeviantArt", "ArtStation"
                },
                ["Education"] = new List<string>
                {
                    "Khan Academy", "Coursera", "edX", "Udemy", "Udacity", "Skillshare", "MasterClass", "Duolingo",
                    "Rosetta Stone", "Babbel", "Memrise", "Anki", "Quizlet", "Kahoot", "Google Classroom",
                    "Moodle", "Blackboard", "Canvas", "Schoology", "Microsoft Teams for Education", "Zoom for Education",
                    "Adobe Acrobat Reader", "PDF Reader", "Calibre", "Kindle", "Goodreads", "Grammarly", "Turnitin"
                },
                ["Utilities"] = new List<string>
                {
                    "File Explorer", "Windows Explorer", "Finder", "7-Zip", "WinRAR", "WinZip", "CCleaner", "Malwarebytes",
                    "Avast", "AVG", "Norton", "McAfee", "Kaspersky", "Windows Defender", "Task Manager", "Resource Monitor",
                    "Device Manager", "Control Panel", "Settings", "Windows Settings", "System Preferences", "Activity Monitor",
                    "Disk Utility", "Disk Cleanup", "Defragmenter", "Optimize Drives", "Storage Sense", "Windows Update",
                    "Driver Booster", "Driver Easy", "Snappy Driver Installer", "Ninite", "Chocolatey", "Scoop", "Winget"
                }
            };
        }

        private Dictionary<string, List<string>> InitializeWebsiteTemplates()
        {
            return new Dictionary<string, List<string>>
            {
                ["Development"] = new List<string>
                {
                    "github.com", "stackoverflow.com", "gitlab.com", "bitbucket.org", "codepen.io", "jsfiddle.net",
                    "replit.com", "codesandbox.io", "glitch.com", "dartpad.dev", "playcode.io", "sqlfiddle.com",
                    "leetcode.com", "hackerrank.com", "codewars.com", "exercism.io", "freecodecamp.org",
                    "w3schools.com", "mdn.org", "dev.to", "hashnode.com", "medium.com", "css-tricks.com",
                    "smashingmagazine.com", "sitepoint.com", "tutsplus.com", "udemy.com", "coursera.org",
                    "edx.org", "pluralsight.com", "frontendmasters.com", "egghead.io", "khanacademy.org",
                    "bootstrap.com", "jquery.com", "reactjs.org", "vuejs.org", "angular.io", "nodejs.org",
                    "python.org", "java.com", "microsoft.com", "oracle.com", "docker.com", "kubernetes.io",
                    "aws.amazon.com", "azure.microsoft.com", "cloud.google.com", "heroku.com", "netlify.com",
                    "vercel.com", "firebase.google.com"
                },
                ["Gaming"] = new List<string>
                {
                    "steam.com", "steampowered.com", "epicgames.com", "origin.com", "ea.com", "uplay.com",
                    "ubisoft.com", "battle.net", "blizzard.com", "gog.com", "gogalaxy.com", "xbox.com",
                    "playstation.com", "nintendo.com", "twitch.tv", "youtube.com", "discord.com",
                    "reddit.com", "ign.com", "gamespot.com", "metacritic.com", "opencritic.com",
                    "howlongtobeat.com", "backloggd.com", "ggapp.io", "steamdb.info", "steamcharts.com",
                    "pcgamingwiki.com", "wikipedia.org", "fandom.com", "gamefaqs.com", "cheatcc.com",
                    "gamepressure.com", "supercheats.com", "cheatcodes.com", "gamewinners.com",
                    "neoseeker.com", "gamebanshee.com", "rpgcodex.net", "rockpapershotgun.com",
                    "eurogamer.net", "polygon.com", "kotaku.com", "destructoid.com", "joystiq.com",
                    "venturebeat.com", "gamesindustry.biz", "gamasutra.com", "gamedev.net",
                    "unity.com", "unrealengine.com", "roblox.com", "minecraft.net"
                },
                ["WebBrowsing"] = new List<string>
                {
                    "google.com", "bing.com", "duckduckgo.com", "yahoo.com", "baidu.com", "yandex.com",
                    "startpage.com", "searx.me", "qwant.com", "ecosia.org", "brave.com", "opera.com",
                    "mozilla.org", "webkit.org", "chromium.org", "vivaldi.com", "maxthon.com",
                    "safari.com", "internetexplorer.com", "edge.com", "chrome.com", "firefox.com",
                    "newtab.com", "speeddial.com", "bookmark.com", "favorites.com", "history.com",
                    "downloads.com", "extensions.com", "addons.com", "plugins.com", "themes.com",
                    "wallpapers.com", "screensavers.com", "desktop.com", "taskbar.com", "startmenu.com",
                    "controlpanel.com", "settings.com", "preferences.com", "options.com", "config.com",
                    "registry.com", "system.com", "windows.com", "mac.com", "linux.com", "ubuntu.com",
                    "debian.org", "fedora.com", "centos.org", "archlinux.org", "gentoo.org"
                },
                ["Communication"] = new List<string>
                {
                    "gmail.com", "outlook.com", "yahoo.com", "protonmail.com", "tutanota.com",
                    "mail.com", "aol.com", "icloud.com", "mail.yahoo.com", "mail.google.com",
                    "outlook.live.com", "office.com", "microsoft365.com", "teams.microsoft.com",
                    "zoom.us", "meet.google.com", "webex.com", "gotomeeting.com", "skype.com",
                    "discord.com", "slack.com", "telegram.org", "whatsapp.com", "signal.org",
                    "viber.com", "line.me", "wechat.com", "qq.com", "kik.com", "snapchat.com",
                    "instagram.com", "facebook.com", "twitter.com", "linkedin.com", "reddit.com",
                    "pinterest.com", "tumblr.com", "flickr.com", "500px.com", "behance.net",
                    "dribbble.com", "deviantart.com", "artstation.com", "deviantart.com",
                    "flickr.com", "500px.com", "unsplash.com", "pexels.com", "pixabay.com",
                    "shutterstock.com", "istockphoto.com"
                },
                ["Productivity"] = new List<string>
                {
                    "docs.google.com", "sheets.google.com", "slides.google.com", "drive.google.com",
                    "onedrive.live.com", "dropbox.com", "box.com", "mega.nz", "icloud.com",
                    "notion.so", "evernote.com", "onenote.com", "roamresearch.com", "obsidian.md",
                    "logseq.com", "workflowy.com", "dynalist.io", "thebrain.com", "mindmeister.com",
                    "xmind.net", "freemind.sourceforge.net", "trello.com", "asana.com", "monday.com",
                    "clickup.com", "airtable.com", "basecamp.com", "jira.com", "confluence.com",
                    "linear.app", "figma.com", "sketch.com", "invisionapp.com", "miro.com",
                    "lucidchart.com", "draw.io", "visio.com", "canva.com", "adobe.com",
                    "creative.adobe.com", "photoshop.com", "illustrator.com", "indesign.com",
                    "premiere.com", "aftereffects.com", "xd.adobe.com", "figma.com", "sketch.com",
                    "invisionapp.com", "framer.com", "webflow.com", "wix.com"
                },
                ["Entertainment"] = new List<string>
                {
                    "netflix.com", "disneyplus.com", "amazon.com", "hulu.com", "hbomax.com",
                    "youtube.com", "vimeo.com", "twitch.tv", "spotify.com", "music.apple.com",
                    "music.amazon.com", "music.youtube.com", "tidal.com", "deezer.com", "pandora.com",
                    "last.fm", "soundcloud.com", "bandcamp.com", "8tracks.com", "mixcloud.com",
                    "iheart.com", "tunein.com", "radio.com", "npr.org", "bbc.co.uk", "cnn.com",
                    "foxnews.com", "msnbc.com", "abcnews.go.com", "cbsnews.com", "nbcnews.com",
                    "reuters.com", "bloomberg.com", "wsj.com", "nytimes.com", "washingtonpost.com",
                    "usatoday.com", "latimes.com", "chicagotribune.com", "bostonglobe.com",
                    "philly.com", "sfgate.com", "dallasnews.com", "houstonchronicle.com",
                    "denverpost.com", "seattletimes.com", "oregonlive.com", "sacbee.com",
                    "mercurynews.com", "latimes.com", "sandiegouniontribune.com"
                },
                ["Social Media"] = new List<string>
                {
                    "facebook.com", "instagram.com", "twitter.com", "tiktok.com", "snapchat.com",
                    "linkedin.com", "reddit.com", "pinterest.com", "youtube.com", "twitch.tv",
                    "discord.com", "telegram.org", "whatsapp.com", "wechat.com", "qq.com",
                    "viber.com", "tumblr.com", "flickr.com", "500px.com", "behance.net",
                    "dribbble.com", "deviantart.com", "artstation.com", "medium.com", "substack.com",
                    "newsletter.com", "mailchimp.com", "constantcontact.com", "aweber.com",
                    "getresponse.com", "convertkit.com", "activecampaign.com", "infusionsoft.com",
                    "hubspot.com", "salesforce.com", "pipedrive.com", "zoho.com", "freshworks.com",
                    "intercom.com", "zendesk.com", "helpscout.com", "groovehq.com", "desk.com",
                    "uservoice.com", "canny.io", "productboard.com", "aha.io", "roadmunk.com",
                    "airfocus.com", "productplan.com", "craft.io"
                },
                ["Education"] = new List<string>
                {
                    "khanacademy.org", "coursera.org", "edx.org", "udemy.com", "udacity.com",
                    "skillshare.com", "masterclass.com", "duolingo.com", "rosettastone.com",
                    "babbel.com", "memrise.com", "ankiweb.net", "quizlet.com", "kahoot.com",
                    "classroom.google.com", "moodle.org", "blackboard.com", "canvas.com",
                    "schoology.com", "teams.microsoft.com", "zoom.us", "webex.com", "gotomeeting.com",
                    "adobe.com", "acrobat.com", "pdf.com", "calibre.com", "kindle.com", "goodreads.com",
                    "grammarly.com", "turnitin.com", "plagiarism.com", "copyscape.com", "copyleaks.com",
                    "quillbot.com", "wordtune.com", "jasper.ai", "copy.ai", "rytr.me", "writesonic.com",
                    "simplified.co", "peppertype.ai", "contentbot.ai", "anyword.com", "hypotenuse.ai",
                    "neuroflash.com", "textcortex.com", "writecream.com", "closerscopy.com",
                    "surferseo.com", "clearscope.io", "ahrefs.com", "semrush.com"
                },
                ["Utilities"] = new List<string>
                {
                    "google.com", "bing.com", "duckduckgo.com", "yahoo.com", "weather.com",
                    "accuweather.com", "weatherunderground.com", "forecast.io", "wunderground.com",
                    "maps.google.com", "bing.com/maps", "openstreetmap.org", "mapquest.com",
                    "here.com", "waze.com", "tomtom.com", "garmin.com", "strava.com", "runkeeper.com",
                    "mapmyrun.com", "endomondo.com", "myfitnesspal.com", "fitbit.com", "garmin.com",
                    "polar.com", "suunto.com", "coros.com", "wahoo.com", "zwift.com", "trainerroad.com",
                    "sufferfest.com", "goldencheetah.org", "trainingpeaks.com", "today'splan.com",
                    "final-surge.com", "athlinks.com", "athletic.net", "athlete.net", "athlete.com",
                    "athlete.net", "athlete.com", "athlete.net", "athlete.com", "athlete.net",
                    "athlete.com", "athlete.net", "athlete.com", "athlete.net", "athlete.com",
                    "athlete.net", "athlete.com", "athlete.net"
                }
            };
        }

        public IEnumerable<string> GetAllCategories()
        {
            return _defaultCategories.Concat(_customCategories).Distinct();
        }

        public IEnumerable<string> GetDefaultCategories()
        {
            return _defaultCategories.ToList();
        }

        public IEnumerable<string> GetCustomCategories()
        {
            return _customCategories.ToList();
        }

        public List<string> GetTemplateAppsForCategory(string category)
        {
            return _categoryTemplates.TryGetValue(category, out var templates) ? templates : new List<string>();
        }

        public List<string> GetTemplateWebsitesForCategory(string category)
        {
            return _websiteTemplates.TryGetValue(category, out var templates) ? templates : new List<string>();
        }

        public string GetCategoryForApp(string appName)
        {
            return _appCategories.TryGetValue(appName, out var category) ? category : "Uncategorized";
        }

        public string GetCategoryForWebsite(string domain)
        {
            return _websiteCategories.TryGetValue(domain, out var category) ? category : "Uncategorized";
        }

        public string AutoDetectCategory(string appName)
        {
            if (string.IsNullOrEmpty(appName))
                return "Uncategorized";

            var appNameLower = appName.ToLowerInvariant();

            // Check each category's template apps
            foreach (var category in _categoryTemplates.Keys)
            {
                var templateApps = _categoryTemplates[category];
                foreach (var templateApp in templateApps)
                {
                    if (appNameLower.Contains(templateApp.ToLowerInvariant()) || 
                        templateApp.ToLowerInvariant().Contains(appNameLower))
                    {
                        return category;
                    }
                }
            }

            // Additional keyword-based detection
            if (appNameLower.Contains("browser") || appNameLower.Contains("chrome") || appNameLower.Contains("firefox") || 
                appNameLower.Contains("edge") || appNameLower.Contains("safari") || appNameLower.Contains("opera"))
                return "WebBrowsing";

            if (appNameLower.Contains("game") || appNameLower.Contains("steam") || appNameLower.Contains("epic") || 
                appNameLower.Contains("origin") || appNameLower.Contains("battle.net") || appNameLower.Contains("valorant") ||
                appNameLower.Contains("league") || appNameLower.Contains("dota") || appNameLower.Contains("csgo") ||
                appNameLower.Contains("minecraft") || appNameLower.Contains("roblox"))
                return "Gaming";

            if (appNameLower.Contains("studio") || appNameLower.Contains("code") || appNameLower.Contains("ide") || 
                appNameLower.Contains("editor") || appNameLower.Contains("compiler") || appNameLower.Contains("git") ||
                appNameLower.Contains("terminal") || appNameLower.Contains("command") || appNameLower.Contains("bash"))
                return "Development";

            if (appNameLower.Contains("chat") || appNameLower.Contains("message") || appNameLower.Contains("call") || 
                appNameLower.Contains("meet") || appNameLower.Contains("zoom") || appNameLower.Contains("skype") ||
                appNameLower.Contains("discord") || appNameLower.Contains("slack") || appNameLower.Contains("teams"))
                return "Communication";

            if (appNameLower.Contains("office") || appNameLower.Contains("word") || appNameLower.Contains("excel") || 
                appNameLower.Contains("powerpoint") || appNameLower.Contains("document") || appNameLower.Contains("spreadsheet") ||
                appNameLower.Contains("presentation") || appNameLower.Contains("note") || appNameLower.Contains("task"))
                return "Productivity";

            if (appNameLower.Contains("media") || appNameLower.Contains("player") || appNameLower.Contains("music") || 
                appNameLower.Contains("video") || appNameLower.Contains("movie") || appNameLower.Contains("stream") ||
                appNameLower.Contains("netflix") || appNameLower.Contains("youtube") || appNameLower.Contains("spotify"))
                return "Entertainment";

            if (appNameLower.Contains("social") || appNameLower.Contains("facebook") || appNameLower.Contains("instagram") || 
                appNameLower.Contains("twitter") || appNameLower.Contains("tiktok") || appNameLower.Contains("snapchat") ||
                appNameLower.Contains("linkedin") || appNameLower.Contains("reddit") || appNameLower.Contains("pinterest"))
                return "Social Media";

            if (appNameLower.Contains("learn") || appNameLower.Contains("course") || appNameLower.Contains("tutorial") || 
                appNameLower.Contains("education") || appNameLower.Contains("school") || appNameLower.Contains("university") ||
                appNameLower.Contains("duolingo") || appNameLower.Contains("khan") || appNameLower.Contains("coursera"))
                return "Education";

            if (appNameLower.Contains("utility") || appNameLower.Contains("tool") || appNameLower.Contains("cleaner") || 
                appNameLower.Contains("optimizer") || appNameLower.Contains("defrag") || appNameLower.Contains("backup") ||
                appNameLower.Contains("security") || appNameLower.Contains("antivirus") || appNameLower.Contains("driver"))
                return "Utilities";

            return "Uncategorized";
        }

        public string AutoDetectWebsiteCategory(string domain)
        {
            if (string.IsNullOrEmpty(domain))
                return "Uncategorized";

            var domainLower = domain.ToLowerInvariant();

            // Check each category's template websites
            foreach (var category in _websiteTemplates.Keys)
            {
                var templateWebsites = _websiteTemplates[category];
                foreach (var templateWebsite in templateWebsites)
                {
                    if (domainLower.Contains(templateWebsite.ToLowerInvariant()) || 
                        templateWebsite.ToLowerInvariant().Contains(domainLower))
                    {
                        return category;
                    }
                }
            }

            // Additional keyword-based detection for websites
            if (domainLower.Contains("mail") || domainLower.Contains("gmail") || domainLower.Contains("outlook") || 
                domainLower.Contains("yahoo") || domainLower.Contains("protonmail") || domainLower.Contains("tutanota") ||
                domainLower.Contains("zoom") || domainLower.Contains("meet") || domainLower.Contains("webex") ||
                domainLower.Contains("skype") || domainLower.Contains("discord") || domainLower.Contains("slack") ||
                domainLower.Contains("telegram") || domainLower.Contains("whatsapp") || domainLower.Contains("signal"))
                return "Communication";

            if (domainLower.Contains("facebook") || domainLower.Contains("instagram") || domainLower.Contains("twitter") || 
                domainLower.Contains("tiktok") || domainLower.Contains("snapchat") || domainLower.Contains("linkedin") ||
                domainLower.Contains("reddit") || domainLower.Contains("pinterest") || domainLower.Contains("tumblr") ||
                domainLower.Contains("flickr") || domainLower.Contains("behance") || domainLower.Contains("dribbble"))
                return "Social Media";

            if (domainLower.Contains("github") || domainLower.Contains("stackoverflow") || domainLower.Contains("gitlab") || 
                domainLower.Contains("bitbucket") || domainLower.Contains("codepen") || domainLower.Contains("jsfiddle") ||
                domainLower.Contains("replit") || domainLower.Contains("codesandbox") || domainLower.Contains("leetcode") ||
                domainLower.Contains("hackerrank") || domainLower.Contains("codewars") || domainLower.Contains("w3schools"))
                return "Development";

            if (domainLower.Contains("steam") || domainLower.Contains("epicgames") || domainLower.Contains("origin") || 
                domainLower.Contains("uplay") || domainLower.Contains("battle.net") || domainLower.Contains("gog") ||
                domainLower.Contains("xbox") || domainLower.Contains("playstation") || domainLower.Contains("nintendo") ||
                domainLower.Contains("twitch") || domainLower.Contains("ign") || domainLower.Contains("gamespot"))
                return "Gaming";

            if (domainLower.Contains("docs.google") || domainLower.Contains("sheets.google") || domainLower.Contains("slides.google") || 
                domainLower.Contains("drive.google") || domainLower.Contains("onedrive") || domainLower.Contains("dropbox") ||
                domainLower.Contains("notion") || domainLower.Contains("evernote") || domainLower.Contains("onenote") ||
                domainLower.Contains("trello") || domainLower.Contains("asana") || domainLower.Contains("monday") ||
                domainLower.Contains("figma") || domainLower.Contains("sketch") || domainLower.Contains("miro"))
                return "Productivity";

            if (domainLower.Contains("netflix") || domainLower.Contains("disneyplus") || domainLower.Contains("hulu") || 
                domainLower.Contains("hbomax") || domainLower.Contains("youtube") || domainLower.Contains("vimeo") ||
                domainLower.Contains("spotify") || domainLower.Contains("music") || domainLower.Contains("tidal") ||
                domainLower.Contains("deezer") || domainLower.Contains("pandora") || domainLower.Contains("soundcloud"))
                return "Entertainment";

            if (domainLower.Contains("khanacademy") || domainLower.Contains("coursera") || domainLower.Contains("edx") || 
                domainLower.Contains("udemy") || domainLower.Contains("udacity") || domainLower.Contains("skillshare") ||
                domainLower.Contains("duolingo") || domainLower.Contains("rosettastone") || domainLower.Contains("babbel") ||
                domainLower.Contains("quizlet") || domainLower.Contains("kahoot") || domainLower.Contains("classroom"))
                return "Education";

            if (domainLower.Contains("google") || domainLower.Contains("bing") || domainLower.Contains("duckduckgo") || 
                domainLower.Contains("yahoo") || domainLower.Contains("weather") || domainLower.Contains("maps") ||
                domainLower.Contains("waze") || domainLower.Contains("strava") || domainLower.Contains("myfitnesspal"))
                return "Utilities";

            return "Uncategorized";
        }

        public void SetCategoryForApp(string appName, string category)
        {
            if (string.IsNullOrEmpty(appName) || string.IsNullOrEmpty(category))
                return;

            _appCategories[appName] = category;
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetCategoryForApps(IEnumerable<string> appNames, string category)
        {
            if (string.IsNullOrEmpty(category))
                return;

            foreach (var appName in appNames)
            {
                if (!string.IsNullOrEmpty(appName))
                {
                    _appCategories[appName] = category;
                }
            }
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AutoCategorizeAllApps(IEnumerable<string> allAppNames)
        {
            foreach (var appName in allAppNames)
            {
                if (!string.IsNullOrEmpty(appName) && !_appCategories.ContainsKey(appName))
                {
                    var detectedCategory = AutoDetectCategory(appName);
                    _appCategories[appName] = detectedCategory;
                }
            }
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void SetCategoryForWebsite(string domain, string category)
        {
            if (!string.IsNullOrEmpty(domain) && !string.IsNullOrEmpty(category))
            {
                _websiteCategories[domain] = category;
                SaveCategories();
                CategoriesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetCategoryForWebsites(IEnumerable<string> domains, string category)
        {
            foreach (var domain in domains)
            {
                if (!string.IsNullOrEmpty(domain))
                {
                    _websiteCategories[domain] = category;
                }
            }
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AutoCategorizeAllWebsites(IEnumerable<string> allDomains)
        {
            foreach (var domain in allDomains)
            {
                if (!string.IsNullOrEmpty(domain) && !_websiteCategories.ContainsKey(domain))
                {
                    var detectedCategory = AutoDetectWebsiteCategory(domain);
                    _websiteCategories[domain] = detectedCategory;
                }
            }
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyTemplateToCategory(string category)
        {
            if (!_categoryTemplates.ContainsKey(category))
                return;

            var templateApps = _categoryTemplates[category];
            foreach (var appName in templateApps)
            {
                _appCategories[appName] = category;
            }
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyWebsiteTemplateToCategory(string category)
        {
            if (!_websiteTemplates.ContainsKey(category))
                return;

            var templateWebsites = _websiteTemplates[category];
            foreach (var domain in templateWebsites)
            {
                _websiteCategories[domain] = category;
            }
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void AddCustomCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName) || 
                _defaultCategories.Contains(categoryName) || 
                _customCategories.Contains(categoryName))
                return;

            _customCategories.Add(categoryName);
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RemoveCustomCategory(string categoryName)
        {
            if (string.IsNullOrEmpty(categoryName) || !_customCategories.Contains(categoryName))
                return;

            _customCategories.Remove(categoryName);

            // Move apps from this category to Uncategorized
            var appsToMove = _appCategories.Where(kvp => kvp.Value == categoryName).ToList();
            foreach (var kvp in appsToMove)
            {
                _appCategories[kvp.Key] = "Uncategorized";
            }

            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RenameCustomCategory(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(oldName) || string.IsNullOrEmpty(newName) ||
                !_customCategories.Contains(oldName) ||
                _defaultCategories.Contains(newName) ||
                _customCategories.Contains(newName))
                return;

            _customCategories.Remove(oldName);
            _customCategories.Add(newName);

            // Update all apps that were in the old category
            var appsToUpdate = _appCategories.Where(kvp => kvp.Value == oldName).ToList();
            foreach (var kvp in appsToUpdate)
            {
                _appCategories[kvp.Key] = newName;
            }

            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public Dictionary<string, int> GetAppCountByCategory()
        {
            var result = new Dictionary<string, int>();
            
            foreach (var category in GetAllCategories())
            {
                result[category] = _appCategories.Count(kvp => kvp.Value == category);
            }

            return result;
        }

        public Dictionary<string, int> GetWebsiteCountByCategory()
        {
            var result = new Dictionary<string, int>();
            
            foreach (var category in GetAllCategories())
            {
                result[category] = _websiteCategories.Count(kvp => kvp.Value == category);
            }

            return result;
        }

        public int GetUncategorizedAppCount()
        {
            return _appCategories.Count(kvp => kvp.Value == "Uncategorized");
        }

        public int GetUncategorizedWebsiteCount()
        {
            return _websiteCategories.Count(kvp => kvp.Value == "Uncategorized");
        }

        public IEnumerable<string> GetAppsInCategory(string category)
        {
            return _appCategories.Where(kvp => kvp.Value == category).Select(kvp => kvp.Key);
        }

        public void ClearAllCategories()
        {
            _appCategories.Clear();
            _customCategories.Clear();
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ResetToDefaults()
        {
            _appCategories.Clear();
            _customCategories.Clear();
            SaveCategories();
            CategoriesChanged?.Invoke(this, EventArgs.Empty);
        }

        public void InitializeWithTemplates()
        {
            foreach (var category in _categoryTemplates.Keys)
            {
                ApplyTemplateToCategory(category);
                ApplyWebsiteTemplateToCategory(category);
            }
        }

        public bool HasAnyCategorizedApps()
        {
            return _appCategories.Any(kvp => kvp.Value != "Uncategorized");
        }

        public void AutoCategorizeIfNoCategoriesExist(IEnumerable<string> allAppNames, IEnumerable<string> allWebsiteDomains)
        {
            // Check if there are any categorized apps or websites (excluding Uncategorized)
            if (!HasAnyCategorizedApps() && !HasAnyCategorizedWebsites())
            {
                // Auto-categorize all apps and websites
                AutoCategorizeAllApps(allAppNames);
                AutoCategorizeAllWebsites(allWebsiteDomains);
            }
        }

        public void CategorizeExistingUncategorizedItems(IEnumerable<string> allAppNames, IEnumerable<string> allWebsiteDomains)
        {
            // Categorize apps that are currently uncategorized
            foreach (var appName in allAppNames)
            {
                if (!string.IsNullOrEmpty(appName) && GetCategoryForApp(appName) == "Uncategorized")
                {
                    var detectedCategory = AutoDetectCategory(appName);
                    SetCategoryForApp(appName, detectedCategory);
                }
            }

            // Categorize websites that are currently uncategorized
            foreach (var domain in allWebsiteDomains)
            {
                if (!string.IsNullOrEmpty(domain) && GetCategoryForWebsite(domain) == "Uncategorized")
                {
                    var detectedCategory = AutoDetectWebsiteCategory(domain);
                    SetCategoryForWebsite(domain, detectedCategory);
                }
            }
        }

        public bool HasAnyCategorizedWebsites()
        {
            return _websiteCategories.Any(kvp => kvp.Value != "Uncategorized");
        }

        private void LoadCategories()
        {
            try
            {
                if (File.Exists(_categoriesFilePath))
                {
                    var json = File.ReadAllText(_categoriesFilePath);
                    var data = JsonConvert.DeserializeObject<CategoryData>(json);
                    
                    if (data != null)
                    {
                        _appCategories.Clear();
                        foreach (var kvp in data.AppCategories)
                        {
                            _appCategories[kvp.Key] = kvp.Value;
                        }

                        _websiteCategories.Clear();
                        if (data.WebsiteCategories != null)
                        {
                            foreach (var kvp in data.WebsiteCategories)
                            {
                                _websiteCategories[kvp.Key] = kvp.Value;
                            }
                        }

                        _customCategories.Clear();
                        foreach (var category in data.CustomCategories)
                        {
                            if (!_defaultCategories.Contains(category))
                            {
                                _customCategories.Add(category);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
                // Use defaults if loading fails
            }
        }

        private void SaveCategories()
        {
            try
            {
                var directory = Path.GetDirectoryName(_categoriesFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var data = new CategoryData
                {
                    AppCategories = _appCategories,
                    WebsiteCategories = _websiteCategories,
                    CustomCategories = _customCategories
                };

                var json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(_categoriesFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving categories: {ex.Message}");
            }
        }

        private class CategoryData
        {
            public Dictionary<string, string> AppCategories { get; set; } = new();
            public Dictionary<string, string> WebsiteCategories { get; set; } = new();
            public List<string> CustomCategories { get; set; } = new();
        }
    }
} 