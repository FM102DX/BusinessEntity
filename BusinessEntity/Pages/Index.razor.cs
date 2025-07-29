using System.Collections.ObjectModel;
using BusinessEntity.Core.Contracts;
using BusinessEntity.Core.Classes;
using BusinessEntity.Core.Services;
using BusinessEntity.Models;
using DynamicData;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ReactiveUI;
using System.Security.Claims;
using BusinessEntity.Contracts;

namespace BusinessEntity.Pages
{    public partial class Index : ComponentBase
    {
        [Inject] public IApplicationSideAuthService AuthService { get; set; } = default!;
        [Inject] public NavigationManager Navigation { get; set; } = default!;
        [Inject] public ILogger<Index> Logger { get; set; } = default!;
        [Inject] public IPossibleEntityRelationTypesProvider RelationTypesProvider { get; set; } = default!;
        [Inject] public ISampleDataService SampleDataService { get; set; } = default!;
        [Inject] public BusinessEntity.Core.Services.BusinessEntityHelper BusinessEntityHelper { get; set; } = default!;
        [Inject] public BusinessEntity.Services.ITreeSelectionService TreeSelectionService { get; set; } = default!;
        
        private string? CurrentUserName { get; set; }
        private string? CurrentUserId { get; set; }
        private bool IsAuthenticated { get; set; }
        private IEnumerable<MacroRelationType> PossibleRelations { get; set; } = new List<MacroRelationType>();
        private IEnumerable<string> EntityTypeEnums { get; set; } = new List<string>();
        private IEnumerable<string> RelationTypeEnums { get; set; } = new List<string>();
        private List<BusinessEntity.Core.Classes.BusinessEntity> BusinessEntities { get; set; } = new List<BusinessEntity.Core.Classes.BusinessEntity>();
        
        // Состояние выбранных узлов дерева через сервис
        private bool IsMultiSelectActive => TreeSelectionService.IsMultiSelectActive;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                IsAuthenticated = await AuthService.IsUserAuthenticatedAsync();
                  if (IsAuthenticated)
                {
                    CurrentUserName = await AuthService.GetUserNameAsync();
                    
                    // Получаем ID пользователя из клеймов
                    var currentUser = await AuthService.GetCurrentUserAsync();
                    if (currentUser?.Identity != null)
                    {
                        CurrentUserId = currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                                       ?? currentUser.FindFirst("sub")?.Value;
                    }
                    
                    // Получаем возможные отношения между сущностями
                    PossibleRelations = RelationTypesProvider.GetPossibleRelations();
                    
                    // Получаем все возможные типы сущностей из enum
                    EntityTypeEnums = Enum.GetNames(typeof(BusinessEntityTypeEnum));
                    
                    // Получаем все возможные типы отношений из enum
                    RelationTypeEnums = Enum.GetNames(typeof(BusinessEntityRelationTypeEnum));
                    
                    // Инициализируем демо-данные
                    await SampleDataService.InitializeSampleDataAsync();
                    
                    // Загружаем созданные сущности
                    var entities = await BusinessEntityHelper.GetAllBusinessEntities();
                    BusinessEntities = entities.ToList();
                    
                    Logger.LogInformation($"User {CurrentUserName} accessed main page");
                }
                else
                {
                    Logger.LogInformation("Anonymous user accessed main page");
                }

                // Подписываемся на изменения выбора в дереве
                TreeSelectionService.SelectionChanged += OnTreeSelectionChanged;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading user information on main page");
            }
        }

        private void OnTreeSelectionChanged(List<TreeNodeItemViewModelBase> selectedNodes)
        {
            try
            {
                Logger.LogInformation($"Tree selection changed: {selectedNodes.Count} nodes selected");
                StateHasChanged(); // Обновляем UI при изменении выбора
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error handling tree selection change");
            }
        }

        private void RedirectToLogin()
        {
            try
            {
                var loginUrl = AuthService.GetLoginUrl("/");
                Logger.LogInformation($"Redirecting user to Authentic login url={loginUrl}");
                Navigation.NavigateTo(loginUrl, forceLoad: true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error redirecting to login");
            }
        }

        // Вспомогательные методы для работы с выбранными узлами
        private string GetSelectedNodesInfo()
        {
            return TreeSelectionService.GetSelectedNodesInfo();
        }
    }
}
