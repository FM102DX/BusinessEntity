using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using SampleOnlineMall.DataAccess.Abstract;
using SampleOnlineMall.DataAccess.Models;
using SampleOnlineMall.Service;
using System.Linq.Expressions;
using Newtonsoft.Json;
using SampleOnlineMall.WebLogger.Services;

namespace SampleOnlineMall.DataAccess
{
    public class EfAsyncRepository<T> : IAsyncRepository<T> where T: BaseEntity
    {
        private DbContext           _context;
        private IWebLoggerService _logger;
        public EfAsyncRepository(DbContext context, IWebLoggerService logger)
        {
            _context = context;
            _logger = logger;
        }

        //GetAll
        public Task<IEnumerable<T>> GetAllAsync()
        {
            try
            {
                var rez = Task.FromResult((IEnumerable<T>)_context.Set<T>().AsNoTracking());
                return rez;
            }
            catch (Exception ex)
            {
                List<T> lst = new List<T>();
                IEnumerable<T> en = (IEnumerable<T>)lst;
                return Task.FromResult(en);
            }
        }
        public Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestTextSearch request)
        {
            int totalCount = GetCountAsync().Result;

            RepositoryResponce<T> responce = new RepositoryResponce<T>()
            {
                UsedPagination = request.UsePagination,
                UsedSearch = request.UseSearch
            };
            _logger.Debug("This is repo -- GetAllByRequestAsync");
            try
            {
                if(request.UsePagination)
                {
                    _logger.Debug("Using pagination");
                    if(request.Page <= 0)
                    {
                        request.Page = 1;
                    }
                    int offset = request.Page-1;
                    var rez = Task.FromResult((IEnumerable<T>)_context
                                    .Set<T>()
                                    .Skip(request.ItemsPerPage * offset)
                                    .Take(request.ItemsPerPage).AsNoTracking());
                    responce.Page = request.Page;
                    responce.ItemsPerPage = request.ItemsPerPage;
                    responce.TotalCount = totalCount;
                    responce.Items = rez.Result;
                    _logger.Debug("End--Using pagination");
                }
                else
                {
                    _logger.Information("Not Using pagination");
                    var rez = Task.FromResult((IEnumerable<T>)_context
                                    .Set<T>()
                                    .AsNoTracking());
                    _logger.Debug("End--Not Using pagination");
                    
                    responce.Items = rez.Result;
                }
                responce.Result = CommonOperationResult.SayOk();
                _logger.Debug($"Result is {JsonConvert.SerializeObject(responce)}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Repository GetAllByRequestAsync error={ex.Message} innerex={ex.InnerException}");
                responce.Result = CommonOperationResult.SayFail();
                responce.Items = new List<T>();

            }
            return Task.FromResult(responce);
        }

