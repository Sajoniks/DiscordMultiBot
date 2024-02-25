using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Discord;

namespace DiscordMultiBot.App.EmbedXml;

public record EmbedXmlField(string Name, string Value, bool Inline = false);

public class EmbedXmlCreator : IEmbedXmlCreator
{
    public static EmbedXmlDoc CreateEmbed(string xmlLayoutName, IReadOnlyDictionary<string, string> bindings)
    {
        return new EmbedXmlCreator(bindings).Create(xmlLayoutName);
    }

    public static EmbedXmlDoc CreateEmbed(string xmlLayoutName)
    {
        return new EmbedXmlCreator().Create(xmlLayoutName);
    }

    
    public IDictionary<string, string> Bindings { get; } = new Dictionary<string, string>();
    public IList<EmbedXmlField> Fields { get; } = new List<EmbedXmlField>();

    public EmbedXmlCreator()
    {
        
    }

    public EmbedXmlCreator(IEnumerable<KeyValuePair<string, string>> dict)
    {
        Bindings = new Dictionary<string, string>(dict);
    }
    
    private StreamReader CreateXmlStream(string name)
    {
        var fileName = $"{name}View.xml";
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().First(x => x.EndsWith(fileName));
        var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new NullReferenceException();
        return new StreamReader(stream);
    }
    
    public EmbedXmlDoc Create(string layoutName)
    {
        var doc = new XmlDocument();
        using (var stream = CreateXmlStream(layoutName))
        {
            doc.Load(stream);
        }

        var comps = new ComponentBuilder();
        var embedBuilder = new EmbedBuilder();
        XmlElement? xRoot = doc.DocumentElement;

        if (xRoot is null)
        {
            throw new Exception();
        }

        if (xRoot.HasAttribute("Color"))
        {
            string colorHex = xRoot.GetAttribute("Color");
            if (
                colorHex.StartsWith('{') && 
                colorHex.EndsWith('}')
            )
            {
                string colorHexBinding = colorHex.Substring(1, colorHex.Length - 2);
                if (Bindings.ContainsKey(colorHexBinding))
                {
                    colorHex = Bindings[colorHexBinding];
                }
                else
                {
                    colorHex = "";
                }
            }

            try
            {
                byte[] colorBytes = Convert.FromHexString(colorHex);
                embedBuilder.WithColor(colorBytes[0], colorBytes[1], colorBytes[2]);
            }
            catch (Exception)
            {
                // ignore color field
            }
        }

        string FormatString(string str)
        {
            string pattern = @"(?:\{([a-zA-Z0-9]*)\})";
            var bindingsMatches = Regex.Matches(str, pattern);

            foreach (Match match in bindingsMatches)
            {
                if (Bindings.ContainsKey(match.Groups[1].Value))
                {
                    str =
                        str.Replace(match.Captures[0].Value, Bindings[match.Groups[1].Value]);
                }
            }

            return str;
        }

        var sbText = new StringBuilder();

        XmlNode? fieldsNode = null;
        foreach (XmlNode xNode in xRoot)
        {
            XmlNode? xId = xNode.Attributes?.GetNamedItem("Id");
            if ((xNode.Name.Equals("Element") || xNode.Name.Equals("Button")) && xId?.Value != null)
            {
                bool isEmoji = false;
                string bindingValue = "";
                string id = xId.Value;
                if (xNode.FirstChild?.Value is not null)
                {
                    isEmoji = xNode.FirstChild.Name.Equals("Emoji");
                    bindingValue = FormatString(xNode.FirstChild.Value);
                }
                else if (xNode.FirstChild is not null)
                {
                    isEmoji = xNode.FirstChild.Name.Equals("Emoji");
                    if (xNode.FirstChild.FirstChild?.Value is not null)
                    {
                        bindingValue = FormatString(xNode.FirstChild.FirstChild.Value);
                    }
                }

                if (bindingValue.Length == 0 && Bindings.ContainsKey(id))
                {
                    bindingValue = Bindings[id];
                }

                if (bindingValue.Length > 0)
                {
                    if (id.Equals("Title"))
                    {
                        embedBuilder.WithTitle(bindingValue);
                    }
                    else if (id.Equals("Desc"))
                    {
                        embedBuilder.WithDescription(bindingValue);
                    }
                    else if (id.Equals("Footer"))
                    {
                        embedBuilder.WithFooter(bindingValue);
                    }
                    else if (id.Equals("Text"))
                    {
                        sbText.AppendLine(bindingValue);
                    }
                    else if (xNode.Name.Equals("Button"))
                    {
                        string style = xNode.Attributes?.GetNamedItem("Style")?.Value ?? ButtonStyle.Primary.ToString();
                        var styleEnum = Enum.Parse<ButtonStyle>(style);
                        
                        var button = new ButtonBuilder()
                            .WithStyle(styleEnum)
                            .WithCustomId(id);

                        if (isEmoji)
                        {
                            button.WithEmote(new Emoji(bindingValue));
                        }
                        else
                        {
                            button.WithLabel(bindingValue);
                        }

                        comps.WithButton(button);
                    }
                }
            }
            else if (xNode.Name.Equals("Fields"))
            {
                fieldsNode = xNode;
                continue;
            }
        }

        if (fieldsNode is not null)
        {
            foreach (XmlNode xField in fieldsNode.ChildNodes)
            {
                if (!xField.Name.Equals("Field")) continue;

                var nameAttr = xField.Attributes?.GetNamedItem("Name");
                var descAttr = xField.Attributes?.GetNamedItem("Value");
                var inlineAttr = xField.Attributes?.GetNamedItem("Inline");
                
                if (nameAttr is null || descAttr is null || nameAttr.Value is null || descAttr.Value is null) continue;

                string name = FormatString( nameAttr.Value );
                string value = FormatString( descAttr.Value );
                bool inline = false;

                if (inlineAttr is not null && inlineAttr.Value is not null)
                {
                    try
                    {
                        inline = Convert.ToBoolean(inlineAttr.Value);
                    }
                    catch (Exception)
                    {
                        inline = false;
                    }
                }

                embedBuilder.AddField(name, value, inline);
            }
        }
        
        foreach (var field in Fields)
        {
            embedBuilder.AddField(field.Name, field.Value, field.Inline);
        }
        
        return new EmbedXmlDoc(
            Text: sbText.ToString(),
            Embeds: new[] { embedBuilder.Build() },
            Comps: comps.Build()
        );
    }
}