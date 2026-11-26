// The program the esf-format test runner calls: esf-program check|canonical|binary|extra.

using System.Text;
using System.Text.Json;
using EveShipFit.EsfFormat;
using EveShipFit.EsfFormat.Tests;

var command = args.Length > 0 ? args[0] : "check";
var catalog = new JsonCatalog(Environment.GetEnvironmentVariable("ESF_CATALOG")!);
using var input = new MemoryStream();
Console.OpenStandardInput().CopyTo(input);
try
{
    using var output = Console.OpenStandardOutput();
    if (command == "extra")
    {
        foreach (var extra in Esf.LoadDocument(input.ToArray(), catalog).Extra)
        {
            var line = extra switch
            {
                TextExtra text => JsonSerializer.Serialize(new { name = text.Name, version = text.Version, text = text.Text }),
                BinaryExtra binary => JsonSerializer.Serialize(new { field = binary.Field, bytes = Esf.ToBase64Url(binary.Bytes) }),
                _ => throw new InvalidOperationException(),
            };
            output.Write(Encoding.UTF8.GetBytes(line + "\n"));
        }
        return 0;
    }
    var fits = Esf.Load(input.ToArray(), catalog);
    if (command == "canonical")
    {
        output.Write(Encoding.UTF8.GetBytes(Esf.Save(fits, catalog)));
    }
    else if (command == "binary")
    {
        output.Write(Esf.SaveBinary(fits, catalog));
    }
}
catch (EsfException e)
{
    Console.Error.WriteLine($"invalid: {e.Message}");
    return 1;
}
return 0;
