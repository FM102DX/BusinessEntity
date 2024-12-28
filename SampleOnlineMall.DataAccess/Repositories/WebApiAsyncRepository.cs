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
using System.Net.Mime;

namespace SampleOnlineMall.DataAccess.DataAccess
{
    public class WebApiAsyncRepository<T> : IAsyncRepository<T> where T : BaseEntity
    {
        //GenericRepository на webApi

        private static readonly object _locker = new object();

        private HttpClient httpClient;

        private Serilog.ILogger _logger
        {
            get { return _options.Logger; }
        }

        private WebApiAsyncRepositoryOptions _options;

        public WebApiAsyncRepository(WebApiAsyncRepositoryOptions options)
        {
            _options = options;
            httpClient = new System.Net.Http.HttpClient(new HttpClientHandler());
            httpClient.BaseAddress = new Uri(_options.BaseAddress);
            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }


        //getall

        public async Task<int> GetCountAsync()
        {
            int rez = -1;
            try
            {
                var response = await httpClient.GetAsync($"{_options.CountHostPath}");
                var json = response.Content.ReadAsStringAsync().Result;
                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:

                        rez = JsonConvert.DeserializeObject<int>(json);
                        break;
                    default:
                        throw new Exception();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            return rez;
        }

        public async Task<IEnumerable<T>> SearchAsync(string searchText)
        {
            IEnumerable<T> items =  new List<T>();
            try
            {
                _logger.Information($"This is WebApiAsyncRepository.search searchText={searchText}");
                
                _logger.Information($"Sending request to {httpClient.BaseAddress}");

                var response = await httpClient.GetAsync($"{_options.SearchHostPath}/{searchText}");
                
                var json = await response.Content.ReadAsStringAsync();

                _logger.Information($"Received json {json}");

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        items = JsonConvert.DeserializeObject<IEnumerable<T>>(json);
                        break;
                    default:
                        throw new Exception();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"ERROR: in WebApiAsyncRepository.search {ex.Message}");
            }
            return items;
        }

        public async Task<IEnumerable<T>> GetAllAsync()
        {
            IEnumerable<T> items = new List<T>();

            try
            {
                var response = await httpClient.GetAsync($"{_options.GetAllHostPath}");

                var json = await response.Content.ReadAsStringAsync();
                
                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        items = JsonConvert.DeserializeObject<IEnumerable<T>>(json);
                        break;
                    default:
                        throw new Exception();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            return items;
        }


        public async Task<T> GetByIdOrNullAsync(Guid id)
        {
            T item = null;
            try
            {
                var response = await httpClient.GetAsync($"{_options.GetByIdOrNullHostPath}{id}");
                var json = await response.Content.ReadAsStringAsync();
                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        item = JsonConvert.DeserializeObject<T>(json);
                        break;
                    default:
                        throw new Exception();
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex.Message);
            }
            return item;
        }

        public async Task<bool> Exists(Guid id)
        {
            var rez = await GetByIdOrNullAsync(id);
            return rez != null;
        }

        public async Task<CommonOperationResult> AddAsync(T t)
        {
            CommonOperationResult rez;
            try
            {
                string json;
                StringContent jsonContent;

                json = JsonConvert.SerializeObject(t, Formatting.Indented,
                        new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                jsonContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PostAsync($"{_options.InsertHostPath}", jsonContent);

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        rez = CommonOperationResult.SayOk(response.Content.ReadAsStringAsync().Result);
                        break;
                    default:
                        throw new Exception();
                }
            }
            catch (Exception ex)
            {
                rez = CommonOperationResult.SayFail($"WebApiRepository caught an exception: {ex.Message}  {ex.StackTrace}");
            }
            return rez;
        }

        public async Task<CommonOperationResult> UpdateAsync(T t)
        {
            CommonOperationResult rez;
            try
            {
                string json;

                StringContent jsonContent;

                json = JsonConvert.SerializeObject(t, Formatting.Indented,
                        new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });

                jsonContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await httpClient.PutAsync($"{_options.UpdateHostPath}", jsonContent);

                switch (response.StatusCode)
                {

                    case System.Net.HttpStatusCode.OK:
                        rez = CommonOperationResult.SayOk(response.Content.ReadAsStringAsync().Result);
                        break;
                    default:
                        throw new Exception();
                }
            }
            catch (Exception ex)
            {
                rez = CommonOperationResult.SayFail($"{ex.Message}");
            }
            return rez;
        }

        public async Task<CommonOperationResult> DeleteAsync(Guid id)
        {

            CommonOperationResult rez;
            try
            {
                var response = await httpClient.DeleteAsync($"{_options.DeleteHostPath}/{id}");

                switch (response.StatusCode)
                {
                    case System.Net.HttpStatusCode.OK:
                        rez = CommonOperationResult.SayOk(response.Content.ReadAsStringAsync().Result);
                        break;
                    default:
                        throw new Exception();
                }
            }
            catch (Exception ex)
            {
                rez = CommonOperationResult.SayFail($"{ex.Message}");
            }
            return rez;

        }

        public async Task<CommonOperationResult> InitAsync(bool deleteDb)
        {
            return await Task.FromResult(CommonOperationResult.SayOk());
        }

        public Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> filter)
        {
            throw new NotImplementedException();
        }

        public async Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestFuncSearch<T> repositoryRequest)
        {
           //web api repository not intended to make a func search because func cant be passed through api
            throw new NotImplementedException();
        }

        public async Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestTextSearch repositoryRequest)
        {
            var repositoryRequestJson = JsonConvert.SerializeObject(repositoryRequest);
            RepositoryResponce<T> repositoryResponce = new RepositoryResponce<T>();
            var httpRequest = new HttpRequestMessage();
            httpRequest.Method = HttpMethod.Post;
            httpRequest.RequestUri = new System.Uri($"{httpClient.BaseAddress}{_options.GetAllByRequestHostPath}");
            httpRequest.Content = new StringContent(repositoryRequestJson, Encoding.UTF8, MediaTypeNames.Application.Json);
            
            try
            {
                var response = await httpClient.SendAsync(httpRequest);
                var json = await response.Content.ReadAsStringAsync();
                repositoryResponce = (RepositoryResponce<T>)JsonConvert.DeserializeObject<RepositoryResponce<T>>(json);
                _logger.Debug($"[WebApiRepository.GetAllByRequestAsync]: response.StatusCode={response.StatusCode} json={json}");
                repositoryResponce.Result = CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                var msg = $"Web api repo--GetAllByRequestAsync--err--message={ex.Message} innerEx={ex.InnerException}";
                _logger.Error(msg);
                repositoryResponce.Result = CommonOperationResult.SayOk(msg);
            }
            return repositoryResponce;
        }
    }
}