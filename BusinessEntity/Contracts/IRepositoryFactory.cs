namespace BusinessEntity.Contracts
{
    public interface IRepositoryFactory<T> where T : IBaseEntity
    {
        IAsyncRepository<T> GetRepository();
    }
}
