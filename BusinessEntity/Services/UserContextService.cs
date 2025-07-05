using BusinessEntity.Contracts;

namespace BusinessEntity.Services
{
    public class UserContextService : IUserContextService
    {
        public Guid? CurrentSpaceId { get; private set; }
        public string? CurrentSpaceName { get; private set; }
        public bool HasSelectedSpace => CurrentSpaceId.HasValue;

        public void SetSpace(Guid id, string name)
        {
            CurrentSpaceId = id;
            CurrentSpaceName = name;
        }

        public void ClearSpace()
        {
            CurrentSpaceId = null;
            CurrentSpaceName = null;
        }
    }
} 