using System;
using System.Collections.Generic;
using Folke.CsTsService.Nodes;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Folke.CsTsService.Tests
{
    public class TestConverter
    {
        private readonly Converter converter;
        private readonly TypeScriptWriter writer;

        public TestConverter()
        {
            converter = new Converter();
            writer = new TypeScriptWriter();
        }

        [Fact]
        public void Converter()
        {
            // Arrange
            var assemblyNode = converter.ReadControllers(new[] {typeof(TestController)});

            // Act
            writer.WriteAssembly(assemblyNode);

            // Assert
            var service = writer.OutputModules["test"];
            Assert.Equal(@"/* This is a generated file. Do not modify or all the changes will be lost. */
import * as helpers from ""folke-service-helpers"";
import * as views from ""./views"";

export class TestController {
	constructor(private client: helpers.ApiClient) {}

    get = () => {
        return this.client.fetchJson<views.Test>(""test/"", ""GET"", undefined);
    }
}

", service.Replace("\r\n", "\n"));
            var views = writer.OutputModules["views"];
            Assert.Equal(@"
export interface Test {
    toto: { [key: string]: string };

    dicOfDic: { [key: string]: { [key: string]: number } };

    byte: number;
}
", views.Replace("\r\n", "\n"));
        }

        private class TestView
        {
            public Dictionary<string, string> Toto { get; set; }
            public Dictionary<string, Dictionary<string, int>> DicOfDic { get; set; }
            public byte Byte { get; set; }
        }

        [Route("test")]
        private class TestController : ControllerBase
        {
            [HttpGet("")]
            public ActionResult<TestView> Get()
            {
                return Ok();
            }
        }
    }
}
