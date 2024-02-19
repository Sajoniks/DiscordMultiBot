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
        if (o.Namespace is not null && o.Namespace.Any())
        {
            Console.WriteLine("Namespace", String.Join(':', o.Namespace));
        }

        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine("Error: input directory does not exist");
            return;
        }

        List<AudioGroup> audioGroups = new();
        var config = new AudioConfig(inputDir, audioGroups);

        var dirInfo = new DirectoryInfo(inputDir);
        foreach (var dir in dirInfo.EnumerateDirectories())
        {
            Console.WriteLine("Parsing {0}", dir.FullName);

            Dictionary<string, object> props = new();
            List<AudioFile> audioFiles = new();
            AudioGroup audioGroup = new AudioGroup(dir.Name, props, audioFiles);
            audioGroups.Add(audioGroup);
            
            props.Add("manual_play", false);
            props.Add("looping", false);

            foreach (var files in new[] { "*.mp3", "*.wav" }.SelectMany(dir.EnumerateFiles))
            {
                Console.WriteLine("-- Added {0}", files.Name);

                Dictionary<string, object> audioProps = new();
                AudioFile audioFile = new AudioFile(files.FullName, audioProps);
                audioFiles.Add(audioFile);

                audioProps.Add("volume", 1.0f);
            }
        }

        Console.WriteLine("Written {0} audio groups", audioGroups.Count);

        
        try
        {
            if (File.Exists(outputPath))
            {
                WriteDiff(outputPath, Read(outputPath, o), config, o);
                return;
            }
        }
        catch (Exception)
        {
             // ignore   
        }

        Write(outputPath, config, o);
    });

AudioConfig Read(string path, Options options)
{
    var baseObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path)) ?? throw new NullReferenceException();
    if (options.Namespace is null || !options.Namespace.Any())
    {
        return baseObject.ToObject<AudioConfig>() ?? throw new NullReferenceException();
    }

    JToken currentRoot = baseObject;
    foreach (string ns in options.Namespace)
    {
        JToken next = currentRoot[ns] ?? throw new NullReferenceException();
        currentRoot = next;
    }

    return currentRoot.ToObject<AudioConfig>() ?? throw new NullReferenceException();
}

void Write(string path, AudioConfig baseConfig, Options options)
{
    if (options.Namespace is null || !options.Namespace.Any())
    {
        File.WriteAllText(path, JsonConvert.SerializeObject(baseConfig, Formatting.Indented));
    }
    else
    {
        JObject prev = default!;
        JObject root = new JObject();
        JObject wrapper = root;
        foreach (string ns in options.Namespace)
        {
            JObject next = new JObject();
            wrapper.Add(ns, next);
            prev = wrapper;
            wrapper = next;
        }
        
        prev[options.Namespace.Last()] = JObject.FromObject(baseConfig);

        File.WriteAllText(path, JsonConvert.SerializeObject(root, Formatting.Indented));
    }
}

void WriteDiff(string path, AudioConfig baseConfig, AudioConfig newConfig, Options options)
{
    IEnumerable<AudioFile> DiffFiles(IEnumerable<AudioFile> oldF, IEnumerable<AudioFile> newF)
    {
        var l1 = oldF.ToList();
        var l2 = newF.ToList();

        
        List<AudioFile> result = new();
        foreach (var zip in l2.Zip(l1))
        {
            var f = new AudioFile(
                zip.First.Path,
                DiffProps(zip.First.Properties, zip.Second.Properties)
            );
            result.Add(f);
            Console.WriteLine("File diff written {0}", f.Path);
        }
        
        for (int i = l1.Count; i < l2.Count; ++i)
        {
            result.Add(l2[i]);
            Console.WriteLine("Added new file {0}", l2[i].Path);
        }

        return result;
    }

    IDictionary<string, object> DiffProps(IDictionary<string, object> newP, IDictionary<string, object> oldP)
    {
        Dictionary<string, object> result = new(newP);
        foreach (var kv in oldP)
        {
            object a = kv.Value;
            object b = result[kv.Key];
            
            if (a.Equals(b)) continue;

            result[kv.Key] = kv.Value;
            
            Console.WriteLine("Prop diff {0} -> {1}", b, result[kv.Key]);
        }

        return result;
    }

    IEnumerable<AudioGroup> DiffGroups(IEnumerable<AudioGroup> oldG, IEnumerable<AudioGroup> newG)
    {
        List<AudioGroup> newGroup = new();

        var l1 = oldG.ToList();
        var l2 = newG.ToList();
        
        
        foreach (var zip in l2.Zip(l1))
        {
            var g = new AudioGroup(
                zip.First.Name,
                DiffProps(zip.First.Properties, zip.Second.Properties),
                DiffFiles(zip.Second.Files, zip.First.Files)
            );
            newGroup.Add(g);
            Console.WriteLine("Group diff written {0}", g.Name);
        }
        
        for (int i = l1.Count; i < l2.Count; ++i)
        {
            newGroup.Add(l2[i]);
            Console.WriteLine("Added new group {0}", l2[i].Name);
        }

        return newGroup;
    }

    string newPath = newConfig.Path;
    var newGroups = DiffGroups(baseConfig.Files, newConfig.Files);
    
    Write(path, new AudioConfig(newPath, newGroups), options);
}

public record AudioFile(string Path, IDictionary<string, object> Properties);
public record AudioGroup(string Name, IDictionary<string, object> Properties, IEnumerable<AudioFile> Files);
public record AudioConfig(string Path, IEnumerable<AudioGroup> Files);

public class Options
{
    [Option('i', "input", Required = true,
        HelpText = "Parses given folder into audio file configuration for DiscordMultiBot")]
    public string InputPath { get; set; } = "";

    [Option('o', "output", Required = true, HelpText = "Output configuration file path")]
    public string OutputPath { get; set; } = "";
    
    [Option("namespace", Required = false, HelpText = "Namespace of the generated configuration root", Separator = ':')]
    public IEnumerable<string>? Namespace { get; set; }
}