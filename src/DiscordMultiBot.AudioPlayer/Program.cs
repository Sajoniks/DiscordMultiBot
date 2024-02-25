

using CommandLine;
using DiscordMultiBot.AudioPlayer.Core;

Parser.Default.ParseArguments<Options>(args)
    .WithParsed(opts =>
    {
        var app = new App(opts.Port);
        app.Start(new Uri(opts.InputString));
    });

class Options
{
    [Option('i', "input-string", Required = true)]
    public string InputString { get; set; } = default!;

    [Option('p', "port", Required = true)]
    public int Port { get; set; } = default!;
}