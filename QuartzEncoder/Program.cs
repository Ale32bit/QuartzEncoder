var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.DocumentTitle = "Quartz Encoder";
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Quartz Encoder");
    options.RoutePrefix = "";

});
app.UseAuthorization();

app.MapControllers();

app.Run();
