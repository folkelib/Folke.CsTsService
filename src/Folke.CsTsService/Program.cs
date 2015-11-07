using System;
using System.Collections.Generic;

namespace Folke.CsTsService
{
    public class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("   Folke.CsTsService (Assembly.dll)+ [-h] -o OutputPath [-m]");
                Console.WriteLine("Options:");
                Console.WriteLine("   -h helper module (default: folke-ko-service-helpers)");
                Console.WriteLine("   -v validator module (default : folke-ko-validator)");
                Console.WriteLine("   -o typescript file output path");
                Console.WriteLine("   -m use MvcAdapter");
                return;
            }
            var assemblies = new List<string>();
            string serviceHelpersModule = "folke-ko-service-helpers";
            string validatorModule = "folke-ko-validator";
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
                            serviceHelpersModule = args[++i];
                            break;
                        case "-o":
                            outputPath = args[++i];
                            break;
                        case "-m":
                            adapter = new MvcAdapter();
                            break;
                        case "-v":
                            validatorModule = args[++i];
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
            converter.Write(assemblies, outputPath, serviceHelpersModule, validatorModule);
        }
    }
}
