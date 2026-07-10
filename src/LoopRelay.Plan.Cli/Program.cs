using System.Text;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Output is redirected.
}

Console.Error.WriteLine("LoopRelay.Plan.Cli is retired as an orchestration entry point.");
Console.Error.WriteLine("Use the unified CLI instead:");
Console.Error.WriteLine("  looprelay plan");
return 1;
