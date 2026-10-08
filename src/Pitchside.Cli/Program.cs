using System.Globalization;
using Pitchside.Cli;

return await CommandLine.RunAsync(args, Console.Out, CultureInfo.InvariantCulture).ConfigureAwait(false);
