// The program the esf-format test runner calls: esf-program check|canonical|binary.

using System.Text;
using EveShipFit.EsfFormat;
using EveShipFit.EsfFormat.Tests;

var command = args.Length > 0 ? args[0] : "check";
var catalog = new JsonCatalog(Environment.GetEnvironmentVariable("ESF_CATALOG")!);
using var input = new MemoryStream();
Console.OpenStandardInput().CopyTo(input);
try
{
    var fits = Esf.Load(input.ToArray(), catalog);
    using var output = Console.OpenStandardOutput();
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
