using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Text;
using Newtonsoft.Json.Serialization;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;
using System.Linq.Expressions;

namespace SampleOnlineMall.DataAccess.DataAccess
{
    public class WebApiAsyncRepositoryOptions
    {
        public Serilog.ILogger Logger { get; set; }
        public string CountHostPath { get; set; }
        public string GetAllHostPath { get; set; }
        public string GetAllByRequestHostPath { get; set; }
        public string InsertHostPath { get; set; }
        public string UpdateHostPath { get; set; }
        public string DeleteHostPath { get; set; }
        public string DeleteAllHostPath { get; set; }
        public string SearchHostPath { get; set; }
        public string GetByIdOrNullHostPath { get; set; }
        public string BaseAddress { get; set; }

        public WebApiAsyncRepositoryOptions ()
        {
            
        }
        public WebApiAsyncRepositoryOptions SetLogger(Serilog.ILogger logger)
        {
            Logger = logger;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetGetAllHostPath(string text)
        {
            GetAllHostPath = text;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetGetAllByRequestHostPath(string text)
        {
            GetAllByRequestHostPath = text;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetInsertHostPath(string text)
        {
            InsertHostPath = text;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetUpdateHostPath(string text)
        {
            UpdateHostPath = text;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetDeleteHostPath(string text)
        {
            DeleteHostPath = text;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetDeleteAllHostPath(string text)
        {
            DeleteAllHostPath = text;
            return this;
        }
        

        public WebApiAsyncRepositoryOptions SetSearchHostPath(string text)
        {
            SearchHostPath = text;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetGetByIdOrNullHostPath(string text)
        {
            GetByIdOrNullHostPath = text;
            return this;
        }
        public WebApiAsyncRepositoryOptions SetBaseAddress(string baseAddress)
        {
            BaseAddress = baseAddress;
            return this;
        }

    }
}