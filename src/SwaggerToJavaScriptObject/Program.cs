using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SwaggerToJavaScriptObject;
using SwaggerToJavaScriptObject.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton<SwaggerAnalyzerService>();
builder.Services.AddSingleton<JavaScriptObjectGeneratorService>();

await builder.Build().RunAsync();
