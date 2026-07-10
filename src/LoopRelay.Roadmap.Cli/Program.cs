using System.Text;

try
{
    Console.OutputEncoding = Encoding.UTF8;
}
catch (IOException)
{
    // Output is redirected.
}

Console.Error.WriteLine("LoopRelay.Roadmap.Cli is retired as an orchestration entry point.");
Console.Error.WriteLine("Use the unified CLI instead:");
Console.Error.WriteLine("  looprelay traditional");
Console.Error.WriteLine("  looprelay eval");
Console.Error.WriteLine("  looprelay storage <init|import|export|sync|verify>");
return 1;