        public async Task<RepositoryResponce<T>> GetAllByRequestAsync(RepositoryRequestFuncSearch<T> request)
        {
            

            RepositoryResponce<T> responce = new RepositoryResponce<T>()
            {
                UsedPagination = request.UsePagination,
                UsedSearch = request.UseSearch
            };
            if (request.Page <= 0)
            {
                request.Page = 1;
            }
            int offset = request.Page - 1;
            try
            {
                if (request.UsePagination && request.UseSearch)
                {
                    var rez = Task.FromResult((IEnumerable<T>)_context
                                    .Set<T>()
                                    .Where(request.SearchFunc)
                                    .AsQueryable<T>()
                                    .Skip(request.ItemsPerPage * offset)
                                    .Take(request.ItemsPerPage)
                                    .AsNoTracking());

                    responce.Page = request.Page;
                    responce.ItemsPerPage = request.ItemsPerPage;
                    responce.Items = rez.Result;
                    responce.TotalCount = responce.Items.ToList().Count;
                    
                }
                else if (!request.UsePagination && request.UseSearch)
                {
                    var rez = Task.FromResult((IEnumerable<T>)_context
                                    .Set<T>()
                                    .Where(request.SearchFunc)
                                    .AsQueryable<T>()
                                    .AsNoTracking());
                    responce.Items = rez.Result;
                    responce.TotalCount = responce.Items.ToList().Count;
                }
                else if (request.UsePagination && !request.UseSearch)
                {
                    var rez = Task.FromResult((IEnumerable<T>)_context
                                        .Set<T>()
                                        .Skip(request.ItemsPerPage * offset)
                                        .Take(request.ItemsPerPage)
                                        .AsNoTracking());
                    responce.Page = request.Page;
                    responce.ItemsPerPage = request.ItemsPerPage;
                    responce.TotalCount = await GetCountAsync();
                    responce.Items = rez.Result;
                }
                else if (!request.UsePagination && !request.UseSearch)
                {
                    var rez = Task.FromResult((IEnumerable<T>)_context.Set<T>().AsNoTracking());
                    responce.Items = rez.Result;
                }
                responce.Result = CommonOperationResult.SayOk();
            }
            catch (Exception ex)
            {
                responce.Result = CommonOperationResult.SayFail($"Error EfAsyncRepository GetAllByRequestAsync msg={ex.Message} innerex={ex.InnerException}");
                responce.Items = new List<T>();
            }
            return responce;
        }

        //others
        public Task<T> GetByIdOrNullAsync(Guid id)
        {
            return _context.Set<T>().SingleOrDefaultAsync(e => e.Id == id);
        }

        public Task<bool> ExistsAsync(Guid id)
        {
            T targetObejct = GetByIdOrNullAsync(id).Result;
            return Task.FromResult(targetObejct == null);
        }

        public Task<CommonOperationResult> AddAsync(T t)
        {
            try
            {
                _logger.Information($"This is repository. Gonna insert object {t}");
                _context.Set<T>().Add(t);
                _context.SaveChanges();
                _logger.Information($"Successful");
                return Task.FromResult(CommonOperationResult.SayOk());
            }
            catch (Exception ex)
            {
                var errMsg = $"Error while inserting {ex}, innerexception is {ex.InnerException}";
                _logger.Debug(errMsg);
                return Task.FromResult(CommonOperationResult.SayFail(errMsg));
            }
        }

        public Task<CommonOperationResult> UpdateAsync(T t)
        {
            _context.Set<T>().Update(t);
            var rez = _context.SaveChanges();
            return Task.FromResult(CommonOperationResult.SayOk(rez.ToString()));
        }

        public Task<CommonOperationResult> DeleteAsync(Guid id)
        {
            T t = this.GetByIdOrNullAsync(id).Result;
            if (t == null)
            {
                return Task.FromResult(CommonOperationResult.SayFail($"Id not found: {id}"));
            }
            _context.Set<T>().Remove(t);
            var rez = _context.SaveChanges();
            return Task.FromResult(CommonOperationResult.SayOk(rez.ToString()));
        }

        public Task<bool> Exists(Guid id)
        {
            var t = this.GetByIdOrNullAsync(id).Result;
            return Task.FromResult(t != null);
        }

        public Task<CommonOperationResult> InitAsync(bool deleteDb=false)
        {
            if (deleteDb) _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();
            return Task.FromResult(CommonOperationResult.SayOk());
        }

        public Task<List<T>> GetItemsListAsync()
        {
            List<T> rez = new List<T>();

            IEnumerable<T> list = GetAllAsync().Result;

            foreach (T t in list)
            {
                rez.Add(t);
            }
            return Task.FromResult(rez);
        }

        public Task<int> GetCountAsync()
        {
            return Task.FromResult(_context.Set<T>().Count());
        }

        public Task<IEnumerable<T>> SearchAsync(Expression<Func<T, bool>> filter)
        {
            var rez = (IEnumerable<T>)(_context.Set<T>().Where(filter));
            return Task.FromResult(rez);
        }

        public Task<IEnumerable<T>> SearchAsync(string text)
        {
            throw new NotImplementedException();
        }

    }
}