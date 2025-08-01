using System.IO;
using System.IO.Compression;

var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..")); // 4 levels up
var input = Path.Combine(solutionRoot, "BackseatDriver", "Data", "instances_data.json");
var output = Path.Combine(solutionRoot, "BackseatDriver", "Data", "instances_data.json.gz");

using var inFile = File.OpenRead(input);
using var outFile = File.Create(output);
using var compressor = new GZipStream(outFile, CompressionLevel.Optimal);
inFile.CopyTo(compressor);
