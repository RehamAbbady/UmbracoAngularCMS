using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddUmbraco(builder.Environment, builder.Configuration)
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:4200")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
            
        });
});

builder.Services.AddControllers();

var app = builder.Build();

await app.BootUmbracoAsync();


if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
        u.AppBuilder.UseRouting();
        u.AppBuilder.UseCors("AllowAngularApp");
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

app.MapControllers();

app.Run();