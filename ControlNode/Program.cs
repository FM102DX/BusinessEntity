using ControlNode.Data;
using ControlNode.Data.AssortmentLoader;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SampleOnlineMall.Core;
using SampleOnlineMall.DataAccess.DataAccess;
using SampleOnlineMall.WebLogger.Services;

namespace ControlNode
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();

            //логгирование в веб-логгер
            builder.Services.AddScoped<IWebLoggerService>(provider => new WebLoggerService("CTRL"));
            var serviceProvider = builder.Services.BuildServiceProvider();
            var wLogger = serviceProvider.GetRequiredService<IWebLoggerService>();
            wLogger.Information("Приложение запущено");

            var opts = new WebApiAsyncRepositoryOptions()
                .SetBaseAddress("http://localhost:5010")
                .SetInsertHostPath("insertitem")
                .SetDeleteAllHostPath("deleteallitems");
            builder.Services.AddScoped<WebApiAsyncRepository<CommodityItemApiFeed>>(provider =>
            {
                return new WebApiAsyncRepository<CommodityItemApiFeed>(opts, wLogger);
            });

            builder.Services.AddScoped<AssortmentLoader>();
            builder.Services.Configure<AppSettings>(builder.Configuration);


            var app = builder.Build();
            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts
                app.UseHsts();
            }
            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.MapBlazorHub();

            app.MapFallbackToPage("/_Host");

            app.Run();
        }
    }
}