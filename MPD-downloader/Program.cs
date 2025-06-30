using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace DashDownloader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Default values
            string mpdUrl = "https://aod-dash-ww-live.akamaized.net/usp/auth/vod/piff_abr_full_audio/2e5865-m002bqm5/vf_m002bqm5_7834cc7e-477d-49b1-a600-b218ffd6d43f.ism/pc_hd_abr_v2_nonuk_dash_master.mpd?__gda__=1751100115_3eabb45df13f66a60892be55d10fa5fd";
            string outputFile = "output.mp4";

            // Parse command line arguments
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--url", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    mpdUrl = args[++i];
                }
                else if ((args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) || 
                          args[i].Equals("-o", StringComparison.OrdinalIgnoreCase)) && 
                         i + 1 < args.Length)
                {
                    outputFile = args[++i];
                }
                else if (args[i].Equals("--help", StringComparison.OrdinalIgnoreCase) || 
                         args[i].Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    DisplayHelp();
                    return;
                }
            }

            try
            {
                await DownloadMpdContent(mpdUrl, outputFile);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                }
                
                Console.WriteLine("Use --help for usage information.");
                Environment.Exit(1);
            }
        }

        private static void DisplayHelp()
        {
            Console.WriteLine("DASH MPD Downloader - Download audio from DASH manifests");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  dotnet run [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --url <url>        MPD manifest URL");
            Console.WriteLine("  --output, -o <file> Output file name (default: output.mp4)");
            Console.WriteLine("  --help, -h         Show this help message");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run --url https://example.com/manifest.mpd --output audio.mp4");
        }

        private static async Task DownloadMpdContent(string mpdUrl, string outputFile)
        {
            using HttpClient client = new HttpClient();

            Console.WriteLine($"MPD URL: {mpdUrl}");
            Console.WriteLine($"Output File: {outputFile}");
            Console.WriteLine("Downloading MPD manifest...");
                
            string mpdContent = await client.GetStringAsync(mpdUrl);

            // Parse the MPD XML
            XDocument doc = XDocument.Parse(mpdContent);
            XNamespace ns = "urn:mpeg:dash:schema:mpd:2011";

            // Parse mediaPresentationDuration (e.g., "PT1H")
            var mediaPresentationDurationAttr = doc.Root.Attribute("mediaPresentationDuration");
            if (mediaPresentationDurationAttr == null)
            {
                throw new InvalidOperationException("MPD manifest does not contain mediaPresentationDuration attribute.");
            }
            
            TimeSpan presentationDuration = XmlConvert.ToTimeSpan(mediaPresentationDurationAttr.Value);
            Console.WriteLine($"Media duration: {presentationDuration.TotalMinutes:F2} minutes");

            // Get the Period element.
            var period = doc.Root.Element(ns + "Period");
            if (period == null)
            {
                throw new InvalidOperationException("Period element not found in the MPD.");
            }

            // Get the BaseURL from the Period (it is a relative URL)
            string baseUrl = period.Element(ns + "BaseURL")?.Value?.Trim() ?? "";

            // Select the audio AdaptationSet.
            var adaptationSet = period.Elements(ns + "AdaptationSet")
                                      .FirstOrDefault(a => (a.Attribute("contentType")?.Value == "audio"));
            if (adaptationSet == null)
            {
                throw new InvalidOperationException("Audio AdaptationSet not found in the MPD.");
            }

            // Get the SegmentTemplate element.
            var segmentTemplate = adaptationSet.Element(ns + "SegmentTemplate");
            if (segmentTemplate == null)
            {
                throw new InvalidOperationException("SegmentTemplate not found in the AdaptationSet.");
            }

            // Get template attributes.
            string? initializationTemplate = segmentTemplate.Attribute("initialization")?.Value;
            string? mediaTemplate = segmentTemplate.Attribute("media")?.Value;
            string? timescaleStr = segmentTemplate.Attribute("timescale")?.Value;
            string? durationStr = segmentTemplate.Attribute("duration")?.Value;

            if (initializationTemplate == null || mediaTemplate == null ||
                timescaleStr == null || durationStr == null)
            {
                throw new InvalidOperationException("One or more required attributes missing in SegmentTemplate.");
            }

            // Parse timescale and duration
            int timescale = int.Parse(timescaleStr);
            int durationUnits = int.Parse(durationStr);
            double segmentDurationSeconds = (double)durationUnits / timescale;
            int totalSegments = (int)Math.Ceiling(presentationDuration.TotalSeconds / segmentDurationSeconds);

            // Choose a Representation (here, the first one)
            var representation = adaptationSet.Elements(ns + "Representation").FirstOrDefault();
            if (representation == null)
            {
                throw new InvalidOperationException("No Representation found in the AdaptationSet.");
            }
            
            string? representationId = representation.Attribute("id")?.Value;
            if (string.IsNullOrEmpty(representationId))
            {
                throw new InvalidOperationException("Representation id is missing.");
            }
            
            string? mimeType = representation.Attribute("mimeType")?.Value;
            string? codecs = representation.Attribute("codecs")?.Value;
            string? bandwidth = representation.Attribute("bandwidth")?.Value;
            
            Console.WriteLine($"Audio format: {mimeType} {codecs}, Bandwidth: {(int.Parse(bandwidth ?? "0") / 1000):N0} kbps");
            Console.WriteLine($"Using Representation: {representationId}");

            // Helper method to replace placeholders in templates.
            string ReplacePlaceholders(string template, int number)
            {
                return template.Replace("$RepresentationID$", representationId)
                               .Replace("$Number$", number.ToString());
            }

            // Create a base Uri using the MPD URL and resolve the BaseURL.
            Uri mpdUri = new Uri(mpdUrl);
            Uri baseUri = new Uri(mpdUri, baseUrl);

            // Create the directory if it doesn't exist
            string? directory = Path.GetDirectoryName(outputFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Create/overwrite the output file.
            using FileStream outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);

            // Build and download the initialization segment.
            string initSegmentPath = ReplacePlaceholders(initializationTemplate, 0).Replace("$Number$", "");
            Uri initUri = new Uri(baseUri, initSegmentPath);
            Console.WriteLine("Downloading initialization segment...");
            byte[] initData = await client.GetByteArrayAsync(initUri.ToString());
            await outputStream.WriteAsync(initData, 0, initData.Length);

            int progressBarWidth = Console.WindowWidth - 30;
            if (progressBarWidth < 10) progressBarWidth = 10;
            
            Console.WriteLine($"Downloading {totalSegments} segments:");

            // Download each media segment.
            for (int segNum = 1; segNum <= totalSegments; segNum++)
            {
                string segmentPath = ReplacePlaceholders(mediaTemplate, segNum);
                Uri segmentUri = new Uri(baseUri, segmentPath);
                
                // Update progress bar
                double progressPercentage = (double)segNum / totalSegments;
                int progressChars = (int)Math.Floor(progressPercentage * progressBarWidth);
                
                Console.Write("\r[");
                Console.Write(new string('#', progressChars));
                Console.Write(new string('-', progressBarWidth - progressChars));
                Console.Write($"] {progressPercentage:P0} ({segNum}/{totalSegments})");
                
                byte[] segmentData = await client.GetByteArrayAsync(segmentUri.ToString());
                await outputStream.WriteAsync(segmentData, 0, segmentData.Length);
            }
            
            Console.WriteLine(); // Add a new line after the progress bar
            Console.WriteLine($"Download complete. Output file: {Path.GetFullPath(outputFile)}");
        }
    }
}
