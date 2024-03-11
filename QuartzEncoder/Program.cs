using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var openApiInfo = new OpenApiInfo()
    {
        Title = "QuartzEncoder",
        Version = "v1",
        Description = "This service converts audio from direct links and media services to DFPWM and MDFPWM.<br/>" +
            "DFPWM and MDFPWM sources are not supported!<br/>" +
            "Do not abuse, thank you, i have your IPs <3",
        License = new()
        {
            Name = "Apache License 2.0",
            Url = new("https://github.com/Ale32bit/QuartzEncoder/blob/main/LICENSE.txt"),
        },
        Contact = new()
        {
            Name = "AlexDevs",
            Url = new Uri("https://github.com/Ale32bit/QuartzEncoder"),
            Email = "quartz@alexdevs.me",
        },
        
    };

    options.SwaggerDoc("v1", openApiInfo);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "Quartz Encoder";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Quartz Encoder");
    options.RoutePrefix = "";

});

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseAuthorization();

app.MapControllers();

app.Run();
