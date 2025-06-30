# DASH MPD Downloader

A .NET 8 command-line application that downloads audio content from DASH (Dynamic Adaptive Streaming over HTTP) MPD (Media Presentation Description) manifests.

## Features

- Parse MPD manifest XML files
- Extract audio segments from DASH streams
- Reconstruct complete audio files from individual segments
- Support for MPEG-DASH standard compliant streams
- Simple console-based progress reporting

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Installation

Clone the repository:

```bash
git clone https://github.com/yourusername/MPD-downloader.git
cd MPD-downloader
```

Build the project:

```bash
dotnet build
```

## Usage

Run the application with:

```bash
dotnet run --project MPD-downloader/MPD-downloader.csproj
```

To download from a specific MPD URL, modify the `mpdUrl` variable in `Program.cs`.

## How It Works

1. Downloads and parses the MPD manifest XML
2. Identifies the audio representation and segment structure
3. Downloads the initialization segment
4. Sequentially downloads all media segments
5. Combines segments into a single output file

## Limitations

- Currently only downloads audio streams
- Fixed output filename (output.mp4)
- No command-line arguments for URL or output file specification
- No adaptive bitrate selection logic

## Future Improvements

- Add command-line arguments for MPD URL and output filename
- Support for video download and multiplexing
- Add quality/bitrate selection options
- Implement resume functionality
- Add progress bar visualization

## License

[MIT](LICENSE)

## Acknowledgments

- [MPEG-DASH](https://mpeg.chiariglione.org/standards/mpeg-dash) for the streaming technology standard