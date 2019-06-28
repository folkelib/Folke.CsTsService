using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Linq;

namespace Folke.CsTsService
{
    public static class ApplicationPartManagerExtensions
    {
        public static void CreateTypeScriptServices(this ApplicationPartManager applicationPartManager, string typeScriptPath, TypeScriptOptions options)
        {
            ControllerFeature feature = new ControllerFeature();
            applicationPartManager.PopulateFeature(feature);
            var controllerTypes = feature.Controllers.Select(c => c.AsType());
            var converter = new Converter();
            var assembly = converter.ReadControllers(controllerTypes);
            var typeScript = new TypeScriptWriter(options: options);
            typeScript.WriteAssembly(assembly);
            typeScript.WriteToFiles(typeScriptPath);
        }
    }
}
