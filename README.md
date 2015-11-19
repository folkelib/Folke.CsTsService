Folke.CsTsService
======

This is a library to create a Typescript client from a .NET MVC6 Controllers.

### Usage

The easiest method to use this library is to add it to your website project. You then
add a new entry point to your project that will write the Typescript file instead of
serving web pages.

Sample entry point:
```cs
public class Program
{
	static void Main(string[] args)
	{
		var converter = new Converter(new WaAdapter());
		converter.Write(new [] { typeof(MyController).Assembly}, 
			"src/services.ts", 
			"bower_components/folke-ko-service-helpers/folke-ko-service-helpers",
			"bower_components/folke-ko-validation/folke-ko-validation");
	}
}
```

The first argument is a list of assembly where the controllers are. The second argument
is the output path. The third and four arguments are the module names of the two bower packages
[folke-ko-service-helpers](https://github.com/folkelib/folke-ko-service-helpers) and
[folke-ko-validation](https://github.com/folkelib/folke-ko-validation) that are needed
by `services.ts`.

In this example the WaAdapter is used, it supposes that you use the [Folke.Mvc.Extensions](https://github.com/folkelib/Folke.Mvc.Extensions)
library that have extensions that allow to know what is the return type of a controller route. If you prefer to
use bare MVC, use the MvcAdapter.
