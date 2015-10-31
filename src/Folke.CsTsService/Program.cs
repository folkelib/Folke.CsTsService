using System;
using System.Collections.Generic;

namespace Folke.CsTsService
{
    public class Program
    {
        static void Main(string[] args)
        {
            var assemblies = new List<string>();
            string helperNamespace = ".";
            string outputPath = "services.ts";
            IApiAdapter adapter = null;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("-"))
                {
                    switch (arg)
                    {
                        case "-h":
                            helperNamespace = args[++i];
                            break;
                        case "-o":
                            outputPath = args[++i];
                            break;
                        case "-m":
                            adapter = new MvcAdapter();
                            break;
                        default:
                            throw new Exception("Unknown option " + arg);
                    }
                }
                else
                {
                    assemblies.Add(arg);
                }
            }

            var converter = new Converter(adapter ?? new WaAdapter());
            converter.Write(assemblies, outputPath, helperNamespace);
        }
    }
}
