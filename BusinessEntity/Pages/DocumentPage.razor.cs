using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Services;

namespace BusinessEntity.Pages
{
    public partial class DocumentPage
    {
        [Parameter]
        public Guid Id { get; set; }

        private Core.Classes.BusinessEntity? Entity;
        private string? DataText;
        private bool IsLoading = true;
        private string? Error;

        [Inject] public BusinessEntityHelper Helper { get; set; } = default!;

        protected override async Task OnParametersSetAsync()
        {
            IsLoading = true;
            Error = null;
            try
            {
                Entity = await Helper.GetBusinessEntityById(Id);
                if (Entity == null)
                {
                    Error = "Документ не найден.";
                    return;
                }
                if (Entity.EntityType != BusinessEntityTypeEnum.Document && Entity.EntityType != BusinessEntityTypeEnum.Document)
                {
                    Error = "Сущность не является документом или страницей.";
                    return;
                }

                var list = await Helper.GetData(Entity);
                if (list != null && list.Any())
                {
                    DataText = string.Join("\n\n", list.Select(d => d.Data));
                }
                else
                {
                    DataText = null;
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
