using CommandLine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

Parser.Default.ParseArguments<Options>(args)
    .WithParsed(o =>
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string inputDir = Path.GetFullPath(o.InputPath, basePath);
        string outputPath = Path.GetFullPath(o.OutputPath, basePath);
        
        Console.WriteLine("Input: {0}", inputDir);
        Console.WriteLine("Output: {0}", outputPath);

        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine("Error: input directory does not exist");
            return;
        }

        var config = new JObject();
        var audioFiles = new JArray();
        
        config.Add("path", inputDir);
        config.Add("files", audioFiles);

        var dirInfo = new DirectoryInfo(inputDir);
        foreach (var dir in dirInfo.EnumerateDirectories())
        {
            Console.WriteLine("Parsing {0}", dir.FullName);
            
            var audioFileProps = new JObject();
            audioFileProps.Add("manual_play", false);
            audioFileProps.Add("looping", false);
            
            var audioFile = new JObject();
            audioFile.Add("name", dir.Name);
            audioFile.Add("props", audioFileProps);

            var audioFileFiles = new JArray();
            audioFile.Add("files", audioFileFiles);
            audioFiles.Add(audioFile);

            foreach (var files in new[] { "*.mp3", "*.wav" }.SelectMany(dir.EnumerateFiles))
            {
                Console.WriteLine("-- Added {0}", files.Name);
                
                var audioFileFile = new JObject();
                var audioFileFileProps = new JObject();
                audioFileFileProps.Add("volume", 1.0f);
                
                audioFileFile.Add("path", files.FullName);
                audioFileFile.Add("props", audioFileFileProps);

                audioFileFiles.Add(audioFileFile);
            }
        }

        Console.WriteLine("Written {0} audios", audioFiles.Count);
        File.WriteAllText(outputPath, JsonConvert.SerializeObject(config, Formatting.Indented));
    });

public class Options
{
    [Option('i', "input", Required = true,
        HelpText = "Parses given folder into audio file configuration for DiscordMultiBot")]
    public string InputPath { get; set; } = "";

    [Option('o', "output", Required = true, HelpText = "Output configuration file path")]
    public string OutputPath { get; set; } = "";
}