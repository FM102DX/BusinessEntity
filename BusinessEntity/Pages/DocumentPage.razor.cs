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
using BusinessEntity.MiniApps.TreeMiniApp.Internal;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Connectors;
using BusinessEntity.MiniApps.UserMiniApp.Contracts.Dtos;
using BusinessEntity.Services;
using Microsoft.JSInterop;
using ReactiveUI;

namespace BusinessEntity.Pages
{
    public partial class DocumentPage
    {
        [Parameter]
        public Guid Id { get; set; }

        [Parameter]
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
        private bool CanEditDocument { get; set; }
        private bool CanDeleteDocument { get; set; }
        private bool CanChangePublicFlag { get; set; }

        [Inject] public BusinessEntityHelper Helper { get; set; } = default!;
        [Inject] public IDataProviderConnector DataProviderConnector { get; set; } = default!;
        [Inject] public IUserConnector UserConnector { get; set; } = default!;
        [Inject] private TreeMiniAppService TreeMiniAppService { get; set; } = default!;
        [Inject] public ITreeSelectionService TreeSelectionService { get; set; } = default!;
        [Inject] public IMessageBus MessageBus { get; set; } = default!;
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] public NavigationManager NavigationManager { get; set; } = default!;

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
                if (!HasFullDocumentAccess && !CanViewPublishedDocument)
                {
                    Entity = null;
                    DataList = null;
                    Error = "Документ недоступен.";
                    return;
                }

                IReadOnlyList<global::BusinessEntity.Core.Classes.BusinessEntityData>? list;
                if (HasFullDocumentAccess)
                {
                    list = await Helper.GetData(Entity);
                }
                else
                {
                    list = latestDocument == null
                        ? Array.Empty<global::BusinessEntity.Core.Classes.BusinessEntityData>()
                        : new global::BusinessEntity.Core.Classes.BusinessEntityData[] { latestDocument };
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

                TreeSelectionService.RequestEntitySelection(Entity.Id);
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
            CanEditDocument = false;
            CanDeleteDocument = false;
            CanChangePublicFlag = false;
            if (Entity == null)
            {
                return;
            }

            var access = await UserConnector.GetCurrentUserContentAccessForEntityAsync(
                new UserContentAccessRequest
                {
                    EntityId = Entity.Id,
                    EntityType = Entity.EntityType,
                    IsCommon = Entity.IsPublic,
                    CreatedByUserId = Entity.CreatedByUserId,
                    PublishedVersion = 0
                });
            IsDocumentOwner = access.IsOwner;
            IsCurrentUserAdmin = access.IsAccessAdmin;
            HasFullDocumentAccess = access.CanViewDraft;
            CanViewPublishedDocument = access.CanViewPublished;
            CanEditDocument = access.CanEditDraft;
            CanChangePublicFlag = access.CanChangeCommonFlag;

            var permissions = await UserConnector.GetCurrentUserPermissionsForEntityAsync(Entity.Id);
            CanDeleteDocument = access.IsAccessAdmin ||
                                permissions.CanAdminItems ||
                                permissions.CanAdminSpace ||
                                permissions.CanGlobalAdmin;
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

        private async Task HandleDeleteRequestedAsync()
        {
            if (Entity == null || !CanDeleteDocument)
            {
                return;
            }

            var entityId = Entity.Id;
            var entityName = Entity.Name;
            var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", $"Удалить документ \"{entityName}\"?");
            if (!confirmed)
            {
                return;
            }

            try
            {
                await TreeMiniAppService.DeleteEntitiesAsync(new[] { entityId });
                MessageBus.SendMessage(new EntityDeletedMessage(entityId));
                TreeSelectionService.RequestEntitySelection(null);
                NavigationManager.NavigateTo("/", replace: true);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
            }
        }
    }
}
