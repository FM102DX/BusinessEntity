using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using BusinessEntity.Core.DomainEntities;
using BusinessEntity.Core.Services;
using BusinessEntity.MiniApps.UserMiniApp.Contracts;
using BusinessEntity.MiniApps.DataProviderMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;

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
        private bool RequestedEditMode => string.Equals(EditQuery, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(EditQuery, "true", StringComparison.OrdinalIgnoreCase);
        private bool StartInEditMode => RequestedEditMode && CanEditDocument;
        private bool IsDocumentOwner { get; set; }
        private bool IsCurrentUserAdmin { get; set; }
        private bool HasFullDocumentAccess { get; set; }
        private bool CanViewPublishedDocument { get; set; }
        private bool CanEditDocument => HasFullDocumentAccess;
        private bool CanChangePublicFlag => IsDocumentOwner;

        [Inject] public BusinessEntityHelper Helper { get; set; } = default!;
        [Inject] public IDataProviderConnector DataProviderConnector { get; set; } = default!;
        [Inject] public IUserConnector UserConnector { get; set; } = default!;

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

                var latestDocument = await DataProviderConnector.GetDataAsync<global::BusinessEntity.Core.DomainEntities.Document>(Id);
                await ResolveAccessAsync();
                if (!HasFullDocumentAccess &&
                    (!CanViewPublishedDocument || (latestDocument?.PublishedVersion ?? 0) <= 0))
                {
                    Entity = null;
                    DataList = null;
                    Error = "Документ недоступен: опубликованная версия отсутствует.";
                    return;
                }

                IReadOnlyList<global::BusinessEntity.Core.Classes.BusinessEntityData>? list;
                if (HasFullDocumentAccess)
                {
                    list = await Helper.GetData(Entity);
                }
                else
                {
                    var publishedData = await DataProviderConnector.GetDataVersionAsync<global::BusinessEntity.Core.DomainEntities.Document>(
                        Id,
                        latestDocument!.PublishedVersion);
                    list = publishedData == null
                        ? Array.Empty<global::BusinessEntity.Core.Classes.BusinessEntityData>()
                        : new global::BusinessEntity.Core.Classes.BusinessEntityData[] { publishedData };
                }

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

        private async Task ResolveAccessAsync()
        {
            IsDocumentOwner = false;
            IsCurrentUserAdmin = false;
            HasFullDocumentAccess = false;
            CanViewPublishedDocument = false;
            if (Entity == null)
            {
                return;
            }

            var user = await UserConnector.EnsureCurrentUserAsync();
            var businessUser = await UserConnector.GetCurrentUserAsync();
            var permissions = await UserConnector.GetCurrentUserPermissionsForEntityAsync(Entity.Id);
            IsDocumentOwner = user != null && Entity.CreatedByUserId == user.Id;
            IsCurrentUserAdmin = IsAccessAdmin(businessUser);
            HasFullDocumentAccess = IsCurrentUserAdmin || IsDocumentOwner || permissions.CanViewDraft;
            CanViewPublishedDocument = HasFullDocumentAccess || permissions.CanViewPublished || Entity.IsPublic;
        }

        private static bool IsAccessAdmin(BusinessEntityUser? user)
        {
            return user?.IsAkadmin == true ||
                   user?.IsGeneralAdmin == true ||
                   string.Equals(user?.UserName, "admin", StringComparison.OrdinalIgnoreCase);
        }

        private async Task HandlePublicChangedAsync(bool value)
        {
            if (Entity == null || !CanChangePublicFlag)
            {
                return;
            }

            Entity.IsPublic = value;
            Entity.LastModifiedDate = DateTime.UtcNow;
            await DataProviderConnector.UpdateAsync(Entity);
        }
    }
}
