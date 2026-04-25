using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.Services;

namespace BusinessEntity.Pages
{
    public partial class DocumentPage
    {
        [Parameter]
        public Guid Id { get; set; }

        [SupplyParameterFromQuery(Name = "edit")]
        public string? EditQuery { get; set; }

        private global::BusinessEntity.Core.Classes.BusinessEntity? Entity;
        private IReadOnlyList<global::BusinessEntity.Core.Classes.BusinessEntityData>? DataList;
        private string? DataText;
        private bool IsLoading = true;
        private string? Error;
        private bool StartInEditMode => string.Equals(EditQuery, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(EditQuery, "true", StringComparison.OrdinalIgnoreCase);

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
                if (Entity.EntityType != global::BusinessEntity.Core.Classes.BusinessEntityTypeEnum.Document && Entity.EntityType != global::BusinessEntity.Core.Classes.BusinessEntityTypeEnum.Document)
                {
                    Error = "Сущность не является документом или страницей.";
                    return;
                }

                var list = await Helper.GetData(Entity);
                DataList = list;
                if (list != null && list.Any())
                {
                    DataText = string.Join("\n\n", list.Select(GetBodyText));
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

        private static string GetBodyText(global::BusinessEntity.Core.Classes.BusinessEntityData data)
        {
            return data switch
            {
                global::BusinessEntity.Core.DomainEntities.Document document => document.Text ?? string.Empty,
                _ => string.Empty
            };
        }
    }
}
